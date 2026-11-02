extends CanvasLayer
## House interstitial card (owner 2026-10-25, Emir #125: "şık interstitialler yapıp AdMob yerine kendi oyunlarımızın
## reklamlarını birbirlerine yapabiliriz"). Created by CoddyAds (copy next to CoddyAds.gd + CoddyBanner.gd, unchanged).
## Full-screen card for ONE of our other games:
##   hero  — the creative's hero images (ads-config `heroImageUrls`, else `interstitialImageUrl`; downscaled ≤ HERO_PX,
##           disk-cached). 2–3 frames cross-fade with a slow pan on Medium / High; Low (or `animated = false`) = one
##           still frame, no tween at all. No hero → the icon large on the game's colours (`brandColor`, else the
##           icon's average colour).
##   info  — icon + title (one line, shrinks then the short name; never "…") + the player's-language slogan
##           (`sloganI18n`, one line, hidden when missing / banned / too long — same rules as the banner) + a big
##           Install button (kit primary button when the game ships coddy_ui) → the store page (utm referrer).
##   close — top corner, a COUNTDOWN_S countdown first (disabled), then an X. Never a "free / trial" claim.
## The game is paused by CoddyAds while this is on screen; everything here runs in PROCESS_MODE_ALWAYS.
## Per-frame cost: zero on Low (no _process, no tween); Medium+ only engine tweens on 2–3 TextureRects.

signal finished(opened_store: bool)

const COUNTDOWN_S := 3
const SLIDE_S := 4.5              ## one hero frame on screen (Medium+)
const FADE_S := 1.0               ## cross-fade between frames
const PAN := 1.06                 ## slow zoom/pan scale over one frame
const HERO_PX := 512              ## hero images are downscaled to this (longest side) before caching
const AD_LABEL := "AD"

var ads: Node
var creative: Dictionary
var heroes: Array = []            ## Texture2D frames (1–3)
var icon: Texture2D = null
var animated := true
var countdown_left := COUNTDOWN_S
var close_btn: Button
var install_btn: Button
var hero_rects: Array = []
var title_label: Label
var slogan_label: Label
var _timer: Timer
var _tween: Tween
var _frame := 0
var _done := false
var _banner_script: Script


func _init(owner_ads: Node = null, c: Dictionary = {}, hero_textures: Array = [], icon_tex: Texture2D = null,
		animate := true) -> void:
	ads = owner_ads
	creative = c
	heroes = hero_textures.filter(func(t): return t is Texture2D)
	icon = icon_tex
	animated = animate and heroes.size() > 1
	layer = 128
	process_mode = Node.PROCESS_MODE_ALWAYS
	name = "CoddyInterstitial"


func _ready() -> void:
	var p: String = get_script().resource_path.get_base_dir().path_join("CoddyBanner.gd")
	_banner_script = load(p) if ResourceLoader.exists(p) else null
	_build()
	_timer = Timer.new()
	_timer.wait_time = 1.0
	_timer.process_mode = Node.PROCESS_MODE_ALWAYS
	_timer.timeout.connect(tick)
	add_child(_timer)
	_timer.start()
	if animated:
		_start_slides()


## One second of the close countdown (Timer; tests call it directly).
func tick() -> void:
	if countdown_left <= 0:
		return
	countdown_left -= 1
	_update_close()
	if countdown_left <= 0 and _timer != null:
		_timer.stop()


func close_enabled() -> bool:
	return close_btn != null and not close_btn.disabled


## This card is also shipped WITHOUT CoddyBanner (Driftwood). Remote titles must still
## be checked against the bundled subset; the optional banner must not disable this guard.
static func can_draw(font: Font, text: String) -> bool:
	if font == null:
		return text.is_empty()
	for i in text.length():
		if text.unicode_at(i) > 32 and not font.has_char(text.unicode_at(i)):
			return false
	return true

static func safe_title(font: Font, localized: String, english: String) -> String:
	if can_draw(font, localized):
		return localized
	return english if can_draw(font, english) else ""


func _lang() -> String:
	return str(ads.ui_lang()) if ads != null and ads.has_method("ui_lang") else TranslationServer.get_locale().substr(0, 2)


