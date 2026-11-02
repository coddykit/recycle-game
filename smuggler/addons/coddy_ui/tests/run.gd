extends SceneTree
## coddy_ui unit tests (headless). From the kit:   godot --headless --path games/tools/ui -s res://addons/coddy_ui/tests/run.gd
## From a game (after sync.sh, with the game's own fonts): godot --headless --path games/<game> -s res://addons/coddy_ui/tests/run.gd
## Prints "CODDY_UI_TESTS: n checks, 0 failures" and exits 1 on any failure.

const Tokens = preload("../ui_tokens.gd")
const UiKit = preload("../ui_kit.gd")
const Theme_ = preload("../ui_theme.gd")
const Icons = preload("../ui_icons.gd")
const Fonts = preload("../ui_fonts.gd")
const UiToast = preload("../ui_toast.gd")
const UiModal = preload("../ui_modal.gd")
const UiPager = preload("../ui_pager.gd")
const UiCoach = preload("../ui_coach.gd")
const UiSegmented = preload("../ui_segmented.gd")
const UiRow = preload("../ui_row.gd")
const UiScroll = preload("../ui_scroll.gd")
const UiReconcile = preload("../ui_reconcile.gd")
const UiShop = preload("../ui_shop.gd")
const UiLoading = preload("../ui_loading.gd")
const Suite = preload("touch_scroll_suite.gd")
const UiSettings = preload("../ui_settings.gd")
const UiLanguage = preload("../ui_language.gd")
const UiSafeArea = preload("../ui_safe_area.gd")
const UiBack = preload("../ui_back.gd")
const ROLES := ["background", "surface", "surface_variant", "surface_high", "surface_sunken", "outline", "on_surface",
	"on_surface_muted", "primary", "primary_pressed", "primary_lip", "primary_container", "on_primary", "accent",
	"accent_container", "on_accent", "success", "warning", "danger", "info", "scrim"]

var checks := 0
var failures := 0


func _initialize() -> void:
	_run.call_deferred()


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		print("FAIL UI ", label)


func _lum(c: Color) -> float:
	var ch := func(v: float) -> float: return v / 12.92 if v <= 0.03928 else pow((v + 0.055) / 1.055, 2.4)
	return 0.2126 * ch.call(c.r) + 0.7152 * ch.call(c.g) + 0.0722 * ch.call(c.b)


func contrast(a: Color, b: Color) -> float:
	var la := _lum(a)
	var lb := _lum(b)
	return (maxf(la, lb) + 0.05) / (minf(la, lb) + 0.05)


func _run() -> void:
	root.size = Vector2i(1280, 720)
	_tokens()
	_icons()
	_fonts()
	await _theme()
	_is_tap()
	await _toast()
	await _toast_translated_1027()
	await _side_cjk_1027()
	await _upper_headers_1027()
	await _toast_locale_1027()
	await _toast_guide_1027()
	await _toast_panels_1027()
	await _modal_columns_1027()
	await _row_subtitle_1027()
	await _refit_oscillation_1027()
	await _modal()
	await _back_1028()
	await _pager()
	await _pager_taps()
	await _pager_counts()
	await _pager_motion()
	await _pager_modal_card()
	await _pager_image()
	await _pager_long_text()
	_pager_cjk_tail()
	await _coach()
	await _closing()
	await _controls()
	await _word_fit()
	await _scroll()
	await _reconcile()
	await _list_cards()
	await _shop()
	await _loading()
	await _privacy()
	await _language()
	await _redesign_1026()
	_safe_area()
	await _safe_overlays()
	var suite := Suite.new(self)
	await suite.run_fixtures(["en", "de"])
	checks += suite.checks
	failures += suite.failures.size()
	print("CODDY_UI_TESTS: %d checks, %d failures" % [checks, failures])
	quit(0 if failures == 0 else 1)


func _tokens() -> void:
	check(Tokens.TOUCH.min >= int(48 * Tokens.DP) - 1, "touch target ≥ 48 dp (%d px)" % Tokens.TOUCH.min)
	check(Tokens.TYPE.body.size >= int(14 * Tokens.DP), "body ≥ 14 sp (%d px)" % Tokens.TYPE.body.size)
	check(Tokens.TYPE.caption.size >= int(12 * Tokens.DP), "caption ≥ 12 sp (%d px)" % Tokens.TYPE.caption.size)
	for name in Tokens.PALETTES:
		var p := Theme_.palette(name)
		var missing := []
		for r in ROLES:
			if not p.has(r):
				missing.append(r)
		check(missing.is_empty(), "%s: every colour role %s" % [name, missing])
		if not missing.is_empty():
			continue
		check(contrast(p.on_surface, p.surface) >= 7.0, "%s: text on surface ≥ 7:1" % name)
		check(contrast(p.on_surface_muted, p.surface_variant) >= 4.5, "%s: muted text ≥ 4.5:1 on raised cards" % name)
		check(contrast(p.on_primary, p.primary) >= 4.5, "%s: text on primary ≥ 4.5:1" % name)
		check(contrast(p.primary, p.surface) >= 4.5, "%s: primary as text ≥ 4.5:1" % name)
	var o := Theme_.palette("canal-control", {"primary": "ff0000"})
	check(o.primary == Color("ff0000") and o.surface == Theme_.palette("canal-control").surface, "per-game override changes only that role")


func _icons() -> void:
	var bad := []
	for n in Icons.names():
		var t := Icons.texture(n, 32, Color.WHITE)
		var img := t.get_image()
		var lit := false
		for y in range(0, img.get_height(), 2):
			for x in range(0, img.get_width(), 2):
				if img.get_pixel(x, y).a > 0.5:
					lit = true
					break
			if lit:
				break
		if img.get_width() != 64 or not lit:
			bad.append(n)
	check(bad.is_empty(), "every icon rasterises (supersampled 2x, visible strokes) %s" % [bad])
	var before := Icons.cached_count()
	Icons.texture("ship", 32, Color.WHITE)
	check(Icons.cached_count() == before, "icons are cached (no re-rasterising)")
	var sw := Icons.switch_1x(true, Color.ORANGE, Color.BLACK, Color.BLACK, Color.GRAY, Color.GRAY)
	check(sw.get_width() == 92 and sw.get_height() == 52, "switch picture is 92x52 at 1x")


func _fonts() -> void:
	if not ResourceLoader.exists(str(Fonts.paths().get("latin", ""))):
		print("SKIP fonts: this project bundles no fonts (run the tests from a game to check its font stack)")
		return
	var f := Fonts.stack(false, "en")
	var b := Fonts.stack(true, "en")
	var sys := false
	for leaf in Fonts.leaves(f) + Fonts.leaves(b):
		if leaf is FontFile and (leaf as FontFile).allow_system_fallback:
			sys = true
	check(not sys, "no OS font fallback anywhere in the stacks")
	check(b is FontVariation and (b as FontVariation).variation_embolden > 0.3, "bold = emboldened variation")
	check(Fonts.stack(false, "en") == f, "font stacks are cached")


func _theme() -> void:
	var t := UiKit.use("canal-control")
	check(t != null and t.default_font != null, "theme builds (bundled font, or the engine font in the kit project)")
	for v in ["PrimaryButton", "SecondaryButton", "GhostButton", "TonalButton", "DangerButton", "IconButton", "TabButton", "SegmentButton", "RowButton"]:
		check(t.is_type_variation(v, "Button"), "button variation " + v)
	for v in ["Card", "Sheet", "Glass", "Group", "Toast", "Bubble", "Track", "InfoCard"]:
		check(t.is_type_variation(v, "PanelContainer"), "panel variation " + v)
	for v in ["Display", "Headline", "Title", "Body", "Caption", "Overline"]:
		check(t.is_type_variation(v, "Label"), "label variation " + v)
	var host := Control.new()
	host.theme = t
	host.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(host)
	var plain := Button.new()
	plain.text = "Plain"
	host.add_child(plain)
	var prim := UiKit.button("Dispatch", "primary", "route")
	host.add_child(prim)
	var ib := UiKit.icon_button("settings", Callable(), "Settings")
	host.add_child(ib)
	await process_frame
	check(plain.get_combined_minimum_size().y >= Tokens.TOUCH.min - 1, "even a plain themed Button is ≥ 48 dp tall (%d)" % plain.get_combined_minimum_size().y)
	check(prim.size.y >= Tokens.TOUCH.min and ib.size.x >= Tokens.TOUCH.min, "kit buttons ≥ 48 dp")
	check(t.get_stylebox("scroll", "VScrollBar") is StyleBoxEmpty, "scroll bar track is invisible")
	check(t.get_stylebox("grabber", "VScrollBar").get_minimum_size().x <= 8.0, "scroll thumb is thin (≤ 8 px)")
	host.queue_free()
	await process_frame


## Kit.is_tap: one action per finger (W66 10-27) — the emulated twin of a touch / click never counts.
func _is_tap() -> void:
	var t := InputEventScreenTouch.new()
	t.pressed = true
	var m := InputEventMouseButton.new()
	m.button_index = MOUSE_BUTTON_LEFT
	m.pressed = true
	check(UiKit.is_tap(t) and UiKit.is_tap(m), "is_tap: a real touch / left click counts")
	var em := m.duplicate() as InputEventMouseButton
	em.device = InputEvent.DEVICE_ID_EMULATION
	var et := t.duplicate() as InputEventScreenTouch
	et.device = InputEvent.DEVICE_ID_EMULATION
	check(not UiKit.is_tap(em) and not UiKit.is_tap(et), "is_tap: the emulated twin (mouse from touch / touch from mouse) does not")
	var up := t.duplicate() as InputEventScreenTouch
	up.pressed = false
	check(UiKit.is_tap(up, true) and not UiKit.is_tap(up) and not UiKit.is_tap(t, true), "is_tap: press vs release")
	var r := m.duplicate() as InputEventMouseButton
	r.button_index = MOUSE_BUTTON_RIGHT
	check(not UiKit.is_tap(r), "is_tap: only the left button")


func _toast() -> void:
	var host := _host(Vector2i(1280, 720))
	var toast := UiToast.new()
	host.add_child(toast)
	toast.setup({"top": 120})
	toast.text = "Maintenance funded. Future incidents can be prevented."
	await process_frame
	check(toast.is_showing() and toast.text.begins_with("Maintenance"), "toast shows a card with the text")
	var r := toast.card().get_global_rect()
	check(r.size.x <= 761 and r.position.x >= 0 and r.end.x <= 1280, "toast stays within its max width")
	check(toast.card().theme_type_variation == "Toast", "toast has its card style (never a bare label)")
	toast.show_toast("Sehr lange deutsche Nachricht über die Wartung des Kanals, die auf zwei Zeilen umbrechen muss, damit alles passt.", "warning")
	await process_frame
	check(toast.card().get_global_rect().size.y < 200 and toast.card().get_global_rect().end.x <= 1280, "long German toast wraps to ≤ 2 lines inside the screen")
	toast.text = ""
	check(not toast.is_showing(), "empty text hides the toast")
	check(toast.mouse_filter == Control.MOUSE_FILTER_IGNORE and toast.card().mouse_filter == Control.MOUSE_FILTER_IGNORE, "toast never eats touches")
	host.queue_free()
	await process_frame


## W66 10-27 (skiresort: 45 language / message pairs cut with "…"): a game hands the toast an ENGLISH KEY and the label
## draws its translation — the toast sizes its text from the drawn text, so every message keeps its two lines.
const TOAST_KEYS := {
	"Power restored": {"de": "Stromversorgung wiederhergestellt", "es": "Suministro eléctrico restablecido",
		"fr": "Alimentation électrique rétablie", "pt_BR": "Fornecimento de energia restabelecido",
		"ru": "Электроснабжение восстановлено", "ja": "電力供給が復旧しました", "ko": "전력 공급이 복구되었습니다",
		"zh": "电力供应已恢复", "hi": "बिजली आपूर्ति बहाल हो गई"},
	"Parking full! Guests turn away. Buy parking in Upgrades.": {
		"de": "Parkplatz voll! Gäste fahren wieder weg. Kaufe Parkplätze unter Ausbau.",
		"es": "¡Aparcamiento lleno! Los clientes se van. Compra plazas en Mejoras.",
		"fr": "Parking plein ! Des clients repartent. Achetez des places dans Améliorations.",
		"pt_BR": "Estacionamento lotado! Clientes vão embora. Compre vagas em Melhorias.",
		"ru": "Парковка заполнена! Гости уезжают. Купите места в разделе «Улучшения».",
		"ja": "駐車場が満車です！お客様が帰っています。「改良」で駐車場を購入しましょう。",
		"ko": "주차장이 꽉 찼습니다! 손님이 돌아갑니다. 업그레이드에서 주차장을 구매하세요.",
		"zh": "停车场已满！游客正在离开。请在“升级”中购买停车位。",
		"hi": "पार्किंग भरी! मेहमान लौट रहे। अपग्रेड में पार्किंग लें।"},
}
const TEN_LANGS := ["en", "de", "es", "fr", "pt_BR", "ru", "ja", "ko", "zh", "hi"]


func _test_translations(table: Dictionary) -> Array:
	var made: Array = []
	for lang in TEN_LANGS:
		if lang == "en":
			continue
		var t := Translation.new()
		t.locale = lang
		for key in table:
			if (table[key] as Dictionary).has(lang):
				t.add_message(key, table[key][lang])
		TranslationServer.add_translation(t)
		made.append(t)
	return made


func _toast_translated_1027() -> void:
	var was := TranslationServer.get_locale()
	var made := _test_translations(TOAST_KEYS)
	var cut: Array = []
	for size in [Vector2i(720, 1280), Vector2i(1280, 720)]:
		var host := _host(size)
		var toast := UiToast.new()
		host.add_child(toast)
		toast.setup({"top": 120, "right": 1.0 if size.x < size.y else 0.62})
		for lang in TEN_LANGS:
			TranslationServer.set_locale(lang)
			for key in TOAST_KEYS:
				toast.show_toast(key, "warning")   # the key, as a game that leaves translation to the label does
				await process_frame
				var lab: Label = toast._label
				var drawn := lab.atr(key)
				if lang != "en" and drawn == key:
					cut.append("%s untranslated" % lang)
				# cut = more than two lines while the card is narrower than it may be (a text too long even at the full
				# width is the game's copy to shorten — skiresort's ToastFitTest — not a sizing bug)
				var full := minf(toast.max_width, toast.size.x - 32.0)
				if (lab.get_line_count() > 2 and toast.card().size.x < full - 2.0) or toast.card().get_global_rect().end.x > size.x:
					cut.append("%dx%d %s '%s' %d lines, card %.0f of %.0f px" % [size.x, size.y, lang, drawn, lab.get_line_count(), toast.card().size.x, full])
		host.queue_free()
		await process_frame
	check(cut.is_empty(), "10-27 toast sized from the drawn (translated) text: 2 messages x 10 languages x 2 screens fit two lines %s" % [cut.slice(0, 4)])
	for t in made:
		TranslationServer.remove_translation(t)
	TranslationServer.set_locale(was)


## W66 10-27 (Lifeguard ja "休/憩/4/0/秒" one glyph per line): a wrapping side button keeps two lines in every script —
## CJK breaks between characters, so its minimum width is the width two lines need, not one glyph.
func _side_cjk_1027() -> void:
	var host := _host(Vector2i(720, 1280))
	var col := VBoxContainer.new()
	col.custom_minimum_size.x = 508
	host.add_child(col)
	var rows := {"ja": ["ライフガード 1", "休憩 40秒"], "zh": ["救生员 1", "休息 40秒"], "ko": ["구조대원 1", "휴식 40초"],
		"de": ["Rettungsschwimmer 1", "Ausruhen 40\u00a0s"], "en": ["Lifeguard 1", "Rest 40\u00a0s"]}
	var made: Array = []
	for lang in rows:
		for kit_button in [true, false]:
			var c := UiKit.list_card("users", rows[lang][0] + " · a long title that takes the row", {"sub": ["tired 0%"], "badge": "Ready"})
			col.add_child(c)
			var b: Button = UiKit.button(rows[lang][1], "secondary") if kit_button else Button.new()
			if not kit_button:
				b.text = rows[lang][1]
			UiKit.list_card_side(c, b, true)
			made.append([lang, kit_button, b])
	for i in 6:
		await process_frame
	for m in made:
		var b: Button = m[2]
		var font: Font = b.get_theme_font("font")
		var fs := b.get_theme_font_size("font_size")
		var inner := b.size.x - UiKit._fit_chrome(b)
		var lines := UiKit.line_count(font, b.text, fs, inner)
		check(lines <= 2 and b.size.y <= Tokens.TOUCH.min + 1.0,
			"10-27 side button %s%s: <= 2 lines in the 48 dp rect (%d lines, %.0fx%.0f at %d px)" % [m[0], "" if m[1] else " (plain Button)", lines, b.size.x, b.size.y, fs])
		check(fs >= UiKit.FIT_TEXT_MIN, "10-27 side button %s: text stays >= %d px (%d)" % [m[0], UiKit.FIT_TEXT_MIN, fs])
	host.queue_free()
	await process_frame


## W66 10-27 (Pit Lane Boss: "RACE" drawn as "Race"): an upper-cased kit header is never auto-translated back through a
## CSV key that equals the upper-cased text; an all-caps key handed over as is still translates (as before).
func _upper_headers_1027() -> void:
	var was := TranslationServer.get_locale()
	var t := Translation.new()
	t.locale = "en"
	for kv in [["RACE", "Race"], ["WEEKEND", "Weekend"], ["NEW", "New"], ["UI_KIT_TEST_PIT", "Pit stop"]]:
		t.add_message(kv[0], kv[1])
	var it := Translation.new()
	it.locale = "it"
	it.add_message("WEEKEND", "Weekend")
	TranslationServer.add_translation(t)
	TranslationServer.add_translation(it)
	var host := _host(Vector2i(720, 1280))
	var bad: Array = []
	for lang in ["en", "it"]:
		TranslationServer.set_locale(lang)
		for key in ["RACE", "WEEKEND"]:
			var shown := TranslationServer.translate(key)   # the game's tr(): "Race" / "Weekend"
			var sec := UiKit.section(shown)
			host.add_child(sec)
			var pill := UiKit.badge(shown)
			host.add_child(pill)
			var pl: Label = pill.get_child(0)
			for l in [sec, pl]:
				if (l as Label).text != shown.to_upper() or (l as Label).atr((l as Label).text) != shown.to_upper():
					bad.append("%s %s -> '%s'" % [lang, key, (l as Label).atr((l as Label).text)])
	TranslationServer.set_locale("en")
	var coach := UiCoach.new()
	host.add_child(coach)
	coach.setup({"skip": "Skip"})
	coach.show_step("Tap Race.", "Weekend", Rect2())
	if coach.step_label.atr(coach.step_label.text) != "WEEKEND":
		bad.append("coach step -> '%s'" % coach.step_label.atr(coach.step_label.text))
	check(bad.is_empty(), "10-27 upper-cased headers (section, badge, coach step) stay upper case, never translated back %s" % [bad])
	var keyed := UiKit.section("UI_KIT_TEST_PIT")
	host.add_child(keyed)
	check(keyed.atr(keyed.text) == "Pit stop", "10-27 an all-caps key handed to a section still auto-translates (as before)")
	host.queue_free()
	await process_frame
	TranslationServer.remove_translation(t)
	TranslationServer.remove_translation(it)
	TranslationServer.set_locale(was)


