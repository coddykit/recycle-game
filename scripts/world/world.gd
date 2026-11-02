class_name World
extends Node3D
const _MsaaGuard := preload("res://addons/coddy_perf/perf_profiles.gd")
## Builds and animates the miniature world: regions, HQ, depots, vehicles,
## ambient traffic, markers and effects. Listens to gameplay signals only.

signal vehicle_tapped(vehicle_id: String)
signal region_tapped(index: int)
signal hq_tapped

var rig: CameraRig
var sun: DirectionalLight3D
var env: WorldEnvironment
var regions: Array[RegionView] = []
var hq: HQSite
var depots := {}            # region index -> Node3D
var actors := {}            # vehicle id -> VehicleActor
var traffic: TrafficSystem
var markers := {}           # job id -> Node3D
var selected_vehicle := ""
var font: Font
var route_line: MeshInstance3D
var _route_key := ""
var _preview := false
var _engine_timer := 0.0
const SUN_ENERGY_SHADOWED := 0.42


func _ready() -> void:
	UIManager.world = self
	Models.hd = GameManager.quality() != "low"   # W66: before any model is built — Low never loads assets/models/hd/
	font = UIKit.bold_font()
	_build_environment()
	_build_water()
	_add_vignette()
	rig = CameraRig.new()
	rig.name = "CameraRig"
	add_child(rig)
	rig.tapped.connect(_on_tap)
	for i in Data.regions.size():
		var rv := RegionView.new()
		add_child(rv)
		rv.setup(i, font)
		regions.append(rv)
	_update_region_detail()
	hq = HQSite.new()
	add_child(hq)
	hq.setup(self)
	if ProgressionManager.intro_done():
		spawn_mentor()
	for d in RegionManager.depots:
		_build_depot(int(d), false)
	route_line = MeshInstance3D.new()
	route_line.mesh = ImmediateMesh.new()
	route_line.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(route_line)
	traffic = TrafficSystem.new()
	traffic.name = "Traffic"
	traffic.fleet = actors   # W68: the ambient cars brake behind / make way for the player's vehicles
	add_child(traffic)
	traffic.refresh_count()
	for v in VehicleManager.vehicles:
		_spawn_actor(v, false)
	for j in ContractManager.jobs:
		_add_marker(j)
	_update_bounds()
	rig.target = rig.home_target
	rig.distance = rig.home_distance
	rig._update_transform(0.0)
	get_viewport().size_changed.connect(_on_camera_viewport_changed)
	apply_quality()
	# gameplay signals
	VehicleManager.vehicle_added.connect(func(v): _spawn_actor(v, true))
	VehicleManager.vehicle_changed.connect(_on_vehicle_changed)
	VehicleManager.vehicle_upgraded.connect(_on_vehicle_upgraded)
	ContractManager.job_started.connect(_on_job_started)
	ContractManager.job_delivered.connect(_on_job_delivered)
	ContractManager.job_failed.connect(_on_job_failed)
	ContractManager.job_returned.connect(_on_job_returned)
	EventManager.event_triggered.connect(_on_event)
	RegionManager.region_unlocked.connect(_on_region_unlocked)
	RegionManager.depot_built.connect(func(i):
		_build_depot(i, true)
		_update_bounds())
	ProgressionManager.garage_upgraded.connect(_on_garage_upgraded)
	ProgressionManager.business_upgraded.connect(func(_id, _l): hq._build_pad())
	HeatManager.heat_changed.connect(_on_heat_changed)
	EconomyManager.cash_changed.connect(func(_c, _d): _refresh_lock_labels())
	EconomyManager.reputation_changed.connect(func(_r, _d): _refresh_lock_labels())
	UIManager.focus_requested.connect(func(p, z): rig.focus_on(p, z))


