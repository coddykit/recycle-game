class_name RegionView
extends Node3D
## One diorama tile: terrain, roads, towns, nature, border crossing and the
## locked/unlocked presentation. Repeated props are drawn with MultiMesh.

var index := 0
var def: Dictionary = {}
var locked := true
var center := Vector3.ZERO
var _open_tile: ArrayMesh
var _locked_tile: ArrayMesh
var _adobe_serial := 0
var tile_mi: MeshInstance3D
var road_mi: MeshInstance3D
var multimeshes: Array[MultiMeshInstance3D] = []
var _base_xforms := {}       # MultiMeshInstance3D -> Array[Transform3D]
var _pop_scale := {}         # W63: MultiMeshInstance3D -> PackedFloat32Array, the pop scale each instance holds
var _occupied: Array = []    # [Vector2 pos, radius]
var _segments: Array = []    # [Vector3 a, Vector3 b]
var _rng := RandomNumberGenerator.new()
var _anim_t := -1.0
var _lock_root: Node3D
var _clouds: Array[Node3D] = []
var _boats: Array[Node3D] = []
var _name_label: Label3D
var _heat_label: Label3D
var _lock_label: Label3D
var _town_labels: Array[Label3D] = []
var checkpoint_arm: Node3D
var checkpoint_light: MeshInstance3D
var checkpoint_pos := Vector3.INF
var _arm_angle := 0.0
var _arm_basis := Basis.IDENTITY
var _time := 0.0

var _font: Font


func setup(i: int, font: Font) -> void:
	index = i
	def = Data.region_at(i)
	_font = font
	center = RegionManager.center(i)
	position = center
	name = "Region_%s" % def.id
	_rng.seed = 7919 * (i + 3)
	_collect_segments()
	_build_roads()
	_build_crossing()
	_build_towns()
	_build_nature()
	_build_dressing()
	_build_tile()
	_flush_multimesh()
	_build_labels()
	_build_lock()
	set_locked(not RegionManager.is_unlocked(i), false)


# --- construction -------------------------------------------------------------
func _collect_segments() -> void:
	var g := RegionManager.graph
	for e in g.edges:
		var ra := int(g.nodes[e.a].region)
		var rb := int(g.nodes[e.b].region)
		if maxi(ra, rb) == index:
			_segments.append([g.pos(e.a), g.pos(e.b), e.type])
	for id in g.nodes:
		var n: Dictionary = g.nodes[id]
		if int(n.region) == index:
			var r := 4.2 if n.kind == "hub" else (3.0 if n.kind == "town" else 2.0)
			if n.kind == "hq":
				r = 3.0
			_occupy(n.pos, r)
	# keep the HQ / depot building plots clear
	if index == 0:
		var gt := Sites.garage_transform(5)
		_occupy(gt.origin, 6.2)
		for t in Sites.hq_spots():
			_occupy((t as Transform3D).origin, 1.0)
	else:
		_occupy(Sites.depot_transform(index).origin, 2.8)
		for t in Sites.depot_spots(index):
			_occupy((t as Transform3D).origin, 0.9)


func _water_edges() -> Array:
	var g: Array = def.grid
	var has := func(dx: int, dz: int) -> bool:
		for r in Data.regions:
			if int(r.grid[0]) == int(g[0]) + dx and int(r.grid[1]) == int(g[1]) + dz:
				return true
		return false
	return [not has.call(1, 0), not has.call(-1, 0), not has.call(0, 1), not has.call(0, -1)]


func _build_tile() -> void:
	var size := RegionManager.tile_size()
	var coast: bool = def.get("coast", false)
	var mesh := TerrainBuilder.tile_mesh(size, Data.color(def.ground), Data.color(def.ground2),
			Data.color(def.side), 31 * (index + 1), _water_edges(), Color("efd9a0"), coast, _casters, index % 8, _wear_segments())
	tile_mi = MeshInstance3D.new()
	_open_tile = mesh
	_locked_tile = TerrainBuilder.locked_tile(mesh, size)
	tile_mi.mesh = mesh
	tile_mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(tile_mi)


