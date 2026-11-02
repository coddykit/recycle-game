extends Node
## coddy_ui scroll views (games/CLAUDE.md §16): every list / sheet / modal body is made here, so it always
##   - scrolls with a finger from anywhere inside (TouchScroll.ensure on its viewport; drags on buttons scroll, taps tap);
##   - shows NO bar when the content fits (SCROLL_MODE_AUTO) and, when it does not, still nothing until the list moves:
##     a thin 6 px themed thumb appears while it scrolls and fades out ~0.9 s after the last movement (Patron 10-22);
##     a position the bar only clamps because the content got shorter (a rebuild, a next-frame refit) is no movement;
##   - is marked `coddy_touch_scroll` (meta) so check_touch_scroll.py and the regression suite can prove it is wired.
## This node is the fader: it lives as a child of its ScrollContainer and only works on scroll changes (no per-frame cost).
##   var sc := UiScroll.make()                 # vertical list (content: sc.get_child(0) after add, or UiScroll.list())
##   var parts := UiScroll.list(12)            # [ScrollContainer, VBoxContainer]
##   UiScroll.adopt(existing_scroll_container) # a ScrollContainer built elsewhere (e.g. a shared addon)

const TouchScroll = preload("touch_scroll.gd")
const META := "coddy_touch_scroll"
const HOLD_S := 0.9
const FADE_S := 0.35

var _sc: ScrollContainer
var _tween: Tween
var _range := {}   # bar → Vector2(max_value, page) as of its last `changed`


static func make(vertical: bool = true, horizontal: bool = false) -> ScrollContainer:
	var sc := ScrollContainer.new()
	sc.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_AUTO if vertical else ScrollContainer.SCROLL_MODE_DISABLED
	sc.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_AUTO if horizontal else ScrollContainer.SCROLL_MODE_DISABLED
	adopt(sc)
	return sc


## [ScrollContainer, VBoxContainer]: a vertical list whose rows fill the width.
static func list(separation: int = 12) -> Array:
	var sc := make(true, false)
	sc.size_flags_vertical = Control.SIZE_EXPAND_FILL
	sc.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var box := VBoxContainer.new()
	box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	box.add_theme_constant_override("separation", separation)
	sc.add_child(box)
	return [sc, box]


## [ScrollContainer, HBoxContainer]: a horizontal strip (cards side by side) that scrolls sideways with a finger.
static func strip(separation: int = 12) -> Array:
	var sc := make(false, true)
	sc.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var box := HBoxContainer.new()
	box.add_theme_constant_override("separation", separation)
	sc.add_child(box)
	return [sc, box]


## Wires an existing ScrollContainer: finger scrolling on its viewport + the fading thumb. Safe to call twice.
static func adopt(sc: ScrollContainer) -> ScrollContainer:
	if sc.has_meta(META):
		return sc
	sc.set_meta(META, true)
	sc.follow_focus = false
	var fader: Node = load("res://addons/coddy_ui/ui_scroll.gd").new()
	fader.name = "ScrollFade"
	sc.add_child(fader, false, Node.INTERNAL_MODE_BACK)
	return sc


## True when `sc` came from here (or was adopted): it scrolls with a finger and has the themed, fading thumb.
static func wired(sc: ScrollContainer) -> bool:
	return sc.has_meta(META)


func _enter_tree() -> void:
	_sc = get_parent() as ScrollContainer
	if _sc == null:
		return
	TouchScroll.ensure(_sc.get_viewport())


func _ready() -> void:
	if _sc == null:
		return
	for bar in [_sc.get_v_scroll_bar(), _sc.get_h_scroll_bar()]:
		bar.modulate.a = 0.0
		_range[bar] = Vector2(bar.max_value, bar.page)
		bar.changed.connect(_range_changed.bind(bar))
		bar.value_changed.connect(_value_changed.bind(bar).unbind(1))


## Thumb alpha now (tests): 0 = hidden.
func thumb_alpha() -> float:
	return _sc.get_v_scroll_bar().modulate.a if _sc != null else 0.0


func _range_changed(bar: ScrollBar) -> void:
	_range[bar] = Vector2(bar.max_value, bar.page)


## Range.set_max / set_page clamp the value (value_changed) BEFORE they emit `changed`: a value change that arrives with a
## max / page the fader has not seen yet is that clamp — the content shrank under a still list, nobody scrolled it.
## (2026-10-28: kit fix 3's next-frame refit shrinks a rebuilt panel for one frame; Parts Rush flashed its thumb.)
func _value_changed(bar: ScrollBar) -> void:
	if Vector2(bar.max_value, bar.page) != _range.get(bar, Vector2(bar.max_value, bar.page)):
		return
	_moved()


func _moved() -> void:
	if _sc == null or not is_inside_tree():
		return
	if _tween != null:
		_tween.kill()
	for bar in [_sc.get_v_scroll_bar(), _sc.get_h_scroll_bar()]:
		bar.modulate.a = 1.0
	_tween = create_tween()
	_tween.tween_interval(HOLD_S)
	_tween.tween_property(_sc.get_v_scroll_bar(), "modulate:a", 0.0, FADE_S)
	_tween.parallel().tween_property(_sc.get_h_scroll_bar(), "modulate:a", 0.0, FADE_S)