func _build_environment() -> void:
	env = WorldEnvironment.new()
	var e := Environment.new()
	e.background_mode = Environment.BG_COLOR
	e.background_color = Color("6db8db")
	e.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	e.ambient_light_color = Color("c9dcf0")
	e.ambient_light_energy = 0.42
	env.environment = e
	add_child(env)
	sun = DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-52, 28, 0)
	sun.light_color = Color("fff1dc")
	sun.light_energy = 1.0
	sun.shadow_enabled = true
	sun.shadow_bias = 0.04
	sun.shadow_normal_bias = 1.2
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	sun.directional_shadow_max_distance = 65.0
	add_child(sun)


## W67 look-dev: a soft screen-space vignette under the HUD (CanvasLayer −1, blend_mul) pulls the eye to the island in
## the middle of the sea. 2D only (no internal 3D buffer → never trips the Android GLES3 MSAA guard). Every tier.
func _add_vignette() -> void:
	var layer := CanvasLayer.new()
	layer.layer = -1
	var rect := ColorRect.new()
	rect.set_anchors_preset(Control.PRESET_FULL_RECT)
	rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var mat := ShaderMaterial.new()
	mat.shader = preload("res://assets/shaders/vignette.gdshader")
	mat.set_shader_parameter("strength", 0.30)
	mat.set_shader_parameter("tint", Vector3(0.90, 0.95, 1.0))
	rect.material = mat
	layer.add_child(rect)
	add_child(layer)


## Per-tier render knobs (owner rule 2026-10-14; Low must really be cheap).
## scale: 3D render scale. msaa: Viewport.MSAA_* value. shadow_atlas: directional shadow size (0 = no
## real-time shadows, baked blob shadows instead). far: camera far plane (never below what the
## zoomed-out camera needs, see _far_for). lod: Viewport.mesh_lod_threshold (higher = coarser LODs).
## decor: share of trees/rocks kept. traffic: share of ambient cars kept. fps: the frame cap on a phone (§10: Low 30).
const PROFILES := {
	"low": {"scale": 0.85, "msaa": Viewport.MSAA_DISABLED, "shadow_atlas": 0, "far": 380.0, "lod": 4.0, "decor": 0.45, "traffic": 0.4, "fps": 30},
	"medium": {"scale": 1.0, "msaa": Viewport.MSAA_2X, "shadow_atlas": 1024, "far": 480.0, "lod": 2.0, "decor": 0.75, "traffic": 0.75, "fps": 60},
	"high": {"scale": 1.0, "msaa": Viewport.MSAA_4X, "shadow_atlas": 2048, "far": 600.0, "lod": 1.0, "decor": 1.0, "traffic": 1.0, "fps": 60},
}
const PerfGuard := preload("res://addons/coddy_perf/perf_guard.gd")
const LowShading := preload("res://addons/coddy_low_shading/low_shading.gd")
const PerfProfiles := preload("res://addons/coddy_perf/perf_profiles.gd")
const PerfProbe := preload("res://addons/coddy_perf/perf_probe.gd")
const PerfDevice := preload("res://addons/coddy_perf/perf_device.gd")
## W60-B1 / W63 (games/tools/perf): dynamic downgrade (10 s under 25 FPS → one tier, a one-time toast, never raised
## by itself), phone frame pacing (a sheet over the map → 20 FPS, 5 s without a touch → 30 / 20), PERFX probe in
## debug phone builds; the checkpoint arms get the vehicle positions 10× a second instead of a new array every frame.
const NEAR_EVERY_S := 0.1
var perf_guard := PerfGuard.new()
var perf_probe := PerfProbe.new()
var _idle_t := 0.0
var _near_t := NEAR_EVERY_S


static func profile(q: String) -> Dictionary:
	return PROFILES.get(q, PROFILES.high)


