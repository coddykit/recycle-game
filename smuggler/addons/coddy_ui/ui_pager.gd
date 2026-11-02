extends Control
## coddy_ui onboarding pager (W64): 2–4 intro pages before the show-and-do guide (games/CLAUDE.md §9). Landscape: a
## centred card — illustration left, step dots + title + one or two short sentences + Skip / Next right. Portrait: a
## modal card (owner 2026-10-23) over the dimmed live game — 88 % of the width, ≤ 60 % of the height, near the bottom;
## illustration on top (Skip in its corner), dots, text centred, a big Next at the bottom. Never a scroll bar:
## the body is two lines in every language (tests check).
##   var pager := UiPager.new(); add_child(pager)
##   pager.setup([{"title": tr("Welcome"), "body": tr("…"), "icon": "ship", "sats": ["coins", "anchor"]}, …],
##               {"next": tr("Next"), "last": tr("Let's go!"), "skip": tr("Skip")})
##   pager.finished.connect(...); pager.skipped.connect(...)
## Page keys: title, body, icon, sats, tone, `image` (Texture2D or "res://…": a hero picture filling the illustration
## area, rounded cover; icon + satellites are not drawn then; missing / unloadable = the icon composition), `art` (older
## name of the same, Texture2D only).
## Node names (tests / guides): IntroCard (the card), IntroNext, IntroSkip, IntroDots, IntroTitle, IntroBody.
##
## Taps (owner 2026-10-23): ONE tap on the last page's button finishes — one press = one page or one `finished`, never
## two. The buttons never move while a page changes (only the art and the title / body animate) and are never disabled
## by a transition, so a tap during a transition lands where the finger is.
## Swipe left / right on the page = Next / back (never finishes; a game that runs its own swipe wins — the kit only moves
## when the page did not change during the gesture). A host that keeps its own page counter and holds this pager as
## `_pager` (the onboarding.gd games) gets that counter kept equal to the pager's page (see _sync_host).
## Motion (tweens only, Tokens.MOTION, nothing per frame at rest; UiKit.reduce_motion = snap): open = backdrop fades,
## card fades + settles from 96 %; page = art slides in from the side the page comes from (satellites swing in on their
## orbit), title then body slide + fade with a short stagger, dots widen + change colour, counter ticks; the last page's
## button becomes the call to action (play icon, one halo ripple + a small pop); a pressed Next dips to 96 %; close = the
## card fades out over the game (a short-lived, input-transparent copy) when the game hides / frees the pager.

signal page_changed(page: int)
signal finished
signal skipped

const Tokens = preload("ui_tokens.gd")
const Illustration = preload("ui_illustration.gd")
const StepDots = preload("ui_step_dots.gd")
const SWIPE := 72.0        ## px a finger must travel sideways (and mostly sideways) to turn the page
const SLIDE := 36.0        ## px the title / body travel in from the side
const PORTRAIT_W := 0.88       ## portrait modal card: share of the viewport width
const PORTRAIT_H := 0.60       ## … at most this share of the viewport height
const PORTRAIT_BOTTOM := 0.04  ## … bottom edge this share of the height above the bottom (≥ 40 px)
const PORTRAIT_ART := 0.62     ## … illustration height as a share of the card width (when the height allows)
const PORTRAIT_ART_MIN := 120.0
const PORTRAIT_ART_SHARE := 0.30   ## #105: the picture keeps ≥ 30 % of the card height, also in long languages
const PORTRAIT_H_LONG := 0.88      ## … for that the card may grow up to this share of the viewport height first,
const TEXT_MIN_PX := 21            ## … then title / body step down to this size (the W64 12 sp floor) — never cut off
const PORTRAIT_PAD_X := 36
const NEXT_ICON := "arrow-right"
const LAST_ICON := "play"

var pages: Array = []
var labels := {"next": "Next", "last": "Start", "skip": "Skip"}
var page := 0
var card: PanelContainer
var next_button: Button
var skip_button: Button
var title_label: Label
var body_label: Label
var dots: Control
var counter: Label
var art: Control
var portrait := false
var _room_key := 0
var _room := 0.0              ## portrait: reserved height of title + body (the longest page, measured)
var _pad: MarginContainer     ## portrait: dots + words + Next (its minimum height sizes the card)
var _text_col: VBoxContainer
var _slide: MarginContainer   ## title + body: slides in with negative / positive side margins (layout stays put)
var _shade: ColorRect
var _halo: Control
var _built_for := ""
var _swipe_from := Vector2.INF
var _swipe_page := -1
var _page_tw: Tween
var _open_tw: Tween
var _press_tw: Tween
var _cta_tw: Tween
var _icon_last := false
var _open_queued := false


