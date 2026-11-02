class_name VehicleActor
extends Node3D
## Visual representation of one owned vehicle. Reads its job from
## ContractManager each frame and glides along the road polyline.

const LANE := 0.5
const ICON_RISK := preload("res://assets/icons/risk.svg")
const ICON_WRENCH := preload("res://assets/icons/wrench.svg")

var vehicle_id := ""
var world: Node  # World
var model: Node3D
var body: Node3D
var wheels: Array[Node3D] = []
var attachments: Node3D
var bubble: Sprite3D
var selection_ring: MeshInstance3D
var dust: CPUParticles3D
var moving := false
var _paint := ""
var _job_key := ""
var _pts := PackedVector3Array()
var _cum := PackedFloat32Array()
var _total_len := 0.0
var _last_pos := Vector3.ZERO
var _time := 0.0
var _heading := Vector3.FORWARD
var _spawn_t := -1.0
var _upgrade_sig := ""
var _wheel_r := 0.2
var _lane := LANE         # W68: a wide model (armored 1.28 m) rides further right, clear of the oncoming lane
var _built_low := false  # the model / attachments were baked into one mesh each (LowBatch, Low profile)


func setup(v: Dictionary, w: Node, spawn_anim: bool) -> void:
	vehicle_id = v.id
	world = w
	name = "Vehicle_" + v.id
	_build_model(v)
	attachments = Node3D.new()
	add_child(attachments)
	_build_bubble()
	_build_ring()
	_build_dust()
	refresh(v)
	var p: Vector3 = world.parking_transform(vehicle_id).origin
	global_position = p
	_last_pos = p
	if spawn_anim:
		_spawn_t = 0.0
		model.position.y = 7.0


func _build_model(v: Dictionary) -> void:
	if model:
		model.queue_free()
	var d := Data.vehicle_def(v.def)
	model = Models.instance(v.def, Models.paint_material(Data.color(v.paint)))
	_paint = v.paint
	add_child(model)
	wheels.clear()
	body = null
	for c in model.get_children():
		if c.name.begins_with("Wheel"):
			wheels.append(c)
		elif c.name == "Body":
			body = c
	var m := Data.meta(v.def)
	_wheel_r = 0.2 if wheels.is_empty() else wheels[0].position.y
	set_meta("height", float(m.max[1]))
	set_meta("length", float(m.size[2]))
	set_meta("width", float(m.size[0]))
	_lane = maxf(LANE, float(m.size[0]) * 0.45 + 0.05)
	# Low: body + wheels are ONE mesh (one draw instead of five; the wheels stop spinning)
	_built_low = GameManager.quality() == "low"
	if _built_low and LowBatch.merge_tree(model) != null:
		wheels.clear()
		body = null
	if d.is_empty():
		return


## Quality changed at runtime: rebuild the model / attachments in the other form.
func apply_low(low: bool) -> void:
	if low == _built_low:
		return
	var v := VehicleManager.get_vehicle(vehicle_id)
	if v.is_empty():
		return
	_build_model(v)
	_upgrade_sig = ""
	refresh(v)


func _build_bubble() -> void:
	bubble = Sprite3D.new()
	bubble.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	bubble.pixel_size = 0.012
	bubble.no_depth_test = true
	bubble.render_priority = 3
	bubble.visible = false
	bubble.texture = ICON_RISK
	add_child(bubble)


func _build_ring() -> void:
	selection_ring = MeshInstance3D.new()
	var tm := TorusMesh.new()
	tm.inner_radius = 1.05
	tm.outer_radius = 1.3
	tm.rings = 18
	tm.ring_segments = 4
	selection_ring.mesh = tm
	selection_ring.material_override = Models.flat_material(Color("ffd23f"), 1.0)
	selection_ring.position.y = 0.06
	selection_ring.scale = Vector3(1, 0.1, 1)
	selection_ring.visible = false
	selection_ring.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(selection_ring)