func _modal() -> void:
	for size in [Vector2i(1280, 720), Vector2i(720, 1280)]:
		var host := _host(size)
		var m := UiModal.new()
		host.add_child(m)
		m.setup("Graphics for this device", {"icon": "gauge", "close": true})
		m.body.add_child(UiKit.label("Graphics start at Medium. You can raise quality in Settings.", "BodyMuted", true))
		m.add_action("Continue", "primary", Callable(), "Continue")
		await process_frame
		await process_frame
		var card := m.get_child(1) as Control
		var tag := "%dx%d" % [size.x, size.y]
		check(card.name == "Card" and m.get_child(0).name == "Scrim", tag + ": modal layout child 0 scrim, child 1 card")
		check(card.size.y < size.y * 0.5, tag + ": a short modal is content-sized (%d px tall), no empty box" % card.size.y)
		check(Rect2(Vector2.ZERO, Vector2(size)).encloses(card.get_global_rect()), tag + ": modal inside the screen")
		check(m.find_child("Continue", true, false) is Button and m.find_child("Close", true, false) is Button, tag + ": action + close found by name")
		var closed := [0]
		m.close_requested.connect(func() -> void: closed[0] += 1)
		(m.find_child("Close", true, false) as Button).pressed.emit()
		check(closed[0] == 1, tag + ": close button asks to close")
		if bool(m._opts.get("close", false)):   # a phone tap on the scrim closes ONCE (touch + Godot's emulated click)
			var shade_c := m.get_child(0) as Control
			var touch := InputEventScreenTouch.new()
			touch.pressed = true
			var emu := InputEventMouseButton.new()
			emu.button_index = MOUSE_BUTTON_LEFT
			emu.pressed = true
			emu.device = InputEvent.DEVICE_ID_EMULATION
			shade_c.gui_input.emit(touch)
			shade_c.gui_input.emit(emu)
			check(closed[0] == 2, tag + ": a phone tap on the scrim asks to close once (%d)" % (closed[0] - 1))
		for i in 30:
			m.body.add_child(UiKit.button("Row %d" % i))
		await process_frame
		await process_frame
		check(card.get_global_rect().size.y <= size.y - 47 and Rect2(Vector2.ZERO, Vector2(size)).encloses(card.get_global_rect()), tag + ": a long body scrolls inside a card that fits")
		check(m.scroll.get_v_scroll_bar().max_value > m.scroll.get_v_scroll_bar().page, tag + ": long body scrolls")
		check(m.sheet == (size.y > size.x), tag + ": portrait = bottom sheet, landscape = dialog")
		host.queue_free()
		await process_frame


func _pager() -> void:
	var pages := [{"title": "Welcome, canal controller", "body": "Ships wait at both ends of your canal.", "icon": "ship", "sats": ["coins", "anchor"]},
		{"title": "Wir zeigen dir jeden einzelnen Schritt", "body": "Tippe dorthin, wohin der Pfeil zeigt. Beim Lernen kann nichts schiefgehen.", "icon": "hand"},
		{"title": "Grow a busy harbor", "body": "Spend tolls on tugs and pilots.", "icon": "trending-up"}]
	for size in [Vector2i(1280, 720), Vector2i(1600, 720), Vector2i(720, 1280), Vector2i(720, 1560)]:
		var host := _host(size)
		var pager := UiPager.new()
		host.add_child(pager)
		pager.setup(pages, {"next": "Next", "last": "Let's go!", "skip": "Skip"})
		var done := [0, 0]
		pager.finished.connect(func() -> void: done[0] += 1)
		pager.skipped.connect(func() -> void: done[1] += 1)
		await process_frame
		await process_frame
		var tag := "%dx%d" % [size.x, size.y]
		var screen := Rect2(Vector2.ZERO, Vector2(size))
		var bad := ""
		for i in pages.size():
			pager.show_page(i, false)
			await process_frame
			for n in ["IntroNext", "IntroTitle", "IntroBody", "IntroDots"]:
				var c := pager.find_child(n, true, false) as Control
				if c == null or not screen.encloses(c.get_global_rect()):
					bad += "%s p%d; " % [n, i]
			if pager.find_children("*", "ScrollContainer", true, false).size() > 0:
				bad += "scroll container; "
		check(bad == "", tag + ": every page fits, no scroll view " + bad)
		check((pager.find_child("IntroNext", true, false) as Control).size.y >= Tokens.TOUCH.min, tag + ": Next ≥ 48 dp")
		pager.show_page(0, false)
		pager.next_page()
		check(pager.page == 1 and pager.dots.current == 1, tag + ": Next advances page and dots")
		pager.next_page()
		pager.next_page()
		check(done[0] == 1, tag + ": Next on the last page finishes")
		pager.skip()
		check(done[1] == 1, tag + ": Skip emits skipped")
		host.queue_free()
		await process_frame


## A host written like the onboarding.gd games (owner 10-23 "son buton 2 kere"): its own page counter, pager.finished →
## its next_page(), which steps the counter and re-shows that page. Before the kit kept `page` in step, the first
## `finished` sent the pager back a page and the last page needed a second tap.
class LegacyHost extends Control:
	var _pager: Control
	var page := 0
	var closed := 0
	var count := 3

	func next_page() -> void:
		if page < count - 1:
			page += 1
			if _pager != null and _pager.page != page:
				_pager.show_page(page)
			return
		_pager.visible = false
		closed += 1


const TAP_PAGES := [{"title": "Welcome, canal controller", "body": "Ships wait at both ends of your canal.", "icon": "ship", "sats": ["coins", "anchor"]},
	{"title": "Wir zeigen dir jeden Schritt", "body": "Tippe dorthin, wohin der Pfeil zeigt.", "icon": "hand", "sats": ["check"]},
	{"title": "Grow a busy harbor", "body": "Spend tolls on tugs and pilots.", "icon": "trending-up"}]


func _mouse(at: Vector2, down: bool) -> void:
	var m := InputEventMouseButton.new()
	m.button_index = MOUSE_BUTTON_LEFT
	m.pressed = down
	m.position = at
	m.global_position = at
	Input.parse_input_event(m)
	Input.flush_buffered_events()


## One tap per page, exactly one `finished` — with device touches (emulated mouse, as on Android) and with a mouse, in
## landscape and portrait, tapping at rest and DURING the page transition, with and without a legacy host.
func _pager_taps() -> void:
	Input.emulate_mouse_from_touch = true
	var s := Suite.new(self)
	for size in [Vector2i(1280, 720), Vector2i(720, 1280)]:
		for mode in ["touch", "mouse", "touch-fast", "legacy-touch", "legacy-fast"]:
			var host := _host(size)
			var parent: Control = host
			var legacy: LegacyHost = null
			if str(mode).begins_with("legacy"):
				legacy = LegacyHost.new()
				legacy.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
				host.add_child(legacy)
				parent = legacy
			var pager := UiPager.new()
			parent.add_child(pager)
			pager.setup(TAP_PAGES, {"next": "Next", "last": "Let's go!", "skip": "Skip"})
			if legacy != null:
				legacy._pager = pager
				pager.finished.connect(legacy.next_page)
			var done := [0]
			var seen: Array = []
			pager.finished.connect(func() -> void: done[0] += 1)
			pager.page_changed.connect(func(i: int) -> void: seen.append(i))
			for i in 30:   # the open animation ends
				await process_frame
			var fast: bool = str(mode).ends_with("fast")
			var tag := "%dx%d %s" % [size.x, size.y, mode]
			var taps := 0
			var per_tap_ok := true
			while taps < 6 and (done[0] == 0 if legacy == null else legacy.closed == 0):
				var before := pager.page
				var at := Suite.centre(pager.next_button)
				if mode == "mouse":
					_mouse(at, true)
					await process_frame
					_mouse(at, false)
					await process_frame
				else:
					await s.tap(at)
				taps += 1
				var finished_now: bool = done[0] > 0
				if not finished_now and pager.page != before + 1:
					per_tap_ok = false
				if not fast:
					for i in 20:
						await process_frame
			check(taps == TAP_PAGES.size(), tag + ": one tap per page, the last tap starts the game (%d taps for %d pages)" % [taps, TAP_PAGES.size()])
			check(per_tap_ok and done[0] == 1, tag + ": every tap moved exactly one page, `finished` exactly once (%d)" % done[0])
			check(seen == [1, 2], tag + ": pages shown in order, never back or skipped " + str(seen))
			if legacy != null:
				check(legacy.closed == 1 and legacy.page == 2, tag + ": the legacy host's counter follows the pager and it closes on the first last-page tap")
			host.queue_free()
			await process_frame
	check(s.failures.is_empty(), "pager taps: touch helper clean")


## Dots + counter always equal the real page count (owner 10-23: "4 sayfa varken 3 nokta"): setup → relabel → add page →
## relabel with fewer pages.
func _pager_counts() -> void:
	var host := _host(Vector2i(1280, 720))
	var pager := UiPager.new()
	host.add_child(pager)
	var pages: Array = TAP_PAGES.duplicate()
	pager.setup(pages, {"next": "Next", "last": "Go", "skip": "Skip"})
	await process_frame
	check(pager.dots.count == 3 and pager.counter.text == "1 / 3", "pager counts: setup 3 pages → 3 dots, 1 / 3")
	pager.relabel(pages.duplicate(), {"next": "Weiter", "last": "Los", "skip": "Überspringen"})
	check(pager.dots.count == 3 and pager.counter.text == "1 / 3", "pager counts: relabel keeps 3")
	pager.add_page({"title": "Fourth", "body": "One more page.", "icon": "star"})
	check(pager.pages.size() == 4 and pager.dots.count == 4 and pager.counter.text == "1 / 4", "pager counts: an added page → 4 dots, 1 / 4 (%d, %s)" % [pager.dots.count, pager.counter.text])
	for i in 3:
		pager.next_page()
	check(pager.page == 3 and pager.dots.current == 3 and pager.counter.text == "4 / 4" and pager.next_button.text == "Los", "pager counts: the 4th page is the last (dots, counter, Start)")
	pager.relabel(TAP_PAGES.slice(0, 2), {})
	check(pager.page == 1 and pager.dots.count == 2 and pager.dots.current == 1 and pager.counter.text == "2 / 2", "pager counts: fewer pages after relabel clamp page + dots")
	host.queue_free()
	await process_frame
	for size in [Vector2i(720, 1280)]:
		var h2 := _host(size)
		var p2 := UiPager.new()
		h2.add_child(p2)
		p2.setup(TAP_PAGES.duplicate(), {})
		p2.add_page({"title": "Fourth", "body": "More.", "icon": "star"})
		check(p2.dots.count == 4, "pager counts: portrait dots follow an added page")
		h2.queue_free()
		await process_frame


## Portrait onboarding = a modal card over the dimmed live game (owner 2026-10-23); landscape card unchanged.
func _pager_modal_card() -> void:
	var de := [{"title": "Wir zeigen dir jeden einzelnen Schritt", "body": "Tippe dorthin, wohin der Pfeil zeigt. Beim Lernen kann nichts schiefgehen und niemand verliert.", "icon": "hand", "sats": ["coins", "anchor"]},
		{"title": "Hafenverwaltung leicht gemacht", "body": "Überspringen ist jederzeit möglich, die Einführung kommt in den Einstellungen wieder.", "icon": "ship"},
		{"title": "Los geht's!", "body": "Kurz.", "icon": "star"}]
	for size in [Vector2i(720, 1280), Vector2i(720, 1560), Vector2i(1280, 720), Vector2i(1600, 720)]:
		for pages in [TAP_PAGES, de]:
			var host := _host(size)
			var live := ColorRect.new()   # the "live game" behind the pager
			live.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
			host.add_child(live)
			var pager := UiPager.new()
			host.add_child(pager)
			pager.setup(pages, {"next": "Weiter", "last": "Los geht's!", "skip": "Überspringen"})
			await process_frame
			await process_frame
			var tag := "%dx%d%s" % [size.x, size.y, " de" if pages == de else ""]
			var vp := Rect2(Vector2.ZERO, Vector2(size))
			var t0 := Time.get_ticks_msec()
			while pager.animating() and Time.get_ticks_msec() - t0 < 2000:   # measure the card at rest (not mid-settle)
				await process_frame
			var shade := pager.find_child("Shade", true, false) as Control
			check(shade != null and shade.get_global_rect().encloses(vp), tag + ": the scrim covers the whole viewport")
			var heights := []
			var bad := ""
			for i in pages.size():
				pager.show_page(i, false)
				await process_frame
				var r := pager.card.get_global_rect()
				heights.append(r.size.y)
				if pager.portrait:
					if not vp.encloses(r):
						bad += "card off screen p%d; " % i
					var rows := ["IntroNext", "IntroTitle", "IntroBody", "IntroDots", "IntroArt"]
					for n in rows:
						var c := pager.find_child(n, true, false) as Control
						if c == null or not r.encloses(c.get_global_rect()):
							bad += "%s outside the card p%d; " % [n, i]
					if pager.skip_button.visible:   # #105: Skip never covers the picture — above the card, or inside off the art
						var sk := pager.skip_button.get_global_rect()
						var above := sk.end.y <= r.position.y + 0.5 and sk.position.x >= r.position.x - 0.5 and sk.end.x <= r.end.x + 0.5
						if not vp.encloses(sk) or sk.intersects(pager.art.get_global_rect()) and above or not (above or r.encloses(sk)):
							bad += "IntroSkip misplaced p%d; " % i
						if above and sk.intersects(pager.art.get_global_rect()):
							bad += "IntroSkip over the picture p%d; " % i
					var t := pager.title_label.get_global_rect()
					var b := pager.body_label.get_global_rect()
					if pager.title_label.get_line_count() * pager.title_label.get_line_height() > t.size.y + 1.0 or pager.body_label.get_line_count() * pager.body_label.get_line_height() > b.size.y + 1.0:
						bad += "text clipped p%d (%d/%d lines, %s %s); " % [i, pager.title_label.get_line_count(), pager.body_label.get_line_count(), t.size, b.size]
					if pager.art.size.y < 110.0:
						bad += "art too small (%d) p%d; " % [pager.art.size.y, i]
					if pager.card.size.y + 0.5 < pager.card.get_combined_minimum_size().y:
						bad += "card below its content minimum p%d; " % i
			check(bad == "", tag + ": portrait card content fits " + bad)
			if pager.portrait:
				var r := pager.card.get_global_rect()
				check(absf(r.size.x / vp.size.x - 0.88) <= 0.04, tag + ": card ≈ 88 %% of the width (%.3f)" % (r.size.x / vp.size.x))
				check(r.size.y <= vp.size.y * 0.62, tag + ": card ≤ 62 %% of the height (%.3f)" % (r.size.y / vp.size.y))
				check(r.end.y <= vp.size.y - 32.0 and r.end.y >= vp.size.y * 0.8, tag + ": card sits near the bottom with a safe margin (bottom %d of %d)" % [r.end.y, vp.size.y])
				check(absf(r.get_center().x - vp.size.x / 2.0) <= 1.0, tag + ": card centred sideways")
				check(r.position.y >= vp.size.y * 0.3, tag + ": the dimmed game stays visible above the card (top %d)" % r.position.y)
				check(heights.max() - heights.min() <= 1.0, tag + ": the card keeps its height across pages (±1 px) " + str(heights))
			else:
				var want_w := minf(1120.0, size.x - 48.0)
				var want_h := minf(540.0, size.y - 40.0)
				var r := pager.card.get_global_rect()
				check(absf(r.size.x - want_w) <= 1.0 and absf(r.size.y - want_h) <= 1.0 and absf(r.get_center().x - size.x / 2.0) <= 1.0 and absf(r.get_center().y - size.y / 2.0) <= 1.0,
					tag + ": landscape card unchanged (%s of %s)" % [r, size])
			# one tap per page, finished exactly once
			var done := [0, 0]
			pager.finished.connect(func() -> void: done[0] += 1)
			pager.page_changed.connect(func(_i: int) -> void: done[1] += 1)
			pager.show_page(0, false)
			done[1] = 0
			for i in pages.size():
				pager.next_button.pressed.emit()
			check(done[0] == 1 and done[1] == pages.size() - 1, tag + ": one tap per page, finished once (%s)" % [done])
			host.queue_free()
			await process_frame


## page["image"] (Texture2D or res:// path): a rounded cover picture in the illustration area instead of icon + satellites;
## a missing path falls back to the icon composition; the card, one-tap and motion behave the same.
func _pager_image() -> void:
	var img := Image.create(400, 200, false, Image.FORMAT_RGBA8)
	img.fill(Color(0.9, 0.5, 0.1))
	var tex := ImageTexture.create_from_image(img)
	var pages := [{"title": "Hero frame", "body": "A real picture of the game.", "icon": "ship", "sats": ["coins", "anchor"], "image": tex},
		{"title": "Icon page", "body": "No picture here.", "icon": "hand", "sats": ["coins", "check"]},
		{"title": "Broken path", "body": "Falls back to the icon.", "icon": "star", "sats": ["check"], "image": "res://does/not/exist.png"}]
	for size in [Vector2i(720, 1280), Vector2i(720, 1560), Vector2i(1280, 720)]:
		var host := _host(size)
		var pager := UiPager.new()
		host.add_child(pager)
		pager.setup(pages, {"next": "Next", "last": "Go", "skip": "Skip"})
		var done := [0, 0]
		pager.finished.connect(func() -> void: done[0] += 1)
		pager.page_changed.connect(func(_i: int) -> void: done[1] += 1)
		await process_frame
		await process_frame
		var tag := "%dx%d" % [size.x, size.y]
		var t0 := Time.get_ticks_msec()
		while pager.animating() and Time.get_ticks_msec() - t0 < 2000:
			await process_frame
		var card := pager.card.get_global_rect()
		var rects := []
		var has := []
		for i in pages.size():
			pager.show_page(i, false)
			await process_frame
			has.append(pager.art.has_picture())
			rects.append(pager.art.get_global_rect())
			if pager.portrait and i == 0:
				var skip_bg := pager.skip_button.get_theme_stylebox("normal") as StyleBoxFlat
				check(skip_bg != null and skip_bg.bg_color.a >= 0.95, tag + ": Skip has a readable solid background over a hero")
			elif i == 1:
				check(pager.skip_button.theme_type_variation == "GhostButton", tag + ": icon page restores the usual Skip style")
		check(has == [true, false, false], tag + ": picture only on the page with a usable image " + str(has))
		check(card.encloses(rects[0]) and rects[0].size.x > 100.0 and rects[0].size.y > 100.0, tag + ": the picture fills the illustration area inside the card")
		check(rects[0] == rects[1] and rects[1] == rects[2] and pager.card.get_global_rect() == card, tag + ": picture, icon and fallback pages share one layout")
		# the transition into / out of a picture page ends, and reduce motion snaps it
		pager.show_page(1, false)
		pager.next_page()
		pager.show_page(0)
		check(pager.animating() and pager.art.has_picture(), tag + ": a picture page comes in with a transition")
		t0 = Time.get_ticks_msec()
		while pager.animating() and Time.get_ticks_msec() - t0 < 2000:
			await process_frame
		check(not pager.animating() and pager.art._zoom == 1.0 and pager.art._enter == 1.0, tag + ": the picture settles at rest")
		UiKit.reduce_motion = true
		pager.show_page(1, false)
		pager.show_page(0)
		check(not pager.animating() and pager.art._zoom == 1.0, tag + ": reduce motion snaps a picture page")
		UiKit.reduce_motion = false
		# mixed pages: one tap per page, finished once
		pager.show_page(0, false)
		done[1] = 0
		for i in pages.size():
			pager.next_button.pressed.emit()
		check(done[0] == 1 and done[1] == pages.size() - 1, tag + ": mixed picture / icon pages: one tap per page, finished once (%s)" % [done])
		host.queue_free()
		await process_frame


