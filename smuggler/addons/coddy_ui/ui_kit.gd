extends RefCounted
## coddy_ui — the fleet game UI kit (W64). Entry point: pick the game's palette once, put the Theme on the HUD root,
## build screens from these factories and the component scripts. Every size is a design token (ui_tokens.gd).
##   const UiKit = preload("res://addons/coddy_ui/ui_kit.gd")
##   theme = UiKit.use("canal-control")          # palette + Theme for the current language (rebuild after a language change)
##   var b := UiKit.button(tr("Dispatch"), "primary", "route", dispatch)
## Motion: tweens only (never per-frame work), Tokens.MOTION durations; `UiKit.reduce_motion = true` snaps them.
## Components (preload the script, .new(), add, then call its setup):
##   ui_toast.gd (UiToast) · ui_modal.gd (UiModal: dialog / bottom sheet) · ui_pager.gd (UiPager: onboarding pages)
##   ui_coach.gd (UiCoach: spotlight + pointer bubble) · ui_segmented.gd · ui_row.gd (settings / list rows)
##   ui_step_dots.gd · ui_illustration.gd · ui_shop.gd (restyles the shared coddy_iap shop) · ui_loading.gd
##   ui_scroll.gd (finger-scroll lists, fading thin thumb) · touch_scroll.gd · ui_reconcile.gd (rebuild without eating taps)
## No strings live here: games pass translated text (tr) — the kit never calls tr().

const Tokens = preload("ui_tokens.gd")
const ThemeBuilder = preload("ui_theme.gd")
const Fonts = preload("ui_fonts.gd")
const Icons = preload("ui_icons.gd")
const Scroll = preload("ui_scroll.gd")
const StepDots = preload("ui_step_dots.gd")

static var p := {}                    ## current palette: role -> Color
static var palette_name := ""
static var tap_sound: Callable = Callable()   ## called on every kit button press (game's soft tap sfx)
## Reduce motion (a game's accessibility setting, or a capture tool that wants still frames): kit components snap to
## their end state instead of playing transitions (UiPager pages / open / close, step dots).
static var reduce_motion := false
## Design generation (2026-10-26): 1 = the W64 look (default — a game that has not adopted the tab/modal redesign looks
## and behaves exactly as before); 2 = the redesign (darker sheets, round close chip in UiModal, pill grab handle,
## columns that stack when narrow, whole-word settings row titles). Set it before the first use().
static var design := 1
static var _themes := {}


## Selects the palette (a name from tokens.json "palettes" or a role dict) + optional role overrides, and returns the
## Theme for the current language. Call again after TranslationServer.set_locale (the font script order changes).
static func use(palette = "fleet", overrides: Dictionary = {}) -> Theme:
	var key := str(palette) + JSON.stringify(overrides) + "|v%d" % design
	if key != palette_name:
		p = ThemeBuilder.palette(palette, overrides)
		palette_name = key
		_themes.clear()
	return theme()


## Frees every cached theme / font stack / icon picture (called automatically when the scene tree ends).
static func clear_caches() -> void:
	_themes.clear()
	Icons.clear_cache()
	Fonts.clear_cache()


static func theme(locale: String = "") -> Theme:
	if p.is_empty():
		use()
	if locale == "":
		locale = TranslationServer.get_locale()
	var k := Fonts.script_for(locale)
	if not _themes.has(k):
		_themes[k] = ThemeBuilder.build(p, locale, design >= 2)
	return _themes[k]


static func c(role: String) -> Color:
	if p.is_empty():
		use()
	return p.get(role, Color.MAGENTA)


static func dp(v: float) -> int:
	return int(roundf(v * Tokens.DP))


static func space(name: String) -> int:
	return int(Tokens.SPACE.get(name, 12))


# ---------------------------------------------------------------- text

## style: Display, Headline, Title, BodyLarge, Body, Caption, Overline, Muted, CaptionMuted, Value, Accent, Brand,
## LabelStrong, BodyMuted. wrap = autowrap (give the label a width: inside a VBox / card, never alone in an HBox).
static func label(text: String, style: String = "Body", wrap: bool = false) -> Label:
	var l := Label.new()
	if style == "Overline":
		upper_text(l, text)
	else:
		l.text = text
	l.theme_type_variation = style
	if wrap:
		l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l


## Upper-cased kit text (Overline, badge, coach step). When upper-casing changed the text it is translated text, so the
## label's auto-translation is switched off: else an upper-cased word that is also a CSV key was translated back
## (tr("RACE") = "Race" → "RACE" → key RACE → "Race"; Pit Lane Boss W66 10-27, it "WEEKEND", de "QUALIFYING"). A text
## that is upper case already (an all-caps key such as "UI_SETTINGS", or CJK) keeps auto-translation, as before.
static func upper_text(l: Label, text: String) -> void:
	var up := text.to_upper()
	if up != text:
		l.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	l.text = up


static func section(text: String) -> Label:
	var l := label(text, "Overline")
	l.add_theme_constant_override("line_spacing", 0)
	return l


# ---------------------------------------------------------------- buttons

## kind: primary, secondary, tonal, ghost, danger, text (a brand-coloured text button). icon: a Lucide name ("" = none). Min height 84 px (48 dp).
static func button(text: String, kind: String = "secondary", icon: String = "", on_press: Callable = Callable(), min_w: int = 0) -> Button:
	var b := Button.new()
	b.text = text
	b.theme_type_variation = kind.capitalize() + "Button"
	b.custom_minimum_size = Vector2(min_w, Tokens.TOUCH.min)
	b.focus_mode = Control.FOCUS_NONE
	b.mouse_filter = Control.MOUSE_FILTER_STOP
	if icon != "":
		b.icon = Icons.texture(icon, 30, Color.WHITE)
		b.expand_icon = false
	_wire(b, on_press)
	watch_fit(b)
	return b


## Round 84 px icon button (glass by default; flat = no fill). tooltip = the translated name (accessibility, tests).
static func icon_button(icon: String, on_press: Callable = Callable(), tooltip: String = "", flat: bool = false) -> Button:
	var b := Button.new()
	b.theme_type_variation = "IconButtonFlat" if flat else "IconButton"
	b.icon = Icons.texture(icon, 34, Color.WHITE)
	b.icon_alignment = HORIZONTAL_ALIGNMENT_CENTER
	b.custom_minimum_size = Vector2(Tokens.TOUCH.icon_button, Tokens.TOUCH.icon_button)
	b.tooltip_text = tooltip
	b.focus_mode = Control.FOCUS_NONE
	_wire(b, on_press)
	return b


