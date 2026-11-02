extends Node
## Conservative art checks. Coplanar overlap is a candidate, not a collision oracle.
var issues: Array = []
var models := 0
var mesh_count := 0
var scene_reports: Array = []
const MAX_EXTENT := 200.0
func check_model(node: Node, model_name: String) -> void:
	if node is MeshInstance3D:
		mesh_count += 1
		if node.mesh == null:
			issues.append(model_name + ": missing mesh")
		else:
			var box: AABB = node.mesh.get_aabb()
			if not box.position.is_finite() or not box.size.is_finite() or box.size.length() > MAX_EXTENT or box.size.length() < 0.00001:
				issues.append(model_name + ": invalid bounds")
			if node.material_override == null and node.mesh.get_surface_count() > 0 and node.mesh.surface_get_material(0) == null:
				issues.append(model_name + ": missing material")
	for child in node.get_children(): check_model(child, model_name)

func asset_exists(path: String) -> bool:
	return ResourceLoader.exists(path)

func coplanar(a: AABB, b: AABB, tolerance := 0.005) -> bool:
	if absf(a.position.y - b.position.y) > tolerance: return false
	return minf(a.end.x,b.end.x)-maxf(a.position.x,b.position.x)>0.05 and minf(a.end.z,b.end.z)-maxf(a.position.z,b.position.z)>0.05

func _ready():
	await get_tree().process_frame
	for file in DirAccess.get_files_at("res://assets/models"):
		if not file.ends_with(".glb"): continue
		models += 1
		var model := Models.instance(file.get_basename())
		check_model(model, file)
		model.free()
	var floor_a := AABB(Vector3.ZERO,Vector3(2,0,2))
	var floor_b := AABB(Vector3(1,0.001,1),Vector3(2,0,2))
	var fault_overlap := coplanar(floor_a,floor_b)
	var fault_missing := not asset_exists("res://assets/models/__intentionally_missing__.glb")
	var separated := not coplanar(floor_a,AABB(Vector3(0,1,0),Vector3(2,0,2)))
	var invalid := MeshInstance3D.new()
	check_model(invalid,"injected")
	var fault_mesh := issues.has("injected: missing mesh")
	issues.erase("injected: missing mesh")
	invalid.free()
	_scan_regions()
	var report := {"models":models,"mesh_count":mesh_count,"issues":issues,"regions":scene_reports,
		"fault_injection":{"coplanar_pair":fault_overlap,"missing_asset":fault_missing,"missing_mesh":fault_mesh,"separated_planes":separated},
		"limits":"Conservative static upward-face overlap candidates. Terrain/road layering is intentional. MultiMesh bounds are checked; per-instance contact/occlusion still needs rendered review."}
	var f := FileAccess.open("res://docs/scene-quality.json",FileAccess.WRITE)
	f.store_string(JSON.stringify(report,"  "));f.close()
	print("ART_QA ",JSON.stringify(report))
	get_tree().quit(0 if issues.is_empty() and fault_overlap and fault_missing and fault_mesh and separated else 1)


func _scan_regions() -> void:
	UIKit.theme()
	for quality in ["low", "high"]:
		GameManager.settings.quality = quality
		for i in RegionManager.count():
			var region := RegionView.new()
			add_child(region)
			region.setup(i, UIKit.font_bold)
			region.set_locked(false, false)
			var faces: Array = []
			_collect_faces(region, faces, [region.tile_mi, region.road_mi])
			var candidates: Array = []
			for a in faces.size():
				for b in range(a+1,faces.size()):
					if faces[a].owner == faces[b].owner: continue
					if coplanar(faces[a].box, faces[b].box):
						candidates.append([faces[a].owner,faces[b].owner])
			scene_reports.append({"region":i,"quality":quality,"static_floor_groups":faces.size(),"overlap_candidates":candidates})
			region.free()

func _collect_faces(node: Node, faces: Array, excluded: Array) -> void:
	if node is MultiMeshInstance3D and node.multimesh:
		var mm: MultiMesh = node.multimesh
		for i in mm.instance_count:
			if not mm.get_instance_transform(i).origin.is_finite(): issues.append("non-finite prop transform")
	if node is MeshInstance3D and node.mesh and not node in excluded:
		var groups := {}
		for surface in node.mesh.get_surface_count():
			var arrays: Array = node.mesh.surface_get_arrays(surface)
			if arrays.is_empty(): continue
			var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
			if indices.is_empty():
				for n in vertices.size(): indices.append(n)
			for n in range(0,indices.size()-2,3):
				var a: Vector3 = node.global_transform * vertices[indices[n]]
				var b: Vector3 = node.global_transform * vertices[indices[n+1]]
				var c: Vector3 = node.global_transform * vertices[indices[n+2]]
				var normal := (b-a).cross(c-a).normalized()
				if normal.y < 0.98 or a.y > 1.5: continue
				var key := int(round(a.y / 0.005))
				var box := AABB(a,Vector3.ZERO).expand(b).expand(c)
				if not groups.has(key): groups[key] = []
				groups[key].append(box)
		for key in groups:
			for box in groups[key]:
				faces.append({"owner":str(node.get_path()),"box":box})
	for child in node.get_children(): _collect_faces(child,faces,excluded)