## Swipes, transitions that end, reduce motion, the close fade and nothing allocated per frame at rest.
func _pager_motion() -> void:
	Input.emulate_mouse_from_touch = true
	var s := Suite.new(self)
	for size in [Vector2i(1280, 720), Vector2i(720, 1280)]:
		var tag := "%dx%d" % [size.x, size.y]
		var host := _host(size)
		var pager := UiPager.new()
		host.add_child(pager)
		pager.setup(TAP_PAGES, {"next": "Next", "last": "Let's go!", "skip": "Skip"})
		var done := [0]
		pager.finished.connect(func() -> void: done[0] += 1)
		await process_frame
		await process_frame
		check(pager.animating(), tag + ": the pager opens with a transition")
		var t0 := Time.get_ticks_msec()
		while pager.animating() and Time.get_ticks_msec() - t0 < 2000:
			await process_frame
		check(not pager.animating() and pager.card.modulate.a == 1.0 and pager.card.scale == Vector2.ONE, tag + ": the open transition ends at rest")
		var mid := Suite.centre(pager.title_label)
		await s.swipe(mid, mid - Vector2(260, 0), 6, true)
		await process_frame
		check(pager.page == 1, tag + ": a swipe left turns to the next page (%d)" % pager.page)
		await s.swipe(mid, mid + Vector2(260, 0), 6, true)
		await process_frame
		check(pager.page == 0, tag + ": a swipe right goes back (%d)" % pager.page)
		await s.swipe(mid, mid - Vector2(20, 260), 6, true)
		await process_frame
		check(pager.page == 0, tag + ": a vertical drag does not turn the page")
		# a game that runs its own swipe (moneytycoon: card.gui_input) never makes one gesture turn two pages
		var own := func(e: InputEvent) -> void:
			if e is InputEventMouseButton and not e.pressed and pager.page < 2:
				pager.next_page()
		pager.card.gui_input.connect(own)
		await s.swipe(mid, mid - Vector2(260, 0), 6, true)
		await process_frame
		check(pager.page == 1, tag + ": game swipe + kit swipe = one page (%d)" % pager.page)
		pager.card.gui_input.disconnect(own)
		pager.show_page(2, false)
		await s.swipe(mid, mid - Vector2(260, 0), 6, true)
		await process_frame
		check(done[0] == 0 and pager.page == 2, tag + ": a swipe on the last page never starts the game")
		# transitions end; the last page's button is the call to action
		pager.show_page(1, false)
		pager.next_page()
		check(pager.next_button.text == "Let's go!" and not pager.skip_button.visible, tag + ": last page: Start, Skip gone at once")
		check(pager.title_label.modulate.a < 1.0 and pager.art.animating(), tag + ": the last page comes in with a transition")
		t0 = Time.get_ticks_msec()
		while pager.animating() and Time.get_ticks_msec() - t0 < 2000:
			await process_frame
		check(not pager.animating() and pager.title_label.modulate.a == 1.0 and pager.body_label.modulate.a == 1.0
			and pager._slide.get_theme_constant("margin_left") == 0 and pager.next_button.scale == Vector2.ONE, tag + ": the page transition + CTA end at rest")
		var Icons = load("res://addons/coddy_ui/ui_icons.gd")
		check(pager.next_button.icon == Icons.texture(UiPager.LAST_ICON, 30, Color.WHITE), tag + ": the last button shows the play icon")
		check(pager.dots.current == 2 and pager.dots._t == 1.0, tag + ": dots settled on the last page")
		# at rest nothing is made per frame
		var objs := Performance.get_monitor(Performance.OBJECT_COUNT)
		for i in 30:
			await process_frame
		check(Performance.get_monitor(Performance.OBJECT_COUNT) <= objs, tag + ": no objects made per frame at rest (%d → %d)" % [objs, Performance.get_monitor(Performance.OBJECT_COUNT)])
		# close: the game hides the pager → an input-transparent copy fades out and is freed
		pager.finished.connect(func() -> void: pager.visible = false)
		pager.next_page()
		var ghost := host.find_child("PagerClosing", false, false) as Control
		check(done[0] == 1 and ghost != null and ghost.mouse_filter == Control.MOUSE_FILTER_IGNORE, tag + ": closing fades a copy of the page over the game")
		var eats := false
		if ghost != null:
			for c in ghost.find_children("*", "Control", true, false):
				if (c as Control).mouse_filter != Control.MOUSE_FILTER_IGNORE:
					eats = true
		check(not eats, tag + ": the closing copy never takes a tap")
		for i in 3:
			await create_timer(0.15).timeout
		check(host.find_child("PagerClosing", false, false) == null, tag + ": the closing copy is gone after the fade")
		# reduce motion: everything snaps
		UiKit.reduce_motion = true
		pager.visible = true
		pager.show_page(0, false)
		await process_frame
		pager.next_page()
		check(not pager.animating() and pager.title_label.modulate.a == 1.0, tag + ": reduce motion snaps page changes")
		pager.next_page()
		pager.next_page()
		check(host.find_child("PagerClosing", false, false) == null, tag + ": reduce motion: no closing fade")
		UiKit.reduce_motion = false
		host.queue_free()
		await process_frame
	check(s.failures.is_empty(), "pager motion: touch helper clean")


func _coach() -> void:
	for size in [Vector2i(1280, 720), Vector2i(1600, 720), Vector2i(720, 1280)]:
		var host := _host(size)
		var coach := UiCoach.new()
		host.add_child(coach)
		coach.setup({"skip": "Skip guide", "hint": "Tap the highlighted button"})
		await process_frame
		var bad := []
		var screen := Rect2(Vector2.ZERO, Vector2(size)).grow(-15)
		for gx in 5:
			for gy in 5:
				var spot := Rect2(Vector2(40 + gx * (size.x - 360) / 4.0, 40 + gy * (size.y - 160) / 4.0), Vector2(280, 84))
				coach.show_step("Tap Dispatch ready convoy. It sends every ship that can go at once.", "Step 12 of 12", spot)
				var b := coach.bubble_rect()
				if not screen.encloses(b) or b.intersects(coach.hole):
					bad.append("%s→%s" % [spot.position, b])
		check(bad.is_empty(), "%dx%d: the coach bubble never covers the spotlight and stays on screen %s" % [size.x, size.y, bad.slice(0, 3)])
		# avoid rects (a map marker the step text points at): the bubble dodges them when any spot can, else ignores them
		if size.x > size.y:
			var spot2 := Rect2(Vector2(size.x * 0.5, size.y - 130), Vector2(280, 84))
			coach.avoid = [] as Array[Rect2]
			coach.show_step("Tap Send bus. The red arrow on the map shows where it goes.", "Step 2 of 7", spot2)
			var natural := coach.bubble_rect()
			coach.avoid = [Rect2(natural.get_center() - Vector2(20, 20), Vector2(40, 40))] as Array[Rect2]   # something under the natural spot
			coach.show_step("Tap Send bus. The red arrow on the map shows where it goes.", "Step 2 of 7", spot2)
			var dodged := coach.bubble_rect()
			check(not dodged.intersects(coach.avoid[0]) and not dodged.intersects(coach.hole) and screen.encloses(dodged),
				"%dx%d: the coach bubble dodges an avoid rect (%s -> %s)" % [size.x, size.y, natural, dodged])
			coach.avoid = [Rect2(Vector2.ZERO, Vector2(size))] as Array[Rect2]   # cannot be dodged: placed as without it
			coach.show_step("Tap Send bus.", "Step 2 of 7", spot2)
			check(screen.encloses(coach.bubble_rect()) and not coach.bubble_rect().intersects(coach.hole), "%dx%d: an avoid rect that covers everything is ignored, the hole still is not" % [size.x, size.y])
			coach.avoid = [] as Array[Rect2]
		coach.show_step("Ships are on their way.", "", Rect2())
		check(screen.encloses(coach.bubble_rect()) and not coach.hole.has_area(), "%dx%d: a waiting step has no spotlight" % [size.x, size.y])
		host.queue_free()
		await process_frame


## #100 (C2): a pager / coach resized or placed while it leaves the tree (closing a screen, quit) must not touch the
## viewport — before the guard UiPager._place() raised "!is_inside_tree()" here and the game's verify exited 1.
func _closing() -> void:
	var host := _host(Vector2i(1280, 720))
	var pager := UiPager.new()
	host.add_child(pager)
	pager.setup([{"title": "One", "body": "Body", "icon": "ship"}, {"title": "Two", "body": "Body", "icon": "hand"}],
		{"next": "Next", "last": "Go", "skip": "Skip"})
	var coach := UiCoach.new()
	host.add_child(coach)
	coach.setup({"skip": "Skip guide", "hint": "Tap"})
	await process_frame
	coach.show_step("Tap here", "Step 1 of 2", Rect2(100, 100, 200, 84))
	await process_frame
	var card_pos: Vector2 = pager.card.position
	host.remove_child(pager)
	host.remove_child(coach)
	pager.size = Vector2(900, 500)     # emits resized outside the tree
	pager._place()
	pager._on_resized()
	coach.size = Vector2(900, 500)
	coach._place()
	check(not pager.is_inside_tree() and pager.card.position == card_pos, "pager: resize / place outside the tree is a no-op (no viewport call)")
	host.add_child(pager)
	host.add_child(coach)
	await process_frame
	pager._place()
	check(pager.card.size.x > 0.0, "pager: placed again once back in the tree")
	pager.queue_free()
	coach.queue_free()
	host.queue_free()
	await process_frame


func _controls() -> void:
	var host := _host(Vector2i(1280, 720))
	var seg := UiSegmented.new()
	seg.custom_minimum_size.x = 520
	host.add_child(seg)
	seg.setup(["Niedrig", "Mittel", "Hoch"], 1, "Quality_")
	var got := [-1]
	seg.changed_to.connect(func(i: int) -> void: got[0] = i)
	await process_frame
	(seg.find_child("Quality_2", true, false) as Button).pressed.emit()
	check(got[0] == 2 and seg.selected == 2 and (seg.buttons[2] as Button).button_pressed and not (seg.buttons[1] as Button).button_pressed, "segmented: one option at a time, emits its index")
	check(seg.size.y >= Tokens.TOUCH.min - 1, "segmented control ≥ 48 dp (%d)" % seg.size.y)
	# Canal Control 1.1.8: disabled options still show the selection (a dimmed selected fill, the others stay empty)
	for b in seg.buttons:
		(b as Button).disabled = true
	await process_frame
	var fill := func(i: int) -> float:
		var sb := (seg.buttons[i] as Button).get_theme_stylebox("disabled") as StyleBoxFlat
		return sb.bg_color.a if sb != null else 0.0
	var on_a: float = ((seg.buttons[2] as Button).get_theme_stylebox("pressed") as StyleBoxFlat).bg_color.a
	check(fill.call(2) > 0.3 * on_a and fill.call(2) < on_a and fill.call(0) < 0.05 and fill.call(1) < 0.05, "segmented disabled: the selected option keeps a dimmed fill (%.2f of %.2f), the others none" % [fill.call(2), on_a])
	seg.select(0, false)
	check(fill.call(0) > 0.3 * on_a and fill.call(2) < 0.05, "segmented disabled: the dimmed fill follows select()")
	var fresh := UiSegmented.new()
	fresh.setup(["N", "S"], 1)
	for b in fresh.buttons:
		(b as Button).disabled = true
	host.add_child(fresh)   # built and selected off-tree: marked once the theme resolves
	await process_frame
	var fresh_sb := (fresh.buttons[1] as Button).get_theme_stylebox("disabled") as StyleBoxFlat
	check(fresh_sb != null and fresh_sb.bg_color.a > 0.3 * on_a, "segmented disabled: a control set up before entering the tree shows its selection too")
	for b in seg.buttons:
		(b as Button).disabled = false
	var g := UiKit.group()
	g.position = Vector2(0, 120)
	g.custom_minimum_size.x = 560
	host.add_child(g)
	var flips := []
	var tg: Button = UiRow.toggle("music", "Music", true, func(v: bool) -> void: flips.append(v))
	UiKit.group_add(g, tg)
	var nav: Button = UiRow.nav("globe", "Language", "Deutsch", Callable())
	UiKit.group_add(g, nav)
	await process_frame
	tg.pressed.emit()
	tg.toggled.emit(not tg.on)
	check(flips == [false] and not tg.on, "switch row: a tap flips it and reports the new state")
	check(tg.size.y >= Tokens.TOUCH.min and nav.size.y >= Tokens.TOUCH.min, "rows ≥ 48 dp")
	check(nav.find_child("Subtitle", true, false).text == "Deutsch" and nav.title == "Language", "nav row shows title + value")
	var dots := UiKit.dots(3, 1)
	host.add_child(dots)
	check(dots.get_combined_minimum_size().x > 50, "step dots size themselves")
	host.queue_free()
	await process_frame


## #98: kit buttons / chips never split a word mid-letter — 10 languages' long button words × 3 widths: wrapped WORD_SMART,
## the font steps down (≥ 18 px) until the longest word fits, a squeezing container cannot go below it; a clipped
## one-line button shrinks until its longest line is whole.
const FIT_WORDS := {"en": "Notifications now", "de": "Taschentuchstand aufbauen", "ru": "Бесплатный чай (₺20)",
	"fr": "Remplissage rapide", "es": "Herramientas nuevas", "pt": "Configurações gerais", "tr": "Gönderilemeyenler var",
	"ja": "無料のお茶を出す", "ko": "회전초밥집 열기", "hi": "सूचनाएँ बंद करें"}


func _word_fit() -> void:
	var host := _host(Vector2i(1280, 720))
	for lang in FIT_WORDS:
		var text: String = FIT_WORDS[lang]
		for width in [150, 220, 320]:
			for icon in ["", "hand"]:
				var box := VBoxContainer.new()
				box.position = Vector2(0, 0)
				box.size = Vector2(width, 400)
				host.add_child(box)
				var b: Button = UiKit.button(text, "primary", icon)
				b.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
				box.add_child(b)
				for i in 3:
					await process_frame
				var size := UiKit.word_fit(b)
				await process_frame
				var font: Font = b.get_theme_font("font")
				var inner := b.size.x - UiKit._fit_chrome(b)
				var widest := UiKit._widest(font, UiKit.fit_words(text), b.get_theme_font_size("font_size"))
				check(size >= UiKit.FIT_FLOOR and b.get_theme_font_size("font_size") >= UiKit.FIT_FLOOR, "word fit %s %d%s: ≥ %d px (%d)" % [lang, width, icon, UiKit.FIT_FLOOR, size])
				# Latin / Cyrillic: always whole. Devanagari / CJK: whole, or (only when it does not fit even at the floor)
				# a grapheme break is allowed — then the font must be at the floor.
				var fs := b.get_theme_font_size("font_size")
				var whole := widest <= inner + 0.5
				var allowed := whole or (fs == UiKit.FIT_FLOOR and UiKit.grapheme_breakable(text))
				check(allowed, "word fit %s %d%s: longest word whole (%.0f ≤ %.0f px at %d)" % [lang, width, icon, widest, inner, fs])
				if lang in ["en", "de", "ru", "fr", "es", "pt", "tr"]:
					check(whole, "word fit %s %d%s: a Latin/Cyrillic word is never split" % [lang, width, icon])
				box.queue_free()
	# squeeze: an HBox gives a wrapping button no width of its own — the kit minimum keeps its longest word whole at 18
	var row := HBoxContainer.new()
	row.size = Vector2(200, 100)
	host.add_child(row)
	var filler := Label.new()
	filler.text = "x"
	filler.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(filler)
	var sq: Button = UiKit.button("Taschentuchstand", "primary", "hand")
	sq.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	row.add_child(sq)
	for i in 3:
		await process_frame
	UiKit.word_fit(sq)
	await process_frame
	var sq_w := UiKit._widest(sq.get_theme_font("font"), PackedStringArray(["Taschentuchstand"]), sq.get_theme_font_size("font_size"))
	check(sq_w <= sq.size.x - UiKit._fit_chrome(sq) + 0.5, "word fit: a squeezing HBox never splits the word (%.0f in %.0f)" % [sq_w, sq.size.x])
	row.queue_free()
	# clipped one-line (fixed-width serve button): the longest line shrinks until whole
	var one := VBoxContainer.new()
	host.add_child(one)
	var clip: Button = UiKit.button("Обслужить · в очереди: 5\nБыстро: обслужите сейчас и получите чаевые!", "primary", "hand")
	clip.clip_text = true
	one.add_child(clip)
	var long_line := UiKit._widest(clip.get_theme_font("font"), clip.text.split("\n"), 22)
	one.size = Vector2(ceilf(long_line + UiKit._fit_chrome(clip) + 2.0), 200)   # fits at 22 px, not at the kit's 26
	for i in 3:
		await process_frame
	var cs := UiKit.word_fit(clip)
	var line_w := UiKit._widest(clip.get_theme_font("font"), clip.text.split("\n"), clip.get_theme_font_size("font_size"))
	check(cs >= 21 and cs <= 22 and line_w <= clip.size.x - UiKit._fit_chrome(clip) + 0.5, "clipped one-line button shrinks to a whole line (%d px)" % cs)
	var tight: Button = UiKit.button("Serve the next guest right now for a generous tip today!", "primary")
	tight.clip_text = true
	tight.custom_minimum_size.x = 200
	var tight_box := HBoxContainer.new()
	tight_box.size = Vector2(200, 90)
	host.add_child(tight_box)
	tight_box.add_child(tight)
	for i in 3:
		await process_frame
	UiKit.word_fit(tight)
	check(tight.get_theme_font_size("font_size") == UiKit.FIT_TEXT_MIN, "a clipped line never goes below the 21 px text minimum (%d)" % tight.get_theme_font_size("font_size"))
	# a transient 8 px layout pass does not shrink anything (bureaucracy clerk buttons were left at 18 px)
	var row8 := HBoxContainer.new()
	host.add_child(row8)
	var mv: Button = UiKit.button("Stamp", "tonal")
	mv.clip_text = true
	mv.custom_minimum_size.x = 1
	mv.add_theme_font_size_override("font_size", 21)
	row8.add_child(mv)
	mv.size = Vector2(8, 84)
	UiKit.word_fit(mv)
	row8.size = Vector2(150, 84)
	mv.size = Vector2(150, 84)
	UiKit.word_fit(mv)
	check(mv.get_theme_font_size("font_size") == 21, "a transient 8 px pass leaves the game's 21 px (%d)" % mv.get_theme_font_size("font_size"))
		# C2 (#101): the fit's own font change relayouts the button synchronously (narrower); the cache key must hold the
	# width that was measured, not the width after the apply — else the settled, narrower width is "already fitted"
	# and the word stays too big (cinnamon "Notifications" 129 > 114 px at 24)
	var host2 := Control.new()
	host2.size = Vector2(1280, 720)
	one.add_child(host2)
	var rb: Button = UiKit.button("Taschentuchstand", "primary")
	rb.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	host2.add_child(rb)
	await process_frame
	var rfont: Font = rb.get_theme_font("font")
	var rchrome := UiKit._fit_chrome(rb)
	var w26 := rfont.get_string_size("Taschentuchstand", HORIZONTAL_ALIGNMENT_LEFT, -1, 26).x
	var w1 := ceilf(rchrome + w26 * 0.93)
	var w2 := ceilf(rchrome + w26 * 0.85)
	var shrink_once := [true]
	rb.theme_changed.connect(func() -> void:
		if shrink_once[0]:
			shrink_once[0] = false
			rb.size.x = w2)   # the relayout a container does when the font changes
	rb.size = Vector2(w1, 84)
	UiKit.word_fit(rb)
	rb.size = Vector2(maxf(w2, rb.get_combined_minimum_size().x), 84)
	UiKit.word_fit(rb)
	var rw := rfont.get_string_size("Taschentuchstand", HORIZONTAL_ALIGNMENT_LEFT, -1, rb.get_theme_font_size("font_size")).x
	check(rw <= rb.size.x - rchrome + 0.5, "word fit cache: refits after its own font change narrowed the button (%.0f ≤ %.0f px at %d)" % [rw, rb.size.x - rchrome, rb.get_theme_font_size("font_size")])
	host2.queue_free()
	# a short text keeps the kit size (no shrinking for nothing), a game's own size is the start size
	var short: Button = UiKit.button("OK", "primary")
	short.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	short.add_theme_font_size_override("font_size", 22)
	one.add_child(short)
	for i in 3:
		await process_frame
	var ss := UiKit.word_fit(short)
	check(ss == 22 and short.get_theme_font_size("font_size") == 22, "a word that fits keeps the game's size (%d, %d)" % [ss, short.get_theme_font_size("font_size")])
	# chip label (wrapping) uses the same fit
	var chip := UiKit.chip("hand", "Taschentuchstand")
	var cl: Label = chip.find_child("Text", true, false)
	cl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	cl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	chip.custom_minimum_size.x = 160
	one.add_child(chip)
	for i in 3:
		await process_frame
	UiKit.word_fit(cl)
	await process_frame
	var cw := UiKit._widest(cl.get_theme_font("font"), PackedStringArray(["Taschentuchstand"]), cl.get_theme_font_size("font_size"))
	check(cl.get_theme_font_size("font_size") >= UiKit.FIT_FLOOR and cw <= cl.size.x + 0.5, "chip text: whole word (%.0f in %.0f)" % [cw, cl.size.x])
	host.queue_free()
	await process_frame