## A tab (icon above a short label) for side tabs / bottom tab bars. Toggle mode: `button_pressed` = selected.
static func tab_button(text: String, icon: String, on_press: Callable = Callable()) -> Button:
	var b := Button.new()
	b.text = text
	b.theme_type_variation = "TabButton"
	b.icon = Icons.texture(icon, 30, Color.WHITE)
	b.icon_alignment = HORIZONTAL_ALIGNMENT_CENTER
	b.vertical_icon_alignment = VERTICAL_ALIGNMENT_TOP
	b.toggle_mode = true
	b.custom_minimum_size = Vector2(0, Tokens.TOUCH.min)
	b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	b.focus_mode = Control.FOCUS_NONE
	b.clip_text = false
	b.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	_wire(b, on_press)
	return b


## #113 drawer / sheet header: icon tile + title + close button, then a hairline divider. Returns
## {"box": VBoxContainer (add it to the sheet), "title": Label, "icon": TextureRect, "close": Button}.
## The title wraps instead of overflowing (10 languages); the close button keeps the 48 dp target.
static func sheet_header(icon_name: String, title: String, on_close: Callable = Callable(), kind: String = "primary") -> Dictionary:
	var box := VBoxContainer.new()
	box.name = "SheetHeader"
	box.add_theme_constant_override("separation", space("s"))
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", space("m"))
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var ic := tile(icon_name, kind, 56, true)
	ic.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(ic)
	var l := label(title, "Title", true)
	l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	l.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(l)
	var close: Button = null
	if on_close.is_valid():
		close = icon_button("x", on_close)
		close.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		row.add_child(close)
	box.add_child(row)
	var sep := HSeparator.new()
	sep.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.add_child(sep)
	return {"box": box, "title": l, "icon": ic, "close": close}


## Swaps the header icon (a tab change re-uses the same header).
static func sheet_header_set(h: Dictionary, icon_name: String, title: String, kind: String = "primary") -> void:
	(h.title as Label).text = title
	var fresh := tile(icon_name, kind, 56, true)
	(h.icon as TextureRect).texture = fresh.texture
	fresh.free()


## #113 opening motion for a sheet/drawer whose CONTENT IS ALREADY BUILT: content is drawn from the first frame
## (alpha starts at 0.7, never 0) and fades to full in 140 ms. Geometry never moves (no scale/slide), so a tap target
## is where it is drawn from frame 1. animate=false (Low tier) = no tween: zero extra per-frame CPU. Returns the Tween.
static func open_sheet(sheet: Control, animate: bool = true, _from_bottom: bool = true) -> Tween:
	if sheet.has_meta("coddy_open_tween"):
		var old = sheet.get_meta("coddy_open_tween")
		if old is Tween and (old as Tween).is_valid():
			(old as Tween).kill()
		sheet.remove_meta("coddy_open_tween")
	sheet.modulate.a = 1.0
	if not animate or not sheet.is_inside_tree():
		return null
	sheet.modulate.a = 0.7
	var tw := sheet.create_tween().set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	tw.tween_property(sheet, "modulate:a", 1.0, 0.14)
	sheet.set_meta("coddy_open_tween", tw)
	return tw


# ---------------------------------------------------------------- tab pages + sheets (2026-10-26 redesign)
#
# The one look of every tab page (drawer / side panel) and every window, on top of the tokens:
#   spacing   4 · 8 · 12 · 16 · 24 · 32 (Tokens.SPACE) — page gutter 24, card gap 21 (12 dp), section gap +12 above
#   radii     card 20 · sheet 28 (top only when it meets the bottom edge) · pill for one-tap actions and badges
#   elevation sheet e3 (over the game) · cards flat (#113 r2: no shadow pass) — depth = sheet one step darker
#   type      page / window title = Title 30 bold · card title LabelStrong 26 · grey line CaptionMuted 23→21 · overline 21
#   icons     card tile 64 · page tile 52 · glyphs 26–34 (≥ 24 dp) · touch ≥ 84 px (48 dp) everywhere
#   colour    sheet = palette background, cards = surface_variant (one clear step), ONE primary per card (the pill on
#             the right), secondary actions = outline, status in badges only
# Low tier (#114): no icon tiles, no progress bars, flat close glyph — a page costs at most +2 draws over the old one.

const PAGE_TILE := 52


## The close affordance of every page / sheet / window: top-right, 84 px touch rect, a 56 px round chip (Low: the
## glyph only). tooltip = the translated "Close" (accessibility, tests).
## A tap that a custom gui_input handler should act on — ONCE (W66 2026-10-27). On a phone Godot delivers every touch
## twice: the InputEventScreenTouch and an emulated left click (emulate_mouse_from_touch, device DEVICE_ID_EMULATION);
## a desktop with emulate_touch_from_mouse does the reverse. Handlers that took both acted twice: a toggle opened and
## closed again (the shop's info strip), a store link opened twice, Sky Tower's floor tap became pick + Call. Only the
## real event counts. on_release: the finger / button came up (strips and tiles act on release, buttons on press).
static func is_tap(ev: InputEvent, on_release: bool = false) -> bool:
	if ev == null or ev.device == InputEvent.DEVICE_ID_EMULATION:
		return false
	if ev is InputEventScreenTouch:
		return (ev as InputEventScreenTouch).pressed != on_release
	if ev is InputEventMouseButton:
		var mb := ev as InputEventMouseButton
		return mb.button_index == MOUSE_BUTTON_LEFT and mb.pressed != on_release
	return false


static func close_button(on_close: Callable = Callable(), tooltip: String = "", low: bool = false) -> Button:
	var b := Button.new()
	b.name = "Close"
	b.theme_type_variation = "CloseButtonFlat" if low else "CloseButton"
	b.icon = Icons.texture("x", 30, Color.WHITE)
	b.icon_alignment = HORIZONTAL_ALIGNMENT_CENTER
	b.custom_minimum_size = Vector2(Tokens.TOUCH.icon_button, Tokens.TOUCH.icon_button)
	b.tooltip_text = tooltip
	b.focus_mode = Control.FOCUS_NONE
	b.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	_wire(b, on_close)
	return b


## The head of a tab page (drawer / side panel): [52 px icon tile] Title + one summary line [close], a hairline under
## it. opts: sub (String or Array long → short: "3 trucks · 2 ready"), kind (tile colour), low (no tile), close_label.
## Returns {"box", "title", "sub", "icon", "close"}; update it in place with page_header_set() (a tab change).
static func page_header(icon_name: String, title: String, on_close: Callable = Callable(), opts: Dictionary = {}) -> Dictionary:
	var low: bool = opts.get("low", false)
	var box := VBoxContainer.new()
	box.name = "PageHeader"
	box.add_theme_constant_override("separation", space("s"))
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var row := HBoxContainer.new()
	row.name = "Row"
	row.add_theme_constant_override("separation", space("m"))
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.add_child(row)
	var ic: TextureRect = null
	if not low:
		ic = tile(icon_name, str(opts.get("kind", "primary")), PAGE_TILE, false)
		ic.name = "Icon"
		ic.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		row.add_child(ic)
	var texts := VBoxContainer.new()
	texts.name = "Texts"
	texts.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	texts.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	texts.add_theme_constant_override("separation", 0)
	texts.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(texts)
	var tl := fit_line([title], "Title")
	tl.name = "Title"
	tl.add_theme_font_size_override("font_size", Tokens.TYPE.title.size)
	tl.set_meta("coddy_fit_start", Tokens.TYPE.title.size)
	texts.add_child(tl)
	var sub = opts.get("sub", "")
	var sl := fit_line(sub if sub is Array and not (sub as Array).is_empty() else [str(sub) if str(sub) != "" else " "])
	sl.name = "Sub"
	sl.visible = not (str(sub) == "" or (sub is Array and (sub as Array).is_empty()))
	texts.add_child(sl)
	var close: Button = null
	if on_close.is_valid():
		close = close_button(on_close, str(opts.get("close_label", "")), low)
		close.name = "PageClose"
		row.add_child(close)
	var sep := HSeparator.new()
	sep.name = "Divider"
	sep.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.add_child(sep)
	return {"box": box, "title": tl, "sub": sl, "icon": ic, "close": close}


