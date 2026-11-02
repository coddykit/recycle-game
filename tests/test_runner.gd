extends Node
## Headless gameplay test + economy pacing report.
## Run: godot --headless res://tests/test_runner.tscn
## A simple "player bot" plays the core loop with game logic only (no rendering)
## and the test asserts the loop, progression, save/load and save recovery.

var failures := 0
var checks := 0
var milestones := {}
var sim_time := 0.0


func _ready() -> void:
	await get_tree().process_frame
	seed(int(OS.get_environment("SIM_SEED")) if OS.has_environment("SIM_SEED") else 12345)
	SaveManager.delete_save()
	_phone()
	Monetization.last_reward = 0
	GameManager.new_game()
	check(SaveManager.qa_mode and SaveManager.SAVE_PATH.begins_with("user://qa/"), "tests cannot overwrite player saves")
	if OS.get_environment("PACING") == "1":
		_pacing()
		return
	_test_models()
	_test_audio_assets()
	_test_first_delivery()
	_test_bot_session(float(OS.get_environment("SIM_MINUTES") if OS.get_environment("SIM_MINUTES") != "" else "45") * 60.0)
	_test_prestige()
	_test_save_roundtrip()
	_test_corrupt_save()
	_test_offline_progress()
	_test_background_pause()
	_test_localization()
	_test_monetization()
	_test_contracts_refresh_button()
	check(not Analytics._ready_ok, "QA never initializes Analytics")
	check(not Analytics._ready_ok and Analytics._fa == null, "QA does not initialize or send native analytics")
	print("")
	print("=== MILESTONES (simulated play time) ===")
	var keys := milestones.keys()
	keys.sort_custom(func(a, b): return milestones[a] < milestones[b])
	for k in keys:
		print("  %-34s %s" % [k, Fmt.time(milestones[k])])
	print("")
	print("Checks: %d  Failures: %d" % [checks, failures])
	print("RESULT: %s" % ("PASS" if failures == 0 else "FAIL"))
	SaveManager.delete_save()
	get_tree().quit(0 if failures == 0 else 1)


func check(cond: bool, what: String) -> void:
	checks += 1
	if not cond:
		failures += 1
		print("  FAIL: " + what)
	else:
		print("  ok:   " + what)


func sim(seconds: float, bot := false) -> void:
	var t := 0.0
	var bot_t := 0.0
	while t < seconds:
		GameManager.tick(0.1)
		t += 0.1
		sim_time += 0.1
		bot_t += 0.1
		if bot and bot_t >= 1.0:
			bot_t = 0.0
			_bot_step()
		_track()


func _test_audio_assets() -> void:
	print("== audio ==")
	for k in AudioManager.SFX:
		check(AudioManager._streams.has(k), "sfx loaded: %s (%s)" % [k, AudioManager._streams.get(k).get_class() if AudioManager._streams.has(k) else "-"])
	check(AudioManager._tracks.has("music_main") and AudioManager._tracks.has("music_empire"), "both music tracks load")
	for lang in ["en", "tr"]:
		for key in ["delivered", "mentor_hello", "tut_contracts", "tab_fleet"]:
			check(ResourceLoader.exists("res://assets/audio/voice/%s/%s.mp3" % [lang, key]), "voice %s/%s" % [lang, key])


func _test_first_delivery() -> void:
	print("== first delivery ==")
	check(EconomyManager.cash == 250.0, "starts with $250")
	check(VehicleManager.vehicles.size() == 1, "starts with one vehicle")
	check(VehicleManager.drivers.size() == 1, "starts with one driver")
	check(ContractManager.offers.size() == 1 and ContractManager.offers[0].tutorial, "tutorial contract waiting")
	var c: Dictionary = ContractManager.offers[0]
	var v: Dictionary = VehicleManager.vehicles[0]
	var e := ContractManager.estimate(c, v, "normal")
	check(float(e.risk) == 0.0, "tutorial contract is risk free")
	var job := ContractManager.dispatch(c, v, "normal")
	check(not job.is_empty(), "dispatch creates a job")
	check(v.state == "busy", "vehicle is busy while driving")
	check(ContractManager.offers.is_empty(), "contract left the offer board")
	sim(float(job.total) + 1.0)
	check(EconomyManager.stat("deliveries") == 1, "delivery completed")
	check(EconomyManager.cash > 250.0 + 200.0, "earned money (cash %s)" % Fmt.money(EconomyManager.cash))
	check(EconomyManager.reputation > 0, "gained reputation")
	sim(45.0)
	check(v.state == "idle", "vehicle returned to base")
	check(ProgressionManager.goal_index >= 1, "first goal completed")
	check(ContractManager.offers.size() > 0, "new offers arrive after the tutorial")
	check(VehicleManager.upgrade(v, "engine"), "can afford the first engine upgrade")
	check(VehicleManager.speed_mult(v) > float(Data.vehicle_def("hatchback").speed), "engine upgrade raised speed")


