extends Node
var failures := 0
var checks := 0
const Validator = preload("res://scripts/core/save_validator.gd")
func check(ok: bool, label: String):
	checks += 1
	if not ok: failures += 1
	print("QUALITY ", "PASS " if ok else "FAIL ", label)
func _ready():
	await get_tree().process_frame
	Iap._test_setup(IapGate.empty_state(), true, true) # a phone that bought nothing: the 2 h offline cap, no pass multiplier
	SaveManager.delete_save()
	_offline()
	_saves()
	_daily()
	_advisor_and_feedback()
	print("QUALITY checks=", checks, " failures=", failures)
	SaveManager.delete_save()
	await get_tree().create_timer(0.2).timeout
	get_tree().quit(1 if failures else 0)
func fresh():
	SaveManager.delete_save()
	GameManager.new_game()
func _offline():
	fresh()
	var repair = VehicleManager.add_vehicle("small_van", true)
	repair.state = "repair"
	repair.repair_left = 1000.0
	ContractManager.dispatch(ContractManager.offers[0], VehicleManager.vehicles[0], "normal")
	GameManager._apply_offline(60.0, {})
	check(is_equal_approx(repair.repair_left, 940.0), "offline repair single clock")
	check(ContractManager.jobs.is_empty(), "saved trip completes without replacement dispatch")
	check(not GameManager.pending_return().is_empty(), "return receipt persisted before presentation")
	var stored := SaveManager.load_data()
	check(stored.session.receipt.applied, "durable receipt and wallet same save")
	var earned := EconomyManager.cash
	GameManager._apply_offline(60, {"time": stored.time - 60, "economy": stored.economy})
	check(is_equal_approx(EconomyManager.cash, earned), "duplicate offline interval cannot settle twice")
	GameManager.acknowledge_return(str(GameManager.pending_return().id))
	check(GameManager.pending_return().is_empty(), "acknowledgement consumes report")
	SaveManager.apply(SaveManager.load_data())
	check(GameManager.pending_return().is_empty(), "acknowledgement survives reload")
	fresh()
	var v = VehicleManager.vehicles[0]
	v.state = "repair"
	v.repair_left = 10000.0
	GameManager._apply_offline(999999, {})
	check(is_equal_approx(v.repair_left, 2800), "absence capped at two hours")
	fresh()
	var cash := EconomyManager.cash
	ContractManager.dispatch(ContractManager.offers[0], VehicleManager.vehicles[0], "normal")
	SaveManager.fail_next_write = true
	GameManager._apply_offline(60, {})
	check(EconomyManager.cash == cash and not ContractManager.jobs.is_empty(), "failed offline commit rolls back simulation and wallet")
	check(GameManager.pending_return().is_empty(), "failed settlement has no success receipt")
	var before := SaveManager.snapshot()
	GameManager._apply_offline(-60, {})
	check(EconomyManager.cash == float(before.economy.cash), "clock rollback awards nothing")
	fresh()
	ProgressionManager.business["automated_dispatch"] = 4
	var r = VehicleManager.add_vehicle("small_van", true)
	r.state = "repair"
	r.repair_left = 1000
	ContractManager.dispatch(ContractManager.offers[0], VehicleManager.vehicles[0], "normal")
	GameManager._apply_offline(120, {"economy":{"ipm":60.0}})
	check(ContractManager.jobs.is_empty(), "offline automation never creates new trips")
	check(float(GameManager.session.receipt.manager) <= 60, "manager dividend bounded by non-overlapping interval")
