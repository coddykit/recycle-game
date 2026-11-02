extends RefCounted
## W66 static scenery A/B using the same perf_gate counters and production World.
## Hides traffic/actors so wall-clock route progress cannot change the art delta.
## The normal (live gameplay) gate remains mandatory in check.sh.
static var _world: Node3D
static func scenes() -> Array:
	return ["desert_start", "locked_preview", "empire_far"]
static func setup(tree: SceneTree, scene: String) -> void:
	if _world == null:
		var gm: Node = tree.root.get_node("GameManager")
		tree.root.get_node("SaveManager").delete_save()
		gm.settings["quality"] = "low"
		gm.settings["quality_policy_v"] = 2
		gm.start()
		gm.settings["quality"] = "low"
		seed(6601025)
		_world = load("res://scripts/world/world.gd").new()
		tree.root.add_child(_world)
		_world.traffic.hide()
		for actor in _world.actors.values():
			actor.hide()
		for node in tree.root.get_children():
			if node != _world:
				node.process_mode = Node.PROCESS_MODE_DISABLED
	var rm: Node = tree.root.get_node("RegionManager")
	if scene == "empire_far":
		rm.unlocked_count = 8
		for rv in _world.regions:
			rv.set_locked(false, false)
		_world._update_region_detail()
		_world.rig.target = rm.center(4)
		_world.rig.distance = _world.rig.max_distance * 0.9
	elif scene == "locked_preview":
		_world.rig.target = rm.center(1)
		_world.rig.distance = 52.0
	# Snap the production camera exactly; frame-count waits leave the exponential
	# camera smoothing at machine-load-dependent positions, spoiling A/B counts.
	_world.rig._time = 0.0
	_world.rig._update_transform(0.0)
	for rv in _world.regions:
		rv.set_label_lod(_world.rig.distance > 58.0)
	_world.process_mode = Node.PROCESS_MODE_DISABLED
	await tree.process_frame
static func teardown(_tree: SceneTree) -> void:
	pass