## Low: no real-time shadows (baked blob shadows), 0.85 render scale, no MSAA (W67 + the 4.7.2 Android MSAA leak), short draw distance,
## coarse LODs, 45% of the scenery props, 40% traffic and fewer particles.
## Medium: 1024 sun shadows, 2x MSAA, native scale. High: 2048 shadows, 4x MSAA.
func apply_quality() -> void:
	var q := GameManager.quality()
	var p := profile(q)
	# W63 Low profile (doner-night 57d4e6f320 rule): Low lights the regions per vertex without specular; Medium / High
	# keep the per-pixel look (vehicles and markers spawned later follow through SceneTree.node_added)
	if is_inside_tree():
		LowShading.install(get_tree())
	LowShading.set_cheap(q == "low")
	sun.shadow_enabled = int(p.shadow_atlas) > 0
	# the compatibility renderer lights shadowed passes brighter; keep both looks equal
	sun.light_energy = SUN_ENERGY_SHADOWED if sun.shadow_enabled else 1.0
	if sun.shadow_enabled:
		RenderingServer.directional_shadow_atlas_set_size(int(p.shadow_atlas), true)
	var vp := get_viewport()
	vp.msaa_3d = p.msaa
	vp.scaling_3d_scale = p.scale
	vp.mesh_lod_threshold = p.lod
	_MsaaGuard.guard_msaa_soon(vp)   # Godot 4.7.2 Android GLES3 MSAA framebuffer leak (coddy_perf)
	if _mobile():
		Engine.max_fps = int(p.fps)
	perf_guard.reset() # a new tier: the downgrade watch warms up again
	_apply_far()
	var hd_changed: bool = Models.hd != (q != "low")
	Models.hd = q != "low"   # W66: vehicles / traffic rebuilt below take the tier's files; regions + HQ swap in place
	if hd_changed and hq:
		hq.swap_models()
	for rv in regions:
		rv.set_decor_share(float(p.decor))
		rv.set_low(q == "low") # baked terrain / props, perf gate draws <= 150
	for id in actors:
		(actors[id] as VehicleActor).apply_low(q == "low")
		(actors[id] as VehicleActor).set_blob_shadow(not sun.shadow_enabled)
	if traffic:
		traffic.refresh_count()


## Draw distance: the tier's far plane, but never closer than the zoomed-out camera needs.
func _apply_far() -> void:
	if rig and rig.camera:
		rig.camera.far = maxf(float(profile(GameManager.quality()).far), rig.max_distance * 1.6)


func _build_water() -> void:
	var water := MeshInstance3D.new()
	water.mesh = TerrainBuilder.water_mesh(1400.0)
	water.material_override = Models.water_material
	# W67: shallows + foam around the island tiles (water.gdshader, up to 8 tiles)
	var tiles := PackedVector2Array()
	for i in mini(RegionManager.count(), 8):
		var c := RegionManager.center(i)
		tiles.append(Vector2(c.x, c.z))
	Models.water_material.set_shader_parameter("tiles", tiles)
	Models.water_material.set_shader_parameter("tile_count", tiles.size())
	Models.water_material.set_shader_parameter("tile_half", RegionManager.tile_size() * 0.5)
	water.position = Vector3(-80, -0.75, -60)
	water.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(water)


func _update_region_detail() -> void:
	for rv in regions:
		rv.set_detail(rv.index <= RegionManager.unlocked_count)


## The same geometry is used on the title, after onboarding and on unlock.
## Locked terrain is visible at the edge but never makes the active view smaller.
func active_camera_points() -> PackedVector3Array:
	var points := PackedVector3Array()
	for i in RegionManager.unlocked_count:
		var rv := regions[i]
		for mmi in rv.multimeshes:
			if mmi.has_meta("decor") or mmi.name == "MM_crate": continue
			var box := mmi.multimesh.mesh.get_aabb()
			for xf in rv._base_xforms[mmi]:
				for corner in 8:
					points.append(rv.center + xf * box.get_endpoint(corner))
		for suffix in ["hub", "a", "b"]:
			points.append(RegionManager.graph.pos("r%d_%s" % [i, suffix]))
		if i > 0 and RegionManager.depots.has(i):
			_append_model_frame(points, "warehouse", Sites.depot_transform(i))
	_append_model_frame(points, "garage_l%d" % clampi(ProgressionManager.garage_level, 1, 5),
		Sites.garage_transform(ProgressionManager.garage_level))
	for spot in Sites.hq_spots(): points.append(spot.origin)
	return points