## A tab change re-uses the header: new icon, title and summary line (alternatives long → short; "" hides it).
static func page_header_set(h: Dictionary, icon_name: String, title: String, sub = "", kind: String = "primary") -> void:
	var tl := h.title as Label
	tl.set_meta("coddy_fit_alts", [title])
	tl.text = title
	fit_line_later.call_deferred(tl)
	page_header_sub(h, sub)
	if h.icon != null and is_instance_valid(h.icon):
		var key := "%s|%s" % [icon_name, kind]
		if (h.icon as TextureRect).get_meta("coddy_tile", "") != key:
			var fresh := tile(icon_name, kind, PAGE_TILE, false)
			(h.icon as TextureRect).texture = fresh.texture
			(h.icon as TextureRect).set_meta("coddy_tile", key)
			fresh.free()


## The summary line alone (live values: refreshed with the list, set only when it changed).
static func page_header_sub(h: Dictionary, sub) -> void:
	var sl := h.sub as Label
	var alts: Array = sub if sub is Array else ([str(sub)] if str(sub) != "" else [])
	sl.visible = not alts.is_empty()
	if alts.is_empty() or sl.get_meta("coddy_fit_alts", []) == alts:
		return
	sl.set_meta("coddy_fit_alts", alts)
	sl.text = str(alts[0])
	fit_line_later.call_deferred(sl)


## A section head inside a list: OVERLINE text [trailing count / value], 12 px of extra air above it (groups read as
## groups). Same one or two text items as section() — no extra draw.
static func section_header(text: String, trailing: String = "") -> MarginContainer:
	var m := MarginContainer.new()
	m.name = "Section"
	m.add_theme_constant_override("margin_top", space("s"))
	m.add_theme_constant_override("margin_left", 4)
	m.add_theme_constant_override("margin_right", 4)
	m.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var h := HBoxContainer.new()
	h.mouse_filter = Control.MOUSE_FILTER_IGNORE
	m.add_child(h)
	var l := section(text)
	l.name = "Text"
	l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	h.add_child(l)
	if trailing != "":
		var t := label(trailing, "CaptionMuted")
		t.name = "Trailing"
		h.add_child(t)
	return m


## The single call to action of a list card, as a pill on the RIGHT of its head row (kit list_card_side): primary =
## buy / do it now (the game's primary colour), tonal = a secondary action. A disabled pill stays readable (dimmed).
## icon (optional, Medium / High only): a Lucide name drawn before the text ("coins").
## low (Low tier, #114): the action as coloured text in the regular face, no pill box (it batches with the grey line).
static func action_pill(text: String, on_press: Callable = Callable(), enabled: bool = true, kind: String = "primary", icon_name: String = "", low: bool = false) -> Button:
	var b := Button.new()
	b.text = text
	if low:
		b.theme_type_variation = "ActionText" if kind == "primary" else "ActionTextTonal"
		icon_name = ""
	else:
		b.theme_type_variation = "ActionPill" if kind == "primary" else "ActionPillTonal"
	b.custom_minimum_size = Vector2(0, Tokens.TOUCH.min)
	b.focus_mode = Control.FOCUS_NONE
	b.disabled = not enabled
	if icon_name != "":
		b.icon = Icons.texture(icon_name, 26, Color.WHITE)
	_wire(b, on_press)
	watch_fit(b)
	return b


## Nothing in the list yet: a quiet card — [tile] title, one or two lines, optional tonal action. Low: no box, no tile,
## no body line, the action as coloured text. Replaces a lone grey line in a tall empty page.
static func empty_card(icon_name: String, title: String, body: String, action: String = "", on_action: Callable = Callable(), low: bool = false) -> PanelContainer:
	var pc := PanelContainer.new()
	pc.name = "EmptyCard"
	# Low (#114): no box, no tile, no second line; the action is coloured text (title + action = 2 text draws)
	pc.theme_type_variation = "ListCardFlat" if low else "EmptyCard"
	pc.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", space("m"))
	v.mouse_filter = Control.MOUSE_FILTER_IGNORE
	pc.add_child(v)
	var h := HBoxContainer.new()
	h.add_theme_constant_override("separation", space("m"))
	h.mouse_filter = Control.MOUSE_FILTER_IGNORE
	v.add_child(h)
	if not low:
		var t := tile(icon_name, "neutral", CARD_TILE, true)
		t.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
		h.add_child(t)
	var texts := VBoxContainer.new()
	texts.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	texts.add_theme_constant_override("separation", 4)
	texts.mouse_filter = Control.MOUSE_FILTER_IGNORE
	h.add_child(texts)
	var tl := label(title, "LabelStrong", true)
	tl.name = "Title"
	tl.ready.connect(func() -> void: _word_min(tl))
	texts.add_child(tl)
	if body != "" and not low:
		var bl := label(body, "CaptionMuted", true)
		bl.name = "Body"
		bl.ready.connect(func() -> void: _word_min(bl))
		texts.add_child(bl)
	if action != "":
		var b := action_pill(action, on_action, true, "primary", "", true) if low else button(action, "tonal", "", on_action)
		b.name = "Action"
		b.size_flags_horizontal = Control.SIZE_SHRINK_END
		v.add_child(b)
	return pc


## A thin level / progress bar for a list card's text column (Medium / High; Low skips it: two more draws per card).
static func card_progress(card: Control, value: float, max_value: float = 1.0) -> ProgressBar:
	var texts := card.get_node("Col/Head/Texts") as VBoxContainer
	var pb := progress(value, max_value)
	pb.name = "Progress"
	pb.custom_minimum_size = Vector2(0, 8)
	pb.theme_type_variation = "CardProgress"
	texts.add_child(pb)
	return pb


static func _tap() -> void:
	if tap_sound.is_valid():
		tap_sound.call()


