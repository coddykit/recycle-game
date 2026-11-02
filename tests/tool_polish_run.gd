extends Node
## Polish 10-27 (W66) new-player run, real screens (needs a GPU window, not headless; run with a throw-away HOME):
##   POLISH_OUT=/abs/dir POLISH_LANG=en godot --path . --windowed --resolution 540x960 --audio-driver Dummy res://tests/tool_polish_run.tscn
## Fresh install → title → intro → every tutorial step played like a player (open the highlighted thing, pick the job,
## Dispatch; game clock x6 while waiting, jobs taken as they come; no cash is granted) with one shot per step and one
## with the window the step opens. Then a mid-game moment: an event window on a job on the road while a new offer arrives.
const BootScript := preload("res://scripts/boot.gd")
var out := ""
var lang := "en"
var hud: Node
var _n := 0


func _ready() -> void:
	out = OS.get_environment("POLISH_OUT") if OS.get_environment("POLISH_OUT") != "" else "user://polish"
	lang = OS.get_environment("POLISH_LANG") if OS.get_environment("POLISH_LANG") != "" else "en"
	DirAccess.make_dir_recursive_absolute(out)
	await _run()
	get_tree().quit()


func _frames(n: int) -> void:
	for i in n:
		await get_tree().process_frame


func _shot(name: String, wait := 0.6) -> void:
	await get_tree().create_timer(wait).timeout
	for i in 3:
		RenderingServer.force_draw()
	_n += 1
	get_viewport().get_texture().get_image().save_png("%s/%02d_%s.png" % [out, _n, name])
	print("SHOT ", name, " step=", TutorialManager.step, " t=", int(EconomyManager.stat("play_time")), " cash=", int(EconomyManager.cash))


func _first_offer() -> Dictionary:
	return ContractManager.offers[0] if ContractManager.offers.size() > 0 else {}


func _take_jobs() -> void:
	for c in ContractManager.offers.duplicate():
		var v := ContractManager.best_vehicle(c)
		if not v.is_empty():
			ContractManager.dispatch(c, v, "normal")
	for j in EventManager.pending_jobs():
		EventManager.resolve(j, EventManager.auto_choice(j), true)


## Real-time wait at x6 until `cond` or `limit` seconds; while waiting the player takes the jobs that come in.
func _wait(cond: Callable, limit: float, take := true) -> void:
	GameManager.time_scale = 6.0
	var t := 0.0
	while not cond.call() and t < limit:
		await get_tree().create_timer(0.25).timeout
		t += 0.25
		if take and int(t * 4) % 8 == 0:
			_take_jobs()
	GameManager.time_scale = 1.0