func _append_model_frame(points: PackedVector3Array, model: String, xf: Transform3D) -> void:
	var m := Data.meta(model)
	var low: Array = m.min
	var high: Array = m.max
	var box := AABB(Vector3(low[0], low[1], low[2]), Vector3(high[0]-low[0], high[1]-low[1], high[2]-low[2]))
	for corner in 8: points.append(xf * box.get_endpoint(corner))


func _update_bounds() -> void:
	var aabb := AABB(RegionManager.center(0), Vector3.ZERO)
	for i in RegionManager.unlocked_count:
		aabb = aabb.expand(RegionManager.center(i))
	rig.set_bounds(aabb.grow(RegionManager.tile_size() * 0.42))
	rig.fit_active(active_camera_points())
	_apply_far()


func _on_camera_viewport_changed() -> void:
	var was_overview := rig.overview
	_update_bounds()
	if was_overview: rig.frame_home(0.0)
	else: rig._clamp_target()


## Hides / restores every region's world labels (the title card, W66 round 2).
func mute_labels(on: bool) -> void:
	for rv in regions:
		rv.mute_labels(on)


func screen_pos(p: Vector3) -> Vector2:
	var cam := rig.camera
	if cam == null or cam.is_position_behind(p):
		return Vector2(-1000, -1000)
	return cam.unproject_position(p)


func fx_dust(p: Vector3) -> void:
	FX.dust(self, p)


# --- vehicles -------------------------------------------------------------------------
func _spawn_actor(v: Dictionary, animate: bool) -> void:
	if actors.has(v.id):
		return
	var a := VehicleActor.new()
	add_child(a)
	a.setup(v, self, animate)
	a.set_blob_shadow(not sun.shadow_enabled)
	actors[v.id] = a
	if animate:
		AudioManager.play("pop")
		AudioManager.voice("new_vehicle", 5.0)


func parking_transform(vehicle_id: String) -> Transform3D:
	var v := VehicleManager.get_vehicle(vehicle_id)
	var home: String = v.get("home", "hq")
	var idx := 0
	for o in VehicleManager.vehicles:
		if o.id == vehicle_id:
			break
		if o.get("home", "hq") == home:
			idx += 1
	var spots: Array
	if home == "hq":
		spots = Sites.hq_spots()
	else:
		spots = Sites.depot_spots(RegionManager.region_of_node(home))
	if spots.is_empty():
		return Transform3D(Basis.IDENTITY, RegionManager.graph.pos(home))
	var t: Transform3D = spots[idx % spots.size()]
	if idx >= spots.size():
		t.origin += t.basis.z * 4.2 * (idx / spots.size())   # W68: a second row clear of the longest model (3.9 m)
	return t


func _on_vehicle_changed(v: Dictionary) -> void:
	if actors.has(v.id):
		actors[v.id].refresh(v)


func _on_vehicle_upgraded(v: Dictionary, _stat: String) -> void:
	if actors.has(v.id):
		var a: VehicleActor = actors[v.id]
		a.refresh(v)
		a.flash()
		FX.sparkle(self, a.global_position)
		AudioManager.voice("upgrade", 40.0)


func select_vehicle(id: String, follow := true) -> void:
	if selected_vehicle != "" and actors.has(selected_vehicle):
		actors[selected_vehicle].set_selected(false)
	selected_vehicle = id
	if actors.has(id):
		var a: VehicleActor = actors[id]
		a.set_selected(true)
		if follow:
			rig.follow(a, minf(rig.distance, 30.0))


func focus_vehicle(id: String) -> void:
	select_vehicle(id, true)


# --- mentor & company identity ----------------------------------------------------------------
var mentor: CharacterActor


func spawn_mentor() -> void:
	if mentor and is_instance_valid(mentor):
		return
	mentor = CharacterActor.new()
	add_child(mentor)
	mentor.setup("mentor", 1.6)
	var f := Sites.hq_frame()
	var gt := Sites.garage_transform(ProgressionManager.garage_level)
	var depth := float(Data.meta("garage_l%d" % ProgressionManager.garage_level).size[2])
	mentor.global_position = gt.origin - f.away * (depth * 0.5 + 1.2) - f.side * 2.6
	mentor.face(f.pos)


