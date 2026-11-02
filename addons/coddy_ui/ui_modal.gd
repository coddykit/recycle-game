extends Control
## coddy_ui modal: a dialog card (landscape) or a bottom sheet (portrait) over a scrim. The card is as tall as its content
## (no more empty 600 px boxes, W64 audit), at most ~88 % of the screen; a taller body scrolls inside the card (finger
## scrolling, fading thumb) while the header (icon, title, close) and the action row stay put.
##   var m := UiModal.new(); content.add_child(m)
##   m.setup(tr("Settings"), {"icon": "settings", "width": 1180, "close": true})
##   m.body.add_child(...)                          # or: var cols := m.columns(2) for a two-column landscape layout
##   m.add_action(tr("Continue"), "primary", close_fn, "Continue")
##   m.close_requested.connect(close_fn)            # X button, scrim tap, Android Back (UiBack, ui_back.gd)
## Android Back (UiBack): opts "back" / `on_back` (Callable) runs instead (a window without a close: its Continue /
## Cancel); otherwise close_requested when the window has a close; a window with neither keeps Back (never quits).
## Node layout (tests rely on it): child 0 = Scrim, child 1 = Card (PanelContainer) → Column → Header / BodyScroll / Actions.

signal close_requested

const Tokens = preload("ui_tokens.gd")
const Scroll = preload("ui_scroll.gd")
const UiSafeArea = preload("ui_safe_area.gd")

var body: VBoxContainer
var card: PanelContainer
var header: HBoxContainer
var title_label: Label
var close_button: Button
var actions: BoxContainer
var scroll: ScrollContainer
var sheet := false                 ## bottom sheet (portrait) instead of a centred card
const COLUMN_MIN := 420.0          ## columns(n) stacks below this column width
var width := 760.0                 ## card width in landscape (clamped to the screen)
var margin := 24.0                 ## px kept free around the card
var max_fraction := 0.9            ## of the available height
var _column: VBoxContainer
var _actions_rule: HSeparator
var _safe_gap: Control             ## sheet: keeps the actions above the navigation bar (background runs on to the edge)
var _opts := {}
var on_back := Callable()          ## Android Back / Esc on this window (opts "back"; a game may set it after setup)
var _laying := false
var _in_layout := false
var _last_key := ""
var _col_rows: Array = []          ## [row, gap] of every columns() call: re-fitted on each layout pass


func _init() -> void:
	name = "Modal"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP


