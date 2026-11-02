extends Node
## Crash hunt (W63 item 13, after Corrupt Tycoon's release SIGSEGV): a release build does not check a method call's
## object, so a callback that touches a freed node ("previously freed instance", "lambda capture ... was freed") is a
## use-after-free there. Smuggler frees its whole HUD and World on every scene reload (language, reset, prestige) while
## the autoloads (UIManager, Economy, Contracts, Vehicles ...) keep emitting. This plays a seeded random walk over the
## real buttons of the HUD, the open sheets and the modals (pressed.emit()), with panels opened / closed, language
## switches (scene reload), prestige, reset, Back, app pause / resume, save + cold restart ("Continue"), cheats and
## game time passing, and fails on any script error or "freed" message the engine logs.
## Run: godot --headless --path . res://tests/monkey_test.tscn      (env MK_SEED, MK_STEPS to explore)
## Saves go to the isolated user://qa/tests/ directory (the scene path contains "tests/").

class Catch extends Logger:
	var seen: Array = []

	func _log_error(function: String, file: String, line: int, code: String, rationale: String, _editor_notify: bool,
			error_type: int, script_backtraces: Array[ScriptBacktrace]) -> void:
		if error_type != ERROR_TYPE_SCRIPT and not file.ends_with(".gd") and not (code + rationale).contains("freed"):
			return
		var bt := ""
		for b in script_backtraces:
			bt += b.format(2, 2)
		seen.append("%s:%d %s - %s\n%s" % [file, line, function, rationale, bt])

	func _log_message(_message: String, _error: bool) -> void:
		pass


var main: Node
var rng := RandomNumberGenerator.new()
var checks := 0
var failures := 0
var counts := {}
## never leave the app or spend: links, purchases, ads, the destructive Erase button (reset is its own action)
const DENY := ["erase", "privacy", "terms", "rate", "restore", "buy", "support", "feedback", "watch", "http", "mail"]


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		print("MONKEY FAIL ", label)
	else:
		print("MONKEY ok ", label)


func _count(k: String) -> void:
	counts[k] = int(counts.get(k, 0)) + 1


func _hud() -> Node:
	return UIManager.hud


func _start_main() -> void:
	main = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	get_tree().current_scene = main


## Follows a scene reload (language switch / reset / prestige).
func _follow_scene() -> bool:
	var cur := get_tree().current_scene
	if cur != null and is_instance_valid(cur) and cur.is_inside_tree():
		if cur != main:
			main = cur
			_count("scene_reload")
		return true
	return false


func _buttons(n: Node, acc: Array) -> void:
	if n is CanvasItem and not (n as CanvasItem).visible:
		return
	if n is BaseButton and (n as Control).is_visible_in_tree() and not (n as BaseButton).disabled:
		acc.append(n)
	for c in n.get_children():
		_buttons(c, acc)


func _allowed(b: BaseButton) -> bool:
	var t := ""
	if b is Button:
		t = (b as Button).text.to_lower()
	for d in DENY:
		if t.contains(d):
			return false
	return true


func _cheat() -> void:
	match rng.randi() % 8:
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
			HeatManager.add_heat(rng.randi() % maxi(1, RegionManager.unlocked_count), rng.randf_range(5.0, 60.0))
		7:
			for i in 40:
				GameManager.tick(0.25)


