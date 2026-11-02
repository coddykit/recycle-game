class_name UIKit
## Theme + widget factory for the mobile UI. Everything is built in code so
## the HUD stays consistent and easy to restyle.

const BG := Color("141829")
const SHEET := Color("1a1f35")
const CARD := Color("252b48")
const CARD_HI := Color("313965")
const TEXT := Color("f4f1ea")
const DIM := Color("a3a9c9")
const GOLD := Color("ffc93c")
const GREEN := Color("3ddc84")
const RED := Color("ff5a6a")
const BLUE := Color("4da3ff")
const PURPLE := Color("9d6bff")
const ORANGE := Color("ff8a3c")
const CYAN := Color("3cf2ff")

## Smallest UI text, in canvas units: the 720-wide canvas maps 1 unit to ~0.5 pt on a phone, so 22 units = 11 pt
## (owner rule 2026-10-18: text >= 11 pt). label(), button() and pill() clamp to it.
const MIN_FONT := 22

const Kit = preload("res://addons/coddy_ui/ui_kit.gd")
const Fonts = preload("res://addons/coddy_ui/ui_fonts.gd")
const PALETTE := "smuggler"
## The game's old variation names -> the fleet kit's looks (W64): one primary look, tonal, secondary, danger.
const BUTTON_ALIASES := {"BtnPrimary": "PrimaryButton", "BtnGold": "PrimaryButton", "BtnBlue": "TonalButton", "BtnPurple": "TonalButton",
		"BtnDanger": "DangerButton", "BtnDark": "SecondaryButton", "BtnFlat": "IconButtonFlat"}
const PANEL_ALIASES := {"CardHi": "CardRaised", "Pill": "Glass", "Modal": "Sheet"}

## The kit is dark only (W64): the old System / Light / Dark appearance setting no longer changes the look.
static var light_mode := false
static var _theme: Theme
static var _theme_locale := ""
static var font_semi: Font
static var font_bold: Font
static var _icons := {}


static func configure(_appearance: String = "") -> void:
	light_mode = false
	_theme = null


static func surface(color: Color) -> Color:
	return color


static func ink(color: Color) -> Color:
	return color


static func theme() -> Theme:
	var locale := TranslationServer.get_locale()
	if _theme and _theme_locale == locale:
		return _theme
	Fonts.configure({"latin": "res://assets/fonts/Baloo2-SemiBold.ttf", "ja": "res://assets/fonts/NotoSansCJKsc-Regular.otf",
			"ko": "res://assets/fonts/NotoSansCJKsc-Regular.otf", "zh": "res://assets/fonts/NotoSansCJKsc-Regular.otf"})
	Kit.tap_sound = func() -> void: AudioManager.play("tap_soft")
	Kit.design = 2   # the 2026-10-26 tab + modal redesign (kit design generation 2), set before the first Kit.use
	var t: Theme = Kit.use(PALETTE).duplicate()
	for k in BUTTON_ALIASES:
		_alias(t, k, BUTTON_ALIASES[k], "Button")
	for k in PANEL_ALIASES:
		_alias(t, k, PANEL_ALIASES[k], "PanelContainer")
	font_semi = t.default_font
	font_bold = t.get_font("font", "Title")
	_theme = t
	_theme_locale = locale
	return t


## A type variation `name` that looks exactly like the kit's `from`.
static func _alias(t: Theme, type_name: String, from: String, base: String) -> void:
	t.set_type_variation(type_name, base)
	for n in t.get_stylebox_list(from):
		t.set_stylebox(n, type_name, t.get_stylebox(n, from))
	for n in t.get_color_list(from):
		t.set_color(n, type_name, t.get_color(n, from))
	for n in t.get_font_list(from):
		t.set_font(n, type_name, t.get_font(n, from))
	for n in t.get_font_size_list(from):
		t.set_font_size(n, type_name, t.get_font_size(n, from))
	for n in t.get_constant_list(from):
		t.set_constant(n, type_name, t.get_constant(n, from))
	for n in t.get_icon_list(from):
		t.set_icon(n, type_name, t.get_icon(n, from))


