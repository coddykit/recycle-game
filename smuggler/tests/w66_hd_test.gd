extends Node
## W66 (10-29) model pass gate: the HD files (assets/models/hd, tools/blender/build_w66.py) keep the canonical
## node names / pivots / wheel origins / bounding box and one palette surface per mesh; Low never loads a file from
## hd/ (regions, crossings, HQ, vehicles, traffic); Low -> Medium swaps every HD model in place (same nodes, same
## draws); Medium -> Low puts the canonical meshes back and re-bakes the Low merge with the same triangles.
var checks := 0
var failures := 0
const HD := ["box_truck", "minibus", "sedan", "taxi", "garage_l2", "border_gantry", "bridge", "tunnel", "windmill",
	"cactus", "bush"]


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		push_error("W66_HD " + label)


func _tris(mesh: Mesh) -> int:
	var n := 0
	for s in mesh.get_surface_count():
		var a := mesh.surface_get_arrays(s)
		n += (a[Mesh.ARRAY_INDEX].size() if a[Mesh.ARRAY_INDEX] != null else a[Mesh.ARRAY_VERTEX].size()) / 3
	return n


func _meshes(root: Node) -> Dictionary:
	var out := {}
	for mi in root.find_children("*", "MeshInstance3D", true, false):
		out[str(root.get_path_to(mi))] = mi
	return out


func _aabb(root: Node3D) -> AABB:
	var box := AABB()
	var first := true
	for mi in root.find_children("*", "MeshInstance3D", true, false):
		var m := mi as MeshInstance3D
		var xf: Transform3D = root.global_transform.affine_inverse() * m.global_transform
		var b: AABB = xf * m.mesh.get_aabb()
		box = b if first else box.merge(b)
		first = false
	return box


func _files() -> void:
	for name in HD:
		check(Models.has_hd(name), name + ": hd file exists")
		var canon := (load(Models.MODEL_DIR + name + ".glb") as PackedScene).instantiate() as Node3D
		var hd := (load(Models.HD_DIR + name + ".glb") as PackedScene).instantiate() as Node3D
		add_child(canon)
		add_child(hd)
		var cm := _meshes(canon)
		var hm := _meshes(hd)
		check(cm.keys() == hm.keys(), name + ": same mesh nodes %s / %s" % [cm.keys(), hm.keys()])
		for p in cm:
			if hm.has(p):
				var a := cm[p] as MeshInstance3D
				var b := hm[p] as MeshInstance3D
				check(a.transform.is_equal_approx(b.transform), name + "/" + p + ": same local transform (wheel origin)")
				check(b.mesh.get_surface_count() == 1, name + "/" + p + ": one palette surface")
				check(_tris(b.mesh) >= _tris(a.mesh) * 0.8, name + "/" + p + ": HD is not a reduced copy")
		var m := Data.meta(name)
		var cb := AABB(Vector3(m.min[0], m.min[1], m.min[2]), Vector3(m.size[0], m.size[1], m.size[2])).grow(0.025)
		var hb := _aabb(hd)
		check(cb.encloses(hb), name + ": HD box %s inside canonical %s" % [hb, cb])
		canon.free()
		hd.free()


func _hd_loaded() -> int:
	var n := 0
	for k in Models._scenes:
		if str(k).begins_with(Models.HD_DIR):
			n += 1
	for k in Models._meshes:
		if str(k).begins_with(Models.HD_DIR):
			n += 1
	return n


