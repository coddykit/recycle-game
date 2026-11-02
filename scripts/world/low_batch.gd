class_name LowBatch
extends RefCounted
## Low profile draw-call cut (perf gate: draws <= 150, games/CLAUDE.md §0). Every model, MultiMesh and mesh of the diorama
## is its own draw; on Low static things are baked into ONE triangle list per region / per vehicle (one draw per
## material). Medium / High keep the authored nodes (the callers hide or rebuild them), so nothing here changes how
## they look. Colours: the palette shader multiplies the paint cell by COLOR (MultiMesh instance colour); the baked
## vertex COLOR carries the same number, so the baked mesh renders with the very same shared material.

static var _flat_cache := {}  # mesh instance id -> {v, n, uv, c} (de-indexed triangle lists)


## Triangle-list arrays of a mesh with no index buffer (cached): appending instances is then pure array copies.
static func _flat(mesh: Mesh, cache := true) -> Dictionary:
	var key := mesh.get_instance_id()
	if cache and _flat_cache.has(key):
		return _flat_cache[key]
	var v := PackedVector3Array()
	var nrm := PackedVector3Array()
	var uv := PackedVector2Array()
	var col := PackedColorArray()
	var has_col := false
	for s in mesh.get_surface_count():
		if mesh.surface_get_primitive_type(s) != Mesh.PRIMITIVE_TRIANGLES:
			continue
		var a := mesh.surface_get_arrays(s)
		var av: PackedVector3Array = a[Mesh.ARRAY_VERTEX]
		var an = a[Mesh.ARRAY_NORMAL]
		var au = a[Mesh.ARRAY_TEX_UV]
		var ac = a[Mesh.ARRAY_COLOR]
		var ai = a[Mesh.ARRAY_INDEX]
		var n := av.size() if ai == null else (ai as PackedInt32Array).size()
		var idx: PackedInt32Array
		if ai == null:
			idx = PackedInt32Array()
			idx.resize(n)
			for i in n:
				idx[i] = i
		else:
			idx = ai
		var base := v.size()
		v.resize(base + n)
		nrm.resize(base + n)
		uv.resize(base + n)
		col.resize(base + n)
		if ac != null:
			has_col = true
		for i in n:
			var k := idx[i]
			v[base + i] = av[k]
			nrm[base + i] = an[k] if an != null else Vector3.UP
			uv[base + i] = au[k] if au != null else Vector2.ZERO
			col[base + i] = ac[k] if ac != null else Color.WHITE
	var d := {"v": v, "n": nrm, "uv": uv, "c": col if has_col else null}
	if cache:
		_flat_cache[key] = d
	return d


static func new_acc() -> Dictionary:
	return {"v": PackedVector3Array(), "n": PackedVector3Array(), "uv": PackedVector2Array(), "c": PackedColorArray()}


## Appends `mesh` placed at `xf`, its COLOR multiplied by `color` (what a MultiMesh instance colour would do).
static func add(acc: Dictionary, mesh: Mesh, xf: Transform3D, color := Color.WHITE, cache := true) -> void:
	if mesh == null:
		return
	var f := _flat(mesh, cache)
	var fv: PackedVector3Array = f.v
	if fv.is_empty():
		return
	var rot := Transform3D(xf.basis.orthonormalized(), Vector3.ZERO)
	acc.v.append_array(xf * fv)
	acc.n.append_array(rot * (f.n as PackedVector3Array))
	acc.uv.append_array(f.uv)
	var c := PackedColorArray()
	c.resize(fv.size())
	if f.c == null:
		c.fill(color)
	elif color == Color.WHITE:
		c = f.c
	else:
		var fc: PackedColorArray = f.c
		for i in c.size():
			c[i] = fc[i] * color
	acc.c.append_array(c)


static func build(acc: Dictionary) -> ArrayMesh:
	var m := ArrayMesh.new()
	if (acc.v as PackedVector3Array).is_empty():
		return m
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = acc.v
	arrays[Mesh.ARRAY_NORMAL] = acc.n
	arrays[Mesh.ARRAY_TEX_UV] = acc.uv
	arrays[Mesh.ARRAY_COLOR] = acc.c
	m.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return m


## Every MeshInstance3D below `root` (visible ones only) as [mesh, transform relative to root].
static func collect(root: Node3D, rel := Transform3D.IDENTITY, out: Array = []) -> Array:
	for c in root.get_children():
		var n3 := c as Node3D
		if n3 == null or not n3.visible:
			continue
		var xf := rel * n3.transform
		if n3 is MeshInstance3D and (n3 as MeshInstance3D).mesh != null:
			out.append([n3, xf])
		collect(n3, xf, out)
	return out


## Replaces all the meshes under `root` (one shared material_override — the palette / paint material) by one baked
## MeshInstance3D named "LowMerged"; returns it (null when there is nothing to bake or the materials differ).
static func merge_tree(root: Node3D) -> MeshInstance3D:
	var items := collect(root)
	if items.size() < 2:
		return null
	var mat: Material = (items[0][0] as MeshInstance3D).material_override
	var acc := new_acc()
	for it in items:
		var mi := it[0] as MeshInstance3D
		if mi.material_override != mat or mi.mesh is PrimitiveMesh:
			return null
	for it in items:
		add(acc, (it[0] as MeshInstance3D).mesh, it[1])
	var out := MeshInstance3D.new()
	out.name = "LowMerged"
	out.mesh = build(acc)
	out.material_override = mat
	out.cast_shadow = (items[0][0] as MeshInstance3D).cast_shadow
	for it in items:
		var mi := it[0] as MeshInstance3D
		mi.get_parent().remove_child(mi)
		mi.queue_free()
	root.add_child(out)
	return out