func mentor_anim(state: String) -> void:
	if mentor and is_instance_valid(mentor):
		mentor.set_state(state)


func set_company_color(c: Color) -> void:
	if hq:
		hq.set_company(ProgressionManager.company_name(), c)


# --- route lines ------------------------------------------------------------------------------
## Draws a glowing dashed ribbon along road nodes (lane-offset like vehicles).
func draw_route(nodes: Array, color: Color, from_t := 0.0) -> void:
	var im: ImmediateMesh = route_line.mesh
	im.clear_surfaces()
	if nodes.size() < 2:
		return
	var pts := RegionManager.graph.path_points(nodes)
	var total := 0.0
	for i in range(1, pts.size()):
		total += pts[i - 1].distance_to(pts[i])
	var start_d := total * clampf(from_t, 0.0, 1.0)
	var verts := PackedVector3Array()
	var acc := 0.0
	for i in range(1, pts.size()):
		var a: Vector3 = pts[i - 1]
		var b: Vector3 = pts[i]
		var l := a.distance_to(b)
		var d := (b - a).normalized()
		var side := Vector3(-d.z, 0, d.x)
		var t := 0.0
		while t < l:
			var seg_end := minf(t + 0.9, l)
			if acc + t >= start_d:
				var p0 := a + d * t + side * 0.5
				var p1 := a + d * seg_end + side * 0.5
				var w := side * 0.22
				var y := Vector3(0, 0.09, 0)
				for v in [p0 - w, p1 + w, p0 + w, p0 - w, p1 - w, p1 + w]:
					verts.append((v as Vector3) + y)
			t += 1.5
		acc += l
	if verts.is_empty():
		return
	im.surface_begin(Mesh.PRIMITIVE_TRIANGLES)
	for v in verts:
		im.surface_add_vertex(v)
	im.surface_end()
	route_line.material_override = Models.flat_material(color, 1.0)


func clear_route() -> void:
	(route_line.mesh as ImmediateMesh).clear_surfaces()
	_route_key = ""
	_preview = false


func preview_route(nodes: Array, route_id: String, frame := false) -> void:
	_preview = true
	_route_key = "preview"
	draw_route(nodes, Data.color(Data.route(route_id).get("color", "3D8BD9")))
	if frame and nodes.size() > 1:
		frame_points(RegionManager.graph.path_points(nodes), 0.25)


## Frames points in the part of the screen left visible above a bottom sheet.
func frame_points(pts: PackedVector3Array, visible_frac: float) -> void:
	var aabb := AABB(pts[0], Vector3.ZERO)
	for p in pts:
		aabb = aabb.expand(p)
	var extent := maxf(aabb.size.x, aabb.size.z)
	# perspective: the top band of a portrait screen looks much further away
	var dist := clampf(extent * 1.2 + 16.0, 24.0, 170.0)
	var y := deg_to_rad(rig.yaw)
	var toward_cam := Vector3(sin(y), 0, cos(y))
	var shift := dist * (1.0 - visible_frac) * 0.98
	rig.focus_on(aabb.get_center() + toward_cam * shift, dist, 0.6)


func end_preview() -> void:
	if _preview:
		clear_route()


func _update_route_line() -> void:
	if _preview:
		return
	if selected_vehicle == "":
		if _route_key != "":
			clear_route()
		return
	var job := ContractManager.get_job_for_vehicle(selected_vehicle)
	if job.is_empty() or job.phase != "out":
		if _route_key != "":
			clear_route()
		return
	var p := ContractManager.progress(job)
	var key := "%s_%d" % [job.id, int(p * 40)]
	if key == _route_key:
		return
	_route_key = key
	draw_route(job.path, Data.color(Data.route(job.route).get("color", "3D8BD9")), p)