func _scroll() -> void:
	var host := _host(Vector2i(1280, 720))
	var parts := UiScroll.list()
	var sc: ScrollContainer = parts[0]
	sc.size = Vector2(500, 400)
	host.add_child(sc)
	(parts[1] as VBoxContainer).add_child(UiKit.button("Only one row"))
	await process_frame
	await process_frame
	check(not sc.get_v_scroll_bar().visible, "content that fits shows no scroll bar at all")
	for i in 20:
		(parts[1] as VBoxContainer).add_child(UiKit.button("Row %d" % i))
	await process_frame
	await process_frame
	check(sc.get_v_scroll_bar().visible and sc.get_v_scroll_bar().max_value > sc.get_v_scroll_bar().page, "overflowing content scrolls")
	check(root.has_meta("coddy_touch_scroll") and is_instance_valid(root.get_meta("coddy_touch_scroll")), "a kit list wires finger scrolling on its viewport")
	var fade: Node = sc.get_node("ScrollFade")
	for i in 5:
		await process_frame
	check(fade.thumb_alpha() <= 0.01, "an overflowing list shows no thumb until it scrolls (%.2f)" % fade.thumb_alpha())
	sc.scroll_vertical = 120
	await process_frame
	check(fade.thumb_alpha() >= 0.99, "the thumb appears while the list scrolls")
	await create_timer(1.5).timeout
	check(fade.thumb_alpha() <= 0.01, "and fades out after it stops (%.2f)" % fade.thumb_alpha())
	# the content shrinking under a still list clamps the position (value_changed) — that is no scroll (Parts Rush 10-28)
	var vbar := sc.get_v_scroll_bar()
	sc.scroll_vertical = int(vbar.max_value)
	await create_timer(1.5).timeout
	var at_end := vbar.value
	for i in 3:
		var row: Node = (parts[1] as VBoxContainer).get_child((parts[1] as VBoxContainer).get_child_count() - 1)
		(parts[1] as VBoxContainer).remove_child(row)
		row.free()
	await process_frame
	await process_frame
	check(vbar.value < at_end - 0.5, "shorter content clamps a list scrolled to its end (%.0f → %.0f)" % [at_end, vbar.value])
	check(fade.thumb_alpha() <= 0.01, "and the clamp shows no thumb (%.2f)" % fade.thumb_alpha())
	sc.scroll_vertical = 40
	await process_frame
	check(fade.thumb_alpha() >= 0.99, "a real scroll after the clamp still shows the thumb")
	host.queue_free()
	await process_frame


func _reconcile() -> void:
	var a := VBoxContainer.new()
	var b1 := Button.new()
	b1.name = "Buy"
	b1.text = "Invest ◆100"
	a.add_child(b1)
	var fresh := VBoxContainer.new()
	var b2 := Button.new()
	b2.name = "Buy"
	b2.text = "Invest ◆150"
	fresh.add_child(b2)
	check(UiReconcile.apply(a, fresh) == "patched" and a.get_child(0) == b1 and b1.text == "Invest ◆150", "same structure → patched in place")
	var fresh2 := VBoxContainer.new()
	fresh2.add_child(Label.new())
	check(UiReconcile.apply(a, fresh2) == "rebuilt", "changed structure → rebuilt when nothing is held")
	a.free()
	# #113: a drawer that just opened (empty list) fills in the SAME frame even though the opening finger is still down
	var holder := Control.new()
	root.add_child(holder)
	var ts: Node = preload("../touch_scroll.gd").attach(holder)
	var live := VBoxContainer.new()
	holder.add_child(live)
	live.position = Vector2(0, 100)
	live.size = Vector2(400, 300)
	ts._start = Vector2(50, 200)   # the finger went down on the list
	ts._touching = true
	var rows := VBoxContainer.new()
	rows.add_child(Button.new())
	check(UiReconcile.apply(live, rows) == "rebuilt" and live.get_child_count() == 1, "#113 empty list + finger down → rebuilt now (no 1 s blank drawer)")
	var rows2 := VBoxContainer.new()
	rows2.add_child(Label.new())
	check(UiReconcile.apply(live, rows2) == "deferred", "filled list + finger down → still deferred (button under finger kept)")
	# #183: the finger is on the TAB BAR (outside the list): a kept list switches to the new tab's rows at once
	ts._start = Vector2(50, 600)
	var rows3 := VBoxContainer.new()
	rows3.add_child(Label.new())
	rows3.add_child(Label.new())
	var t0 := Time.get_ticks_usec()
	check(UiReconcile.apply(live, rows3) == "rebuilt" and live.get_child_count() == 2, "#183 finger on the tab bar → kept list rebuilt now (was deferred to the 1 Hz refresh)")
	# #183: a lost release (game _input ate the finger-up) outside the list no longer holds it for HOLD_SAFETY_MS
	ts._touching = false
	ts.finger_down = true
	ts._down_ms = Time.get_ticks_msec()
	var rows4 := VBoxContainer.new()
	rows4.add_child(Button.new())
	check(UiReconcile.apply(live, rows4) == "rebuilt", "#183 stale finger_down outside the list → rebuilt (no 3 s hold)")
	check((Time.get_ticks_usec() - t0) / 1000.0 <= 150.0, "#183 kept-list switch ≤ 150 ms")
	ts.finger_down = false
	ts._touching = false
	holder.free()
	# #113 sheet header + opening motion + selected-tab indicator
	var h: Dictionary = UiKit.sheet_header("trending-up", "Invest", func(): pass)
	check(h.box is VBoxContainer and (h.title as Label).text == "Invest" and h.close is Button and (h.close as Button).custom_minimum_size.y >= 84, "#113 sheet header: tile + title + 48 dp close")
	check((h.box as Node).get_child(1) is HSeparator, "#113 sheet header: hairline divider")
	UiKit.sheet_header_set(h, "users", "Staff")
	check((h.title as Label).text == "Staff" and (h.icon as TextureRect).texture != null, "#113 sheet header retitles + new icon")
	(h.box as Node).free()
	var sheet := PanelContainer.new()
	root.add_child(sheet)
	check(UiKit.open_sheet(sheet, false) == null and sheet.modulate.a == 1.0, "#113 Low: no tween, content fully visible")
	var tw: Tween = UiKit.open_sheet(sheet, true)
	check(tw != null and sheet.modulate.a >= 0.69 and sheet.scale == Vector2.ONE, "#113 Med/High: content visible from frame 1 (alpha ≥ 0.7), geometry fixed")
	for k in 40:
		await process_frame
	check(sheet.modulate.a == 1.0, "#113 opening settles (140 ms fade)")
	sheet.free()
	var tsb := UiKit.theme().get_stylebox("pressed", "TabButton")
	check(tsb is StyleBoxTexture and tsb.get_meta("indicator", Color.BLACK) == UiKit.c("primary"), "#113 selected tab: accent bar in the game's primary colour")


## #113 round 2: list card — one badge, one grey line, locked dim + lock, selected accent edge, 12 dp gap, tab bar inside.
func _list_cards() -> void:
	# the selected-tab bar sits inside the tab box, clear of the rounded corners (round 1 bent over them)
	var tsb := UiKit.theme().get_stylebox("pressed", "TabButton") as StyleBoxTexture
	var img: Image = tsb.texture.get_image()
	var bar: Color = UiKit.c("primary")
	var y := Theme_.TAB_BAR_INSET + 2
	var first := -1
	for x in img.get_width():
		var px := img.get_pixel(x, y)
		if absf(px.r - bar.r) < 0.08 and absf(px.g - bar.g) < 0.08 and absf(px.b - bar.b) < 0.08 and px.a > 0.9:
			first = x
			break
	check(first >= 10 and first < int(tsb.texture_margin_left), "#113 r2 tab bar starts inside the corner (x=%d, radius 14)" % first)
	check(img.get_pixel(1, 1).a < 0.05 and img.get_pixel(img.get_width() - 2, 1).a < 0.05, "#113 r2 tab bar never paints the rounded corners")
	check(img.get_pixel(img.get_width() / 2, 0).a < 0.05 or img.get_pixel(img.get_width() / 2, 0) != bar, "#113 r2 tab bar is inset from the top edge")
	var host := _host(Vector2i(720, 1280))
	var col := VBoxContainer.new()
	col.custom_minimum_size.x = 640
	col.add_theme_constant_override("separation", UiKit.CARD_GAP)
	host.add_child(col)
	var a := UiKit.list_card("map", "Tokyo", {"sub": "Guests ×1.00 · prices ×1.00", "badge": "Here", "badge_kind": "open", "selected": true, "kind": "success"})
	var b := UiKit.list_card("map", "Osaka", {"sub": ["Opens at 1500 served · guests ×1.15 · prices ×1.15 · a much longer tail that will never fit one line on a phone", "Guests ×1.15 · prices ×1.15"], "badge": "Locked", "locked": true})
	var c := UiKit.list_card("trending-up", "Sharp knives", {"sub": "Faster plates", "badge": "¥1,200", "badge_kind": "price"})
	UiKit.list_card_body(c).add_child(UiKit.button("Buy", "primary"))
	for k in [a, b, c]:
		col.add_child(k)
	for i in 4:
		await process_frame
	for k in [a, b, c]:
		var badges := 0
		for n in k.find_children("*", "Label", true, false):
			if String(n.name) == "Badge":
				badges += 1
		check(badges == 1, "#113 r2 card has exactly one badge (%s)" % k.get_node("Col/Head/Texts/Title").text)
		var sub: Label = k.get_node("Col/Head/Texts/Sub")
		var lines := sub.get_line_count()
		var w := sub.get_theme_font("font").get_string_size(sub.text, HORIZONTAL_ALIGNMENT_LEFT, -1, sub.get_theme_font_size("font_size")).x
		check(lines == 1 and w <= sub.size.x and sub.autowrap_mode == TextServer.AUTOWRAP_OFF and not sub.text.ends_with("…") and sub.get_theme_font_size("font_size") >= 21,
			"#113 r2 sub line: one whole line ≥ 21 px (%s, %d px of %d)" % [sub.text, int(w), int(sub.size.x)])
		check(k.get_node("Col/Head/Tile").custom_minimum_size.x >= UiKit.CARD_TILE or (k.get_node("Col/Head/Tile") as TextureRect).texture.get_width() >= UiKit.CARD_TILE, "#113 r2 big square tile")
	check((b.get_node("Col/Head/Texts/Sub") as Label).text == "Guests ×1.15 · prices ×1.15", "#113 r2 too-long line → the shorter alternative (no '…', no wrap)")
	# locked: dim surface + muted title + lock tile; selected: primary accent border
	check(b.theme_type_variation == "ListCardLocked", "#113 r2 locked card uses the dim style")
	var lsb := b.get_theme_stylebox("panel") as StyleBoxFlat
	var nsb := c.get_theme_stylebox("panel") as StyleBoxFlat
	check(_lum(lsb.bg_color.blend(Color(0, 0, 0, 0)) * lsb.bg_color.a) < _lum(nsb.bg_color) and (b.get_node("Col/Head/Texts/Title") as Label).get_theme_color("font_color") == UiKit.c("on_surface_muted"), "#113 r2 locked card is dimmer, title muted")
	var lock_tile := UiKit.tile("lock", "neutral", UiKit.CARD_TILE)
	check((b.get_node("Col/Head/Tile") as TextureRect).texture == lock_tile.texture, "#113 r2 locked card shows the lock icon")
	lock_tile.free()
	var ssb := a.get_theme_stylebox("panel") as StyleBoxFlat
	check(a.theme_type_variation == "ListCardSelected" and ssb.border_width_left >= 3 and ssb.border_color == UiKit.c("primary"), "#113 r2 selected card: accent edge in the game's primary colour")
	check(nsb.shadow_size == 0 and nsb == UiKit.theme().get_stylebox("panel", "ListCard"), "#113 r2 cards share one flat theme stylebox (no shadow pass, no per-card box)")
	check(absf((b.position.y - (a.position.y + a.size.y)) - 21.0) < 0.6 and UiKit.CARD_GAP == UiKit.dp(12), "#113 r2 12 dp between cards")
	check(a.get_node("Col/Head/Badge") is Label and a.get_node("Col/Head/Badge").get_theme_stylebox("normal") is StyleBoxFlat, "#113 r2 badge = one Label with a pill stylebox")
	check(c.size.y >= 84 and (UiKit.list_card_body(c).get_child(1) as Button).size.y >= 84, "#113 r2 48 dp targets")
	# a long German title + badge on a narrow phone card stays inside the card
	var d := UiKit.list_card("users", "Personalschulung für Fortgeschrittene und Meister der Küche", {"sub": ["Verdoppelt die Geschwindigkeit aller Stationen dauerhaft und für immer, ohne jede Ausnahme"], "badge": "Gesperrt", "locked": true})
	col.add_child(d)
	for i in 4:
		await process_frame
	for l in d.find_children("*", "Label", true, false):
		var ll := l as Label
		check(ll.get_visible_line_count() >= ll.get_line_count(), "#113 r2 no cut lines: %s (%d of %d shown, h=%d)" % [ll.text, ll.get_visible_line_count(), ll.get_line_count(), int(ll.size.y)])
	var bd: Label = d.get_node("Col/Head/Badge")
	check(bd.get_global_rect().end.x <= d.get_global_rect().end.x + 0.5 and d.size.x <= 640.5, "#113 r2 long de title + badge stay inside the card")
	# reconcile: state change (locked → open) patches style + texts in place, the sub line refits
	var fresh := VBoxContainer.new()
	for k in [["Tokyo", false], ["Osaka", false], ["Sharp knives", false], ["Personalschulung für Fortgeschrittene", false]]:
		fresh.add_child(UiKit.list_card("map", k[0], {"sub": "Open now", "badge": "Open", "badge_kind": "open"}))
	UiKit.list_card_body(fresh.get_child(2)).add_child(UiKit.button("Buy", "primary"))
	var res := UiReconcile.apply(col, fresh)
	for i in 3:
		await process_frame
	check(res == "patched" and b.theme_type_variation == "ListCard" and (b.get_node("Col/Head/Badge") as Label).theme_type_variation == "BadgeOpen" and (b.get_node("Col/Head/Texts/Sub") as Label).text == "Open now"
		and not (b.get_node("Col/Head/Texts/Title") as Label).has_theme_color_override("font_color"), "#113 r2 reconcile: locked → open in place")
	# #114 Low tier: the badge leads the grey line (same Label) — no pill, no second text item; locked / selected unchanged
	var low_col := VBoxContainer.new()
	low_col.custom_minimum_size.x = 640
	host.add_child(low_col)
	var la := UiKit.list_card("map", "Tokyo", {"sub": "Guests ×1.00", "badge": "Here", "badge_kind": "open", "selected": true, "low": true})
	var lb := UiKit.list_card("map", "Osaka", {"sub": ["Opens at 1500 served · a much longer tail that will never fit one line on a phone card at all", "Opens at 1500"], "badge": "Locked", "locked": true, "low": true})
	var lc := UiKit.list_card("trending-up", "Sharp knives", {"badge": "Lv 2/5", "low": true})
	for k in [la, lb, lc]:
		low_col.add_child(k)
	for i in 4:
		await process_frame
	for k in [la, lb, lc]:
		var labels: Array = k.find_children("*", "Label", true, false)
		var sub: Label = k.get_node("Col/Head/Texts/Sub")
		check(k.get_node_or_null("Col/Head/Tile") == null and k.find_children("*", "TextureRect", true, false).is_empty(), "#114 Low card: no icon tile")
		check(k.get_node_or_null("Col/Head/Badge") == null and labels.size() == 2 and sub.text.begins_with(str(sub.get_meta("coddy_low_badge"))),
			"#114 Low card: title + ONE line that starts with the badge (%s)" % sub.text)
		check(sub.get_line_count() == 1 and not sub.text.ends_with("…") and sub.get_theme_font_size("font_size") >= 21, "#114 Low line stays one whole line (%s)" % sub.text)
	check(lb.theme_type_variation == "ListCardLocked" and la.theme_type_variation == "ListCardSelected", "#114 Low keeps locked dim + selected edge")
	check((lb.get_node("Col/Head/Texts/Sub") as Label).text == "Locked  ·  Opens at 1500", "#114 Low: a long line falls back to badge + short alternative")
	host.queue_free()
	await process_frame


func _shop() -> void:
	var host := _host(Vector2i(1280, 720))
	var holder := VBoxContainer.new()
	holder.name = "iap_shop"
	holder.custom_minimum_size.x = 560
	host.add_child(holder)
	var intro := Label.new()
	intro.text = "The whole game is free."
	holder.add_child(intro)
	var head := Label.new()
	head.name = "group_upgrades"
	head.text = "UPGRADES"
	holder.add_child(head)
	for slug in ["bundle", "noads", "funds_s"]:
		var card := PanelContainer.new()
		card.name = "prod_" + slug
		var h := HBoxContainer.new()
		card.add_child(h)
		var v := VBoxContainer.new()
		var t := Label.new()
		t.text = slug.capitalize()
		v.add_child(t)
		var d := Label.new()
		d.text = "What it does."
		v.add_child(d)
		h.add_child(v)
		var buy := Button.new()
		buy.name = "buy"
		buy.text = "$2.99"
		h.add_child(buy)
		holder.add_child(card)
	var restore := Button.new()
	restore.name = "iap_restore"
	holder.add_child(restore)
	UiShop.style(holder, {"best": "Best value", "compact": false})
	UiShop.style(holder, {"best": "Best value", "compact": false})
	await process_frame
	check(holder.get_node_or_null("shop_intro") != null, "shop intro becomes an info card")
	check((holder.get_node("prod_bundle") as Control).theme_type_variation == "CardRaised" and holder.get_node("prod_bundle").find_child("tile", true, false) != null, "products get a card + icon tile")
	check(holder.get_node("prod_bundle").find_children("best", "", true, false).size() == 1, "restyling twice adds one badge (idempotent)")
	check((holder.get_node("prod_bundle").find_child("buy", true, false) as Button).theme_type_variation == "PrimaryButton" and (holder.get_node("prod_noads").find_child("buy", true, false) as Button).theme_type_variation == "SecondaryButton", "bundle price = brand button, others secondary")
	check(UiShop.icon_for_slug("noads") == "ban" and UiShop.icon_for_slug("funds_xl") == "coins", "slug icons")
	check(head.theme_type_variation == "Overline" and head.auto_translate_mode == Node.AUTO_TRANSLATE_MODE_DISABLED,
		"shop group overline (upper-cased translated text) never auto-translates back")
	host.queue_free()
	await process_frame
	await _shop_fit_112()
	await _shop_compact()
	await _shop_cards()
	await _shop_freed_same_frame()
	await _tight_and_side()
	await _narrow_tag_136()
	_sentence_ends()