## Very simple player: take the best jobs, handle events, spend money sensibly.
func _bot_step() -> void:
	for j in EventManager.pending_jobs():
		EventManager.resolve(j, EventManager.auto_choice(j), true)
	var offers := ContractManager.offers.duplicate()
	offers.append_array(ContractManager.queue)
	offers.sort_custom(func(a, b): return float(a.reward) > float(b.reward))
	for c in offers:
		var v := ContractManager.best_vehicle(c)
		if v.is_empty():
			continue
		var route := "long" if HeatManager.get_heat(RegionManager.region_of_node(c.dest)) > 60 else "normal"
		ContractManager.dispatch(c, v, route)
	# The pacing player follows the first-delivery tutorial before investing.
	if OS.get_environment("PACING") == "1" and EconomyManager.stat("deliveries") == 0: return
	# spending priorities
	if ProgressionManager.garage_check().ok:
		ProgressionManager.upgrade_garage()
	var nxt := RegionManager.next_locked()
	if nxt >= 0 and RegionManager.unlock_check(nxt).ok:
		RegionManager.unlock(nxt)
	if VehicleManager.vehicles.size() < ProgressionManager.vehicle_slots():
		var best := ""
		for d in Data.vehicles:
			if VehicleManager.buy_check(d.id).ok and EconomyManager.cash > float(d.price) * 1.3:
				best = d.id
		if best != "":
			VehicleManager.buy(best)
	for b in ["automated_dispatch", "fleet_capacity", "contract_office", "warehouse", "dispatch_center",
			"operations_room", "regional_network", "maintenance_bay", "driver_lounge", "advanced_logistics"]:
		if ProgressionManager.business_check(b).ok and EconomyManager.cash > ProgressionManager.business_cost(b) * 2.0:
			ProgressionManager.buy_business(b)
	for cn in Data.connections:
		if ProgressionManager.connection_check(cn.id).ok:
			ProgressionManager.buy_connection(cn.id)
	for i in RegionManager.unlocked_count:
		if RegionManager.depot_check(i).ok and EconomyManager.cash > RegionManager.depot_cost(i) * 2.5:
			RegionManager.build_depot(i)
	for v in VehicleManager.vehicles:
		for u in ["engine", "capacity", "reliability", "navigation", "handling"]:
			if VehicleManager.upgrade_check(v, u).ok and EconomyManager.cash > VehicleManager.upgrade_cost(v, u) * 3.0:
				VehicleManager.upgrade(v, u)


var max_heat := 0.0
var heat_samples := 0.0
var heat_sum := 0.0


func _track() -> void:
	for i in RegionManager.unlocked_count:
		max_heat = maxf(max_heat, HeatManager.get_heat(i))
	heat_sum += HeatManager.get_heat(0)
	heat_samples += 1.0
	_mark(ProgressionManager.garage_level >= 2, "garage L2 (Local Depot)")
	_mark(ProgressionManager.garage_level >= 3, "garage L3 (Regional Warehouse)")
	_mark(ProgressionManager.garage_level >= 4, "garage L4 (Logistics Center)")
	_mark(ProgressionManager.garage_level >= 5, "garage L5 (Underground HQ)")
	_mark(VehicleManager.vehicles.size() >= 2, "2nd vehicle")
	_mark(VehicleManager.vehicles.size() >= 4, "4 vehicles")
	_mark(VehicleManager.vehicles.size() >= 8, "8 vehicles")
	for i in range(1, RegionManager.count()):
		_mark(RegionManager.unlocked_count > i, "region: " + RegionManager.region_name(i))
	_mark(ProgressionManager.automation_level() >= 1, "Auto Accept")
	_mark(ProgressionManager.automation_level() >= 3, "Auto Dispatch")
	_mark(RegionManager.depots.size() >= 1, "first depot")
	for s in range(1, Data.stages.size()):
		_mark(ProgressionManager.stage >= s, "stage %d %s" % [s + 1, Data.stages[s].name])


