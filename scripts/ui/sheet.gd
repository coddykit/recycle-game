class_name Sheet
extends Control
const Kit = preload("res://addons/coddy_ui/ui_kit.gd")
const UiScroll = preload("res://addons/coddy_ui/ui_scroll.gd")
const SheetTabs = preload("res://addons/coddy_ui/ui_segmented.gd")
## Bottom sheet base: slides up above the nav bar, world stays visible on top.
## Subclasses implement build(body) and optionally live_update(delta).

var sheet_name := ""
var title := ""
var arg: Variant = null
var height_frac := 0.62
var hud: Control
var panel: PanelContainer
var backdrop: ColorRect
var header: HBoxContainer
var header_extra: HBoxContainer
var title_label: Label
var tabs_row: HBoxContainer
var scroll: ScrollContainer
var body: VBoxContainer
var footer: VBoxContainer
## 2026-10-26 redesign: the kit page header (tile, title, summary line, close chip) and its tab icon (Lucide)
var icon_name := "file-text"
var head: Dictionary = {}
var _sub_alts: Array = []
var _column: VBoxContainer
var _cap_h := 0.0
var _fit_h := 0.0
var _slide := 0.0
var _fit_queued := false
var _refresh_queued := false
var _closing := false
## W63 (W62 Airport lesson): a rebuild frees every button of the sheet. Cash, offers and vehicles change every few
## seconds in late game, so a rebuild landed between press and release and the tap was lost. While a finger is
## down the rebuild waits and runs right after the release (the GUI handles the release first, so `pressed` fires).
const HOLD_MAX_S := 4.0   # a lost release never freezes the sheet
var _finger_down := false
var _finger_t := 0.0
var _refresh_after_release := false


func _init() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func open(h: Control, a: Variant) -> void:
	hud = h
	arg = a
	_build_frame()
	on_opened()
	refresh_now()
	if UIKit.reduced_motion():
		UIManager.notify_panel(sheet_name, true)
		return
	# slide in (the offset is applied on top of the fitted height: a height fit during the slide never moves the target)
	_slide = 1.0
	_place()
	var t := create_tween().set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	t.tween_method(_set_slide, 1.0, 0.0, 0.28)
	backdrop.modulate.a = 0.0
	t.parallel().tween_property(backdrop, "modulate:a", 1.0, 0.25)
	AudioManager.play("whoosh", 0.1, -6.0)
	UIManager.notify_panel(sheet_name, true)


## Low graphics tier (weak phones, 2026-10-26 redesign): flat rows, coloured-text actions, no tiles / hairlines / bars.
static func low() -> bool:
	return GameManager.quality() == "low"


func _build_frame() -> void:
	var vp_h := get_viewport_rect().size.y
	var nav_h: float = hud.nav_height()
	_cap_h = minf(floorf(vp_h * height_frac), vp_h - nav_h - 280 - hud.top_inset)
	_fit_h = _cap_h
	backdrop = ColorRect.new()
	backdrop.color = Color(0.03, 0.04, 0.09, 0.35)
	backdrop.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	backdrop.mouse_filter = Control.MOUSE_FILTER_STOP
	backdrop.gui_input.connect(func(e: InputEvent):
		if (e is InputEventMouseButton and e.pressed) or (e is InputEventScreenTouch and e.pressed):
			close())
	add_child(backdrop)
	panel = PanelContainer.new()
	panel.theme_type_variation = "SheetBottom"
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	panel.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	add_child(panel)
	var col := UIKit.vbox(12)
	panel.add_child(col)
	# grab handle: the kit's rounded pill (one stylebox, the same single draw as the old bar)
	var handle := PanelContainer.new()
	handle.name = "Grabber"
	handle.theme_type_variation = "Grabber"
	handle.custom_minimum_size = Vector2(64, 6)
	handle.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	handle.mouse_filter = Control.MOUSE_FILTER_IGNORE
	col.add_child(handle)
	# 2026-10-26 redesign: the kit page header — [52 px tab tile] Title + ONE summary line [round close chip], hairline
	var lw := low()
	head = Kit.page_header(icon_name, tr(title), close, {"sub": _sub_alts, "low": lw, "close_label": tr("Close")})
	col.add_child(head.box)
	header = head.box.get_node("Row")
	title_label = head.title
	(head.close as Button).name = "Close"
	if lw:
		head.box.get_node("Divider").visible = false   # Low: no hairline (one draw less)
	header_extra = UIKit.hbox(10)
	header_extra.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	header.add_child(header_extra)
	header.move_child(header_extra, head.close.get_index())
	tabs_row = UIKit.hbox(8)
	tabs_row.visible = false
	col.add_child(tabs_row)
	scroll = UiScroll.make(true, false)
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	col.add_child(scroll)
	var pad := MarginContainer.new()
	pad.add_theme_constant_override("margin_bottom", 40)
	pad.add_theme_constant_override("margin_right", 4)
	pad.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	pad.mouse_filter = Control.MOUSE_FILTER_PASS
	scroll.add_child(pad)
	body = UIKit.vbox(Kit.CARD_GAP)
	body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	pad.add_child(body)
	footer = UIKit.vbox(8)
	col.add_child(footer)
	_column = col
	_place()
	# the sheet follows its rows (a short list leaves the world visible) — at most once a frame
	pad.minimum_size_changed.connect(_queue_fit)
	footer.minimum_size_changed.connect(_queue_fit)
	tabs_row.visibility_changed.connect(_queue_fit)