# --- job feedback -------------------------------------------------------------------------
func _on_job_started(job: Dictionary) -> void:
	_add_marker(job)
	AudioManager.play("dispatch")
	if actors.has(job.vehicle_id):
		var a: VehicleActor = actors[job.vehicle_id]
		a.flash()
		FX.dust(self, a.global_position)
		if job.contract.get("tutorial", false) or ContractManager.jobs.size() == 1:
			select_vehicle(job.vehicle_id, true)


func _add_marker(job: Dictionary) -> void:
	if markers.has(job.id) or job.phase != "out":
		return
	var pin := Models.instance("pin", Models.paint_material(Data.color(
			Data.category(job.contract.category).get("color", "ffd23f"))))
	add_child(pin)
	pin.global_position = RegionManager.graph.pos(job.contract.dest) + Vector3(0, 1.4, 0)
	pin.scale = Vector3.ONE * 1.4
	pin.set_meta("base_y", pin.position.y)
	markers[job.id] = pin


func _remove_marker(job_id: String) -> void:
	if markers.has(job_id):
		var m: Node3D = markers[job_id]
		markers.erase(job_id)
		var t := m.create_tween()
		t.tween_property(m, "scale", Vector3.ZERO, 0.25).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_IN)
		t.tween_callback(m.queue_free)


func _on_job_delivered(job: Dictionary, result: Dictionary) -> void:
	_remove_marker(job.id)
	var dest := RegionManager.graph.pos(job.contract.dest)
	var a: VehicleActor = actors.get(job.vehicle_id)
	var from := a.global_position if a else dest
	if a:
		var v := VehicleManager.get_vehicle(job.vehicle_id)
		UnloadSequence.new().start(self, a, dest, str(job.contract.category), int(job.contract.units),
				Data.color(str(v.get("paint", "E05A47"))))
	FX.confetti(self, dest)
	FX.coins(self, from)
	AudioManager.play("complete")
	var net := float(result.net)
	AudioManager.voice("delivered_big" if net >= maxf(2000.0, EconomyManager.income_per_minute() * 0.5) else "delivered", 25.0)
	var col := Color("5cff8a") if net >= 0 else Color("ff5a5a")
	UIManager.float_text(from + Vector3.UP * 2.0, ("+" if net >= 0 else "") + Fmt.money(net), col)
	if net > 0:
		UIManager.coin_burst(from + Vector3.UP, net)
	if result.late:
		UIManager.float_text(from + Vector3.UP * 3.0, "LATE -30%", Color("ffc93c"))
	if result.get("driver_level_up", false):
		UIManager.float_text(from + Vector3.UP * 3.6, "DRIVER LEVEL UP!", Color("3cf2ff"))
	UIManager.haptic(30)
	if a:
		a.flash()


func _on_job_failed(job: Dictionary, reason: String) -> void:
	_remove_marker(job.id)
	var a: VehicleActor = actors.get(job.vehicle_id)
	var p := a.global_position if a else RegionManager.graph.pos(job.contract.dest)
	FX.smoke(self, p)
	AudioManager.play("fail")
	AudioManager.voice("failed", 10.0)
	UIManager.float_text(p + Vector3.UP * 2.0, "FAILED", Color("ff5a5a"))
	UIManager.toast("%s: %s" % [tr(Data.category(job.contract.category).name), tr(reason)], Color("ff7a7a"), "risk")
	rig.shake(0.5)
	UIManager.haptic(60)


func _on_job_returned(job: Dictionary) -> void:
	_remove_marker(job.id)
	if actors.has(job.vehicle_id):
		var a: VehicleActor = actors[job.vehicle_id]
		FX.dust(self, a.global_position)
		if selected_vehicle == job.vehicle_id and rig.follow_node == a:
			rig.follow_node = null


func _on_event(job: Dictionary) -> void:
	AudioManager.play("event")
	AudioManager.voice("event", 30.0)
	if actors.has(job.vehicle_id):
		var a: VehicleActor = actors[job.vehicle_id]
		if job.event_id in ["breakdown", "engine"]:
			FX.smoke(self, a.global_position)
		rig.shake(0.25)