func _pick(m) -> String:
	return str(ads._pick_i18n(m)) if ads != null and ads.has_method("_pick_i18n") else str((m if m is Dictionary else {}).get("en", ""))


func _kit_theme() -> Theme:
	const KIT := "res://addons/coddy_ui/ui_kit.gd"
	return load(KIT).theme() if ResourceLoader.exists(KIT) else null


## The creative's colour: `brandColor` ("#rrggbb") when configured, else the icon's average colour, else a deep navy.
func brand_color() -> Color:
	var s := str(creative.get("brandColor", ""))
	if s != "" and Color.html_is_valid(s):
		return Color.html(s)
	if icon != null:
		var img := icon.get_image()
		if img != null:
			img = img.duplicate()
			if img.is_compressed():
				img.decompress()
			img.resize(1, 1, Image.INTERPOLATE_BILINEAR)
			var c := img.get_pixel(0, 0)
			return Color.from_hsv(c.h, clampf(c.s, 0.35, 0.8), clampf(c.v, 0.35, 0.6))
	return Color(0.09, 0.13, 0.25)


func _canvas() -> Vector2:
	var vp := get_viewport()
	return vp.get_visible_rect().size if vp != null else Vector2(720, 1280)


func _build() -> void:
	var vp := _canvas()
	var u := minf(vp.x, vp.y) / 360.0          # canvas px per dp (the card fills the screen; deterministic)
	var landscape := vp.x > vp.y
	var kt := _kit_theme()
	var root := Control.new()
	root.name = "Root"
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_STOP
	if kt != null:
		root.theme = kt
	add_child(root)

	var base := brand_color()
	var bg := TextureRect.new()
	bg.name = "Background"
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	var g := Gradient.new()
	g.set_color(0, base.lightened(0.08))
	g.set_color(1, base.darkened(0.72))
	var gt := GradientTexture2D.new()
	gt.gradient = g
	gt.width = 16
	gt.height = 64
	gt.fill_from = Vector2(0.5, 0.0)
	gt.fill_to = Vector2(0.5, 1.0)
	bg.texture = gt
	bg.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	bg.stretch_mode = TextureRect.STRETCH_SCALE
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(bg)

	# Layout (Patron 2nd round): PORTRAIT = header (icon + title + slogan) at the top, 2–3 real portrait screenshots as
	# one tall card in the middle (slow cross-fade + drift on Medium+, one still frame on Low), a big Install at the
	# bottom — no empty bands. LANDSCAPE = landscape screenshots on the left, the info column on the right.
	# Frames keep their aspect (store art carries text — never crop it).
	var top := roundf(64.0 * u)
	var pad := roundf(20.0 * u)
	var f: Font = kt.default_font if kt != null and kt.default_font != null else ThemeDB.fallback_font
	var head: BoxContainer = VBoxContainer.new() if landscape else HBoxContainer.new()   # landscape: icon above the text
	head.name = "Head"
	head.add_theme_constant_override("separation", int((8.0 if landscape else 14.0) * u))
	var icon_px := roundf((64.0 if landscape else 56.0) * u)
	if icon != null:   # an ad layer without a cached icon (older bridged games): title + slogan only
		var ic := _rounded_icon(icon_px, u)
		if landscape:
			ic.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
		head.add_child(ic)
	var texts := VBoxContainer.new()
	texts.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	texts.alignment = BoxContainer.ALIGNMENT_CENTER
	texts.add_theme_constant_override("separation", int(2.0 * u))
	head.add_child(texts)
	var title := _pick(creative.get("titleI18n", {}))
	title = safe_title(f, title, str(creative.get("titleI18n", {}).get("en", "")))
	title_label = _line("title", title, roundf(22.0 * u), Color.WHITE)
	if _banner_script != null:
		var short: String = _banner_script.short_title(creative, title, _lang())
		if can_draw(f, short): title_label.set_meta("short_title", short)
		var tmin := int(roundf(16.0 * u))
		title_label.resized.connect(func() -> void: fit_line(title_label, int(roundf(22.0 * u)), tmin))
	texts.add_child(title_label)
	var slogan := ""
	if _banner_script != null:
		slogan = _banner_script.slogan_for(creative, _lang())
		if not can_draw(f, slogan):
			slogan = ""
	slogan_label = _line("subtitle", slogan, roundf(15.0 * u), Color(1, 1, 1, 0.82))
	slogan_label.visible = slogan != ""
	if _banner_script != null and slogan != "":
		var smin := int(roundf(13.0 * u))
		slogan_label.resized.connect(func() -> void: fit_line(slogan_label, int(roundf(15.0 * u)), smin))
	texts.add_child(slogan_label)
	if landscape:
		title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		slogan_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	install_btn = _install_button(u, kt)

	var region: Rect2
	if landscape:
		region = Rect2(Vector2(pad, top), Vector2(vp.x * 0.55 - pad - 8.0 * u, vp.y - top - pad))
	else:
		var head_h := maxf(icon_px, 50.0 * u)
		var btn_h := roundf(56.0 * u)
		var head_y := top
		head.position = Vector2(pad, head_y)
		head.size = Vector2(vp.x - 2.0 * pad, head_h)
		install_btn.position = Vector2(pad, vp.y - pad - btn_h - roundf(8.0 * u))
		install_btn.size = Vector2(vp.x - 2.0 * pad, btn_h)
		var r_top := head_y + head_h + roundf(16.0 * u)
		region = Rect2(Vector2(pad, r_top), Vector2(vp.x - 2.0 * pad, install_btn.position.y - roundf(16.0 * u) - r_top))
	var aspect := 9.0 / 16.0 if not landscape else 16.0 / 9.0
	if not heroes.is_empty():
		var t0: Texture2D = heroes[0]
		aspect = float(t0.get_width()) / maxf(1.0, float(t0.get_height()))
	var hs := Vector2(region.size.x, region.size.x / aspect)
	if hs.y > region.size.y:
		hs = Vector2(region.size.y * aspect, region.size.y)
	var hero_rect := Rect2(region.position + (region.size - hs) / 2.0, hs)
	var hero := Panel.new()
	hero.name = "Hero"
	hero.position = hero_rect.position
	hero.size = hero_rect.size
	hero.clip_children = CanvasItem.CLIP_CHILDREN_AND_DRAW
	var hsb := StyleBoxFlat.new()
	hsb.bg_color = base.darkened(0.35)
	hsb.set_corner_radius_all(int(18.0 * u))
	hsb.shadow_color = Color(0, 0, 0, 0.35)
	hsb.shadow_size = int(10.0 * u)
	hsb.anti_aliasing = true
	hero.add_theme_stylebox_override("panel", hsb)
	hero.mouse_filter = Control.MOUSE_FILTER_STOP
	hero.gui_input.connect(_on_hero_input)
	root.add_child(hero)
	if heroes.is_empty():
		_build_icon_hero(hero, hero_rect.size, u, base)
	else:
		for i in heroes.size():
			var tr := TextureRect.new()
			tr.name = "Frame%d" % i
			tr.texture = heroes[i]
			tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			tr.size = hero_rect.size
			tr.pivot_offset = hero_rect.size / 2.0
			tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
			tr.modulate.a = 1.0 if i == 0 else 0.0
			tr.visible = i == 0 or animated
			hero.add_child(tr)
			hero_rects.append(tr)

	if landscape:
		var info_rect := Rect2(Vector2(vp.x * 0.55, top * 0.5), Vector2(vp.x * 0.45, vp.y - top))
		var max_w := minf(info_rect.size.x - 2.0 * pad, 420.0 * u)
		var col := VBoxContainer.new()
		col.name = "Info"
		col.alignment = BoxContainer.ALIGNMENT_CENTER
		col.add_theme_constant_override("separation", int(10.0 * u))
		col.position = Vector2(info_rect.position.x + (info_rect.size.x - max_w) / 2.0, info_rect.position.y)
		col.size = Vector2(max_w, minf(info_rect.size.y - pad, 260.0 * u))
		root.add_child(col)
		col.add_child(head)
		var gap := Control.new()
		gap.custom_minimum_size = Vector2(0, 6.0 * u)
		col.add_child(gap)
		col.add_child(install_btn)
	else:
		root.add_child(head)
		root.add_child(install_btn)

	# "AD" chip (top-left) + close (top-right)
	var chip := Label.new()
	chip.name = "AdChip"
	chip.text = AD_LABEL
	chip.add_theme_font_size_override("font_size", int(roundf(10.0 * u)))
	chip.add_theme_color_override("font_color", Color(1, 1, 1, 0.85))
	var csb := StyleBoxFlat.new()
	csb.bg_color = Color(0, 0, 0, 0.45)
	csb.set_corner_radius_all(int(6.0 * u))
	csb.content_margin_left = 6.0 * u
	csb.content_margin_right = 6.0 * u
	csb.content_margin_top = 2.0 * u
	csb.content_margin_bottom = 2.0 * u
	chip.add_theme_stylebox_override("normal", csb)
	chip.position = Vector2(12.0 * u, 12.0 * u)
	root.add_child(chip)

	close_btn = Button.new()
	close_btn.name = "Close"
	var cs := roundf(40.0 * u)
	close_btn.flat = true
	close_btn.custom_minimum_size = Vector2(cs, cs)
	close_btn.size = Vector2(cs, cs)
	close_btn.position = Vector2(vp.x - cs - 12.0 * u, 12.0 * u)
	close_btn.focus_mode = Control.FOCUS_NONE
	for st in ["normal", "hover", "pressed", "disabled", "focus", "hover_pressed"]:
		close_btn.add_theme_stylebox_override(st, StyleBoxEmpty.new())
	var face := Control.new()   # circle + countdown digit / X, drawn (no font glyph or theme stylebox involved)
	face.name = "X"
	face.mouse_filter = Control.MOUSE_FILTER_IGNORE
	face.set_anchors_preset(Control.PRESET_FULL_RECT)
	var digit_font: Font = kt.default_font if kt != null and kt.default_font != null else ThemeDB.fallback_font
	face.draw.connect(func() -> void:
		var sz := face.size
		var c := sz / 2.0
		face.draw_circle(c, sz.x / 2.0, Color(0, 0, 0, 0.55), true, -1.0, true)
		face.draw_circle(c, sz.x / 2.0 - 0.5, Color(1, 1, 1, 0.28), false, maxf(1.0, u), true)
		if countdown_left > 0:
			var fs := int(roundf(16.0 * u))
			var txt := str(countdown_left)
			var w := digit_font.get_string_size(txt, HORIZONTAL_ALIGNMENT_LEFT, -1, fs)
			face.draw_string(digit_font, Vector2(c.x - w.x / 2.0, c.y + (digit_font.get_ascent(fs) - digit_font.get_descent(fs)) / 2.0),
				txt, HORIZONTAL_ALIGNMENT_LEFT, -1, fs, Color(1, 1, 1, 0.9))
		else:
			var k := sz.x * 0.32
			face.draw_line(Vector2(k, k), sz - Vector2(k, k), Color.WHITE, maxf(2.0, 2.2 * u), true)
			face.draw_line(Vector2(sz.x - k, k), Vector2(k, sz.y - k), Color.WHITE, maxf(2.0, 2.2 * u), true))
	close_btn.add_child(face)
	close_btn.pressed.connect(func() -> void: _finish(false))
	root.add_child(close_btn)
	_update_close()


