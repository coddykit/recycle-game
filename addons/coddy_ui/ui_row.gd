extends Button
## coddy_ui list / settings row: [icon tile] title (+ subtitle) [trailing: chevron | switch | value | custom]. The whole
## row is the touch target (≥ 96 px = 55 dp tall) and still scrolls its list when dragged (it is a Button: TouchScroll
## cancels the press). Build with the static helpers:
##   UiRow.nav("globe", tr("Language"), tr("English"), open_picker)      # chevron row
##   UiRow.toggle("music", tr("Music"), on, func(v): set_music(v))      # switch row (toggle_mode; the row flips it)
##   UiRow.item("ship", "accent", tr("Tanker #3"), tr("North port"), select)   # list row (style "card": own background)
## `title` / `subtitle` / `value` / `on` / `selected` are live properties (set them to update in place: ui_reconcile).
## Button.text stays "" (the row draws its own labels); find a row by its node name or `title`.

const Tokens = preload("ui_tokens.gd")
const Icons = preload("ui_icons.gd")

signal switched(on: bool)

var title := "":
	set(v):
		title = v
		if _title != null:
			_title.text = v
var subtitle := "":
	set(v):
		subtitle = v
		if _sub != null:
			_sub.text = v
			_sub.visible = v != ""
			_fit_sub()
var value := "":
	set(v):
		value = v
		if _value != null:
			_value.text = v
			_value.visible = v != ""
var on := false:
	set(v):
		on = v
		if trailing == "switch":
			set_pressed_no_signal(v)
		_paint()
var selected := false:
	set(v):
		selected = v
		_paint()
var icon_name := ""
var icon_kind := "accent"
var trailing := "chevron"           ## chevron | switch | value | none
var style := "plain"                ## plain (inside a group) | card (own rounded background)
var _box: HBoxContainer
var _tile: TextureRect
var _title: Label
var _sub: Label
var _value: Label
var _trail: TextureRect
var _built := false


static func _new_row() -> Button:
	return load("res://addons/coddy_ui/ui_row.gd").new()


static func nav(icon: String, title_text: String, value_text: String = "", on_press: Callable = Callable(), kind: String = "accent") -> Button:
	var r := _new_row()
	r.icon_name = icon
	r.icon_kind = kind
	r.title = title_text
	r.subtitle = value_text
	r.trailing = "chevron"
	if on_press.is_valid():
		r.pressed.connect(on_press)
	return r


static func toggle(icon: String, title_text: String, is_on: bool, on_change: Callable = Callable(), kind: String = "accent") -> Button:
	var r := _new_row()
	r.icon_name = icon
	r.icon_kind = kind
	r.title = title_text
	r.trailing = "switch"
	r.toggle_mode = true
	r.on = is_on
	r.toggled.connect(func(v: bool) -> void:
		r.on = v
		r.switched.emit(v))
	if on_change.is_valid():
		r.switched.connect(on_change)
	return r


static func item(icon: String, kind: String, title_text: String, sub_text: String = "", on_press: Callable = Callable(), trail: String = "chevron") -> Button:
	var r := _new_row()
	r.icon_name = icon
	r.icon_kind = kind
	r.title = title_text
	r.subtitle = sub_text
	r.trailing = trail
	r.style = "card"
	if on_press.is_valid():
		r.pressed.connect(on_press)
	return r


func _init() -> void:
	pressed.connect(load("res://addons/coddy_ui/ui_kit.gd")._tap)
	focus_mode = Control.FOCUS_NONE
	theme_type_variation = "RowButton"
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	custom_minimum_size.y = Tokens.TOUCH.row
	clip_contents = false


func _ready() -> void:
	_build()


func _build() -> void:
	if _built:
		return
	_built = true
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	_box = HBoxContainer.new()
	_box.name = "Box"
	_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_box.add_theme_constant_override("separation", 20)
	_box.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var m := Vector4(24, 12, 24, 12) if style == "plain" else Vector4(18, 12, 22, 12)
	_box.offset_left = m.x
	_box.offset_top = m.y
	_box.offset_right = -m.z
	_box.offset_bottom = -m.w
	add_child(_box)
	if icon_name != "":
		_tile = kit.tile(icon_name, icon_kind, 56 if style == "plain" else 60, true)
		_tile.name = "Tile"
		_box.add_child(_tile)
	var texts := VBoxContainer.new()
	texts.name = "Texts"
	texts.mouse_filter = Control.MOUSE_FILTER_IGNORE
	texts.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	texts.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	texts.add_theme_constant_override("separation", 0)
	_title = kit.label(title, "LabelStrong", true)
	_title.name = "Title"
	# never a word split letter by letter in a narrow column (2026-10-26: "Mus / ic", "R / e / d / u / c / e"): the
	# title's minimum width is its longest word; set in the tree and after a text change is drawn (not in a layout pass)
	if int(kit.design) >= 2:   # the redesign (UiKit.design 2); design 1 keeps the old row exactly
		_title.ready.connect(func() -> void: kit._word_min(_title))
		_title.draw.connect(func() -> void: kit.refit_later(_title, kit.REFIT_WORD_MIN))
	texts.add_child(_title)
	_sub = kit.label(subtitle, "CaptionMuted", true)
	_sub.name = "Subtitle"
	_sub.visible = subtitle != ""
	# 10-27 (Train Baron landscape Settings: "coddykit.c / om"): the grey line wraps between words only; a word wider than
	# the column steps the font down (never below 21 px), then ends in "…" on one line — never split mid-word
	_sub.autowrap_mode = TextServer.AUTOWRAP_WORD
	_sub.resized.connect(_fit_sub)
	texts.add_child(_sub)
	_box.add_child(texts)
	_value = kit.label(value, "Muted")
	_value.name = "Value"
	_value.visible = value != ""
	_value.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	_box.add_child(_value)
	if trailing in ["chevron", "switch"]:
		_trail = TextureRect.new()
		_trail.name = "Trailing"
		_trail.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_trail.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		_trail.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		_trail.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		_trail.custom_minimum_size = Vector2(92, 52) if trailing == "switch" else Vector2(30, 30)
		_box.add_child(_trail)
	_box.minimum_size_changed.connect(_fit)
	_paint()
	_fit()


