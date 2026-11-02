extends PanelContainer
## coddy_ui segmented control: 2–4 equal options on a sunken track, the chosen one filled with the brand colour
## (Low / Medium / High, Day / Week…). 84 px tall (48 dp); labels that do not fit step down to the 12 sp caption, then wrap.
##   var seg := UiSegmented.new(); seg.setup([tr("Low"), tr("Medium"), tr("High")], current)
##   seg.changed_to.connect(func(i): set_quality(i))
## Buttons are named "<prefix><i>" (prefix default "Option_") so tests and guides can find them.
## Disabled options keep showing the selection: the chosen one draws a dimmed selected fill (Canal Control 1.1.8 — with
## a ship in the canal both directions were disabled and neither looked chosen). Set `disabled` on the buttons as usual.

signal changed_to(index: int)

const Tokens = preload("ui_tokens.gd")
const Icons = preload("ui_icons.gd")

var selected := 0
var buttons: Array = []
var _group := ButtonGroup.new()
var _row: HBoxContainer
var _fit_w := -1.0
var _fit_size := 0
var _mark_queued := false
const DISABLED_SELECTED_ALPHA := 0.45   ## the selected fill / text while disabled (enabled = 1.0)


func _init() -> void:
	theme_type_variation = "Track"
	size_flags_horizontal = Control.SIZE_EXPAND_FILL


func setup(options: Array, current: int = 0, prefix: String = "Option_", icons: Array = []) -> void:
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	if _row != null:
		_row.queue_free()
	_row = HBoxContainer.new()
	_row.add_theme_constant_override("separation", 6)
	add_child(_row)
	buttons.clear()
	for i in options.size():
		var b := Button.new()
		b.name = "%s%d" % [prefix, i]
		b.text = str(options[i])
		b.theme_type_variation = "SegmentButton"
		b.toggle_mode = true
		b.button_group = _group
		b.focus_mode = Control.FOCUS_NONE
		b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		b.custom_minimum_size = Vector2(0, Tokens.TOUCH.min - 12)
		b.clip_text = true   # the label never widens the control (it steps down, then trims): no layout feedback
		b.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
		if i < icons.size() and str(icons[i]) != "":
			b.icon = Icons.texture(str(icons[i]), 26, Color.WHITE)
		var index := i
		b.pressed.connect(kit._tap)
		b.pressed.connect(func() -> void: select(index, true))
		_row.add_child(b)
		buttons.append(b)
	select(current, false)
	resized.connect(_fit_labels)


func select(index: int, emit: bool = true) -> void:
	selected = clampi(index, 0, buttons.size() - 1)
	for i in buttons.size():
		(buttons[i] as Button).set_pressed_no_signal(i == selected)
	_mark_disabled_selected()
	if emit:
		changed_to.emit(selected)


func _notification(what: int) -> void:
	if what == NOTIFICATION_THEME_CHANGED and not _mark_queued:
		_mark_queued = true   # on entering the tree this comes before the buttons are in it: resolve their theme next idle
		_mark_disabled_selected.call_deferred()


## Godot draws a disabled button with its "disabled" style even when it is pressed: the selected option gets a "disabled"
## override = its own "pressed" style at DISABLED_SELECTED_ALPHA (only drawn while disabled; the others keep the theme's).
func _mark_disabled_selected() -> void:
	_mark_queued = false
	if not is_inside_tree():
		return   # the theme is resolved in the tree; NOTIFICATION_THEME_CHANGED comes on enter
	for i in buttons.size():
		var b: Button = buttons[i]
		if i != selected:
			for st in ["disabled", "disabled_mirrored"]:
				b.remove_theme_stylebox_override(st)
			b.remove_theme_color_override("font_disabled_color")
			b.remove_theme_color_override("icon_disabled_color")
			continue
		var box := b.get_theme_stylebox("pressed").duplicate() as StyleBox
		if box is StyleBoxFlat:
			var f := box as StyleBoxFlat
			f.bg_color.a *= DISABLED_SELECTED_ALPHA
			f.border_color.a *= DISABLED_SELECTED_ALPHA
			f.shadow_color.a *= DISABLED_SELECTED_ALPHA
		for st in ["disabled", "disabled_mirrored"]:
			b.add_theme_stylebox_override(st, box)
		var text := b.get_theme_color("font_pressed_color")
		b.add_theme_color_override("font_disabled_color", Color(text, text.a * 0.7))
		b.add_theme_color_override("icon_disabled_color", Color(text, text.a * 0.7))


## Long languages: shrink the option labels together (down to 21 px) before they wrap.
func _fit_labels() -> void:
	if buttons.is_empty() or size.x <= 0 or absf(size.x - _fit_w) < 1.0:
		return   # only when the width really changed (a font change must not start a resize ping-pong)
	_fit_w = size.x
	var b0: Button = buttons[0]
	var f: Font = b0.get_theme_font("font")
	var each := (size.x - 12.0 - 6.0 * (buttons.size() - 1)) / buttons.size() - 28.0
	var s := Tokens.TYPE.body.size
	while s > Tokens.TYPE.caption.size:
		var ok := true
		for b in buttons:
			var extra := 34.0 if (b as Button).icon != null else 0.0
			if f.get_string_size((b as Button).text, HORIZONTAL_ALIGNMENT_LEFT, -1, s).x + extra > each:
				ok = false
				break
		if ok:
			break
		s -= 1
	if s == _fit_size:
		return
	_fit_size = s
	for b in buttons:
		(b as Button).add_theme_font_size_override("font_size", s)


## ui_reconcile: take a freshly built control's state in place (selection, texts, enabled) — the held node survives.
func copy_from(o: Control) -> void:
	if o.selected != selected:
		select(o.selected, false)
	for i in mini(buttons.size(), o.buttons.size()):
		var a: Button = buttons[i]
		var b: Button = o.buttons[i]
		if a.text != b.text:
			a.text = b.text
		if a.disabled != b.disabled:
			a.disabled = b.disabled