func _init() -> void:
	name = "Pager"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP


func _ready() -> void:
	_queue_open()


func _notification(what: int) -> void:
	if what == NOTIFICATION_VISIBILITY_CHANGED and visible and is_inside_tree():
		_queue_open()


func setup(p_pages: Array, p_labels: Dictionary = {}) -> void:
	pages = p_pages
	for k in p_labels:
		labels[k] = p_labels[k]
	_build()
	show_page(clampi(page, 0, pages.size() - 1), false)


func relabel(p_pages: Array, p_labels: Dictionary) -> void:
	pages = p_pages
	for k in p_labels:
		labels[k] = p_labels[k]
	skip_button.text = str(labels.skip)
	show_page(page, false)


## Adds a page after setup (dots, counter and the last-page button follow at once).
func add_page(p: Dictionary) -> void:
	pages.append(p)
	show_page(page, false)


func _build() -> void:
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	portrait = kit.portrait(self)
	var key := "p" if portrait else "l"
	if _built_for == key:
		return
	_built_for = key
	_room_key = 0
	for ch in get_children():
		ch.queue_free()
	_shade = kit.scrim(0.72)
	_shade.name = "Shade"
	_shade.set_anchors_preset(Control.PRESET_TOP_LEFT)   # sized by hand to the whole screen (the pager may sit in a safe area)
	add_child(_shade)
	card = PanelContainer.new()
	card.name = "IntroCard"
	card.theme_type_variation = "Sheet"
	card.gui_input.connect(_card_input)
	art = Illustration.new()
	art.name = "IntroArt"
	dots = StepDots.new()
	dots.name = "IntroDots"
	dots.count = maxi(1, pages.size())
	title_label = kit.label("", "Display", true)
	title_label.name = "IntroTitle"
	body_label = kit.label("", "BodyMuted", true)
	body_label.name = "IntroBody"
	next_button = kit.button("", "primary", NEXT_ICON, next_page)
	next_button.button_down.connect(_press_feedback.bind(true))
	next_button.button_up.connect(_press_feedback.bind(false))
	next_button.name = "IntroNext"
	next_button.icon_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_icon_last = false
	_halo = _Halo.new()
	next_button.add_child(_halo)
	skip_button = kit.button(str(labels.skip), "ghost", "", skip)
	skip_button.name = "IntroSkip"
	_text_col = VBoxContainer.new()
	_text_col.name = "Text"
	_slide = MarginContainer.new()
	_slide.name = "Slide"
	var words := VBoxContainer.new()
	words.add_child(title_label)
	words.add_child(body_label)
	_slide.add_child(words)
	for side in ["left", "right", "top", "bottom"]:
		_slide.add_theme_constant_override("margin_" + side, 0)
	if portrait:
		_build_portrait(kit)
	else:
		_build_landscape(kit)
	words.add_theme_constant_override("separation", _text_col.get_theme_constant("separation"))
	if not resized.is_connected(_on_resized):
		resized.connect(_on_resized)


func _build_landscape(kit) -> void:
	add_child(card)
	var sb: StyleBoxFlat = (card.get_theme_stylebox("panel") as StyleBoxFlat).duplicate()
	sb.content_margin_left = 0
	sb.content_margin_top = 0
	sb.content_margin_right = 0
	sb.content_margin_bottom = 0
	card.add_theme_stylebox_override("panel", sb)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 0)
	card.add_child(row)
	var r := float(Tokens.RADIUS.xl)
	sb.set_corner_radius_all(int(r))
	art.corner_radius = Vector4(r, 0, 0, r)
	art.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	art.size_flags_stretch_ratio = 0.72
	row.add_child(art)
	var pad := MarginContainer.new()
	pad.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	pad.add_theme_constant_override("margin_left", 52)
	pad.add_theme_constant_override("margin_right", 52)
	pad.add_theme_constant_override("margin_top", 44)
	pad.add_theme_constant_override("margin_bottom", 40)
	row.add_child(pad)
	_text_col.add_theme_constant_override("separation", 16)
	pad.add_child(_text_col)
	var top := HBoxContainer.new()
	top.add_child(dots)
	var gap := Control.new()
	gap.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	top.add_child(gap)
	counter = kit.label("", "CaptionMuted")
	top.add_child(counter)
	_text_col.add_child(top)
	var s1 := Control.new()
	s1.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_text_col.add_child(s1)
	_text_col.add_child(_slide)
	var s2 := Control.new()
	s2.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_text_col.add_child(s2)
	var bottom := HBoxContainer.new()
	bottom.add_child(skip_button)
	var gap2 := Control.new()
	gap2.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bottom.add_child(gap2)
	next_button.custom_minimum_size.x = 240
	bottom.add_child(next_button)
	_text_col.add_child(bottom)


