extends Control
## coddy_ui step dots: "page 2 of 3" as pills (current = a wider brand pill). Drawn with draw_rect/draw_circle on change
## only (no per-frame work); the width and colour change animate with one short tween (UiKit.reduce_motion: snap).
## `count` is always the real number of pages (the pager sets it on every page) and re-clamps `current`.

const Tokens = preload("ui_tokens.gd")
const DOT := 12.0
const WIDE := 36.0
const GAP := 10.0

@export var count := 3:
	set(v):
		count = maxi(1, v)
		if current > count - 1:
			current = count - 1
		update_minimum_size()
		queue_redraw()
@export var current := 0:
	set(v):
		var old := current
		current = clampi(v, 0, count - 1)
		if is_inside_tree() and old != current and not load("res://addons/coddy_ui/ui_kit.gd").reduce_motion:
			_from = old
			_t = 0.0
			if _tw != null:
				_tw.kill()
			_tw = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
			_tw.tween_property(self, "_t", 1.0, Tokens.MOTION.slow)
		else:
			_t = 1.0
		queue_redraw()
var on_color := Color("ffb347")
var off_color := Color("2b4e70")
var _from := 0
var _tw: Tween
var _t := 1.0:
	set(v):
		_t = v
		queue_redraw()


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	on_color = kit.c("primary")
	off_color = kit.c("outline")


func _get_minimum_size() -> Vector2:
	return Vector2((count - 1) * (DOT + GAP) + WIDE, DOT)


func _draw() -> void:
	var x := 0.0
	var y := (size.y - DOT) / 2.0
	for i in count:
		var w := DOT
		var col := off_color
		if i == current:
			w = lerpf(DOT, WIDE, _t)
			col = off_color.lerp(on_color, _t)
		elif i == _from and _t < 1.0:
			w = lerpf(WIDE, DOT, _t)
			col = on_color.lerp(off_color, _t)
		_pill(Rect2(x, y, w, DOT), col)
		x += w + GAP


func _pill(r: Rect2, col: Color) -> void:
	var rad := r.size.y / 2.0
	draw_circle(Vector2(r.position.x + rad, r.position.y + rad), rad, col, true, -1.0, true)
	draw_circle(Vector2(r.end.x - rad, r.position.y + rad), rad, col, true, -1.0, true)
	if r.size.x > r.size.y:
		draw_rect(Rect2(r.position.x + rad, r.position.y, r.size.x - 2.0 * rad, r.size.y), col)
