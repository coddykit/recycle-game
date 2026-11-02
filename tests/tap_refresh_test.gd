extends Node
## W63 (W62 Airport lesson): a tap on a sheet button must survive the sheet's live rebuilds. Late game: cash keeps
## coming in (cash_changed → Upgrades / Map rebuild), vehicles keep returning (vehicle_changed → Contracts rebuild),
## the Daily sheet refreshes on its own every 5 s. Each tap: press (emulated mouse + touch, as Android sends) → a few
## frames with rebuild triggers → release, through the real viewport. Every tap must reach its button.
## Before the fix every rebuild under the finger freed the pressed button and the tap was lost.
## Run: godot --headless --path . res://tests/tap_refresh_test.tscn
var failures := 0
var checks := 0
var _presses := 0


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		print("TAPREFRESH FAIL ", label)
	else:
		print("TAPREFRESH ok ", label)


func _frames(n := 6) -> void:
	for i in n:
		await get_tree().process_frame


func _settle(seconds := 0.5) -> void:
	await get_tree().create_timer(seconds, true, false, true).timeout
	await get_tree().process_frame


func _ready() -> void:
	get_tree().create_timer(200.0).timeout.connect(func(): get_tree().quit(2))
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
	EconomyManager.earn(5e8, "test", false)
	EconomyManager.add_reputation(900)
	for r in range(1, 5):
		RegionManager.unlock(r)
	for i in 4:
		ProgressionManager.upgrade_garage()
	for i in 8:
		if VehicleManager.buy(["small_van", "armored"][i % 2]).is_empty():
			break
	for i in 3:
		ContractManager.refresh_offers()
	await _settle(0.6)
	hud._modal_queue.clear()
	for m in hud.modal_layer.get_children():
		m.queue_free()
	hud._modal_open = false
	await _frames(3)

	# Upgrades: business buy buttons while cash keeps rising (cash_changed → rebuild every frame of the hold)
	await _scenario(hud, "upgrades", "ActionPill", func(): EconomyManager.earn(1000.0, "test", false), 8)
	# Contracts: Accept while vehicles keep reporting (vehicle_changed → rebuild)
	await _scenario(hud, "contracts", "", func():
		if not VehicleManager.vehicles.is_empty():
			VehicleManager.vehicle_changed.emit(VehicleManager.vehicles[0]), 6)
	# Map: any enabled button while cash rises (2026-10-26 rows: the enabled pills are the depot builds → depot permits)
	for i in 3:
		ProgressionManager.buy_business("regional_network")
	await _scenario(hud, "map", "", func(): EconomyManager.earn(1000.0, "test", false), 4)

	# a lost release (app went to background mid-press) never freezes the sheet
	UIManager.open_panel("upgrades", null)
	await _settle(0.5)
	var sheet: Node = hud.current_sheet
	var vp := get_tree().root
	vp.push_input(_mouse(Vector2(270, 700), true), true)
	EconomyManager.earn(1000.0, "test", false)
	await _frames(2)
	sheet._notification(NOTIFICATION_APPLICATION_FOCUS_OUT)
	var before: Node = sheet.body.get_child(0) if sheet.body.get_child_count() > 0 else null
	EconomyManager.earn(1000.0, "test", false)
	await _frames(3)
	var after: Node = sheet.body.get_child(0) if sheet.body.get_child_count() > 0 else null
	check(before != after, "after focus-out the deferred rebuild runs (no frozen sheet)")
	vp.push_input(_mouse(Vector2(270, 700), false), true)
	UIManager.close_panel()
	await _settle(0.4)

	main.queue_free()
	await _frames(4)
	SaveManager.delete_save()
	print("TAPREFRESH checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)


func _scenario(hud: Node, panel: String, style: String, poke: Callable, want: int) -> void:
	UIManager.open_panel(panel, null)
	await _settle(0.55)
	var sheet: Node = hud.current_sheet
	var taps := 0
	var hits := 0
	for trial in want:
		var b := _pick(sheet, style)
		if b == null:
			break
		_presses = 0
		b.pressed.connect(_on_press)
		var at: Vector2 = b.get_global_rect().get_center()
		var vp := get_tree().root
		vp.push_input(_mouse(at, true), true)
		vp.push_input(_touch(at, true), true)
		for f in 3:
			poke.call()
			await get_tree().process_frame
		var survived := is_instance_valid(b) and b.is_inside_tree()
		vp.push_input(_mouse(at, false), true)
		vp.push_input(_touch(at, false), true)
		taps += 1
		if _presses == 1 and survived:
			hits += 1
		if is_instance_valid(b) and b.pressed.is_connected(_on_press):
			b.pressed.disconnect(_on_press)
		await _settle(0.3)
		if hud.current_sheet != sheet:   # the action closed the sheet (garage upgrade): reopen
			UIManager.open_panel(panel, null)
			await _settle(0.55)
			sheet = hud.current_sheet
		hud._modal_queue.clear()
		for m in hud.modal_layer.get_children():
			m.queue_free()
		hud._modal_open = false
	check(taps > 0, "%s: found buttons to tap (%d)" % [panel, taps])
	check(hits == taps, "W63 %s: every tap during live rebuilds registers (%d/%d)" % [panel, hits, taps])
	UIManager.close_panel()
	await _settle(0.4)


## An enabled, visible, on-screen button of the sheet body (optionally of one style), skipping the close button.
func _pick(sheet: Node, style: String) -> BaseButton:
	var vis: Rect2 = sheet.scroll.get_global_rect()
	for n in sheet.body.find_children("*", "BaseButton", true, false):
		var b := n as BaseButton
		if b.disabled or not b.is_visible_in_tree():
			continue
		if style != "" and b.theme_type_variation != style:
			continue
		var r: Rect2 = b.get_global_rect()
		if r.size.x < 8 or not vis.encloses(r):
			continue
		return b
	return null


func _on_press() -> void:
	_presses += 1


func _touch(pos: Vector2, pressed: bool) -> InputEventScreenTouch:
	var t := InputEventScreenTouch.new()
	t.index = 0
	t.position = pos
	t.pressed = pressed
	return t


func _mouse(pos: Vector2, pressed: bool) -> InputEventMouseButton:
	var m := InputEventMouseButton.new()
	m.device = InputEvent.DEVICE_ID_EMULATION
	m.button_index = MOUSE_BUTTON_LEFT
	m.pressed = pressed
	m.position = pos
	m.global_position = pos
	if pressed:
		m.button_mask = MOUSE_BUTTON_MASK_LEFT
	return m