func _ready() -> void:
	get_tree().create_timer(280.0).timeout.connect(func(): get_tree().quit(2))
	rng.seed = int(OS.get_environment("MK_SEED")) if OS.get_environment("MK_SEED") != "" else 20261021
	var steps := int(OS.get_environment("MK_STEPS")) if OS.get_environment("MK_STEPS") != "" else 500
	await get_tree().process_frame
	get_tree().root.size = Vector2i(540, 960)
	await get_tree().process_frame
	var catch := Catch.new()
	OS.add_logger(catch)
	SaveManager.delete_save()
	GameManager.settings["language"] = "en"
	GameManager.settings["quality_notice"] = ""
	GameManager.apply_language()
	GameManager.time_scale = 3.0
	_start_main()
	for i in 30:
		await get_tree().process_frame
	var langs: Array = GameManager.LANGUAGES
	var panels: Array = ["contracts", "dispatch", "fleet", "map", "upgrades", "business", "settings", "shop", "daily"]
	var presses := 0
	for step in steps:
		if not _follow_scene() or _hud() == null:
			await get_tree().process_frame
			continue
		var h := _hud()
		var r := rng.randf()
		if r < 0.55:
			var acc: Array = []
			_buttons(h, acc)
			acc = acc.filter(_allowed)
			if not acc.is_empty():
				var b: BaseButton = acc[rng.randi() % acc.size()]
				if b is OptionButton:
					var ob := b as OptionButton
					if ob.item_count > 0:
						var idx := rng.randi() % ob.item_count
						ob.select(idx)
						ob.item_selected.emit(idx)
				elif b is CheckBox or b is CheckButton:
					b.button_pressed = not b.button_pressed
				else:
					b.pressed.emit()
				presses += 1
		elif r < 0.68:
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
			_count("panel")
		elif r < 0.74:
			UIManager.close_panel()
			if rng.randf() < 0.5:
				h._dismiss_top_modal()
			_count("close")
		elif r < 0.80:
			# the real Settings path: save, then reload the whole scene; or just the engine locale
			var code: String = langs[rng.randi() % langs.size()]
			if rng.randf() < 0.6:
				GameManager.set_setting("language", code)
			else:
				TranslationServer.set_locale(code)
			_count("lang")
		elif r < 0.83:
			GameManager.set_setting("quality", ["low", "medium", "high"][rng.randi() % 3])
			_count("quality")
		elif r < 0.86:
			if h._modal_open or h.current_sheet != null:  # Back with nothing open quits the app by design
				h.notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
			_count("back")
		elif r < 0.89:
			get_tree().root.propagate_notification(Node.NOTIFICATION_APPLICATION_PAUSED)
			await get_tree().create_timer(0.1, true, false, true).timeout
			if rng.randf() < 0.4:
				GameManager._paused_at -= 3600.0
			get_tree().root.propagate_notification(Node.NOTIFICATION_APPLICATION_RESUMED)
			_count("pause_resume")
		elif r < 0.91:
			if GameManager.running:
				GameManager.save_now()
				main.queue_free()  # a cold start: the old Main goes away, a new one boots and loads the save
				await get_tree().process_frame
				await get_tree().process_frame
				_start_main()
				_count("cold_start")
		elif r < 0.95:
			if GameManager.running:
				_cheat()
				_count("cheat")
		elif r < 0.97:
			for j in ContractManager.jobs:
				if j.phase == "out" and not j.get("pending_event", false):
					j.event_id = str(Data.events[rng.randi() % Data.events.size()].id)
					EventManager.trigger(j)
					break
			_count("event")
		elif r < 0.985:
			if ProgressionManager.prestige_available() and GameManager.running:
				GameManager.prestige()
				_count("prestige")
		else:
			if GameManager.running and rng.randf() < 0.5:
				GameManager.reset_progress()
				_count("reset")
		# game time passes between presses (frames + a real pause now and then for tweens, timers and reloads)
		for k in 3:
			await get_tree().process_frame
		if step % 10 == 9:
			await get_tree().create_timer(0.3, true, false, true).timeout
	for i in 40:
		await get_tree().process_frame
	GameManager.time_scale = 1.0
	TranslationServer.set_locale("en")
	OS.remove_logger(catch)
	print("MONKEY steps=", steps, " presses=", presses, " counts=", JSON.stringify(counts))
	check(presses > steps / 5, "the walk pressed buttons (%d)" % presses)
	for s in catch.seen.slice(0, 12):
		print("MONKEY SCRIPT ERROR ", s)
	check(catch.seen.size() == 0, "no script error or freed-instance message during the walk (%d)" % catch.seen.size())
	var cur := get_tree().current_scene
	if cur != null and is_instance_valid(cur):
		cur.queue_free()
	await get_tree().process_frame
	await get_tree().process_frame
	SaveManager.delete_save()
	print("MONKEY checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)
