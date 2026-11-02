extends Node
const TabClock = preload("res://addons/coddy_ui/tests/tab_latency_clock.gd")  # #121/#125: pass/fail = frames, ms = report
## #113: tap a bottom tab through the device input path (ScreenTouch + the emulated mouse copy, pushed through the viewport
## like Android) and time input -> sheet open -> first row of THAT sheet drawn. `-- --phone` = a phone-sized Low run;
## `--limit N` = the red line in ms (default 100; the phone check uses 100).
## Run: godot --headless --path . res://tests/tab_open_probe.tscn [-- --phone --limit 100]
var failures := 0
var limit_ms := 100.0
var hud: Node


func _tap(pos: Vector2, pressed: bool) -> void:
	var t := InputEventScreenTouch.new()
	t.index = 0
	t.position = pos
	t.pressed = pressed
	get_viewport().push_input(t, true)
	var m := InputEventMouseButton.new()
	m.device = InputEvent.DEVICE_ID_EMULATION
	m.button_index = MOUSE_BUTTON_LEFT
	m.pressed = pressed
	m.position = pos
	if pressed:
		m.button_mask = MOUSE_BUTTON_MASK_LEFT
	get_viewport().push_input(m, true)


func _first_row(s: Node) -> Control:
	if s == null or not is_instance_valid(s) or s.body == null:
		return null
	for c in s.body.get_children():
		if c is Control and (c as Control).visible and (c as Control).size.y > 1.0:
			return c
	return null


func _measure(key: String, supplied: Button = null) -> Array:
	var b: Button = supplied if supplied != null else hud.nav_buttons[key].button
	var parent: Node = b.get_parent()
	while parent != null:
		if parent is ScrollContainer:
			parent.ensure_control_visible(b)
		parent = parent.get_parent()
	for i in 4:
		await get_tree().process_frame
	var old_rows: Array = hud.current_sheet.body.get_children() if is_instance_valid(hud.current_sheet) else []
	var at := b.get_global_rect().get_center()
	_tap(at, true)
	await get_tree().process_frame
	await get_tree().process_frame
	var t0 := Time.get_ticks_usec()
	var frame_start := TabClock.begin()
	_tap(at, false)
	var vis := -1.0
	var row := -1.0
	while Time.get_ticks_usec() - t0 < 6000000:
		if DisplayServer.get_name() == "headless":
			await get_tree().process_frame
		else:
			await RenderingServer.frame_post_draw
		var dt := (Time.get_ticks_usec() - t0) / 1000.0
		var s: Node = hud.current_sheet
		if vis < 0.0 and s != null and is_instance_valid(s) and (s.sheet_name == key or (key.begins_with("fleet_") and s.sheet_name == "fleet" and s.tab == key.trim_prefix("fleet_"))) and s.is_inside_tree():
			vis = dt
		if vis >= 0.0 and _first_row(s) != null and not old_rows.has(_first_row(s)):
			row = dt
			_frame_check(str(key), frame_start)
			break
	return [vis, row]