func _mark(cond: bool, key: String) -> void:
	if cond and not milestones.has(key):
		milestones[key] = sim_time


func _test_bot_session(seconds: float) -> void:
	print("== bot session (%s simulated) ==" % Fmt.time(seconds))
	var start_cash := EconomyManager.cash
	sim(seconds, true)
	print("  cash %s  rep %d  deliveries %d  failed %d  events %d  ipm %s" % [Fmt.money(EconomyManager.cash),
			EconomyManager.reputation, int(EconomyManager.stat("deliveries")), int(EconomyManager.stat("failed")),
			int(EconomyManager.stat("events")), Fmt.money(EconomyManager.income_per_minute())])
	print("  heat: max %.0f, avg in Dust Valley %.1f" % [max_heat, heat_sum / maxf(1.0, heat_samples)])
	print("  garage L%d  vehicles %d  regions %d  stage %d  goal #%d" % [ProgressionManager.garage_level,
			VehicleManager.vehicles.size(), RegionManager.unlocked_count, ProgressionManager.stage + 1, ProgressionManager.goal_index])
	check(EconomyManager.stat("deliveries") > 40, "bot completed many deliveries")
	check(ProgressionManager.garage_level >= 2, "garage was upgraded")
	check(VehicleManager.vehicles.size() >= 2, "fleet expanded")
	check(RegionManager.unlocked_count >= 2, "second region unlocked")
	check(EconomyManager.stat("events") > 0, "random events happened")
	check(EconomyManager.stat("earned") > start_cash, "economy is producing income")
	var max_h := 0.0
	for h in HeatManager.heat:
		max_h = maxf(max_h, float(h))
	check(max_h <= 100.0, "heat stays within 0-100")
	check(EconomyManager.stat("influence_earned") > 10, "influence was earned (%d)" % int(EconomyManager.stat("influence_earned")))
	var owned := 0
	for k in ProgressionManager.connections:
		owned += int(ProgressionManager.connections[k])
	check(owned > 0, "connections were bought (%d levels)" % owned)


func _test_prestige() -> void:
	print("== prestige ==")
	while not ProgressionManager.prestige_available():
		sim(120.0, true)
		if sim_time > 6 * 3600:
			break
	check(ProgressionManager.prestige_available(), "prestige becomes available (%s)" % Fmt.time(sim_time))
	var conn_before := ProgressionManager.connections.duplicate()
	var deliveries := EconomyManager.stat("deliveries")
	var stars := ProgressionManager.apply_prestige()
	check(stars >= 1, "earned %d legacy stars" % stars)
	check(ProgressionManager.legacy() == stars, "legacy stored")
	check(RegionManager.unlocked_count == 1, "regions reset")
	check(ProgressionManager.connections == conn_before, "connections kept")
	check(EconomyManager.stat("deliveries") == deliveries, "lifetime stats kept")
	check(ProgressionManager.legacy_mult() > 1.0, "legacy boosts income")
	check(VehicleManager.vehicles.size() >= 2, "veteran head start vehicles")
	sim(300.0, true)
	check(EconomyManager.stat("deliveries") > deliveries, "new run plays normally")


func _test_save_roundtrip() -> void:
	print("== save / load ==")
	check(SaveManager.save_game(), "save succeeds")
	var cash := EconomyManager.cash
	var vehicles := VehicleManager.vehicles.size()
	var regions := RegionManager.unlocked_count
	var garage := ProgressionManager.garage_level
	var ups := VehicleManager.total_upgrades(VehicleManager.vehicles[0])
	var jobs := ContractManager.jobs.size()
	var d := SaveManager.load_data()
	check(not d.is_empty(), "save file loads")
	GameManager.new_game()
	check(VehicleManager.vehicles.size() == 1, "new game resets state")
	SaveManager.apply(d)
	check(is_equal_approx(EconomyManager.cash, cash), "cash restored")
	check(VehicleManager.vehicles.size() == vehicles, "vehicles restored")
	check(RegionManager.unlocked_count == regions, "regions restored")
	check(ProgressionManager.garage_level == garage, "garage restored")
	check(VehicleManager.total_upgrades(VehicleManager.vehicles[0]) == ups, "vehicle upgrades restored")
	check(ContractManager.jobs.size() == jobs, "in-flight jobs restored")
	sim(60.0, true)
	check(true, "game keeps running after load")


