extends Node
## Touch audit 2026-10-27: ONE phone tap = ONE action. Android delivers every touch twice — the InputEventScreenTouch and
## an emulated left click (device DEVICE_ID_EMULATION) — and this project also turns every desktop click into an emulated
## touch (emulate_touch_from_mouse). The HUD's Heat chip and goal chip accepted both halves, so open_panel() ran twice
## and the sheet opened and shut again in the same tap. Each tap here goes through the real viewport in Android's order
## (emulated click first, then the touch) and must leave exactly that sheet open; the desktop order (real click + the
## emulated touch) too.
## Run: godot --headless --path . res://tests/phone_tap_once_test.tscn
var failures := 0
var checks := 0


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		print("PHONETAP FAIL ", label)
	else:
		print("PHONETAP ok ", label)


func _frames(n := 6) -> void:
	for i in n:
		await get_tree().process_frame


func _settle(seconds := 0.5) -> void:
	await get_tree().create_timer(seconds, true, false, true).timeout
	await get_tree().process_frame


func _ready() -> void:
	get_tree().create_timer(120.0).timeout.connect(func(): get_tree().quit(2))
	await get_tree().process_frame
	get_tree().root.size = Vector2i(540, 960)
	await get_tree().process_frame
	SaveManager.delete_save()
	GameManager.settings["language"] = "en"
	GameManager.settings["quality_notice"] = ""
	GameManager.apply_language()
	GameManager.new_game()
	var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	await _frames(6)
	var hud: Node = main.hud
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
	await _settle(0.4)
	hud._modal_queue.clear()
	for m in hud.modal_layer.get_children():
		m.queue_free()
	hud._modal_open = false
	await _frames(3)

	await _chip(hud, hud.heat_chip, "map", "Heat chip", true)
	await _chip(hud, hud.goal_chip, "advice", "goal chip", true)
	await _chip(hud, hud.heat_chip, "map", "Heat chip (desktop click)", false)

	main.queue_free()
	await _frames(4)
	SaveManager.delete_save()
	print("PHONETAP checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)


## One tap on `chip`: `phone` = emulated click + touch (Android), else real click + emulated touch (desktop here).
func _chip(hud: Node, chip: Control, sheet: String, label: String, phone: bool) -> void:
	if hud._modal_open:
		hud._dismiss_top_modal()
		await _settle(0.4)
	if hud.current_sheet != null and is_instance_valid(hud.current_sheet):
		hud.close_panel()
		await _settle(0.4)
	check(chip != null and chip.is_visible_in_tree(), label + ": shown")
	if chip == null or not chip.is_visible_in_tree():
		return
	var at: Vector2 = chip.get_global_rect().get_center()
	var vp := get_tree().root
	for down in [true, false]:
		if phone:
			vp.push_input(_mouse(at, down, InputEvent.DEVICE_ID_EMULATION), true)
			vp.push_input(_touch(at, down, 0), true)
		else:
			vp.push_input(_mouse(at, down, 0), true)
			vp.push_input(_touch(at, down, InputEvent.DEVICE_ID_EMULATION), true)
		await _frames(2)
	await _settle(0.5)
	if sheet == "advice":
		check(hud._modal_open and hud.modal_layer.get_child_count() == 1 and hud._modal_queue.is_empty(),
				label + ": one tap opens exactly one advice modal")
		return
	var s: Node = hud.current_sheet
	var open: bool = s != null and is_instance_valid(s) and str(s.sheet_name) == sheet and not bool(s._closing)
	check(open, "%s: one tap leaves the %s sheet open (got %s)" % [label, sheet, str(s.sheet_name) if s != null and is_instance_valid(s) else "none"])


func _touch(pos: Vector2, pressed: bool, device: int) -> InputEventScreenTouch:
	var t := InputEventScreenTouch.new()
	t.device = device
	t.index = 0
	t.position = pos
	t.pressed = pressed
	return t


func _mouse(pos: Vector2, pressed: bool, device: int) -> InputEventMouseButton:
	var m := InputEventMouseButton.new()
	m.device = device
	m.button_index = MOUSE_BUTTON_LEFT
	m.pressed = pressed
	m.position = pos
	m.global_position = pos
	if pressed:
		m.button_mask = MOUSE_BUTTON_MASK_LEFT
	return m
