extends Node
## W66 (10-29) "the models players actually see", occlusion-aware (train-baron / airport-baron method).
## A fresh game played by the test bot (tests/test_runner.gd _bot_step: best jobs, events, garage / region / vehicle /
## upgrade spending) on the real Main scene, WINDOWED on a phone portrait view (W66_SIZE, default 540 x 1200) at
## W66_QUALITY (default medium). Every W66_EVERY game seconds (default 60) for W66_MINUTES game minutes (default 10)
## three player cameras are framed:
##   home    = the overview the game opens on / returns to (rig.home_target, rig.home_distance),
##   region  = the newest unlocked region's centre at distance 34 (unlock focus / pinch-in),
##   vehicle = a vehicle on the road at distance 22 (tap a vehicle → follow).
## Per view: the real frame (<W66_OUT>/<t>_<view>.png, HUD hidden, every 3rd sample) and an ID pass: every
## GeometryInstance3D gets an unshaded colour per model key ("MM_<model>" MultiMesh → <model>, a GLB instance → its
## file name, the region dressing / terrain / water by role, else "code:<node>"), black unlit environment, render
## scale 1, no AA → visible pixels per key. Prints "W66_ID <t> <view> <key> <pixels> <total>".
##   HOME=<tmp> W66_OUT=<dir> godot --path . --audio-driver Dummy --windowed res://tools/w66_coverage.tscn
## Read-only: autosave stays off (the save lives in the throw-away HOME).

const VIEWS := ["home", "region", "vehicle"]

var main: Node
var world: Node
var rig: Node
var out_dir := ""
var quality := "medium"
var px := Vector2i(540, 1200)
var minutes := 10.0
var every := 60.0
var _rng := RandomNumberGenerator.new()
var _deadline_ms := 0


func _ready() -> void:
	_run()


func _process(_delta: float) -> void:
	if _deadline_ms > 0 and Time.get_ticks_msec() > _deadline_ms:
		push_error("W66_TIMEOUT")
		get_tree().quit(3)


func _run() -> void:
	_deadline_ms = Time.get_ticks_msec() + 900000
	out_dir = OS.get_environment("W66_OUT")
	if OS.get_environment("W66_QUALITY") != "":
		quality = OS.get_environment("W66_QUALITY")
	if OS.get_environment("W66_SIZE") != "":
		var s := OS.get_environment("W66_SIZE").split("x")
		px = Vector2i(int(s[0]), int(s[1]))
	if OS.get_environment("W66_MINUTES") != "":
		minutes = float(OS.get_environment("W66_MINUTES"))
	if OS.get_environment("W66_EVERY") != "":
		every = float(OS.get_environment("W66_EVERY"))
	_rng.seed = 66
	seed(12345)
	DirAccess.make_dir_recursive_absolute(out_dir)
	await get_tree().process_frame
	var gm := GameManager
	SaveManager.delete_save()
	gm.settings["quality"] = quality
	gm.settings["quality_policy_v"] = QualityPolicy.POLICY_VERSION
	gm.settings["quality_notice"] = ""
	get_tree().root.borderless = true
	DisplayServer.window_set_position(Vector2i(0, 0))
	DisplayServer.window_set_size(px)
	get_tree().root.size = px
	for i in 4:
		await get_tree().process_frame
	main = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	get_tree().current_scene = main
	for i in 10:
		await get_tree().process_frame
	world = main.world
	rig = world.rig
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
	world.perf_guard.reset()
	var t := 0.0
	var next := every
	var n := 0
	Engine.time_scale = 8.0
	var bot_acc := 0.0
	var last := Time.get_ticks_msec()
	while t < minutes * 60.0:
		await get_tree().process_frame
		var now := Time.get_ticks_msec()
		var dt := minf(0.25, (now - last) / 1000.0) * Engine.time_scale
		last = now
		t += dt
		bot_acc += dt
		if bot_acc >= 1.0:
			bot_acc = 0.0
			_bot_step()
		if t >= next:
			next += every
			n += 1
			Engine.time_scale = 0.0
			await _sample("t%02d" % n, n % 3 == 0)
			Engine.time_scale = 8.0
			last = Time.get_ticks_msec()
	print("W66_STATE regions=%d garage=%d vehicles=%d deliveries=%d" % [RegionManager.unlocked_count,
			ProgressionManager.garage_level, VehicleManager.vehicles.size(), int(EconomyManager.stat("deliveries"))])
	print("W66_DONE ", quality)
	Engine.time_scale = 1.0
	main.queue_free()
	for i in 4:
		await get_tree().process_frame
	get_tree().quit()