# --- progression feedback ----------------------------------------------------------------
func _on_region_unlocked(index: int) -> void:
	if index < regions.size():
		regions[index].set_locked(false, true)
		var c := RegionManager.center(index)
		FX.fireworks(self, c)
	for r in regions:
		r.update_lock_label()
	traffic.refresh_count()
	_update_bounds()
	rig.frame_home(1.2)
	_update_region_detail()
	AudioManager.play("unlock")
	AudioManager.voice("region", 2.0)
	UIManager.haptic(80)


func _on_garage_upgraded(level: int) -> void:
	hq.set_level(level, true)
	_update_bounds()
	var gt := Sites.garage_transform(level)
	rig.focus_on(gt.origin, 30.0, 0.8)
	rig.shake(0.6)
	AudioManager.play("upgrade")
	AudioManager.voice("garage", 2.0)
	UIManager.haptic(60)


func _build_depot(index: int, animate: bool) -> void:
	if depots.has(index):
		return
	var root := Node3D.new()
	add_child(root)
	var building := Models.instance("warehouse", Models.paint_material(Color("ffd23f")))
	root.add_child(building)
	building.global_transform = Sites.depot_transform(index)
	var f := Sites.depot_frame(index)
	var pad := MeshInstance3D.new()
	pad.mesh = TerrainBuilder.pad_mesh(Vector2(2.0 * (1.1 + 3.5 * Sites.SPOT_SPACING), 3.6), Color("55585f"), 6, Color("e9e4d8"))
	pad.material_override = Models.terrain_material
	root.add_child(pad)
	pad.global_transform = Transform3D(Basis(f.side, Vector3.UP, -f.away), f.pos + f.away * 2.4)
	var sign := Models.instance("depot_sign", Models.paint_material(Color("ffd23f")))
	root.add_child(sign)
	sign.global_transform = Transform3D(Basis.looking_at(-f.away, Vector3.UP), f.pos + f.away * 3.0 + f.side * 3.4)
	depots[index] = root
	if animate:
		building.scale = Vector3(0.01, 0.01, 0.01)
		var t := create_tween()
		t.tween_property(building, "scale", Vector3.ONE, 0.8).set_trans(Tween.TRANS_ELASTIC).set_ease(Tween.EASE_OUT)
		FX.dust(self, building.global_position)
		FX.confetti(self, building.global_position)
		rig.focus_on(building.global_position, 32.0)
		AudioManager.play("upgrade")
		AudioManager.voice("depot", 2.0)


func _refresh_lock_labels() -> void:
	for rv in regions:
		rv.refresh_name()   # the new next region gets its name back, the ones past it stay nameless
	var nxt := RegionManager.next_locked()
	if nxt >= 0 and nxt < regions.size():
		regions[nxt].update_lock_label()


# --- input ---------------------------------------------------------------------------------
func _on_tap(screen: Vector2) -> void:
	# vehicles first (they're small), then the HQ, then locked regions
	var best := ""
	var best_d := 70.0
	for id in actors:
		var a: VehicleActor = actors[id]
		var sp := screen_pos(a.global_position + Vector3.UP * 0.5)
		var d := sp.distance_to(screen)
		if d < best_d:
			best_d = d
			best = id
	if best != "":
		select_vehicle(best, true)
		AudioManager.play("tap_soft")
		vehicle_tapped.emit(best)
		return
	var gt := Sites.garage_transform(hq.level)
	if screen_pos(gt.origin + Vector3.UP).distance_to(screen) < 110.0:
		AudioManager.play("tap_soft")
		hq_tapped.emit()
		return
	for rv in regions:
		if rv.locked and screen_pos(rv.lock_anchor()).distance_to(screen) < 120.0:
			AudioManager.play("tap_soft")
			region_tapped.emit(rv.index)
			return
	if selected_vehicle != "":
		select_vehicle("", false)


func _on_heat_changed(region: int, _value: float) -> void:
	if region >= 0 and region < regions.size():
		regions[region].update_heat()