func _fit() -> void:
	if _box == null:
		return
	var need := _box.get_combined_minimum_size().y + 24.0
	var h := maxf(Tokens.TOUCH.row if style == "plain" else Tokens.TOUCH.min + 12, need)
	if absf(h - custom_minimum_size.y) > 0.5:
		custom_minimum_size.y = h


var _fit_w := -1.0


func _notification(what: int) -> void:
	# Height follows the wrapped title only when the WIDTH changed (height changes come from _fit itself).
	if what == NOTIFICATION_RESIZED and _box != null and absf(size.x - _fit_w) >= 1.0:
		_fit_w = size.x
		_fit.call_deferred()


var _sub_key := ""


## The grey line's longest word in the column's width: the caption size, one step smaller at a time down to the 21 px
## text minimum; still too wide → one line with "…". Runs on a resize / text change (never per frame).
func _fit_sub() -> void:
	if _sub == null or not _sub.is_inside_tree() or _sub.size.x <= 0.0 or not _sub.visible:
		return
	var shown := _sub.atr(_sub.text)
	var key := "%s|%.0f" % [shown, _sub.size.x]
	if key == _sub_key:
		return
	_sub_key = key
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	_sub.remove_theme_font_size_override("font_size")
	var font: Font = _sub.get_theme_font("font")
	var base := _sub.get_theme_font_size("font_size")
	var words: PackedStringArray = kit.fit_words(shown)
	var fs := base
	while fs > kit.FIT_TEXT_MIN and kit._widest(font, words, fs) > _sub.size.x:
		fs -= 1
	if fs != base:
		_sub.add_theme_font_size_override("font_size", fs)
	var long: bool = kit._widest(font, words, fs) > _sub.size.x + 0.5
	_sub.autowrap_mode = TextServer.AUTOWRAP_OFF if long else TextServer.AUTOWRAP_WORD
	_sub.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS if long else TextServer.OVERRUN_NO_TRIMMING


func _paint() -> void:
	if not _built:
		return
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	var Th = load("res://addons/coddy_ui/ui_theme.gd")
	if _trail != null:
		if trailing == "switch":
			_trail.texture = Th.switch_texture(kit.p, on, disabled)
		else:
			_trail.texture = Icons.texture("chevron-right", 30, kit.c("on_surface_muted"))
	var normal: StyleBox = null
	var pressed_box: StyleBox = null
	if style == "card" or selected:
		var bg: Color = kit.c("primary_container") if selected else kit.c("surface_variant")
		var n: StyleBoxFlat = Th.box(bg, Tokens.RADIUS.s, Vector4.ZERO)
		if selected:
			n.border_width_left = 5
			n.border_color = kit.c("primary")
		normal = n
		pressed_box = Th.box(bg.lightened(0.06), Tokens.RADIUS.s, Vector4.ZERO)
		if selected:
			(pressed_box as StyleBoxFlat).border_width_left = 5
			(pressed_box as StyleBoxFlat).border_color = kit.c("primary")
	if normal != null:
		for st in ["normal", "hover", "focus", "disabled"]:
			add_theme_stylebox_override(st, normal)
		add_theme_stylebox_override("pressed", pressed_box)
		add_theme_stylebox_override("hover_pressed", pressed_box)
	else:
		for st in ["normal", "hover", "focus", "disabled", "pressed", "hover_pressed"]:
			remove_theme_stylebox_override(st)
	if trailing == "switch":
		var plain: StyleBox = normal if normal != null else StyleBoxEmpty.new()
		add_theme_stylebox_override("pressed", plain)
		add_theme_stylebox_override("hover_pressed", plain)
	if _title != null:
		_title.modulate.a = 0.5 if disabled else 1.0


func set_disabled_state(v: bool) -> void:
	disabled = v
	_paint()


## ui_reconcile: take another (freshly built) row's content without replacing this node (a held finger stays valid).
func copy_from(o: Button) -> void:
	if title != o.title:
		title = o.title
	if subtitle != o.subtitle:
		subtitle = o.subtitle
	if value != o.value:
		value = o.value
	if on != o.on:
		on = o.on
	if selected != o.selected:
		selected = o.selected
	if disabled != o.disabled:
		disabled = o.disabled
		_paint()
	if icon_kind != o.icon_kind or icon_name != o.icon_name:
		icon_kind = o.icon_kind
		icon_name = o.icon_name
		if _tile != null:
			var kit = load("res://addons/coddy_ui/ui_kit.gd")
			var fb: Array = kit.tile_colors(icon_kind)
			_tile.texture = Icons.tile(icon_name, int(_tile.custom_minimum_size.x), fb[0], fb[1], true)