## Portrait = a modal card over the dimmed live game (owner 2026-10-23): ~88 % of the width, at most 60 % of the height,
## near the bottom, the scrim covers the whole screen. Illustration on top (Skip over its corner), then dots, title +
## body (room reserved for the longest page so nothing jumps), the big Next. The card is sized in _place_portrait().
func _build_portrait(kit) -> void:
	add_child(card)   # theme "Sheet" (rounded xl card), padding handled by the rows below
	var sb: StyleBoxFlat = (card.get_theme_stylebox("panel") as StyleBoxFlat).duplicate()
	sb.content_margin_left = 0
	sb.content_margin_top = 0
	sb.content_margin_right = 0
	sb.content_margin_bottom = 0
	sb.set_corner_radius_all(Tokens.RADIUS.xl)
	card.add_theme_stylebox_override("panel", sb)
	card.clip_contents = true
	var col := VBoxContainer.new()
	col.add_theme_constant_override("separation", 0)
	card.add_child(col)
	var r := float(Tokens.RADIUS.xl)
	art.corner_radius = Vector4(r, r, 0, 0)
	art.custom_minimum_size.y = PORTRAIT_ART_MIN
	col.add_child(art)
	_pad = MarginContainer.new()
	_pad.name = "Pad"
	_pad.add_theme_constant_override("margin_left", PORTRAIT_PAD_X)
	_pad.add_theme_constant_override("margin_right", PORTRAIT_PAD_X)
	_pad.add_theme_constant_override("margin_top", 22)
	_pad.add_theme_constant_override("margin_bottom", 26)
	col.add_child(_pad)
	_text_col.add_theme_constant_override("separation", 16)
	_pad.add_child(_text_col)
	dots.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	_text_col.add_child(dots)
	title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	body_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_slide.size_flags_vertical = Control.SIZE_EXPAND_FILL
	(_slide.get_child(0) as Control).size_flags_vertical = Control.SIZE_SHRINK_CENTER   # the words sit centred in the reserved room
	_text_col.add_child(_slide)
	next_button.custom_minimum_size.y = Tokens.TOUCH.comfortable
	next_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_text_col.add_child(next_button)
	add_child(skip_button)
	counter = null


func _on_resized() -> void:
	if not is_inside_tree():
		return   # #100: a resize while the pager leaves the tree (closing / quit) has no viewport to measure
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	if kit.portrait(self) != portrait:
		_build()
		show_page(page, false)
	_place()


func _place() -> void:
	if card == null or not is_inside_tree():
		return   # #100: get_viewport_rect() / global_position need the tree (C2: closing error, verify exit 1)
	_shade.global_position = Vector2.ZERO
	_shade.size = get_viewport_rect().size
	if portrait:
		_place_portrait()
	else:
		var w := minf(1120.0, size.x - 48.0)
		var h := minf(540.0, size.y - 40.0)
		card.size = Vector2(w, 0)
		card.size = Vector2(w, maxf(h, card.get_combined_minimum_size().y))
		card.position = ((size - card.size) / 2.0).round()
	card.pivot_offset = card.size / 2.0
	_balance_words()


