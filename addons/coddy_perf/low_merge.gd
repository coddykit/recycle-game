extends RefCounted
## Shared coddy_perf module (games/tools/perf, copied by sync.sh; first used by disaster-command 64e8ce6395, Patron #79).
## Low profile draw-call cut (perf_gate draws ≤ 150): low-poly flat-colour models with several plain albedo materials
## per mesh draw once per material. On Low each such mesh is swapped for ONE surface whose vertex
## colours carry the materials' albedo (one shared vertex-colour material, per-vertex shading, no specular, like the
## W63 Low rule); Medium / High put the authored mesh and its surface materials back. Merged meshes are cached per
## (mesh, colours), so tinted copies get their own and identical models share one.
## Plain surfaces = triangle lists with an opaque BaseMaterial3D without texture, emission or unshaded glow. All plain
## surfaces of a mesh become ONE vertex-colour surface; any other surface (a lamp's emissive glass, a textured sign…)
## stays its own surface with its own material, so a model with 5 plain + 1 lamp surface draws 2 instead of 6
## (canal-control ships / towers, Patron #79). Meshes with fewer than 2 plain surfaces are left as authored.

static var _cache := {}
static var _mat: StandardMaterial3D


static func material() -> StandardMaterial3D:
	if _mat == null:
		_mat = StandardMaterial3D.new()
		_mat.vertex_color_use_as_albedo = true
		# albedo_color is sRGB; vertex colours are linear unless flagged — without this the merged models come out dark
		# and washed out (S3, Corrupt single-mesh Low, 2026-10-22)
		_mat.vertex_color_is_srgb = true
		_mat.shading_mode = BaseMaterial3D.SHADING_MODE_PER_VERTEX
		_mat.specular_mode = BaseMaterial3D.SPECULAR_DISABLED
		_mat.roughness = 1.0
	return _mat


## low = true: merge every eligible MeshInstance3D under root; false: restore the authored meshes.
static func apply(root: Node, low: bool) -> void:
	if root == null:
		return
	var nodes: Array = root.find_children("*", "MeshInstance3D", true, false)
	if root is MeshInstance3D:
		nodes.append(root)
	for mi: MeshInstance3D in nodes:
		if low:
			_merge(mi)
		else:
			_restore(mi)


static func is_merged(mi: MeshInstance3D) -> bool:
	return mi.has_meta("low_merge_orig")


static func _plain(m: Material) -> bool:
	if not (m is BaseMaterial3D):
		return false
	var b := m as BaseMaterial3D
	return b.albedo_texture == null and b.transparency == BaseMaterial3D.TRANSPARENCY_DISABLED and not b.emission_enabled \
		and b.shading_mode != BaseMaterial3D.SHADING_MODE_UNSHADED and not b.vertex_color_use_as_albedo


static func _merge(mi: MeshInstance3D) -> void:
	if is_merged(mi) or mi.material_override != null or not (mi.mesh is ArrayMesh):
		return
	var mesh := mi.mesh as ArrayMesh
	var n := mesh.get_surface_count()
	if n < 2:
		return
	var plain: Array[int] = []
	var colors: Array[Color] = []
	var mats: Array[Material] = []
	for i in n:
		var m := mi.get_active_material(i)
		mats.append(m)
		if mesh.surface_get_primitive_type(i) == Mesh.PRIMITIVE_TRIANGLES and _plain(m):
			plain.append(i)
			colors.append((m as BaseMaterial3D).albedo_color)
	if plain.size() < 2:
		return
	var key := str(mesh.get_rid().get_id()) + ":" + str(colors) + ":" + str(mats.map(func(m): return m.get_instance_id() if m else 0))
	var merged: ArrayMesh = _cache.get(key)
	if merged == null:
		merged = _build(mesh, plain, colors, mats)
		if merged == null:
			return
		_cache[key] = merged
	var overrides := []
	for i in n:
		overrides.append(mi.get_surface_override_material(i))
		mi.set_surface_override_material(i, null)
	mi.set_meta("low_merge_orig", mesh)
	mi.set_meta("low_merge_overrides", overrides)
	mi.mesh = merged


static func _restore(mi: MeshInstance3D) -> void:
	if not is_merged(mi):
		return
	mi.mesh = mi.get_meta("low_merge_orig")
	var overrides: Array = mi.get_meta("low_merge_overrides", [])
	for i in mini(overrides.size(), mi.mesh.get_surface_count()):
		mi.set_surface_override_material(i, overrides[i])
	mi.remove_meta("low_merge_orig")
	mi.remove_meta("low_merge_overrides")


static func _build(mesh: ArrayMesh, plain: Array[int], colors: Array[Color], mats: Array[Material]) -> ArrayMesh:
	var verts := PackedVector3Array()
	var norms := PackedVector3Array()
	var cols := PackedColorArray()
	var idx := PackedInt32Array()
	for p in plain.size():
		var i: int = plain[p]
		var a := mesh.surface_get_arrays(i)
		var v: PackedVector3Array = a[Mesh.ARRAY_VERTEX]
		var nr: Variant = a[Mesh.ARRAY_NORMAL]
		var vc: Variant = a[Mesh.ARRAY_COLOR]
		var ix: Variant = a[Mesh.ARRAY_INDEX]
		var base := verts.size()
		verts.append_array(v)
		if nr is PackedVector3Array and (nr as PackedVector3Array).size() == v.size():
			norms.append_array(nr)
		else:
			for k in v.size():
				norms.append(Vector3.UP)
		var c: Color = colors[p]
		var has_vc: bool = vc is PackedColorArray and (vc as PackedColorArray).size() == v.size()
		for k in v.size():
			cols.append(c * (vc as PackedColorArray)[k] if has_vc else c)
		if ix is PackedInt32Array and (ix as PackedInt32Array).size() > 0:
			for k in (ix as PackedInt32Array):
				idx.append(base + k)
		else:
			for k in v.size():
				idx.append(base + k)
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = norms
	arrays[Mesh.ARRAY_COLOR] = cols
	arrays[Mesh.ARRAY_INDEX] = idx
	var out := ArrayMesh.new()
	out.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	out.surface_set_material(0, material())
	# the other surfaces (emissive lamps, textures…) are copied as they are, with the material they were drawn with
	for i in mesh.get_surface_count():
		if plain.has(i):
			continue
		out.add_surface_from_arrays(mesh.surface_get_primitive_type(i), mesh.surface_get_arrays(i))
		out.surface_set_material(out.get_surface_count() - 1, mats[i])
	return out