## The chunky Baloo 2 face of the 3D world labels (region names, HQ sign); the UI itself uses the kit's font stack.
static var _world_font: Font

static func bold_font() -> Font:
	if _world_font == null:
		_world_font = load("res://assets/fonts/Baloo2-ExtraBold.ttf")
		_world_font.fallbacks = [load("res://assets/fonts/NotoSansCJKsc-Regular.otf")]
	return _world_font


static func _box(color: Color, radius: int, border := 0, shadow := Color(0, 0, 0, 0), shadow_size := 0) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = surface(color)
	s.set_corner_radius_all(radius)
	s.corner_detail = 6
	s.content_margin_left = 18
	s.content_margin_right = 18
	s.content_margin_top = 14
	s.content_margin_bottom = 14
	if border > 0:
		s.set_border_width_all(border)
		s.border_color = color.lightened(0.3)
	if shadow_size > 0:
		s.shadow_color = shadow
		s.shadow_size = shadow_size
		s.shadow_offset = Vector2(0, 4)
	s.anti_aliasing = true
	return s


## The game's own icon names -> the kit's Lucide icons (unknown names fall back).
const ICON_MAP := {"star": "star", "risk": "triangle-alert", "gear": "settings", "hand": "hand", "shield": "shield-check", "contract": "file-text",
		"cash": "coins", "trophy": "trophy", "check": "circle-check", "mentor": "message-square", "lock": "lock", "truck": "truck",
		"garage": "house", "depot": "warehouse", "heat": "flame", "influence": "gem", "map": "map", "close": "x", "refresh": "rotate-cw",
		"upgrade": "circle-arrow-up", "business": "briefcase", "driver": "users", "target": "target", "bolt": "zap", "clock": "clock",
		"person": "user", "sound": "volume-2", "box": "package", "pin": "map", "route": "route", "robot": "zap", "wrench": "wrench",
		"arrow_right": "arrow-right", "arrow_left": "chevron-left"}


## A kit label that wraps inside a card / list (min width 1: the container gives the width, a long line never widens it).
static func wlabel(text: String, style := "Body") -> Label:
	var l := Kit.label(text, style, true)
	l.custom_minimum_size.x = 1
	return l


static func lucide(game_icon: String, fallback := "") -> String:
	return str(ICON_MAP.get(game_icon, fallback))


## Modal / toast tile colour for an accent colour of the old API.
static func kind_of(accent: Color) -> String:
	if accent == RED:
		return "danger"
	if accent == GREEN:
		return "success"
	if accent == ORANGE:
		return "warning"
	if accent == BLUE or accent == CYAN:
		return "info"
	if accent == PURPLE:
		return "accent"
	return "primary"


## Old button variation -> UiModal action kind.
static func action_kind(variation: String) -> String:
	match variation:
		"BtnDanger": return "danger"
		"BtnDark": return "secondary"
		"BtnBlue", "BtnPurple": return "tonal"
		_: return "primary"


# --- widgets -----------------------------------------------------------------------
static func tex(name: String) -> Texture2D:
	if _icons.has(name):
		return _icons[name]
	var path := "res://assets/icons/%s.svg" % name
	var t: Texture2D = load(path) if ResourceLoader.exists(path) else null
	_icons[name] = t
	return t


static func label(text: String, size := 26, color := TEXT, bold := false, align := HORIZONTAL_ALIGNMENT_LEFT) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", maxi(size, MIN_FONT))
	l.add_theme_color_override("font_color", ink(color))
	if bold:
		l.add_theme_font_override("font", font_bold)
	l.horizontal_alignment = align
	l.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l


static func icon(name: String, size := 40, modulate := Color.WHITE) -> TextureRect:
	var r := TextureRect.new()
	r.texture = tex(name)
	r.custom_minimum_size = Vector2(size, size)
	r.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	r.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	r.modulate = modulate
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return r