## The portrait modal card: width 88 % of the viewport (≤ 900 px), height = what its content needs (art fills up to ~62 %
## of the width) but ≤ 60 % of the viewport and never above the top of the pager; bottom edge = 4 % of the height (≥ 40 px)
## above the bottom of the viewport / of the pager (a game that puts the pager in its safe area keeps its inset).
func _place_portrait() -> void:
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	var vp := get_viewport_rect()
	var w := minf(vp.size.x * PORTRAIT_W, 900.0)
	var margin := maxf(40.0, vp.size.y * PORTRAIT_BOTTOM)
	var bottom := minf(vp.end.y, global_position.y + size.y) - margin
	var max_h := minf(vp.size.y * PORTRAIT_H, bottom - vp.position.y)
	art.custom_minimum_size.y = PORTRAIT_ART_MIN
	_unshrink_words()   # every layout starts at the size the game / theme gave the words (#105)
	card.size = Vector2(w, 0)
	kit.settle(card)
	kit.settle(card)
	_slide.custom_minimum_size.y = _words_room(kit, w - 2.0 * PORTRAIT_PAD_X)
	kit.settle(card)
	var rest := _pad.get_combined_minimum_size().y   # dots + words + Next + paddings
	# #105 (S3 finding: es / fr / ru on 720x1280 left the picture a ~120 px strip under the Skip button): the picture keeps
	# ≥ PORTRAIT_ART_SHARE of the card. A long text first lets the card grow (up to PORTRAIT_H_LONG of the screen), then
	# steps the title / body font down (to TEXT_MIN_PX); the words are never cut.
	var long_h := minf(vp.size.y * PORTRAIT_H_LONG, bottom - vp.position.y - _skip_room())
	var floor_art := maxf(PORTRAIT_ART_MIN, rest * PORTRAIT_ART_SHARE / (1.0 - PORTRAIT_ART_SHARE))
	if rest + floor_art > long_h:
		rest = _shrink_words(kit, w, long_h)
		floor_art = maxf(PORTRAIT_ART_MIN, rest * PORTRAIT_ART_SHARE / (1.0 - PORTRAIT_ART_SHARE))
	var cap := maxf(max_h, minf(long_h, rest + floor_art))
	var art_h := clampf(w * PORTRAIT_ART, floor_art, maxf(floor_art, cap - rest))
	art.custom_minimum_size.y = art_h
	card.size = Vector2(w, rest + art_h)
	kit.settle(card)
	var gpos := Vector2(vp.position.x + (vp.size.x - w) / 2.0, bottom - card.size.y).round()
	card.position = gpos - global_position
	# Skip sits ABOVE the card (right-aligned), off the picture; with no room above it, inside the card's top-right corner
	var skip_w := maxf(skip_button.size.x, skip_button.get_combined_minimum_size().x)
	var skip_h := maxf(skip_button.size.y, skip_button.get_combined_minimum_size().y)
	if gpos.y - skip_h - 12.0 >= vp.position.y + 8.0:
		skip_button.position = card.position + Vector2(card.size.x - skip_w, -skip_h - 12.0)
	else:
		skip_button.position = card.position + Vector2(card.size.x - skip_w - 16.0, 16.0)


## Height kept free above the card for the Skip button.
func _skip_room() -> float:
	return maxf(skip_button.size.y, skip_button.get_combined_minimum_size().y) + 20.0


var _shrunk: Array = []   ## [[label, had_override, size]] restored by _unshrink_words


func _unshrink_words() -> void:
	for e in _shrunk:
		var l: Label = e[0]
		if not is_instance_valid(l):
			continue
		if e[1]:
			l.add_theme_font_size_override("font_size", e[2])
		else:
			l.remove_theme_font_size_override("font_size")
	if not _shrunk.is_empty():
		_shrunk.clear()
		_room_key = 0


## Steps the title / body font size down (2 px at a time, not below TEXT_MIN_PX) until the words + picture floor fit
## `limit`; returns the new height of everything but the picture.
func _shrink_words(kit, w: float, limit: float) -> float:
	var tsize := title_label.get_theme_font_size("font_size")
	var bsize := body_label.get_theme_font_size("font_size")
	if _shrunk.is_empty():
		_shrunk = [[title_label, title_label.has_theme_font_size_override("font_size"), tsize],
			[body_label, body_label.has_theme_font_size_override("font_size"), bsize]]
	var rest := _pad.get_combined_minimum_size().y
	while (tsize > TEXT_MIN_PX or bsize > TEXT_MIN_PX):
		tsize = maxi(TEXT_MIN_PX, tsize - 2)
		bsize = maxi(TEXT_MIN_PX, bsize - 2)
		title_label.add_theme_font_size_override("font_size", tsize)
		body_label.add_theme_font_size_override("font_size", bsize)
		_room_key = 0
		_slide.custom_minimum_size.y = _words_room(kit, w - 2.0 * PORTRAIT_PAD_X)
		kit.settle(card)
		rest = _pad.get_combined_minimum_size().y
		if rest + maxf(PORTRAIT_ART_MIN, rest * PORTRAIT_ART_SHARE / (1.0 - PORTRAIT_ART_SHARE)) <= limit:
			break
	return rest