func _run() -> void:
	SaveManager.delete_save()
	GameManager.settings["language"] = lang
	GameManager.settings["quality_notice"] = ""
	GameManager.apply_language()
	GameManager.new_game()
	await _frames(3)
	var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	await _frames(10)
	hud = main.hud
	await _shot("title", 1.5)
	if is_instance_valid(hud.title_card):
		hud.title_card.dismiss()
	await get_tree().create_timer(1.0).timeout
	var page := 0
	while is_instance_valid(hud.onboarding) and page < 12:
		page += 1
		if hud.onboarding._typing:
			hud.onboarding._typing.kill()
		hud.onboarding._text.visible_ratio = 1.0
		await _shot("intro%d" % page, 0.5)
		if not is_instance_valid(hud.onboarding):
			break
		if hud.onboarding._name_box.visible:
			hud.onboarding._confirm_name()
		else:
			hud.onboarding._next()
		await _frames(4)
	await get_tree().create_timer(1.0).timeout
	await _shot("after_intro", 1.0)
	# close whatever the game shows after the intro (graphics notice) like a player: the window's first button
	var guard := 0
	while hud._modal_open and guard < 4:
		guard += 1
		for m in hud.modal_layer.get_children():
			m.queue_free()
		hud._modal_open = false
		await _frames(4)
	var seen := {}
	guard = 0
	while not TutorialManager.finished and guard < 40:
		guard += 1
		var s := TutorialManager.current()
		var id := str(s.get("id", ""))
		if not TutorialManager.is_active():
			await _wait(func(): return TutorialManager.is_active() or TutorialManager.current().get("id", "") != id, 90.0, id != "again")
			continue
		var first := not seen.has(id)
		seen[id] = true
		if first:
			await _shot("tut_" + id, 0.8)
		match id:
			"contracts":
				hud.open_panel("contracts")
				await _shot("tut_contracts_open", 0.8)
			"accept":
				var o := _first_offer()
				if not o.is_empty():
					hud.open_panel("dispatch", o.id)
			"dispatch":
				await _frames(10)
				var o2: Dictionary = hud.current_sheet.contract if hud.current_sheet != null and "contract" in hud.current_sheet else _first_offer()
				var v := ContractManager.best_vehicle(o2) if not o2.is_empty() else {}
				if not v.is_empty():
					ContractManager.dispatch(o2, v, "normal")
				hud.close_panel()
			"watch":
				await _wait(func(): return EconomyManager.stat("deliveries") > 0, 120.0, false)
			"cash":
				await _wait(func(): return TutorialManager.current().get("id", "") != "cash", 10.0, false)
			"upgrade":
				hud.open_panel("fleet")
				await _shot("tut_upgrade_fleet", 0.8)
				await _wait(func(): return ContractManager.jobs.is_empty(), 60.0, false)   # the car is back: "again" shows
				VehicleManager.upgrade(VehicleManager.vehicles[0], "engine")
				hud.close_panel()
			"again":
				hud.open_panel("contracts")
				await _shot("tut_again_contracts", 0.8)
				var o3 := _first_offer()
				if not o3.is_empty():
					hud.open_panel("dispatch", o3.id)
					await _shot("tut_again_dispatch", 1.0)
					var v3 := ContractManager.best_vehicle(o3)
					if not v3.is_empty():
						ContractManager.dispatch(o3, v3, "normal")
				hud.close_panel()
				await _wait(func(): return TutorialManager.current().get("id", "") != "again", 60.0)
			"garage":
				hud.open_panel("upgrades")
				await _shot("tut_garage_upgrades", 0.8)
				ProgressionManager.upgrade_garage()
				hud.close_panel()
			"region":
				hud.open_panel("map")
				await _shot("tut_region_map", 1.0)
				await _wait(func(): return TutorialManager.current().get("id", "") != "region", 20.0, false)
				hud.close_panel()
			"done":
				await _wait(func(): return TutorialManager.finished, 15.0, false)
		await _frames(6)
	await _shot("tutorial_done", 1.0)
	print("POLISH tutorial finished=", TutorialManager.finished, " step=", TutorialManager.step, " play_time=", int(EconomyManager.stat("play_time")))
	# ten-minute mark: keep playing like a player until ~10 game minutes, then the screens a player opens
	await _wait(func(): return EconomyManager.stat("play_time") > 600.0, 150.0)
	await _shot("ten_min_hud", 1.0)
	for p in ["contracts", "fleet", "map", "upgrades", "business"]:
		if ProgressionManager.tab_unlocked(p):
			hud.open_panel(p)
			await _shot("ten_min_" + p, 0.9)
			hud.close_panel()
			await _frames(20)
	# an expired offer opened from a stale card
	hud.open_panel("dispatch", "no-such-offer")
	await _shot("expired_offer", 0.8)
	hud.close_panel()
	await _frames(20)
	# event window while a new offer arrives
	_take_jobs()
	await _frames(5)
	if ContractManager.jobs.size() > 0:
		var job: Dictionary = ContractManager.jobs[0]
		if str(job.get("event_id", "")) == "":
			job["event_id"] = EventManager.pick_event(job)
		EventManager.trigger(job)
		await _frames(10)
		hud._offer_toast_cd = 0.0
		ContractManager.refresh_offers()
		await _shot("event_with_offer", 1.0)
	main.queue_free()
	await _frames(5)