func _update_close() -> void:
	if close_btn == null:
		return
	close_btn.disabled = countdown_left > 0
	close_btn.set_meta("label", str(countdown_left) if countdown_left > 0 else "X")
	var x := close_btn.get_node_or_null("X")
	if x != null:
		x.queue_redraw()


## One line, never "…", with at most TWO font sizes touched (memory, Patron round 2): a step-by-step shrink rasterised
## the glyphs at every size on the way (the landscape card's one-time +30 MB of font cache). Measure once at `start`,
## jump straight to the size that fits (≥ floor); the title then tries its short name; a slogan that still does not fit
## is hidden.
static func fit_line(l: Label, start: int, floor_px: int) -> int:
	if not is_instance_valid(l) or l.size.x <= 1.0 or l.text == "":
		return start
	var f: Font = l.get_theme_font("font")
	var w := f.get_string_size(l.text, HORIZONTAL_ALIGNMENT_LEFT, -1, start).x
	if w > l.size.x and l.name == "title" and str(l.get_meta("short_title", "")) != "" \
			and w * float(floor_px) / float(start) > l.size.x:
		l.text = str(l.get_meta("short_title"))
		w = f.get_string_size(l.text, HORIZONTAL_ALIGNMENT_LEFT, -1, start).x
	var s := start
	if w > l.size.x:
		s = maxi(floor_px, int(floorf(float(start) * l.size.x / w)))
	if l.get_theme_font_size("font_size") != s:
		l.add_theme_font_size_override("font_size", s)
	if l.name == "subtitle":
		var fits := w * float(s) / float(start) <= l.size.x + 0.5
		if l.visible != fits:
			l.visible = fits
	return s