## Height the title + body need on the LONGEST page at the card's real width, measured with the REAL labels (the game's
## font, its wrapping, line spacing) after a layout pass — so the card never changes size between pages. Each page's text
## is put in the labels, the column is laid out, the words' minimum height is read; the current page's text comes back.
## Cached per (width, texts, fonts): a page change only measures once.
func _words_room(kit, width: float) -> float:
	var key := hash([width, pages.map(func(pg: Dictionary) -> String: return str(pg.get("title", "")) + "\n" + str(pg.get("body", ""))),
		title_label.get_theme_font("font"), title_label.get_theme_font_size("font_size"),
		body_label.get_theme_font("font"), body_label.get_theme_font_size("font_size")])
	if key == _room_key:
		return _room
	var t := title_label.text
	var b := body_label.text
	var words := _slide.get_child(0) as Control
	_slide.custom_minimum_size.y = 0.0
	var room := 0.0
	for pg in pages:
		title_label.text = str(pg.get("title", ""))
		body_label.text = str(pg.get("body", ""))
		kit.settle(card)
		kit.settle(card)
		room = maxf(room, words.get_combined_minimum_size().y)
	title_label.text = t
	body_label.text = b
	kit.settle(card)
	_room_key = key
	_room = ceilf(room)
	return _room


## No lone character on the last line (Patron 10-25, ja card 1 "…ま / す"): CJK wraps between any two characters, so a
## title / body can end with one glyph alone. When it would, the label is narrowed (centred, its text untouched) to the
## widest width that keeps the same line count and gives the last line two or more characters — the measured card height
## stays. Runs on every layout from the column's full width (a resize re-balances).
func _balance_words() -> void:
	for l in [title_label, body_label]:
		var col := (l as Label).get_parent() as Control
		if col == null:
			continue
		var bw := _balanced_width(l, l.text, col.size.x)
		var flag := Control.SIZE_FILL
		if bw > 0.0:
			flag = Control.SIZE_SHRINK_CENTER if l.horizontal_alignment == HORIZONTAL_ALIGNMENT_CENTER else Control.SIZE_SHRINK_BEGIN
		if l.size_flags_horizontal != flag:
			l.size_flags_horizontal = flag
		if l.custom_minimum_size.x != bw:
			l.custom_minimum_size.x = bw


const _TAIL_MARKS := "。、！？!?．.，,」』）)〉》・…ー"


## 0 = the text wraps fine at `w`; else the narrower width at which its last line is no longer a lone character.
static func _balanced_width(l: Label, text: String, w: float) -> float:
	if text.length() < 4 or w < 40.0:
		return 0.0
	var lines := _wrap(l, text, w)
	if lines.size() < 2 or not _lone(text, lines[-1]):
		return 0.0
	var at: int = (lines[-1] as Vector2i).x
	if at > 0 and text[at - 1] == " ":
		return 0.0   # wrapped at a space: a one-letter last word is a real word (Latin), leave it
	var t := w
	while t > w * 0.6:
		t -= 4.0
		var try := _wrap(l, text, t)
		if try.size() != lines.size():
			return 0.0   # no narrower width keeps the line count: leave it
		if not _lone(text, try[-1]):
			return ceilf(t)
	return 0.0


static func _wrap(l: Label, text: String, w: float) -> Array:
	var para := TextParagraph.new()
	para.width = w
	para.break_flags = TextServer.BREAK_MANDATORY | TextServer.BREAK_WORD_BOUND | TextServer.BREAK_ADAPTIVE
	para.add_string(text, l.get_theme_font("font"), l.get_theme_font_size("font_size"), TranslationServer.get_locale())
	var out := []
	for i in para.get_line_count():
		out.append(para.get_line_range(i))
	return out


static func _lone(text: String, line: Vector2i) -> bool:
	var glyphs := 0
	for ch in text.substr(line.x, line.y - line.x).strip_edges():
		if not ch in _TAIL_MARKS:
			glyphs += 1
	return glyphs <= 1


func _motion() -> bool:
	return is_inside_tree() and is_visible_in_tree() and not load("res://addons/coddy_ui/ui_kit.gd").reduce_motion


