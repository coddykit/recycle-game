extends Node
## Reproducible portrait camera evidence. Restored progress, production World/HUD,
## fixed simulation and camera; --qa keeps saves/analytics isolated.
func _ready() -> void:
	var a := GameManager.debug_args
	var wh := str(a.get("size", "720x1280")).split("x")
	DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_BORDERLESS, true)
	DisplayServer.window_set_position(Vector2i.ZERO)
	get_tree().root.size = Vector2i(int(wh[0]), int(wh[1]))
	GameManager.start()
	GameManager.settings["quality"] = str(a.get("quality", "low"))
	GameManager.settings["quality_policy_v"] = QualityPolicy.POLICY_VERSION
	ProgressionManager.flags["intro_done"] = true
	ProgressionManager.flags["tabs_all"] = true
	TutorialManager.skip()
	RegionManager.unlocked_count = int(a.get("regions", "1"))
	if RegionManager.unlocked_count > 1:
		ProgressionManager.garage_level = 3 if RegionManager.unlocked_count == 2 else 5
		for i in range(1, RegionManager.unlocked_count):
			RegionManager.depots.append(i)
	seed(6601025)
	var w := World.new()
	add_child(w)
	w.process_mode = Node.PROCESS_MODE_DISABLED # keep worker carry/traffic timers identical during UI pop-in
	var layer := CanvasLayer.new()
	add_child(layer)
	var hud := preload("res://scenes/ui/hud.tscn").instantiate()
	layer.add_child(hud)
	hud.setup(w)
	await get_tree().create_timer(1.2).timeout # title pop-in must finish before freezing the frame
	for i in 30:
		await get_tree().process_frame
	if is_instance_valid(hud.title_card): hud.title_card.set_process(false)
	w.rig.yaw = 45.0
	w.rig._time = 0.0
	if a.has("pose"):
		var pose: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(str(a.pose)))
		w.rig.target = Vector3(pose.target[0], 0, pose.target[1])
		w.rig.distance = pose.distance
		w.rig.max_distance = pose.max_distance
		w._apply_far()
	w.rig._update_transform(0.0)
	for rv in w.regions:
		rv.set_label_lod(w.rig.distance > 58.0)
	# Same static scene for A/B; moving traffic is checked by the normal perf gate.
	w.traffic.hide()
	for actor in w.actors.values(): actor.hide()
	for node in get_tree().root.get_children():
		if node != self: node.process_mode = Node.PROCESS_MODE_DISABLED
	w.process_mode = Node.PROCESS_MODE_DISABLED
	await get_tree().process_frame
	RenderingServer.force_draw()
	var out := str(a.get("out", "/tmp/camera"))
	get_viewport().get_texture().get_image().save_png(out + ".png")
	layer.hide()
	for i in 4: await get_tree().process_frame
	RenderingServer.force_draw()
	get_viewport().get_texture().get_image().save_png(out + "-world.png")
	var points := []
	for i in RegionManager.unlocked_count:
		var c := RegionManager.center(i)
		for x in [-16.0, 16.0]:
			for z in [-16.0, 16.0]:
				var p := w.rig.camera.unproject_position(c + Vector3(x, 0, z))
				points.append([p.x, p.y])
	var active := camera_points(w)
	var box := Rect2(w.rig.camera.unproject_position(active[0]), Vector2.ZERO)
	for point in active: box = box.expand(w.rig.camera.unproject_position(point))
	var vp := get_viewport().get_visible_rect().size
	var rid := get_viewport().get_viewport_rid()
	var report := {"active_fill_x": box.size.x / vp.x, "active_fill_y": box.size.y / vp.y,
		"active_center": [box.get_center().x / vp.x, box.get_center().y / vp.y],
		"sea": sea_metrics(w.rig.camera, vp),
		"draws_3d": RenderingServer.viewport_get_render_info(rid, RenderingServer.VIEWPORT_RENDER_INFO_TYPE_VISIBLE, RenderingServer.VIEWPORT_RENDER_INFO_DRAW_CALLS_IN_FRAME),
		"primitives_3d": RenderingServer.viewport_get_render_info(rid, RenderingServer.VIEWPORT_RENDER_INFO_TYPE_VISIBLE, RenderingServer.VIEWPORT_RENDER_INFO_PRIMITIVES_IN_FRAME),"size": wh, "regions": RegionManager.unlocked_count,
		"distance": w.rig.distance, "max_distance": w.rig.max_distance,
		"target": [w.rig.target.x, w.rig.target.z], "active_corners": points,
		"primitives": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_PRIMITIVES_IN_FRAME),
		"draws": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME),
		"objects": RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_OBJECTS_IN_FRAME)}
	var f := FileAccess.open(out + ".json", FileAccess.WRITE)
	f.store_string(JSON.stringify(report, "\t"))
	print("CAMERA_EVIDENCE ", JSON.stringify(report))
	# Capture the actual Tap to start result too (old code went from 95 to 40).
	layer.show()
	w.process_mode = Node.PROCESS_MODE_INHERIT
	if is_instance_valid(hud.title_card): hud.title_card.dismiss()
	await get_tree().create_timer(1.6).timeout
	w.rig._time = 0.0
	w.rig.yaw = 45.0
	w.rig._update_transform(0.0)
	w.process_mode = Node.PROCESS_MODE_DISABLED
	RenderingServer.force_draw()
	get_viewport().get_texture().get_image().save_png(out + "-play.png")
	get_tree().quit()