func _build_dust() -> void:
	dust = CPUParticles3D.new()
	dust.amount = maxi(1, int(round(10 * GameManager.particle_scale())))
	dust.lifetime = 0.7
	dust.local_coords = false
	dust.emitting = false
	var sm := SphereMesh.new()
	sm.radius = 0.12
	sm.height = 0.24
	sm.radial_segments = 6
	sm.rings = 3
	dust.mesh = sm
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.albedo_color = Color(0.92, 0.86, 0.74)
	mat.vertex_color_use_as_albedo = true
	dust.material_override = mat
	dust.direction = Vector3(0, 1, 0)
	dust.spread = 30.0
	dust.initial_velocity_min = 0.3
	dust.initial_velocity_max = 0.7
	dust.gravity = Vector3(0, 0.4, 0)
	dust.scale_amount_min = 0.6
	dust.scale_amount_max = 1.2
	var curve := Curve.new()
	curve.add_point(Vector2(0, 0.6))
	curve.add_point(Vector2(1, 0))
	dust.scale_amount_curve = curve
	dust.position = Vector3(0, 0.1, 0.6)
	dust.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(dust)


## Rebuild paint / upgrade attachments after a change.
func refresh(v: Dictionary) -> void:
	if v.paint != _paint:
		Models.apply_material(model, Models.paint_material(Data.color(v.paint)))
		_paint = v.paint
	var sig := JSON.stringify(v.upgrades)
	if sig == _upgrade_sig:
		return
	_upgrade_sig = sig
	for c in attachments.get_children():
		attachments.remove_child(c)
		c.queue_free()
	var m := Data.meta(v.def)
	var top := float(m.max[1])
	var front := float(m.min[2])
	var back := float(m.max[2])
	var lv := func(k: String) -> int: return VehicleManager.upgrade_level(v, k)
	if lv.call("capacity") >= 2:
		_attach("att_roofrack", Vector3(0, top - 0.02, (front + back) * 0.15), 1.0)
	if lv.call("engine") >= 2:
		_attach("att_spoiler", Vector3(0, top - 0.05 if lv.call("capacity") < 2 else top + 0.3, back - 0.25), 1.0)
	if lv.call("navigation") >= 2:
		_attach("att_antenna", Vector3(0.25, top - 0.02, front * 0.4), 0.9)
	if lv.call("reliability") >= 2:
		_attach("att_bullbar", Vector3(0, 0.05, front - 0.04), 1.0)
	if lv.call("suspension") >= 2 or lv.call("maintenance") >= 3:
		_attach("att_lightbar", Vector3(0, top, front * 0.6), 1.0)
	if body:
		body.position.y = 0.025 * lv.call("suspension")
	if _built_low:
		LowBatch.merge_tree(attachments)


func _attach(model_name: String, pos: Vector3, s: float) -> void:
	var a := Models.instance(model_name)
	a.position = pos
	a.scale = Vector3.ONE * s
	attachments.add_child(a)


var _blob: MeshInstance3D


## Soft fake shadow used when real-time shadows are off (Low quality).
func set_blob_shadow(on: bool) -> void:
	if on and _blob == null:
		_blob = MeshInstance3D.new()
		var q := QuadMesh.new()
		q.size = Vector2(float(get_meta("width", 1.0)) * 1.25, float(get_meta("length", 1.6)) * 1.15)
		q.orientation = PlaneMesh.FACE_Y
		_blob.mesh = q
		var m := StandardMaterial3D.new()
		m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		m.albedo_color = Color(0, 0, 0, 0.28)
		_blob.material_override = m
		_blob.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		_blob.position.y = 0.06
		add_child(_blob)
	if _blob:
		_blob.visible = on


func set_selected(on: bool) -> void:
	selection_ring.visible = on


func flash() -> void:
	var t := create_tween()
	model.scale = Vector3.ONE * 1.25
	t.tween_property(model, "scale", Vector3.ONE, 0.45).set_trans(Tween.TRANS_ELASTIC).set_ease(Tween.EASE_OUT)


# --- path following ----------------------------------------------------------------
func _set_path(nodes: Array) -> void:
	var g := RegionManager.graph
	var raw := g.path_points(nodes)
	_pts = PackedVector3Array()
	var n := raw.size()
	for i in n:
		var dir_sum := Vector3.ZERO
		if i > 0:
			dir_sum += (raw[i] - raw[i - 1]).normalized()
		if i < n - 1:
			dir_sum += (raw[i + 1] - raw[i]).normalized()
		dir_sum.y = 0
		var off := Vector3.ZERO
		if dir_sum.length() > 0.01:
			var d := dir_sum.normalized()
			off = Vector3(-d.z, 0, d.x) * _lane
		_pts.append(raw[i] + off)
	_cum = PackedFloat32Array()
	_total_len = 0.0
	_cum.append(0.0)
	for i in range(1, _pts.size()):
		_total_len += _pts[i - 1].distance_to(_pts[i])
		_cum.append(_total_len)


