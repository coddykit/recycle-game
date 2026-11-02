extends RefCounted
## coddy_anim · MMGait — Gait for MultiMesh crowds (W68): the same rules as gait.gd (phase advances by the distance the
## figure ACTUALLY moved, standing still stops the cycle, heading eases towards the motion — no yaw flips) for N
## instances in packed arrays, no object per person. The game moves its people as before and asks for the body
## transform: two steps per cycle give a small bob, the body rolls a little over the stepping foot; both fade out
## while standing. Yaw 0 = facing +Z (gait.gd). `facing_minus_z` for meshes authored facing −Z (Basis.looking_at).

var walk_stride := 1.15     ## metres per full cycle (two steps)
var move_speed := 0.12      ## m/s below which a figure counts as standing
var turn_rate := 9.0        ## 1/s heading ease
var max_turn := 6.0         ## rad/s: a turn-back takes ~0.5 s, never a flip
var bob_h := 0.05           ## m, peak body lift per step
var sway := 0.05            ## rad, body roll over the stepping foot
var facing_minus_z := false

var last := PackedVector3Array()
var yaw := PackedFloat32Array()
var phase := PackedFloat32Array()    ## cycles (0..1)
var amp := PackedFloat32Array()      ## 0 standing .. 1 walking (eased)


func resize(n: int) -> void:
	last.resize(n)
	yaw.resize(n)
	phase.resize(n)
	amp.resize(n)


## Places figure i without a step: no bob, facing yaw0 (e.g. towards where it is about to walk).
func reset(i: int, pos: Vector3, yaw0: float, phase0: float = 0.0) -> void:
	last[i] = pos
	yaw[i] = yaw0
	phase[i] = phase0
	amp[i] = 0.0


## Heading of a direction on the ground (yaw 0 = +Z).
static func yaw_of(dir: Vector3) -> float:
	return atan2(dir.x, dir.z)


## Advances figure i to pos and returns its body transform (multiply the mesh's own xform after it). `idle_yaw`: where
## a standing figure turns to (NAN = keeps its heading) — e.g. round before it walks back.
func step(i: int, pos: Vector3, dt: float, idle_yaw: float = NAN) -> Transform3D:
	var d := Vector3(pos.x - last[i].x, 0.0, pos.z - last[i].z)
	var dist := d.length()
	last[i] = pos
	var moving := dt > 0.0 and dist / dt > move_speed
	if moving:
		_turn(i, atan2(d.x, d.z), dt)
		phase[i] = fposmod(phase[i] + dist / walk_stride, 1.0)
	elif not is_nan(idle_yaw):
		_turn(i, idle_yaw, dt)
	amp[i] = move_toward(amp[i], 1.0 if moving else 0.0, dt * 5.0)
	var s := sin(phase[i] * TAU)
	var b := Basis(Vector3.UP, yaw[i] + (PI if facing_minus_z else 0.0))
	if sway > 0.0:
		b = b * Basis(Vector3.BACK, s * sway * amp[i])
	return Transform3D(b, pos + Vector3(0.0, absf(s) * bob_h * amp[i], 0.0))


func _turn(i: int, target: float, dt: float) -> void:
	var lim := max_turn * dt
	yaw[i] = wrapf(yaw[i] + clampf(angle_difference(yaw[i], target) * (1.0 - exp(-turn_rate * dt)), -lim, lim), -PI, PI)
