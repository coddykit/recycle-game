extends Node
## Quality pass 2026-10-18: app lifecycle and owner rules.
## First launch without a save, save + cold restart ("Continue"), background / foreground, language switch through the
## real Settings path (scene reload), late UI calls while the HUD is gone (monkey finding), the guided-onboarding Skip,
## the boot curtain / 60 fps cap / no Godot splash / gl_compatibility, text >= 11 pt, touch >= 44 pt, no free trial.
## Run: godot --headless --path . res://tests/lifecycle_test.tscn
var failures := 0
var checks := 0
var main: Node


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("LIFECYCLE ", "PASS " if ok else "FAIL ", label)


func frames(n := 3) -> void:
	for i in n:
		await get_tree().process_frame


## Starts Main as the current scene (so the real language switch can reload it), like Boot does on a device.
func start_main() -> Node:
	main = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	get_tree().current_scene = main
	await frames(4)
	return main


func skip_intro() -> void:
	var hud = main.hud
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	ProgressionManager.flags["intro_done"] = true
	hud._restore_game_chrome()
	await frames(2)


func _ready() -> void:
	await get_tree().process_frame
	SaveManager.delete_save()
	GameManager.settings["quality_notice"] = ""
	await _first_launch()
	await _restart_keeps_state()
	await _background_foreground()
	await _goal_toast_off_the_map()
	await _language_switch()
	await _late_ui_calls()
	await _tutorial_skip()
	await _toast_off_sheet_head()
	_boot_and_renderer()
	await _text_and_touch()
	_no_free_trial()
	_translations_complete()
	TranslationServer.set_locale("en")
	SaveManager.delete_save()
	print("LIFECYCLE checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)


func _first_launch() -> void:
	check(not SaveManager.has_save(), "first launch: no save on disk")
	await start_main()
	check(GameManager.running, "first launch: game runs without a save")
	check(not ProgressionManager.intro_done(), "first launch: intro still to play")
	check(is_instance_valid(main.hud.title_card), "first launch: title card shown")
	check(VehicleManager.vehicles.size() == 1 and EconomyManager.cash > 0.0, "first launch: starter vehicle and cash")
	check(TutorialManager.step == 0 and not TutorialManager.finished, "first launch: tutorial at step 0")
	check(SaveManager.has_save(), "first launch: a save was written right away")
	for i in 20:
		GameManager.tick(0.25)
	await frames(2)
	check(GameManager.running, "first launch: still running after ticks")


func _restart_keeps_state() -> void:
	# 10-28 fleet Back check: Back during the first-run story (name step) never saves and quits the game
	var hud = main.hud
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if not is_instance_valid(hud.onboarding):
		hud._maybe_onboard()
	await frames(2)
	check(is_instance_valid(hud.onboarding), "back: the first-run story is up")
	hud.notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	await frames(2)
	check(is_instance_valid(hud.onboarding) and GameManager.running and not get_tree().paused, "back: Back during the first-run story keeps it on screen (no save + quit)")
	await skip_intro()
	ProgressionManager.flags["company"] = "Quality Logistics"
	EconomyManager.earn(4321.0, "test", false)
	var offer: Dictionary = ContractManager.offers[0]
	var job := ContractManager.dispatch(offer, VehicleManager.vehicles[0], "normal")
	check(not job.is_empty(), "restart: a delivery is on the road")
	TutorialManager.step = 3
	var cash := EconomyManager.cash
	var jobs := ContractManager.jobs.size()
	check(GameManager.save_now(), "restart: save works")
	main.queue_free()
	await frames(4)
	await start_main()
	check(absf(EconomyManager.cash - cash) < 1.0, "restart: cash survives a cold start (%.0f vs %.0f)" % [EconomyManager.cash, cash])
	check(ContractManager.jobs.size() == jobs, "restart: the delivery on the road survives")
	check(ProgressionManager.company_name() == "Quality Logistics", "restart: company name survives")
	check(TutorialManager.step == 3, "restart: tutorial step survives")
	check(VehicleManager.vehicles.size() == 1, "restart: fleet survives")


## #99: the goal toast takes the goal chip's own row (not a card over the map); other toasts keep their row.
func _goal_toast_off_the_map() -> void:
	await skip_intro()
	var hud = main.hud
	hud._on_goal_completed("Reach $1,000 cash", 100.0)
	await frames(3)
	var chip: Control = hud.goal_chip
	var card: Control = hud.ui_toast.card()
	check(card.visible and card.get_global_rect().position.y <= chip.get_global_rect().position.y + 8.0 and card.get_global_rect().end.x <= chip.get_global_rect().end.x + 40.0,
		"goal toast: sits on the goal chip row, not below it (%s vs %s)" % [card.get_global_rect(), chip.get_global_rect()])
	hud.toast("Vehicle seized", UIKit.RED)   # the guide is still up here: only a danger toast shows over it (W66 fleet sweep 10-27)
	await frames(3)
	check(card.get_global_rect().position.y > chip.get_global_rect().end.y - 4.0, "goal toast: other toasts keep the row below the chip")


func _background_foreground() -> void:
	await skip_intro()
	var play := EconomyManager.stat("play_time")
	get_tree().root.propagate_notification(NOTIFICATION_APPLICATION_PAUSED)
	check(GameManager._suspended, "background: the game suspends")
	check(not main.hud.can_present(), "background: no modal is presented while suspended")
	var saved := SaveManager.load_data()
	check(not saved.is_empty() and absf(float(saved.time) - Time.get_unix_time_from_system()) < 5.0, "background: progress saved on pause")
	GameManager._process(0.1)
	check(EconomyManager.stat("play_time") == play, "background: no ticks while suspended")
	GameManager._paused_at -= 3600.0
	var cash := EconomyManager.cash
	get_tree().root.propagate_notification(NOTIFICATION_APPLICATION_RESUMED)
	check(not GameManager._suspended, "foreground: resumes")
	check(GameManager._paused_at == 0.0, "foreground: the away clock is consumed")
	var cash_after := EconomyManager.cash
	get_tree().root.propagate_notification(NOTIFICATION_APPLICATION_RESUMED)
	check(is_equal_approx(EconomyManager.cash, cash_after), "foreground: a second resume pays nothing twice")
	check(cash_after >= cash, "foreground: absence never costs money")
	await frames(2)


func _language_switch() -> void:
	var cash := EconomyManager.cash
	var old := main
	GameManager.set_setting("language", "de")
	await frames(6)
	check(get_tree().current_scene != old and is_instance_valid(get_tree().current_scene), "language: scene reloaded")
	main = get_tree().current_scene
	check(TranslationServer.get_locale().begins_with("de"), "language: locale switched to German")
	check(str(SaveManager.load_settings().get("language", "")) == "de", "language: choice persisted")
	check(GameManager.running, "language: game running again after the reload")
	check(absf(EconomyManager.cash - cash) < 1.0, "language: cash unchanged by the reload")
	check(UIManager.current_panel == "settings", "language: Settings reopened after the reload")
	GameManager.set_setting("language", "ja")
	await frames(6)
	main = get_tree().current_scene
	check(TranslationServer.get_locale().begins_with("ja") and GameManager.running, "language: second switch (ja) works too")
	GameManager.set_setting("language", "en")
	await frames(6)
	main = get_tree().current_scene
	check(TranslationServer.get_locale().begins_with("en") and GameManager.running, "language: back to English")


## Monkey finding 2026-10-18: a panel opened after the HUD left the tree (scene reload in flight) raised
## "Invalid access to property or key 'process_frame' on a base object of type 'null instance'".
func _late_ui_calls() -> void:
	await skip_intro()
	UIManager.close_panel()
	await frames(25)
	var layer: Node = main.hud.get_parent()
	main.remove_child(layer)
	UIManager.open_panel("contracts")
	UIManager.toast("late toast")
	UIManager.modal("Late", "late modal")
	UIManager.close_panel()
	check(main.hud.current_sheet == null, "late UI: opening a panel with the HUD out of the tree is a no-op")
	main.hud.open_panel("contracts")
	check(main.hud.current_sheet == null, "late UI: HUD.open_panel guards itself too")
	main.add_child(layer)
	await frames(2)
	UIManager.open_panel("contracts")
	await frames(3)
	check(main.hud.current_sheet != null, "late UI: panels open again once the HUD is back")
	UIManager.close_panel()
	await frames(20)


## Kit round 2 (10-27): the toast row sat on a full-height sheet's header (title + close chip); it keeps off the header
## and the tab strip, a toast already up moves when the sheet opens, and goes back to its row when it closes.
func _toast_off_sheet_head() -> void:
	var hud = main.hud
	var was: Vector2i = get_tree().root.size
	for sz in [Vector2i(720, 960), Vector2i(720, 1280), Vector2i(720, 1560)]:
		get_tree().root.size = sz
		hud.close_panel()
		await frames(20)
		hud.ui_toast.hide_toast()
		hud.toast("Contract delivered early: the client paid a bonus and wants another run", Color.WHITE)
		await get_tree().create_timer(0.3).timeout
		var card: Control = hud.ui_toast.card()
		var row_y: float = card.get_global_rect().position.y
		check(hud.ui_toast.is_showing(), "toast vs sheet %s: a toast shows on the map" % sz)
		for name in ["contracts", "fleet", "upgrades", "settings"]:
			hud.open_panel(name)
			await get_tree().create_timer(0.5).timeout   # the sheet's slide-in (0.28 s) and height fit
			var s = hud.current_sheet
			if s == null or not is_instance_valid(s):
				check(false, "toast vs sheet: %s opens" % name)
				continue
			if not hud.ui_toast.is_showing():
				hud.toast("Contract delivered early: the client paid a bonus and wants another run", Color.WHITE)   # two lines
				await get_tree().create_timer(0.3).timeout
			var head_r: Rect2 = (s.head.box as Control).get_global_rect()
			var r: Rect2 = card.get_global_rect()
			check(hud.ui_toast.is_showing() and not r.intersects(head_r) and not (s.tabs_row.is_visible_in_tree() and r.intersects(s.tabs_row.get_global_rect())),
				"toast vs sheet %s: on %s the toast %s keeps off the sheet's header %s and tab strip" % [sz, name, r, head_r])
			hud.ui_toast.hide_toast()
		hud.close_panel()
		await get_tree().create_timer(0.5).timeout
		hud.toast("Garage upgraded", Color.WHITE)
		await frames(20)
		check(absf(card.get_global_rect().position.y - row_y) < 1.0, "toast vs sheet %s: with the sheet closed the toast is back on its row (%.0f vs %.0f)" % [sz, card.get_global_rect().position.y, row_y])
		hud.ui_toast.hide_toast()
	get_tree().root.size = was
	await frames(10)


func _tutorial_skip() -> void:
	GameManager.new_game()   # a fresh empire: no delivery yet, so the tutorial really is on step 1
	await skip_intro()
	await frames(3)
	var ov = main.hud.tutorial
	check(ov.visible and ov.skip_btn.visible, "onboarding: coach bubble + Skip are visible on step 1")
	var target: Control = UIManager.get_target("nav_contracts")
	check(target != null, "onboarding: step 1 has a visible target (Contracts tab)")
	check(ov.coach.hole.has_area() and ov.coach.hole.grow(-8.0).is_equal_approx(target.get_global_rect()), "onboarding: the kit coach spotlights the real button (a rounded hole the size of the target)")
	check(ov.skip_btn.size.y >= 84.0 and ov.skip_btn.size.x >= 84.0, "onboarding: Skip is a >= 48 dp (84 px) touch target (%s)" % str(ov.skip_btn.size))
	# W66 fleet sweep 10-27: no toast over the guide's bubble / spotlight (a danger toast still shows)
	main.hud.ui_toast.hide_toast()
	main.hud.toast("Dispatched 1 vehicle", Color.WHITE)
	check(not main.hud.ui_toast.is_showing(), "onboarding: no toast while the guide bubble is up")
	main.hud.toast("Vehicle seized", UIKit.RED)
	check(main.hud.ui_toast.is_showing() or UIKit.kind_of(UIKit.RED) != "danger", "onboarding: a danger toast still shows during the guide")
	main.hud.ui_toast.hide_toast()
	ov.skip_btn.pressed.emit()
	await frames(3)
	check(TutorialManager.finished and not ov.visible, "onboarding: Skip ends the tutorial and hides the coach")
	check(bool(ProgressionManager.flags.get("tabs_all", false)), "onboarding: Skip unlocks every tab")


func _boot_and_renderer() -> void:
	var boot: GDScript = load("res://scripts/boot.gd")
	check(boot.max_fps_for(true) == 60, "boot: phones are capped at 60 fps")
	check(boot.max_fps_for(false) == 0, "boot: desktop is not capped")
	check(str(ProjectSettings.get_setting("application/run/main_scene")) == "res://scenes/boot/boot.tscn", "boot: the loading curtain scene is the main scene")
	check(not bool(ProjectSettings.get_setting("application/boot_splash/show_image", true)), "boot: no native splash image (never the Godot logo)")
	var bg: Color = ProjectSettings.get_setting("application/boot_splash/bg_color")
	check(bg.is_equal_approx(boot.SPLASH_BG), "boot: the curtain starts in the native splash colour")
	check(boot.model_paths().size() >= 50, "boot: the curtain preloads the model GLBs (%d)" % boot.model_paths().size())
	check(ResourceLoader.exists(boot.LOGO) and ResourceLoader.exists("res://scenes/boot/boot.tscn"), "boot: logo and scene exist")
	check(str(ProjectSettings.get_setting("rendering/renderer/rendering_method")) == "gl_compatibility", "renderer: gl_compatibility")
	check(str(ProjectSettings.get_setting("rendering/renderer/rendering_method.mobile")) == "gl_compatibility", "renderer: mobile uses gl_compatibility (Vulkan launch crash)")
	var cfg := ConfigFile.new()
	check(cfg.load("res://export_presets.cfg") == OK, "export presets readable")
	var text := FileAccess.get_file_as_string("res://export_presets.cfg").to_lower()
	check(not text.contains("vulkan") and not text.contains("rendering_method=\"forward"), "export presets never ask for Vulkan / Forward+")
	for tier in ["low", "medium", "high"]:
		var p: Dictionary = World.profile(tier)
		check(p.has("scale") and p.has("msaa"), "tiers: %s profile exists" % tier)
	check(World.profile("low").shadow_atlas == 0 and World.profile("low").msaa == Viewport.MSAA_DISABLED and is_equal_approx(World.profile("low").scale, 0.85), "tiers: Low has no shadows, 0.85 scale, no MSAA (W67)")
	check(float(World.profile("low").scale) < float(World.profile("medium").scale) and float(World.profile("medium").scale) <= float(World.profile("high").scale), "tiers: Low < Medium ≤ High render scale (W67: Medium native)")
	check(int(World.profile("high").msaa) > int(World.profile("medium").msaa), "tiers: High MSAA above Medium (4x vs 2x)")
	check(DeviceTier.classify({"platform": "android", "ram_bytes": 8 * DeviceTier.GIB, "cores": 8, "adapter": "Adreno 660", "vendor": "Qualcomm"}) == "medium", "phones: strong device defaults to Medium, never High")
	check(DeviceTier.classify({"platform": "android", "ram_bytes": 3 * DeviceTier.GIB, "cores": 8, "adapter": "Mali-G52", "vendor": "ARM"}) == "low", "phones: weak device defaults to Low")


func _walk(n: Node, out: Array) -> void:
	if n is CanvasItem and not (n as CanvasItem).visible:
		return
	out.append(n)
	for c in n.get_children():
		_walk(c, out)


func _text_and_touch() -> void:
	await skip_intro()
	GameManager.settings["advisor"] = false
	check(UIKit.MIN_FONT >= 22, "text: UIKit floor is 22 units (11 pt on the 720-wide canvas)")
	var small := []
	var tiny_buttons := []
	for name in ["contracts", "dispatch", "fleet", "map", "upgrades", "business", "settings", "shop", "daily"]:
		var arg: Variant = null
		if name == "dispatch":
			if ContractManager.offers.is_empty():
				ContractManager.refresh_offers()
			arg = ContractManager.offers[0].id
		UIManager.open_panel(name, arg)
		await frames(14)
		var nodes: Array = []
		_walk(main.hud, nodes)
		for n in nodes:
			if n is Label and (n as Label).text.strip_edges() != "":
				var fs: int = (n as Label).get_theme_font_size("font_size")
				if fs < 21:
					small.append("%s:%s:%d" % [name, (n as Label).text.left(24), fs])
			elif n is Button and not (n as Button).disabled and (n as Button).is_visible_in_tree():
				var s: Vector2 = (n as Button).size
				if (n as Button).theme_type_variation == "SegmentButton":
					s = ((n as Button).get_parent().get_parent() as Control).size   # the kit's segmented control is 84 px tall as a whole
				if s.x < 84.0 or s.y < 84.0:
					tiny_buttons.append("%s:%s:%s" % [name, (n as Button).text.left(20), str(s)])
	UIManager.close_panel()
	await frames(20)
	check(small.is_empty(), "text: every visible label is >= 21 px (12 sp, the kit floor) (%s)" % str(small.slice(0, 5)))
	check(tiny_buttons.is_empty(), "touch: every visible button is >= 84 px / 48 dp (kit floor) (%s)" % str(tiny_buttons.slice(0, 5)))


## Owner rule 2026-09-26: no free trial anywhere in the fleet (paywall copy, store text, catalogue).
func _no_free_trial() -> void:
	var patterns := {
		"en": "free[ -]?trial|try (it )?free|trial period|\\btrial\\b", "tr": "ücretsiz dene|deneme sürüm", "de": "kostenlos testen|testzeitraum|probezeitraum|kostenlose testversion",
		"es": "prueba gratis|prueba gratuita|periodo de prueba", "fr": "essai gratuit|période d.essai", "ja": "無料トライアル|無料体験|無料お試し",
		"ko": "무료 체험|무료 평가판", "pt": "teste grátis|período de teste|avaliação gratuita", "ru": "пробн|бесплатный период", "zh": "免费试用|免费体验",
	}
	var csv := FileAccess.open("res://translations/strings.csv", FileAccess.READ)
	var langs: PackedStringArray = csv.get_csv_line()
	var hits := []
	var res := {}
	for l in patterns:
		var re := RegEx.new()
		re.compile("(?i)" + str(patterns[l]))
		res[l] = re
	while not csv.eof_reached():
		var row: PackedStringArray = csv.get_csv_line()
		for i in range(1, mini(row.size(), langs.size())):
			var re: RegEx = res.get(langs[i])
			if re != null and re.search(row[i]) != null:
				hits.append("%s: %s" % [langs[i], row[i].left(50)])
	check(hits.is_empty(), "no free trial: strings.csv in 10 languages (%s)" % str(hits.slice(0, 3)))
	var files_hit := []
	var any := RegEx.new()
	any.compile("(?i)free[ -]?trial|introductory|intro_offer|freeTrial|trial_period|trialDuration")
	for path in ["res://data/monetization.json", "res://store/iap_catalogue.json", "res://store/IAP.md", "res://store/listing_en.md", "res://store/listing_tr.md",
			"res://store/app-review-notes.md", "res://scripts/monetization/monetization.gd", "res://scripts/monetization/rewarded_ads.gd", "res://data/onboarding.json"]:
		if FileAccess.file_exists(path) and any.search(FileAccess.get_file_as_string(path)) != null:
			files_hit.append(path)
	check(files_hit.is_empty(), "no free trial: shop data, store listing and monetization code (%s)" % str(files_hit))


## 10 languages 100 %: no row of a non-Latin language may still be the English text (zh region names were), and the
## "Needs <garage level>" reasons must name the level in the player's language.
func _translations_complete() -> void:
	var allow := ["Borderline Empire", "Rita Redline", "Borderline", "Dot", "Android", "GodotxRevenueCat"]
	var csv := FileAccess.open("res://translations/strings.csv", FileAccess.READ)
	var langs: PackedStringArray = csv.get_csv_line()
	var same := []
	var rows := 0
	while not csv.eof_reached():
		var row: PackedStringArray = csv.get_csv_line()
		if row.size() < langs.size() or row[0] == "":
			continue
		rows += 1
		for i in range(1, langs.size()):
			if langs[i] in ["ja", "ko", "zh", "ru"] and row[i] == row[0] and not (row[0] in allow) and row[0].length() >= 3:
				var letters := RegEx.create_from_string("[A-Za-z]{3,}")
				if letters.search(row[0]) != null:
					same.append("%s: %s" % [langs[i], row[0]])
	check(rows >= 600, "translations: %d rows read" % rows)
	check(same.is_empty(), "translations: ja/ko/zh/ru rows are all translated (%s)" % str(same.slice(0, 4)))
	TranslationServer.set_locale("ja")
	GameManager.new_game()
	var armored: Dictionary = VehicleManager.buy_check("armored")
	var business_reason := ""
	for d in Data.business_upgrades:
		var c: Dictionary = ProgressionManager.business_check(str(d.id))
		if c.get("locked", false):
			business_reason = str(c.reason)
			break
	var latin := RegEx.create_from_string("[A-Za-z]{3,}")
	check(armored.get("locked", false) and latin.search(str(armored.reason)) == null, "translations: vehicle lock reason is fully Japanese (%s)" % str(armored.get("reason")))
	check(business_reason != "" and latin.search(business_reason) == null, "translations: business lock reason is fully Japanese (%s)" % business_reason)
