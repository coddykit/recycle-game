extends SceneTree
## Monkey run (not a gate test; minutes; headless or windowed). A real Main scene with a brand-new game (title card,
## intro and tutorial ON), then for MK_SECONDS real seconds random UI taps (registered targets, any visible button,
## any screen point), drags, panel toggles, language switches through the real Settings path (scene reload), graphics-tier
## switches, Android Back, save + restart ("Continue"), app pause / resume (also a 1 h absence), speed changes, cheats that
## unlock content (cash, regions, vehicles, depots, automation, garage), random events and prestige. Any engine or
## script error shows up in the log (`SCRIPT ERROR` / `ERROR:` lines); the tool itself prints counters and memory.
##   godot --headless --path . -s tests/tool_monkey.gd        (env MK_SEED, MK_SECONDS, MK_SPEED, MK_ACTS=lang,quality)
## Saves go to the isolated user://qa/tests/ directory (any run whose arguments contain "tests/").

var main: Node
var rng := RandomNumberGenerator.new()
var counts := {}
var only: PackedStringArray = OS.get_environment("MK_ACTS").split(",") if OS.get_environment("MK_ACTS") != "" else PackedStringArray()
var errors_seen := 0


# autoloads are not compile-time identifiers in a SceneTree script: bind them by name (same names as project.godot)
var UIManager: Node
var GameManager: Node
var EconomyManager: Node
var RegionManager: Node
var ProgressionManager: Node
var VehicleManager: Node
var ContractManager: Node
var EventManager: Node
var HeatManager: Node
var SaveManager: Node
var Data: Node


func _initialize() -> void:
	for n in ["UIManager", "GameManager", "EconomyManager", "RegionManager", "ProgressionManager", "VehicleManager", "ContractManager",
			"EventManager", "HeatManager", "SaveManager", "Data"]:
		set(n, root.get_node(n))
	_run()


func _name_of(a: int) -> String:
	if a < 30: return "tap_button"
	if a < 38: return "tap_target"
	if a < 46: return "tap_random"
	if a < 52: return "drag"
	if a < 60: return "panel"
	if a < 64: return "close"
	if a < 67: return "lang"
	if a < 71: return "quality"
	if a < 75: return "back"
	if a < 78: return "save"
	if a < 81: return "save+continue"
	if a < 85: return "pause/resume"
	if a < 88: return "speed"
	if a < 93: return "cheat"
	if a < 96: return "event"
	if a < 97: return "prestige"
	if a < 98: return "reset"
	return "resize"


func _mem() -> String:
	return "static %.1f MB, objects %d, nodes %d, orphans %d, resources %d" % [Performance.get_monitor(Performance.MEMORY_STATIC) / 1048576.0,
		int(Performance.get_monitor(Performance.OBJECT_COUNT)), int(Performance.get_monitor(Performance.OBJECT_NODE_COUNT)),
		int(Performance.get_monitor(Performance.OBJECT_ORPHAN_NODE_COUNT)), int(Performance.get_monitor(Performance.OBJECT_RESOURCE_COUNT))]


func _count(k: String) -> void:
	counts[k] = int(counts.get(k, 0)) + 1


func _tap(pos: Vector2, drag := Vector2.ZERO) -> void:
	for pressed in [true, false]:
		var e := InputEventMouseButton.new()
		e.button_index = MOUSE_BUTTON_LEFT
		e.pressed = pressed
		e.position = pos if pressed or drag == Vector2.ZERO else pos + drag
		e.global_position = e.position
		root.push_input(e, false)
		if pressed and drag != Vector2.ZERO:
			var m := InputEventMouseMotion.new()
			m.position = pos + drag
			m.global_position = m.position
			m.relative = drag
			m.button_mask = MOUSE_BUTTON_MASK_LEFT
			root.push_input(m, false)


func _hud() -> Node:
	return UIManager.hud if UIManager.hud != null and is_instance_valid(UIManager.hud) else null