func _process(delta: float) -> void:
	_pace(delta)
	# feed vehicle positions to checkpoint arms (10× a second: a fresh array every frame was garbage for nothing)
	_near_t += delta
	var moving := 0
	var pts: Array = []
	var feed := _near_t >= NEAR_EVERY_S
	if feed:
		_near_t = 0.0
		pts = traffic.positions()
	for id in actors:
		var a: VehicleActor = actors[id]
		if feed:
			pts.append(a.global_position)
		if a.moving:
			moving += 1
	var zoomed_out := rig._cur_distance > 58.0
	for rv in regions:
		if feed and rv.checkpoint_arm:
			rv.near_points = pts
		rv.set_label_lod(zoomed_out)
	var t := Time.get_ticks_msec() / 1000.0
	for id in markers:
		var m: Node3D = markers[id]
		m.position.y = float(m.get_meta("base_y")) + sin(t * 3.0) * 0.25
		m.rotation.y += delta * 1.5
	_update_route_line()
	_engine_timer -= delta
	if _engine_timer <= 0:
		_engine_timer = 0.25
		AudioManager.set_engine_activity(moving)



static func _mobile() -> bool:
	return OS.has_feature("mobile") or OS.has_feature("ios") or OS.has_feature("android")


## Frame pacing + the dynamic downgrade + the PERFX probe, once per frame (games/tools/perf). Never in headless runs.
## Under a window the 20 FPS waits until it has been left alone 1.5 s (Play review 10-23: lists scrolled at 20 FPS).
func _pace(delta: float) -> void:
	if DisplayServer.get_name() == "headless":
		return
	_idle_t = PerfProfiles.idle_step(_idle_t, delta, get_viewport()) # a list drag / fling never reaches _input
	if _mobile():
		var cap := PerfProfiles.live_cap(int(profile(GameManager.quality()).get("fps", 60)), UIManager.current_panel != "", _idle_t)
		if Engine.max_fps != cap:
			Engine.max_fps = cap
	perf_tick(Engine.get_frames_per_second(), delta)
	if PerfProbe.enabled():
		var line := perf_probe.frame(delta)
		if line != "":
			print(line)


## 10 s in a row under 25 FPS lowers the tier by one (never raises it); the first time ever a toast says so (the
## graphics notice texts, already in every language). Throttled frames (a cap under 30) are not a slow device.
func perf_tick(fps: float, delta: float) -> int:
	if Engine.max_fps > 0 and Engine.max_fps < 30:
		perf_guard.reset()
		return -1
	var to := perf_guard.sample(fps, delta, PerfDevice.tier_of(GameManager.quality()))
	if to < 0:
		return -1
	var first := not bool(GameManager.settings.get("perf_notice", false))
	if first:
		GameManager.set_setting("perf_notice", true)
	GameManager.set_quality(PerfDevice.tier_name(to), false) # an automatic tier, not the player's pick; saves and applies
	if first:
		UIManager.toast(tr("Graphics are set to Medium for smooth play on this device. You can raise them anytime in Settings → Graphics quality.") \
			if to == 1 else tr("Graphics are set to Low for smooth play on this device. You can raise them anytime in Settings → Graphics quality."))
	return to


## §10: nothing is drawn while the app is in the background (phones only send these notifications); coming back the
## first seconds are a warm-up for the guard, not a slow device, and the idle clock starts again.
func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_PAUSED:
		RenderingServer.render_loop_enabled = false
	elif what == NOTIFICATION_APPLICATION_RESUMED:
		RenderingServer.render_loop_enabled = true
		_idle_t = 0.0
		perf_guard.reset()


func _input(event: InputEvent) -> void:
	if event is InputEventScreenTouch or event is InputEventScreenDrag or event is InputEventMouseButton or event is InputEventKey \
			or event is InputEventMagnifyGesture or event is InputEventPanGesture \
			or (event is InputEventMouseMotion and (event as InputEventMouseMotion).button_mask != 0):
		_idle_t = 0.0