static func button(text: String, variation := "", icon_name := "", min_size := Vector2(0, 88), font_size := 26) -> Button:
	var b := Button.new()
	b.text = text
	if variation != "":
		b.theme_type_variation = variation
	if icon_name != "":
		b.icon = tex(icon_name)
		b.expand_icon = false
	b.custom_minimum_size = Vector2(maxf(108, min_size.x), maxf(88, min_size.y))
	b.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	b.add_theme_font_size_override("font_size", maxi(font_size, MIN_FONT))
	b.focus_mode = Control.FOCUS_ALL
	b.mouse_filter = Control.MOUSE_FILTER_PASS
	b.button_down.connect(func():
		AudioManager.play("click")
		UIManager.haptic(10))
	b.pressed.connect(func(): bounce(b))
	return b


## A button whose caption is a picture (E49, W61 memory: the first Han / Hangul text makes the text server load its CJK
## line-break data, ~55 MB each — tools/make_lang_labels.py draws the native language names once). The picture is tinted
## like the caption text and sized to `font_px` text height.
static func picture_caption(b: Button, path: String, font_px: int) -> void:
	var tex: Texture2D = load(path)
	b.text = ""
	b.icon = tex
	b.icon_alignment = HORIZONTAL_ALIGNMENT_CENTER
	b.expand_icon = true
	b.add_theme_constant_override("icon_max_width", int(round(font_px * 1.3 * tex.get_width() / float(tex.get_height()))))
	b.tooltip_text = ""
	b.tree_entered.connect(func() -> void: # the theme (font colours of the variation) is known once the button is in the tree
		for k in ["normal", "hover", "pressed", "hover_pressed", "focus", "disabled"]:
			b.add_theme_color_override("icon_%s_color" % k, b.get_theme_color("font_color" if k == "normal" else "font_%s_color" % k)))


static func reduced_motion() -> bool:
	return bool(GameManager.settings.get("reduced_motion", false))


static func bounce(c: Control, amount := 0.92) -> void:
	if reduced_motion(): return
	if not is_instance_valid(c):
		return
	c.pivot_offset = c.size * 0.5
	var t := c.create_tween()
	t.tween_property(c, "scale", Vector2.ONE * amount, 0.06)
	t.tween_property(c, "scale", Vector2.ONE, 0.25).set_trans(Tween.TRANS_ELASTIC).set_ease(Tween.EASE_OUT)


static func pop_in(c: Control, delay := 0.0) -> void:
	if reduced_motion():
		c.scale = Vector2.ONE
		c.modulate.a = 1
		return
	c.pivot_offset = c.size * 0.5
	c.scale = Vector2(0.85, 0.85)
	c.modulate.a = 0.0
	var t := c.create_tween()
	t.tween_interval(delay)
	t.tween_property(c, "modulate:a", 1.0, 0.15)
	t.parallel().tween_property(c, "scale", Vector2.ONE, 0.35).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)


static func hbox(sep := 12, align := BoxContainer.ALIGNMENT_BEGIN) -> HBoxContainer:
	var h := HBoxContainer.new()
	h.add_theme_constant_override("separation", sep)
	h.alignment = align
	h.mouse_filter = Control.MOUSE_FILTER_PASS
	return h


## A row that wraps onto more lines instead of growing wider than the screen (long translations).
static func flow(h_sep := 14, v_sep := 4) -> HFlowContainer:
	var f := HFlowContainer.new()
	f.add_theme_constant_override("h_separation", h_sep)
	f.add_theme_constant_override("v_separation", v_sep)
	f.mouse_filter = Control.MOUSE_FILTER_PASS
	return f


static func vbox(sep := 12) -> VBoxContainer:
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", sep)
	v.mouse_filter = Control.MOUSE_FILTER_PASS
	return v