static func _wire(b: BaseButton, on_press: Callable) -> void:
	b.pressed.connect(_tap)
	if on_press.is_valid():
		b.pressed.connect(on_press)


# ---------------------------------------------------------------- surfaces & bits

## kind: Card, CardRaised, Sheet, SheetBottom, Glass, Group, Track, InfoCard, WarningCard, Toast, Bubble, Chip.
static func card(kind: String = "Card") -> PanelContainer:
	var pc := PanelContainer.new()
	pc.theme_type_variation = kind
	return pc


## A settings / list group: a rounded block whose rows are separated by hairlines. Add rows with group_add().
static func group() -> PanelContainer:
	var g := card("Group")
	var v := VBoxContainer.new()
	v.name = "Rows"
	v.add_theme_constant_override("separation", 0)
	g.add_child(v)
	return g


static func group_add(g: PanelContainer, row: Control) -> Control:
	var v: VBoxContainer = g.get_node("Rows")
	if v.get_child_count() > 0:
		var s := HSeparator.new()
		s.mouse_filter = Control.MOUSE_FILTER_IGNORE
		v.add_child(s)
	v.add_child(row)
	return row


## kind → (fg role, bg role) for icon tiles: primary, accent, success, warning, danger, info, neutral.
static func tile_colors(kind: String) -> Array:
	match kind:
		"neutral": return [c("on_surface_muted"), c("surface_high")]
		"primary_solid": return [c("on_primary"), c("primary")]
		_: return [c(kind) if p.has(kind) else c("primary"), c(kind + "_container") if p.has(kind + "_container") else c("primary_container")]


static func tile(icon: String, kind: String = "primary", px: int = 64, round: bool = false) -> TextureRect:
	var fb := tile_colors(kind)
	return Icons.tile_rect(icon, px, fb[0], fb[1], round)


static func icon(name: String, px: int = 30, role: String = "on_surface") -> TextureRect:
	return Icons.rect(name, px, c(role))


static func chip(icon_name: String, text: String, icon_role: String = "on_surface_muted") -> PanelContainer:
	var pc := card("Chip")
	pc.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var h := HBoxContainer.new()
	h.add_theme_constant_override("separation", 10)
	if icon_name != "":
		h.add_child(icon(icon_name, 26, icon_role))
	var l := label(text, "LabelStrong")
	l.add_theme_font_size_override("font_size", 23)
	l.name = "Text"
	watch_fit(l)
	h.add_child(l)
	pc.add_child(h)
	return pc


static func badge(text: String, kind: String = "primary") -> PanelContainer:
	var pc := card("BadgeSuccess" if kind == "success" else "Badge")
	pc.mouse_filter = Control.MOUSE_FILTER_IGNORE
	pc.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	var l := Label.new()
	upper_text(l, text)
	l.add_theme_font_override("font", Fonts.stack(true))
	l.add_theme_font_size_override("font_size", 21)
	l.add_theme_color_override("font_color", c("success") if kind == "success" else c("on_primary"))
	pc.add_child(l)
	return pc


# ---------------------------------------------------------------- list cards (#113 round 2)

## Space between list cards: 12 dp.
const CARD_GAP := 21
## Icon tile of a list card (square, rounded): 64 canvas px.
const CARD_TILE := 64
## The one-line secondary text starts here and steps down to FIT_TEXT_MIN before a shorter alternative is tried.
const CARD_SUB_PX := 23
## badge_kind → Label variation (ui_theme.gd)
const BADGE_KINDS := {"locked": "BadgeLocked", "open": "BadgeOpen", "price": "BadgePrice", "level": "BadgeLevel",
	"warning": "BadgeWarning"}


## A list card: [square icon tile] title over ONE grey line [one status badge]. Body content (an action button) goes
## into list_card_body(card). opts:
##   kind      tile colour kind (tile_colors)          sub        String, or Array of alternatives long → short
##   badge     the single value / state text           badge_kind locked | open | price | level | warning
##   locked    dim card + lock icon in the tile        selected   accent border in the game's primary colour
##   low       Low quality tier (#114: no extra draw): no icon tile, the badge text leads the grey line (one Label, no pill)
##   flat_low  with low: a plain row without the card box (2026-10-26 pill rows: one draw less per card)
## Weak-device cost: the card, tile, title, sub and badge are 5 canvas items (the badge is a Label with its own pill
## stylebox, no PanelContainer); every stylebox comes from the shared theme; text fitting runs on resize / redraw only.
static func list_card(icon_name: String, title: String, opts: Dictionary = {}) -> PanelContainer:
	var locked: bool = opts.get("locked", false)
	var pc := PanelContainer.new()
	pc.name = "ListCard"
	pc.theme_type_variation = card_variation(locked, opts.get("selected", false))
	if opts.get("low", false) and opts.get("flat_low", false) and pc.theme_type_variation == "ListCard":
		pc.theme_type_variation = "ListCardFlat"   # opt-in (2026-10-26): a Low row without its box
	pc.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var col := VBoxContainer.new()
	col.name = "Col"
	col.add_theme_constant_override("separation", 14)
	col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	pc.add_child(col)
	var head := HBoxContainer.new()
	head.name = "Head"
	head.add_theme_constant_override("separation", 16)
	head.mouse_filter = Control.MOUSE_FILTER_IGNORE
	col.add_child(head)
	var t := tile("lock" if locked else icon_name, "neutral" if locked else str(opts.get("kind", "primary")), CARD_TILE, false)
	t.name = "Tile"
	t.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	if opts.get("low", false):
		t.free()   # #114 Low: no icon tile — every texture item is one more draw per card (measured: a shared texture does
		           # not batch across cards); the card keeps its title, line, dim/selected state and action
	else:
		head.add_child(t)
	var texts := VBoxContainer.new()
	texts.name = "Texts"
	texts.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	texts.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	texts.add_theme_constant_override("separation", 2)
	texts.mouse_filter = Control.MOUSE_FILTER_IGNORE
	head.add_child(texts)
	var tl := label(title, "LabelStrong", true)
	tl.name = "Title"
	tl.custom_minimum_size.x = 96
	if locked:
		tl.add_theme_color_override("font_color", c("on_surface_muted"))
	# a word is never split: the title's minimum width = its longest word (set when it enters the tree and after a text
	# change is drawn — never from inside a layout pass, which would leave a stale one-line height)
	tl.ready.connect(func() -> void: _word_min(tl))
	tl.draw.connect(func() -> void: refit_later(tl, REFIT_WORD_MIN))
	texts.add_child(tl)
	var sub = opts.get("sub", "")
	var alts: Array = sub if sub is Array else [str(sub)]
	alts = alts.filter(func(a): return str(a) != "")
	var badge_text := str(opts.get("badge", ""))
	var low: bool = opts.get("low", false)
	if low and badge_text != "":
		# Low tier (#114: no extra draw on a weak phone): the badge is the head of the grey line — same Label, same font,
		# no pill stylebox, no second text item. Medium / High keep the separate pill.
		alts = [badge_text] if alts.is_empty() else alts.map(func(a): return "%s  ·  %s" % [badge_text, str(a)])
	if alts.size() > 0:
		var sl := fit_line(alts)
		if low and badge_text != "":
			sl.set_meta("coddy_low_badge", badge_text)
		texts.add_child(sl)
	if badge_text != "" and not low:
		var b := card_badge(badge_text, str(opts.get("badge_kind", "locked" if locked else "level")))
		head.add_child(b)
	return pc