func _test_corrupt_save() -> void:
	print("== corrupted save recovery ==")
	SaveManager.save_game()
	var cash := EconomyManager.cash
	SaveManager.save_game()  # second save rotates the first into the backup
	var f := FileAccess.open(SaveManager.SAVE_PATH, FileAccess.WRITE)
	f.store_string("{ this is not json ]]")
	f.close()
	var d := SaveManager.load_data()
	check(not d.is_empty(), "falls back to backup save")
	check(SaveManager.apply(d), "backup applies")
	check(absf(EconomyManager.cash - cash) < 1.0, "backup has the right cash")
	var backup_text := FileAccess.get_file_as_string(SaveManager.BACKUP_PATH)
	check(SaveManager.save_game(), "can save after recovery")
	check(FileAccess.get_file_as_string(SaveManager.BACKUP_PATH) == backup_text, "saving after corruption preserves valid backup")
	f = FileAccess.open(SaveManager.SAVE_PATH, FileAccess.WRITE)
	f.store_string("{}")
	f.close()
	var f2 := FileAccess.open(SaveManager.BACKUP_PATH, FileAccess.WRITE)
	f2.store_string("garbage")
	f2.close()
	var events := []
	var on_bad := func(code: String, recovered: bool): events.append([code, recovered])
	SaveManager.save_unreadable.connect(on_bad)
	check(SaveManager.load_data().is_empty(), "fully corrupt saves are rejected (new game)")
	check(events.size() == 1 and not events[0][1], "save_unreadable sent, not recovered (both files damaged)")
	# §14 / LESSONS B1: the new game's first save must not write over the damaged primary — it is kept aside
	var damaged_text := FileAccess.get_file_as_string(SaveManager.SAVE_PATH)
	for i in 8:
		var old_copy := SaveManager.SAVE_PATH + ".unreadable" + ("" if i == 0 else "." + str(i))
		if FileAccess.file_exists(old_copy): DirAccess.remove_absolute(ProjectSettings.globalize_path(old_copy))
	check(SaveManager.save_game(), "new game can save after a fully damaged save")
	check(FileAccess.get_file_as_string(SaveManager.SAVE_PATH + ".unreadable") == damaged_text, "damaged primary kept as .unreadable, not overwritten")
	check(FileAccess.get_file_as_string(SaveManager.BACKUP_PATH) == "garbage", "damaged backup left untouched")
	# a second damage rotates, nothing is lost
	f = FileAccess.open(SaveManager.SAVE_PATH, FileAccess.WRITE)
	f.store_string("second damage")
	f.close()
	check(SaveManager.save_game(), "saves again after a second damage")
	check(FileAccess.get_file_as_string(SaveManager.SAVE_PATH + ".unreadable") == "second damage" and FileAccess.get_file_as_string(SaveManager.SAVE_PATH + ".unreadable.1") == damaged_text, "older unreadable copy rotated, not deleted")
	# primary damaged, backup good → recovered = true
	SaveManager.save_game()
	f = FileAccess.open(SaveManager.SAVE_PATH, FileAccess.WRITE)
	f.store_string("{ broken")
	f.close()
	events.clear()
	check(not SaveManager.load_data().is_empty() and events.size() == 1 and events[0][1], "save_unreadable sent with recovered=1 when the backup loads")
	SaveManager.save_unreadable.disconnect(on_bad)
	var partial := {"economy": {"cash": 999}, "vehicles": {"vehicles": "broken"}}
	check(not SaveManager.apply(partial), "malformed nested save rejected before mutation")
	check(VehicleManager.vehicles.size() >= 1, "player always keeps a vehicle")


func _test_offline_progress() -> void:
	print("== offline progress ==")
	GameManager.new_game()
	var c: Dictionary = ContractManager.offers[0]
	ContractManager.dispatch(c, VehicleManager.vehicles[0], "normal")
	SaveManager.save_game()
	var d := SaveManager.load_data()
	d.time = float(d.time) - 600.0
	d.session.settled_at = d.time
	SaveManager.apply(d)
	GameManager._apply_offline(600.0, d)
	check(ContractManager.jobs.is_empty(), "trips finish while away")
	check(EconomyManager.stat("deliveries") >= 1, "offline delivery counted")


