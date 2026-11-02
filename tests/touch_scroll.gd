extends Node
## games/CLAUDE.md §16 gate 2 — finger scrolling + tap safety with the shared kit suite
## (res://addons/coddy_ui/tests/touch_scroll_suite.gd, device-like events through Input.parse_input_event): kit fixtures in
## en + de, then Borderline Empire's real Settings and Shop lists in en + de: a drag that starts on a switch / price button
## scrolls and presses nothing, a flick coasts, a tap on a switch flips it, the list behind a window stays put.
## Run: godot --headless --path . res://tests/touch_scroll.tscn  (tests/touch_scroll_test.tscn walks every other sheet and the job strip.)
const Suite = preload("res://addons/coddy_ui/tests/touch_scroll_suite.gd")


func _ready() -> void:
	_run.call_deferred()


func _settle(seconds: float) -> void:
	await get_tree().create_timer(seconds, true, false, true).timeout
	await get_tree().process_frame


func _frames(n: int) -> void:
	for i in n:
		await get_tree().process_frame


func _run() -> void:
	UIKit.theme()   # the game's font files are configured before the kit fixtures build their theme
	var s := Suite.new(get_tree())
	await s.run_fixtures(["en", "de"])
	get_tree().root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED   # canvas = window pixels: the suite sends window-pixel events
	for loc in ["en", "de"]:
		await _game(s, loc)
	get_tree().quit(1 if s.report("TOUCH_SCROLL_TESTS") > 0 else 0)


func _game(s: Suite, loc: String) -> void:
	get_tree().root.size = Vector2i(720, 1280)   # the game's own canvas size
	await _frames(2)
	SaveManager.delete_save()
	GameManager.settings["language"] = loc
	GameManager.settings["quality_notice"] = ""
	GameManager.apply_language()
	GameManager.new_game()
	var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	await _frames(8)
	var hud: Node = main.hud
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	ProgressionManager.flags["intro_done"] = true
	TutorialManager.skip()
	hud._restore_game_chrome()
	hud._modal_queue.clear()
	hud._offer_toast_cd = 1e12
	Iap._test_setup({"v": 1, "owned": {}, "any": false, "pending": [], "seen": [], "delivered": {}}, true, true)
	var tag := "smuggler " + loc
	# Settings: switches in a long list
	UIManager.open_panel("settings")
	await _settle(0.7)
	var sheet = hud.current_sheet
	var sc: ScrollContainer = sheet.scroll
	# Reset restored scroll memory BEFORE choosing a visible target.
	await s.reset(sc)
	var sw: Button = null
	for b in sheet.body.find_children("toggle_*", "Button", true, false):
		if sc.get_global_rect().encloses(b.get_global_rect()):
			sw = b
			break
	s.check(sw != null, tag + ": a settings switch is on screen")
	if sw != null:
		var key := str(sw.name).trim_prefix("toggle_")
		var before := bool(GameManager.settings.get(key, true))
		await s.drag_scrolls(sc, sw, tag + " settings")
		s.check(bool(GameManager.settings.get(key, true)) == before, tag + ": the drag flipped no switch")
		await s.flick_coasts(sc, sw, tag + " settings")
		await s.reset(sc)
		sw = sheet.body.find_child("toggle_" + key, true, false)
		await s.tap_clicks(sw, func(): return bool(GameManager.settings.get(key, true)) != before, tag + " settings switch")
	UIManager.close_panel()
	await _frames(30)
	# Shop: product rows with price buttons
	UIManager.open_panel("shop")
	await _settle(0.7)
	sheet = hud.current_sheet
	sc = sheet.scroll
	await s.reset(sc)
	var buy: Button = null
	for b in sheet.body.find_children("buy", "Button", true, false):
		if sc.get_global_rect().encloses(b.get_global_rect()) and not b.disabled:
			buy = b
			break
	if buy == null:   # no store prices on a desktop run: the disabled "…" price button still takes the drag
		for b in sheet.body.find_children("buy", "Button", true, false):
			if sc.get_global_rect().encloses(b.get_global_rect()):
				buy = b
				break
	s.check(buy != null, tag + ": a shop price button is on screen")
	if buy != null:
		await s.drag_scrolls(sc, buy, tag + " shop")
		s.check(not Iap.busy, tag + ": the drag started no purchase")
		await s.flick_coasts(sc, buy, tag + " shop")
	UIManager.close_panel()
	await _frames(20)
	main.queue_free()
	await _frames(4)
	SaveManager.delete_save()
