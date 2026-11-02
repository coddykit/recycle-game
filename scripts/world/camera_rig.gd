class_name CameraRig
extends Node3D
## Fixed-angle isometric camera: drag to pan, pinch / wheel to zoom,
## a little two-finger twist. Never free-flies: the diorama stays readable.

signal tapped(screen_pos: Vector2)

const DEFAULT_YAW := 45.0
const MAX_TWIST := 35.0
const ROTATE_STEP := 15.0
const PITCH := -50.0

var camera: Camera3D
var target := Vector3.ZERO
var distance := 34.0
var min_distance := 14.0
var max_distance := 150.0
var yaw := DEFAULT_YAW
## Rest angle chosen by the player (buttons, Q/E, right-drag, two-finger twist).
var yaw_rest := DEFAULT_YAW
var _rmb_drag := false
var bounds := AABB(Vector3(-30, 0, -30), Vector3(60, 1, 60))
var follow_node: Node3D = null
var input_enabled := true
var home_target := Vector3.ZERO
var home_distance := 60.0
var overview := true

var _touches := {}
var _drag_moved := 0.0
var _touch_start_time := 0
var _velocity := Vector2.ZERO
var _pinch_start_dist := 0.0
var _pinch_start_zoom := 0.0
var _pinch_start_angle := 0.0
var _pinch_start_yaw := 0.0
var _shake := 0.0
var _time := 0.0
var _focus_tween: Tween
var _cur_target := Vector3.ZERO
var _cur_distance := 34.0


func _ready() -> void:
	camera = Camera3D.new()
	camera.fov = 34.0
	camera.keep_aspect = Camera3D.KEEP_WIDTH
	camera.near = 0.5
	camera.far = 600.0
	add_child(camera)
	camera.current = true
	_cur_target = target
	_cur_distance = distance
	_update_transform(0.0)


func set_bounds(aabb: AABB) -> void:
	bounds = aabb


func world_per_pixel() -> float:
	var vp := get_viewport().get_visible_rect().size
	var width := 2.0 * _cur_distance * tan(deg_to_rad(camera.fov) * 0.5)
	return width / maxf(1.0, vp.x)


func _ground_axes() -> Array:
	var y := deg_to_rad(yaw)
	var right := Vector3(cos(y), 0, -sin(y))
	var up := Vector3(-sin(y), 0, -cos(y))
	return [right, up]


func pan_pixels(delta_px: Vector2) -> void:
	_cancel_focus(false)
	overview = false
	var axes := _ground_axes()
	var k := world_per_pixel()
	# screen "up" on the ground is foreshortened by the pitch
	var fore := 1.0 / maxf(0.3, sin(deg_to_rad(-PITCH)))
	target -= axes[0] * delta_px.x * k + axes[1] * (-delta_px.y) * k * fore
	_clamp_target()


func _clamp_target() -> void:
	# As the view widens, keep its pivot close to the active content. A close-up
	# can explore the whole unlocked area; an overview cannot drift into the sea.
	var inset := smoothstep(min_distance, max_distance, distance) * 0.80
	var low := bounds.position.lerp(home_target, inset)
	var high := bounds.end.lerp(home_target, inset)
	target.x = clampf(target.x, low.x, high.x)
	target.z = clampf(target.z, low.z, high.z)
	target.y = 0


func rotate_step(dir: int) -> void:
	overview = false
	yaw_rest = clampf(yaw_rest + ROTATE_STEP * dir, DEFAULT_YAW - MAX_TWIST, DEFAULT_YAW + MAX_TWIST)


func reset_rotation() -> void:
	yaw_rest = DEFAULT_YAW


func zoom_by(factor: float) -> void:
	_cancel_focus()
	overview = false
	distance = clampf(distance * factor, min_distance, max_distance)
	_clamp_target()


func _cancel_focus(clear_velocity := true) -> void:
	if _focus_tween and _focus_tween.is_valid():
		_focus_tween.kill()
	if clear_velocity: _velocity = Vector2.ZERO


func frame_home(duration := 0.8) -> void:
	if not _touches.is_empty() or _rmb_drag: return
	reset_rotation()
	focus_on(home_target, home_distance, duration)
	overview = true


## Perspective fit of the active buildings and roads, not the terrain/water.
## Event-driven (start, unlock, viewport resize); no per-frame scene traversal.
func fit_active(points: PackedVector3Array) -> void:
	if points.is_empty(): return
	var vp := get_viewport().get_visible_rect().size
	var safe := Rect2(vp * Vector2(0.10, 0.18), vp * Vector2(0.80, 0.64))
	var fit := fit_into(points, safe)
	home_target = fit[0]
	home_distance = fit[1]
	max_distance = home_distance # the complete active frame is the farthest useful view
	distance = clampf(distance, min_distance, max_distance)


