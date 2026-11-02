extends RefCounted
## coddy_anim · CrowdAnimator — people / animals on foot. The game moves `root` (x, z); the animator turns the movement
## into a gait, a heading and a pose. Two back ends, picked in setup():
##   skeletal  — an AnimationPlayer under root (Meshy GLB: clips idle / walk / run / work). The player is switched to
##               MANUAL process and advanced by AnimManager at the tier rate; walk/run playback speed =
##               ground speed × clip length / stride_m, so feet do not slide.
##   procedural — no skeleton: FigureRig splits one flat mesh into limbs and swaps pre-built poses (still one draw), plus
##               a body bob; with no limbs at all only the bob / sway is drawn.
## Variation per figure (seeded): cycle phase, height ±8 %, idle tempo ±6 %, a tint for `tint_param` instance uniforms.
## Agent API used by AnimManager: anim_update(dt), world_pos(), is_skeletal(), set_skeletal_allowed(on), notifier.

const Gait = preload("gait.gd")
const FigureRig = preload("figure_rig.gd")

var root: Node3D
var gait := Gait.new()
var player: AnimationPlayer
var mesh_node: MeshInstance3D       ## procedural back end: the figure's flat mesh node
var notifier: VisibleOnScreenNotifier3D
var clips := {"idle": "idle", "walk": "walk", "run": "run", "work": "work"}
var clip_len := {}                  ## clip -> seconds (read from the player)
var working := false                ## play the work clip while standing
var idle_yaw := NAN
var skeletal_allowed := true
var on_impostor := Callable()       ## optional: on_impostor.call(root, true) when the skeleton is frozen by the cap
var state := ""                     ## the clip / pose kind on screen
var play_speed := 1.0               ## last AnimationPlayer speed_scale
var height_scale := 1.0
var tint := Color.WHITE
var idle_tempo := 1.0
var bob_amp := 0.03
var _poses := {}                    ## procedural: kind -> Array[ArrayMesh]
var _rest_y := 0.0
var _t := 0.0


## opts: clips {state: clip name}, walk_stride, run_stride, run_speed, rig_id (cache key for FigureRig), mesh_node,
## tint_param (instance uniform name), tint_base (Color), no_scale (bool).
func setup(node: Node3D, seed_: int, opts: Dictionary = {}) -> void:
	root = node
	if opts.has("clips"):
		for k in opts.clips:
			clips[k] = opts.clips[k]
	gait.walk_stride = float(opts.get("walk_stride", gait.walk_stride))
	gait.run_stride = float(opts.get("run_stride", gait.run_stride))
	gait.run_speed = float(opts.get("run_speed", gait.run_speed))
	var rng := RandomNumberGenerator.new()
	rng.seed = seed_
	gait.phase = rng.randf()
	height_scale = rng.randf_range(0.92, 1.08)
	idle_tempo = rng.randf_range(0.94, 1.06)
	var base: Color = opts.get("tint_base", Color(0.8, 0.8, 0.8))
	tint = Color.from_hsv(fposmod(base.h + rng.randf_range(-0.08, 0.08), 1.0), clampf(base.s + rng.randf_range(-0.15, 0.15), 0.0, 1.0),
			clampf(base.v + rng.randf_range(-0.12, 0.12), 0.0, 1.0))
	if not bool(opts.get("no_scale", false)):
		root.scale = Vector3.ONE * height_scale
	player = _find_player(root)
	notifier = _find_notifier(root)
	if player != null:
		player.callback_mode_process = AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
		for k in clips:
			var c: String = clips[k]
			if player.has_animation(c):
				clip_len[c] = player.get_animation(c).length
	else:
		mesh_node = opts.get("mesh_node", _find_mesh(root))
		if mesh_node != null:
			_rest_y = mesh_node.position.y
			if mesh_node.mesh is ArrayMesh:
				var id: String = opts.get("rig_id", str(mesh_node.mesh.get_instance_id()))
				for kind in ["walk", "run"]:
					_poses[kind] = FigureRig.meshes(id, mesh_node.mesh, kind)
				if not (_poses.walk as Array).is_empty():
					_poses["idle"] = [mesh_node.mesh]
	if opts.has("tint_param") and mesh_node != null:
		mesh_node.set_instance_shader_parameter(opts.tint_param, tint)
	elif opts.has("tint_param"):
		var m := _find_mesh(root)
		if m != null:
			m.set_instance_shader_parameter(opts.tint_param, tint)
	gait.reset(world_pos(), 0.0 if is_nan(idle_yaw) else idle_yaw)
	gait.phase = rng.randf()


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


## One animation step (AnimManager calls it at the tier rate with the time since this figure's last step).
func anim_update(dt: float) -> void:
	_t += dt
	gait.step(world_pos(), dt, false, idle_yaw)
	root.rotation.y = gait.yaw
	var kind := gait.gait
	if kind == "idle" and working:
		kind = "work"
	if player != null:
		_skeletal(kind, dt)
	else:
		_procedural(kind)


func _skeletal(kind: String, dt: float) -> void:
	var clip: String = clips.get(kind, "")
	if not player.has_animation(clip):
		clip = clips.idle
	if not player.has_animation(clip):
		return
	if player.current_animation != clip:
		player.play(clip, 0.2)
	state = kind
	if kind == "walk" or kind == "run":
		play_speed = Gait.clip_speed_scale(gait.inst_speed, gait.stride(), float(clip_len.get(clip, 1.0)))
	else:
		play_speed = idle_tempo
	player.speed_scale = play_speed
	if skeletal_allowed:
		player.advance(dt)


func _procedural(kind: String) -> void:
	state = kind
	if mesh_node == null:
		return
	var moving := kind == "walk" or kind == "run"
	var list: Array = _poses.get(kind if moving else "idle", [])
	if moving and not list.is_empty():
		mesh_node.mesh = list[gait.pose(list.size())]
	elif not list.is_empty():
		mesh_node.mesh = list[0]
	if moving:   # two bobs per cycle (one per step), peak at mid-stance
		mesh_node.position.y = _rest_y + bob_amp * absf(sin(gait.phase * TAU))
		mesh_node.rotation.z = 0.04 * sin(gait.phase * TAU)
	else:        # breathing / weight shift
		mesh_node.position.y = _rest_y + 0.006 * sin(_t * 2.2 * idle_tempo + gait.phase * TAU)
		mesh_node.rotation.z = 0.015 * sin(_t * 0.7 * idle_tempo + gait.phase * TAU)


static func _find_player(n: Node) -> AnimationPlayer:
	if n is AnimationPlayer:
		return n
	for c in n.get_children():
		var p := _find_player(c)
		if p != null:
			return p
	return null


static func _find_notifier(n: Node) -> VisibleOnScreenNotifier3D:
	for c in n.get_children():
		if c is VisibleOnScreenNotifier3D:
			return c
	return null


static func _find_mesh(n: Node) -> MeshInstance3D:
	if n is MeshInstance3D:
		return n
	for c in n.get_children():
		var m := _find_mesh(c)
		if m != null:
			return m
	return null