## Road segments in tile-local XZ for the terrain's worn verges (W67).
func _wear_segments() -> Array:
	var out := []
	for s in _segments:
		var a: Vector3 = s[0] - center
		var b: Vector3 = s[1] - center
		out.append([Vector2(a.x, a.z), Vector2(b.x, b.z)])
	return out


func _build_roads() -> void:
	var segs := []
	for s in _segments:
		segs.append([s[0], s[1]])
	var nodes := []
	var g := RegionManager.graph
	for id in g.nodes:
		if int(g.nodes[id].region) == index:
			nodes.append(g.nodes[id].pos)
	var asphalt := Data.color(def.get("road_tint", "45474D"))
	var mesh := TerrainBuilder.road_mesh(segs, center, 2.0, asphalt, asphalt.lightened(0.35),
			Color("f4f1ea"), nodes, true)
	road_mi = MeshInstance3D.new()
	road_mi.mesh = mesh
	road_mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(road_mi)
	# street lights along the roads
	for s in _segments:
		if s[2] != "road":
			continue
		var a: Vector3 = s[0]
		var b: Vector3 = s[1]
		var l := a.distance_to(b)
		var d := (b - a).normalized()
		var side := Vector3(-d.z, 0, d.x)
		var t := 4.0
		var flip := 1.0
		while t < l - 4.0:
			var p := a + d * t + side * flip * 1.7
			if _free(p, 0.4, false):
				_add_prop("street_light", p, _face(p, p - side * flip), 1.0)
				_occupy(p, 0.5)
			t += 7.0
			flip = -flip


func _build_crossing() -> void:
	var g := RegionManager.graph
	for c in g.crossings:
		if int(c.index) != index - 1:
			continue
		var pa := g.pos(c.a)
		var pb := g.pos(c.b)
		var along := (pb - pa).normalized()
		var side := Vector3(-along.z, 0, along.x)
		# bridge across the water gap
		var bridge := Models.instance("bridge")
		bridge.set_meta("model", "bridge")
		add_child(bridge)
		bridge.global_transform = Transform3D(_look_basis(along), (pa + pb) * 0.5)
		_lockables.append(bridge)
		var theme_mat := Models.paint_material(Data.color(str(def.get("checkpoint_color", "E5484D"))))
		var flag := Models.instance("border_flag", theme_mat)
		add_child(flag)
		flag.global_transform = Transform3D(_look_basis(side), pb + along * 0.4 - side * 3.1)
		flag.set_meta("unlocked_mat", theme_mat)
		_lockables.append(flag)
		_occupy(flag.global_position, 0.6)
		if c.type == "highway":
			var gantry := Models.instance("border_gantry", theme_mat)
			gantry.set_meta("model", "border_gantry")
			add_child(gantry)
			gantry.global_transform = Transform3D(_look_basis(along), pb + along * 0.25)
			gantry.set_meta("unlocked_mat", theme_mat)
			_lockables.append(gantry)
			var booth_pos := pb + along * 2.4 + side * 2.4
			_add_caster("checkpoint", booth_pos - center, 1.0)
			var booth := Models.instance(str(def.get("checkpoint_style", "checkpoint")), theme_mat)
			booth.set_meta("unlocked_mat", theme_mat)
			add_child(booth)
			booth.global_transform = Transform3D(_look_basis(-side), booth_pos)
			_lockables.append(booth)
			checkpoint_arm = Node3D.new()
			add_child(checkpoint_arm)
			var arm := Models.instance("checkpoint_arm")
			checkpoint_arm.add_child(arm)
			var pivot := pb + along * 2.4 + side * 1.35
			_arm_basis = Basis(-side, Vector3.UP, -side.cross(Vector3.UP))
			checkpoint_arm.global_transform = Transform3D(_arm_basis, pivot)
			_lockables.append(checkpoint_arm)
			checkpoint_pos = pb + along * 2.4
			checkpoint_light = MeshInstance3D.new()
			var sm := SphereMesh.new()
			sm.radius = 0.16
			sm.height = 0.32
			sm.radial_segments = 8
			sm.rings = 4
			checkpoint_light.mesh = sm
			add_child(checkpoint_light)
			checkpoint_light.global_position = booth_pos + Vector3(0, 1.55, 0)
			_occupy(booth_pos, 1.4)
			for k in 2:
				var bp := pb + along * (0.6 + k * 0.1) - side * (2.0 + k * 0.9)
				_add_prop("barrier", bp, _look_basis(along), 1.0)
				_occupy(bp, 0.8)
		else:
			var tunnel := Models.instance("tunnel")
			tunnel.set_meta("model", "tunnel")
			add_child(tunnel)
			var tp := pb + along * 3.2
			tunnel.global_transform = Transform3D(_look_basis(along), tp)
			_lockables.append(tunnel)
			_occupy(tp + side * 1.8, 1.3)
			_occupy(tp - side * 1.8, 1.3)
			for k in 3:
				var mp := tp + side * (3.2 + k * 1.4) * (1 if k % 2 == 0 else -1) + along * _rng.randf_range(-1.0, 1.5)
				if _free(mp, 1.0, false):
					_add_prop("rock_small", mp, _rand_basis(), _rng.randf_range(1.2, 1.8))
					_occupy(mp, 1.0)