## Patron 10-28: TOUCH.min is a floor whatever font a variation is built with. A theme whose button font has a short line
## (a FontVariation with negative top / bottom spacing: ~13 px shorter than Noto, shorter than Montserrat) still gives every
## full-size button variation >= 84 px — measured on real Buttons, not on the margin arithmetic.
func _touch_floor_1028(Th) -> void:
	var base: Theme = UiKit.theme()
	var short := FontVariation.new()
	short.base_font = base.default_font if base.default_font != null else ThemeDB.fallback_font
	short.spacing_top = -7
	short.spacing_bottom = -6
	var t := Theme.new()
	t.default_font = short
	var host := Control.new()
	host.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	host.theme = t
	root.add_child(host)
	var line: float = short.get_height(Tokens.TYPE.label.size)
	for type in ["Button", "PrimaryButton", "SecondaryButton", "TonalButton", "DangerButton", "GhostButton", "TextButton", "OutlineButton"]:
		if type != "Button":
			t.set_type_variation(type, "Button")
		t.set_font("font", type, short)
		var opts := {}
		if type == "GhostButton":
			opts = {"margin": Vector4(20, 24, 20, 24)}
		elif type == "TextButton":
			opts = {"margin": Vector4(16, 24, 16, 24)}
		Th._button(t, type, UiKit.p, Color.BLACK, Color.WHITE, opts)
		var b := Button.new()
		b.text = "Dispatch"
		b.theme_type_variation = "" if type == "Button" else type
		host.add_child(b)
		await process_frame
		check(b.get_combined_minimum_size().y >= Tokens.TOUCH.min, "10-28: %s with a %d px font line is >= %d px (%d)" % [type, int(line), Tokens.TOUCH.min, int(b.get_combined_minimum_size().y)])
	host.queue_free()
	await process_frame


## #112 (found by S5 on Airport's Montserrat): button padding from the game font's line, a long product name takes the
## row's icon room, and a description given its second line back is re-measured (both lines drawn).
func _shop_fit_112() -> void:
	var Th = load("res://addons/coddy_ui/ui_theme.gd")
	var m: Vector4 = Th._touch_margin(Vector4(28, 24, 28, 24), 33.0)   # a Montserrat-like line at 26 px (Noto's is ~36)
	check(33.0 + m.y + m.w >= 84.0, "#112: a button with a shorter font line is still >= 84 px (%d)" % int(33.0 + m.y + m.w))
	check(Th._touch_margin(Vector4(28, 24, 28, 24), 36.0) == Vector4(28, 24, 28, 24), "#112: Noto's line keeps the usual 24 px padding")
	m = Th._touch_margin(Vector4(28, 24, 28, 24), 0.0)
	check(m.y + m.w >= 84.0, "10-28: with no measurable font the margin alone holds the 84 px floor (%d)" % int(m.y + m.w))
	await _touch_floor_1028(Th)
	var host := _host(Vector2i(720, 1280))
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 14)
	row.size = Vector2(300, 90)
	host.add_child(row)
	var tile := TextureRect.new()
	tile.custom_minimum_size = Vector2(48, 48)
	row.add_child(tile)
	var col := VBoxContainer.new()
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(col)
	var title := Label.new()
	title.text = "Warteschlangenhilfe"
	col.add_child(title)
	var desc := Label.new()
	desc.text = "Keine Pflichtwerbung. Begrenzte Belohnungen ohne Videos und noch etwas mehr Text."
	desc.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	col.add_child(desc)
	await process_frame
	var f: Font = title.get_theme_font("font")
	var need: float = f.get_string_size(title.text, HORIZONTAL_ALIGNMENT_LEFT, -1, 21).x
	var narrow := need - 20.0   # does not fit at 21 px, fits with the tile's 62 px
	tile.size = Vector2(48, 48)
	UiShop._fit_title(title, narrow)
	check(not tile.visible, "#112: a name that does not fit even at 21 px takes the icon tile's room")
	UiShop._fit_title(title, need + 80.0)
	check(tile.visible, "#112: the tile comes back when the name fits beside it again")
	UiShop._set_lines(desc, 1)
	await process_frame
	await process_frame
	UiShop._set_lines(desc, 2)
	await process_frame
	await process_frame
	check(desc.get_visible_line_count() == 2, "#112: a description given its second line back draws both lines (%d)" % desc.get_visible_line_count())
	host.queue_free()
	await process_frame


## Patron 10-22: the default shop is compact — ≥ 3 products on one screen (landscape side panel and portrait sheet).
func _shop_compact() -> void:
	for size in [Vector2i(1280, 720), Vector2i(720, 1280)]:
		var host := _host(size)
		var parts := UiScroll.list(12)
		var sc: ScrollContainer = parts[0]
		sc.position = Vector2(820, 270) if size.x > size.y else Vector2(24, 700)
		sc.size = Vector2(428, 422) if size.x > size.y else Vector2(672, 520)
		host.add_child(sc)
		var holder := VBoxContainer.new()
		holder.name = "iap_shop"
		holder.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		(parts[1] as VBoxContainer).add_child(holder)
		var intro := Label.new()
		intro.text = "Das ganze Spiel ist kostenlos. Nur Werbung entfernen, Hafen-Edition und Komplette Hafen-Kollektion entfernen Pflichtwerbung."
		holder.add_child(intro)
		var head := Label.new()
		head.name = "group_perm"
		head.text = "UPGRADES"
		holder.add_child(head)
		for slug in ["noads", "premium", "speed", "bundle", "funds_s"]:
			var card := PanelContainer.new()
			card.name = "prod_" + slug
			var h := HBoxContainer.new()
			card.add_child(h)
			var v := VBoxContainer.new()
			var titles := {"noads": "Werbung entfernen", "premium": "Hafen-Edition", "speed": "Warteschlangenhilfe", "bundle": "Komplette Hafen-Kollektion mit allen Extras", "funds_s": "Kleines Budget"}
			for t in [titles[slug], "Keine Pflichtwerbung. Begrenzte Belohnungen ohne Videos."]:
				var l := Label.new()
				l.text = t
				l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
				v.add_child(l)
			h.add_child(v)
			var buy := Button.new()
			buy.name = "buy"
			buy.text = "Preis wird geladen…"
			buy.disabled = true
			h.add_child(buy)
			holder.add_child(card)
		UiShop.style(holder, {"best": "Bestes Angebot"})
		for i in 3:
			await process_frame
		var view := sc.get_global_rect()
		var seen := 0
		var tallest := 0.0
		for c in holder.get_children():
			if String(c.name).begins_with("prod_"):
				tallest = maxf(tallest, (c as Control).size.y)
				if view.encloses((c as Control).get_global_rect()):
					seen += 1
		var tag := "%dx%d" % [size.x, size.y]
		check(seen >= 3, tag + ": compact shop shows ≥ 3 whole products in one screen (%d)" % seen)
		check(tallest <= 140.0, tag + ": compact product row ≤ 140 px with a 2-line title + 2-line description (%d)" % tallest)
		# Patron 10-22: nothing cut to one line — descriptions wrap to 2 lines, a loading price shows "…", never "Preis w…"
		var d0: Label = null
		var t0: Label = null
		for l in holder.get_node("prod_noads").find_children("*", "Label", true, false):
			if l.text.begins_with("Keine"):
				d0 = l
			elif l.text.begins_with("Werbung"):
				t0 = l
		check(d0 != null and d0.max_lines_visible == 2 and d0.autowrap_mode != TextServer.AUTOWRAP_OFF and d0.get_line_count() >= 2 and d0.get_visible_line_count() == 2, tag + ": the description wraps to two lines and both are drawn (%d/%d)" % [d0.get_line_count() if d0 else -1, d0.get_visible_line_count() if d0 else -1])
		check(t0 != null and t0.max_lines_visible == 2 and t0.get_line_count() == 1, tag + ": a short title stays one line")
		var tb: Label = holder.get_node("prod_bundle").find_children("*", "Label", true, false)[0]
		var tbf: Font = tb.get_theme_font("font")
		var tbs: int = tb.get_theme_font_size("font_size")
		var one_line := tbf.get_string_size(tb.text, HORIZONTAL_ALIGNMENT_LEFT, -1, tbs).x <= tb.size.x + 1
		check(tbs >= 21 and (one_line or (tb.get_line_count() >= 2 and tb.size.y >= 2.0 * tbs)), tag + ": a long title fits on one line or shows two whole lines (then …, ≥ 21 px)")
		var tw: Label = holder.get_node("prod_speed").find_children("*", "Label", true, false)[0]
		var twf: Font = tw.get_theme_font("font")
		check(twf.get_string_size("Warteschlangenhilfe", HORIZONTAL_ALIGNMENT_LEFT, -1, tw.get_theme_font_size("font_size")).x <= tw.size.x + 1, tag + ": a long single word (Warteschlangenhilfe) fits whole")
		var b1 := holder.get_node("prod_premium").find_child("buy", true, false) as Button
		check(b1.text == "…" and b1.tooltip_text == "Preis wird geladen…", tag + ": a price still loading shows a fixed …")
		b1.text = "1.234,99 €"
		UiShop.fit_price(b1, 124)
		await process_frame
		var bf: Font = b1.get_theme_font("font")
		var bsz: int = b1.get_theme_font_size("font_size")
		check(bf.get_string_size(b1.text, HORIZONTAL_ALIGNMENT_LEFT, -1, bsz).x + 32 <= b1.size.x + 1 and bsz >= 21, tag + ": a loaded price fits its button untrimmed (%d px font, %d px button)" % [bsz, b1.size.x])
		var strip := holder.get_node_or_null("shop_intro") as Control
		check(strip != null and strip.size.y <= 50.0, tag + ": info strip is one line (%d px)" % (strip.size.y if strip else -1))
		UiShop.toggle_intro(strip)
		for i in 3:
			await process_frame
		check(strip.size.y > 60.0, tag + ": a tap opens the whole info text (%d px)" % strip.size.y)
		UiShop.toggle_intro(strip)
		for i in 3:
			await process_frame
		check(strip.size.y <= 50.0, tag + ": a second tap closes it again")
		# W66 10-27: a phone finger = the touch AND Godot's emulated click (DEVICE_ID_EMULATION) through gui_input — one toggle
		var lift := InputEventScreenTouch.new()
		lift.pressed = false
		var echo := InputEventMouseButton.new()
		echo.button_index = MOUSE_BUTTON_LEFT
		echo.pressed = false
		echo.device = InputEvent.DEVICE_ID_EMULATION
		strip.gui_input.emit(lift)
		strip.gui_input.emit(echo)
		for i in 3:
			await process_frame
		check(strip.size.y > 60.0, tag + ": a PHONE tap (touch + emulated click) opens the info text once (%d px)" % strip.size.y)
		strip.gui_input.emit(lift)
		strip.gui_input.emit(echo)
		for i in 3:
			await process_frame
		check(strip.size.y <= 50.0, tag + ": a second phone tap closes it")
		var buy0 := holder.get_node("prod_noads").find_child("buy", true, false) as Button
		check(buy0.size.y >= Tokens.TOUCH.min and buy0.get_global_rect().end.x <= view.end.x + 1, tag + ": price button ≥ 48 dp, inside the row")
		host.queue_free()
		await process_frame


func _loading() -> void:
	var host := _host(Vector2i(1280, 720))
	var l := UiLoading.new()
	l.game_title = "Canal\nControl"
	host.add_child(l)
	await process_frame
	l.set_progress(1, 2, "Loading the harbor…")
	check(is_equal_approx(l.progress.value, 0.5) and l.status.text == "Loading the harbor…", "loading screen: progress + translated status")
	host.queue_free()
	await process_frame


class FakeAds extends RefCounted:
	var required := false
	var shown := 0
	func privacy_options_required() -> bool:
		return required
	func show_privacy_options() -> void:
		shown += 1


func _privacy() -> void:
	var host := _host(Vector2i(1280, 720))
	var col := VBoxContainer.new()
	col.custom_minimum_size.x = 560
	host.add_child(col)
	var ads := FakeAds.new()
	UiSettings.privacy(col, ads, {"title": "Privacy", "options": "Privacy options", "policy": "Privacy policy"})
	check(col.find_child("PrivacyOptions", true, false) == null and col.find_child("PrivacyPolicy", true, false) != null, "privacy options hidden unless UMP requires them; policy link always")
	ads.required = true
	var col2 := VBoxContainer.new()
	host.add_child(col2)
	UiSettings.privacy(col2, ads, {"title": "Privacy", "options": "Privacy options", "policy": "Privacy policy"})
	var opt := col2.find_child("PrivacyOptions", true, false) as Button
	check(opt != null, "privacy options row when REQUIRED")
	if opt != null:
		opt.pressed.emit()
	check(ads.shown == 1, "privacy options row opens the UMP form")
	(col2.find_child("PrivacyPolicy", true, false) as Button).pressed.emit()
	check(UiSettings.opened_urls.back() == "https://www.coddykit.com/pages/privacy-policy", "policy link opens the fleet privacy page")
	check(UiSettings.privacy_options_visible(null) == false, "no ads service → no options row")
	host.queue_free()
	await process_frame


## Android Back, one layer at a time (2026-10-28: pharmacy-rush quit when Back was pressed over the open Shop panel).
## (The kit test raises only the root Window's go_back_requested — what UiBack listens to. The full UiBack.send() also
## notifies every node, and a game's own Back handler in an autoload — Airport's GameState — would quit the test run.)
func _back_1028() -> void:
	var host := _host(Vector2i(720, 1280))
	quit_on_go_back = true
	var router := UiBack.new()
	host.add_child(router)
	var quits := [0]
	router.quit_hook = func() -> void: quits[0] += 1
	check(not quit_on_go_back, "back: the router turns the engine's quit-on-Back off")
	# a game panel (the Shop drawer) and a closable window over it
	var panel := PanelContainer.new()
	panel.name = "ShopPanel"
	host.add_child(panel)
	UiBack.layer(panel, func() -> void: panel.visible = false)
	var hidden := PanelContainer.new()
	hidden.visible = false
	host.add_child(hidden)
	var hidden_closed := [0]
	UiBack.layer(hidden, func() -> void: hidden_closed[0] += 1)
	var m := UiModal.new()
	host.add_child(m)
	m.setup("Settings", {"icon": "settings", "close": true})
	m.close_requested.connect(func() -> void: m.queue_free())
	var top := UiModal.new()
	host.add_child(top)
	top.setup("Language", {"icon": "globe", "close": true})
	top.close_requested.connect(func() -> void: top.queue_free())
	# a panel added AFTER the windows is still under them (windows first)
	var late := PanelContainer.new()
	host.add_child(late)
	var late_closed := [0]
	UiBack.layer(late, func() -> void:
		late_closed[0] += 1
		late.visible = false)
	await process_frame
	root.go_back_requested.emit()
	check(router.last == "window" and top.is_queued_for_deletion() and not m.is_queued_for_deletion(), "back: the TOP window closes first (%s)" % router.last)
	await process_frame
	root.go_back_requested.emit()
	check(router.last == "window" and not is_instance_valid(top) and m.is_queued_for_deletion(), "back: then the window under it")
	await process_frame
	root.go_back_requested.emit()
	check(router.last == "layer" and late_closed[0] == 1 and panel.visible, "back: then the later game panel (%s)" % router.last)
	root.go_back_requested.emit()
	check(router.last == "layer" and not panel.visible and quits[0] == 0, "back: then the Shop panel closes — the app does NOT quit")
	check(hidden_closed[0] == 0, "back: a hidden panel is never 'closed'")
	# a window without a close keeps Back; with a back opt Back runs it
	var keep := UiModal.new()
	host.add_child(keep)
	keep.setup("Day report", {"icon": "info"})
	var keep_closed := [0]
	keep.close_requested.connect(func() -> void: keep_closed[0] += 1)
	await process_frame
	root.go_back_requested.emit()
	check(router.last == "window" and keep_closed[0] == 0 and quits[0] == 0 and is_instance_valid(keep) and keep.visible, "back: a window without a close stays, the app does not quit under it")
	keep.queue_free()
	var cont := UiModal.new()
	host.add_child(cont)
	var conts := [0]
	cont.setup("Away report", {"icon": "info", "back": func() -> void:
		conts[0] += 1
		cont.queue_free()})
	await process_frame
	root.go_back_requested.emit()
	check(router.last == "window" and conts[0] == 1, "back: a window's back opt runs (Continue)")
	await process_frame
	# Esc on the main screen never quits; a full-screen ad blocks Back
	var esc := InputEventAction.new()
	esc.action = "ui_cancel"
	esc.pressed = true
	Input.parse_input_event(esc)
	await process_frame
	check(router.last == "main" and quits[0] == 0, "back: Esc on the main screen does nothing (%s)" % router.last)
	var ad := Control.new()
	ad.add_to_group(UiBack.BLOCK_GROUP)
	host.add_child(ad)
	root.go_back_requested.emit()
	check(router.last == "blocked" and quits[0] == 0, "back: a full-screen ad keeps Back")
	ad.queue_free()
	await process_frame
	# the game's own layer, then the main screen: save (WM_CLOSE_REQUEST) + quit, or the game's main handler
	var game_open := [true]
	router.setup({"game": func() -> bool:
		if game_open[0]:
			game_open[0] = false
			return true
		return false})
	root.go_back_requested.emit()
	check(router.last == "game" and quits[0] == 0, "back: the game's own layer closes before any quit")
	root.go_back_requested.emit()
	check(router.last == "quit" and quits[0] == 1, "back: on the main screen the game saves (close request) and quits")
	var mains := [0]
	router.setup({"main": func() -> void: mains[0] += 1})
	root.go_back_requested.emit()
	check(router.last == "main" and mains[0] == 1 and quits[0] == 1, "back: a main-screen handler (quit confirm) replaces the quit")
	host.queue_free()
	await process_frame


func _host(size: Vector2i) -> Control:
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	root.size = size
	var host := Control.new()
	host.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	host.theme = UiKit.use("canal-control")
	root.add_child(host)
	return host


## Kit round 2 (10-27, W66 fleet sweep: ~17 games patched these by hand) ------------------------------------------------

## A language switch hides a toast built in the old language; the one TRANSLATION_CHANGED Godot 4.7.2 sends right after
## the toast enters the tree (same locale) keeps it.
func _toast_locale_1027() -> void:
	var was := TranslationServer.get_locale()
	TranslationServer.set_locale("en")
	var host := _host(Vector2i(1280, 720))
	var toast := UiToast.new()
	toast.setup({"top": 120})
	toast.show_toast("Crew dispatched.", "success")   # shown before it is in the tree, then added (the 4.7.2 ping)
	host.add_child(toast)
	await process_frame
	toast.notification(NOTIFICATION_TRANSLATION_CHANGED)   # same locale: not a switch
	await process_frame
	check(toast.is_showing(), "10-27 toast: the translation ping after entering the tree (same locale) keeps it")
	TranslationServer.set_locale("de")
	await process_frame
	check(not toast.is_showing(), "10-27 toast: a language switch hides the toast built in the old language")
	toast.show_toast("Crew dispatched.", "success")
	await process_frame
	toast.notification(NOTIFICATION_TRANSLATION_CHANGED)
	check(toast.is_showing() and str(toast.get("locale")) == "de", "10-27 toast: a toast shown after the switch stays (%s)" % toast.get("locale"))
	TranslationServer.set_locale(was)
	host.queue_free()
	await process_frame