## The compact explainer at the top of a list (#113 r2): one small icon + ONE line (alternatives long → short, the
## fit of fit_line()). Replaces a paragraph InfoCard; 3 canvas items.
static func list_note(icon_name: String, alts: Array, kind: String = "accent") -> PanelContainer:
	var pc := PanelContainer.new()
	pc.name = "ListNote"
	pc.theme_type_variation = "NoteCard"
	pc.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	pc.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var h := HBoxContainer.new()
	h.add_theme_constant_override("separation", 12)
	h.mouse_filter = Control.MOUSE_FILTER_IGNORE
	pc.add_child(h)
	var ic := icon(icon_name, 28, kind)
	ic.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	h.add_child(ic)
	# #132: each alternative's first whole sentence is a further (shorter) alternative — "।" / "。！？" count as ends
	var all := alts.duplicate()
	for a in alts:
		for x in load("res://addons/coddy_ui/ui_shop.gd").sub_alts(str(a), true):
			if not all.has(x):
				all.append(x)
	var l := fit_line(all, "Caption")
	l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	l.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	h.add_child(l)
	return pc


static func _tight_button(b: Button) -> void:
	for st in ["normal", "hover", "pressed", "hover_pressed", "disabled", "focus"]:
		var sb := b.get_theme_stylebox(st)
		if sb == null or sb is StyleBoxEmpty:
			continue
		var d := sb.duplicate() as StyleBox
		d.content_margin_top = 6
		d.content_margin_bottom = 6
		b.add_theme_stylebox_override(st, d)


## Deferred-safe wrappers (Patron #132): a label freed in the same frame never reaches a typed parameter.
static func fit_line_later(l) -> void:
	if is_instance_valid(l) and (l as Node).is_inside_tree():
		fit_line_now(l)


static func _word_min(l: Label) -> void:
	if not is_instance_valid(l) or not l.is_inside_tree():
		return
	var font: Font = l.get_theme_font("font")
	var w := ceilf(_widest(font, fit_words(l.text), l.get_theme_font_size("font_size"))) + 2.0
	w = maxf(w, 96.0)
	if absf(l.custom_minimum_size.x - w) > 0.5:
		l.custom_minimum_size.x = w


## Theme variation of a list card for its state (games restyle a live card with this, e.g. after a reconcile).
static func card_variation(locked: bool, selected: bool) -> String:
	if selected:
		return "ListCardSelected"
	return "ListCardLocked" if locked else "ListCard"


## The column under the card's head row: add the card's action button(s) here.
static func list_card_body(card: Control) -> VBoxContainer:
	return card.get_node("Col") as VBoxContainer


## #132: a compact action on the RIGHT of the card's head row (after the badge) instead of a full-width button under
## it — the card stays one row tall (the 48 dp touch rect sets its height). Same canvas items as the full-width action.
static func list_card_side(card: Control, action: Control, drop_tile: bool = false) -> Control:
	var head := card.get_node("Col/Head") as HBoxContainer
	# the row has room for ONE right-hand item: the badge becomes the head of the grey line (as on Low; one draw less)
	var badge := head.get_node_or_null("Badge") as Label
	var sub := head.get_node_or_null("Texts/Sub") as Label
	if badge != null and sub != null:
		var alts: Array = sub.get_meta("coddy_fit_alts", [sub.text])
		sub.set_meta("coddy_fit_alts", alts.map(func(a): return "%s  ·  %s" % [badge.text, str(a)]))
		sub.set_meta("coddy_low_badge", badge.text)
		sub.text = str(sub.get_meta("coddy_fit_alts")[0])
		head.remove_child(badge)
		badge.free()
	if action.get_parent() != null:
		action.get_parent().remove_child(action)
	action.size_flags_horizontal = Control.SIZE_SHRINK_END
	action.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	action.custom_minimum_size.y = maxf(action.custom_minimum_size.y, Tokens.TOUCH.min)
	if action is Button and str((action as Button).theme_type_variation).begins_with("Action"):
		# an action pill (2026-10-26) stays ONE line: "450 coins" never wraps into a tall two-line pill — the title
		# column gives way (its line fits down to 21 px, then the shorter alternative)
		(action as Button).autowrap_mode = TextServer.AUTOWRAP_OFF
	elif action is Button:
		# a long label wraps between words inside the 48 dp rect ("Ausruhen / 40 s") instead of pushing the panel wider;
		# its minimum width = the widest word (the kit's word fit), never a split word
		var b := action as Button
		b.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		b.custom_minimum_size.x = 0.0
		# at most TWO lines (10-27, Lifeguard W66): CJK may break between any two characters, so "the widest word" alone
		# let ja "休憩 40秒" shrink to a one-glyph column (休/憩/4/0/秒); the fit keeps the width two lines need
		b.set_meta("coddy_fit_lines", 2)
		watch_fit(b)
		# two short lines fit the 48 dp rect: the button's vertical padding drops to 6 px (same stylebox look, 0 draws)
		b.ready.connect(func() -> void: _tight_button(b), CONNECT_ONE_SHOT)
	# the row is one 48 dp rect tall: the card takes the dense padding (the shop cards' look)
	if card is PanelContainer:
		var v := (card as PanelContainer).theme_type_variation
		if v == "ListCard" or v == "ListCardSelected" or v == "ListCardFlat":
			(card as PanelContainer).theme_type_variation = v.replace("ListCard", "ListCardDense")
	if drop_tile:
		var t := head.get_node_or_null("Tile")
		if t != null:
			head.remove_child(t)
			t.free()   # the row's width goes to the title (one draw less)
	head.add_child(action)
	return action


## The single status / value badge of a list card (a Label with a pill stylebox; one draw).
static func card_badge(text: String, kind: String = "level") -> Label:
	var l := Label.new()
	l.name = "Badge"
	l.text = text
	l.theme_type_variation = BADGE_KINDS.get(kind, "BadgeLevel")
	l.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	l.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l