func show_page(i: int, animate: bool = true) -> void:
	if pages.is_empty():
		return
	var from := page
	page = clampi(i, 0, pages.size() - 1)
	var pg: Dictionary = pages[page]
	title_label.text = str(pg.get("title", ""))
	body_label.text = str(pg.get("body", ""))
	var last := page == pages.size() - 1
	next_button.text = str(labels.last if last else labels.next)
	_set_cta(last, animate and from != page)
	_set_skip(not last, animate)
	skip_button.text = str(labels.skip)
	dots.count = pages.size()    # always the real page count (a page added / relabelled after setup included)
	dots.current = page
	var dir := signi(page - from)
	if counter != null:
		counter.text = "%d / %d" % [page + 1, pages.size()]
	var picture := _picture(pg)
	# A real hero can be pale or busy: the overlaid portrait Skip needs an opaque kit surface.
	skip_button.theme_type_variation = "SecondaryButton" if portrait and picture != null else "GhostButton"
	art.show_page(str(pg.get("icon", "sparkles")), pg.get("sats", []), str(pg.get("tone", "accent")), picture, animate, dir)
	_place()
	_place.call_deferred()
	_text_in(dir, from != page, animate)
	page_changed.emit(page)
	_sync_host()


## The page's real picture: `image` (a Texture2D or a "res://…" path; owner's onboarding v2 hero frames) wins over the
## older `art`. Unusable (missing path, not a texture) = null → the icon + satellites composition stays as the fallback.
func _picture(pg: Dictionary) -> Texture2D:
	for key in ["image", "art"]:
		var v: Variant = pg.get(key, null)
		if v is Texture2D:
			return v
		if v is String and not (v as String).is_empty() and ResourceLoader.exists(v):
			var res := load(v)
			if res is Texture2D:
				return res
	return null


## Title then body slide in from the side the page comes from (+ fade, a short stagger); the counter ticks.
func _text_in(dir: int, changed: bool, animate: bool) -> void:
	if _page_tw != null:
		_page_tw.kill()
		_page_tw = null
	_set_slide(0.0)
	title_label.modulate.a = 1.0
	body_label.modulate.a = 1.0
	if counter != null:
		counter.modulate.a = 1.0
	if not animate or not _motion():
		return
	var dx := SLIDE * (dir if dir != 0 else 1) * (-1.0 if is_layout_rtl() else 1.0)
	_set_slide(dx)
	title_label.modulate.a = 0.0
	body_label.modulate.a = 0.0
	_page_tw = create_tween().set_parallel(true).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	_page_tw.tween_method(_set_slide, dx, 0.0, Tokens.MOTION.slow).set_delay(0.03)
	_page_tw.tween_property(title_label, "modulate:a", 1.0, Tokens.MOTION.base).set_delay(0.03)
	_page_tw.tween_property(body_label, "modulate:a", 1.0, Tokens.MOTION.base).set_delay(0.09)
	if counter != null and changed:
		counter.modulate.a = 0.35
		_page_tw.tween_property(counter, "modulate:a", 1.0, Tokens.MOTION.base)


## A host written as "my own page counter + pager.finished → my next_page()" (the onboarding.gd games: canal-control,
## chaiwala-rush, pharmacy-rush, …) stepped its counter only when `finished` came, so the last page needed two taps and
## the first `finished` sent the pager back a page (owner 2026-10-23: "son buton 2 kere", "4 sayfa varken 3 nokta").
## The kit keeps such a host's `page` equal to its own, after page_changed (hosts that track it themselves see the change).
func _sync_host() -> void:
	var host := get_parent()
	if host != null and host.get("_pager") == self and typeof(host.get("page")) == TYPE_INT and host.get("page") != page:
		host.set("page", page)


## Title + body offset (px, + = right) through the side margins: the box keeps its width and the layout never moves.
func _set_slide(x: float) -> void:
	if _slide == null:
		return
	_slide.add_theme_constant_override("margin_left", int(round(x)))
	_slide.add_theme_constant_override("margin_right", int(round(-x)))


func _set_cta(last: bool, animate: bool) -> void:
	if last != _icon_last:
		_icon_last = last
		var Icons = load("res://addons/coddy_ui/ui_icons.gd")
		next_button.icon = Icons.texture(LAST_ICON if last else NEXT_ICON, 30, Color.WHITE)
	if _cta_tw != null:
		_cta_tw.kill()
		_cta_tw = null
	next_button.scale = Vector2.ONE
	_halo.ring = 0.0
	if last and animate and _motion():
		# the end is clear: one halo ripple out of the button and a small pop, once
		next_button.pivot_offset = next_button.size / 2.0
		_cta_tw = create_tween().set_parallel(true)
		_cta_tw.tween_property(_halo, "ring", 1.0, 0.7).set_delay(Tokens.MOTION.base).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
		_cta_tw.tween_property(next_button, "scale", Vector2.ONE * 1.045, Tokens.MOTION.fast).set_delay(Tokens.MOTION.base).set_ease(Tween.EASE_OUT)
		_cta_tw.chain().tween_property(next_button, "scale", Vector2.ONE, Tokens.MOTION.slow).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_BACK)


