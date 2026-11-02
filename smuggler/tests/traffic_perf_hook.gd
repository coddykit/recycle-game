extends RefCounted
## Deterministic Low perf_gate A/B fixture for ambient traffic changes.
## Use --hook=res://tests/traffic_perf_hook.gd and launch Godot with --fixed-fps 60.
## Wall-clock timing is informational; compare geometry, UI draws and static memory.
const Hook = preload("res://tests/perf_gate_hook.gd")
static var _seeded := false


static func scenes() -> Array:
	return Hook.scenes()


static func setup(tree: SceneTree, scene: String) -> void:
	if not _seeded:
		seed(83)
		_seeded = true
	await Hook.setup(tree, scene)


static func teardown(tree: SceneTree) -> void:
	Hook.teardown(tree)