func _ready() -> void:
	get_tree().create_timer(200.0).timeout.connect(func(): get_tree().quit(2))
	var args := OS.get_cmdline_user_args()
	var phone := args.has("--phone")
	if args.has("--limit"):
		limit_ms = float(args[args.find("--limit") + 1])
	Input.emulate_mouse_from_touch = true
	await get_tree().process_frame
	get_tree().root.size = Vector2i(390, 844) if phone else Vector2i(540, 960)
	await get_tree().process_frame
	SaveManager.delete_save()
	GameManager.settings["language"] = "en"
	GameManager.settings["quality_notice"] = ""
	GameManager.settings["quality"] = 0 if phone else 1
	GameManager.apply_language()
	GameManager.new_game()
	var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	for i in 6:
		await get_tree().process_frame
	hud = main.hud
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	ProgressionManager.flags["intro_done"] = true
	ProgressionManager.flags["tabs_all"] = true
	TutorialManager.skip()
	hud._restore_game_chrome()
	hud._modal_queue.clear()
	hud._offer_toast_cd = 1e12
	EconomyManager.earn(5e8, "test", false)
	for i in 3:
		ContractManager.refresh_offers()
	await get_tree().create_timer(0.6, true, false, true).timeout
	hud._modal_queue.clear()
	for mm in hud.modal_layer.get_children():
		mm.queue_free()
	hud._modal_open = false
	for i in 3:
		await get_tree().process_frame
	var rows := []
	for key in ["contracts", "fleet", "map", "upgrades", "business"]:
		var r: Array = await _measure(key)
		rows.append([key, r[0], r[1]])
		await get_tree().create_timer(0.35).timeout
		if key == "fleet":
			for sub in ["Showroom", "Drivers", "Vehicles"]:
				var button: Button
				for child in hud.current_sheet.tabs_row.find_children("*", "Button", true, false):
					if child.text == sub or str(child.get_meta("tab", "")) == sub:
						button = child
				var result: Array = await _measure("fleet_" + sub, button)
				rows.append(["fleet_" + sub, result[0], result[1]])
				await get_tree().create_timer(0.1).timeout
		if key == "business":
			var result: Array = await _measure("daily", hud.current_sheet.body.get_child(1))
			rows.append(["daily", result[0], result[1]])
		if key == "contracts":
			var result: Array = await _measure("dispatch", UIManager.get_target("offer_accept_first"))
			rows.append(["dispatch", result[0], result[1]])
		UIManager.close_panel()
		await get_tree().create_timer(0.4, true, false, true).timeout
	var window_result: Array = await _measure("settings", hud.find_child("SettingsButton", true, false))
	rows.append(["settings", window_result[0], window_result[1]])
	await get_tree().create_timer(0.35).timeout
	window_result = await _measure("shop", hud.current_sheet.find_child("settings_shop", true, false))
	rows.append(["shop", window_result[0], window_result[1]])
	UIManager.close_panel()
	await get_tree().create_timer(0.35).timeout
	await _toasts()
	var worst := 0.0
	for r in rows:
		print("TAB_OPEN %s %s visible_ms=%.0f first_row_ms=%.0f" % ["phone" if phone else "mac", r[0], r[1], r[2]])
		var v: float = r[2] if r[2] >= 0.0 else 99999.0
		worst = maxf(worst, v)
		if v == 99999.0:   # never showed (ms is a report only; frames gate it)
			failures += 1
			print("FAIL TAB_OPEN %s %.0f ms > %.0f ms" % [r[0], v, limit_ms])
	print("TAB_OPEN_PROBE: worst=%.0f ms limit=%.0f ms, %d failures" % [worst, limit_ms, failures])
	get_tree().quit(0 if failures == 0 else 1)


func _toasts() -> void:
	for locale in ["hi", "ja", "ru"]:
		GameManager.settings["language"] = locale
		GameManager.apply_language()
		hud._update_goal()
		for sz in [Vector2i(720, 1280), Vector2i(1280, 720)]:
			get_tree().root.size = sz
			for i in 8:
				await get_tree().process_frame
			hud.toast("QA सूचना · お知らせ · Уведомление", Color.WHITE, "info")
			for i in 8:
				await get_tree().process_frame
			var card: Control = hud.ui_toast.card()
			var header: Control = hud.goal_chip.get_parent().get_parent().get_parent()
			var ok: bool = card.visible and not card.get_global_rect().intersects(header.get_global_rect())
			print("TOAST_LAYOUT %s actual=%s %s %s" % [locale, GameManager.language(), sz, "PASS" if ok else "FAIL"])
			if not ok:
				failures += 1
			if locale != GameManager.language():
				print("TOAST_LOCALE_UNAVAILABLE %s fallback=%s" % [locale, GameManager.language()])

## #121/#125: content must be there by the 2nd drawn frame after the release (headless: process frames). Milliseconds
## stay in the report only — host load on the Jenkins agent stretched them into false failures (Pharmacy 1.1.4 #9).
func _frame_check(key: String, stamp: Vector2i) -> void:
	var frames := TabClock.elapsed(stamp)
	print("TAB_FRAMES %s frames=%d basis=%s" % [key, frames, TabClock.basis()])
	if frames > 2:
		failures += 1
		print("FAIL TAB_FRAMES %s %d > 2" % [key, frames])