func _test_background_pause() -> void:
	print("== background progress ==")
	GameManager.new_game()
	GameManager.running = true
	GameManager._notification(NOTIFICATION_APPLICATION_FOCUS_OUT)
	var before := EconomyManager.stat("play_time")
	GameManager._process(0.1)
	check(EconomyManager.stat("play_time") == before, "background frame does not also tick live progress")
	GameManager._notification(NOTIFICATION_APPLICATION_FOCUS_IN)
	GameManager._process(0.1)
	check(EconomyManager.stat("play_time") > before, "foreground resumes simulation")
	GameManager.running = false


func _test_localization() -> void:
	print("== locales and mobile controls ==")
	UIKit.configure("dark")
	UIKit.theme()
	var file := FileAccess.open("res://translations/strings.csv", FileAccess.READ)
	var header := file.get_csv_line()
	var rows: Array = []
	while not file.eof_reached():
		var row := file.get_csv_line()
		if row.size() == header.size(): rows.append(row)
	for lang in GameManager.LANGUAGES:
		GameManager.settings.language = lang
		GameManager.apply_language()
		var resource := load("res://translations/strings.%s.translation" % lang) as Translation
		check(resource != null, "locale loads: " + lang)
		var missing := []
		var glyphs := []
		var column := header.find(lang)
		for row in rows:
			if resource == null or resource.get_message(row[0]).is_empty(): missing.append(row[0])
			for i in str(row[column]).length():
				var character := str(row[column]).unicode_at(i)
				if character > 32 and not UIKit.font_semi.has_char(character) and character not in glyphs:
					glyphs.append(character)
		# E48: the game upper-cases text (UIKit.upper: business sheet "SELL FOR n STARS", the shop's group heads / offer label);
		# every string in upper case must be drawable by both bundled fonts too (Turkish İ Ş Ğ, Cyrillic, accents).
		var upper_corpus := UIKit.upper(IapText.t("offer"))
		for group in IapCatalog.groups():
			upper_corpus += UIKit.upper(IapText.group_title(str(group.get("id", ""))))
		for row in rows:
			upper_corpus += UIKit.upper(str(row[column]))
		var upper_glyphs := ""
		for ch in upper_corpus:
			var cp := ch.unicode_at(0)
			if cp <= 32 or cp == 0xA0 or cp == 0x202F or upper_glyphs.contains(ch):
				continue
			if not UIKit.font_semi.has_char(cp) or not UIKit.font_bold.has_char(cp):
				upper_glyphs += ch
		check(upper_glyphs == "", "%s: every upper-cased string is drawable by the bundled fonts (%s)" % [lang, upper_glyphs])
		check(missing.is_empty(), "%s: all %d strings available (%d missing)" % [lang, rows.size(), missing.size()])
		check(glyphs.is_empty(), "%s: bundled fonts cover every character (%s)" % [lang, str(glyphs)])
		var button := UIKit.button("Continue", "BtnPrimary", "", Vector2(32, 32))
		check(button.custom_minimum_size.x >= 88 and button.custom_minimum_size.y >= 88, lang + ": touch target floor")
		button.free()
	# S1 #170: the connection level-up toast names the connection in the player's language (was the raw data name)
	GameManager.settings.language = "tr"
	GameManager.apply_language()
	var conn_toast: String = load("res://scripts/ui/panels/business_sheet.gd").conn_toast(Data.connection("inside_contact"), 2)
	check(conn_toast.contains(tr("Inside Contact")) and not conn_toast.contains("Inside Contact"), "connection toast localized: " + conn_toast)
	GameManager.settings.language = "unsupported"
	check(GameManager.language() == "en", "unsupported locale falls back to English")
	GameManager.settings.language = "en"
	GameManager.apply_language()


func _test_models() -> void:
	print("== Blender model inventory ==")
	var inventory: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://data/model_meta.json"))
	for model in inventory:
		check(Models.scene(model) != null, "Blender GLB loads: " + str(model))