## Skip leaves at once on the last page (its copy fades where it stood) and fades back in on the way back.
func _set_skip(on: bool, animate: bool) -> void:
	if on == skip_button.visible:
		return
	var moving := animate and _motion()
	if not on and moving:
		_fade_copy(skip_button)
	skip_button.visible = on
	skip_button.modulate.a = 1.0
	if on and moving:
		skip_button.modulate.a = 0.0
		create_tween().tween_property(skip_button, "modulate:a", 1.0, Tokens.MOTION.base).set_delay(Tokens.MOTION.fast)


## An input-transparent copy of `c` that fades out where `c` stood (the original is already gone / hidden).
func _fade_copy(c: Control) -> void:
	var copy := c.duplicate(0) as Control
	_inert(copy)
	add_child(copy)
	copy.global_position = c.global_position
	copy.size = c.size
	var tw := copy.create_tween()
	tw.tween_property(copy, "modulate:a", 0.0, Tokens.MOTION.fast)
	tw.tween_callback(copy.queue_free)


static func _inert(n: Node) -> void:
	if n is Control:
		(n as Control).mouse_filter = Control.MOUSE_FILTER_IGNORE
		(n as Control).focus_mode = Control.FOCUS_NONE
	for ch in n.get_children():
		_inert(ch)


func _press_feedback(down: bool) -> void:
	if not _motion():
		return
	if _press_tw != null:
		_press_tw.kill()
	next_button.pivot_offset = next_button.size / 2.0
	_press_tw = create_tween().set_ease(Tween.EASE_OUT)
	if down:
		_press_tw.tween_property(next_button, "scale", Vector2.ONE * 0.96, Tokens.MOTION.fast * 0.6)
	else:
		_press_tw.set_trans(Tween.TRANS_BACK).tween_property(next_button, "scale", Vector2.ONE, Tokens.MOTION.base)


func next_page() -> void:
	if pages.is_empty():
		return
	if page < pages.size() - 1:
		show_page(page + 1)
	else:
		_finish()


func _finish() -> void:
	var copy := _close_copy()
	finished.emit()
	_leave(copy)


func skip() -> void:
	var copy := _close_copy()
	skipped.emit()
	_leave(copy)


## The close fade: a copy of the backdrop + card, made BEFORE the game reacts (it may hide, free or rebuild the pager).
func _close_copy() -> Control:
	if card == null or not _motion() or get_parent() == null:
		return null
	var ghost := Control.new()
	ghost.name = "PagerClosing"
	ghost.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ghost.position = position
	ghost.size = size
	var shade := _shade.duplicate(0) as Control
	var c := card.duplicate(Node.DUPLICATE_SCRIPTS) as Control
	var a := c.find_child("IntroArt", true, false)
	if a != null:   # the copy's illustration draws the page it showed (its script fields are not copied)
		for f in ["icon_name", "satellites", "tone", "art", "corner_radius"]:
			a.set(f, art.get(f))
	for n in c.find_children("Halo", "", true, false):
		n.queue_free()
	ghost.add_child(shade)
	ghost.add_child(c)
	_inert(ghost)
	c.position = card.position
	c.size = card.size
	c.pivot_offset = card.size / 2.0
	c.modulate = card.modulate
	c.scale = card.scale
	shade.size = _shade.size
	shade.modulate = _shade.modulate
	return ghost


## Shows the close fade only when the game took the pager away (hidden / freed / out of the tree); else drops it.
func _leave(ghost: Control) -> void:
	if ghost == null:
		return
	var host := get_parent()
	var gone := not is_inside_tree() or not is_visible_in_tree() or is_queued_for_deletion()
	if not gone or host == null or not host.is_inside_tree() or not _shown(host):
		ghost.queue_free()
		return
	host.add_child(ghost)
	host.move_child(ghost, mini(get_index() + 1, host.get_child_count() - 1))
	(ghost.get_child(0) as Control).global_position = Vector2.ZERO
	var c := ghost.get_child(1) as Control
	var tw := ghost.create_tween().set_parallel(true).set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_CUBIC)
	tw.tween_property(ghost, "modulate:a", 0.0, Tokens.MOTION.base)
	tw.tween_property(c, "scale", Vector2.ONE * 0.97, Tokens.MOTION.base)
	tw.chain().tween_callback(ghost.queue_free)