## tests/test_runner.gd _bot_step (the pacing player), verbatim apart from the PACING gate.
func _bot_step() -> void:
	for j in EventManager.pending_jobs():
		EventManager.resolve(j, EventManager.auto_choice(j), true)
	var offers := ContractManager.offers.duplicate()
	offers.append_array(ContractManager.queue)
	offers.sort_custom(func(a, b): return float(a.reward) > float(b.reward))
	for c in offers:
		var v := ContractManager.best_vehicle(c)
		if v.is_empty():
			continue
		var route := "long" if HeatManager.get_heat(RegionManager.region_of_node(c.dest)) > 60 else "normal"
		ContractManager.dispatch(c, v, route)
	if ProgressionManager.garage_check().ok:
		ProgressionManager.upgrade_garage()
	var nxt := RegionManager.next_locked()
	if nxt >= 0 and RegionManager.unlock_check(nxt).ok:
		RegionManager.unlock(nxt)
	if VehicleManager.vehicles.size() < ProgressionManager.vehicle_slots():
		var best := ""
		for d in Data.vehicles:
			if VehicleManager.buy_check(d.id).ok and EconomyManager.cash > float(d.price) * 1.3:
				best = d.id
		if best != "":
			VehicleManager.buy(best)
	for b in ["automated_dispatch", "fleet_capacity", "contract_office", "warehouse", "dispatch_center",
			"operations_room", "regional_network", "maintenance_bay", "driver_lounge", "advanced_logistics"]:
		if ProgressionManager.business_check(b).ok and EconomyManager.cash > ProgressionManager.business_cost(b) * 2.0:
			ProgressionManager.buy_business(b)
	for cn in Data.connections:
		if ProgressionManager.connection_check(cn.id).ok:
			ProgressionManager.buy_connection(cn.id)
	for i in RegionManager.unlocked_count:
		if RegionManager.depot_check(i).ok and EconomyManager.cash > RegionManager.depot_cost(i) * 2.5:
			RegionManager.build_depot(i)
	for v in VehicleManager.vehicles:
		for u in ["engine", "capacity", "reliability", "navigation", "handling"]:
			if VehicleManager.upgrade_check(v, u).ok and EconomyManager.cash > VehicleManager.upgrade_cost(v, u) * 3.0:
				VehicleManager.upgrade(v, u)


func _sample(tag: String, keep_png: bool) -> void:
	for v in VIEWS:
		if not _frame(v):
			continue
		for i in 40:   # the rig lerps toward its target
			await get_tree().process_frame
		main.hud.visible = false
		if keep_png:
			await get_tree().process_frame
			var img := get_tree().root.get_texture().get_image()
			img.save_png(out_dir.path_join("%s_%s.png" % [tag, v]))
		await _id_pass(tag, v)
		main.hud.visible = true
	rig.follow_node = null
	rig.target = rig.home_target
	rig.distance = rig.home_distance


func _frame(view: String) -> bool:
	rig.follow_node = null
	match view:
		"home":
			rig.target = rig.home_target
			rig.distance = rig.home_distance
			return true
		"region":
			rig.target = RegionManager.center(maxi(0, RegionManager.unlocked_count - 1))
			rig.distance = 34.0
			return true
		"vehicle":
			var ids: Array = []
			for id in world.actors:
				var a: Node3D = world.actors[id]
				if a.visible:
					ids.append(id)
			if ids.is_empty():
				return false
			var a: Node3D = world.actors[ids[_rng.randi() % ids.size()]]
			rig.target = Vector3(a.global_position.x, 0, a.global_position.z)
			rig.distance = 22.0
			return true
	return false