## Behave like a phone build on the fake store (no purchases): the shop gates are live, nothing is owned.
func _phone(owned := []) -> void:
	var st := IapGate.empty_state()
	for slug in owned:
		IapGate.record(st, str(slug), "")
	Iap._test_setup(st, true, true)


func _test_monetization() -> void:
	print("== monetization integrity ==")
	var pid := "com.coddykit.borderlineempire.empire_pass"
	_phone()
	Iap._on_rc_purchase_result({"cancelled": true})
	check(not Iap.owns("empire_pass"), "cancelled purchase grants no rights")
	Iap._on_rc_purchase_result({"error": "network"})
	check(not Iap.owns("empire_pass"), "network failure grants no rights")
	Iap._on_rc_entitlement("different_product", true)
	check(not Iap.owns("empire_pass") and not Iap.purchased_any(), "unrelated entitlement cannot unlock the pass")
	Iap._on_rc_purchase_result({"product_id": pid, "transaction_id": "t-1"})
	check(Iap.owns("empire_pass") and is_equal_approx(Monetization.delivery_multiplier(), 1.25), "verified purchase unlocks the exact advertised multiplier")
	GameManager.new_game()
	check(Iap.owns("empire_pass"), "progress reset preserves purchase")
	Iap._on_rc_entitlement("empire_pass", false)
	check(Iap.owns("empire_pass"), "negative cached response cannot erase verified ownership")
	_phone()
	Monetization.ad_log = {}
	Monetization.last_reward = 0
	Monetization._pending_reward = "proof-token"
	Monetization._reward_amount = 150
	var before := EconomyManager.cash
	Monetization._on_reward_earned("wrong-token")
	check(EconomyManager.cash == before, "unmatched reward callback grants nothing")
	Monetization._on_reward_earned("proof-token")
	check(EconomyManager.cash == before + 150, "earned reward grants once")
	Monetization._on_reward_earned("proof-token")
	check(EconomyManager.cash == before + 150, "duplicate earned callback cannot double grant")
	check(Monetization.cooldown_left() > 590, "reward starts ten-minute cooldown")
	Monetization.claim_sponsor()
	check(EconomyManager.cash == before + 150, "cooldown blocks another claim")
	Monetization.last_reward = 0
	Monetization._pending_reward = "cancelled-ad"
	Monetization._ads.finished.emit()
	Monetization._on_reward_earned("cancelled-ad")
	check(EconomyManager.cash == before + 150, "dismissed ad without completion grants nothing")
	Monetization._persist()
	_phone()


func _test_contracts_refresh_button() -> void:
	print("== contracts header ==")
	var cs: Sheet = load("res://scripts/ui/panels/contracts_sheet.gd").new()
	cs.header_extra = HBoxContainer.new()
	var body := VBoxContainer.new()
	cs.build(body)
	var rb: Button = cs.header_extra.get_child(0)
	var font := rb.get_theme_font("font")
	var text_w := font.get_string_size(rb.text, HORIZONTAL_ALIGNMENT_LEFT, -1, rb.get_theme_font_size("font_size")).x
	check(rb.autowrap_mode == TextServer.AUTOWRAP_OFF and rb.get_combined_minimum_size().x >= text_w + 36,
			"contracts refresh price stays on one line (%s)" % rb.text)
	cs.header_extra.free()
	body.free()
	cs.free()


# Test-only opportunity probe: repeatable board refresh / rearranging the queue are
# deliberately excluded. They spend money or move cards without progressing play.
func _pacing_can_act() -> bool:
	if not EventManager.pending_jobs().is_empty(): return true
	var available := ContractManager.offers + ContractManager.queue
	for c in available:
		var v := ContractManager.best_vehicle(c)
		if not v.is_empty() and ContractManager.dispatch_check(c, v) == "": return true
	if EconomyManager.stat("deliveries") == 0: return false
	if ProgressionManager.garage_check().ok: return true
	var nxt := RegionManager.next_locked()
	if nxt >= 0 and RegionManager.unlock_check(nxt).ok: return true
	for d in Data.vehicles:
		if VehicleManager.buy_check(d.id).ok: return true
	for d in Data.business_upgrades:
		if ProgressionManager.business_check(d.id).ok: return true
	for d in Data.connections:
		if ProgressionManager.connection_check(d.id).ok: return true
	for i in RegionManager.unlocked_count:
		if RegionManager.depot_check(i).ok: return true
	for v in VehicleManager.vehicles:
		for u in Data.vehicle_upgrades:
			if VehicleManager.upgrade_check(v, u.id).ok: return true
	return false