## While a coach bubble / guide card is up an ordinary toast is skipped (one already up goes); warning / danger and a
## priority "guide" line still show, clear of the card and its spotlight — also with the toast on a higher canvas layer
## (lego: toast layer 110 over the guide layer 10).
func _toast_guide_1027() -> void:
	for layered in [false, true]:
		for size in [Vector2i(1280, 720), Vector2i(720, 1280)]:
			var host := _host(size)
			var guide_parent: Node = host
			var toast_parent: Node = host
			if layered:
				var gl := CanvasLayer.new()
				gl.layer = 10
				var tl := CanvasLayer.new()
				tl.layer = 110
				host.add_child(gl)
				host.add_child(tl)
				var gh := Control.new()
				gh.theme = host.theme
				gh.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
				gl.add_child(gh)
				var th := Control.new()
				th.theme = host.theme
				th.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
				th.mouse_filter = Control.MOUSE_FILTER_IGNORE
				tl.add_child(th)
				guide_parent = gh
				toast_parent = th
			var tag := "%dx%d%s" % [size.x, size.y, " layered" if layered else ""]
			var coach := UiCoach.new()
			guide_parent.add_child(coach)
			coach.setup({"skip": "Skip guide", "hint": "Tap the highlighted button"})
			var toast := UiToast.new()
			toast_parent.add_child(toast)
			toast.setup({"top": 120})
			await process_frame
			toast.show_toast("Line built.", "info")
			await process_frame
			check(toast.is_showing(), tag + ": 10-27 toast: an ordinary toast shows with no guide up")
			# the spotlight sits right on the toast's row: the step's card comes up while the toast is on screen
			var spot := Rect2(Vector2(size.x / 2.0 - 140.0, 110.0), Vector2(280, 84))
			coach.show_step("Tap Hire to get a second cook.", "Step 3 of 9", spot)
			await process_frame
			await process_frame
			check(not toast.is_showing(), tag + ": 10-27 toast: an ordinary toast on screen goes when the guide card comes up")
			var before = toast.get("skipped")
			toast.show_toast("Line built.", "success")
			await process_frame
			check(not toast.is_showing() and before != null and str(toast.get("skipped")) == str(int(before) + 1), tag + ": 10-27 toast: an ordinary toast is skipped while the guide card is up")
			var api := toast.has_method("show_guide_toast") and toast.has_method("keep_clear")
			check(api, tag + ": 10-27 toast: guide-priority / keep_clear API")
			if not api:
				host.queue_free()
				await process_frame
				continue
			for k in ["warning", "danger", "guide"]:
				toast.hide_toast()
				if k == "guide":
					toast.call("show_toast", "New usta hired!", "success", "", "guide")
				else:
					toast.show_toast("Not enough cash.", k)
				await process_frame
				var r: Rect2 = toast.card().get_global_rect()
				var b := coach.bubble_rect()
				check(toast.is_showing() and not r.intersects(b) and not r.intersects(coach.hole) and Rect2(Vector2.ZERO, Vector2(size)).encloses(r),
					"%s: 10-27 toast: a %s toast shows during the guide, clear of the card %s and spotlight %s (%s)" % [tag, k, b, coach.hole, r])
			# the card moves to another step: the guide-priority toast moves with it, its fade untouched
			toast.hide_toast()
			toast.call("show_guide_toast", "New usta hired!")
			await process_frame
			var tw = toast.get("_tween")
			var shown_n := toast.shown
			coach.show_step("Tap Spit to add a chicken spit.", "Step 4 of 9", Rect2(Vector2(40, size.y - 200.0), Vector2(240, 84)))
			coach.show_step("Watch the queue.", "Step 4 of 9", Rect2(toast.card().get_global_rect().get_center() - Vector2(60, 30), Vector2(120, 60)))
			await process_frame
			await process_frame
			var r2: Rect2 = toast.card().get_global_rect()
			check(not toast.is_showing() or (not r2.intersects(coach.bubble_rect()) and not r2.intersects(coach.hole)),
				tag + ": 10-27 toast: a guide-priority toast follows a moving guide card (never over it)")
			check(toast.shown == shown_n and toast.get("_tween") == tw, tag + ": 10-27 toast: re-placing keeps the toast's fade (no new show)")
			# the guide ends: ordinary toasts are back
			coach.hide_step()
			await process_frame
			toast.show_toast("Line built.", "info")
			await process_frame
			check(toast.is_showing(), tag + ": 10-27 toast: ordinary toasts show again after the guide")
			# a game's own guide card (not a UiCoach) registered with add_guide
			var card := PanelContainer.new()
			card.position = Vector2(40, size.y - 400.0)
			card.custom_minimum_size = Vector2(400, 200)
			guide_parent.add_child(card)
			toast.call("add_guide", card)
			await process_frame
			await process_frame
			check(not toast.is_showing(), tag + ": 10-27 toast: a registered guide card hides an ordinary toast")
			card.visible = false
			await process_frame
			toast.show_toast("Line built.", "info")
			check(toast.is_showing(), tag + ": 10-27 toast: a hidden guide card does not hold toasts back")
			host.queue_free()
			await process_frame


## keep_clear(panel): the toast never covers a registered panel (sheet head, event card, tip card); a visible toast moves
## when one opens / closes / resizes, without a new fade; a full-height "foot" panel takes it at its foot.
func _toast_panels_1027() -> void:
	var size := Vector2i(720, 1280)
	var host := _host(size)
	var sheet := Control.new()
	sheet.position = Vector2(0, 100)
	sheet.size = Vector2(720, 1180)
	host.add_child(sheet)
	var head := Control.new()
	head.position = Vector2(0, 0)
	head.size = Vector2(720, 180)
	sheet.add_child(head)
	var toast := UiToast.new()
	host.add_child(toast)
	toast.setup({"top": 120})
	sheet.visible = false
	check(toast.has_method("keep_clear"), "10-27 toast panels: keep_clear API")
	if not toast.has_method("keep_clear"):
		host.queue_free()
		await process_frame
		return
	toast.call("keep_clear", head)
	toast.show_toast("Dispatched 1 vehicle.", "success")
	await create_timer(0.3).timeout   # after the slide-in
	var home: Rect2 = toast.card().get_global_rect()
	var tw = toast.get("_tween")
	sheet.visible = true   # the sheet opens under a toast that is up
	await process_frame
	await process_frame
	var r: Rect2 = toast.card().get_global_rect()
	check(not r.intersects(head.get_global_rect()) and r.position.y >= head.get_global_rect().end.y,
		"10-27 toast panels: a sheet opening under a toast moves it off the sheet's head (%s vs %s)" % [r, head.get_global_rect()])
	check(toast.get("_tween") == tw and toast.shown == 1, "10-27 toast panels: the move keeps the toast's fade")
	sheet.position.y = 300.0   # the sheet slides (its head row's own rect does not change)
	await process_frame
	await process_frame
	r = toast.card().get_global_rect()
	check(not r.intersects(head.get_global_rect()), "10-27 toast panels: the toast follows a sliding sheet's head (%s vs %s)" % [r, head.get_global_rect()])
	sheet.visible = false
	await process_frame
	await process_frame
	check(toast.card().get_global_rect().position.is_equal_approx(home.position), "10-27 toast panels: the sheet closes, the toast goes back to its row")
	# an event card that grows under the toast
	var ev := PanelContainer.new()
	ev.position = Vector2(60, 300)
	ev.size = Vector2(600, 100)
	host.add_child(ev)
	toast.call("keep_clear", ev, {"pad": 16})
	await process_frame
	await process_frame
	check(toast.card().get_global_rect().position.is_equal_approx(home.position), "10-27 toast panels: a card away from the toast leaves it where it is")
	ev.position = Vector2(60, 90)
	ev.size = Vector2(600, 220)
	await process_frame
	await process_frame
	r = toast.card().get_global_rect()
	check(not r.intersects(ev.get_global_rect()), "10-27 toast panels: an event card that moves / grows under the toast pushes it off (%s vs %s)" % [r, ev.get_global_rect()])
	toast.call("release", ev)
	ev.queue_free()
	# a full-height sheet with no room outside: at its foot (registered as "foot")
	var full := Control.new()
	full.position = Vector2(0, 0)
	full.size = Vector2(720, 1280)
	host.add_child(full)
	toast.call("keep_clear", full, {"foot": true})
	toast.show_toast("Garage upgraded.", "success")
	await process_frame
	await process_frame
	r = toast.card().get_global_rect()
	check(r.end.y <= 1280.0 and r.position.y > 900.0, "10-27 toast panels: a full-height foot sheet takes the toast at its foot (%s)" % r)
	full.queue_free()
	await process_frame
	await process_frame
	toast.show_toast("Garage upgraded.", "success")
	await create_timer(0.3).timeout
	check(is_equal_approx(toast.card().get_global_rect().position.y, home.position.y) and toast.get("_clear").size() == 1, "10-27 toast panels: a freed panel is released by itself (%s vs %s, %d)" % [toast.card().get_global_rect(), home, toast.get("_clear").size()])
	host.queue_free()
	await process_frame


## Modal columns follow the screen: side by side on a wide screen, stacked after a resize to a narrow one (and back);
## an empty column is hidden (Airport aea487cbdc: the emptied column kept its share and squeezed the other one).
func _modal_columns_1027() -> void:
	UiKit.design = 2
	var host := _host(Vector2i(1600, 720))
	var m := UiModal.new()
	host.add_child(m)
	m.setup("Settings", {"icon": "settings", "width": 1180, "close": true, "sheet": false})
	var cols: Array = m.columns(2)
	(cols[0] as VBoxContainer).add_child(UiRow.toggle("music", "Music", true))
	(cols[1] as VBoxContainer).add_child(UiRow.toggle("music", "Sound effects", true))
	for i in 4:
		await process_frame
	check((cols[0] as Control).get_parent() is HBoxContainer, "10-27 modal columns: side by side on a 1600 px wide screen")
	root.size = Vector2i(720, 1600)
	for i in 4:
		await process_frame
	check((cols[0] as Control).get_parent() is VBoxContainer and (cols[1] as Control).get_parent() == (cols[0] as Control).get_parent(),
		"10-27 modal columns: stacked after the screen turns narrow")
	check(m.card.get_global_rect().end.x <= 720.5, "10-27 modal columns: the card stays on the narrow screen (%s)" % m.card.get_global_rect())
	root.size = Vector2i(1600, 720)
	for i in 4:
		await process_frame
	check((cols[0] as Control).get_parent() is HBoxContainer, "10-27 modal columns: side by side again on the wide screen")
	host.queue_free()
	await process_frame
	var host2 := _host(Vector2i(1600, 720))
	var m2 := UiModal.new()
	host2.add_child(m2)
	m2.setup("Settings", {"icon": "settings", "width": 1180, "close": true, "sheet": false})
	var c2: Array = m2.columns(2)
	(c2[0] as VBoxContainer).add_child(UiRow.toggle("music", "Language", true))
	for i in 4:
		await process_frame
	var used := (c2[0] as Control).size.x
	var all := ((c2[0] as Control).get_parent() as Control).size.x
	check(not (c2[1] as Control).visible and used >= all - 1.0, "10-27 modal columns: an empty column is hidden, the other takes the width (%.0f of %.0f)" % [used, all])
	(c2[1] as VBoxContainer).add_child(UiRow.toggle("music", "Sound", true))
	for i in 4:
		await process_frame
	check((c2[1] as Control).visible, "10-27 modal columns: a column that gets content shows again")
	# a game that rebuilds the body frees the old columns row (Train Baron monkey test: "previously freed instance")
	((c2[0] as Control).get_parent()).free()
	root.size = Vector2i(1280, 720)
	for i in 4:
		await process_frame
	check(m2.get("_col_rows").is_empty(), "10-27 modal columns: a freed columns row is forgotten")
	host2.queue_free()
	await process_frame
	UiKit.design = 1
	UiKit.use("canal-control")


## A settings row's grey line never splits a word ("coddykit.c / om", Train Baron landscape Settings): it steps down to
## 21 px at most, then ends in "…" on one line; a sentence still wraps between words.
func _row_subtitle_1027() -> void:
	for design in [1, 2]:
		UiKit.design = design
		var host := _host(Vector2i(1280, 720))
		var col := VBoxContainer.new()
		col.custom_minimum_size.x = 250
		host.add_child(col)
		var g: PanelContainer = UiKit.group()
		col.add_child(g)
		var row: Button = UiRow.nav("book-open", "Privacy policy", "coddykit.com", func() -> void: pass)
		UiKit.group_add(g, row)
		var wide := VBoxContainer.new()
		wide.position = Vector2(400, 0)
		wide.custom_minimum_size.x = 700
		host.add_child(wide)
		var row2: Button = UiRow.nav("book-open", "Privacy policy", "Opens coddykit.com in your browser so you can read how the game handles your data.", func() -> void: pass)
		wide.add_child(row2)
		for i in 6:
			await process_frame
		var sub: Label = row.get_node("Box/Texts/Subtitle")
		var fs := sub.get_theme_font_size("font_size")
		var fits: bool = sub.get_theme_font("font").get_string_size("coddykit.com", HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x <= sub.size.x + 0.5
		check(sub.get_line_count() == 1 and fs >= 21 and (fits or (sub.autowrap_mode == TextServer.AUTOWRAP_OFF and sub.text_overrun_behavior == TextServer.OVERRUN_TRIM_ELLIPSIS)),
			"10-27 row subtitle (design %d): a long word in a narrow row stays whole or ends in … on one line, >= 21 px (%d lines, %d px, %.0f px wide)" % [design, sub.get_line_count(), fs, sub.size.x])
		check(row.get_global_rect().end.x <= col.get_global_rect().end.x + 1.0, "10-27 row subtitle (design %d): the row does not widen its column" % design)
		var sub2: Label = row2.get_node("Box/Texts/Subtitle")
		check(sub2.get_line_count() >= 2 and sub2.autowrap_mode == TextServer.AUTOWRAP_WORD and sub2.get_theme_font_size("font_size") == Tokens.TYPE.caption.size,
			"10-27 row subtitle (design %d): a sentence still wraps between words at the caption size" % design)
		host.queue_free()
		await process_frame
	UiKit.design = 1
	UiKit.use("canal-control")


## Kit fix 3 (10-27, Corrupt State Z Fold7 crash, c96efe8265): a redraw-triggered refit ran with call_deferred, i.e. in
## the same deferred-call flush as the redraw — a fit that changes the layout re-queued itself without end ("Message
## queue out of memory" → CallQueue::statistics touched a freed object). The refit now runs on the next frame, at most
## REFIT_CAP changing refits per control per frame, and a layout that flips back and forth settles on its last result.
class QueueCatch extends Logger:
	var seen: Array = []

	func _log_error(function: String, file: String, line: int, code: String, rationale: String, _editor_notify: bool,
			error_type: int, _script_backtraces: Array[ScriptBacktrace]) -> void:
		var text := code + " " + rationale
		if error_type == ERROR_TYPE_SCRIPT or file.ends_with(".gd") or text.contains("freed") or text.contains("Message queue"):
			seen.append("%s:%d %s %s" % [file, line, function, text])

	func _log_message(message: String, _error: bool) -> void:
		if message.contains("Message queue out of memory"):
			seen.append(message)


func _refit_oscillation_1027() -> void:
	var catch := QueueCatch.new()
	OS.add_logger(catch)
	var host := _host(Vector2i(1280, 720))
	await process_frame
	var pending0 := process_frame.get_connections().size()
	# a wrapping label kept to one line (coddy_fit_lines = 1) whose host gives it more room when the fit had to shrink
	# the text and takes the room back when the text is at full size: each fit flips its width across the threshold
	var text := "Harbour master office"
	var l: Label = UiKit.label(text, "Body", true)
	l.size_flags_horizontal = Control.SIZE_FILL
	l.set_meta("coddy_fit_lines", 1)
	var font: Font = host.get_theme_font("font", "Label")
	var base: int = Tokens.TYPE.body.size
	var wide := ceilf(font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, base).x) + 12.0
	var narrow := ceilf(font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, base - 3).x) + 4.0
	var resizes := [0]
	var draws := [0]
	l.resized.connect(func() -> void: resizes[0] += 1)
	l.draw.connect(func() -> void: draws[0] += 1)
	l.size.x = narrow
	l.theme_changed.connect(func() -> void:
		var fs := l.get_theme_font_size("font_size")
		if fs == base:
			l.size.x = narrow
		elif fs >= UiKit.FIT_FLOOR and fs < base:
			l.size.x = wide)
	UiKit.watch_fit(l)
	host.add_child(l)
	var per_frame: Array = []
	var states: Array = []
	for i in 60:
		var r0: int = resizes[0] + draws[0]
		await process_frame
		per_frame.append(resizes[0] + draws[0] - r0)
		states.append("%d|%d" % [l.get_theme_font_size("font_size"), int(l.size.x)])
	var first := 0
	for n in per_frame.slice(0, 10):
		first += int(n)
	var late := 0
	for n in per_frame.slice(10):
		late += int(n)
	var settled := true
	for st in states.slice(10):
		settled = settled and st == states[10]
	check(first <= 40, "10-27 refit oscillation: the first 10 frames stay bounded (%d resizes + draws; a same-flush refit loops until the queue overflows)" % first)
	check(late == 0, "10-27 refit oscillation: no resize / redraw once settled (%d in 50 frames: no per-frame work)" % late)
	check(settled, "10-27 refit oscillation: the label settles (font|width %s)" % [states.slice(8, 14)])
	check(l.get_theme_font_size("font_size") >= UiKit.FIT_TEXT_MIN, "10-27 refit oscillation: settled text >= 21 px (%d)" % l.get_theme_font_size("font_size"))
	check(process_frame.get_connections().size() == pending0, "10-27 refit oscillation: no pending refit left on process_frame (%d → %d)" % [pending0, process_frame.get_connections().size()])
	# a real text change still refits after the hold (the hold is per input, not forever)
	l.text = "Harbour master office and the old customs house"
	for i in 6:
		await process_frame
	var fs_after := l.get_theme_font_size("font_size")
	var lines_ok := UiKit.line_count(l.get_theme_font("font"), l.text, fs_after, l.size.x) <= 1 or fs_after == UiKit.FIT_TEXT_MIN
	check(lines_ok, "10-27 refit oscillation: a new text after the hold is fitted again (%d px, %.0f px wide)" % [fs_after, l.size.x])
	# freed while a next-frame refit is pending (queued free inside its own draw): no freed call, nothing left connected
	var gone: Label = UiKit.label("Customs", "Body", true)
	UiKit.watch_fit(gone)
	host.add_child(gone)
	await process_frame
	await process_frame
	gone.draw.connect(func() -> void: gone.queue_free())
	gone.text = "Customs house"
	for i in 4:
		await process_frame
	check(not is_instance_valid(gone), "10-27 refit oscillation: the label freed in its own draw is gone")
	# freed by a parent's free() in the same process_frame emission that runs its pending refit (a test or a game
	# resuming from `await process_frame`): the refit must not be a lambda that captured the freed control
	var box := VBoxContainer.new()
	host.add_child(box)
	var inner: Label = UiKit.label("Customs", "Body", true)
	UiKit.watch_fit(inner)
	box.add_child(inner)
	await process_frame
	await process_frame
	inner.text = "Customs house"
	await process_frame   # drawn in the last flush: its refit waits on this very process_frame emission
	box.free()
	await process_frame
	await process_frame
	l.queue_free()
	await process_frame
	await process_frame
	check(process_frame.get_connections().size() == pending0, "10-27 refit: a freed control leaves no pending refit (%d → %d)" % [pending0, process_frame.get_connections().size()])
	OS.remove_logger(catch)
	check(catch.seen.is_empty(), "10-27 refit oscillation: no queue overflow / freed call / script error %s" % [catch.seen.slice(0, 3)])
	host.queue_free()
	await process_frame


## Language picker (Patron #108): a list, "System language" first, native names, the current one marked; a tap applies it
## and closes the list. Opening the list never changes the language (an OptionButton popup under the finger did).
func _language() -> void:
	var host := Control.new()
	host.size = Vector2(1280, 720)
	root.add_child(host)
	var names := {"en": "English", "tr": "Türkçe", "de": "Deutsch", "es": "Español", "fr": "Français", "ja": "日本語",
		"ko": "한국어", "pt": "Português", "ru": "Русский", "zh": "中文"}
	var picked: Array = []
	var row := UiLanguage.row("Language", UiLanguage.label("de", names, "System language"), func() -> void: pass)
	check(row.name == "LanguageRow" and row.subtitle == "Deutsch", "language row shows the current language natively")
	check(UiLanguage.label("", names, "System language") == "System language", "system preference shows the system label")
	row.free()   # never entered the tree
	var m: Control = UiLanguage.open(host, {"title": "Language", "system": "System language", "codes": names.keys(),
		"names": names, "current": "de", "on_pick": func(code: String) -> void: picked.append(code)})
	await process_frame
	await process_frame
	var rows := m.find_children("Lang_*", "Button", true, false)
	check(rows.size() == 11, "list: system + 10 languages (%d)" % rows.size())
	check(rows.size() > 0 and rows[0].name == "Lang_system", "System language is the first row")
	check(picked.is_empty(), "opening the list changes nothing")
	var de: Button = m.find_child("Lang_de", true, false)
	check(de != null and de.selected and de.title == "Deutsch", "current language marked, native name")
	var ja: Button = m.find_child("Lang_ja", true, false)
	check(ja != null and ja.title == "日本語", "CJK name as text when no picture is given")
	(m.find_child("Lang_tr", true, false) as Button).pressed.emit()
	await process_frame
	check(picked == ["tr"], "a tap applies exactly that language")
	check(not is_instance_valid(m) or m.is_queued_for_deletion(), "the list closes after a pick")
	await process_frame
	var m2: Control = UiLanguage.open(host, {"title": "Language", "system": "System language", "codes": ["en", "ja"],
		"names": names, "current": "", "pictures": {"ja": PlaceholderTexture2D.new()}, "on_pick": func(code: String) -> void: picked.append(code)})
	await process_frame
	check((m2.find_child("Lang_system", true, false) as Button).selected, "system row marked when no language is chosen")
	check(m2.find_child("Lang_ja", true, false).find_child("NamePicture", true, false) != null, "picture name for CJK when given")
	(m2.find_child("Lang_system", true, false) as Button).pressed.emit()
	await process_frame
	check(picked == ["tr", ""], "system row picks the empty code")
	host.queue_free()
	await process_frame


