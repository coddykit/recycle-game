extends Node
## Physics-free perf gate (fleet rule from pharmacy-rush 2d767340a5): the game has no physics bodies, queries or
## _physics_process callbacks, so the project runs the physics tick at <= 30 Hz with ONE catch-up step per frame and
## the Dummy 2D/3D servers (a slow Low frame used to run up to 8 empty 60 Hz ticks). `godot --headless --path . res://tests/physics_gate_test.tscn`.
var checks := 0
var failures := 0

func _ready() -> void:
	_run()

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		print("FAIL ", label)

func _scan(dir: String, bad: Array, banned: Array) -> void:
	var pre := dir if dir.ends_with("/") else dir + "/"
	for sub in DirAccess.get_directories_at(dir):
		if sub.begins_with(".") or (dir == "res://" and sub in ["tests", "tools", "docs", "store", "android", "builds", "build", "blender", "jenkins", "export"]):
			continue
		_scan(pre + sub, bad, banned)
	for f in DirAccess.get_files_at(dir):
		if not (f.ends_with(".gd") or f.ends_with(".tscn") or f.ends_with(".tres")):
			continue
		var text := FileAccess.get_file_as_string(pre + f)
		for word in banned:
			if text.contains(word):
				bad.append(pre + f + ": " + word)

func _run() -> void:
	await get_tree().process_frame
	var banned := ["_physics_process", "set_physics_process", "physics_frame", "process_in_physics", "PROCESS_PHYSICS", "Area2D", "Area3D", "CollisionShape", "CollisionPolygon", "RigidBody", "StaticBody", "CharacterBody", "AnimatableBody", "VehicleBody", "PhysicsBody", "PhysicsRayQuery", "PhysicsServer", "intersect_ray", "direct_space_state", "RayCast", "ShapeCast", "move_and_slide", "SoftBody"]
	var bad := []
	_scan("res://", bad, banned)
	check(bad.is_empty(), "no physics callbacks / bodies / queries in the game's scripts + scenes (%s)" % str(bad.slice(0, 5)))
	check(int(ProjectSettings.get_setting("physics/common/physics_ticks_per_second")) <= 30, "physics tick <= 30 Hz")
	check(int(ProjectSettings.get_setting("physics/common/max_physics_steps_per_frame")) <= 1, "one physics catch-up step per frame")
	check(str(ProjectSettings.get_setting("physics/3d/physics_engine")) == "Dummy" and str(ProjectSettings.get_setting("physics/2d/physics_engine")) == "Dummy", "Dummy 2D + 3D physics servers")
	check(Engine.physics_ticks_per_second <= 30 and Engine.max_physics_steps_per_frame <= 1, "engine runs the configured tick (%d Hz, %d step)" % [Engine.physics_ticks_per_second, Engine.max_physics_steps_per_frame])
	var body := RigidBody3D.new()
	var shape := CollisionShape3D.new()
	shape.shape = BoxShape3D.new()
	body.add_child(shape)
	add_child(body)
	for i in 10:
		await get_tree().physics_frame
	check(body.position.y == 0.0, "the Dummy 3D physics server simulates nothing (y=%.2f)" % body.position.y)
	body.free()
	print("PHYSICS GATE checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)