func _sample(t: float) -> Vector3:
	if _pts.size() == 0:
		return global_position
	if _pts.size() == 1 or _total_len <= 0.0:
		return _pts[0]
	var d := clampf(t, 0.0, 1.0) * _total_len
	for i in range(1, _pts.size()):
		if d <= _cum[i]:
			var seg := _cum[i] - _cum[i - 1]
			var f := 0.0 if seg <= 0 else (d - _cum[i - 1]) / seg
			return _pts[i - 1].lerp(_pts[i], f)
	return _pts[_pts.size() - 1]


func _process(delta: float) -> void:
	_time += delta
	var job: Dictionary = ContractManager.get_job_for_vehicle(vehicle_id)
	var target := global_position
	var paused := false
	if not job.is_empty():
		var ph: String = "out" if job.phase == "unload" else str(job.phase)
		var key: String = "%s_%s" % [job.id, ph]
		if key != _job_key:
			_job_key = key
			_set_path(job.path if ph == "out" else job.ret_path)
		# ease in/out at the ends of the trip so vehicles don't snap
		var p := ContractManager.progress(job)
		target = _sample(_smooth(p))
		paused = job.get("pending_event", false)
	else:
		_job_key = ""
		target = world.parking_transform(vehicle_id).origin
	var prev := global_position
	if job.is_empty():
		global_position = global_position.lerp(target, minf(1.0, delta * 3.0))
	else:
		global_position = target
	var vel := global_position - prev
	var speed := vel.length() / maxf(delta, 0.0001)
	moving = speed > 0.15
	# heading
	var desired := _heading
	if vel.length() > 0.002:
		desired = Vector3(vel.x, 0, vel.z).normalized()
	elif job.is_empty():
		desired = -world.parking_transform(vehicle_id).basis.z
	if desired.length() > 0.1:
		_heading = _heading.slerp(desired, minf(1.0, delta * 8.0)).normalized()
		if _heading.length() > 0.1:
			basis = Basis.looking_at(_heading, Vector3.UP)
	# wheels + bob
	for w in wheels:
		w.rotate_x(-vel.length() / maxf(0.05, _wheel_r))
	if model:
		var bob := sin(_time * 18.0) * 0.015 if moving else 0.0
		if _spawn_t >= 0.0:
			_spawn_t += delta
			var h := 7.0 * maxf(0.0, 1.0 - _spawn_t / 0.45)
			model.position.y = h
			if _spawn_t >= 0.45:
				_spawn_t = -1.0
				model.position.y = 0.0
				world.fx_dust(global_position)
				flash()
		else:
			model.position.y = bob
	dust.emitting = moving and speed > 0.8
	bubble.visible = paused
	if paused:
		bubble.position = Vector3(0, float(get_meta("height", 1.0)) + 1.0 + sin(_time * 6.0) * 0.15, 0)
		var s := 1.0 + sin(_time * 8.0) * 0.08
		bubble.scale = Vector3.ONE * s
	var v := VehicleManager.get_vehicle(vehicle_id)
	if not v.is_empty() and v.state == "repair":
		bubble.visible = true
		bubble.texture = ICON_WRENCH
		bubble.position = Vector3(0, float(get_meta("height", 1.0)) + 0.9, 0)
	elif paused:
		bubble.texture = ICON_RISK
	if selection_ring.visible:
		selection_ring.rotation.y += delta * 1.5


static func _smooth(p: float) -> float:
	# gentle ease at both ends, linear in the middle (continuous, 0 -> 1)
	var e := 0.05
	var g: float
	if p < e:
		g = p * p / (2.0 * e)
	elif p > 1.0 - e:
		var q := 1.0 - p
		g = (1.0 - e) - q * q / (2.0 * e)
	else:
		g = p - e * 0.5
	return clampf(g / (1.0 - e), 0.0, 1.0)