## Visible in the tree for any parent: a CanvasItem asks the tree; a CanvasLayer (a game hosting the pager on its own
## layer, airport-baron) has no is_visible_in_tree() — its own `visible` decides.
static func _shown(n: Node) -> bool:
	if n is CanvasItem:
		return (n as CanvasItem).is_visible_in_tree()
	if n is CanvasLayer:
		return (n as CanvasLayer).visible
	return true


func open() -> bool:
	return visible


## Opening: the backdrop fades in, the card fades in and settles from 96 % (centre-anchored: it never leaves its rect).
func _queue_open() -> void:
	if _open_queued:
		return
	_open_queued = true
	_play_open.call_deferred()


func _play_open() -> void:
	_open_queued = false
	if card == null or not _motion():
		return
	if _open_tw != null:
		_open_tw.kill()
	_place()
	_shade.modulate.a = 0.0
	card.modulate.a = 0.0
	card.scale = Vector2.ONE * 0.96
	_open_tw = create_tween().set_parallel(true).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	_open_tw.tween_property(_shade, "modulate:a", 1.0, Tokens.MOTION.base)
	_open_tw.tween_property(card, "modulate:a", 1.0, Tokens.MOTION.base).set_delay(0.04)
	_open_tw.tween_property(card, "scale", Vector2.ONE, Tokens.MOTION.slow + 0.08).set_delay(0.04)
	if not pages.is_empty():   # the first page's art + text come in with the card (no page_changed: the page is the same)
		var pg: Dictionary = pages[page]
		art.show_page(str(pg.get("icon", "sparkles")), pg.get("sats", []), str(pg.get("tone", "accent")), _picture(pg), true, 0)
		_text_in(0, false, true)


## True while anything of the pager still moves (tests: every transition ends; nothing animates at rest).
func animating() -> bool:
	for tw in [_page_tw, _open_tw, _cta_tw, _press_tw]:
		if tw != null and tw.is_valid() and tw.is_running():
			return true
	return art != null and art.animating()


## Swipe on the page (the card takes the finger: art and text pass it through). Reads the phone's emulated mouse copy of
## the finger only, so a touch is never counted twice; touch events themselves are left to the game.
func _card_input(event: InputEvent) -> void:
	var mb := event as InputEventMouseButton
	if mb == null or mb.button_index != MOUSE_BUTTON_LEFT:
		return
	if mb.pressed:
		_swipe_from = mb.position
		_swipe_page = page
		return
	if _swipe_from == Vector2.INF:
		return
	var d := mb.position - _swipe_from
	_swipe_from = Vector2.INF
	if absf(d.x) < SWIPE or absf(d.x) < absf(d.y) * 1.5:
		return
	var forward := d.x < 0.0
	if is_layout_rtl():
		forward = not forward
	_swipe.call_deferred(forward, _swipe_page)


func _swipe(forward: bool, from: int) -> void:
	if page != from or not is_visible_in_tree():
		return   # the game turned the page itself during this gesture (its own swipe): never twice
	if forward and page < pages.size() - 1:
		show_page(page + 1)
	elif not forward and page > 0:
		show_page(page - 1)


## The last page's call-to-action ripple: a rounded ring that grows out of the button and fades (drawn behind it, only
## while `ring` runs 0 → 1).
class _Halo extends Control:
	var ring := 0.0:
		set(v):
			ring = v
			queue_redraw()
	var _sb: StyleBoxFlat

	func _init() -> void:
		name = "Halo"
		mouse_filter = Control.MOUSE_FILTER_IGNORE
		show_behind_parent = true
		set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)

	func _draw() -> void:
		if ring <= 0.0 or ring >= 1.0:
			return
		var kit = load("res://addons/coddy_ui/ui_kit.gd")
		var grow := 4.0 + 22.0 * ring
		if _sb == null:
			_sb = StyleBoxFlat.new()
			_sb.draw_center = false
			_sb.anti_aliasing = true
		var sb := _sb
		var base := get_parent().get_theme_stylebox("normal") as StyleBoxFlat
		var rad := (base.corner_radius_top_left if base != null else 20) + int(grow)
		sb.set_corner_radius_all(rad)
		sb.set_border_width_all(maxi(1, int(round(4.0 * (1.0 - ring)))) + 1)
		sb.border_color = Color(kit.c("primary"), 0.7 * (1.0 - ring))
		draw_style_box(sb, Rect2(Vector2.ONE * -grow, size + Vector2.ONE * grow * 2.0))
