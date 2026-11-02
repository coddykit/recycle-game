extends Sheet
const UiRow = preload("res://addons/coddy_ui/ui_row.gd")
const UiSegmented = preload("res://addons/coddy_ui/ui_segmented.gd")
const UiSettings = preload("res://addons/coddy_ui/ui_settings.gd")
## Audio / haptics / quality toggles, credits and progress reset.


func _init() -> void:
	super._init()
	title = "Settings"
	icon_name = "settings"
	height_frac = 0.76


func build(b: VBoxContainer) -> void:
	if not low():   # Low: no summary line (the version is in the credits at the end; one draw less)
		set_sub(["%s  ·  v%s" % [Data.title(), str(ProjectSettings.get_setting("application/config/version", ""))]])
	var shop_b := UiRow.nav("shopping-bag", tr("Shop & sponsor bonus"), "", func(): UIManager.open_panel("shop"), "primary")
	shop_b.name = "settings_shop"
	b.add_child(shop_b)
	UiSettings.section(b, tr("Sound"), [
		_toggle("sound", "Sound effects", "volume-2"), _toggle("music", "Music", "music"), _toggle("voice", "Announcer voice", "user")])
	UiSettings.section(b, tr("Comfort"), [
		_toggle("haptics", "Vibration", "smartphone"), _toggle("reduced_motion", "Reduced motion", "gauge"), _toggle("advisor", "Dot reminders", "message-square")])
	_graphics(b)
	var lang_head := Kit.section(tr("Language"))
	b.add_child(lang_head)
	b.add_child(_language_row())
	b.add_child(_hq_row())
	var tut := UiRow.nav("circle-play", tr("Skip tutorial") if not TutorialManager.finished else tr("Tutorial complete"), "", func():
		TutorialManager.skip()
		if hud:
			hud._update_tabs()
		refresh())
	tut.name = "settings_skip_tutorial"
	tut.set_disabled_state(TutorialManager.finished)
	var intro := UiRow.nav("rotate-ccw", tr("Replay intro"), "", func():
		if hud == null:
			return
		hud.close_panel()
		ProgressionManager.flags["intro_done"] = false
		hud._maybe_onboard())
	UIManager.register_target("settings_replay_intro", intro)
	UiSettings.section(b, tr("Help"), [
		UiRow.nav("message-square", tr("Dot's advice"), "", TutorialManager.show_advice),
		UiRow.nav("circle-help", tr("How to play"), "", func():
			UIManager.modal("How to play", "1. Accept a contract.\n2. Choose a vehicle and route, then dispatch.\n3. Collect delivery income and upgrade your fleet.\n4. Expand your garage, unlock regions and automate deliveries.", "hand", [], UIKit.BLUE)),
		UiRow.nav("file-text", tr("Daily logistics"), "", func(): UIManager.open_panel("daily")),
		intro, tut])
	var restore := UiRow.nav("rotate-cw", IapText.t("restore"), "", func(): Iap.restore())
	restore.name = "settings_restore"
	restore.set_disabled_state(Iap.busy)
	var feedback := UiRow.nav("message-square", tr("Send feedback"), "", func():
		if hud:
			hud.show_feedback())
	feedback.name = "settings_feedback"
	var buy_rows: Array = [restore, feedback]
	if Monetization.rate_link_available():
		var rate := UiRow.nav("star", tr("Rate Borderline Empire"), "", Monetization.open_rate_page)
		rate.name = "settings_rate"
		buy_rows.append(rate)
	UiSettings.section(b, tr("Purchases & support"), buy_rows)
	var data_use := UiRow.nav("info", tr("How your data is used"), "", func():
		UIManager.modal("Privacy", tr("Game progress stays on your device. Optional ads use Google AdMob to process ad interactions and device information. Purchases use the platform store and RevenueCat to validate receipts and restore ownership. No account is required. You can manage advertising consent in Privacy choices.") + "\n\n" + tr("Firebase Analytics measures sessions, screen views and gameplay milestones using an app instance identifier. Your company name and saved game are not sent. Analytics advertising storage and personalization are disabled."), "shield", [], UIKit.BLUE))
	var analytics := UiRow.toggle("chart-bar", tr("Share anonymous analytics"), Analytics.consent, Analytics.set_consent)
	analytics.name = "analytics_consent"
	UiSettings.privacy(b, Ads, {"title": tr("Privacy"), "options": tr("Privacy options"), "policy": tr("Privacy policy")}, [analytics, data_use])
	var save_b := UIKit.button("Save now", "", "save", Vector2(0, 84), 24)
	save_b.theme_type_variation = "OutlineButton"
	save_b.pressed.connect(func():
		if GameManager.save_now():
			UIManager.toast("Game saved", UIKit.GREEN, "check")
		else:
			UIManager.toast("Could not save. Check available storage and try again.", UIKit.RED, "risk"))
	b.add_child(save_b)
	var reset := UIKit.button("Reset progress", "BtnDanger", "", Vector2(0, 84), 24)
	reset.name = "reset_progress" # the QA reset-dialog capture presses this real button (debug_tools.gd)
	reset.pressed.connect(func():
		UIManager.modal("Start over?", "This erases your whole empire. There is no undo.", "risk",
				[["Cancel", "BtnDark", Callable()], ["Erase", "BtnDanger", func(): GameManager.reset_progress()]], UIKit.RED))
	b.add_child(reset)
	var credits := UIKit.wlabel(tr("%s v%s\nA fictional tycoon game. All places, groups and goods are made up.\nBuilt with Godot Engine · Font: Baloo 2 (OFL)") % [Data.title(), str(ProjectSettings.get_setting("application/config/version", ""))], "CaptionMuted")
	credits.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	b.add_child(credits)


