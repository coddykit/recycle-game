extends Node
## Low profile draw-call cut (perf gate draws <= 150): a vehicle / traffic model and an unlocked region are baked into
## a handful of meshes on Low, the authored nodes come back on Medium. Run: godot --headless --path . res://tests/low_batch_test.tscn
var failures := 0
var checks := 0


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("LOWBATCH ", "PASS " if ok else "FAIL ", label)


static func _meshes(root: Node, only_visible := true) -> Array:
	var out := []
	var st: Array = [root]
	while not st.is_empty():
		var n: Node = st.pop_back()
		st.append_array(n.get_children())
		if (n is MeshInstance3D or n is MultiMeshInstance3D) and (not only_visible or (n as Node3D).is_visible_in_tree()):
			out.append(n)
	return out


func _ready() -> void:
	await get_tree().process_frame
	# --- a vehicle model: body + 4 wheels -> one mesh, same paint material, same triangles ---
	var paint := Models.paint_material(Color("e5484d"))
	var car: Node3D = Models.instance("hatchback", paint)
	add_child(car)
	var before := _meshes(car)
	var tris := 0
	for mi in before:
		tris += (mi as MeshInstance3D).mesh.get_surface_count()
	check(before.size() >= 3, "the authored car has several meshes (%d)" % before.size())
	var merged := LowBatch.merge_tree(car)
	await get_tree().process_frame
	check(merged != null and _meshes(car).size() == 1, "Low: body + wheels are one mesh")
	check(merged != null and merged.mesh.get_surface_count() == 1, "one surface = one draw")
	check(merged != null and merged.material_override == paint, "the paint material is kept")
	var bb := merged.get_aabb() if merged != null else AABB()
	var m := Data.meta("hatchback")
	check(bb.size.z > float(m.size[2]) * 0.8 and bb.size.x > float(m.size[0]) * 0.8, "the baked car has the model's size")
	Models.apply_material(car, Models.paint_material(Color("3d8bd9")))
	check(merged != null and merged.material_override != paint, "a repaint reaches the baked mesh")
	car.free()
	# --- a region: unlocked -> baked terrain + props; locked / Medium -> authored nodes ---
	var rv := RegionView.new()
	add_child(rv)
	rv.setup(2, UIKit.bold_font())
	rv.set_locked(false, false)
	await get_tree().process_frame
	var authored := _meshes(rv).size()
	check(authored > 8, "the authored region draws many meshes (%d)" % authored)
	rv.set_low(true)
	await get_tree().process_frame
	var low := _meshes(rv)
	var mms := low.filter(func(n): return n is MultiMeshInstance3D)
	check(rv.is_low_merged(), "Low: the unlocked region is baked")
	check(mms.is_empty(), "Low: no prop MultiMesh is drawn")
	check(low.size() <= 8, "Low: the region is <= 8 meshes (%d, was %d)" % [low.size(), authored])
	rv.set_decor_share(0.45)
	await get_tree().process_frame
	check(rv.is_low_merged(), "a new decor share re-bakes, still baked")
	rv.set_low(false)
	await get_tree().process_frame
	check(not rv.is_low_merged() and _meshes(rv).size() >= authored - 1, "Medium: the authored nodes are back")
	rv.set_low(true)
	rv.set_locked(true, false)
	await get_tree().process_frame
	check(not rv.is_low_merged(), "a locked region keeps its authored nodes (they tint per node)")
	rv.free()
	print("LOWBATCH checks=%d failures=%d" % [checks, failures])
	get_tree().quit(1 if failures > 0 else 0)
