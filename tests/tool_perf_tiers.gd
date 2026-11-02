extends SceneTree
## Frame-time / stall evidence per graphics tier (not a test suite; needs a GPU: run WINDOWED, via shot-lock.sh).
## Builds a fresh start ("start") or a late-game empire ("midgame": all regions, depots, garage, a fleet on the road)
## and per tier (low / medium / high) records, with vsync OFF: frame-time mean / p50 / p95 / p99 / max, long frames
## (> 33 ms, > 50 ms), draw calls, primitives, objects, static + video memory, node count; plus one-off main-thread
## stalls (Main instantiate, new game, region unlock, tier switch, opening each panel).
##   godot --path . --resolution 540x960 -s tests/tool_perf_tiers.gd -- --out=/abs/dir --stage=midgame --frames=240
## One JSON per run: <out>/perf_<renderer>_<w>x<h>_<stage>.json

var out_dir := "/private/tmp/smuggler-perf"
var stage := "midgame"
var frames := 240
var UIManager: Node
var GameManager: Node
var EconomyManager: Node
var RegionManager: Node
var ProgressionManager: Node
var VehicleManager: Node
var ContractManager: Node
var Data: Node


func _initialize() -> void:
	for n in ["UIManager", "GameManager", "EconomyManager", "RegionManager", "ProgressionManager", "VehicleManager", "ContractManager", "Data"]:
		set(n, root.get_node(n))
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--out="):
			out_dir = a.substr(6)
		elif a.begins_with("--stage="):
			stage = a.substr(8)
		elif a.begins_with("--frames="):
			frames = int(a.substr(9))
	_run()


func _pct(sorted: Array, p: float) -> float:
	return float(sorted[mini(sorted.size() - 1, int(sorted.size() * p))])


func _ms(t0: int) -> float:
	return snappedf((Time.get_ticks_usec() - t0) / 1000.0, 0.1)


func _settle(n := 30) -> void:
	for _i in n:
		await process_frame


func _build_midgame(main: Node) -> Dictionary:
	var stalls := {}
	var em := EconomyManager
	for r in range(1, RegionManager.count()):
		em.earn(1e8, "perf", false)
		em.add_reputation(400)
		var t0 := Time.get_ticks_usec()
		RegionManager.unlock(r)
		await process_frame
		stalls["unlock_region_%d_ms" % r] = _ms(t0)
	em.earn(1e9, "perf", false)
	for i in 6:
		ProgressionManager.upgrade_garage()
	for d in range(RegionManager.count()):
		RegionManager.build_depot(d)
	for def in Data.vehicles:
		em.earn(1e8, "perf", false)
		VehicleManager.buy(str(def.id))
	await _settle(10)
	# put the fleet on the road: every idle vehicle takes a contract
	var guard := 0
	while guard < 12:
		guard += 1
		ContractManager.refresh_offers()
		var sent := 0
		for c in ContractManager.offers.duplicate():
			var v: Dictionary = ContractManager.best_vehicle(c)
			if not v.is_empty() and ContractManager.dispatch_check(c, v) == "":
				ContractManager.dispatch(c, v, "normal")
				sent += 1
		if sent == 0:
			break
	stalls["jobs_on_road"] = ContractManager.jobs.size()
	stalls["vehicles"] = VehicleManager.vehicles.size()
	return stalls


func _census(w: Node) -> Dictionary:
	var out := {"mesh_instances": 0, "multimesh": 0, "lights": 0, "particles": 0, "label3d": 0, "visible_meshes": 0}
	var stack: Array = [w]
	while not stack.is_empty():
		var x: Node = stack.pop_back()
		if x is MeshInstance3D:
			out.mesh_instances += 1
			if (x as MeshInstance3D).is_visible_in_tree():
				out.visible_meshes += 1
		elif x is MultiMeshInstance3D:
			out.multimesh += 1
		elif x is Light3D:
			out.lights += 1
		elif x is GPUParticles3D or x is CPUParticles3D:
			out.particles += 1
		elif x is Label3D:
			out.label3d += 1
		stack.append_array(x.get_children())
	return out