var _lockables: Array[Node3D] = []


func _build_towns() -> void:
	var g := RegionManager.graph
	var houses: Array = def.get("houses", ["house_cottage"])
	var hub_b: Array = def.get("hub_buildings", houses)
	for suffix in ["hub", "a", "b"]:
		var id := "r%d_%s" % [index, suffix]
		if not g.nodes.has(id):
			continue
		var p := g.pos(id)
		var is_hub: bool = suffix == "hub"
		var list: Array = hub_b if is_hub else houses
		var count := 6 if is_hub else 4
		var placed := 0
		var tries := 0
		while placed < count and tries < 80:
			tries += 1
			var ang := _rng.randf() * TAU
			var rad := _rng.randf_range(3.4, 6.8 if is_hub else 5.6)
			var bp := p + Vector3(cos(ang), 0, sin(ang)) * rad
			var model: String = list[placed % list.size()]
			var m := Data.meta(model)
			var footprint := maxf(float(m.size[0]), float(m.size[2])) * 0.55
			if not _free(bp, footprint, true):
				continue
			# Keep original footprint and RNG calls: only the displayed house changes.
			var visual := model
			if model == "house_adobe":
				visual = "adobe_%d" % (_adobe_serial % 6)
				_adobe_serial += 1
			_add_prop(visual, bp, _face(bp, p), 1.0, true)
			_occupy(bp, footprint)
			placed += 1
		# a few crates & a sign at each stop
		for k in 2:
			var cp := p + Vector3(_rng.randf_range(-2.6, 2.6), 0, _rng.randf_range(-2.6, 2.6))
			if _free(cp, 0.35, true):
				_add_prop("crate", cp, _rand_basis(), _rng.randf_range(0.9, 1.3))
				_occupy(cp, 0.4)


func _build_nature() -> void:
	var h := RegionManager.tile_size() * 0.5 - 1.4
	var mountains: Array = def.get("mountains", [])
	for i in int(def.get("mountain_count", 0)):
		if mountains.is_empty():
			break
		for _t in 30:
			var corner := Vector3(_rng.randf_range(-h, h), 0, _rng.randf_range(-h, h))
			if absf(corner.x) < h * 0.55 and absf(corner.z) < h * 0.55:
				continue
			if _free(corner, 3.6, true):
				_add_prop(mountains.pick_random(), corner, _rand_basis(), _rng.randf_range(0.8, 1.15))
				_occupy(corner, 3.6)
				break
	var trees: Array = def.get("trees", [])
	var n_trees := int(def.get("tree_count", 0))
	_scatter(trees, n_trees, 0.6, 0.75, 1.35, h)
	_scatter(def.get("rocks", []), int(def.get("rock_count", 0)), 0.6, 0.7, 1.3, h)
	# boats on coastal waters
	if def.get("coast", false):
		var we := _water_edges()
		var dirs := [Vector3.RIGHT, Vector3.LEFT, Vector3.BACK, Vector3.FORWARD]
		for k in 4:
			if not we[k]:
				continue
			for j in 2:
				var off: Vector3 = dirs[k] * (h + 5.0 + j * 3.0)
				var lateral := Vector3(-dirs[k].z, 0, dirs[k].x) * _rng.randf_range(-12, 12)
				var boat := Models.instance("yacht" if index >= 6 or j == 1 else "boat")
				add_child(boat)
				boat.global_position = center + off + lateral + Vector3(0, -0.75, 0)
				boat.rotation.y = _rng.randf() * TAU
				_boats.append(boat)
				_lockables.append(boat)