## [pivot, distance] of the default-yaw view that shows every point inside `safe` (screen px), as close as possible.
## Pure: the camera is restored. fit_active's search, also used by the World Map to keep region labels in view.
func fit_into(points: PackedVector3Array, safe: Rect2) -> Array:
	var saved := camera.global_transform
	var centre := Vector3.ZERO
	for point in points: centre += point
	centre /= points.size()
	centre.y = 0.0
	var direction := Vector3(sin(deg_to_rad(DEFAULT_YAW)) * cos(deg_to_rad(PITCH)),
		-sin(deg_to_rad(PITCH)), cos(deg_to_rad(DEFAULT_YAW)) * cos(deg_to_rad(PITCH)))
	var lo := min_distance
	var hi := 1200.0
	var best := centre
	for i in 24:
		var d := (lo + hi) * 0.5
		var pivot := centre
		for k in 4:
			camera.global_position = pivot + direction * d
			camera.look_at(pivot, Vector3.UP)
			var box := _project_box(points)
			if box.size == Vector2.ZERO: break
			pivot += _ground_at(box.get_center()) - _ground_at(safe.get_center())
		camera.global_position = pivot + direction * d
		camera.look_at(pivot, Vector3.UP)
		var box := _project_box(points)
		if box.size != Vector2.ZERO and safe.encloses(box):
			hi = d
			best = pivot
		else:
			lo = d
	camera.global_transform = saved
	return [best, hi]


func _project_box(points: PackedVector3Array) -> Rect2:
	var box := Rect2()
	var first := true
	for point in points:
		if camera.is_position_behind(point): return Rect2()
		var p := camera.unproject_position(point)
		if first:
			box = Rect2(p, Vector2.ZERO)
			first = false
		else: box = box.expand(p)
	return box


func _ground_at(screen: Vector2) -> Vector3:
	var origin := camera.project_ray_origin(screen)
	var ray := camera.project_ray_normal(screen)
	return origin - ray * (origin.y / ray.y)


func focus_on(pos: Vector3, zoom := -1.0, duration := 0.8) -> void:
	if not _touches.is_empty() or _rmb_drag: return
	if UIKit.reduced_motion(): duration = 0.0
	overview = false
	follow_node = null
	_velocity = Vector2.ZERO
	if _focus_tween and _focus_tween.is_valid():
		_focus_tween.kill()
	_focus_tween = create_tween().set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_IN_OUT)
	_focus_tween.tween_property(self, "target", Vector3(pos.x, 0, pos.z), duration)
	if zoom > 0:
		_focus_tween.parallel().tween_property(self, "distance", clampf(zoom, min_distance, max_distance), duration)


func follow(node: Node3D, zoom := -1.0) -> void:
	_cancel_focus()
	overview = false
	follow_node = node
	if zoom > 0:
		distance = clampf(zoom, min_distance, max_distance)


func shake(amount := 0.4) -> void:
	if UIKit.reduced_motion(): return
	_shake = maxf(_shake, amount)


func _process(delta: float) -> void:
	_time += delta
	if follow_node and is_instance_valid(follow_node):
		target = target.lerp(Vector3(follow_node.global_position.x, 0, follow_node.global_position.z), minf(1.0, delta * 3.0))
	elif follow_node:
		follow_node = null
	var kp := Vector2(Input.get_axis("ui_left", "ui_right"), Input.get_axis("ui_up", "ui_down"))
	if kp.length() > 0.1 and input_enabled and not get_viewport().gui_get_focus_owner():
		follow_node = null
		pan_pixels(-kp * 700.0 * delta)
	if _touches.is_empty() and _velocity.length() > 1.0:
		pan_pixels(_velocity * delta)
		_velocity = _velocity.lerp(Vector2.ZERO, minf(1.0, delta * 5.0))
	if _touches.is_empty() and not _rmb_drag:
		yaw = lerpf(yaw, yaw_rest, minf(1.0, delta * 6.0))
	_update_transform(delta)