## ONE grey line of text that never wraps, never ends in '…' and never splits a word: the font steps down from
## CARD_SUB_PX to the 21 px text minimum, then the next (shorter) alternative is tried; only when even the shortest one
## does not fit at 21 px does it wrap on word boundaries (never a cut). Re-fits on resize / redraw (no per-frame work).
static func fit_line(alts: Array, style: String = "CaptionMuted") -> Label:
	var l := label(str(alts[0]), style)
	l.name = "Sub"
	l.clip_text = true                              # the minimum width never pushes the card wider; fit_line_now()
	l.text_overrun_behavior = TextServer.OVERRUN_NO_TRIMMING   # guarantees the text is whole
	l.add_theme_font_size_override("font_size", CARD_SUB_PX)
	l.set_meta("coddy_no_fit", true)
	l.set_meta("coddy_fit_alts", alts.map(func(a): return str(a)))
	l.resized.connect(func() -> void: refit(l, REFIT_LINE))
	l.draw.connect(func() -> void: refit_later(l, REFIT_LINE))
	return l


## Alternatives for fit_line() from a "a · b · c" text: the whole text, then trailing parts dropped one by one
## (["a · b · c", "a · b", "a"]) — a narrow card keeps the most important first parts on one line.
static func dot_alts(text: String, sep: String = "·") -> Array:
	var parts := PackedStringArray()
	for part in text.split(sep, false):
		if part.strip_edges() != "":
			parts.append(part.strip_edges())
	var out := [text]
	var joiner := "  %s  " % sep if text.contains("  %s  " % sep) else " %s " % sep
	for n in range(parts.size() - 1, 0, -1):
		out.append(joiner.join(parts.slice(0, n)))
	return out


## The fit itself (tests call it directly). Returns the font size used (-1 = not laid out yet).
static func fit_line_now(l: Label) -> int:
	if not l.is_inside_tree() or l.size.x < 24.0:
		return -1
	var alts: Array = l.get_meta("coddy_fit_alts", [l.text])
	var font: Font = l.get_theme_font("font")
	var key := "%s|%d|%d" % ["\u001f".join(PackedStringArray(alts)), int(l.size.x), font.get_instance_id()]
	if l.get_meta("_line_key", "") == key and l.text == str(l.get_meta("_line_out", "")):
		return l.get_theme_font_size("font_size")
	var avail := l.size.x - 2.0
	var start: int = int(l.get_meta("coddy_fit_start", CARD_SUB_PX))   # a page title starts at Title 30
	for a in alts:
		var s := start
		while s > FIT_TEXT_MIN and _lines_w(font, a, s) > avail:
			s -= 1
		if _lines_w(font, a, s) <= avail:
			_line_set(l, a, s, false)
			l.set_meta("_line_key", key)
			return s
	_line_set(l, str(alts[alts.size() - 1]), FIT_TEXT_MIN, true)   # last resort: wrap on words (never a cut)
	l.set_meta("_line_key", key)
	return FIT_TEXT_MIN