## #105 (S3: es / fr / ru on 720x1280 left the picture a ~120 px strip under Skip): with a long text the picture still
## keeps >= 30 % of the card, no text is cut, Skip is off the picture, and every text stays >= 21 px.
## Patron 10-25: a CJK title never ends with one character alone on its last line (ja "…ま / す"); Latin words untouched.
func _pager_cjk_tail() -> void:
	var l := Label.new()
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	root.add_child(l)
	var font := l.get_theme_font("font")
	var fs := l.get_theme_font_size("font_size")
	var ja := "ここから避難所が始まります"
	var w := font.get_string_size(ja.substr(0, ja.length() - 1), HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x + 2.0
	check(UiPager._wrap(l, ja, w).size() == 2, "pager: the CJK probe wraps with one character left over")
	var bw: float = UiPager._balanced_width(l, ja, w)
	var lines: Array = UiPager._wrap(l, ja, bw)
	check(bw > 0.0 and bw < w and lines.size() == 2 and not UiPager._lone(ja, lines[1]),
		"pager: a narrower width leaves no lone CJK character (%.0f of %.0f)" % [bw, w])
	check(UiPager._balanced_width(l, "ここから", w) == 0.0, "pager: a CJK line that fits keeps its width")
	var en := "Your haven starts here a"
	check(UiPager._balanced_width(l, en, font.get_string_size("Your haven starts here", HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x + 4.0) == 0.0,
		"pager: word-wrapped text keeps its width")
	var ko := "생존자를 안전하게 맞이하세요"
	var kw := font.get_string_size(ko.substr(0, ko.length() - 1), HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x + 2.0
	var kl: Array = UiPager._wrap(l, ko, kw)
	if kl.size() == 2 and UiPager._lone(ko, kl[1]):
		var kb: float = UiPager._balanced_width(l, ko, kw)
		check(kb > 0.0 and not UiPager._lone(ko, UiPager._wrap(l, ko, kb)[-1]), "pager: a Korean word broken mid-word leaves no lone syllable")
	l.free()


func _pager_long_text() -> void:
	var long := "Construye la estación más grande del valle: contrata personal, abre nuevas rutas, mejora cada andén y mantén felices a los pasajeros mientras la ciudad crece a tu alrededor día tras día. " .repeat(2)
	var img := Image.create(64, 48, false, Image.FORMAT_RGB8)
	img.fill(Color(0.3, 0.5, 0.7))
	var tex := ImageTexture.create_from_image(img)
	for vs in [Vector2i(720, 1280), Vector2i(720, 1560)]:
		root.size = vs
		var pager := UiPager.new()
		pager.size = Vector2(vs)
		root.add_child(pager)
		pager.setup([{"title": "Un título bastante largo para esta página", "body": long, "image": tex},
			{"title": "Segunda", "body": long, "image": tex}], {"next": "Siguiente", "last": "Empezar", "skip": "Saltar"})
		for i in 6:
			await process_frame
		var tag := "long text %dx%d" % [vs.x, vs.y]
		var card := pager.card.get_global_rect()
		var art := pager.art.get_global_rect()
		check(art.size.y >= card.size.y * 0.30 - 1.0, tag + ": the picture keeps >= 30 %% of the card (%d / %d)" % [art.size.y, card.size.y])
		var t := pager.title_label
		var b := pager.body_label
		check(t.get_line_count() * t.get_line_height() <= t.size.y + 1.0 and b.get_line_count() * b.get_line_height() <= b.size.y + 1.0, tag + ": no text cut")
		var floor_px := mini(21, ThemeDB.fallback_font_size) if pager.theme == null and t.theme == null else 21
		check(t.get_theme_font_size("font_size") >= floor_px and b.get_theme_font_size("font_size") >= floor_px, tag + ": text >= %d px (%d / %d)" % [floor_px, t.get_theme_font_size("font_size"), b.get_theme_font_size("font_size")])
		check(not pager.skip_button.get_global_rect().intersects(art), tag + ": Skip is off the picture")
		check(Rect2(Vector2.ZERO, Vector2(vs)).encloses(card), tag + ": the card stays on screen")
		pager.queue_free()
		await process_frame
	root.size = Vector2i(1280, 720)



## #113 r2: the shop in the list-card look (opts.cards) — one badge (the price pill), one grey line, accent edge on the
## featured product, 12 dp gaps, the buy handler untouched, owned = the open pill.
func _shop_cards() -> void:
	var host := _host(Vector2i(720, 1280))
	var holder := VBoxContainer.new()
	holder.name = "iap_shop"
	holder.custom_minimum_size.x = 600
	host.add_child(holder)
	var intro := Label.new()
	intro.text = "Das ganze Spiel ist kostenlos. Nur Werbung entfernen entfernt Pflichtwerbung."
	holder.add_child(intro)
	var hits := []
	for slug in ["bundle", "noads", "funds_s"]:
		var card := PanelContainer.new()
		card.name = "prod_" + slug
		var h := HBoxContainer.new()
		card.add_child(h)
		var v := VBoxContainer.new()
		for t in ["Komplette Kollektion" if slug == "bundle" else slug.capitalize(), "Schnellere Teller. Verdoppelt die Geschwindigkeit aller Stationen dauerhaft, ohne jede Ausnahme und für immer", "einzeln $9.99"]:
			var l := Label.new()
			l.text = t
			l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			v.add_child(l)
		h.add_child(v)
		var buy := Button.new()
		buy.name = "buy"
		buy.text = "Gekauft" if slug == "noads" else "$2.99"
		buy.disabled = slug == "noads"
		buy.pressed.connect(func(): hits.append(slug))
		h.add_child(buy)
		holder.add_child(card)
	var tags := {"funds_s": "Verbrauchsartikel · Werbung bleibt", "bundle": ""}
	var shorts := {"funds_s": UiShop.short_tag(true, true, "de")}
	var shop_opts := {"cards": true, "tag_for": func(s: String) -> String: return str(tags.get(s, "Werbung bleibt")),
		"tag_short_for": func(s: String) -> String: return str(shorts.get(s, UiShop.short_tag(false, true, "de")))}
	UiShop.style(holder, shop_opts)
	UiShop.style(holder, shop_opts)
	for i in 5:
		await process_frame
	var b: PanelContainer = holder.get_node("prod_bundle")
	var n: PanelContainer = holder.get_node("prod_noads")
	var f: PanelContainer = holder.get_node("prod_funds_s")
	check(holder.get_node("shop_intro").theme_type_variation == "NoteCard", "#113 r2 shop: compact note box on top")
	check(b.theme_type_variation == "ListCardDenseSelected" and f.theme_type_variation == "ListCardDense" and (b.get_theme_stylebox("panel") as StyleBoxFlat).border_width_left >= 3, "#113 r2 shop: featured = accent edge, others plain list card")
	for k in [b, n, f]:
		var shown := 0
		for l in k.find_children("*", "Label", true, false):
			if (l as Label).is_visible_in_tree():
				shown += 1
		check(shown == 2, "#113 r2 shop: title + ONE secondary line (%s: %d labels)" % [k.name, shown])
		var sub: Label = k.find_child("Sub", true, false)
		var w := sub.get_theme_font("font").get_string_size(sub.text, HORIZONTAL_ALIGNMENT_LEFT, -1, sub.get_theme_font_size("font_size")).x
		var tagged := str(sub.get_meta("coddy_shop_tag", "")) != ""
		w = UiKit._lines_w(sub.get_theme_font("font"), sub.text, sub.get_theme_font_size("font_size"))
		check(sub.get_line_count() == 1 and not sub.text.contains("\n") and w <= sub.size.x and not sub.text.ends_with("…") and sub.get_theme_font_size("font_size") >= 21,
			"#113 r2 / #136 shop sub: ONE whole line, a policy tag too (%s)" % sub.text)
		var pills := 0
		for bt in k.find_children("*", "Button", true, false):
			pills += 1
		check(pills == 1, "#113 r2 shop: exactly one pill on the right")
		check((k.find_child("tile", true, false) as TextureRect).custom_minimum_size.x >= UiKit.CARD_TILE, "#113 r2 shop: big tile")
		check((k.find_child("buy", true, false) as Button).size.y >= 84, "#113 r2 shop: 48 dp buy target")
	check((n.find_child("buy", true, false) as Button).theme_type_variation == "OwnedButton" and (f.find_child("buy", true, false) as Button).theme_type_variation == "PriceButton", "#113 r2 shop: owned = open pill, price = price pill")
	check(absf(f.position.y - (n.position.y + n.size.y) - float(UiKit.CARD_GAP)) < 0.6, "#113 r2 shop: 12 dp between cards")
	(f.find_child("buy", true, false) as Button).pressed.emit()
	check(hits == ["funds_s"], "#113 r2 shop: the buy handler is untouched")
	# store policy (Patron 10-25): the policy tag ends the visible line on every card that has one, in every language
	for k in [b, n, f]:
		var slug := String(k.name).trim_prefix("prod_")
		var want := str(tags.get(slug, "Werbung bleibt"))
		var sub: Label = k.find_child("Sub", true, false)
		if want == "":
			check(not sub.text.contains("·  "), "policy tag: none for %s" % slug)
			continue
		check(sub.is_visible_in_tree() and (sub.text.ends_with(want) or sub.text.ends_with(str(shop_opts.tag_short_for.call(slug)))) and sub.get_visible_line_count() >= sub.get_line_count() and not sub.text.ends_with("…"),
			"policy tag visible on the card (%s: %s)" % [slug, sub.text])
	host.queue_free()
	await process_frame


class _ErrCount extends Logger:
	var n := 0
	var last := ""
	func _log_error(_f: String, _file: String, _line: int, code: String, rationale: String, _e: bool, _t: int, _bt: Array[ScriptBacktrace]) -> void:
		n += 1
		last = "%s %s" % [code, rationale]
	func _log_message(_m: String, _e: bool) -> void:
		pass


## Patron #132: the shop opened and freed in the same frame (lifeguard / bureaucracy) — no deferred call may touch it.
func _shop_freed_same_frame() -> void:
	var lg := _ErrCount.new()
	OS.add_logger(lg)
	for compact in [false, true]:
		var host := _host(Vector2i(1280, 720))
		var holder := VBoxContainer.new()
		holder.name = "iap_shop"
		host.add_child(holder)
		for slug in ["bundle", "noads", "funds_s"]:
			var card := PanelContainer.new()
			card.name = "prod_" + slug
			var h := HBoxContainer.new()
			card.add_child(h)
			var v := VBoxContainer.new()
			for t in [slug.capitalize(), "Faster plates. Doubles every station, forever", "separately $9.99"]:
				var l := Label.new()
				l.text = t
				v.add_child(l)
			h.add_child(v)
			var buy := Button.new()
			buy.text = "$2.99"
			h.add_child(buy)
			holder.add_child(card)
		UiShop.style(holder, {"cards": not compact, "compact": compact, "tag_for": func(_s: String) -> String: return "Ads stay"})
		if compact:
			host.queue_free()
		else:
			host.free()   # a game closing the shop from a deferred handler frees it before the queued fits run
		for i in 4:
			await process_frame
	OS.remove_logger(lg)
	check(lg.n == 0, "#132 shop freed in the same frame: no deferred error (%d: %s)" % [lg.n, lg.last])


## #132: "tight" shop cards are one 48 dp rect tall with no tile; list_card_side() keeps a list card one row tall.
func _tight_and_side() -> void:
	var host := _host(Vector2i(1280, 720))
	var holder := VBoxContainer.new()
	holder.name = "iap_shop"
	holder.custom_minimum_size.x = 508
	host.add_child(holder)
	for slug in ["noads", "funds_s"]:
		var card := PanelContainer.new()
		card.name = "prod_" + slug
		var h := HBoxContainer.new()
		card.add_child(h)
		var v := VBoxContainer.new()
		for t in [slug.capitalize(), "Faster plates. Doubles every station, forever"]:
			var l := Label.new()
			l.text = t
			v.add_child(l)
		h.add_child(v)
		var buy := Button.new()
		buy.text = "$2.99"
		h.add_child(buy)
		holder.add_child(card)
	UiShop.style(holder, {"cards": true, "tight": true, "tag_for": func(_s: String) -> String: return "One-time · Ads not removed"})
	var c := UiKit.list_card("users", "Lifeguard 1", {"sub": ["tired 0%"], "badge": "Ready"})
	host.add_child(c)
	var b := Button.new()
	b.text = "Rest 40\u00a0s"
	UiKit.list_card_side(c, b, true)
	for i in 5:
		await process_frame
	for slug in ["noads", "funds_s"]:
		var pc: Control = holder.get_node("prod_" + slug)
		check(pc.size.y <= Tokens.TOUCH.min + 2.0 and pc.find_children("*", "TextureRect", true, false).is_empty(),
			"#132 tight shop card %s: one 48 dp rect, no tile (%.0f)" % [slug, pc.size.y])
	var head := c.get_node("Col/Head")
	check(head.get_node_or_null("Badge") == null and head.get_node_or_null("Tile") == null and b.get_parent() == head,
		"#132 list_card_side: action on the head row, badge folded into the line, tile dropped")
	check(str(c.get_node("Col/Head/Texts/Sub").text).begins_with("Ready"), "#132 list_card_side: the badge leads the grey line")
	check(b.size.y >= Tokens.TOUCH.min, "#132 list_card_side: 48 dp touch (%.0f)" % b.size.y)
	host.queue_free()
	await process_frame


## #136 (Critter landscape drawer): on a narrow card the policy tag never drops to a second line or gets cut — the
## short tag takes over; Low drops the tile (one draw less per card).
func _narrow_tag_136() -> void:
	for lang in ["en", "de", "ru", "hi"]:
		var host := _host(Vector2i(1280, 720))
		var holder := VBoxContainer.new()
		holder.name = "iap_shop"
		holder.custom_minimum_size.x = 440   # Critter landscape drawer card width
		holder.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
		host.add_child(holder)
		var card := PanelContainer.new()
		card.name = "prod_funds_s"
		var h := HBoxContainer.new()
		card.add_child(h)
		var v := VBoxContainer.new()
		for t in ["Funds", "A pile of cash for the next build"]:
			var l := Label.new()
			l.text = t
			v.add_child(l)
		h.add_child(v)
		var buy := Button.new()
		buy.text = "$2.99"
		h.add_child(buy)
		holder.add_child(card)
		UiShop.style(holder, {"cards": true, "low": true,
			"tag_for": func(_s: String) -> String: return "Consumable · Ads are not removed by this purchase",
			"tag_short_for": func(_s: String) -> String: return UiShop.short_tag(true, true, lang)})
		for i in 5:
			await process_frame
		var sub: Label = card.find_child("Sub", true, false)
		var w := UiKit._lines_w(sub.get_theme_font("font"), sub.text, sub.get_theme_font_size("font_size"))
		check(not sub.text.contains("\n") and sub.get_line_count() == 1 and w <= sub.size.x + 0.5 and sub.get_theme_font_size("font_size") >= 21
			and sub.text.ends_with(UiShop.short_tag(true, true, lang)), "#136 %s narrow card: short tag on ONE whole line (%s | lines %d w %.0f/%.0f px %d)" % [lang, sub.text, sub.get_line_count(), w, sub.size.x, sub.get_theme_font_size("font_size")])
		check(card.find_children("*", "TextureRect", true, false).is_empty(), "#136 %s Low shop card: no tile" % lang)
		host.queue_free()
		await process_frame


## Patron (disaster e0fcae684e): the one-sentence notes split on the Hindi danda and CJK sentence ends too.
func _sentence_ends() -> void:
	check(UiShop.sub_alts("पूरा गेम मुफ़्त है। केवल विज्ञापन हटाएँ विज्ञापन हटाता है।", true).has("पूरा गेम मुफ़्त है"), "danda: first sentence")
	check(UiShop.sub_alts("ゲームはすべて無料です。広告を削除で消えます。", true).has("ゲームはすべて無料です"), "CJK 。: first sentence")
	check(UiShop.sub_alts("无需付费！只有去广告会移除广告。", true).has("无需付费"), "CJK ！: first sentence")
	check(UiShop.sub_alts("要去广告吗？购买即可。", true).has("要去广告吗"), "CJK ？: first sentence")
	var n := UiKit.list_note("info", ["पूरा गेम मुफ़्त है। ज़बरदस्ती के विज्ञापन केवल विज्ञापन हटाएँ से हटते हैं।"])
	var alts: Array = (n.find_children("*", "Label", true, false)[0] as Label).get_meta("coddy_fit_alts", [])
	check(alts.has("पूरा गेम मुफ़्त है"), "list_note: the danda sentence is an alternative (%s)" % [alts])
	n.free()


## 2026-10-26 tab + modal redesign: page header, close chip, action pill, empty card, Low variants, modal columns that
## stack on a narrow card, a settings row title that never splits a word, the rounded grab handle.
func _redesign_1026() -> void:
	# design 1 (the default) = the old W64 surfaces exactly: a game that has not adopted the redesign is unchanged
	UiKit.design = 1
	var v1 := UiKit.use("pest-patrol")
	var v1s := v1.get_stylebox("panel", "Sheet") as StyleBoxFlat
	check(v1s.bg_color.is_equal_approx(UiKit.c("surface")) and v1s.content_margin_left == 32.0 and (v1.get_stylebox("panel", "SheetBottom") as StyleBoxFlat).corner_radius_bottom_left > 0,
		"redesign: design 1 keeps the old sheet surfaces")
	var h0 := _host(Vector2i(720, 1600))
	var m0 := UiModal.new()
	h0.add_child(m0)
	m0.setup("Away", {"icon": "clock", "close": true})
	var cols0: Array = m0.columns(2)
	check(m0.card.find_child("Grabber", true, false) is ColorRect and m0.close_button.theme_type_variation == "IconButtonFlat"
		and (cols0[0] as Control).get_parent() is VBoxContainer, "redesign: design 1 modal = old grab bar + flat X (sheet columns stack as before)")
	h0.queue_free()
	await process_frame
	UiKit.design = 2
	var host := _host(Vector2i(720, 1600))
	var col := VBoxContainer.new()
	col.custom_minimum_size.x = 656
	host.add_child(col)
	var h := UiKit.page_header("users", "Crew", func() -> void: pass, {"sub": ["No field chief yet: crews take the oldest call after a while.", "No field chief yet"], "close_label": "Close"})
	col.add_child(h.box)
	var c := UiKit.list_card("package", "Hire crew", {"sub": ["+1 truck and crew"], "badge": "Lv 1/8"})
	col.add_child(c)
	UiKit.card_progress(c, 1.0, 8.0)
	var pill := UiKit.action_pill("1,320 Münzen", func() -> void: pass, true)
	UiKit.list_card_side(c, pill)
	var low_c := UiKit.list_card("package", "Hire crew", {"sub": ["+1 truck and crew"], "badge": "Lv 1/8", "low": true, "flat_low": true})
	col.add_child(low_c)
	var low_pill := UiKit.action_pill("450 coins", func() -> void: pass, false, "primary", "coins", true)
	UiKit.list_card_side(low_c, low_pill)
	var e := UiKit.empty_card("lock", "Buy City contracts in Invest", "Buy City contracts in Invest to get offers from the town hall.", "Invest", func() -> void: pass)
	col.add_child(e)
	var el := UiKit.empty_card("lock", "Buy City contracts in Invest", "body", "Invest", func() -> void: pass, true)
	col.add_child(el)
	for i in 6:
		await process_frame
	var close: Button = h.close
	check(close.size.x >= Tokens.TOUCH.min and close.size.y >= Tokens.TOUCH.min and close.theme_type_variation == "CloseButton",
		"redesign: page close chip keeps the 48 dp touch rect (%s)" % close.size)
	var cs := close.get_theme_stylebox("normal") as StyleBoxFlat
	check(cs != null and cs.expand_margin_left < 0.0, "redesign: the close chip is drawn smaller than its touch rect")
	check((h.title as Label).get_theme_font_size("font_size") == Tokens.TYPE.title.size and (h.sub as Label).visible,
		"redesign: page title at Title size + a visible summary line")
	check((h.sub as Label).get_line_count() == 1, "redesign: the summary line stays one line (%s)" % (h.sub as Label).text)
	UiKit.page_header_set(h, "trending-up", "Invest", "")
	await process_frame
	check(not (h.sub as Label).visible and (h.title as Label).text == "Invest", "redesign: page_header_set swaps title, hides an empty line")
	check(pill.get_parent() == c.get_node("Col/Head") and pill.autowrap_mode == TextServer.AUTOWRAP_OFF and pill.size.y <= Tokens.TOUCH.min + 1.0,
		"redesign: the action pill sits on the head row on ONE line")
	check(pill.size.y >= Tokens.TOUCH.min and c.theme_type_variation == "ListCardDense", "redesign: pill row = one 48 dp rect, dense card")
	check(c.get_node_or_null("Col/Head/Texts/Progress") is ProgressBar, "redesign: Medium level bar in the text column")
	check(low_pill.theme_type_variation == "ActionText" and low_pill.icon == null and low_pill.get_theme_stylebox("normal") is StyleBoxEmpty,
		"redesign: Low action = coloured text, no pill box, no icon")
	check(low_c.theme_type_variation == "ListCardDenseFlat" and low_c.get_theme_stylebox("panel") is StyleBoxEmpty, "redesign: Low flat row has no box")
	check(e.theme_type_variation == "EmptyCard" and e.find_child("Action", true, false) != null and e.find_child("Body", true, false) != null,
		"redesign: empty card with its action")
	check(el.theme_type_variation == "ListCardFlat" and el.find_child("Body", true, false) == null and el.find_children("*", "TextureRect", true, false).is_empty(),
		"redesign: Low empty card = no box, no tile, no body line")
	host.queue_free()
	await process_frame
	# a centred card on a portrait phone stacks its columns; a settings row title keeps whole words
	var host2 := _host(Vector2i(720, 1600))
	var m := UiModal.new()
	host2.add_child(m)
	m.setup("Settings", {"icon": "settings", "width": 1180, "close": true, "sheet": false})
	var cols: Array = m.columns(2)
	check((cols[0] as Control).get_parent() is VBoxContainer, "redesign: two modal columns stack on a 720 px wide screen")
	var row := UiRow.toggle("music", "Reduce motion", false)
	(cols[0] as VBoxContainer).add_child(row)
	for i in 6:
		await process_frame
	var t: Label = row.get_node("Box/Texts/Title")
	check(t.get_line_count() <= 2, "redesign: a settings row title never splits letter by letter (%d lines)" % t.get_line_count())
	check(m.close_button != null and m.close_button.theme_type_variation == "CloseButton", "redesign: modal close = the fleet close chip")
	host2.queue_free()
	await process_frame
	var host3 := _host(Vector2i(720, 1600))
	var sh := UiModal.new()
	host3.add_child(sh)
	sh.setup("Away", {"icon": "clock"})
	for i in 3:
		await process_frame
	var grab := sh.card.find_child("Grabber", true, false)
	check(sh.sheet and grab is PanelContainer and (grab as Control).get_theme_stylebox("panel") is StyleBoxFlat, "redesign: bottom sheet grab handle is a rounded pill")
	var sb := sh.card.get_theme_stylebox("panel") as StyleBoxFlat
	check(sb.corner_radius_bottom_left == 0 and sb.corner_radius_top_left > 0, "redesign: bottom sheet rounded at the top only")
	host3.queue_free()
	await process_frame
	var w1 := UiKit.use("pest-patrol")
	check(w1.get_stylebox("panel", "Sheet") is StyleBoxFlat and (w1.get_stylebox("panel", "Sheet") as StyleBoxFlat).bg_color.is_equal_approx(UiKit.c("background")),
		"redesign: a sheet is one step darker than its cards")
	UiKit.design = 1
	UiKit.use("canal-control")

## Owner 10-26 (Döner on a 3-button phone): the bottom tabs sat under the Android navigation bar — Godot's immersive safe
## area holds only the cutout. UiSafeArea pushes the safe rect off the navigation bar (visible or not).
func _safe_area() -> void:
	var scr := Vector2i(1080, 2340)
	var cut := Rect2i(0, 110, 1080, 2230)   # immersive Godot: cutout at the top only, nothing at the bottom
	check(UiSafeArea.combine(cut, scr, Vector4.ZERO) == cut, "safe area: no nav bar → the engine rect unchanged")
	var three := UiSafeArea.combine(cut, scr, Vector4(0, 0, 0, 126))
	check(three == Rect2i(0, 110, 1080, 2340 - 126 - 110), "safe area: 3-button bar (126 px) lifts the bottom edge %s" % three)
	var gesture := UiSafeArea.combine(cut, scr, Vector4(0, 0, 0, 63))
	check(gesture.end.y == 2340 - 63, "safe area: gesture handle strip reserved too")
	var deep := Rect2i(0, 110, 1080, 2340 - 110 - 200)   # engine already insets more than the bar → keep it
	check(UiSafeArea.combine(deep, scr, Vector4(0, 0, 0, 126)) == deep, "safe area: a deeper engine inset wins")
	var land := UiSafeArea.combine(Rect2i(110, 0, 2340 - 110, 1080), Vector2i(2340, 1080), Vector4(0, 0, 126, 0))
	check(land == Rect2i(110, 0, 2340 - 110 - 126, 1080), "safe area: landscape 3-button bar on the right %s" % land)
	check(UiSafeArea.combine(Rect2i(), scr, Vector4(0, 0, 0, 126)) == Rect2i(0, 0, 1080, 2340 - 126), "safe area: empty engine rect → screen minus the bar")
	check(UiSafeArea.combine(cut, scr, Vector4(0, 0, 0, 5000)) == cut, "safe area: nonsense insets keep the engine rect")
	var e := UiSafeArea.to_canvas(three, scr, Vector2(720, 1560))
	check(is_equal_approx(e.w, 126.0 * 1560.0 / 2340.0) and is_equal_approx(e.y, 110.0 * 1560.0 / 2340.0) and e.x == 0.0 and e.z == 0.0,
		"safe area: canvas gaps scale per axis %s" % e)
	check(UiSafeArea.to_canvas(Rect2i(), scr, Vector2(720, 1560)) == Vector4.ZERO, "safe area: empty rect → no gaps")
	check(UiSafeArea.parse_insets("Insets{left=0, top=63, right=0, bottom=126}") == Vector4(0, 63, 0, 126), "safe area: android Insets.toString parsed")
	check(UiSafeArea.parse_insets("") == Vector4.ZERO, "safe area: unparsable → zero")
	UiSafeArea.nav_override = Vector4(0, 0, 0, 126)
	check(UiSafeArea.nav_insets() == Vector4(0, 0, 0, 126), "safe area: test override reaches nav_insets()")
	UiSafeArea.nav_override = Vector4(-1, -1, -1, -1)
	check(UiSafeArea.nav_insets() == Vector4.ZERO, "safe area: off Android there is no navigation-bar inset")
	check(UiSafeArea.rect() == DisplayServer.get_display_safe_area(), "safe area: off Android rect() is the engine value")
	check(UiSafeArea.overlay_edges(Vector2(720, 1600)) == Vector4.ZERO, "safe area: overlays have no gaps on a desktop window")


## Owner 10-27 (Borderline intro on a 3-button phone): the mentor panel's Continue sat on the navigation bar and "Skip
## intro" under the status bar. Overlays use overlay_edges(): edges() + the status bar (visible or not) at the top; the
## kit modal / coach / toast keep their buttons and text inside it even on a full-screen host.
func _safe_overlays() -> void:
	UiSafeArea.simulate(Vector2i(1080, 2400), Vector4(0, 110, 0, 126))
	check(UiSafeArea.simulating() and UiSafeArea.rect() == Rect2i(0, 110, 1080, 2400 - 236), "safe sim: rect() is the simulated phone's")
	var e := UiSafeArea.overlay_edges(Vector2(720, 1600))
	check(is_equal_approx(e.y, 110.0 * 1600.0 / 2400.0) and is_equal_approx(e.w, 126.0 * 1600.0 / 2400.0), "safe sim: overlay gaps in canvas px %s" % e)
	UiSafeArea.simulate(Vector2i(1080, 2400), Vector4(0, 0, 0, 126))
	check(UiSafeArea.overlay_edges(Vector2(720, 1600)).y == 0.0, "safe sim: no status bar → no top gap")
	check(UiSafeArea.inner_gaps(Vector4(0, 40, 0, 84), Rect2(0, 60, 720, 1440), Vector2(720, 1600)) == Vector4(0, 0, 0, 0),
		"safe: a layer already inside the safe band keeps nothing extra")
	check(UiSafeArea.inner_gaps(Vector4(0, 40, 0, 84), Rect2(0, 0, 720, 1600), Vector2(720, 1600)) == Vector4(0, 40, 0, 84),
		"safe: a full-screen layer keeps the whole gaps")
	UiSafeArea.simulate(Vector2i(1080, 2400), Vector4(0, 110, 0, 126))
	var size := Vector2i(720, 1600)
	var gap := Vector4(0, 110.0 * 1600.0 / 2400.0, 0, 126.0 * 1600.0 / 2400.0)
	var host := _host(size)
	var m := UiModal.new()
	host.add_child(m)
	m.setup("Graphics for this device", {"icon": "gauge", "close": true})
	m.body.add_child(UiKit.label("Graphics start at Medium.", "BodyMuted", true))
	var ok := m.add_action("Continue", "primary", Callable(), "Continue")
	for i in 3:
		await process_frame
	var card := m.get_child(1) as Control
	check(m.sheet and is_equal_approx(card.get_global_rect().end.y, size.y), "safe sheet: the background still runs to the bottom edge")
	check(ok.get_global_rect().end.y <= size.y - gap.w + 0.5, "safe sheet: Continue sits above the navigation bar (%s)" % ok.get_global_rect())
	host.queue_free()
	await process_frame
	var land := _host(Vector2i(1600, 720))
	UiSafeArea.simulate(Vector2i(2400, 1080), Vector4(0, 63, 126, 0))
	var m2 := UiModal.new()
	land.add_child(m2)
	m2.setup("Welcome back", {"icon": "gauge", "close": true, "width": 1560})
	m2.body.add_child(UiKit.label("While you were away…", "BodyMuted", true))
	m2.add_action("Collect", "primary", Callable(), "Collect")
	for i in 3:
		await process_frame
	var r2 := (m2.get_child(1) as Control).get_global_rect()
	check(r2.end.x <= 1600 - 126.0 * 1600.0 / 2400.0 + 0.5 and r2.position.y >= 63.0 * 720.0 / 1080.0 - 0.5,
		"safe dialog (landscape): the card clears the right-side bar and the status bar %s" % r2)
	land.queue_free()
	await process_frame
	UiSafeArea.simulate(Vector2i(1080, 2400), Vector4(0, 110, 0, 126))
	var h3 := _host(size)
	var coach := UiCoach.new()
	h3.add_child(coach)
	coach.setup({"skip": "Skip guide", "hint": "Tap the highlighted button"})
	coach.show_step("Open the shop", "Step 1 of 5", Rect2(20, size.y - 150, 200, 120))
	for i in 3:
		await process_frame
	var br := coach.bubble_rect()
	check(br.position.y >= gap.y and br.end.y <= size.y - gap.w, "safe coach: the bubble stays between the bars %s" % br)
	coach.show_step("Welcome", "", Rect2())
	for i in 3:
		await process_frame
	check(coach.bubble_rect().end.y <= size.y - gap.w, "safe coach: no-spotlight bubble above the navigation bar %s" % coach.bubble_rect())
	coach.hide_step()   # 10-27: a guide card up holds an ordinary toast back (kit round 2)
	var toast := UiToast.new()
	h3.add_child(toast)
	toast.setup({"top": 10})
	toast.show_toast("Saved!", "success")
	await create_timer(0.45).timeout   # after the slide-in
	check(toast.card().position.y >= gap.y, "safe toast: below the status bar on a full-screen layer (%s)" % toast.card().position)
	h3.queue_free()
	await process_frame
	UiSafeArea.simulate(Vector2i.ZERO, Vector4.ZERO)
	check(not UiSafeArea.simulating() and UiSafeArea.overlay_edges(Vector2(720, 1600)) == Vector4.ZERO, "safe sim: off again")
	await _safe_layout_1028()
	_hidden_nav_1030()


## Owner 10-30 (Chaiwala, immersive 3-button phone: a ~56 dp dark strip under the house banner — the HIDDEN navigation
## bar's full height was reserved). Shown bar → reserved in full; hidden → only the gesture strip / MIN_EDGE_DP.
func _hidden_nav_1030() -> void:
	check(UiSafeArea.reserve_side(144, 144, 0, 24) == 144.0, "hidden nav: a SHOWN 3-button bar is reserved in full")
	check(UiSafeArea.reserve_side(144, 0, 0, 24) == 24.0, "hidden nav: a hidden 3-button bar keeps only the 8 dp margin")
	check(UiSafeArea.reserve_side(48, 0, 48, 24) == 48.0, "hidden nav: gesture navigation keeps its handle strip")
	check(UiSafeArea.reserve_side(16, 0, 0, 24) == 16.0, "hidden nav: never more than the bar itself")
	check(UiSafeArea.reserve_side(0, 0, 0, 24) == 0.0, "hidden nav: no bar → nothing")
	check(UiSafeArea.reserve(Vector4(0, 0, 126, 0), Vector4.ZERO, Vector4.ZERO, 24) == Vector4(0, 0, 24, 0), "hidden nav: landscape side bar too")
	UiSafeArea.simulate(Vector2i(1080, 1920), Vector4(0, 0, 0, 144), 72.0, "buttons")
	check(UiSafeArea.rect() == Rect2i(0, 72, 1080, 1920 - 72 - 24), "hidden nav sim: 3 buttons hidden → 24 px bottom %s" % UiSafeArea.rect())
	check(UiSafeArea.rect().position.y == 72, "hidden nav sim: the status bar is still reserved at the top")
	UiSafeArea.simulate(Vector2i(1080, 2400), Vector4(0, 110, 0, 48), 110.0, "gesture")
	check(UiSafeArea.rect().end.y == 2400 - 48, "hidden nav sim: gesture nav keeps its 48 px strip")
	UiSafeArea.simulate(Vector2i(2400, 1080), Vector4(110, 0, 144, 0), 63.0, "buttons")
	check(UiSafeArea.rect() == Rect2i(110, 63, 2400 - 110 - 24, 1080 - 63), "hidden nav sim: landscape — the cutout stays, the hidden side bar shrinks %s" % UiSafeArea.rect())
	UiSafeArea.simulate(Vector2i(1080, 1920), Vector4(0, 0, 0, 144), 72.0)
	check(UiSafeArea.rect().end.y == 1920 - 144, "hidden nav sim: default (bar shown) unchanged")
	UiSafeArea.simulate(Vector2i.ZERO, Vector4.ZERO)


## Patron 10-28 (S1 banner audit): HUDs that read rect() — the cutout only, on a phone in immersive mode — sat under the
## status bar. A 3-value simulation keeps the status bar apart from the cutout; SafeLayout.scan finds text / buttons in
## the bands.
func _safe_layout_1028() -> void:
	const SafeLayout = preload("safe_layout.gd")
	UiSafeArea.simulate(Vector2i(1080, 1920), Vector4(0, 0, 0, 144), 72.0)
	check(UiSafeArea.rect() == Rect2i(0, 72, 1080, 1920 - 72 - 144), "safe sim 3: rect() reserves the status bar on a phone without a cutout %s" % UiSafeArea.rect())
	var e := UiSafeArea.overlay_edges(Vector2(720, 1280))
	check(is_equal_approx(e.y, 48.0) and is_equal_approx(e.w, 96.0), "safe sim 3: overlay_edges reserves the status bar %s" % e)
	check(UiSafeArea.edges(Vector2(720, 1280)) == e, "safe sim 3: edges() == overlay_edges() (the HUD keeps off the status bar too)")
	UiSafeArea.simulate(Vector2i(1080, 2400), Vector4(0, 140, 0, 126), 100.0)
	check(UiSafeArea.rect().position.y == 140, "safe sim 3: a cutout deeper than the status bar wins")
	UiSafeArea.simulate(Vector2i(2400, 1080), Vector4(110, 0, 126, 0), 63.0)
	check(UiSafeArea.rect() == Rect2i(110, 63, 2400 - 236, 1080 - 63), "safe sim 3: landscape — cutout at the side, the status bar still on top %s" % UiSafeArea.rect())
	UiSafeArea.simulate(Vector2i.ZERO, Vector4.ZERO)
	check(UiSafeArea.sim_status < 0.0, "safe sim 3: a 2-value simulate() clears the status value")
	check(SafeLayout.phones(false).size() == 6 and SafeLayout.phones(true).size() == 6, "safe layout: six phones per orientation (2 with a hidden bar)")
	var was := root.size
	var phone: Dictionary = SafeLayout.phones(false)[1]
	SafeLayout.set_phone(root, phone)
	await process_frame
	var host := Control.new()
	host.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(host)
	var top := Button.new()
	top.text = "Settings"
	top.position = Vector2(20, 4)
	top.size = Vector2(120, 84)
	host.add_child(top)
	var ok := Label.new()
	ok.text = "Cash"
	ok.position = Vector2(20, 200)
	host.add_child(ok)
	var bg := Panel.new()   # a background may run to the edge
	bg.position = Vector2.ZERO
	bg.size = Vector2(720, 100)
	host.add_child(bg)
	await process_frame
	var bad := SafeLayout.scan(root, {"from": host})
	check(bad.size() == 1 and str(bad[0]).begins_with("top") and "Settings" in str(bad[0]), "safe layout: a button under the status bar is found, the label and panel are not %s" % [bad])
	var safe := SafeLayout.safe_rect(root)
	check(safe.position.y > 8.0 and safe.end.y < root.get_visible_rect().size.y - 8.0, "safe layout: the safe rect leaves both bars %s" % safe)
	top.position.y = safe.position.y + 4.0
	ok.position.y = safe.position.y + 120.0
	await process_frame
	check(SafeLayout.scan(root, {"from": host}).is_empty(), "safe layout: below the status bar → clean %s" % [SafeLayout.scan(root, {"from": host})])
	var low := Button.new()
	low.text = "Tab"
	low.position = Vector2(0, root.get_visible_rect().size.y - 90)
	low.size = Vector2(200, 90)
	host.add_child(low)
	await process_frame
	check(SafeLayout.scan(root, {"from": host}).size() == 1, "safe layout: a tab on the navigation bar is found")
	check(SafeLayout.overlaps(low, top) == false and SafeLayout.overlaps(low, low), "safe layout: overlaps() on screen rects")
	host.free()
	SafeLayout.clear(root, was)
