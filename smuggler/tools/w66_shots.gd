extends Node
## W66 (10-29) compare frames: the same deterministic moment and cameras on any tree (run it on a clean origin/main
## archive for "before"). Fresh game, 3 regions + garage L2 + a box truck and a minibus on jobs, traffic running;
## run with --fixed-fps 60 so every run sees the same frames:
##   HOME=<tmp> W66_SHOT_OUT=<dir> W66_SHOT_Q=low|medium|high godot --path . --audio-driver Dummy --windowed \
##       --fixed-fps 60 --resolution 540x1200 res://tools/w66_shots.tscn
## Writes <dir>/<camera>.png (HUD hidden) and prints "W66_SHOT <camera> draws=<n> prims=<n>".

const CAMS := ["home", "dust", "green", "crossing1", "crossing2", "hq", "traffic", "truck"]


func _ready() -> void:
	var out := OS.get_environment("W66_SHOT_OUT")
	var q := OS.get_environment("W66_SHOT_Q") if OS.get_environment("W66_SHOT_Q") != "" else "medium"
	DirAccess.make_dir_recursive_absolute(out)
	seed(6601)
	SaveManager.delete_save()
	GameManager.settings["quality"] = q
	GameManager.settings["quality_policy_v"] = QualityPolicy.POLICY_VERSION
	GameManager.settings["quality_notice"] = ""
	await get_tree().process_frame
	var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	for i in 10:
		await get_tree().process_frame
	var hud: Node = main.hud
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	ProgressionManager.flags["intro_done"] = true
	ProgressionManager.flags["tabs_all"] = true
	TutorialManager.skip()
	hud._restore_game_chrome()
	hud._modal_queue.clear()
	var w: Node = main.world
	w.perf_guard.reset()
	for r in range(RegionManager.unlocked_count, 3):
		EconomyManager.earn(1e8, "w66", false)
		EconomyManager.add_reputation(400)
		RegionManager.unlock(r)
		await get_tree().process_frame
	EconomyManager.earn(1e9, "w66", false)
	ProgressionManager.upgrade_garage()
	for id in ["box_truck", "minibus"]:
		VehicleManager.add_vehicle(id)
	for i in 10:
		await get_tree().process_frame
	ContractManager.refresh_offers()
	for c in ContractManager.offers.duplicate():
		var v: Dictionary = ContractManager.best_vehicle(c)
		if not v.is_empty() and ContractManager.dispatch_check(c, v) == "":
			ContractManager.dispatch(c, v, "normal")
	for i in 420:   # 7 s at --fixed-fps 60: the unlock pop-ins have landed, vehicles are on the road
		await get_tree().process_frame
	for n in get_tree().root.get_children():
		if n != self:
			n.process_mode = Node.PROCESS_MODE_DISABLED
	var layer: CanvasLayer = main.get_node("UI")
	layer.visible = false
	var rig: Node = w.rig
	var g = RegionManager.graph
	var crossings: Array = []
	for c in g.crossings:
		crossings.append((g.pos(c.b) + g.pos(c.a)) * 0.5)
	var rid := get_viewport().get_viewport_rid()
	for cam in CAMS:
		var at := Vector3.ZERO
		var dist := 30.0
		match cam:
			"home":
				at = rig.home_target
				dist = rig.home_distance
			"dust":
				at = RegionManager.center(0)
			"green":
				at = RegionManager.center(1)
			"crossing1", "crossing2":
				var k := 0 if cam == "crossing1" else 1
				if k >= crossings.size():
					continue
				at = crossings[k]
				dist = 20.0
			"hq":
				at = w.hq.garage.global_position
				dist = 18.0
			"traffic":
				var best: Node3D = null
				for c in w.traffic.cars:
					var hn := c.node as Node3D
					if hn.visible and (best == null or hn.global_position.length() < best.global_position.length()):
						best = hn
				if best == null:
					continue
				at = best.global_position
				dist = 14.0
			"truck":
				var a: Node3D = null
				for id in w.actors:
					if str(VehicleManager.get_vehicle(id).get("def", "")) == "box_truck":
						a = w.actors[id]
				if a == null:
					continue
				at = a.global_position
				dist = 14.0
		rig.follow_node = null
		rig.target = Vector3(at.x, 0, at.z)
		rig.distance = dist
		rig._cur_target = rig.target
		rig._cur_distance = dist
		rig._update_transform(0.0)
		for rv in w.regions:
			rv.set_label_lod(rig.distance > 58.0)
		for i in 3:
			await get_tree().process_frame
		RenderingServer.force_draw()
		get_viewport().get_texture().get_image().save_png(out.path_join(cam + ".png"))
		print("W66_SHOT %s draws=%d prims=%d" % [cam,
			RenderingServer.viewport_get_render_info(rid, RenderingServer.VIEWPORT_RENDER_INFO_TYPE_VISIBLE, RenderingServer.VIEWPORT_RENDER_INFO_DRAW_CALLS_IN_FRAME),
			RenderingServer.viewport_get_render_info(rid, RenderingServer.VIEWPORT_RENDER_INFO_TYPE_VISIBLE, RenderingServer.VIEWPORT_RENDER_INFO_PRIMITIVES_IN_FRAME)])
	print("W66_SHOTS_DONE ", q)
	get_tree().quit()