## Widest line of a text (an alternative may hold an explicit "\n": the shop's policy tag on its own second line).
static func _lines_w(font: Font, text: String, size: int) -> float:
	var w := 0.0
	for part in text.split("\n"):
		w = maxf(w, font.get_string_size(part, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x)
	return w


static func _line_set(l: Label, text: String, size: int, wrap: bool) -> void:
	if l.text != text:
		l.text = text
	if l.get_theme_font_size("font_size") != size:
		l.add_theme_font_size_override("font_size", size)
	var mode := TextServer.AUTOWRAP_WORD_SMART if wrap else TextServer.AUTOWRAP_OFF
	if l.autowrap_mode != mode:
		l.autowrap_mode = mode
	l.clip_text = not wrap
	l.set_meta("coddy_line_wrapped", wrap)
	l.set_meta("_line_out", text)


## A HUD figure: icon + big value + caption under it. The value Label is named "Value", the caption "Caption".
static func stat(icon_name: String, value: String, caption: String, icon_role: String = "primary") -> HBoxContainer:
	var h := HBoxContainer.new()
	h.add_theme_constant_override("separation", 14)
	h.mouse_filter = Control.MOUSE_FILTER_IGNORE
	h.add_child(icon(icon_name, 34, icon_role))
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", -4)
	var val := label(value, "Value")
	val.name = "Value"
	v.add_child(val)
	if caption != "":
		var cap := label(caption, "CaptionMuted")
		cap.name = "Caption"
		v.add_child(cap)
	h.add_child(v)
	return h


static func progress(value: float, max_value: float = 1.0) -> ProgressBar:
	var pb := ProgressBar.new()
	pb.show_percentage = false
	pb.max_value = max_value
	pb.step = 0.0
	pb.value = value
	pb.custom_minimum_size = Vector2(0, 12)
	pb.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return pb


static func dots(count: int, current: int = 0) -> Control:
	var d: Control = StepDots.new()
	d.count = count
	d.current = current
	return d


## Centered icon + title + one line + optional action (lists with nothing in them yet).
static func empty_state(icon_name: String, title: String, body: String, action: String = "", on_action: Callable = Callable()) -> VBoxContainer:
	var v := VBoxContainer.new()
	v.alignment = BoxContainer.ALIGNMENT_CENTER
	v.add_theme_constant_override("separation", 12)
	var t := tile(icon_name, "accent", 96, true)
	t.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	v.add_child(t)
	var tl := label(title, "Title", true)
	tl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(tl)
	var bl := label(body, "CaptionMuted", true)
	bl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(bl)
	if action != "":
		var b := button(action, "tonal", "", on_action)
		b.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
		v.add_child(b)
	return v


## A full-rect dimming layer that eats touches (behind modals, the onboarding pager).
static func scrim(alpha: float = 0.66) -> ColorRect:
	var r := ColorRect.new()
	r.name = "Scrim"
	r.color = Color(c("scrim"), alpha)
	r.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	r.mouse_filter = Control.MOUSE_FILTER_STOP
	return r


## Portrait when the viewport is taller than wide (bottom sheets, stacked buttons) — components call this.
static func portrait(node: CanvasItem) -> bool:
	var s := node.get_viewport_rect().size if node.is_inside_tree() else Vector2(1280, 720)
	return s.y > s.x


## Lays a container tree out NOW (containers normally sort one frame later) and refreshes autowrap labels, so a
## component can measure its real content height in the same call: autowrap labels report the height for their LAST
## width and parents cache minimum sizes. Used by UiModal / UiCoach / UiPager before they size themselves.
static func settle(root: Control) -> void:
	root.propagate_notification(Container.NOTIFICATION_SORT_CHILDREN)
	for l in root.find_children("*", "Label", true, false):
		if (l as Label).autowrap_mode != TextServer.AUTOWRAP_OFF:
			(l as Label).update_minimum_size()
	root.update_minimum_size()


## Steps a label's font down until `text` fits `width` px on one line (never below the 12 sp caption = 21 px); returns
## the size used. For HUD values / tabs in long languages (de, ru) — instead of letting Godot shrink text to 9 px.
static func fit_font(l: Label, width: float, start: int, floor_px: int = 21) -> int:
	var f: Font = l.get_theme_font("font")
	var s := start
	while s > floor_px and f.get_string_size(l.text, HORIZONTAL_ALIGNMENT_LEFT, -1, s).x > width:
		s -= 1
	l.add_theme_font_size_override("font_size", s)
	return s


# ---------------------------------------------------------------- word fit (W64 #98)

## The floor of the word fit (Patron #98): a word that does not fit its button / chip steps the font down to 18 px.
const FIT_FLOOR := 18
## The fleet text minimum (12 sp caption, README §1): a clipped one-line button never goes below it, and a wrapping
## text only goes below it when one word still does not fit at 21 px.
const FIT_TEXT_MIN := 21

## Keeps a kit button / chip label whole-word (W64 #98, fleet finding de "Taschentuchsta/nd", ru "Бесплат/ный",
## de "Bienenkaste/n"): re-fits when its text, width or style changes (checked on redraw / resize — no per-frame work).
## Opt out with set_meta("coddy_no_fit", true).
static func watch_fit(c: Control) -> void:
	if c.has_meta("_fit_watched"):
		return
	c.set_meta("_fit_watched", true)
	c.resized.connect(func() -> void: refit(c, REFIT_WORD_FIT))
	c.draw.connect(func() -> void: refit_later(c, REFIT_WORD_FIT))


## Refits that follow a resize or a redraw (kit fix 3, 10-27; Corrupt State Z Fold7 crash): a redraw-triggered refit used
## call_deferred — the redraw itself runs inside the deferred-call flush, so a refit that changed the size or the wrapping
## queued its next redraw + refit into the SAME flush. A layout that flips back and forth then grew the queue without end
## ("Message queue out of memory", then CallQueue::statistics touched a freed object: SIGSEGV). Now:
## - a redraw schedules the refit on the NEXT frame (SceneTree.process_frame, one shot), once per control and kind;
## - at most REFIT_CAP refits that change something run per control per frame (a further one waits for the next frame);
## - a control whose refits alternate between two states (A, B, A, B within REFIT_WINDOW frames) keeps its current result
##   until its text, width or style changes to something new — an oscillating layout settles instead of looping;
## - a freed control is never touched (is_instance_valid), and leaving the tree drops its pending refit.
## Nothing runs per frame while nothing changes (a draw happens only after a change).
const REFIT_WORD_FIT := 0
const REFIT_LINE := 1
const REFIT_WORD_MIN := 2
const REFIT_CAP := 2
const REFIT_WINDOW := 3


static func refit_later(c: Control, kind: int) -> void:
	if not is_instance_valid(c) or not c.is_inside_tree():
		return
	var meta := "_rf_next%d" % kind
	var tree := c.get_tree()
	if c.has_meta(meta) and tree.process_frame.is_connected(c.get_meta(meta)):
		return   # already pending for the next frame
	# the id, not the control: a control freed earlier in the same process_frame emission (its tree_exiting disconnect
	# does not reach the emission's copied slot list) must not be a freed lambda capture
	var id := c.get_instance_id()
	var run := func() -> void:
		var n = instance_from_id(id)
		if n is Control:
			if n.has_meta(meta):
				n.remove_meta(meta)
			refit(n, kind)
	c.set_meta(meta, run)
	tree.process_frame.connect(run, CONNECT_ONE_SHOT)
	if not c.has_meta("_rf_exit"):
		c.set_meta("_rf_exit", true)
		c.tree_exiting.connect(func() -> void: _refit_drop(c))


static func _refit_drop(c: Control) -> void:
	if not is_instance_valid(c) or not c.is_inside_tree():
		return
	var tree := c.get_tree()
	for kind in [REFIT_WORD_FIT, REFIT_LINE, REFIT_WORD_MIN]:
		var meta := "_rf_next%d" % kind
		if c.has_meta(meta):
			var run: Callable = c.get_meta(meta)
			if tree.process_frame.is_connected(run):
				tree.process_frame.disconnect(run)
			c.remove_meta(meta)


## One guarded refit now (resize path; the next-frame path lands here too).
static func refit(c: Control, kind: int) -> void:
	if not is_instance_valid(c) or not c.is_inside_tree():
		return
	var frame := Engine.get_process_frames()
	var sig := _refit_state(c)
	if c.get_meta("_rf_hold", "") == sig:
		return   # settled on an oscillation: keep this result until the input changes
	if c.has_meta("_rf_hold"):
		c.remove_meta("_rf_hold")
	var hist: Array = c.get_meta("_rf_hist", [])
	if hist.size() == 3 and hist[1][1] == sig and hist[0][1] == hist[2][1] and hist[0][1] != sig \
			and frame - int(hist[0][0]) <= REFIT_WINDOW:
		c.set_meta("_rf_hold", sig)   # A, B, A and now B again: the layout flips back and forth
		return
	if int(c.get_meta("_rf_frame", -1)) != frame:
		c.set_meta("_rf_frame", frame)
		c.set_meta("_rf_n", 0)
	if int(c.get_meta("_rf_n", 0)) >= REFIT_CAP:
		refit_later(c, kind)   # a third change in one frame waits for the next frame
		return
	match kind:
		REFIT_WORD_FIT:
			word_fit(c)
		REFIT_LINE:
			fit_line_now(c as Label)
		REFIT_WORD_MIN:
			_word_min(c as Label)
	if not is_instance_valid(c) or _refit_state(c) == sig:
		return   # nothing changed (the key-cached common case): not counted
	c.set_meta("_rf_n", int(c.get_meta("_rf_n", 0)) + 1)
	hist.append([frame, sig])
	if hist.size() > 3:
		hist.pop_front()
	c.set_meta("_rf_hist", hist)


## What a refit reads and writes: text, width, style, font size, minimum width.
static func _refit_state(c: Control) -> String:
	return "%s|%d|%s|%d|%d" % [c.get("text"), int(c.size.x), c.theme_type_variation, c.get_theme_font_size("font_size"),
		int(c.custom_minimum_size.x)]


## True when a word that does not fit even at FIT_FLOOR may break between graphemes (Patron #98 follow-up): Devanagari
## and CJK only — a Latin / Cyrillic / Greek word is never split (its minimum width keeps it whole).
static func grapheme_breakable(word: String) -> bool:
	for ch in word:
		var u := ch.unicode_at(0)
		if (u >= 0x0900 and u <= 0x097F) or (u >= 0x3040 and u <= 0x30FF) or (u >= 0x3400 and u <= 0x9FFF) or (u >= 0xAC00 and u <= 0xD7AF):
			return true
	return false


## Word fit for a Button or Label:
## - wrapping (autowrap on): text wraps WORD_SMART; if its longest word is wider than the text area the font steps down
##   one px at a time to FIT_FLOOR, and the control's minimum width never lets a container squeeze it below that word
##   at the floor — so a word is never split mid-letter (CJK breaks between characters, as typeset).
## - one line + clip_text: the font steps down until the longest line fits (no clipped letters), floor FIT_FLOOR.
## - one line, no clip: the control grows with its text — nothing to do.
## The size a game sets itself (font_size override) is the start size; returns the size in use.
static func word_fit(c: Control) -> int:
	if c.has_meta("coddy_no_fit") or not (c is Button or c is Label):
		return -1
	if c.has_meta("_fit_busy"):
		return c.get_theme_font_size("font_size")   # our own override / minimum re-entered via resized
	c.set_meta("_fit_busy", true)
	var used := _word_fit(c)
	c.remove_meta("_fit_busy")
	return used


static func _word_fit(c: Control) -> int:
	if not c.is_inside_tree():
		return c.get_theme_font_size("font_size")   # the theme font is not resolved yet: never measure / cache with a stand-in
	var text: String = c.text
	var wrap: bool = c.autowrap_mode != TextServer.AUTOWRAP_OFF
	# the key carries the resolved font + size: a fit made before the theme reached the control (engine default
	# 16 px font) is redone once it arrives
	var font_id := c.get_theme_font("font").get_instance_id()
	var cur: int = c.get_theme_font_size("font_size")
	var key := "%s|%d|%s|%s|%s|%d|%d" % [text, int(c.size.x), c.theme_type_variation, wrap, c.clip_text, font_id, cur]
	var mine: int = c.get_meta("_fit_set", -1)
	if c.get_meta("_fit_key", "") == key and (mine < 0 or cur == mine):
		return cur
	var base: int = cur
	if mine >= 0 and cur == mine:
		base = int(c.get_meta("_fit_base", cur))
	c.set_meta("_fit_base", base)
	if text.strip_edges() == "" or (not wrap and not c.clip_text):
		_fit_apply(c, base, base)
		c.set_meta("_fit_key", key)
		return base
	var font: Font = c.get_theme_font("font")
	var chrome := _fit_chrome(c)
	var units: PackedStringArray = fit_words(text) if wrap else text.split("\n")
	var max_lines: int = int(c.get_meta("coddy_fit_lines", 0)) if wrap else 0
	if wrap:
		# the minimum width keeps the longest word whole at the 21 px text minimum (a11y gate); below it only when a
		# container cannot give that width
		var floor_need := ceilf(_widest(font, units, mini(base, FIT_TEXT_MIN)) + chrome + 1.0)
		if max_lines > 0:
			floor_need = maxf(floor_need, ceilf(lines_width(font, text, mini(base, FIT_TEXT_MIN), max_lines) + chrome + 1.0))
		var min0: float = float(c.get_meta("_fit_min0", c.custom_minimum_size.x))
		if absf(c.custom_minimum_size.x - float(c.get_meta("_fit_min_set", -1.0))) > 0.5:
			min0 = c.custom_minimum_size.x   # the game changed its own minimum since
		c.set_meta("_fit_min0", min0)
		var want := maxf(min0, floor_need)
		if absf(c.custom_minimum_size.x - want) > 0.5:
			c.custom_minimum_size.x = want
		c.set_meta("_fit_min_set", want)
	var avail := c.size.x - chrome
	if avail < 24.0:
		return cur   # not laid out yet (a container pass can hand out 8 px first): the final resize fits it
	var size := base
	var text_min := mini(base, FIT_TEXT_MIN)
	while size > text_min and (_widest(font, units, size) > avail or (max_lines > 0 and line_count(font, text, size, avail) > max_lines)):
		size -= 1
	while wrap and size > FIT_FLOOR and _widest(font, units, size) > avail:
		size -= 1   # only a single word that is wider than the text area even at 21 px goes below the text minimum
	var measured_width := c.size.x
	_fit_apply(c, base, size)
	c.set_meta("_fit_key", "%s|%d|%s|%s|%s|%d|%d" % [text, int(measured_width), c.theme_type_variation, wrap, c.clip_text, font_id, c.get_theme_font_size("font_size")])
	return size


## The unbreakable pieces of a text: space-separated words; CJK ideographs / kana stand alone (they may break anywhere).
static func fit_words(text: String) -> PackedStringArray:
	var out := PackedStringArray()
	for word in text.replace("\n", " ").split(" ", false):
		var run := ""
		for ch in word:
			var u := ch.unicode_at(0)
			if (u >= 0x3040 and u <= 0x30FF) or (u >= 0x3400 and u <= 0x9FFF) or (u >= 0xF900 and u <= 0xFAFF) or (u >= 0xFF00 and u <= 0xFFEF):
				if run != "":
					out.append(run)
					run = ""
				out.append(ch)
			else:
				run += ch
		if run != "":
			out.append(run)
	return out


## Lines `text` wraps into at `width` px, with the kit's wrap rules (WORD_SMART: between words; CJK between characters).
## Fit-time only (a key-cached fit on resize / redraw), never per frame.
static func line_count(font: Font, text: String, size: int, width: float) -> int:
	_para.clear()
	_para.break_flags = TextServer.BREAK_MANDATORY | TextServer.BREAK_WORD_BOUND | TextServer.BREAK_ADAPTIVE
	_para.add_string(text, font, size)
	_para.width = maxf(width, 1.0)
	return _para.get_line_count()


## The narrowest text width that keeps `text` within `lines` lines (never below its widest unbreakable piece).
static func lines_width(font: Font, text: String, size: int, lines: int) -> float:
	var lo := _widest(font, fit_words(text), size)
	if line_count(font, text, size, lo) <= lines:
		return lo
	var hi := _widest(font, text.split("\n"), size) + 1.0
	while hi - lo > 1.0:
		var mid := floorf((lo + hi) / 2.0)
		if line_count(font, text, size, mid) <= lines:
			hi = mid
		else:
			lo = mid
	return hi


static var _para := TextParagraph.new()


static func _widest(font: Font, units: PackedStringArray, size: int) -> float:
	var w := 0.0
	for u in units:
		w = maxf(w, font.get_string_size(u, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x)
	return w


## Horizontal space that is not text: the style's side margins (+ a button's icon slot and its gap).
static func _fit_chrome(c: Control) -> float:
	var sb: StyleBox = c.get_theme_stylebox("normal")
	var chrome := 0.0
	if sb != null:
		chrome += sb.get_content_margin(SIDE_LEFT) + sb.get_content_margin(SIDE_RIGHT)
	if c is Button and (c as Button).icon != null and c.text != "":
		var b := c as Button
		var iw := float(b.icon.get_width())
		var cap := b.get_theme_constant("icon_max_width")
		if cap > 0:
			iw = minf(iw, float(cap))
		chrome += iw + float(b.get_theme_constant("h_separation"))
	return chrome


static func _fit_apply(c: Control, base: int, size: int) -> void:
	var mine: int = c.get_meta("_fit_set", -1)
	if size == base and mine < 0:
		return
	c.set_meta("_fit_set", size)
	if c.get_theme_font_size("font_size") != size:
		c.add_theme_font_size_override("font_size", size)