func _scatter(models: Array, count: int, radius: float, smin: float, smax: float, h: float) -> void:
	if models.is_empty():
		return
	var placed := 0
	var tries := 0
	while placed < count and tries < count * 12:
		tries += 1
		var p := center + Vector3(_rng.randf_range(-h, h), 0, _rng.randf_range(-h, h))
		# loose clustering keeps forests readable
		if _rng.randf() < 0.5 and placed > 0:
			var anchor: Array = _occupied[_rng.randi_range(0, _occupied.size() - 1)]
			p = Vector3(anchor[0].x, 0, anchor[0].y) + Vector3(_rng.randf_range(-3, 3), 0, _rng.randf_range(-3, 3))
			if absf(p.x - center.x) > h or absf(p.z - center.z) > h:
				continue
		var s := _rng.randf_range(smin, smax)
		if not _free(p, radius * s, true):
			continue
		_add_prop(models.pick_random(), p, _rand_basis(), s)
		_occupy(p, radius * s)
		placed += 1


# --- placement helpers --------------------------------------------------------
func _decor_models() -> Array:
	var list: Array = []
	for key in ["trees", "rocks", "mountains"]:
		list.append_array(def.get(key, []))
	return list


var _props := {}  # model -> Array[[Transform3D, Color]]
## Art pass 2 (10-27): decor models that also ship a "<model>_low" mesh (<= the old model's triangles) swapped in on
## Low by set_low() — the Medium / High mesh carries the new detail.
const LOW_VARIANTS := ["mountain_desert", "rock_desert"]


func _add_prop(model: String, world_pos: Vector3, basis: Basis, scale_f: float, colored := false) -> void:
	if not _props.has(model):
		_props[model] = []
	var col := Color.WHITE
	if colored:
		var cols: Array = def.get("house_colors", ["F4F1EA"])
		col = Data.color(cols[_rng.randi_range(0, cols.size() - 1)])
	var local := world_pos - center
	_props[model].append([Transform3D(basis.scaled(Vector3.ONE * scale_f), local), col])
	_add_caster(model, local, scale_f)


var _casters: Array = []


func _add_caster(model: String, local: Vector3, scale_f: float) -> void:
	var m := Data.meta("house_adobe" if model.begins_with("adobe_") else model)
	var h := float(m.size[1]) * scale_f
	var foot := maxf(float(m.size[0]), float(m.size[2])) * 0.5 * scale_f
	var off := TerrainBuilder.shadow_dir() * minf(h * 0.35, 2.5)
	_casters.append([Vector2(local.x + off.x * 0.5, local.z + off.z * 0.5), foot + 0.35 + h * 0.18,
			clampf(0.18 + h * 0.06, 0.18, 0.4)])


func _flush_multimesh() -> void:
	for model in _props:
		var list: Array = _props[model]
		var mm := MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.use_colors = true
		# Green Republic is the first locked tile: three silhouettes, same groups/draws.
		var plant := {"tree_round": "desert_plant_0", "bush": "desert_plant_1", "tree_pine": "desert_plant_2"}
		mm.mesh = Models.mesh(str(plant.get(model, model)) if index == 1 else model)
		mm.instance_count = list.size()
		var xf: Array[Transform3D] = []
		for i in list.size():
			mm.set_instance_transform(i, list[i][0])
			mm.set_instance_color(i, list[i][1])
			xf.append(list[i][0])
		var mmi := MultiMeshInstance3D.new()
		mmi.multimesh = mm
		mmi.name = "MM_" + model
		var shown: String = str(plant.get(model, model)) if index == 1 else model
		if Models.has_hd(shown):   # W66: Medium / High draw hd/<model>, swapped in place on a tier change
			mmi.set_meta("hd_model", shown)
		if model.begins_with("adobe_"):
			mmi.set_meta("adobe_model", model)
		elif model in LOW_VARIANTS:   # art pass 2: Low draws the <= old-cost "<model>_low" mesh
			mmi.set_meta("low_model", model)
		if model in _decor_models():
			mmi.set_meta("decor", true)
		mmi.material_override = Models.base_material
		add_child(mmi)
		multimeshes.append(mmi)
		_base_xforms[mmi] = xf
	_props.clear()


