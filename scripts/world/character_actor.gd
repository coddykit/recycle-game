class_name CharacterActor
extends Node3D
## Rigid-limb low-poly character with procedural animation
## (idle / walk / carry / wave / cheer). No skeleton needed: the Blender
## export keeps Torso, Arm_L/R and Leg_L/R as separately pivoted meshes.

signal arrived

var state := "idle"
var speed := 1.2
const LEG_M := 0.18       ## hip pivot to sole in model units (tools/blender/build_assets.py character())
const REACH := 0.644      ## sin of the leg angle at heel strike (0.7 rad, as before)
var model: Node3D
var _parts := {}
var _t := 0.0
var _phase := 0.0
var _target := Vector3.INF
var _after := "idle"
var _carry: Node3D


func setup(model_name: String, s := 1.3) -> CharacterActor:
	model = Models.instance(model_name)
	model.scale = Vector3.ONE * s
	add_child(model)
	for c in model.get_children():
		_parts[str(c.name)] = c
	_t = randf() * 10.0
	return self


func walk_to(p: Vector3, then_state := "idle", spd := 1.2) -> void:
	_target = Vector3(p.x, global_position.y, p.z)
	_after = then_state
	speed = spd
	if state != "carry":
		state = "walk"


func set_state(s: String) -> void:
	state = s
	_target = Vector3.INF


func carry(item: Node3D) -> void:
	if _carry:
		_carry.queue_free()
	_carry = item
	if item:
		add_child(item)
		item.position = Vector3(0, 0.42 * model.scale.y, -0.22 * model.scale.z)
		state = "carry"


func drop_carry() -> void:
	if _carry:
		_carry.queue_free()
	_carry = null
	if state == "carry":
		state = "idle"


func face(p: Vector3) -> void:
	var d := p - global_position
	d.y = 0
	if d.length() > 0.01:
		basis = Basis.looking_at(d.normalized(), Vector3.UP)


func _process(delta: float) -> void:
	_t += delta
	var moving := false
	if _target != Vector3.INF:
		var d := _target - global_position
		d.y = 0
		var step := speed * delta
		if d.length() <= step:
			global_position = Vector3(_target.x, global_position.y, _target.z)
			_target = Vector3.INF
			if state == "walk":
				state = _after
			arrived.emit()
		else:
			global_position += d.normalized() * step
			var want := Basis.looking_at(d.normalized(), Vector3.UP)
			# the unload fade scales walkers to zero while they still walk: slerp wants a normalized basis (engine
			# error every frame otherwise), so turn the rotation part and put the scale back
			var sc := basis.get_scale()
			if sc.x > 0.05:
				basis = basis.orthonormalized().slerp(want, minf(1.0, delta * 10.0)).orthonormalized().scaled(sc)
			moving = true
	_animate(delta, moving)


func _rot(part: String, x: float, z := 0.0) -> void:
	var n: Node3D = _parts.get(part)
	if n:
		n.rotation = Vector3(x, 0, z)


## Leg at gait phase p (cycles; < 0 = standing straight).
func _leg(part: String, p: float) -> void:
	var n: Node3D = _parts.get(part)
	if n == null:
		return
	if p < 0.0:
		n.rotation = Vector3.ZERO
		n.scale = Vector3.ONE
		return
	var u := clampf((p - 0.5) * 2.0, 0.0, 1.0)
	var len := 1.0 - 0.14 * sin(u * PI) if p >= 0.5 else 1.0
	var x := REACH * (1.0 - 4.0 * p) if p < 0.5 else REACH * (-1.0 + 2.0 * smoothstep(0.0, 1.0, u)) / len
	n.rotation = Vector3(asin(clampf(x, -0.95, 0.95)), 0, 0)
	n.scale = Vector3(1.0, len, 1.0)


func _animate(delta: float, moving: bool) -> void:
	var bob := 0.0
	if moving:
		# W68: the cycle follows metres walked (was 9 rad/m with sine legs: the stance foot slid ~14 % per step). Stance
		# half: the foot moves back exactly as far as the body moves forward (planted) and the body sinks L(1 - cos);
		# swing half: the leg shortens (lifts the shoe) and eases forward. +rotation.x = foot forward (model faces -z).
		var leg := LEG_M * model.scale.y
		_phase = fposmod(_phase + delta * speed / (4.0 * leg * REACH) * TAU, TAU)
		var pl := _phase / TAU
		var pr := fposmod(pl + 0.5, 1.0)
		_leg("Leg_L", pl)
		_leg("Leg_R", pr)
		var xs := REACH * (1.0 - 4.0 * minf(pl, pr))
		bob = -(1.0 - sqrt(maxf(1.0 - xs * xs, 0.0))) * leg
		if state == "carry":
			_rot("Arm_L", 1.25, 0.1)
			_rot("Arm_R", 1.25, -0.1)
		else:
			_rot("Arm_L", -cos(pl * TAU) * 0.55)
			_rot("Arm_R", -cos(pr * TAU) * 0.55)
	else:
		_leg("Leg_L", -1.0)
		_leg("Leg_R", -1.0)
		match state:
			"wave":
				_rot("Arm_L", sin(_t * 1.5) * 0.05)
				_rot("Arm_R", 0.0, deg_to_rad(150) + sin(_t * 10.0) * 0.35)
			"cheer":
				_rot("Arm_L", 0.0, -deg_to_rad(155) + sin(_t * 12.0) * 0.2)
				_rot("Arm_R", 0.0, deg_to_rad(155) - sin(_t * 12.0) * 0.2)
				bob = absf(sin(_t * 9.0)) * 0.08
			"carry":
				_rot("Arm_L", 1.25, 0.1)
				_rot("Arm_R", 1.25, -0.1)
			_:
				_rot("Arm_L", sin(_t * 1.6) * 0.06, -0.05)
				_rot("Arm_R", -sin(_t * 1.6) * 0.06, 0.05)
				bob = sin(_t * 2.0) * 0.006
	model.position.y = bob
