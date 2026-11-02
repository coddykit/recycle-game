extends RefCounted
## coddy_ui Theme builder: design tokens (ui_tokens.gd, generated from design/tokens.json) + a role palette → one Godot
## Theme. Every standard control a game uses gets the fleet look, so no raw Godot grey panel, default scroll bar or
## default font is ever visible (W64). Named looks are theme type variations — set `theme_type_variation` on a node:
##   Label:          Display, Headline, Title, BodyLarge, Body, Caption, Overline, Muted, CaptionMuted, Value, Accent, Brand
##   Button:         PrimaryButton, SecondaryButton, TonalButton, GhostButton, DangerButton, IconButton, IconButtonFlat,
##                   TabButton, SegmentButton, RowButton, TextButton
##                   ActionPill, ActionPillTonal, OutlineButton, CloseButton, CloseButtonFlat (2026-10-26)
##   PanelContainer: Card, CardRaised, Sheet, SheetBottom, Glass, Group, Track, InfoCard, WarningCard, Toast, Bubble,
##                   Badge, BadgeSuccess, Chip, Scrim, Grabber, EmptyCard
## Weak-device safe: only StyleBoxFlat / StyleBoxEmpty / StyleBoxLine, no shaders, built once per palette + language.

const Tokens = preload("ui_tokens.gd")
const Fonts = preload("ui_fonts.gd")
const Icons = preload("ui_icons.gd")
## #113 r2 list card padding (canvas px): left, top, right, bottom
const LIST_CARD_M := Vector4(20, 18, 20, 18)
const LIST_CARD_DENSE_M := Vector4(20, 8, 16, 8)
const LIST_CARD_TIGHT_M := Vector4(16, 0, 10, 0)   # #132 landscape: the 48 dp price pill IS the row height
## #113 r2 price pill: drawn this much inside its 84 px (48 dp) touch rect at top and bottom → a 56 px badge-sized pill
const PILL_INSET := 14


static func palette(name_or_dict, overrides: Dictionary = {}) -> Dictionary:
	var src: Dictionary
	if name_or_dict is Dictionary:
		src = (name_or_dict as Dictionary).duplicate()
	else:
		src = (Tokens.PALETTES.get(str(name_or_dict), Tokens.PALETTES["fleet"]) as Dictionary).duplicate()
	for k in overrides:
		src[k] = overrides[k]
	var out := {}
	for k in src:
		out[k] = src[k] if src[k] is Color else Color(str(src[k]))
	return out


static func box(bg: Color, radius: int, margin: Vector4 = Vector4(16, 12, 16, 12), border: int = 0, border_color: Color = Color.TRANSPARENT, elevation: String = "e0") -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = bg
	s.set_corner_radius_all(radius)
	s.corner_detail = 8
	s.anti_aliasing = true
	s.content_margin_left = margin.x
	s.content_margin_top = margin.y
	s.content_margin_right = margin.z
	s.content_margin_bottom = margin.w
	if border > 0:
		s.set_border_width_all(border)
		s.border_color = border_color
	var e: Dictionary = Tokens.ELEVATION.get(elevation, Tokens.ELEVATION["e0"])
	if int(e.size) > 0:
		s.shadow_size = int(e.size)
		s.shadow_offset = Vector2(0, float(e.y))
		s.shadow_color = Color(0, 0, 0, float(e.alpha))
	return s


static func _top_only(s: StyleBoxFlat) -> StyleBoxFlat:
	s.corner_radius_bottom_left = 0
	s.corner_radius_bottom_right = 0
	s.border_width_bottom = 0
	return s


static func _empty(margin: Vector4 = Vector4.ZERO) -> StyleBoxEmpty:
	var s := StyleBoxEmpty.new()
	s.content_margin_left = margin.x
	s.content_margin_top = margin.y
	s.content_margin_right = margin.z
	s.content_margin_bottom = margin.w
	return s


static func _alpha(c: Color, a: float) -> Color:
	return Color(c, a)


## Button height = font line (~1.38 x size) + top/bottom margin; with label 26 px the default is 84 px = 48 dp.
## #112: Airport's Montserrat line is shorter than Noto's and its buttons came out 81 px (24 + 24 + one line < 84).
## Patron 10-28: TOUCH.min is a FLOOR, not a sum that happens to work for one font. Full-size buttons (the default margin,
## Ghost / Text) get their vertical margin from the floor: max(margin, (TOUCH.min - line) / 2), the line measured on the
## font and size THIS variation draws with (its own font, its own font_size) when the theme is built — no shared value
## from another variation / an earlier build, and with no measurable font the margin alone holds the whole floor
## ((TOUCH.min - 0) / 2 each side). Kit factories (button, action_pill, tab_button, icon / close buttons) also set
## custom_minimum_size.y = TOUCH.min, which no font can lower.
static func _touch_margin(m: Vector4, line: float) -> Vector4:
	var v := maxf(m.y, ceilf((Tokens.TOUCH.min - maxf(line, 0.0)) / 2.0))
	return Vector4(m.x, v, m.z, maxf(m.w, v))


