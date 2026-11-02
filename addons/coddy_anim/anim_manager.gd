extends Node
## coddy_anim · AnimManager — ONE node updates every figure (no per-character _process). Each agent is stepped at the
## tier rate (AnimLod.HZ: 15 / 30 / 60 Hz) with its own elapsed time; due times are staggered so the work is spread over
## frames (time slicing). Off-screen agents are not stepped at all. The skeletal cap keeps only the N agents nearest the
## camera (or `focus`) animated; the rest freeze / switch to impostors via set_skeletal_allowed(false).
## Visibility: the agent's VisibleOnScreenNotifier3D if it has one, else `visibility_fn` (pos -> bool), else the
## camera frustum, else visible.
##   var am := AnimManager.new(); add_child(am); am.tier = quality; am.camera = cam; am.add(animator)
## Tests (or a game that drives its own clock) set auto_process = false and call step(dt).

const AnimLod = preload("anim_lod.gd")

var tier := AnimLod.LOW
var camera: Camera3D
var focus := Vector3.INF            ## distance origin for the skeletal cap (INF = the camera)
var visibility_fn := Callable()
var auto_process := true
var cap_refresh_s := 0.25
var agents: Array = []              ## untyped: CrowdAnimator / Swimmer / any object with the agent API
var updates := 0                    ## anim_update calls so far (perf counter / tests)
var skeletal_live := 0              ## agents currently allowed a live skeleton
var _due := PackedFloat64Array()
var _last := PackedFloat64Array()
var _now := 0.0
var _cap_t := 0.0


func _ready() -> void:
	set_process(auto_process)


func _process(delta: float) -> void:
	if auto_process:
		step(delta)


func add(agent) -> void:
	agents.append(agent)
	var interval := 1.0 / float(AnimLod.hz(tier))
	_due.append(_now + interval * float(agents.size() % 8) / 8.0)   # staggered phase
	_last.append(_now)
	_cap_t = 0.0


func remove(agent) -> void:
	var i := agents.find(agent)
	if i < 0:
		return
	agents.remove_at(i)
	_due.remove_at(i)
	_last.remove_at(i)


func is_visible_agent(agent) -> bool:
	var n = agent.notifier
	if n != null and is_instance_valid(n) and n.is_inside_tree():
		return n.is_on_screen()
	var p: Vector3 = agent.world_pos()
	if visibility_fn.is_valid():
		return bool(visibility_fn.call(p))
	if camera != null and is_instance_valid(camera) and camera.is_inside_tree():
		return camera.is_position_in_frustum(p)
	return true


func step(dt: float) -> void:
	_now += dt
	_cap_t -= dt
	if _cap_t <= 0.0:
		_cap_t = cap_refresh_s
		_apply_cap()
	var interval := 1.0 / float(AnimLod.hz(tier))
	for i in agents.size():
		if _now + 1e-9 < _due[i]:
			continue
		_due[i] = maxf(_due[i] + interval, _now + interval * 0.5)
		var agent = agents[i]
		if not is_visible_agent(agent):
			_last[i] = _now   # off screen: no step, and no catch-up burst when it returns
			continue
		var adt := minf(_now - _last[i], 0.25)
		_last[i] = _now
		agent.anim_update(adt)
		updates += 1


func _apply_cap() -> void:
	var origin := focus
	if origin == Vector3.INF and camera != null and is_instance_valid(camera):
		origin = camera.global_position if camera.is_inside_tree() else camera.position
	if origin == Vector3.INF:
		origin = Vector3.ZERO
	var skel: Array = []
	for a in agents:
		if a.is_skeletal():
			if is_visible_agent(a):
				skel.append([(a.world_pos() as Vector3).distance_squared_to(origin), a])
			else:
				a.set_skeletal_allowed(false)
	skel.sort_custom(func(x, y): return float(x[0]) < float(y[0]))
	var cap := AnimLod.skeletal_cap(tier)
	skeletal_live = 0
	for k in skel.size():
		var on: bool = k < cap
		skel[k][1].set_skeletal_allowed(on)
		if on:
			skeletal_live += 1
