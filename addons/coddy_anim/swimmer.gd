extends RefCounted
## coddy_anim · Swimmer — a person in the water: swim (moving) / tread (still, treading water) / struggle (in trouble,
## irregular flailing — set by the game). The game moves root (x, z); the swimmer owns root.position.y: it rides the
## wave given as a Callable wave(x, z, t) -> metres (use the SAME maths as the water shader so the head stays at the
## surface). Skeletal clips swim / tread / struggle (swim rate follows the ground speed / swim_stride), else the
## FigureRig poses swim / tread / flail. Same agent API as CrowdAnimator.

const Gait = preload("gait.gd")
const FigureRig = preload("figure_rig.gd")

const SWIM := "swim"
const TREAD := "tread"
const STRUGGLE := "struggle"

var root: Node3D
var gait := Gait.new()
var player: AnimationPlayer
var mesh_node: MeshInstance3D
var notifier: VisibleOnScreenNotifier3D
var wave := Callable()          ## wave.call(x, z, t) -> float; none = flat water at base_y
var base_y := 0.0               ## water level (root y at a zero wave)
var clips := {"swim": "swim", "tread": "tread", "struggle": "struggle"}
var clip_len := {}
var struggling := false
var state := TREAD
var state_changes := 0
var skeletal_allowed := true
var on_impostor := Callable()
var play_speed := 1.0
var t := 0.0                    ## wave clock (keep it equal to the shader TIME if the game passes it in)
var _seed := 0.0
var _poses := {}


func setup(node: Node3D, seed_: int, opts: Dictionary = {}) -> void:
	root = node
	wave = opts.get("wave", Callable())
	base_y = float(opts.get("base_y", root.position.y))
	gait.swim_stride = float(opts.get("swim_stride", gait.swim_stride))
	if opts.has("clips"):
		for k in opts.clips:
			clips[k] = opts.clips[k]
	var rng := RandomNumberGenerator.new()
	rng.seed = seed_
	_seed = rng.randf() * 10.0
	gait.phase = rng.randf()
	player = _find(root, "AnimationPlayer")
	notifier = _find(root, "VisibleOnScreenNotifier3D")
	if player != null:
		player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
		for k in clips:
			if player.has_animation(clips[k]):
				clip_len[clips[k]] = player.get_animation(clips[k]).length
	else:
		mesh_node = opts.get("mesh_node", _find(root, "MeshInstance3D"))
		if mesh_node != null and mesh_node.mesh is ArrayMesh:
			var id: String = opts.get("rig_id", str(mesh_node.mesh.get_instance_id()))
			for kind in ["swim", "tread", "flail"]:
				_poses[kind] = FigureRig.meshes(id, mesh_node.mesh, kind)
	gait.reset(world_pos(), 0.0)


func world_pos() -> Vector3:
	return root.global_position if root.is_inside_tree() else root.position


func is_skeletal() -> bool:
	return player != null


func set_skeletal_allowed(on: bool) -> void:
	if on == skeletal_allowed:
		return
	skeletal_allowed = on
	if on_impostor.is_valid():
		on_impostor.call(root, not on)


func set_struggling(on: bool) -> void:
	struggling = on


## Surface height under (x, z) at the wave clock.
func surface(x: float, z: float) -> float:
	return base_y + (float(wave.call(x, z, t)) if wave.is_valid() else 0.0)


func anim_update(dt: float) -> void:
	t += dt
	var p := world_pos()
	gait.step(p, dt, true, NAN)
	var next := STRUGGLE if struggling else (SWIM if gait.gait == "swim" else TREAD)
	if next != state:
		state = next
		state_changes += 1
	var y := surface(p.x, p.z)
	var roll := 0.0
	var pitch := 0.0
	match state:
		STRUGGLE:
			var fb := FigureRig.flail_body(t, _seed)
			y += fb.x
			roll = fb.y
		SWIM:
			pitch = 1.2   # body near horizontal, face down
			y -= 0.05
		_:
			y += 0.02 * sin(t * 2.4 + _seed)   # treading: small rhythmic lift on top of the wave
	root.position.y = y
	root.rotation = Vector3(pitch, gait.yaw, roll)
	if player != null:
		var clip: String = clips[state]
		if player.has_animation(clip):
			if player.current_animation != clip:
				player.play(clip, 0.25)
			play_speed = Gait.clip_speed_scale(gait.inst_speed, gait.swim_stride, float(clip_len.get(clip, 1.0))) \
					if state == SWIM else 1.0
			player.speed_scale = play_speed
			if skeletal_allowed:
				player.advance(dt)
	elif mesh_node != null:
		var kind := "flail" if state == STRUGGLE else state
		var list: Array = _poses.get(kind, [])
		if not list.is_empty():
			var i := FigureRig.flail_pose(t, _seed) if state == STRUGGLE else gait.pose(list.size())
			mesh_node.mesh = list[i % list.size()]


static func _find(n: Node, cls: String) -> Node:
	if n.is_class(cls):
		return n
	for c in n.get_children():
		var f := _find(c, cls)
		if f != null:
			return f
	return null
