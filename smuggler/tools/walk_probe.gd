extends SceneTree
## W68 walk evidence: real CharacterActors walking across a floor with 0.25 m lines, side view, plus the stance-foot
## slide ratio (travel of the slower foot / body travel; 0 = planted). Windowed:
##   Godot --path . --rendering-method gl_compatibility -s res://tools/walk_probe.gd -- --out <dir> [--frames N]
## Headless prints only the ratio.


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	var args := OS.get_cmdline_user_args()
	var out := args[args.find("--out") + 1] if args.has("--out") else ""
	if out != "":
		DirAccess.make_dir_recursive_absolute(out)
	var frames := int(args[args.find("--frames") + 1]) if args.has("--frames") else 150
	root.size = Vector2i(960, 540)
	var env := WorldEnvironment.new()
	env.environment = Environment.new()
	env.environment.background_mode = Environment.BG_COLOR
	env.environment.background_color = Color("d8d0c0")
	env.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.environment.ambient_light_color = Color.WHITE
	env.environment.ambient_light_energy = 0.6
	root.add_child(env)
	var light := DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-50, -30, 0)
	root.add_child(light)
	for i in 25:
		var line := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(0.015, 0.01, 3.0)
		line.mesh = box
		line.position = Vector3((i - 12) * 0.25, -0.005, 0)
		root.add_child(line)
	var actors: Array = []
	var specs := [["driver", 1.3, 1.2, -0.6], ["worker", 1.35, 1.6, 0.0], ["mentor", 1.6, 1.0, 0.6]]
	for s in specs:
		var c: Node3D = load("res://scripts/world/character_actor.gd").new()   # runtime load: needs the Models autoload
		root.add_child(c)
		c.setup(s[0], s[1])
		c.position = Vector3(-2.8, 0, s[3])
		c.walk_to(Vector3(2.8, 0, s[3]), "idle", s[2])
		actors.append(c)
	var cam := Camera3D.new()
	root.add_child(cam)
	cam.fov = 35.0
	cam.position = Vector3(0, 0.6, 6.5)
	cam.look_at(Vector3(0, 0.4, 0))
	cam.current = true
	var slide := 0.0
	var travel := 0.0
	var prev := {}
	for f in frames:
		await process_frame
		for c in actors:
			if c._target == Vector3.INF:
				continue
			var fl: Vector3 = c._parts["Leg_L"].global_transform * Vector3(0, -0.18, 0)
			var fr: Vector3 = c._parts["Leg_R"].global_transform * Vector3(0, -0.18, 0)
			if prev.has(c):   # the stance foot = the one that moved least this frame
				slide += minf(absf(fl.x - prev[c][0].x), absf(fr.x - prev[c][1].x))
				travel += absf(c.position.x - prev[c][2].x)
			prev[c] = [fl, fr, c.position]
		if out != "":
			await RenderingServer.frame_post_draw
			root.get_texture().get_image().save_png("%s/frame-%03d.png" % [out, f])
	print("WALK_SLIDE stance_foot/body=%.3f" % (slide / maxf(travel, 0.001)))
	quit()