func _occupy(p: Vector3, r: float) -> void:
	_occupied.append([Vector2(p.x, p.z), r])


func _free(p: Vector3, r: float, check_roads: bool) -> bool:
	var h := RegionManager.tile_size() * 0.5 - 0.8
	if absf(p.x - center.x) > h - r * 0.5 or absf(p.z - center.z) > h - r * 0.5:
		return false
	var p2 := Vector2(p.x, p.z)
	for o in _occupied:
		if p2.distance_to(o[0]) < r + float(o[1]):
			return false
	if check_roads:
		for s in _segments:
			var a := Vector2(s[0].x, s[0].z)
			var b := Vector2(s[1].x, s[1].z)
			var q := Geometry2D.get_closest_point_to_segment(p2, a, b)
			if p2.distance_to(q) < r + 1.5:
				return false
	return true


func _face(from: Vector3, to: Vector3) -> Basis:
	var d := to - from
	d.y = 0
	if d.length() < 0.01:
		return Basis.IDENTITY
	return _look_basis(d.normalized())


## Basis whose -Z (model front) points along `dir`.
static func _look_basis(dir: Vector3) -> Basis:
	dir.y = 0
	if dir.length() < 0.001:
		return Basis.IDENTITY
	return Basis.looking_at(dir.normalized(), Vector3.UP)


func _rand_basis() -> Basis:
	return Basis(Vector3.UP, _rng.randf() * TAU)


# --- labels & lock --------------------------------------------------------------
func _make_label(text: String, size: int, color: Color, outline := 14) -> Label3D:
	var l := Label3D.new()
	l.text = text
	l.font = _font
	l.font_size = size
	l.outline_size = outline
	l.modulate = color
	l.outline_modulate = Color(0.08, 0.09, 0.15, 0.9)
	l.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	l.pixel_size = 0.016
	l.no_depth_test = true
	l.render_priority = 2
	l.outline_render_priority = 1
	l.double_sided = true
	return l


func _build_labels() -> void:
	_name_label = _make_label(UIKit.upper(tr(str(def.name))), 150, Color.WHITE, 32)
	_name_label.position = Vector3(0, 8.6, 0)
	add_child(_name_label)
	_heat_label = _make_label("", 90, Color.WHITE, 24)
	_heat_label.position = Vector3(0, 6.0, 0)
	add_child(_heat_label)
	var g := RegionManager.graph
	for suffix in ["hub", "a", "b"]:
		var id := "r%d_%s" % [index, suffix]
		if not g.nodes.has(id):
			continue
		var l := _make_label(str(g.nodes[id].name), 60 if suffix == "hub" else 50, Color("fff6dc"), 18)
		add_child(l)
		l.global_position = g.pos(id) + Vector3(0, 2.6 if suffix == "hub" else 2.0, 0)
		_town_labels.append(l)
	update_heat()


var _lod_far := false
## W66 round 2 (10-27): the title card shows the diorama without world labels (the town name Rustwater sat under the subtitle).
var labels_muted := false


func mute_labels(on: bool) -> void:
	labels_muted = on
	if _name_label == null:
		return
	_name_label.visible = _name_shown()
	_heat_label.visible = not on and _lod_far
	for l in _town_labels:
		l.visible = not on and not locked and not _lod_far
	if _lock_label != null:
		_lock_label.visible = not on