## The text line a variation draws with (0 = no font yet).
static func _line_of(t: Theme, type: String, size: int) -> float:
	var f: Font = t.get_font("font", type) if t.has_font("font", type) else t.default_font
	return f.get_height(size) if f != null else 0.0


static func _button(t: Theme, type: String, p: Dictionary, normal: Color, text: Color, opts: Dictionary = {}) -> void:
	var r: int = opts.get("radius", Tokens.RADIUS.s)
	var m: Vector4 = opts.get("margin", Vector4(28, 24, 28, 24))
	if not opts.has("margin") or type in ["GhostButton", "TextButton"]:
		m = _touch_margin(m, _line_of(t, type, int(opts.get("font_size", Tokens.TYPE.label.size))))
	var lip: Color = opts.get("lip", Color.TRANSPARENT)
	var pressed: Color = opts.get("pressed", normal.darkened(0.12))
	var hover: Color = opts.get("hover", normal.lightened(0.06))
	var elevation: String = opts.get("elevation", "e0")
	var states := {"normal": normal, "hover": hover, "pressed": pressed, "hover_pressed": pressed, "focus": normal,
		"disabled": opts.get("disabled", p.surface_variant)}
	for st in states:
		var c: Color = states[st]
		var s: StyleBox
		if c.a <= 0.001 and lip.a <= 0.001 and st != "pressed":
			s = _empty(m)
		else:
			s = box(c, r, m, 0, Color.TRANSPARENT, elevation if st in ["normal", "hover"] else "e0")
			if lip.a > 0.001 and st != "disabled":
				var f := s as StyleBoxFlat
				f.border_width_bottom = Tokens.TOUCH.lip
				f.border_color = lip if st in ["normal", "hover", "focus"] else lip.darkened(0.15)
		var ind: Color = opts.get("indicator", Color.TRANSPARENT)
		if ind.a > 0.001 and st in ["pressed", "hover_pressed"]:
			# #113 selected tab: an accent bar painted INTO the tab's own picture (one nine-patch = the same single draw);
			# the bar sits inside the box, clear of the rounded corners (round 1 bent it over the corners)
			s = tab_indicator_box(pressed, ind, r, m)
		if st == "focus":
			s = _empty()
		t.set_stylebox(st, type, s)
	var muted_text: Color = opts.get("disabled_text", _alpha(p.on_surface_muted, 0.6))
	for k in ["font_color", "font_focus_color"]:
		t.set_color(k, type, text)
	t.set_color("font_hover_color", type, opts.get("hover_text", text))
	t.set_color("font_pressed_color", type, opts.get("pressed_text", text))
	t.set_color("font_hover_pressed_color", type, opts.get("pressed_text", text))
	t.set_color("font_disabled_color", type, muted_text)
	t.set_color("icon_normal_color", type, opts.get("icon", text))
	t.set_color("icon_focus_color", type, opts.get("icon", text))
	t.set_color("icon_hover_color", type, opts.get("hover_text", opts.get("icon", text)))
	t.set_color("icon_pressed_color", type, opts.get("pressed_text", text))
	t.set_color("icon_hover_pressed_color", type, opts.get("pressed_text", text))
	t.set_color("icon_disabled_color", type, muted_text)
	t.set_font_size("font_size", type, opts.get("font_size", Tokens.TYPE.label.size))
	t.set_constant("h_separation", type, opts.get("gap", 12))
	t.set_constant("icon_max_width", type, opts.get("icon_max", 34))
	t.set_constant("outline_size", type, 0)


## #113 r2: the selected-tab picture — a rounded box in `bg` with a pill-shaped accent bar `bar_h` px tall, inset from the
## top edge and kept clear of the corner radius. A StyleBoxTexture nine-patch: the bar's round caps live in the fixed
## corner slices, its straight part in the stretched top-middle slice, so it never bends over a corner at any width.
const TAB_BAR_H := 5
const TAB_BAR_INSET := 4