func _pacing() -> void:
	# Daily feedback varies pitch with the global RNG behind a wall-clock cooldown.
	# Mute only this measurement fixture so host load cannot change the economy seed.
	AudioManager.suspend_audio(true)
	ProgressionManager.flags["intro_done"] = true
	var reward_policy := OS.get_environment("DAILY_REWARDS") == "1"
	if reward_policy: DailyManager.ensure_day()
	var idle := 0.0
	var starved := 0.0
	var daily_total := 0.0
	var blocked_run := 0
	var max_blocked_15 := 0
	var max_blocked_30 := 0
	var blocked_seconds := 0
	var action_gap := 0
	var max_action_gap := 0
	var max_action_gap_15 := 0
	var trips: Array = []
	var routes := {}
	ContractManager.job_started.connect(func(j):
		if sim_time <= 1800:
			trips.append({"at":sim_time, "outbound_s":j.total, "route":j.contract.origin + ":" + j.contract.dest})
			routes[j.contract.origin + ":" + j.contract.dest] = true
			_mark(routes.size() >= 2, "first_new_route"))
	ContractManager.job_returned.connect(func(j):
		if sim_time <= 1800:
			for trip in trips:
				if not trip.has("return_s") and trip.route == j.contract.origin + ":" + j.contract.dest:
					trip.return_s = j.ret_total
					break)
	var first_30 := {}
	for second in 3600:
		# Integer loop avoids sim(1.0)'s float accumulation running eleven ticks.
		for substep in 10:
			GameManager.tick(0.1)
			sim_time = second + (substep + 1) / 10.0
			_track()
		var before := JSON.stringify([EconomyManager.stats, ProgressionManager.to_dict(), VehicleManager.to_dict()]) if second < 1800 else ""
		var can_act := _pacing_can_act() if second < 1800 else true
		_bot_step()
		_mark(EconomyManager.stat("deliveries") > 0, "first_delivery")
		_mark(EconomyManager.stat("vehicle_upgrades") > 0, "first_upgrade")
		_track()
		if ContractManager.jobs.is_empty(): idle += 1
		if EconomyManager.cash < 25: starved += 1
		if reward_policy:
			for task in DailyManager.state.get("tasks", []):
				if not task.claimed and DailyManager.progress(task) >= float(task.target):
					can_act = true
					if DailyManager.claim(str(task.id), int(DailyManager.state.day)): daily_total += float(task.reward)
		if second < 1800:
			blocked_run = 0 if can_act else blocked_run + 1
			if not can_act: blocked_seconds += 1
			max_blocked_30 = maxi(max_blocked_30, blocked_run)
			if second < 900: max_blocked_15 = maxi(max_blocked_15, blocked_run)
			var acted := before != JSON.stringify([EconomyManager.stats, ProgressionManager.to_dict(), VehicleManager.to_dict()])
			action_gap = 0 if acted else action_gap + 1
			max_action_gap = maxi(max_action_gap, action_gap)
			if second < 900: max_action_gap_15 = maxi(max_action_gap_15, action_gap)
		if second == 1799:
			first_30 = {"earned":EconomyManager.stat("earned"), "deliveries":EconomyManager.stat("deliveries"), "regions":RegionManager.unlocked_count, "vehicles":VehicleManager.vehicles.size()}
	var report := {"seed":OS.get_environment("SIM_SEED"), "daily_rewards":reward_policy, "seconds":3600,
		"milestones":milestones, "idle_seconds":idle, "cash_starved_seconds":starved,
		"max_forced_idle_15_s":max_blocked_15, "max_forced_idle_30_s":max_blocked_30,
		"forced_idle_30_s":blocked_seconds, "max_bot_action_gap_30_s":max_action_gap,
		"max_bot_action_gap_15_s":max_action_gap_15, "first_30":first_30, "trips_30":trips,
		"daily_grants":daily_total, "earned":EconomyManager.stat("earned"), "failed_jobs":EconomyManager.stat("failed")}
	print("PACING_JSON:", JSON.stringify(report))
	SaveManager.delete_save()
	get_tree().quit()