static func card(variation := "Card", color := Color(0, 0, 0, 0), radius := 22) -> PanelContainer:
	var p := PanelContainer.new()
	p.theme_type_variation = variation
	p.mouse_filter = Control.MOUSE_FILTER_PASS
	if color.a > 0:
		var s := _box(color, radius, 0, Color(0, 0, 0, 0.28), 6)
		p.add_theme_stylebox_override("panel", s)
	return p


## Card stylebox with a coloured accent stripe on the left.
static func card_style(bg := CARD, accent := Color(0, 0, 0, 0), stripe := 12) -> StyleBoxFlat:
	var s := _box(bg, 22, 0, Color(0, 0, 0, 0.28), 6)
	if accent.a > 0:
		s.border_width_left = stripe
		s.border_color = accent
		s.content_margin_left = 18 + stripe
	return s


static func plural(n: int, word: String) -> String:
	return "%d %s%s" % [n, TranslationServer.translate(word), pl(n)]


## English plural "s" (Turkish nouns stay singular after numbers).
## Locale-aware uppercase (Turkish dotted/dotless i: i -> İ, ı -> I).
static func upper(text: String) -> String:
	if TranslationServer.get_locale().begins_with("tr"):
		text = text.replace("i", "İ").replace("ı", "I")
	return text.to_upper()


static func pl(n: int) -> String:
	if n == 1 or not TranslationServer.get_locale().begins_with("en"):
		return ""
	return "s"


static func pill(text: String, color: Color, text_color := Color("10121c"), size := 20) -> PanelContainer:
	var p := PanelContainer.new()
	var s := _box(color, 14)
	s.content_margin_left = 10
	s.content_margin_right = 10
	s.content_margin_top = 2
	s.content_margin_bottom = 2
	p.add_theme_stylebox_override("panel", s)
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var l := label(text, size, text_color, true, HORIZONTAL_ALIGNMENT_CENTER)
	p.add_child(l)
	return p


static func stat_chip(icon_name: String, text: String, color := TEXT, size := 22) -> HBoxContainer:
	var h := hbox(4)
	h.mouse_filter = Control.MOUSE_FILTER_IGNORE
	h.add_child(icon(icon_name, size + 6))
	h.add_child(label(text, size, color))
	return h


static func bar(value: float, color: Color, height := 14, width := 0.0) -> ProgressBar:
	var b := ProgressBar.new()
	b.min_value = 0
	b.max_value = 1
	b.step = 0.001
	b.value = value
	b.show_percentage = false
	b.custom_minimum_size = Vector2(width, height)
	b.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var bg := _box(Color(0, 0, 0, 0.35), height / 2)
	bg.content_margin_top = 0
	bg.content_margin_bottom = 0
	var fill := _box(color, height / 2)
	fill.content_margin_top = 0
	fill.content_margin_bottom = 0
	b.add_theme_stylebox_override("background", bg)
	b.add_theme_stylebox_override("fill", fill)
	return b


static func pips(level: int, max_level: int, color := GOLD, size := 14) -> HBoxContainer:
	var h := hbox(4)
	h.mouse_filter = Control.MOUSE_FILTER_IGNORE
	for i in max_level:
		var c := Panel.new()
		c.custom_minimum_size = Vector2(size * 1.6, size)
		var s := _box(color if i < level else Color(1, 1, 1, 0.12), size / 2)
		c.add_theme_stylebox_override("panel", s)
		c.mouse_filter = Control.MOUSE_FILTER_IGNORE
		h.add_child(c)
	return h


static func spacer(h := true) -> Control:
	var c := Control.new()
	if h:
		c.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	else:
		c.size_flags_vertical = Control.SIZE_EXPAND_FILL
	c.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return c


static func section(text: String) -> Label:
	var l := label(upper(TranslationServer.translate(text)), 20, DIM, true)
	l.add_theme_constant_override("outline_size", 0)
	return l


static func rarity_color(rarity: String) -> Color:
	return Data.color(Data.rarity(rarity).get("color", "a7aeb8"))


static func wrap(l: Label) -> Label:
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.custom_minimum_size.x = 10
	l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return l