## The region's name: far away, or a locked region the player can buy next. A locked region past the next one stays
## nameless under its clouds (W66 10-27, #158-2: their names peeked out at the World Map's top edge).
func _name_shown() -> bool:
	if labels_muted or not (_lod_far or locked):
		return false
	return not locked or index <= RegionManager.unlocked_count


func refresh_name() -> void:
	if _name_label != null:
		_name_label.visible = _name_shown()


## Far away: show region names. Close up: show town names (less clutter).
func set_label_lod(far: bool) -> void:
	if far == _lod_far and _name_label.visible == _name_shown():
		return
	_lod_far = far
	_name_label.visible = _name_shown()
	_heat_label.visible = far and not labels_muted
	for l in _town_labels:
		l.visible = not locked and not far and not labels_muted


func update_heat() -> void:
	if _heat_label == null:
		return
	if locked:
		_heat_label.text = ""
		return
	var h := HeatManager.get_heat(index)
	_heat_label.text = tr("HEAT %d  %s") % [int(h), UIKit.upper(Fmt.heat_label(h))]
	_heat_label.modulate = Fmt.heat_color(h)
	if checkpoint_light:
		checkpoint_light.material_override = Models.flat_material(Fmt.heat_color(h), 1.0 if h > 50 else 0.0)


func _build_lock() -> void:
	_lock_root = Node3D.new()
	add_child(_lock_root)
	var lock := Models.instance("lock")
	lock.scale = Vector3.ONE * 2.2
	lock.position = Vector3(0, 3.2, 0)
	lock.name = "LockIcon"
	_lock_root.add_child(lock)
	_lock_label = _make_label("", 120, Color("ffd23f"), 28)
	_lock_label.position = Vector3(0, 1.6, 0)
	_lock_root.add_child(_lock_label)
	for k in 4:
		var cl := Models.instance("cloud")
		cl.position = Vector3(_rng.randf_range(-13, 13), _rng.randf_range(4.5, 7.0), _rng.randf_range(-13, 13))
		cl.scale = Vector3.ONE * _rng.randf_range(1.6, 2.6)
		cl.set_meta("phase", _rng.randf() * TAU)
		for c in cl.get_children():
			if c is GeometryInstance3D:
				(c as GeometryInstance3D).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		_lock_root.add_child(cl)
		_clouds.append(cl)
	update_lock_label()


func update_lock_label() -> void:
	if _lock_label == null:
		return
	var check := RegionManager.unlock_check(index)
	var price := Fmt.money(float(def.unlock_cost))
	var txt := "%s   ★ %d" % [price, int(def.unlock_rep)]
	if index > RegionManager.unlocked_count:
		txt = "LOCKED"
	_lock_label.text = txt
	_lock_label.modulate = Color("5cff8a") if check.ok else Color("ffd23f")


## Quality tiers keep only a share of the decorative scenery (trees, rocks, mountains), never the
## buildings. Instances are placed in random order, so a prefix is an even thinning.
func set_decor_share(share: float) -> void:
	if not is_equal_approx(share, _decor_share):
		_decor_share = share
		_drop_low() # the baked props carry the old share
	for mmi in multimeshes:
		if mmi.has_meta("decor"):
			var total: int = mmi.multimesh.instance_count
			mmi.multimesh.visible_instance_count = total if share >= 1.0 else maxi(1, int(ceil(total * share)))
	_refresh_low()


## Far-future regions only show their tile, roads and sign (fewer draw calls).
func set_detail(on: bool) -> void:
	_detail = on
	_apply_visibility()


# --- Low profile: one draw per material instead of one per prop / model (LowBatch, perf gate draws <= 150) -------------
var _low := false
var _detail := true
var _decor_share := 1.0
var _merged_pal: MeshInstance3D   # every prop MultiMesh + static crossing model of this region, one surface
var _merged_ter: MeshInstance3D   # tile + roads, one surface
var _merged_nodes: Array = []     # the _lockables folded into _merged_pal
var _merged_on := false