static func tab_indicator_box(bg: Color, bar: Color, radius: int, m: Vector4) -> StyleBoxTexture:
	var side := radius + 8                       # corner slice: the radius + room for the bar's round cap
	var w := side * 2 + 8
	var h := side * 2 + 8
	var x0 := float(radius) * 0.9
	var svg := "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"%d\" height=\"%d\" viewBox=\"0 0 %d %d\"><rect width=\"%d\" height=\"%d\" rx=\"%d\" fill=\"#%s\" fill-opacity=\"%.3f\"/><rect x=\"%.1f\" y=\"%d\" width=\"%.1f\" height=\"%d\" rx=\"%.1f\" fill=\"#%s\" fill-opacity=\"%.3f\"/></svg>" % [
		w, h, w, h, w, h, radius, bg.to_html(false), bg.a, x0, TAB_BAR_INSET, w - 2.0 * x0, TAB_BAR_H, TAB_BAR_H / 2.0, bar.to_html(false), bar.a]
	var tex: Texture2D = Icons._tex("tabind|%s|%s|%d" % [bg.to_html(), bar.to_html(), radius], svg, 1.0)
	var sb := StyleBoxTexture.new()
	sb.texture = tex
	sb.texture_margin_left = side
	sb.texture_margin_right = side
	sb.texture_margin_top = side
	sb.texture_margin_bottom = side
	sb.content_margin_left = m.x
	sb.content_margin_top = m.y
	sb.content_margin_right = m.z
	sb.content_margin_bottom = m.w
	sb.set_meta("indicator", bar)
	return sb