func _key(gi: GeometryInstance3D) -> String:
	if gi is MultiMeshInstance3D and str(gi.name).begins_with("MM_"):
		return str(gi.name).substr(3)
	var n: Node = gi
	while n != null and n != main:
		if n.scene_file_path.ends_with(".glb"):
			return n.scene_file_path.get_file().get_basename()
		n = n.get_parent()
	var p := gi.get_parent()
	if p is RegionView:
		var rv := p as RegionView
		if gi == rv.tile_mi:
			return "terrain:tile"
		if gi == rv.road_mi:
			return "terrain:road"
		if gi == rv._dressing:
			return "dressing"
	var nm := str(gi.name)
	if nm.begins_with("@"):
		nm = gi.get_class()
	return "code:%s/%s" % [str(p.name).rstrip("0123456789").trim_prefix("@"), nm.rstrip("0123456789")]


func _id_color(i: int) -> Color:
	# 6 levels per channel (0, 51, ..., 255) → 215 keys; 0 = black background
	return Color8((i / 36) % 6 * 51, (i / 6) % 6 * 51, i % 6 * 51)


func _id_pass(tag: String, view: String) -> void:
	var cam: Camera3D = rig.camera
	var keep_env := cam.environment
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color.BLACK
	env.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	env.ambient_light_source = Environment.AMBIENT_SOURCE_DISABLED
	cam.environment = env
	var keep_scale := get_tree().root.scaling_3d_scale
	var keep_msaa := get_tree().root.msaa_3d
	var keep_aa := get_tree().root.screen_space_aa
	get_tree().root.scaling_3d_scale = 1.0
	get_tree().root.msaa_3d = Viewport.MSAA_DISABLED
	get_tree().root.screen_space_aa = Viewport.SCREEN_SPACE_AA_DISABLED
	var layers: Array = []
	for cl in get_tree().root.find_children("*", "CanvasLayer", true, false):
		if (cl as CanvasLayer).visible:
			layers.append(cl)
			(cl as CanvasLayer).visible = false
	var keys: Array = []
	var mats := {}
	var touched: Array = []
	for g in main.find_children("*", "GeometryInstance3D", true, false):
		var gi := g as GeometryInstance3D
		if gi is Label3D or gi is Sprite3D or not gi.is_visible_in_tree():
			continue
		var key := _key(gi)
		if not mats.has(key):
			keys.append(key)
			var m := StandardMaterial3D.new()
			m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
			m.albedo_color = _id_color(keys.size())
			m.disable_fog = true
			m.cull_mode = BaseMaterial3D.CULL_DISABLED
			mats[key] = m
		touched.append([gi, gi.material_override, gi.transparency, gi.cast_shadow])
		gi.material_override = mats[key]
		gi.transparency = 0.0
	for i in 6:
		await get_tree().process_frame
	var img := get_tree().root.get_texture().get_image()
	img.convert(Image.FORMAT_RGB8)
	var counts := {}
	var data := img.get_data()
	for i in range(0, data.size(), 3):
		var r := int(round(data[i] / 51.0))
		var gg := int(round(data[i + 1] / 51.0))
		var b := int(round(data[i + 2] / 51.0))
		if absi(data[i] - r * 51) > 10 or absi(data[i + 1] - gg * 51) > 10 or absi(data[i + 2] - b * 51) > 10:
			continue
		var code := r * 36 + gg * 6 + b
		counts[code] = int(counts.get(code, 0)) + 1
	var total := img.get_width() * img.get_height()
	for i in keys.size():
		var c := int(counts.get(i + 1, 0))
		if c > 0:
			print("W66_ID %s %s %s %d %d" % [tag, view, keys[i], c, total])
	for t in touched:
		var gi := t[0] as GeometryInstance3D
		if is_instance_valid(gi):
			gi.material_override = t[1]
			gi.transparency = t[2]
	for cl in layers:
		(cl as CanvasLayer).visible = true
	cam.environment = keep_env
	get_tree().root.scaling_3d_scale = keep_scale
	get_tree().root.msaa_3d = keep_msaa
	get_tree().root.screen_space_aa = keep_aa
	await get_tree().process_frame