func _update_transform(delta: float) -> void:
	var k := 1.0 if delta <= 0 or UIKit.reduced_motion() else minf(1.0, delta * 10.0)
	_cur_target = _cur_target.lerp(target, k)
	_cur_distance = lerpf(_cur_distance, distance, k)
	_shake = maxf(0.0, _shake - delta * 1.5)
	# gentle "breathing" micro movement keeps the diorama alive
	var sway := 0.0 if UIKit.reduced_motion() else sin(_time * 0.35) * 0.35
	var y := deg_to_rad(yaw + sway)
	var p := deg_to_rad(PITCH)
	var dir := Vector3(sin(y) * cos(p), -sin(p), cos(y) * cos(p))
	var shake_off := Vector3(randf_range(-1, 1), randf_range(-1, 1), randf_range(-1, 1)) * _shake * 0.3
	global_position = _cur_target
	camera.global_position = _cur_target + dir * _cur_distance + shake_off
	camera.look_at(_cur_target + shake_off * 0.5, Vector3.UP)


func _unhandled_input(event: InputEvent) -> void:
	if not input_enabled:
		return
	if event is InputEventScreenTouch:
		var t := event as InputEventScreenTouch
		if t.pressed:
			_touches[t.index] = t.position
			_velocity = Vector2.ZERO
			if _touches.size() == 1:
				_drag_moved = 0.0
				_touch_start_time = Time.get_ticks_msec()
			elif _touches.size() == 2:
				_start_pinch()
		else:
			if _touches.has(t.index):
				_touches.erase(t.index)
				if _touches.is_empty() and _drag_moved < 14.0 and Time.get_ticks_msec() - _touch_start_time < 400:
					tapped.emit(t.position)
				elif _touches.size() == 1:
					_drag_moved = 99.0
	elif event is InputEventScreenDrag:
		var d := event as InputEventScreenDrag
		if not _touches.has(d.index):
			_touches[d.index] = d.position
		_touches[d.index] = d.position
		if _touches.size() == 1:
			_drag_moved += d.relative.length()
			if _drag_moved > 8.0:
				follow_node = null
				pan_pixels(d.relative)
				_velocity = d.relative / maxf(0.001, get_process_delta_time())
				_velocity = _velocity.limit_length(3000.0)
		elif _touches.size() >= 2:
			_update_pinch()
	elif event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.pressed and mb.button_index == MOUSE_BUTTON_WHEEL_UP:
			zoom_by(0.9)
		elif mb.pressed and mb.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			zoom_by(1.1)
		elif mb.button_index == MOUSE_BUTTON_RIGHT:
			_rmb_drag = mb.pressed
			if not mb.pressed:
				yaw_rest = yaw
	elif event is InputEventMouseMotion and _rmb_drag:
		yaw = clampf(yaw - (event as InputEventMouseMotion).relative.x * 0.25,
				DEFAULT_YAW - MAX_TWIST, DEFAULT_YAW + MAX_TWIST)
	elif event is InputEventKey and event.pressed and not event.echo:
		match (event as InputEventKey).keycode:
			KEY_Q:
				rotate_step(-1)
			KEY_E:
				rotate_step(1)
			KEY_R:
				reset_rotation()
			KEY_EQUAL, KEY_KP_ADD:
				zoom_by(0.85)
			KEY_MINUS, KEY_KP_SUBTRACT:
				zoom_by(1.15)
	elif event is InputEventMagnifyGesture:
		zoom_by(1.0 / (event as InputEventMagnifyGesture).factor)
	elif event is InputEventPanGesture:
		pan_pixels(-(event as InputEventPanGesture).delta * 12.0)


func _start_pinch() -> void:
	_cancel_focus()
	overview = false
	var pts := _touches.values()
	_pinch_start_dist = (pts[0] as Vector2).distance_to(pts[1])
	_pinch_start_zoom = distance
	_pinch_start_angle = ((pts[1] as Vector2) - (pts[0] as Vector2)).angle()
	_pinch_start_yaw = yaw
	follow_node = null


func _update_pinch() -> void:
	var pts := _touches.values()
	var dist := (pts[0] as Vector2).distance_to(pts[1])
	if _pinch_start_dist > 10.0:
		distance = clampf(_pinch_start_zoom * _pinch_start_dist / maxf(10.0, dist), min_distance, max_distance)
	_clamp_target()
	var ang := ((pts[1] as Vector2) - (pts[0] as Vector2)).angle()
	var dyaw := rad_to_deg(wrapf(ang - _pinch_start_angle, -PI, PI))
	yaw = clampf(_pinch_start_yaw - dyaw, DEFAULT_YAW - MAX_TWIST, DEFAULT_YAW + MAX_TWIST)
	yaw_rest = yaw
