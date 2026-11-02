extends RefCounted
## coddy_anim · Gait — one figure's locomotion state, driven by the distance ACTUALLY moved (generalised from the
## lifeguard-hq Walker). The game moves the node; the gait reads the position: phase advances by distance / stride, so
## feet never slide, standing still stops the cycle, and the heading eases towards the velocity (yaw 0 = facing +Z).

var walk_stride := 1.15     ## metres per full cycle (two steps) of the walk clip / pose cycle
var run_stride := 2.1
var swim_stride := 1.5
var run_speed := 1.5        ## m/s above which the gait is a run
var move_speed := 0.12      ## m/s below which the figure counts as standing
var turn_rate := 10.0       ## 1/s heading ease

var last := Vector3.ZERO
var has_last := false
var phase := 0.0            ## cycles (0..1)
var speed := 0.0            ## smoothed ground speed, m/s
var inst_speed := 0.0       ## this step's ground speed, m/s (what the clip rate follows)
var yaw := 0.0
var gait := "idle"          ## idle | walk | run | swim | tread


func reset(pos: Vector3, yaw0: float = 0.0) -> void:
	last = pos
	has_last = true
	yaw = yaw0
	speed = 0.0
	inst_speed = 0.0
	gait = "idle"


## Stride (m per cycle) of the current gait; 0 for standing gaits.
func stride() -> float:
	match gait:
		"walk":
			return walk_stride
		"run":
			return run_stride
		"swim":
			return swim_stride
	return 0.0


## `swim`: deep water (crawl instead of walk, tread instead of idle). `idle_yaw`: where it faces while standing
## (NAN = keep the current heading).
func step(pos: Vector3, dt: float, swim: bool = false, idle_yaw: float = NAN) -> void:
	if not has_last:
		reset(pos, 0.0 if is_nan(idle_yaw) else idle_yaw)
		return
	if dt <= 0.0:
		return
	var d := Vector3(pos.x - last.x, 0.0, pos.z - last.z)
	var dist := d.length()
	last = pos
	inst_speed = dist / dt
	speed = lerpf(speed, inst_speed, 1.0 - exp(-12.0 * dt))
	var k := 1.0 - exp(-turn_rate * dt)
	if inst_speed > move_speed and dist > 0.0005:
		yaw = lerp_angle(yaw, atan2(d.x, d.z), k)
		if swim:
			gait = "swim"
		elif speed > run_speed:
			gait = "run"
		else:
			gait = "walk"
		phase = fposmod(phase + dist / stride(), 1.0)
	else:
		inst_speed = 0.0
		if not is_nan(idle_yaw):
			yaw = lerp_angle(yaw, idle_yaw, k)
		gait = "tread" if swim else "idle"
		if swim:
			phase = fposmod(phase + dt, 1.0)


## Playback speed for a skeletal clip so the feet match the ground: (m/s) / (m per clip loop) * (s per clip loop).
static func clip_speed_scale(ground_speed: float, stride_m: float, clip_len_s: float) -> float:
	if stride_m <= 0.0 or clip_len_s <= 0.0:
		return 1.0
	return ground_speed * clip_len_s / stride_m


func pose(count: int = 8) -> int:
	return int(fposmod(phase, 1.0) * float(count)) % count
