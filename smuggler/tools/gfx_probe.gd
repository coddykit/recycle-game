extends Node
## Draw-call / primitive probe per graphics tier at the main view (needs a real renderer, not --headless).
## Run: godot --path . res://tools/gfx_probe.tscn -- --qa --unlock=4 --out=/tmp/gfx_smuggler.json
var rows: Array = []


func _avg(frames: int) -> Dictionary:
	var dc := 0.0
	var pr := 0.0
	var ob := 0.0
	var gpu := 0.0
	var cpu := 0.0
	var vp := get_viewport().get_viewport_rid()
	RenderingServer.viewport_set_measure_render_time(vp, true)
	for i in frames:
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		dc += RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME)
		pr += RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_PRIMITIVES_IN_FRAME)
		ob += RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_OBJECTS_IN_FRAME)
		gpu += RenderingServer.viewport_get_measured_render_time_gpu(vp)
		cpu += RenderingServer.viewport_get_measured_render_time_cpu(vp)
	return {"draw_calls": int(dc / frames), "primitives": int(pr / frames), "objects": int(ob / frames),
			"gpu_ms": snappedf(gpu / frames, 0.01), "cpu_ms": snappedf(cpu / frames, 0.01)}


func _ready() -> void:
	var args := {}
	for a in OS.get_cmdline_user_args():
		var p := a.trim_prefix("--").split("=", true, 1)
		args[p[0]] = p[1] if p.size() > 1 else "1"
	var main: Node = load("res://scenes/main/main.tscn").instantiate()
	add_child(main)
	await get_tree().create_timer(1.5).timeout
	if args.has("unlock"):
		RegionManager.unlocked_count = clampi(int(args.unlock), 1, RegionManager.count())
		main.world._update_region_detail()
		main.world._update_bounds()
		for i in RegionManager.unlocked_count:
			main.world.regions[i].set_locked(false, false)
	for tier in ["high", "medium", "low"]:
		GameManager.settings["quality"] = tier
		main.world.apply_quality()
		await get_tree().create_timer(0.5).timeout
		var m := await _avg(20)
		m["tier"] = tier
		m["scale"] = main.get_viewport().scaling_3d_scale
		m["far"] = main.world.rig.camera.far
		m["shadows"] = main.world.sun.shadow_enabled
		m["renderer"] = RenderingServer.get_current_rendering_method()
		m["adapter"] = RenderingServer.get_video_adapter_name()
		rows.append(m)
		print("GFX_PROBE ", JSON.stringify(m))
	if args.has("out"):
		var f := FileAccess.open(str(args.out), FileAccess.WRITE)
		f.store_string(JSON.stringify(rows, "  "))
		f.close()
	get_tree().quit(0)