func _saves():
	fresh()
	ProgressionManager.connections["lawyer"] = 1
	check(SaveManager.save_game(), "valid snapshot saves")
	SaveManager.apply(SaveManager.load_data())
	check(ProgressionManager.conn("lawyer") == 1, "Connections survive reload")
	var good := SaveManager.snapshot()
	var bad := good.duplicate(true)
	bad.vehicles.vehicles[0].upgrades = []
	check(not Validator.valid(bad), "invalid nested upgrade type rejected")
	check(not SaveManager.apply(bad), "invalid snapshot never mutates managers")
	bad = good.duplicate(true)
	bad.vehicles.vehicles[0].erase("id")
	check(not Validator.valid(bad), "missing vehicle ID rejected")
	bad = good.duplicate(true)
	bad.vehicles.vehicles.append(bad.vehicles.vehicles[0].duplicate(true))
	check(not Validator.valid(bad), "duplicate vehicle ID rejected")
	bad = good.duplicate(true)
	bad.vehicles.vehicles[0].driver = "missing"
	check(not Validator.valid(bad), "dangling driver rejected")
	bad = good.duplicate(true)
	bad.economy.cash = INF
	check(not Validator.valid(bad), "non-finite economy rejected")
	bad = good.duplicate(true)
	bad.heat.heat = "invalid"
	check(not Validator.valid(bad), "invalid heat rejected")
	check(SaveManager.save_game(), "backup created")
	var backup := FileAccess.get_file_as_string(SaveManager.BACKUP_PATH)
	bad = good.duplicate(true)
	bad.vehicles.vehicles[0].upgrades = []
	SaveManager.atomic_json(SaveManager.SAVE_PATH, bad)
	check(not SaveManager.load_data().is_empty(), "nested corruption falls back to backup")
	SaveManager.save_game()
	check(FileAccess.get_file_as_string(SaveManager.BACKUP_PATH) == backup, "nested corruption cannot overwrite valid backup")
	var legacy := good.duplicate(true)
	legacy.version = 1
	legacy.erase("session")
	legacy.erase("daily")
	SaveManager.atomic_json(SaveManager.SAVE_PATH, legacy)
	var migrated := SaveManager.load_data()
	check(migrated.version == 2 and migrated.has("session"), "v1 migration supplies receipt schema")
	check(float(migrated.economy.cash) == float(legacy.economy.cash), "migration preserves cash")
	var future := good.duplicate(true)
	future.version = 99
	SaveManager.atomic_json(SaveManager.SAVE_PATH, future)
	var original := FileAccess.get_file_as_string(SaveManager.SAVE_PATH)
	check(SaveManager.load_data().is_empty() and SaveManager.write_protected, "future save enters read-only mode")
	check(not SaveManager.save_game(), "future save cannot be overwritten")
	check(FileAccess.get_file_as_string(SaveManager.SAVE_PATH) == original, "future file preserved byte for byte")
	SaveManager.delete_save()
	fresh()
	var settings := GameManager.settings.duplicate(true)
	SaveManager.fail_next_write = true
	GameManager.set_setting("language", "ja")
	check(GameManager.settings == settings, "failed settings commit leaves current language unchanged")
func _daily():
	fresh()
	check(DailyManager.ensure_day(), "daily plan commits")
	var plan := DailyManager.to_dict()
	check(plan.tasks.size() == 3, "three tasks selected")
	SaveManager.apply(SaveManager.load_data())
	check(JSON.stringify(DailyManager.to_dict()) == JSON.stringify(plan), "daily selection and quotes survive reload")
	DailyManager.ensure_day(int(plan.day) - 1)
	check(JSON.stringify(DailyManager.to_dict()) == JSON.stringify(plan), "clock rollback cannot reroll tasks")
	var task: Dictionary = DailyManager.state.tasks[0]
	EconomyManager.add_stat(task.metric, float(task.target))
	var id := str(task.id)
	var day := int(DailyManager.state.day)
	var cash := EconomyManager.cash
	var reward := float(task.reward)
	SaveManager.fail_next_write = true
	check(not DailyManager.claim(id, day), "storage failure refuses daily reward")
	check(EconomyManager.cash == cash, "failed daily reward rolls back wallet")
	check(not DailyManager.state.tasks[0].claimed, "failed daily reward rolls back claim marker")
	check(DailyManager.claim(id, day), "eligible daily reward commits")
	check(EconomyManager.cash == cash + reward, "frozen quoted reward credited")
	check(not DailyManager.claim(id, day), "duplicate claim rejected")
	SaveManager.apply(SaveManager.load_data())
	check(not DailyManager.claim(id, day), "duplicate claim after reload rejected")
	check(not DailyManager.claim(id, day - 1), "stale day request rejected")
	var persisted := DailyManager.to_dict()
	GameManager.new_game(true)
	check(DailyManager.to_dict() == persisted, "prestige reset preserves daily state")
	var bad := SaveManager.snapshot()
	bad.daily.tasks[0].reward = 999999
	check(not Validator.valid(bad), "invalid reward schema rejected")
func _advisor_and_feedback():
	fresh()
	VehicleManager.vehicles[0].driver = ""
	check(TutorialManager.next_action().blocker == "driver", "advisor identifies unassigned driver")
	fresh()
	EconomyManager.cash = 0
	check(TutorialManager.next_action().panel == "contracts", "advisor points cash-starved player to contracts")
	fresh()
	EconomyManager.cash = 1e8
	EconomyManager.reputation = 100
	check(TutorialManager.next_action().panel == "upgrades", "advisor identifies affordable garage gate")
	AudioManager.suspend_audio(true)
	AudioManager.play("cash")
	check(AudioManager.suspended, "feedback suspended in background")
	for player in AudioManager._pool:
		check(not player.playing, "background SFX stopped")
	AudioManager.suspend_audio(false)
	check(not AudioManager.suspended, "feedback resumes")
	GameManager.settings.reduced_motion = true
	check(UIKit.reduced_motion(), "reduced-motion preference routed")
	GameManager.settings.reduced_motion = false
	TranslationServer.set_locale("tr")
	check(not EventManager._describe({"damage":10}).contains("Vehicle damaged"), "dynamic event translated")