## v2 = the 2026-10-26 tab + modal look (UiKit.design = 2): sheets one step darker than their cards, a bottom sheet
## rounded at the top only. v1 (default) keeps the old surfaces byte-for-byte for games that have not adopted it.
static func build(p: Dictionary, locale: String = "", v2: bool = false) -> Theme:
	var t := Theme.new()
	var regular := Fonts.stack(false, locale)
	var bold := Fonts.stack(true, locale)
	var T: Dictionary = Tokens.TYPE
	var R: Dictionary = Tokens.RADIUS
	t.default_font = regular
	t.default_font_size = T.body.size

	# ---- text
	t.set_font("font", "Label", regular)
	t.set_font_size("font_size", "Label", T.body.size)
	t.set_color("font_color", "Label", p.on_surface)
	t.set_constant("line_spacing", "Label", 2)
	var labels := {
		"Display": [T.display.size, bold, p.on_surface], "Headline": [T.headline.size, bold, p.on_surface],
		"Title": [T.title.size, bold, p.on_surface], "BodyLarge": [T.body_l.size, regular, p.on_surface],
		"Body": [T.body.size, regular, p.on_surface], "Caption": [T.caption.size, regular, p.on_surface],
		"Overline": [T.overline.size, bold, p.on_surface_muted], "Muted": [T.body.size, regular, p.on_surface_muted],
		"CaptionMuted": [T.caption.size, regular, p.on_surface_muted], "Value": [T.title.size, bold, p.on_surface],
		"Accent": [T.body.size, bold, p.accent], "Brand": [T.title.size, bold, p.primary],
		"LabelStrong": [T.label.size, bold, p.on_surface], "BodyMuted": [T.body_l.size, regular, p.on_surface_muted],
	}
	# #113 r2 list-card status badges: a Label with its own pill stylebox (one canvas item = one draw, no PanelContainer)
	var badges := {"BadgeLocked": [p.surface_high, p.on_surface_muted], "BadgeOpen": [p.success_container, p.success],
		"BadgePrice": [p.primary_container, p.primary], "BadgeLevel": [p.surface_high, p.on_surface],
		"BadgeWarning": [p.warning_container, p.warning]}
	for k in badges:
		labels[k] = [T.caption.size + 2, bold, badges[k][1]]
	for k in labels:
		t.set_type_variation(k, "Label")
		t.set_font_size("font_size", k, labels[k][0])
		t.set_font("font", k, labels[k][1])
		t.set_color("font_color", k, labels[k][2])
	for k in badges:
		t.set_stylebox("normal", k, box(badges[k][0], R.pill, Vector4(16, 5, 16, 6)))

	# ---- buttons (base Button = secondary)
	for type in ["Button", "PrimaryButton", "SecondaryButton", "TonalButton", "PriceButton", "OwnedButton", "GhostButton", "DangerButton", "IconButton",
			"IconButtonFlat", "TabButton", "SegmentButton", "RowButton", "TextButton", "ActionPill", "ActionPillTonal", "OutlineButton",
			"CloseButton", "CloseButtonFlat", "ActionText", "ActionTextTonal"]:
		if type != "Button":
			t.set_type_variation(type, "Button")
		t.set_font("font", type, regular if type.begins_with("ActionText") else bold)
	var sunk := _alpha(p.surface_sunken, 0.8)
	_button(t, "Button", p, p.surface_high, p.on_surface, {"lip": sunk})
	_button(t, "SecondaryButton", p, p.surface_high, p.on_surface, {"lip": sunk})
	_button(t, "PrimaryButton", p, p.primary, p.on_primary, {"lip": p.primary_lip, "pressed": p.primary_pressed, "hover": p.primary.lightened(0.08), "elevation": "e1"})
	_button(t, "TonalButton", p, p.primary_container, p.primary, {"pressed": p.primary_container.darkened(0.1)})
	_button(t, "GhostButton", p, Color.TRANSPARENT, p.on_surface_muted, {"pressed": _alpha(p.on_surface, 0.08), "hover": _alpha(p.on_surface, 0.05), "hover_text": p.on_surface, "pressed_text": p.on_surface, "margin": Vector4(20, 24, 20, 24), "disabled": Color.TRANSPARENT})
	_button(t, "TextButton", p, Color.TRANSPARENT, p.primary, {"pressed": _alpha(p.primary, 0.12), "hover": _alpha(p.primary, 0.06), "margin": Vector4(16, 24, 16, 24), "disabled": Color.TRANSPARENT})
	_button(t, "DangerButton", p, p.danger, Color("1a0806"), {"lip": p.danger.darkened(0.45), "pressed": p.danger.darkened(0.12)})
	var icon_m := Vector4(25, 25, 25, 25)
	_button(t, "IconButton", p, _alpha(p.surface, 0.94), p.on_surface, {"radius": R.pill, "margin": icon_m, "elevation": "e2", "pressed": p.surface_high, "hover": p.surface_variant})
	var ib := t.get_stylebox("normal", "IconButton") as StyleBoxFlat
	ib.set_border_width_all(1)
	ib.border_color = _alpha(p.outline, 0.75)
	_button(t, "IconButtonFlat", p, Color.TRANSPARENT, p.on_surface_muted, {"radius": R.pill, "margin": icon_m, "pressed": _alpha(p.on_surface, 0.1), "hover": _alpha(p.on_surface, 0.06), "hover_text": p.on_surface, "pressed_text": p.on_surface, "disabled": Color.TRANSPARENT})
	_button(t, "TabButton", p, Color.TRANSPARENT, p.on_surface_muted, {"radius": 14, "margin": Vector4(8, 12, 8, 10), "pressed": p.primary_container, "pressed_text": p.primary, "indicator": p.primary, "hover": _alpha(p.on_surface, 0.05), "hover_text": p.on_surface, "font_size": T.caption.size, "gap": 4, "icon_max": 30, "disabled": Color.TRANSPARENT})
	_button(t, "SegmentButton", p, Color.TRANSPARENT, p.on_surface_muted, {"radius": 10, "margin": Vector4(14, 18, 14, 18), "pressed": p.primary, "pressed_text": p.on_primary, "hover": _alpha(p.on_surface, 0.05), "hover_text": p.on_surface, "font_size": T.body.size, "gap": 8, "icon_max": 26, "disabled": Color.TRANSPARENT})
	# #113 r2 shop list cards: the price IS the card's one badge — a pill button (48 dp tall), owned = the open badge
	_button(t, "PriceButton", p, p.primary_container, p.primary, {"radius": R.pill, "margin": Vector4(20, 16, 20, 16), "pressed": p.primary_container.darkened(0.12), "disabled": _alpha(p.primary_container, 0.55), "disabled_text": _alpha(p.primary, 0.75), "font_size": T.label.size})
	_button(t, "OwnedButton", p, p.success_container, p.success, {"radius": R.pill, "margin": Vector4(20, 16, 20, 16), "disabled": p.success_container, "disabled_text": p.success, "font_size": T.label.size})
	# 2026-10-26 tab + modal redesign: the card's one call to action on the RIGHT of its head row (UiKit.action_pill) — a
	# 60 px pill inside the 84 px (48 dp) touch rect; primary = the thing to buy now, tonal = a secondary action
	_button(t, "ActionPill", p, p.primary, p.on_primary, {"radius": R.pill, "margin": Vector4(22, 16, 22, 16), "pressed": p.primary_pressed, "hover": p.primary.lightened(0.08), "disabled": _alpha(p.surface_high, 0.7), "disabled_text": _alpha(p.on_surface_muted, 0.9), "font_size": T.label.size - 2, "gap": 8, "icon_max": 26})
	_button(t, "ActionPillTonal", p, p.surface_high, p.on_surface, {"radius": R.pill, "margin": Vector4(22, 16, 22, 16), "pressed": p.surface_high.darkened(0.12), "hover": p.surface_high.lightened(0.06), "disabled": _alpha(p.surface_high, 0.45), "disabled_text": _alpha(p.on_surface_muted, 0.8), "font_size": T.label.size - 2, "gap": 8, "icon_max": 26})
	# Low tier (#114): the same call to action as coloured text only — no pill box and the regular face, so it batches
	# with the card's grey line (a card costs no more draws than before the redesign at a denser row height)
	_button(t, "ActionText", p, Color.TRANSPARENT, p.primary, {"radius": R.s, "margin": Vector4(16, 16, 12, 16), "pressed": _alpha(p.primary, 0.12), "hover": _alpha(p.primary, 0.06), "disabled": Color.TRANSPARENT, "disabled_text": _alpha(p.on_surface_muted, 0.75), "font_size": T.label.size - 2})
	_button(t, "ActionTextTonal", p, Color.TRANSPARENT, p.on_surface, {"radius": R.s, "margin": Vector4(16, 16, 12, 16), "pressed": _alpha(p.on_surface, 0.1), "hover": _alpha(p.on_surface, 0.05), "disabled": Color.TRANSPARENT, "disabled_text": _alpha(p.on_surface_muted, 0.75), "font_size": T.label.size - 2})
	# a secondary action under a card: hairline outline, no fill — the primary keeps all the colour (one stylebox = one draw, as before)
	_button(t, "OutlineButton", p, Color.TRANSPARENT, p.on_surface, {"radius": R.s, "pressed": _alpha(p.on_surface, 0.08), "hover": _alpha(p.on_surface, 0.05), "disabled": Color.TRANSPARENT})
	for st in ["normal", "hover", "pressed", "hover_pressed", "disabled"]:
		var ob := t.get_stylebox(st, "OutlineButton")
		if ob is StyleBoxEmpty:
			ob = box(Color(0, 0, 0, 0), R.s, Vector4(ob.content_margin_left, ob.content_margin_top, ob.content_margin_right, ob.content_margin_bottom))
			t.set_stylebox(st, "OutlineButton", ob)
		(ob as StyleBoxFlat).set_border_width_all(2)
		(ob as StyleBoxFlat).border_color = _alpha(p.outline, 0.55 if st == "disabled" else 1.0)
	# the close affordance of every sheet / page / window: an 84 px touch rect with a 56 px round chip drawn in it;
	# CloseButtonFlat (Low tier) = the glyph only (one draw less)
	_button(t, "CloseButton", p, p.surface_high, p.on_surface, {"radius": R.pill, "margin": Vector4(25, 25, 25, 25), "pressed": p.surface_high.darkened(0.12), "hover": p.surface_high.lightened(0.06), "disabled": Color.TRANSPARENT, "icon_max": 30})
	_button(t, "CloseButtonFlat", p, Color.TRANSPARENT, p.on_surface_muted, {"radius": R.pill, "margin": Vector4(25, 25, 25, 25), "pressed": _alpha(p.on_surface, 0.1), "hover": _alpha(p.on_surface, 0.06), "hover_text": p.on_surface, "pressed_text": p.on_surface, "disabled": Color.TRANSPARENT, "icon_max": 30})
	for pill in ["ActionPill", "ActionPillTonal"]:
		for st in ["normal", "hover", "pressed", "hover_pressed", "disabled"]:
			var af := t.get_stylebox(st, pill) as StyleBoxFlat
			if af != null:
				af.expand_margin_top = -12
				af.expand_margin_bottom = -12
	for st in ["normal", "hover", "pressed", "hover_pressed"]:
		var cf := t.get_stylebox(st, "CloseButton") as StyleBoxFlat
		if cf != null:
			for side in [SIDE_LEFT, SIDE_TOP, SIDE_RIGHT, SIDE_BOTTOM]:
				cf.set_expand_margin(side, -14)
	for pill in ["PriceButton", "OwnedButton"]:   # a badge-sized pill drawn inside the full 48 dp touch rect
		for st in ["normal", "hover", "pressed", "hover_pressed", "disabled"]:
			var pf := t.get_stylebox(st, pill) as StyleBoxFlat
			if pf != null:
				pf.expand_margin_top = -PILL_INSET
				pf.expand_margin_bottom = -PILL_INSET
	_button(t, "RowButton", p, Color.TRANSPARENT, p.on_surface, {"radius": 0, "margin": Vector4(24, 14, 24, 14), "pressed": _alpha(p.on_surface, 0.07), "hover": _alpha(p.on_surface, 0.04), "disabled": Color.TRANSPARENT})

	# ---- surfaces
	var M := Vector4(24, 20, 24, 20)
	var line := _alpha(p.outline, 0.7)
	var panels := {
		"PanelContainer": box(p.surface, R.m, M, 1, line, "e1"),
		"Card": box(p.surface, R.m, M, 1, line, "e1"),
		"CardRaised": box(p.surface_variant, R.m, Vector4(18, 18, 18, 18), 1, _alpha(p.outline, 0.45), "e0"),
		# 2026-10-26: sheets sit one step DARKER than their cards (background, not surface) — the cards read as cards
		# instead of grey-on-grey; a bottom sheet is rounded at the top only (it meets the screen edge)
		"Sheet": box(p.background, R.l, Vector4(28, 24, 28, 24), 1, _alpha(p.outline, 0.7), "e3") if v2 else box(p.surface, R.l, Vector4(32, 28, 32, 28), 1, _alpha(p.outline, 0.8), "e3"),
		"SheetBottom": _top_only(box(p.background, R.xl, Vector4(28, 12, 28, 24), 1, _alpha(p.outline, 0.7), "e3")) if v2 else box(p.surface, R.xl, Vector4(28, 12, 28, 24), 1, _alpha(p.outline, 0.8), "e3"),
		"Grabber": box(_alpha(p.on_surface_muted, 0.45), 4, Vector4.ZERO),
		"EmptyCard": box(_alpha(p.surface_variant, 0.45), R.m, Vector4(24, 22, 24, 22), 2, _alpha(p.outline, 0.45)),
		"Glass": box(_alpha(p.surface, 0.93), R.l, Vector4(26, 10, 30, 10), 1, _alpha(p.outline, 0.75), "e2"),
		"Group": box(p.surface_variant, R.m, Vector4.ZERO),
		"Track": box(p.surface_sunken, R.s, Vector4(6, 6, 6, 6)),
		"InfoCard": box(p.accent_container, R.m, Vector4(20, 16, 20, 16)),
		"WarningCard": box(p.warning_container, R.m, Vector4(20, 16, 20, 16)),
		"Toast": box(p.surface_high, 42, Vector4(16, 14, 30, 14), 1, _alpha(p.outline, 0.8), "e3"),
		"Bubble": box(p.surface_high, R.m, Vector4(28, 22, 28, 20), 1, _alpha(p.outline, 0.8), "e3"),
		"Badge": box(p.primary, 17, Vector4(14, 2, 14, 2)),
		"BadgeSuccess": box(p.success_container, 17, Vector4(14, 2, 14, 2)),
		"Chip": box(p.surface_variant, 26, Vector4(18, 8, 20, 8)),
		"Scrim": box(_alpha(p.scrim, 0.66), 0, Vector4.ZERO),
		# #113 r2 list cards (UiKit.list_card): flat (no shadow pass), one stylebox shared by every card of a state
		"ListCard": box(p.surface_variant, R.m, LIST_CARD_M, 1, _alpha(p.outline, 0.55)),
		"ListCardSelected": box(p.surface_variant, R.m, LIST_CARD_M, 3, p.primary),
		"NoteCard": box(p.accent_container, R.s, Vector4(18, 10, 18, 10)),
		# the shop's cards: the 48 dp price pill already gives the row its height — less padding keeps ≥ 3 products on screen
		"ListCardDense": box(p.surface_variant, R.m, LIST_CARD_DENSE_M, 1, _alpha(p.outline, 0.55)),
		"ListCardDenseSelected": box(p.surface_variant, R.m, LIST_CARD_DENSE_M, 3, p.primary),
		# #132 short landscape panel (UiShop "tight"): no vertical padding — the 48 dp touch rect of the price is the card
		"ListCardTight": box(p.surface_variant, R.m, LIST_CARD_TIGHT_M, 1, _alpha(p.outline, 0.55)),
		"ListCardTightSelected": box(p.surface_variant, R.m, LIST_CARD_TIGHT_M, 3, p.primary),
		# 2026-10-26 Low tier: a plain row — same padding, no box (the row is title + line + the coloured action text;
		# the denser pill rows show more cards, so each costs one draw less to stay within the old page's draws)
		"ListCardFlat": _empty(LIST_CARD_M),
		"ListCardDenseFlat": _empty(LIST_CARD_DENSE_M),
		"ListCardLocked": box(_alpha(p.surface_sunken, 0.55), R.m, LIST_CARD_M, 1, _alpha(p.outline, 0.3)),
	}
	for k in panels:
		if k != "PanelContainer":
			t.set_type_variation(k, "PanelContainer")
		t.set_stylebox("panel", k, panels[k])
	t.set_stylebox("panel", "Panel", box(p.surface, R.m, Vector4.ZERO, 1, line))

	# ---- scrolling: no visible track, a 6 px pill thumb (UiScroll fades it out when the list is still)
	t.set_stylebox("panel", "ScrollContainer", _empty())
	for bar in ["VScrollBar", "HScrollBar"]:
		var v: bool = bar == "VScrollBar"
		var track := _empty(Vector4(3, 0, 3, 0) if v else Vector4(0, 3, 0, 3))
		t.set_stylebox("scroll", bar, track)
		t.set_stylebox("scroll_focus", bar, track)
		var thumbs := {"grabber": 0.30, "grabber_highlight": 0.42, "grabber_pressed": 0.55}
		for st in thumbs:
			var g := box(_alpha(p.on_surface, thumbs[st]), 3, Vector4(3, 3, 3, 3))
			t.set_stylebox(st, bar, g)
		var none := ImageTexture.create_from_image(Image.create(1, 1, false, Image.FORMAT_RGBA8))
		for ic in ["increment", "increment_highlight", "increment_pressed", "decrement", "decrement_highlight", "decrement_pressed"]:
			t.set_icon(ic, bar, none)

	# ---- pickers / menus
	t.set_stylebox("panel", "PopupMenu", box(p.surface_high, R.m, Vector4(12, 12, 12, 12), 1, _alpha(p.outline, 0.9), "e3"))
	t.set_stylebox("hover", "PopupMenu", box(p.primary_container, R.s, Vector4(16, 0, 16, 0)))
	var sep := StyleBoxLine.new()
	sep.color = _alpha(p.outline, 0.6)
	sep.thickness = 1
	t.set_stylebox("separator", "PopupMenu", sep)
	t.set_font("font", "PopupMenu", bold)
	t.set_font_size("font_size", "PopupMenu", T.label.size)
	t.set_color("font_color", "PopupMenu", p.on_surface)
	t.set_color("font_hover_color", "PopupMenu", p.primary)
	t.set_color("font_disabled_color", "PopupMenu", p.on_surface_muted)
	t.set_constant("v_separation", "PopupMenu", 44)
	t.set_constant("h_separation", "PopupMenu", 16)
	t.set_constant("item_start_padding", "PopupMenu", 20)
	t.set_constant("item_end_padding", "PopupMenu", 24)
	t.set_constant("icon_max_width", "PopupMenu", 84)
	var radio_on := Icons.texture_1x("check", 28, p.primary, 2.5)
	t.set_icon("radio_checked", "PopupMenu", radio_on)
	t.set_icon("checked", "PopupMenu", radio_on)
	t.set_stylebox("panel", "PopupPanel", box(p.surface_high, R.m, Vector4(12, 12, 12, 12), 1, _alpha(p.outline, 0.9), "e3"))
	_button(t, "OptionButton", p, Color.TRANSPARENT, p.on_surface, {"radius": R.s, "margin": Vector4(24, 14, 24, 14), "pressed": _alpha(p.on_surface, 0.07), "hover": _alpha(p.on_surface, 0.04), "disabled": Color.TRANSPARENT})
	t.set_font("font", "OptionButton", bold)
	t.set_icon("arrow", "OptionButton", Icons.texture_1x("chevron-right", 30, p.on_surface_muted))
	t.set_constant("arrow_margin", "OptionButton", 4)
	t.set_constant("modulate_arrow", "OptionButton", 0)

	# ---- toggles: CheckButton = switch on the right, CheckBox = rounded tick box (pictures at 1x: Godot draws them unscaled)
	_button(t, "CheckButton", p, Color.TRANSPARENT, p.on_surface, {"radius": R.s, "margin": Vector4(24, 16, 24, 16), "pressed": Color.TRANSPARENT, "hover": _alpha(p.on_surface, 0.04), "disabled": Color.TRANSPARENT})
	t.set_font("font", "CheckButton", bold)
	t.set_icon("checked", "CheckButton", switch_texture(p, true, false, 1.0))
	t.set_icon("unchecked", "CheckButton", switch_texture(p, false, false, 1.0))
	t.set_icon("checked_disabled", "CheckButton", switch_texture(p, true, true, 1.0))
	t.set_icon("unchecked_disabled", "CheckButton", switch_texture(p, false, true, 1.0))
	t.set_constant("check_v_offset", "CheckButton", 0)
	_button(t, "CheckBox", p, Color.TRANSPARENT, p.on_surface, {"radius": R.s, "margin": Vector4(16, 16, 16, 16), "pressed": Color.TRANSPARENT, "hover": _alpha(p.on_surface, 0.04), "disabled": Color.TRANSPARENT})
	t.set_font("font", "CheckBox", regular)
	t.set_font_size("font_size", "CheckBox", T.body.size)
	t.set_icon("checked", "CheckBox", Icons.tile("check", 40, p.on_primary, p.primary, false, 10))
	t.set_icon("unchecked", "CheckBox", Icons.tile("x", 40, Color.TRANSPARENT, p.surface_sunken, false, 10))
	t.set_icon("radio_checked", "CheckBox", Icons.tile("check", 40, p.on_primary, p.primary, true))
	t.set_icon("radio_unchecked", "CheckBox", Icons.tile("x", 40, Color.TRANSPARENT, p.surface_sunken, true))

	# ---- progress, slider, text field, separators, tooltip
	t.set_stylebox("background", "ProgressBar", box(p.surface_sunken, 6, Vector4(0, 6, 0, 6)))
	t.set_stylebox("fill", "ProgressBar", box(p.primary, 6, Vector4(0, 6, 0, 6)))
	t.set_font_size("font_size", "ProgressBar", T.caption.size)
	t.set_color("font_color", "ProgressBar", p.on_surface)
	t.set_type_variation("CardProgress", "ProgressBar")   # 2026-10-26: a list card's thin level bar
	t.set_stylebox("background", "CardProgress", box(p.surface_sunken, 4, Vector4(0, 4, 0, 4)))
	t.set_stylebox("fill", "CardProgress", box(p.primary, 4, Vector4(0, 4, 0, 4)))
	t.set_stylebox("slider", "HSlider", box(p.surface_sunken, 6, Vector4(0, 6, 0, 6)))
	t.set_stylebox("grabber_area", "HSlider", box(p.primary, 6, Vector4(0, 6, 0, 6)))
	t.set_stylebox("grabber_area_highlight", "HSlider", box(p.primary.lightened(0.08), 6, Vector4(0, 6, 0, 6)))
	var knob := _knob(p.on_surface, p.primary)
	t.set_icon("grabber", "HSlider", knob)
	t.set_icon("grabber_highlight", "HSlider", knob)
	t.set_stylebox("normal", "LineEdit", box(p.surface_sunken, R.s, Vector4(20, 22, 20, 22), 2, p.outline))
	t.set_stylebox("focus", "LineEdit", box(Color.TRANSPARENT, R.s, Vector4(20, 22, 20, 22), 2, p.primary))
	t.set_stylebox("read_only", "LineEdit", box(p.surface_variant, R.s, Vector4(20, 22, 20, 22)))
	t.set_color("font_color", "LineEdit", p.on_surface)
	t.set_color("font_placeholder_color", "LineEdit", p.on_surface_muted)
	t.set_color("caret_color", "LineEdit", p.primary)
	t.set_color("selection_color", "LineEdit", _alpha(p.primary, 0.35))
	var hline := StyleBoxLine.new()
	hline.color = _alpha(p.outline, 0.55)
	hline.thickness = 1
	t.set_stylebox("separator", "HSeparator", hline)
	t.set_constant("separation", "HSeparator", 1)
	var vline := StyleBoxLine.new()
	vline.color = _alpha(p.outline, 0.55)
	vline.thickness = 1
	vline.vertical = true
	t.set_stylebox("separator", "VSeparator", vline)
	t.set_constant("separation", "VSeparator", 1)
	t.set_stylebox("panel", "TooltipPanel", box(p.surface_high, R.s, Vector4(16, 10, 16, 10), 1, p.outline, "e2"))
	t.set_color("font_color", "TooltipLabel", p.on_surface)
	t.set_font_size("font_size", "TooltipLabel", T.caption.size)
	t.set_constant("separation", "VBoxContainer", Tokens.SPACE.s)
	t.set_constant("separation", "HBoxContainer", Tokens.SPACE.s)
	return t


static func switch_texture(p: Dictionary, on: bool, disabled: bool = false, scale: float = 2.0) -> Texture2D:
	if scale >= 2.0:
		return Icons.switch(on, p.primary, p.on_primary, p.surface_sunken, p.outline, p.on_surface_muted, disabled)
	return Icons.switch_1x(on, p.primary, p.on_primary, p.surface_sunken, p.outline, p.on_surface_muted, disabled)


static func _knob(fill: Color, ring: Color) -> Texture2D:
	return Icons.knob(fill, ring)