## Bottom offset of the sheet: fitted height + the slide offset (open / close motion).
func _place() -> void:
	if panel == null:
		return
	var vp_h := get_viewport_rect().size.y
	var nav_h: float = hud.nav_height() if hud else 0.0
	var dy := _slide * (_fit_h + 60.0)
	panel.offset_top = -(_fit_h + nav_h) + dy
	panel.offset_bottom = -nav_h + 30 + dy
	backdrop.offset_bottom = vp_h - nav_h - _fit_h + 30


func _set_slide(v: float) -> void:
	_slide = v
	_place()


func _queue_fit() -> void:
	if _fit_queued or _closing:
		return
	_fit_queued = true
	_fit.call_deferred()


## The sheet is only as tall as its rows (never taller than the old height): grabber + header + tabs + list + footer.
func _fit() -> void:
	_fit_queued = false
	if _closing or panel == null or not is_instance_valid(scroll):
		return
	var sb := panel.get_theme_stylebox("panel")
	var sep := float(_column.get_theme_constant("separation"))
	var want := sb.get_minimum_size().y - 30.0   # 30 px of the panel hide under the nav bar
	var shown := 0
	for c in _column.get_children():
		if c is Control and (c as Control).visible:
			var h := (scroll.get_child(0) as Control).get_combined_minimum_size().y if c == scroll else (c as Control).get_combined_minimum_size().y
			if c == footer and footer.get_child_count() == 0:
				continue
			want += h
			shown += 1
	want += sep * maxi(0, shown - 1) + 8.0
	var h := clampf(ceilf(want), minf(360.0, _cap_h), _cap_h)
	if absf(h - _fit_h) > 0.5:
		_fit_h = h
		_place()


## The header's ONE summary line (String or Array long → short; "" hides it). Safe before the frame exists (tests).
func set_sub(alts) -> void:
	var a: Array = alts if alts is Array else ([str(alts)] if str(alts) != "" else [])
	_sub_alts = a.map(func(x): return tr(str(x)))
	if not head.is_empty():
		Kit.page_header_sub(head, _sub_alts)


func set_title(t: String) -> void:
	title = t
	if not head.is_empty() and is_instance_valid(title_label):
		Kit.page_header_set(head, icon_name, tr(t), _sub_alts)


# --- 2026-10-26 list rows -------------------------------------------------------------------
## A list row: kit list card [icon] title + ONE grey line. `game_icon` = the game's own colour icon (assets/icons),
## drawn where the kit tile goes; opts = the kit's list_card options. Texts are translated here (the kit fits what it
## measures). Low: no icon, a flat row.
func row(game_icon: String, title_text: String, sub = "", opts: Dictionary = {}) -> PanelContainer:
	var o := {"low": low(), "flat_low": true}
	o.merge(opts, true)
	var alts: Array = sub if sub is Array else [str(sub)]
	o["sub"] = alts.map(func(x): return tr(str(x)))
	var card := Kit.list_card("package", tr(title_text), o)
	var tile := card.get_node_or_null("Col/Head/Tile")
	if tile != null and game_icon != "" and not bool(opts.get("locked", false)):
		var ic := UIKit.icon(game_icon, Kit.CARD_TILE)
		ic.name = "Tile"
		ic.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		tile.get_parent().add_child(ic)
		tile.get_parent().move_child(ic, 0)
		tile.free()
	return card


## The ONE call to action of a row, as a pill on its right (price on the pill). Low: coloured text, no pill box.
func pill(card: Control, text: String, cb: Callable, enabled := true, kind := "primary") -> Button:
	var b := Kit.action_pill(tr(text), cb, enabled, kind, "", low())
	b.custom_minimum_size.x = 84   # 48 dp both ways ("ON" alone was 77 px wide)
	Kit.list_card_side(card, b)
	return b


## Medium / High: a thin level / progress bar in the row's text column (Low skips it).
func progress(card: Control, value: float, max_value := 1.0) -> void:
	if not low():
		Kit.card_progress(card, clampf(value, 0.0, max_value), max_value)