## opts: icon (Lucide name), icon_kind (tile colour role), width (px), close (bool: X button + scrim tap),
## sheet ("auto" | true | false), scrim (alpha), subtitle (text under the title), low (Low tier: flat close glyph),
## back (Callable: what Android Back / Esc does, see back()).
func setup(title: String, opts: Dictionary = {}) -> void:
	_opts = opts
	on_back = opts.get("back", Callable())
	add_to_group("coddy_back")   # UiBack.GROUP: Android Back reaches the top open window first
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	width = float(opts.get("width", width))
	var want_sheet = opts.get("sheet", "auto")
	sheet = kit.portrait(self) if str(want_sheet) == "auto" else bool(want_sheet)
	var shade: ColorRect = kit.scrim(float(opts.get("scrim", 0.66)))
	shade.gui_input.connect(func(ev: InputEvent) -> void:
		# a phone tap is ONE close (UiKit.is_tap drops Godot's emulated click; W66 10-27) — close_requested fired twice
		if bool(_opts.get("close", false)) and kit.is_tap(ev):
			close_requested.emit())
	add_child(shade)
	card = PanelContainer.new()
	card.name = "Card"
	card.theme_type_variation = "SheetBottom" if sheet else "Sheet"
	add_child(card)
	_column = VBoxContainer.new()
	_column.name = "Column"
	_column.add_theme_constant_override("separation", 20)
	card.add_child(_column)
	if sheet and int(kit.design) >= 2:
		# 2026-10-26: the grab handle is a rounded pill (one stylebox, same single draw as the old square bar)
		var grab := PanelContainer.new()
		grab.name = "Grabber"
		grab.theme_type_variation = "Grabber"
		grab.custom_minimum_size = Vector2(64, 6)
		grab.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
		grab.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_column.add_child(grab)
	elif sheet:
		var grab_v1 := ColorRect.new()
		grab_v1.name = "Grabber"
		grab_v1.color = kit.c("outline")
		grab_v1.custom_minimum_size = Vector2(72, 8)
		grab_v1.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
		grab_v1.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_column.add_child(grab_v1)
	header = HBoxContainer.new()
	header.name = "Header"
	header.add_theme_constant_override("separation", 18)
	_column.add_child(header)
	if str(opts.get("icon", "")) != "":
		var t: TextureRect = kit.tile(str(opts.icon), str(opts.get("icon_kind", "primary")), 64, bool(opts.get("icon_round", false)))
		t.name = "Icon"
		header.add_child(t)
	var titles := VBoxContainer.new()
	titles.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	titles.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	titles.add_theme_constant_override("separation", 2)
	title_label = kit.label(title, "Headline", true)
	title_label.name = "Title"
	titles.add_child(title_label)
	if str(opts.get("subtitle", "")) != "":
		titles.add_child(kit.label(str(opts.subtitle), "CaptionMuted", true))
	header.add_child(titles)
	if bool(opts.get("close", false)):
		# the fleet close affordance (2026-10-26): top-right, centred on the title row, a round chip (Low: the glyph)
		if int(kit.design) >= 2:
			close_button = kit.close_button(func() -> void: close_requested.emit(), str(opts.get("close_label", "")), bool(opts.get("low", false)))
		else:
			close_button = kit.icon_button("x", func() -> void: close_requested.emit(), str(opts.get("close_label", "")), true)
			close_button.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
		close_button.name = "Close"
		header.add_child(close_button)
	var parts: Array = Scroll.list(int(opts.get("gap", 14)))
	scroll = parts[0]
	scroll.name = "BodyScroll"
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	body = parts[1]
	body.name = "Body"
	_column.add_child(scroll)
	# a hairline over the action row, shown only while the body scrolls under it (sticky actions; 0 draws otherwise)
	_actions_rule = HSeparator.new()
	_actions_rule.name = "ActionsRule"
	_actions_rule.visible = false
	_actions_rule.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_column.add_child(_actions_rule)
	actions = VBoxContainer.new() if sheet else HBoxContainer.new()
	actions.name = "Actions"
	actions.add_theme_constant_override("separation", 12)
	if actions is HBoxContainer:
		(actions as HBoxContainer).alignment = BoxContainer.ALIGNMENT_END
	actions.visible = false
	_column.add_child(actions)
	if sheet:
		_safe_gap = Control.new()
		_safe_gap.name = "SafeGap"
		_safe_gap.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_safe_gap.visible = false
		_column.add_child(_safe_gap)
	resized.connect(_queue_layout)
	body.minimum_size_changed.connect(_queue_layout)
	body.child_entered_tree.connect(func(_n: Node) -> void: _queue_layout())
	_layout()
	_queue_layout()


## kind: primary / secondary / tonal / ghost / danger. Portrait sheets stack full-width buttons (primary on top).
func add_action(text: String, kind: String = "primary", on_press: Callable = Callable(), node_name: String = "") -> Button:
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	var b: Button = kit.button(text, kind, str(_opts.get("action_icon", "")) if kind == "primary" else "", on_press)
	if node_name != "":
		b.name = node_name
	b.custom_minimum_size.x = 200 if not sheet and kind == "primary" else 0
	if sheet:
		b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		actions.add_child(b)
		if kind == "primary":
			actions.move_child(b, 0)
	else:
		actions.add_child(b)
	actions.visible = true
	_layout()
	_queue_layout()
	return b