## Low: unlocked regions draw their terrain and props as two baked meshes (a locked region and the unlock pop-in keep
## the authored nodes, they animate / tint per node). Medium / High: the authored nodes, the baked meshes freed.
func set_low(on: bool) -> void:
	if _low != on:
		_drop_low()
	_low = on
	for mmi in multimeshes:
		if mmi.has_meta("adobe_model") or mmi.has_meta("low_model"):
			var model := str(mmi.get_meta("adobe_model", mmi.get_meta("low_model", "")))
			mmi.multimesh.mesh = Models.mesh(model + "_low" if on else model)
		elif mmi.has_meta("hd_model"):   # W66: Models.hd already follows the tier (World.apply_quality)
			mmi.multimesh.mesh = Models.mesh(str(mmi.get_meta("hd_model")))
	for n in _lockables:
		if n.has_meta("model"):
			Models.swap_meshes(n, str(n.get_meta("model")))
	_refresh_low()


func is_low_merged() -> bool:
	return _merged_on


func _refresh_low() -> void:
	if tile_mi == null or road_mi == null:
		return
	var want := _low and not locked and _anim_t < 0.0
	if want and _merged_pal == null:
		_bake_low()
	elif not want and _merged_pal != null:
		_drop_low()
	_merged_on = want and _merged_pal != null
	_apply_visibility()


func _drop_low() -> void:
	for m in [_merged_pal, _merged_ter]:
		if m != null:
			m.queue_free()
	_merged_pal = null
	_merged_ter = null
	_merged_nodes.clear()
	_merged_on = false
	if is_inside_tree() and tile_mi != null:
		_apply_visibility()


## W67 round 2: Medium / High set dressing, one vertex-coloured mesh per region (RegionDressing).
static var _dressing_mat: StandardMaterial3D
var _dressing: MeshInstance3D


func _build_dressing() -> void:
	if _dressing_mat == null:
		_dressing_mat = StandardMaterial3D.new()
		_dressing_mat.vertex_color_use_as_albedo = true
		_dressing_mat.vertex_color_is_srgb = true
		_dressing_mat.roughness = 1.0
		_dressing_mat.cull_mode = BaseMaterial3D.CULL_DISABLED   # thin rails / decks are seen from both sides
	_dressing = MeshInstance3D.new()
	_dressing.mesh = RegionDressing.build(self)
	_dressing.material_override = _dressing_mat
	_dressing.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(_dressing)


func _apply_visibility() -> void:
	if _dressing:
		_dressing.visible = _detail and not _low and not locked
	for mmi in multimeshes:
		mmi.visible = _detail and not _merged_on
	for n in _lockables:
		n.visible = _detail and not (_merged_on and n in _merged_nodes)
	if _merged_pal:
		_merged_pal.visible = _detail
	if tile_mi:
		tile_mi.visible = not _merged_on
		road_mi.visible = not _merged_on


func _bake_low() -> void:
	var acc := LowBatch.new_acc()
	for mmi in multimeshes:
		var mm := mmi.multimesh
		var xf: Array = _base_xforms[mmi]
		var n := mm.instance_count if mm.visible_instance_count < 0 else mm.visible_instance_count
		for i in mini(n, xf.size()):
			LowBatch.add(acc, mm.mesh, xf[i], mm.get_instance_color(i))
	for n in _lockables:
		if n == checkpoint_arm or n in _boats:
			continue
		var paint := Color.WHITE
		var um = n.get_meta("unlocked_mat") if n.has_meta("unlocked_mat") else null
		if um is ShaderMaterial:
			paint = (um as ShaderMaterial).get_shader_parameter("paint_color").srgb_to_linear()
		for it in LowBatch.collect(n, n.transform):
			LowBatch.add(acc, (it[0] as MeshInstance3D).mesh, it[1], paint)
		_merged_nodes.append(n)
	_merged_pal = MeshInstance3D.new()
	_merged_pal.mesh = LowBatch.build(acc)
	_merged_pal.material_override = Models.base_material
	_merged_pal.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(_merged_pal)
	var ter := LowBatch.new_acc()
	LowBatch.add(ter, tile_mi.mesh, Transform3D.IDENTITY, Color.WHITE, false)
	LowBatch.add(ter, road_mi.mesh, Transform3D.IDENTITY, Color.WHITE, false)
	_merged_ter = MeshInstance3D.new()
	_merged_ter.mesh = LowBatch.build(ter)
	_merged_ter.material_override = Models.terrain_material
	_merged_ter.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(_merged_ter)