## A big choice tile (dispatch vehicles / routes) in the list-card look: the card surface, the selected one with the
## game's primary border (was a solid gold slab with dark text). Text colours stay the card's.
static func choice_style(b: Button, selected: bool) -> void:
	var t := UIKit.theme()
	var sb := t.get_stylebox("panel", "ListCardSelected" if selected else "ListCard")
	for st in ["normal", "hover", "pressed", "hover_pressed", "focus", "disabled"]:
		b.add_theme_stylebox_override(st, sb)


## The whole row opens it too (as the old full-row button did): a press and release in place, not a list drag.
func tap_row(card: Control, cb: Callable) -> void:
	card.mouse_filter = Control.MOUSE_FILTER_PASS
	card.gui_input.connect(func(e: InputEvent):
		if not (e is InputEventMouseButton) or (e as InputEventMouseButton).button_index != MOUSE_BUTTON_LEFT:
			return
		if e.pressed:
			card.set_meta("tap_at", e.global_position)
		elif card.has_meta("tap_at") and (card.get_meta("tap_at") as Vector2).distance_to(e.global_position) < 24.0:
			card.remove_meta("tap_at")
			cb.call())


## A group head: overline + optional trailing value.
func section(text: String, trailing := "") -> Control:
	var m := Kit.section_header(tr(text), trailing)
	var l: Label = m.find_child("Text", true, false)
	if l != null:
		l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		l.custom_minimum_size.x = 1
	return m


func set_tabs(names: Array, current: String, on_pick: Callable) -> void:
	for c in tabs_row.get_children():
		tabs_row.remove_child(c)
		c.queue_free()
	tabs_row.visible = true
	var seg := SheetTabs.new()
	seg.name = "Tabs"
	seg.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	seg.setup(names.map(func(n): return tr(str(n))), maxi(0, names.find(current)), "Tab_")
	for i in seg.buttons.size():
		(seg.buttons[i] as Button).set_meta("tab", str(names[i]))
	seg.changed_to.connect(func(i: int): on_pick.call(names[i]))
	tabs_row.add_child(seg)


## Coalesces many change signals into one rebuild per frame.
func refresh() -> void:
	if _refresh_queued or _closing:
		return
	_refresh_queued = true
	call_deferred("refresh_now")


func refresh_now() -> void:
	_refresh_queued = false
	if _closing or body == null:
		return
	if _finger_down and _finger_t < HOLD_MAX_S:
		_refresh_after_release = true
		return
	_refresh_after_release = false
	var sv := scroll.scroll_vertical
	for c in body.get_children():
		body.remove_child(c)
		c.queue_free()
	for c in footer.get_children():
		footer.remove_child(c)
		c.queue_free()
	for c in header_extra.get_children():
		header_extra.remove_child(c)
		c.queue_free()
	build(body)
	if not is_inside_tree():
		return
	await get_tree().process_frame
	if is_instance_valid(scroll):
		scroll.scroll_vertical = sv


func close() -> void:
	if _closing:
		return
	_closing = true
	UIManager.notify_panel(sheet_name, false)
	on_closed()
	var t := create_tween().set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN)
	t.tween_method(_set_slide, _slide, 1.0, 0.2)
	t.parallel().tween_property(backdrop, "modulate:a", 0.0, 0.2)
	t.tween_callback(queue_free)
	if hud and hud.current_sheet == self:
		hud.current_sheet = null


func close_instant() -> void:
	if _closing:
		return
	_closing = true
	UIManager.notify_panel(sheet_name, false)
	on_closed()
	queue_free()


func _process(delta: float) -> void:
	if _finger_down:
		_finger_t += delta
		if _finger_t >= HOLD_MAX_S and _refresh_after_release:
			refresh()
	if not _closing and body:
		live_update(delta)


## Runs before the GUI sees the event: tracks the primary finger (touch or its emulated mouse twin).
func _input(e: InputEvent) -> void:
	var down: bool
	if e is InputEventScreenTouch:
		if (e as InputEventScreenTouch).index != 0:
			return
		down = e.pressed
	elif e is InputEventMouseButton:
		if (e as InputEventMouseButton).button_index != MOUSE_BUTTON_LEFT:
			return
		down = e.pressed
	else:
		return
	if down:
		if not _finger_down:
			_finger_t = 0.0
		_finger_down = true
	else:
		_finger_down = false
		if _refresh_after_release:
			refresh()   # deferred: the release reaches the original button first


func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT or what == NOTIFICATION_APPLICATION_PAUSED:
		_finger_down = false
		if _refresh_after_release:
			refresh()


# --- virtuals -------------------------------------------------------------------------
func build(_b: VBoxContainer) -> void:
	pass


func live_update(_delta: float) -> void:
	pass


func on_opened() -> void:
	pass


func on_closed() -> void:
	pass