## n side-by-side columns in landscape (stacked in portrait). Returns the column VBoxes.
## Re-checked on every layout pass (10-27): a screen-size change can turn side by side into stacked and back, and a
## column with nothing visible in it is hidden (an empty column kept its EXPAND share and squeezed the other one:
## "Langu / age", Airport Settings W66). A column the game hid itself stays hidden.
func columns(n: int, gap: int = 28) -> Array:
	var out: Array = []
	var stack := _stack_columns(n, gap, sheet)
	var row: BoxContainer = VBoxContainer.new() if stack else HBoxContainer.new()
	_style_columns(row, gap, stack)
	for i in n:
		var v := VBoxContainer.new()
		v.name = "Col%d" % i
		v.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		v.size_flags_stretch_ratio = 1.0
		v.add_theme_constant_override("separation", 12)
		v.child_entered_tree.connect(func(_n: Node) -> void: _queue_layout())
		v.child_exiting_tree.connect(func(_n: Node) -> void: _queue_layout())
		v.minimum_size_changed.connect(_queue_layout)
		row.add_child(v)
		out.append(v)
	body.add_child(row)
	_col_rows.append([row, gap])
	return out


## Side by side only when every column gets a readable width (≥ COLUMN_MIN px): a centred card on a portrait phone or a
## 7-inch handheld stacks them (2026-10-26: Settings in two 260 px columns split "Music" into "Mus / ic").
func _stack_columns(n: int, gap: int, fallback: bool) -> bool:
	if sheet:
		return true
	if n <= 1 or not is_inside_tree() or int(load("res://addons/coddy_ui/ui_kit.gd").design) < 2:
		return fallback
	var avail := size.x if size.x > 0.0 else get_viewport_rect().size.x
	var w := clampf(width, minf(420.0, avail - 2 * margin), avail - 2 * margin)
	return (w - 64.0 - gap * (n - 1)) / float(n) < COLUMN_MIN


func _style_columns(row: BoxContainer, gap: int, stack: bool) -> void:
	row.name = "Columns"
	row.add_theme_constant_override("separation", gap if not stack else 14)
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL


## Empty columns hidden, side by side vs stacked for the current width. Returns a key of the result.
func _fit_columns() -> String:
	var key := ""
	# a game that rebuilds its body frees the old rows: forget them (never touch a freed row)
	_col_rows = _col_rows.filter(func(e: Array) -> bool: return is_instance_valid(e[0]) and not (e[0] as Node).is_queued_for_deletion())
	for i in _col_rows.size():
		var row: BoxContainer = _col_rows[i][0]
		var gap: int = _col_rows[i][1]
		var used := 0
		for col in row.get_children():
			var c := col as Control
			var filled := c.get_children().any(func(k: Node) -> bool: return k is CanvasItem and (k as CanvasItem).visible)
			if not filled and c.visible:
				c.visible = false
				c.set_meta("coddy_empty_hidden", true)
			elif filled and not c.visible and c.has_meta("coddy_empty_hidden"):
				c.visible = true
				c.remove_meta("coddy_empty_hidden")
			if c.visible:
				used += 1
		var stacked := row is VBoxContainer
		var want := _stack_columns(used, gap, stacked) if used > 1 else stacked
		if want != stacked:
			row = _swap_columns(row, gap, want)
			_col_rows[i][0] = row
		key += "%d%d" % [used, 1 if want else 0]
	return key


func _swap_columns(row: BoxContainer, gap: int, stack: bool) -> BoxContainer:
	var to: BoxContainer = VBoxContainer.new() if stack else HBoxContainer.new()
	var parent := row.get_parent()
	var at := row.get_index()
	parent.remove_child(row)
	_style_columns(to, gap, stack)
	for c in row.get_children():
		row.remove_child(c)
		to.add_child(c)
	parent.add_child(to)
	parent.move_child(to, at)
	row.free()
	return to


func _queue_layout() -> void:
	if not _laying and not _in_layout:
		_laying = true
		_layout.call_deferred()