func _on_tier(w: Node, hd: bool, label: String) -> void:
	var dir := Models.HD_DIR if hd else Models.MODEL_DIR
	var seen := {}
	for rv in w.regions:
		for mmi in rv.multimeshes:
			if mmi.has_meta("hd_model"):
				var id := str(mmi.get_meta("hd_model"))
				seen[id] = true
				check(mmi.multimesh.mesh == Models.mesh(id) and Models.path_of(id).begins_with(dir),
					"%s: region %d %s draws the %s mesh" % [label, rv.index, id, "HD" if hd else "canonical"])
		for n in rv._lockables:
			if n.has_meta("model"):
				var id := str(n.get_meta("model"))
				seen[id] = true
				var src := (load(dir + id + ".glb") as PackedScene).instantiate()
				var want := _meshes(src)
				for p in _meshes(n):
					check(want.has(p) and (want[p] as MeshInstance3D).mesh == (_meshes(n)[p] as MeshInstance3D).mesh,
						"%s: %s/%s from %s" % [label, id, p, dir])
				src.free()
	for id in ["bush", "cactus", "windmill", "bridge", "border_gantry", "tunnel"]:
		check(seen.has(id), label + ": the world has a " + id)
	var g: Node3D = w.hq.garage
	var gsrc := (load(dir + "garage_l2.glb") as PackedScene).instantiate()
	for p in _meshes(gsrc):
		check(_meshes(g).has(p) and (_meshes(g)[p] as MeshInstance3D).mesh == (_meshes(gsrc)[p] as MeshInstance3D).mesh,
			label + ": HQ garage_l2/" + p + " from " + dir)
	gsrc.free()


func _merged_tris(w: Node) -> int:
	var n := 0
	for rv in w.regions:
		if rv._merged_pal:
			n += _tris(rv._merged_pal.mesh)
	return n


func _ready() -> void:
	await get_tree().process_frame
	_files()
	SaveManager.delete_save()
	GameManager.settings["quality"] = "low"
	GameManager.settings["quality_policy_v"] = QualityPolicy.POLICY_VERSION
	GameManager.settings["quality_notice"] = ""
	var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	for i in 10:
		await get_tree().process_frame
	var w: Node = main.world
	EconomyManager.earn(1e9, "w66", false)
	EconomyManager.add_reputation(400)
	RegionManager.unlock(1)
	ProgressionManager.upgrade_garage()
	VehicleManager.add_vehicle("box_truck")
	for i in 150:   # the unlock pop-in (1.6 s) lands, the region bakes on Low
		await get_tree().process_frame
	check(w.hq.level == 2, "HQ is garage_l2 (%d)" % w.hq.level)
	for i in 5:
		await get_tree().process_frame
	check(not Models.hd, "Low: Models.hd is off before and after the world is built")
	check(_hd_loaded() == 0, "Low: no file from hd/ is loaded (%d)" % _hd_loaded())
	_on_tier(w, false, "Low")
	var low_tris := _merged_tris(w)
	check(low_tris > 0, "Low: regions are baked")
	var draws_before := 0
	for rv in w.regions:
		draws_before += rv.multimeshes.size() + rv._lockables.size()
	GameManager.set_quality("medium", false)
	for i in 5:
		await get_tree().process_frame
	check(Models.hd, "Medium: Models.hd on")
	_on_tier(w, true, "Medium")
	var truck: Node = null
	for id in w.actors:
		if str(VehicleManager.get_vehicle(id).get("def", "")) == "box_truck":
			truck = w.actors[id]
	var tsrc := (load(Models.HD_DIR + "box_truck.glb") as PackedScene).instantiate()
	check(truck != null and _meshes(truck.model).has("Body") and (_meshes(truck.model)["Body"] as MeshInstance3D).mesh ==
		(_meshes(tsrc)["Body"] as MeshInstance3D).mesh, "Medium: the player's box truck is rebuilt from hd/")
	tsrc.free()
	var draws_after := 0
	for rv in w.regions:
		draws_after += rv.multimeshes.size() + rv._lockables.size()
	check(draws_after == draws_before, "Medium: same MultiMesh / node count (no extra draws)")
	GameManager.set_quality("low", false)
	for i in 5:
		await get_tree().process_frame
	check(not Models.hd, "back to Low: Models.hd off")
	_on_tier(w, false, "back to Low")
	check(_merged_tris(w) == low_tris, "back to Low: the Low merge has the same triangles (%d / %d)" % [_merged_tris(w), low_tris])
	for c in w.traffic.cars:
		for mi in (c.node as Node).find_children("*", "MeshInstance3D", true, false):
			check(not str((mi as MeshInstance3D).mesh.resource_path).contains("/hd/"), "back to Low: traffic car canonical")
	print("W66_HD checks=%d failures=%d" % [checks, failures])
	main.queue_free()
	for i in 10:
		await get_tree().process_frame
	get_tree().quit(1 if failures else 0)