func _line(n: String, text: String, px: float, col: Color) -> Label:
	var l := Label.new()
	l.name = n
	l.text = text
	l.add_theme_font_size_override("font_size", int(px))
	l.add_theme_color_override("font_color", col)
	l.text_overrun_behavior = TextServer.OVERRUN_NO_TRIMMING
	l.autowrap_mode = TextServer.AUTOWRAP_OFF
	l.clip_text = true
	l.custom_minimum_size.x = 1
	l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l


func _rounded_icon(px: float, u: float) -> Control:
	var frame := Panel.new()
	frame.name = "Icon"
	frame.custom_minimum_size = Vector2(px, px)
	frame.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	frame.clip_children = CanvasItem.CLIP_CHILDREN_ONLY
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color.WHITE
	sb.set_corner_radius_all(int(px * 0.22))
	sb.anti_aliasing = true
	frame.add_theme_stylebox_override("panel", sb)
	var tr := TextureRect.new()
	tr.texture = icon
	tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tr.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	tr.set_anchors_preset(Control.PRESET_FULL_RECT)
	tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
	frame.add_child(tr)
	return frame


## No hero image: the icon large, centred on a soft halo of the game's colour.
func _build_icon_hero(hero: Control, sz: Vector2, u: float, base: Color) -> void:
	var halo := Panel.new()
	halo.name = "Halo"
	var hs := minf(sz.x, sz.y) * 0.78
	halo.size = Vector2(hs, hs)
	halo.position = (sz - halo.size) / 2.0
	var hsb := StyleBoxFlat.new()
	hsb.bg_color = Color(base.lightened(0.35), 0.22)
	hsb.set_corner_radius_all(int(hs / 2.0))
	halo.add_theme_stylebox_override("panel", hsb)
	halo.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hero.add_child(halo)
	var big := _rounded_icon(minf(sz.x, sz.y) * 0.5, u)
	big.name = "BigIcon"
	big.size = big.custom_minimum_size
	big.position = (sz - big.size) / 2.0
	big.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hero.add_child(big)


