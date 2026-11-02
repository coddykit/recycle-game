class_name RegionDressing
## W67 round 2 (Patron #84): Medium / High set dressing for one region as ONE vertex-coloured mesh (one draw): roadside
## fence runs, bush clusters along the verges, a sign at every town, wooden piers on coastal water edges. Its own RNG
## (the region's prop RNG and placements stay exactly as they were); it only uses free spots. Hidden on Low and while
## the region is locked (region_view._apply_visibility).

const BIOME := {   # [fence/pier wood, bush, bush accent, sign panel]
	"dust_valley": [Color("8a6a48"), Color("8f9a5c"), Color("b7a565"), Color("f28c28")],
	"green_republic": [Color("7a5a3c"), Color("4f8a45"), Color("6fa556"), Color("5dbb63")],
	"free_coast": [Color("9a7a58"), Color("5f9a55"), Color("8fbf6a"), Color("3d8bd9")],
	"iron_federation": [Color("5c6266"), Color("5e7458"), Color("74866a"), Color("8c929c")],
	"red_frontier": [Color("7a4a32"), Color("8a8a50"), Color("a59a5a"), Color("b8573a")],
	"frostland": [Color("6a5848"), Color("4f6f5a"), Color("dfe9ee"), Color("8ec9f0")],
	"golden_isles": [Color("a08058"), Color("4f9a50"), Color("e8c860"), Color("f2c14e")],
	"neon_state": [Color("3a3550"), Color("7a3f9a"), Color("ff4fd8"), Color("ff4fd8")],
}


static func build(rv: Node3D) -> ArrayMesh:
	var pal: Array = BIOME.get(str(rv.def.id), BIOME.green_republic)
	var rng := RandomNumberGenerator.new()
	rng.seed = 104729 * (int(rv.index) + 5)
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var c: Vector3 = rv.center
	var taken: Array = []   # dressing's own spots (Vector2, r)
	var free := func(p: Vector3, r: float) -> bool:
		if not rv._free(p, r, false):
			return false
		for t in taken:
			if Vector2(p.x, p.z).distance_to(t[0]) < r + float(t[1]):
				return false
		return true
	# fence runs and bush clusters along the roads (both sides, outside the shoulder)
	for s in rv._segments:
		var a: Vector3 = s[0]
		var b: Vector3 = s[1]
		var l := a.distance_to(b)
		if l < 6.0:
			continue
		var d := (b - a).normalized()
		var side := Vector3(-d.z, 0, d.x)
		var t := 3.0
		while t < l - 6.0:
			var flip := 1.0 if rng.randf() < 0.5 else -1.0
			var roll := rng.randf()
			var base := a + d * t + side * flip * 2.4
			if roll < 0.32:   # fence run, 4.5 m
				var end := base + d * 4.5
				if free.call(base, 0.4) and free.call(end, 0.4) and free.call((base + end) * 0.5, 0.4):
					_fence(st, base - c, end - c, pal[0])
					taken.append([Vector2((base.x + end.x) * 0.5, (base.z + end.z) * 0.5), 2.4])
				t += 6.0
			elif roll < 0.62:   # bush cluster
				var p := base + side * flip * 0.6
				if free.call(p, 0.9):
					for k in 3:
						var off := Vector3(rng.randf_range(-0.7, 0.7), 0, rng.randf_range(-0.7, 0.7))
						_bush(st, p - c + off, rng.randf_range(0.5, 0.8), pal[1].lerp(pal[2], rng.randf() * 0.6))
					taken.append([Vector2(p.x, p.z), 1.0])
				t += 3.5
			else:
				t += 2.5
	# a sign at every town of the region
	var g = RegionManager.graph
	for id in g.nodes:
		var n: Dictionary = g.nodes[id]
		if int(n.region) != int(rv.index) or n.kind != "town":
			continue
		for tries in 6:
			var ang := rng.randf() * TAU
			var p: Vector3 = n.pos + Vector3(cos(ang), 0, sin(ang)) * 3.6
			if free.call(p, 0.5):
				# W66: blank orange town panels read as floating slabs from the camera.
				# Town labels already identify Dust Valley stops; remove these three panels.
				if int(rv.index) != 0:
					_sign(st, p - c, ang, pal[0], pal[3])
				taken.append([Vector2(p.x, p.z), 0.6])
				break
	# wooden piers into the sea on coastal edges (two per water edge)
	var we: Array = rv._water_edges()
	var h := RegionManager.tile_size() * 0.5
	var dirs := [Vector3(1, 0, 0), Vector3(-1, 0, 0), Vector3(0, 0, 1), Vector3(0, 0, -1)]
	for i in 4:
		if not we[i]:
			continue
		var out: Vector3 = dirs[i]
		var along := Vector3(-out.z, 0, out.x)
		for k in 2:
			var p: Vector3 = out * (h - 1.0) + along * rng.randf_range(-h * 0.6, h * 0.6)
			_pier(st, p, out, pal[0])
	st.generate_normals()
	return st.commit()