func lock_anchor() -> Vector3:
	return center + Vector3(0, 3.5, 0)


func set_locked(value: bool, animate: bool) -> void:
	locked = value
	tile_mi.mesh = _locked_tile if locked else _open_tile
	var mat: Material = Models.locked_material if locked else Models.base_material
	for mmi in multimeshes:
		mmi.material_override = mat
	for n in _lockables:
		Models.apply_material(n, Models.locked_material if locked else n.get_meta("unlocked_mat", Models.base_material))
	tile_mi.material_override = Models.terrain_locked_material if locked else Models.terrain_material
	road_mi.material_override = Models.terrain_locked_material if locked else Models.terrain_material
	_lock_root.visible = locked
	_apply_visibility()
	for l in _town_labels:
		l.visible = not locked and not _lod_far and not labels_muted
	_name_label.modulate = Color(1, 1, 1, 0.75) if locked else Color.WHITE
	_name_label.visible = _name_shown()
	if checkpoint_light:
		checkpoint_light.visible = not locked
	update_heat()
	update_lock_label()
	if animate and not locked:
		_anim_t = 0.0
		_apply_pop(0.0)
	_refresh_low()


func _apply_pop(t: float) -> void:
	for mmi in multimeshes:
		var xf: Array = _base_xforms[mmi]
		var mm := mmi.multimesh
		var last: PackedFloat32Array = _pop_scale.get(mmi, PackedFloat32Array())
		if last.size() != xf.size():
			last.resize(xf.size())
			last.fill(-1.0)
		for i in xf.size():
			var base: Transform3D = xf[i]
			var dist := Vector2(base.origin.x, base.origin.z).length() / 28.0
			var local_t := clampf((t - dist * 0.9) / 0.5, 0.0, 1.0)
			var s := maxf(_back_out(local_t), 0.001)
			if s == last[i]:
				continue   # W63: not started yet / already landed — nothing to write
			last[i] = s
			mm.set_instance_transform(i, Transform3D(base.basis.scaled(Vector3.ONE * s), base.origin))
		_pop_scale[mmi] = last


static func _back_out(t: float) -> float:
	var c1 := 1.70158
	var c3 := c1 + 1.0
	return 1.0 + c3 * pow(t - 1.0, 3) + c1 * pow(t - 1.0, 2)


func _process(delta: float) -> void:
	_time += delta
	if _anim_t >= 0.0:
		_anim_t += delta
		_apply_pop(_anim_t)
		if _anim_t > 1.6:
			_anim_t = -1.0
			_apply_pop(10.0)
			_refresh_low()
	if locked:
		for cl in _clouds:
			var ph: float = cl.get_meta("phase")
			cl.position.x += sin(_time * 0.2 + ph) * delta * 0.4
			cl.position.y += sin(_time * 0.7 + ph) * delta * 0.15
		var lock := _lock_root.get_node_or_null("LockIcon")
		if lock:
			lock.rotation.y += delta * 0.8
			lock.position.y = 3.2 + sin(_time * 2.0) * 0.25
	for b in _boats:
		b.position.y = -0.75 + sin(_time * 1.3 + b.position.x) * 0.06
		b.rotation.z = sin(_time * 1.1 + b.position.z) * 0.04
	if checkpoint_arm:
		var target := 0.0
		if not locked and _vehicle_near():
			target = 1.35
		_arm_angle = lerpf(_arm_angle, target, minf(1.0, delta * 6.0))
		checkpoint_arm.basis = _arm_basis * Basis(Vector3(0, 0, 1), _arm_angle)


var near_points: Array = []  # filled by World each frame (vehicle positions)


func _vehicle_near() -> bool:
	if checkpoint_pos == Vector3.INF:
		return false
	for p in near_points:
		if (p as Vector3).distance_squared_to(checkpoint_pos) < 9.0:
			return true
	return false