## Content-sized: header + body (up to what fits) + actions. Runs deferred after any content / size change; settles in
## two passes when wrapping text changes height with the width.
func _layout() -> void:
	_laying = false
	if card == null or not is_inside_tree() or _in_layout:
		return
	var avail := size
	if avail.x <= 0 or avail.y <= 0:
		return
	# Nothing changed since the last pass (size, content count, body minimum): stop (no deferred ping-pong).
	var gaps := _safe_gaps()
	_in_layout = true
	var cols := _fit_columns()
	_in_layout = false
	var key := "%s|%d|%s|%d|%s|%s" % [avail, body.get_child_count(), body.get_combined_minimum_size(), actions.get_child_count(), gaps, cols]
	if key == _last_key:
		return
	_in_layout = true
	_do_layout(avail, gaps)
	_in_layout = false
	_last_key = "%s|%d|%s|%d|%s|%s" % [avail, body.get_child_count(), body.get_combined_minimum_size(), actions.get_child_count(), gaps, cols]


## What this full-rect layer still has to keep free for the system bars (canvas px; 0 when its host is already inside
## the safe area, or off a phone). 2026-10-27: a modal added to a full-screen root sat under the navigation bar.
func _safe_gaps() -> Vector4:
	var vp := get_viewport_rect().size
	var e := UiSafeArea.overlay_edges(vp)
	return Vector4.ZERO if e == Vector4.ZERO else UiSafeArea.inner_gaps(e, get_global_rect(), vp)


func _do_layout(full: Vector2, gaps: Vector4 = Vector4.ZERO) -> void:
	# a sheet's background runs on to the bottom edge (its SafeGap keeps the actions above the bar); a card stays inside
	var avail := full - Vector2(gaps.x + gaps.z, gaps.y + (0.0 if sheet else gaps.w))
	if _safe_gap != null:
		_safe_gap.custom_minimum_size.y = gaps.w
		_safe_gap.visible = gaps.w > 0.0
	var w := avail.x if sheet else clampf(width, minf(420.0, avail.x - 2 * margin), avail.x - 2 * margin)
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	scroll.custom_minimum_size.y = 0
	card.size = Vector2(w, card.size.y)
	kit.settle(card)   # real widths for wrapping text before measuring (two passes: wraps change heights)
	kit.settle(card)
	var sb: StyleBox = card.get_theme_stylebox("panel")
	var chrome := sb.get_minimum_size().y
	var sep := float(_column.get_theme_constant("separation"))
	var fixed := chrome
	var shown := 0
	for ch in _column.get_children():
		if ch == scroll or not (ch as Control).visible:
			continue
		fixed += (ch as Control).get_combined_minimum_size().y
		shown += 1
	fixed += sep * shown
	var max_h := avail.y * max_fraction if sheet else avail.y - 2 * margin
	var want := body.get_combined_minimum_size().y
	var body_h := clampf(want, 0.0, maxf(0.0, max_h - fixed))
	var scrolls := want > body_h + 1.0 and actions.visible and int(kit.design) >= 2
	if _actions_rule.visible != scrolls:
		_actions_rule.visible = scrolls
		fixed += (1.0 + sep) * (1.0 if scrolls else -1.0)
		body_h = clampf(want, 0.0, maxf(0.0, max_h - fixed))
	scroll.custom_minimum_size.y = body_h
	var h := minf(fixed + body_h, max_h)
	card.size = Vector2(w, h)
	if card.size.y > h + 0.5:   # a stale cached minimum kept it taller: refresh and size again
		card.update_minimum_size()
		card.size = Vector2(w, h)
	kit.settle(card)
	if sheet:
		card.position = Vector2(gaps.x, gaps.y + avail.y - h)
	else:
		card.position = (Vector2(gaps.x, gaps.y) + (avail - card.size) / 2.0).round()


## Android Back (UiBack) / Esc on this window: the "back" opt, else close_requested when it has a close, else nothing.
func back() -> void:
	if on_back.is_valid():
		on_back.call()
	elif bool(_opts.get("close", false)):
		close_requested.emit()


func _unhandled_input(ev: InputEvent) -> void:
	if ev.is_action_pressed("ui_cancel") and (bool(_opts.get("close", false)) or on_back.is_valid()):
		back()
		get_viewport().set_input_as_handled()
