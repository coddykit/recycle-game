extends Node
## Review triage 10-30 (D3, tr 2★ "nice but too slow"): the player's ×1 / ×2 game speed (GameManager.game_speed,
## HUD SpeedButton, docs/REVIEW_BORDERLINE_SLOW_2026-10-28.md "Ek"). Hidden and ×1 during the tutorial, one phone tap
## (emulated click + touch, Android's order) toggles it once, the choice is saved in settings.json and read back on the
## next launch, the economy tick runs twice as far per real second at ×2, and a pause (ad / background) still stops it.
## Run: godot --headless --path . res://tests/game_speed_test.tscn
var failures := 0
var checks := 0


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		print("GAMESPEED FAIL ", label)
	else:
		print("GAMESPEED ok ", label)


func _frames(n := 6) -> void:
	for i in n:
		await get_tree().process_frame


func _settle(seconds := 0.5) -> void:
	await get_tree().create_timer(seconds, true, false, true).timeout
	await get_tree().process_frame


## Game seconds the clock advances in `n` frames of `dt` real seconds (play_time counts the scaled dt).
func _advance(n: int, dt: float) -> float:
	var t0 := EconomyManager.stat("play_time")
	for i in n:
		GameManager._process(dt)
	return EconomyManager.stat("play_time") - t0


func _ready() -> void:
	get_tree().create_timer(120.0).timeout.connect(func(): get_tree().quit(2))
	await get_tree().process_frame
	get_tree().root.size = Vector2i(540, 960)
	await get_tree().process_frame
	SaveManager.delete_save()
	GameManager.settings["language"] = "en"
	GameManager.settings["quality_notice"] = ""
	GameManager.settings["speed"] = 1
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
	GameManager.running = true

	# 1. the tutorial: no button, always ×1 (even with a saved ×2)
	TutorialManager.reset()
	GameManager.settings["speed"] = 2
	hud._update_speed_button()
	check(not GameManager.speed_unlocked(), "locked while the tutorial runs")
	check(GameManager.game_speed() == 1.0, "×1 during the tutorial even with a saved ×2")
	check(not hud.speed_btn.visible, "the speed button is hidden during the tutorial")
	check(GameManager.toggle_game_speed() == 1, "a toggle during the tutorial does nothing")
	near(_advance(10, 0.05), 0.5, "tutorial: 10 × 50 ms = 0.5 game s")
	GameManager.settings["speed"] = 1

	# 2. tutorial over: the button shows, one phone tap = ×2 (a double action would land back on ×1)
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
	if hud.current_sheet != null and is_instance_valid(hud.current_sheet):
		hud.close_panel()
	await _settle(0.4)
	hud._update_speed_button()
	await _frames(3)
	check(GameManager.speed_unlocked(), "unlocked once the tutorial is finished")
	check(hud.speed_btn.is_visible_in_tree(), "the speed button is on the HUD")
	check(hud.speed_btn.text == "×1", "it reads ×1")
	check(hud.speed_btn.tooltip_text == tr("Game speed") and tr("Game speed") != "", "translated tooltip")
	check(hud.speed_btn.size.y >= 44.0, "a full touch target (%d px)" % hud.speed_btn.size.y)
	await _tap(hud.speed_btn)
	check(int(GameManager.settings.get("speed", 1)) == 2 and GameManager.game_speed() == 2.0, "one tap: ×2")
	check(hud.speed_btn.text == "×2", "the button reads ×2")
	check(int(SaveManager.load_settings().get("speed", 0)) == 2, "×2 is written to settings.json")

	# 3. economy tick ×2 per real second; pause and background still stop the clock
	near(_advance(10, 0.05), 1.0, "×2: 10 × 50 ms = 1.0 game s")
	var j0 := ContractManager.jobs.size()
	get_tree().paused = true
	near(_advance(10, 0.05), 0.0, "a full-screen ad (tree paused) stops the clock at ×2")
	get_tree().paused = false
	GameManager._suspended = true
	near(_advance(10, 0.05), 0.0, "the background (suspended) stops the clock at ×2")
	GameManager._suspended = false
	check(ContractManager.jobs.size() == j0, "nothing moved while paused")

	# 4. persists across sessions: a fresh settings read gives ×2 back
	GameManager.settings = {"speed": 1}
	GameManager.load_settings("low")
	check(int(GameManager.settings.get("speed", 1)) == 2 and GameManager.game_speed() == 2.0, "next launch: still ×2")

	# 5. the guide replayed: back to ×1 and hidden until it ends; the choice is kept for later
	TutorialManager.reset()
	hud._update_speed_button()
	check(GameManager.game_speed() == 1.0 and not hud.speed_btn.visible, "a replayed guide runs at ×1 without the button")
	TutorialManager.skip()
	hud._update_speed_button()
	check(GameManager.game_speed() == 2.0 and hud.speed_btn.visible, "after it, ×2 again")

	# 6. a second tap: back to ×1
	await _settle(0.3)
	await _tap(hud.speed_btn)
	check(int(GameManager.settings.get("speed", 2)) == 1 and hud.speed_btn.text == "×1", "a second tap: ×1")
	near(_advance(10, 0.05), 0.5, "×1 again: 0.5 game s")

	# 7. the label / toast in all ten languages
	for l in GameManager.LANGUAGES:
		TranslationServer.set_locale(l)
		var t := tr("Game speed ×%d") % 2
		check(t.contains("2") and (l == "en" or tr("Game speed") != "Game speed"), "%s: %s / %s" % [l, tr("Game speed"), t])
	TranslationServer.set_locale("en")

	main.queue_free()
	await _frames(4)
	SaveManager.delete_save()
	print("GAMESPEED checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)


func near(a: float, b: float, label: String) -> void:
	check(absf(a - b) < 0.001, "%s (got %.4f)" % [label, a])


## One phone tap in Android's order: emulated click then the touch, press and release, through the real viewport.
func _tap(c: Control) -> void:
	var at: Vector2 = c.get_global_rect().get_center()
	var vp := get_tree().root
	for down in [true, false]:
		vp.push_input(_mouse(at, down, InputEvent.DEVICE_ID_EMULATION), true)
		vp.push_input(_touch(at, down, 0), true)
		await _frames(2)
	await _settle(0.3)


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