func camera_points(w: World) -> PackedVector3Array:
	# Identical measured subjects on baseline and new code; no terrain corners.
	var points := PackedVector3Array()
	for i in RegionManager.unlocked_count:
		var rv := w.regions[i]
		for mmi in rv.multimeshes:
			if mmi.has_meta("decor") or mmi.name == "MM_crate": continue
			var box := mmi.multimesh.mesh.get_aabb()
			for xf in rv._base_xforms[mmi]:
				for corner in 8: points.append(rv.center + xf * box.get_endpoint(corner))
		for suffix in ["hub", "a", "b"]:
			points.append(RegionManager.graph.pos("r%d_%s" % [i, suffix]))
		if i > 0 and RegionManager.depots.has(i):
			append_model(points, "warehouse", Sites.depot_transform(i))
	append_model(points, "garage_l%d" % clampi(ProgressionManager.garage_level, 1, 5), Sites.garage_transform(ProgressionManager.garage_level))
	for spot in Sites.hq_spots(): points.append(spot.origin)
	return points


func append_model(points: PackedVector3Array, model: String, xf: Transform3D) -> void:
	var m := Data.meta(model)
	var low: Array = m.min
	var high: Array = m.max
	var box := AABB(Vector3(low[0], low[1], low[2]), Vector3(high[0]-low[0], high[1]-low[1], high[2]-low[2]))
	for corner in 8: points.append(xf * box.get_endpoint(corner))


func sea_metrics(cam: Camera3D, vp: Vector2) -> Dictionary:
	# Geometric sea mask: sampled ground rays outside every existing tile.
	# Boats, foam and bridge slivers do not count as useful land.
	var sea := 0
	var bottom_rows := 0
	var height := int(vp.y / 10)
	var still_empty := true
	for row in range(height - 1, -1, -1):
		var row_land := 0
		for col in 72:
			var screen := Vector2((col + 0.5) * vp.x / 72.0, (row + 0.5) * vp.y / height)
			var origin := cam.project_ray_origin(screen)
			var ray := cam.project_ray_normal(screen)
			var point := origin - ray * (origin.y / ray.y)
			var land := false
			if ray.y < 0:
				for i in RegionManager.count():
					var c := RegionManager.center(i)
					if absf(point.x-c.x) <= RegionManager.tile_size()*0.5 and absf(point.z-c.z) <= RegionManager.tile_size()*0.5:
						land = true
			if land: row_land += 1
			else: sea += 1
		if row_land > 0: still_empty = false
		if still_empty: bottom_rows += 1
	return {"fraction": float(sea) / (72 * height), "empty_bottom_fraction": float(bottom_rows) / height}