static func _fence(st: SurfaceTool, a: Vector3, b: Vector3, col: Color) -> void:
	var n := 4
	for k in n:
		var p := a.lerp(b, float(k) / (n - 1))
		_box(st, p + Vector3(0, 0.35, 0), Vector3(0.09, 0.7, 0.09), col.darkened(0.15))
	for y in [0.3, 0.6]:
		var m := (a + b) * 0.5 + Vector3(0, y, 0)
		var d := b - a
		var len := d.length()
		var basis := Basis.looking_at(d.normalized(), Vector3.UP)
		_box_b(st, m, Vector3(0.05, 0.06, len), col, basis)


static func _bush(st: SurfaceTool, p: Vector3, r: float, col: Color) -> void:
	# a squat rounded clump: a 6-sided ring at 0.45 r, a crown and a foot (12 triangles)
	var crown := p + Vector3(0, r * 1.05, 0)
	var foot := p + Vector3(0, 0.02, 0)
	for i in 6:
		var a0 := TAU * i / 6.0
		var a1 := TAU * (i + 1) / 6.0
		var q0 := p + Vector3(cos(a0) * r, r * 0.45, sin(a0) * r)
		var q1 := p + Vector3(cos(a1) * r, r * 0.45, sin(a1) * r)
		_tri(st, q0, crown, q1, col, col.lightened(0.18))
		_tri(st, q1, foot, q0, col, col.darkened(0.3))


static func _sign(st: SurfaceTool, p: Vector3, ang: float, wood: Color, panel: Color) -> void:
	var basis := Basis(Vector3.UP, ang)
	_box_b(st, p + Vector3(0, 0.75, 0), Vector3(0.1, 1.5, 0.1), wood, basis)
	_box_b(st, p + Vector3(0, 1.35, 0), Vector3(1.0, 0.5, 0.08), panel, basis)


static func _pier(st: SurfaceTool, p: Vector3, out: Vector3, wood: Color) -> void:
	var basis := Basis.looking_at(out, Vector3.UP)
	var deck := p + out * 3.0 + Vector3(0, -0.2, 0)
	_box_b(st, deck, Vector3(1.9, 0.16, 5.0), wood.lightened(0.18), basis)
	for s in [-0.6, 0.6]:
		for f in [1.0, 2.6, 4.2]:
			var q: Vector3 = p + out * float(f) + Vector3(-out.z, 0, out.x) * float(s) + Vector3(0, -0.8, 0)
			_box(st, q, Vector3(0.14, 0.9, 0.14), wood.darkened(0.25))


static func _box(st: SurfaceTool, c: Vector3, size: Vector3, col: Color) -> void:
	_box_b(st, c, size, col, Basis.IDENTITY)


static func _box_b(st: SurfaceTool, c: Vector3, size: Vector3, col: Color, b: Basis) -> void:
	var h := size * 0.5
	var v := []
	for sy in [-1, 1]:
		for corner in [Vector2(-1, -1), Vector2(1, -1), Vector2(1, 1), Vector2(-1, 1)]:
			v.append(c + b * Vector3(corner.x * h.x, sy * h.y, corner.y * h.z))
	var faces := [[4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]]
	for f in faces:
		var k := 1.0 if f[0] == 4 else 0.8
		_tri(st, v[f[0]], v[f[2]], v[f[1]], col * k, col * k)
		_tri(st, v[f[0]], v[f[3]], v[f[2]], col * k, col * k)


static func _tri(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, col: Color, tip_col: Color) -> void:
	st.set_color(col)
	st.add_vertex(a)
	st.set_color(tip_col)
	st.add_vertex(b)
	st.set_color(col)
	st.add_vertex(c)