func _visible_buttons() -> Array:
	var out: Array = []
	var h := _hud()
	if h == null:
		return out
	var stack: Array = [h]
	while not stack.is_empty():
		var n: Node = stack.pop_back()
		if n is CanvasItem and not (n as CanvasItem).visible:
			continue
		if n is Button and (n as Button).is_visible_in_tree() and not (n as Button).disabled:
			out.append(n)
		stack.append_array(n.get_children())
	return out


func _cheat() -> void:
	match rng.randi() % 9:
		0: EconomyManager.earn(rng.randf_range(1000.0, 2.0e6), "monkey", false)
		1: EconomyManager.add_reputation(rng.randi_range(10, 400))
		2:
			EconomyManager.earn(1e7, "monkey", false)
			EconomyManager.add_reputation(300)
			var r: int = RegionManager.next_locked()
			if r >= 0:
				RegionManager.unlock(r)
		3:
			EconomyManager.earn(1e7, "monkey", false)
			VehicleManager.buy(str(Data.vehicles[rng.randi() % Data.vehicles.size()].id))
		4:
			EconomyManager.earn(1e7, "monkey", false)
			EconomyManager.add_reputation(300)
			ProgressionManager.upgrade_garage()
		5:
			EconomyManager.earn(1e7, "monkey", false)
			ProgressionManager.buy_business(str(Data.business_upgrades[rng.randi() % Data.business_upgrades.size()].id))
		6:
			EconomyManager.earn(1e7, "monkey", false)
			RegionManager.build_depot(rng.randi() % maxi(1, RegionManager.unlocked_count))
		7:
			HeatManager.add_heat(rng.randi() % maxi(1, RegionManager.unlocked_count), rng.randf_range(5.0, 60.0))
		8:
			for i in 40:
				GameManager.tick(0.25)


func _start_main() -> void:
	main = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	root.add_child(main)
	current_scene = main


## Follows a scene reload (language switch / reset / prestige) or rebuilds Main after a simulated app restart.
func _follow_scene() -> bool:
	if current_scene != null and is_instance_valid(current_scene) and current_scene.is_inside_tree():
		if current_scene != main:
			main = current_scene
			_count("scene_reload")
		return true
	return false