func _install_button(u: float, kt: Theme) -> Button:
	var label := "Install"
	if _banner_script != null:
		label = _banner_script.install_text(_lang())
	var btn := Button.new()
	btn.name = "Install"
	btn.text = label
	if kt != null and kt.has_stylebox("normal", "PrimaryButton"):
		btn.theme_type_variation = "PrimaryButton"
	else:
		for st in ["normal", "hover", "pressed", "focus"]:
			var sb := StyleBoxFlat.new()
			sb.bg_color = Color(1.0, 0.8, 0.25) if st != "pressed" else Color(0.9, 0.7, 0.2)
			sb.set_corner_radius_all(int(14.0 * u))
			btn.add_theme_stylebox_override(st, sb)
		btn.add_theme_color_override("font_color", Color(0.1, 0.1, 0.12))
		btn.add_theme_color_override("font_pressed_color", Color(0.1, 0.1, 0.12))
		btn.add_theme_color_override("font_hover_color", Color(0.1, 0.1, 0.12))
	btn.set_meta("coddy_no_fit", true)
	btn.add_theme_font_size_override("font_size", int(roundf(20.0 * u)))
	btn.custom_minimum_size = Vector2(0, roundf(56.0 * u))
	btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	btn.focus_mode = Control.FOCUS_NONE
	btn.pressed.connect(func() -> void: _finish(true))
	return btn


