class_name FX
## Lightweight one-shot particle effects (CPUParticles3D: works everywhere,
## tiny particle counts for mobile).

static var _mat: StandardMaterial3D
static var _box: BoxMesh
static var _ball: SphereMesh
static var _pool := {}


static func _material() -> StandardMaterial3D:
	if _mat == null:
		_mat = StandardMaterial3D.new()
		_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		_mat.vertex_color_use_as_albedo = true
	return _mat


static func _box_mesh() -> BoxMesh:
	if _box == null:
		_box = BoxMesh.new()
		_box.size = Vector3(0.16, 0.16, 0.05)
	return _box


static func _ball_mesh() -> SphereMesh:
	if _ball == null:
		_ball = SphereMesh.new()
		_ball.radius = 0.16
		_ball.height = 0.32
		_ball.radial_segments = 6
		_ball.rings = 3
	return _ball


static func burst(parent: Node, pos: Vector3, colors: Array, amount: int, speed: Vector2,
		lifetime: float, gravity: float, mesh: Mesh, spread := 60.0, scale_range := Vector2(0.8, 1.4)) -> CPUParticles3D:
	# pooled: identical bursts reuse finished emitters instead of allocating
	var key := str([colors, amount, speed, lifetime, gravity, spread, scale_range, mesh.get_instance_id(),
			parent.get_instance_id()])
	var free_list: Array = _pool.get(key, [])
	while free_list.size() > 0:
		var reuse: CPUParticles3D = free_list.pop_back()
		if is_instance_valid(reuse):
			reuse.global_position = pos
			reuse.restart()
			return reuse
	_pool[key] = free_list
	var p := CPUParticles3D.new()
	p.one_shot = true
	p.explosiveness = 0.95
	p.amount = maxi(1, int(round(amount * GameManager.particle_scale())))
	p.lifetime = lifetime
	p.mesh = mesh
	p.material_override = _material()
	p.direction = Vector3.UP
	p.spread = spread
	p.initial_velocity_min = speed.x
	p.initial_velocity_max = speed.y
	p.gravity = Vector3(0, -gravity, 0)
	p.angular_velocity_min = -360
	p.angular_velocity_max = 360
	p.scale_amount_min = scale_range.x
	p.scale_amount_max = scale_range.y
	p.damping_min = 0.5
	p.damping_max = 1.5
	var curve := Curve.new()
	curve.add_point(Vector2(0, 1))
	curve.add_point(Vector2(0.7, 0.8))
	curve.add_point(Vector2(1, 0))
	p.scale_amount_curve = curve
	var grad := Gradient.new()
	var offs := PackedFloat32Array()
	var cols := PackedColorArray()
	for i in colors.size():
		offs.append(float(i) / float(colors.size()))
		cols.append(colors[i])
	grad.offsets = offs
	grad.colors = cols
	grad.interpolation_mode = Gradient.GRADIENT_INTERPOLATE_CONSTANT
	p.color_initial_ramp = grad
	p.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(p)
	p.global_position = pos
	p.emitting = true
	p.finished.connect(func():
		if is_instance_valid(p):
			p.emitting = false
			(_pool[key] as Array).append(p))
	return p


static func confetti(parent: Node, pos: Vector3) -> void:
	burst(parent, pos + Vector3.UP, [Color("ffd23f"), Color("ff4fd8"), Color("3cf2ff"), Color("5cff8a"), Color("ff8a3c")],
			36, Vector2(4, 7), 1.6, 9.0, _box_mesh(), 50.0)


static func dust(parent: Node, pos: Vector3) -> void:
	burst(parent, pos + Vector3.UP * 0.2, [Color("eadfc8"), Color("cfc2a8")], 14, Vector2(1.5, 3), 0.8, 1.0,
			_ball_mesh(), 80.0, Vector2(1.0, 2.2))


static func sparkle(parent: Node, pos: Vector3) -> void:
	burst(parent, pos + Vector3.UP * 0.8, [Color("fff3b0"), Color("ffd23f"), Color("ffffff")], 22, Vector2(2, 4.5), 1.0,
			3.0, _box_mesh(), 90.0, Vector2(0.6, 1.2))


static func smoke(parent: Node, pos: Vector3) -> void:
	burst(parent, pos + Vector3.UP * 0.8, [Color("5a5f6a"), Color("8c929c")], 12, Vector2(0.6, 1.4), 1.6, -1.2,
			_ball_mesh(), 25.0, Vector2(1.2, 2.4))


static func coins(parent: Node, pos: Vector3) -> void:
	burst(parent, pos + Vector3.UP * 1.2, [Color("ffd23f"), Color("f2c14e"), Color("fff3b0")], 16, Vector2(3, 5.5), 1.0,
			10.0, _box_mesh(), 35.0, Vector2(1.0, 1.6))


static func fireworks(parent: Node, pos: Vector3) -> void:
	var tree := parent.get_tree()
	for i in 5:
		var off := Vector3(randf_range(-6, 6), randf_range(3, 7), randf_range(-6, 6))
		var cols: Array = [[Color("ffd23f"), Color("ff8a3c")], [Color("3cf2ff"), Color("ffffff")], [Color("ff4fd8"), Color("b36bff")],
				[Color("5cff8a"), Color("fff3b0")]].pick_random()
		burst(parent, pos + off, cols, 40, Vector2(5, 8), 1.4, 4.0, _box_mesh(), 180.0)
		await tree.create_timer(0.22).timeout
		if not is_instance_valid(parent):
			return


## Little crates / a passenger hopping from the vehicle to the destination.
static func unload(parent: Node, from: Vector3, to: Vector3, passenger: bool, count: int) -> void:
	for i in count:
		var model := Models.instance("passenger" if passenger else "crate")
		parent.add_child(model)
		model.global_position = from + Vector3(0, 0.4, 0)
		model.scale = Vector3.ONE * (1.2 if passenger else 1.0)
		var dest := to + Vector3(randf_range(-0.8, 0.8), 0, randf_range(-0.8, 0.8))
		var arc := func(f: float) -> void:
			if is_instance_valid(model):
				var p := from.lerp(dest, f)
				p.y += sin(f * PI) * 1.4 + 0.2
				model.global_position = p
				if not passenger:
					model.rotation.y = f * TAU
		var t := model.create_tween()
		t.tween_interval(i * 0.15)
		t.tween_method(arc, 0.0, 1.0, 0.55)
		t.tween_property(model, "scale", Vector3.ZERO, 0.25).set_delay(0.15)
		t.tween_callback(model.queue_free)