func _run() -> void:
	rng.seed = int(OS.get_environment("MK_SEED")) if OS.get_environment("MK_SEED") != "" else 7
	var secs := float(OS.get_environment("MK_SECONDS")) if OS.get_environment("MK_SECONDS") != "" else 90.0
	GameManager.time_scale = float(OS.get_environment("MK_SPEED")) if OS.get_environment("MK_SPEED") != "" else 3.0
	await process_frame
	SaveManager.delete_save()
	_start_main()
	await process_frame
	var langs: Array = GameManager.LANGUAGES
	var panels: Array = ["contracts", "dispatch", "fleet", "map", "upgrades", "business", "settings", "shop", "daily"]
	var t_end := Time.get_ticks_msec() + int(secs * 1000.0)
	var step := 0
	for _i in 30:
		await process_frame
	print("[monkey] start ", _mem())
	while Time.get_ticks_msec() < t_end:
		step += 1
		var name := _name_of(rng.randi_range(0, 99))
		if only.size() > 0 and not only.has(name):
			await process_frame
			continue
		if not _follow_scene() or _hud() == null:
			await process_frame
			continue
		var h := _hud()
		var vs := Vector2(root.get_visible_rect().size)
		match name:
			"tap_button":
				var bs := _visible_buttons()
				if not bs.is_empty():
					var b: Button = bs[rng.randi() % bs.size()]
					# never press the destructive reset button (separate "reset" action) or Quit-like exits
					if not str(b.text).begins_with("Erase"):
						_tap(b.get_global_rect().get_center())
			"tap_target":
				var keys: Array = UIManager._targets.keys()
				if not keys.is_empty():
					var c: Control = UIManager.get_target(str(keys[rng.randi() % keys.size()]))
					if c != null:
						_tap(c.get_global_rect().get_center())
			"tap_random":
				_tap(Vector2(rng.randf() * vs.x, rng.randf() * vs.y))
			"drag":
				_tap(Vector2(rng.randf() * vs.x, rng.randf() * vs.y), Vector2(rng.randf_range(-250, 250), rng.randf_range(-250, 250)))
			"panel":
				var pn: String = panels[rng.randi() % panels.size()]
				var arg: Variant = null
				if pn == "dispatch":
					if ContractManager.offers.is_empty():
						continue
					arg = ContractManager.offers[rng.randi() % ContractManager.offers.size()].id
				elif pn == "fleet" and rng.randf() < 0.3 and not VehicleManager.vehicles.is_empty():
					arg = {"vehicle": VehicleManager.vehicles[0].id}
				elif pn == "map" and rng.randf() < 0.3:
					arg = rng.randi() % RegionManager.count()
				UIManager.open_panel(pn, arg)
			"close":
				UIManager.close_panel()
				if rng.randf() < 0.5:
					h._dismiss_top_modal()
			"lang":
				var code: String = langs[rng.randi() % langs.size()]
				if rng.randf() < 0.5:
					GameManager.set_setting("language", code)   # the real Settings path: save, reload the scene
				else:
					TranslationServer.set_locale(code)
			"quality":
				GameManager.set_setting("quality", ["low", "medium", "high"][rng.randi() % 3])
			"back":
				# Back with nothing open saves and quits the app by design: only send it when something can absorb it
				if h._modal_open or h.current_sheet != null:
					h.notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
			"save":
				GameManager.save_now()
			"save+continue":
				if GameManager.running:
					GameManager.save_now()
					# a cold start: the old Main goes away, a new one boots and loads the save ("Continue")
					main.queue_free()
					await process_frame
					await process_frame
					_start_main()
					_count("cold_start")
			"pause/resume":
				root.propagate_notification(Node.NOTIFICATION_APPLICATION_PAUSED)
				await create_timer(0.1).timeout
				if rng.randf() < 0.4:
					GameManager._paused_at -= 3600.0   # an hour in the background
				root.propagate_notification(Node.NOTIFICATION_APPLICATION_RESUMED)
			"speed":
				GameManager.time_scale = [1.0, 3.0, 8.0][rng.randi() % 3]
			"cheat":
				if GameManager.running:
					_cheat()
			"event":
				for j in ContractManager.jobs:
					if j.phase == "out" and not j.get("pending_event", false):
						j.event_id = str(Data.events[rng.randi() % Data.events.size()].id)
						EventManager.trigger(j)
						break
			"prestige":
				if ProgressionManager.prestige_available() and GameManager.running:
					GameManager.prestige()
			"reset":
				if rng.randf() < 0.25 and GameManager.running:
					GameManager.reset_progress()
			"resize":
				var sizes := [Vector2i(540, 960), Vector2i(720, 1280), Vector2i(540, 1170), Vector2i(1280, 720), Vector2i(1024, 1366)]
				root.size = sizes[rng.randi() % sizes.size()]
		_count(name)
		if step % 400 == 0:
			print("[monkey] step ", step, " ", _mem(), " regions ", RegionManager.unlocked_count)
		if step % 4 == 0:
			await process_frame
		else:
			await create_timer(0.02).timeout
	GameManager.time_scale = 1.0
	TranslationServer.set_locale("en")
	for _i in 60:
		await process_frame
	print("[monkey] end   ", _mem())
	print("[monkey] done ", step, " steps ", JSON.stringify(counts), " cash ", EconomyManager.cash, " regions ", RegionManager.unlocked_count,
		" vehicles ", VehicleManager.vehicles.size(), " stage ", ProgressionManager.stage)
	SaveManager.delete_save()
	quit(0)