func _on_hero_input(e: InputEvent) -> void:
	if e.device == InputEvent.DEVICE_ID_EMULATION:
		return   # a phone sends the touch AND an emulated click: act once (W66 10-27)
	if (e is InputEventMouseButton and e.pressed and e.button_index == MOUSE_BUTTON_LEFT) \
			or (e is InputEventScreenTouch and e.pressed):
		_finish(true)


## Medium+ only: cross-fade the frames with a slow pan. Engine tweens, no script per frame.
func _start_slides() -> void:
	if hero_rects.size() < 2:
		return
	_tween = create_tween().set_loops()
	_tween.set_process_mode(Tween.TWEEN_PROCESS_IDLE)
	for i in hero_rects.size():
		var cur: TextureRect = hero_rects[i]
		var nxt: TextureRect = hero_rects[(i + 1) % hero_rects.size()]
		_tween.tween_property(cur, "scale", Vector2(PAN, PAN), SLIDE_S).from(Vector2.ONE)
		_tween.tween_callback(func() -> void: nxt.scale = Vector2.ONE)
		_tween.tween_property(nxt, "modulate:a", 1.0, FADE_S).from(0.0)
		_tween.parallel().tween_property(cur, "modulate:a", 0.0, FADE_S)


func _finish(opened: bool) -> void:
	if _done:
		return
	_done = true
	if _tween != null:
		_tween.kill()
	finished.emit(opened)
	queue_free()


# ---------------------------------------------------------------- helpers for ad layers that are not the kit CoddyAds
## (bridged games' own forks call these; the kit CoddyAds has the same rules in hero_urls / hero_list)

## Frames for a creative: heroI18n[lang][orientation] → en → the other orientation → heroImageUrls → interstitialImageUrl.
static func hero_urls(c: Dictionary, lang := "en", portrait := true) -> Array:
	var orient := "portrait" if portrait else "landscape"
	var other := "landscape" if portrait else "portrait"
	var m = c.get("heroI18n", null)
	if m is Dictionary:
		for key in [[lang, orient], ["en", orient], [lang, other], ["en", other]]:
			var e = m.get(key[0], null)
			if e is Dictionary and e.get(key[1], null) is Array:
				var got := https3(e[key[1]])
				if not got.is_empty():
					return got
	var out := https3(c.get("heroImageUrls", []) if c.get("heroImageUrls", null) is Array else [])
	if out.is_empty() and str(c.get("interstitialImageUrl", "")).begins_with("http"):
		out.append(str(c.get("interstitialImageUrl")))
	return out


static func https3(a: Array) -> Array:
	var out: Array = []
	for u in a:
		if str(u).begins_with("http") and not out.has(str(u)) and out.size() < 3:
			out.append(str(u))
	return out


## The frames for the screen `ads` is on: its UI language (ui_lang / TranslationServer) + the viewport orientation.
static func screen_heroes(ads: Node, c: Dictionary) -> Array:
	var lang := str(ads.ui_lang()) if ads != null and ads.has_method("ui_lang") else TranslationServer.get_locale().substr(0, 2).to_lower()
	var vp := ads.get_viewport().get_visible_rect().size if ads != null and ads.is_inside_tree() else Vector2(720, 1280)
	return hero_urls(c, lang, vp.y >= vp.x)


## Low device class (no tween, one frame): the shared coddy_perf class; unknown handheld = Low, desktop = not Low.
static func low_device() -> bool:
	if not (OS.has_feature("android") or OS.has_feature("ios")):
		return false
	const PD := "res://addons/coddy_perf/perf_device.gd"
	if not ResourceLoader.exists(PD):
		return true
	var pd = load(PD)
	return int(pd.default_tier(pd.probe())) == 0