## One settings switch (kit row): the value lives in GameManager.settings[key]; no rebuild, the switch animates in place.
func _toggle(key: String, text: String, icon_name: String) -> Control:
	var row := UiRow.toggle(icon_name, tr(text), bool(GameManager.settings.get(key, true)), func(v: bool): GameManager.set_setting(key, v))
	row.name = "toggle_" + key
	return row


func _graphics(b: VBoxContainer) -> void:
	b.add_child(Kit.section(tr("Graphics")))
	var card := Kit.card("Card")
	var col := UIKit.vbox(12)
	card.add_child(col)
	var keys := ["low", "medium", "high"]
	var seg := UiSegmented.new()
	seg.name = "quality_segmented"
	seg.setup([tr("Low"), tr("Medium"), tr("High")], maxi(0, keys.find(GameManager.quality())), "Quality_")
	seg.changed_to.connect(func(i: int): GameManager.set_quality(keys[i]))
	col.add_child(seg)
	var hint := UIKit.wlabel(tr("Low turns off real-time shadows for older phones."), "CaptionMuted")
	col.add_child(hint)
	b.add_child(card)


func _language_row() -> Control:
	var card := Kit.card("Card")
	var row := GridContainer.new()
	row.columns = 2
	row.add_theme_constant_override("h_separation", 12)
	row.add_theme_constant_override("v_separation", 12)
	card.add_child(row)
	var cur := str(GameManager.settings.get("language", "auto"))
	var choices := [["auto", "Auto"]]
	for i in GameManager.LANGUAGES.size():
		choices.append([GameManager.LANGUAGES[i], GameManager.LANGUAGE_NAMES[i]])
	for q in choices:
		# E49: ja / ko / zh names are pictures (tools/make_lang_labels.py) unless the UI already runs in that language
		var pic: bool = q[0] in ["ja", "ko", "zh"] and GameManager.language() != q[0]
		var bt := UIKit.button("" if pic else q[1], "BtnGold" if cur == q[0] else "BtnDark", "", Vector2(0, 84), 24)
		bt.name = "lang_" + str(q[0])
		bt.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		bt.auto_translate = q[0] == "auto"
		if pic:
			UIKit.picture_caption(bt, "res://assets/ui/lang_%s.png" % q[0], 22)
		bt.pressed.connect(func():
			if cur != q[0]:
				GameManager.set_setting("language", q[0]))
		row.add_child(bt)
	return card


const HQ_COLORS := ["D4AF37", "C9CED6", "7B1E2B", "0F5B4D", "1B2A5C", "F2EAD3"]


## The paid HQ Signature look: gold sign on/off and six extra company colours (a locked swatch opens the shop).
func _hq_row() -> Control:
	var owned := Iap.has("hq_theme")
	var card := Kit.card("Card")
	card.name = "hq_signature"
	var col := UIKit.vbox(12)
	card.add_child(col)
	if owned:
		col.add_child(_hq_switch())
	else:
		col.add_child(UiRow.nav("lock", tr("HQ Signature"), "", func(): Iap.locked("hq_theme"), "neutral"))
	var grid := GridContainer.new()
	grid.columns = 6
	grid.add_theme_constant_override("h_separation", 10)
	grid.add_theme_constant_override("v_separation", 10)
	col.add_child(grid)
	for hex in HQ_COLORS:
		var sw := Button.new()
		sw.name = "hq_" + str(hex)
		sw.custom_minimum_size = Vector2(88, 88)
		sw.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		sw.focus_mode = Control.FOCUS_NONE
		var st := StyleBoxFlat.new()
		st.bg_color = Data.color(str(hex)) if owned else Data.color(str(hex)).darkened(0.45)
		st.set_corner_radius_all(26)
		if str(ProgressionManager.flags.get("company_color", "")) == str(hex):
			st.set_border_width_all(5)
			st.border_color = Color.WHITE
		for state in ["normal", "hover", "pressed"]:
			sw.add_theme_stylebox_override(state, st)
		sw.pressed.connect(func():
			if not owned:
				AudioManager.play("error", 0.0, -6.0)
				Iap.locked("hq_theme")
				return
			AudioManager.play("pop")
			ProgressionManager.flags["company_color"] = str(hex)
			GameManager.request_save()
			if UIManager.world:
				UIManager.world.set_company_color(Data.color(str(hex)))
			refresh())
		grid.add_child(sw)
	return card


func _hq_switch() -> Control:
	var tog := UiRow.toggle("house", tr("HQ Signature"), bool(GameManager.settings.get("hq_signature", true)), func(v: bool):
		GameManager.set_setting("hq_signature", v)
		_restyle_hq())
	tog.name = "hq_signature_toggle"
	return tog


func _restyle_hq() -> void:
	if UIManager.world and UIManager.world.hq:
		UIManager.world.hq.refresh_sign()