func _run() -> void:
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0
	RenderingServer.viewport_set_measure_render_time(root.get_viewport_rid(), true)
	await process_frame
	root.get_node("SaveManager").delete_save()
	var stalls := {}
	var t0 := Time.get_ticks_usec()
	var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	root.add_child(main)
	current_scene = main
	stalls["main_instantiate_ms"] = _ms(t0)
	await _settle(10)
	var hud: Node = main.hud
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	ProgressionManager.flags["intro_done"] = true
	ProgressionManager.flags["tabs_all"] = true
	TutorialManager_skip()
	hud._restore_game_chrome()
	hud._modal_queue.clear()
	var w: Node = main.world
	t0 = Time.get_ticks_usec()
	if stage == "midgame":
		var extra: Dictionary = await _build_midgame(main)
		stalls.merge(extra)
	stalls["scenario_build_ms"] = _ms(t0)
	await _settle(40)
	var rows := {}
	for q in ["low", "medium", "high"]:
		t0 = Time.get_ticks_usec()
		GameManager.settings["quality"] = q
		w.apply_quality()
		stalls["switch_to_%s_ms" % q] = _ms(t0)
		t0 = Time.get_ticks_usec()
		await process_frame
		stalls["switch_to_%s_next_frame_ms" % q] = _ms(t0)
		for view in ["near", "far"]:
			if view == "near":
				w.rig.focus_on(RegionManager.center(0), 34.0, 0.05)
			else:
				w.rig.focus_on(RegionManager.center(maxi(0, RegionManager.unlocked_count / 2)), w.rig.max_distance * 0.9, 0.05)
			await _settle(90)
			var ms: Array = []
			var dc := 0.0
			var prim := 0.0
			var obj := 0.0
			var cpu_ms := 0.0
			var gpu_ms := 0.0
			var last := Time.get_ticks_usec()
			for _i in frames:
				await process_frame
				var now := Time.get_ticks_usec()
				ms.append((now - last) / 1000.0)
				last = now
				dc += Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)
				prim += Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME)
				obj += Performance.get_monitor(Performance.RENDER_TOTAL_OBJECTS_IN_FRAME)
				cpu_ms += RenderingServer.viewport_get_measured_render_time_cpu(root.get_viewport_rid()) + RenderingServer.get_frame_setup_time_cpu()
				gpu_ms += RenderingServer.viewport_get_measured_render_time_gpu(root.get_viewport_rid())
			var sorted := ms.duplicate()
			sorted.sort()
			var sum := 0.0
			var l33 := 0
			var l50 := 0
			for v in ms:
				sum += float(v)
				if float(v) > 33.4:
					l33 += 1
				if float(v) > 50.0:
					l50 += 1
			rows["%s_%s" % [q, view]] = {
				"frame_ms_mean": snappedf(sum / ms.size(), 0.01), "frame_ms_p50": snappedf(_pct(sorted, 0.5), 0.01),
				"frame_ms_p95": snappedf(_pct(sorted, 0.95), 0.01), "frame_ms_p99": snappedf(_pct(sorted, 0.99), 0.01),
				"frame_ms_max": snappedf(float(sorted[-1]), 0.01), "frames_over_33ms": l33, "frames_over_50ms": l50,
				"render_cpu_ms": snappedf(cpu_ms / frames, 0.01), "render_gpu_ms": snappedf(gpu_ms / frames, 0.01),
				"draw_calls": snappedf(dc / frames, 0.1), "primitives_k": snappedf(prim / frames / 1000.0, 0.1), "objects": snappedf(obj / frames, 0.1),
				"mem_static_mb": snappedf(Performance.get_monitor(Performance.MEMORY_STATIC) / 1048576.0, 0.1),
				"mem_video_mb": snappedf(Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED) / 1048576.0, 0.1),
				"nodes": int(Performance.get_monitor(Performance.OBJECT_NODE_COUNT)), "render_scale": root.scaling_3d_scale}
			rows["%s_%s" % [q, view]]["census"] = _census(w)
			print("[perf] ", q, " ", view, " ", JSON.stringify(rows["%s_%s" % [q, view]]))
	# one-off main-thread stalls of the UI: opening every panel (sheet build) on the Medium tier
	GameManager.settings["quality"] = "medium"
	w.apply_quality()
	w.rig.focus_on(RegionManager.center(0), 34.0, 0.05)
	await _settle(30)
	var panels := {}
	for pn in ["contracts", "dispatch", "fleet", "map", "upgrades", "business", "settings", "shop", "daily"]:
		var arg: Variant = null
		if pn == "dispatch":
			if ContractManager.offers.is_empty():
				ContractManager.refresh_offers()
			arg = ContractManager.offers[0].id
		t0 = Time.get_ticks_usec()
		UIManager.open_panel(pn, arg)
		var open_ms := _ms(t0)
		t0 = Time.get_ticks_usec()
		await process_frame
		var frame_ms := _ms(t0)
		await _settle(25)
		panels[pn] = {"open_ms": open_ms, "first_frame_ms": frame_ms,
			"static_mb": snappedf(Performance.get_monitor(Performance.MEMORY_STATIC) / 1048576.0, 0.1)}
		UIManager.close_panel()
		await _settle(20)
	var report := {"date": Time.get_date_string_from_system(), "renderer": RenderingServer.get_current_rendering_method(),
		"driver": RenderingServer.get_current_rendering_driver_name(), "gpu": RenderingServer.get_video_adapter_name(),
		"window": str(root.size), "stage": stage, "stalls": stalls, "panels": panels, "tiers": rows}
	DirAccess.make_dir_recursive_absolute(out_dir)
	var f := FileAccess.open(out_dir.path_join("perf_%s_%dx%d_%s.json" % [report["renderer"], root.size.x, root.size.y, stage]), FileAccess.WRITE)
	f.store_string(JSON.stringify(report, "  "))
	f.close()
	print("[perf] stalls ", JSON.stringify(stalls))
	print("[perf] panels ", JSON.stringify(panels))
	root.get_node("SaveManager").delete_save()
	quit(0)


func TutorialManager_skip() -> void:
	root.get_node("TutorialManager").skip()
