class_name TerrainBuilder
## Procedural meshes for the diorama: region tiles, roads, parking lots.
## Everything is vertex coloured and drawn with one shared terrain material.

const TILE_DEPTH := 2.2
## W67 material codes in the vertex alpha, read by terrain.gdshader (tile tops: biome (region index) → (i + 0.5) / 16).
const CODE_CLIFF := 0.55
const CODE_SHOULDER := 0.65
const CODE_ASPHALT := 0.75
const CODE_MARKING := 0.85
const SUN_EULER_DEG := Vector3(-52, 28, 0)


## Horizontal direction shadows fall in (used to offset baked contact shadows).
static func shadow_dir() -> Vector3:
	var b := Basis.from_euler(Vector3(deg_to_rad(SUN_EULER_DEG.x), deg_to_rad(SUN_EULER_DEG.y), 0))
	var d: Vector3 = -b.z
	d.y = 0
	return d.normalized()


## A diorama tile: gently mottled top, darker cliff sides.
## `water_edges` = [+x, -x, +z, -z] booleans; those edges get a beach band.
## `casters` = [[Vector2 centre (tile local), radius, strength], ...] baked as
## soft contact shadows into the vertex colours (cheap "baked lighting").
static func tile_mesh(size: float, c1: Color, c2: Color, side: Color, rng_seed: int,
		water_edges: Array, beach: Color, beach_on: bool, casters: Array = [], biome := -1,
		wear: Array = []) -> ArrayMesh:
	var top_a := (float(biome) + 0.5) / 16.0 if biome >= 0 else 1.0
	var side_a := CODE_CLIFF if biome >= 0 else 1.0
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var noise := FastNoiseLite.new()
	noise.seed = rng_seed
	noise.frequency = 0.07
	var n := 40
	var h := size * 0.5
	var step := size / n
	var cols := []
	for z in n + 1:
		var row := []
		for x in n + 1:
			var px := -h + x * step
			var pz := -h + z * step
			var t := clampf(noise.get_noise_2d(px, pz) * 1.4 + 0.5, 0.0, 1.0)
			var c := c1.lerp(c2, smoothstep(0.35, 0.75, t))
			if beach_on:
				var d := 99.0
				if water_edges[0]:
					d = minf(d, h - px)
				if water_edges[1]:
					d = minf(d, px + h)
				if water_edges[2]:
					d = minf(d, h - pz)
				if water_edges[3]:
					d = minf(d, pz + h)
				c = c.lerp(beach, 1.0 - smoothstep(1.5, 3.2, d))
			var ao := 0.0
			var vp := Vector2(px, pz)
			for cs in casters:
				var dist: float = vp.distance_to(cs[0])
				var r: float = cs[1]
				if dist < r:
					ao += float(cs[2]) * (1.0 - smoothstep(r * 0.35, r, dist))
			c = c.darkened(clampf(ao, 0.0, 0.42))
			# W67: worn, dusty verges along the roads (`wear` = [[Vector2 a, Vector2 b], ...] tile local) — grounds the
			# asphalt in the terrain; baked into the vertex colours, zero cost
			if not wear.is_empty():
				var dw := 99.0
				for sg in wear:
					dw = minf(dw, _seg_dist(vp, sg[0], sg[1]))
				var k := 1.0 - smoothstep(1.4, 3.6, dw)
				c = c.lerp(c1.darkened(0.20).lerp(Color("b9a27e"), 0.30), k * 0.75)
			c.a = top_a
			row.append(c)
		cols.append(row)
	st.set_normal(Vector3.UP)
	for z in n:
		for x in n:
			var p00 := Vector3(-h + x * step, 0, -h + z * step)
			var p10 := p00 + Vector3(step, 0, 0)
			var p01 := p00 + Vector3(0, 0, step)
			var p11 := p00 + Vector3(step, 0, step)
			_v(st, p00, cols[z][x])
			_v(st, p10, cols[z][x + 1])
			_v(st, p11, cols[z + 1][x + 1])
			_v(st, p00, cols[z][x])
			_v(st, p11, cols[z + 1][x + 1])
			_v(st, p01, cols[z + 1][x])
	# cliff sides
	var top_c := Color(side, side_a)
	var bot_c := Color(side.darkened(0.45), side_a)
	var lip_hi := Color(c1.darkened(0.08), top_a)
	var lip_lo := Color(c1.darkened(0.18), top_a)
	var corners := [Vector3(-h, 0, -h), Vector3(h, 0, -h), Vector3(h, 0, h), Vector3(-h, 0, h)]
	for i in 4:
		var a: Vector3 = corners[i]
		var b: Vector3 = corners[(i + 1) % 4]
		var nrm := (b - a).cross(Vector3.DOWN).normalized()
		var ad := a + Vector3.DOWN * TILE_DEPTH
		var bd := b + Vector3.DOWN * TILE_DEPTH
		st.set_normal(nrm)
		# lip strip in ground colour for a crisp grassy edge
		var lip := 0.18
		var al := a + Vector3.DOWN * lip
		var bl := b + Vector3.DOWN * lip
		_v(st, a, lip_hi)
		_v(st, al, lip_lo)
		_v(st, b, lip_hi)
		_v(st, b, lip_hi)
		_v(st, al, lip_lo)
		_v(st, bl, lip_lo)
		_v(st, al, top_c)
		_v(st, ad, bot_c)
		_v(st, bl, top_c)
		_v(st, bl, top_c)
		_v(st, ad, bot_c)
		_v(st, bd, bot_c)
	return st.commit()


## W66 locked preview: existing dune detail, muted sand and a dashed boundary baked
## into the tile's ONE surface. No shader branch, texture or extra draw/pass.
static func locked_tile(source: ArrayMesh, size: float) -> ArrayMesh:
	var arrays := source.surface_get_arrays(0)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR]
	for i in vertices.size():
		if vertices[i].y < -0.01:
			continue
		var c := colors[i]
		var luminance := c.r * 0.299 + c.g * 0.587 + c.b * 0.114
		# Art pass 2 (10-27, W66 model #3): a locked land keeps half of its own ground (half desaturated) and its
		# per-vertex relief / wear / shade instead of one flat sand for every region — green, snow, coast or neon
		# still read through the lock tint. Vertex colours only: same mesh, same draw.
		var biome := c.lerp(Color(luminance, luminance, luminance), 0.45)
		var sand := Color("c9b997").lerp(biome, 0.55).darkened(clampf((0.75 - luminance) * 0.45, 0.0, 0.25))
		sand.a = 0.5 / 16.0 # reuse Dust Valley dune code, even on the low material
		colors[i] = sand
	arrays[Mesh.ARRAY_COLOR] = colors
	var terrain := ArrayMesh.new()
	terrain.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	var strip := SurfaceTool.new()
	strip.begin(Mesh.PRIMITIVE_TRIANGLES)
	strip.set_normal(Vector3.UP)
	var h := size * 0.5 - 0.45
	var corners := [Vector3(-h, 0, -h), Vector3(h, 0, -h), Vector3(h, 0, h), Vector3(-h, 0, h)]
	for edge in 4:
		var a: Vector3 = corners[edge]
		var b: Vector3 = corners[(edge + 1) % 4]
		var d := (b - a).normalized()
		var t := 0.0
		while t < a.distance_to(b):
			_strip(strip, a + d * t, a + d * minf(t + 1.15, a.distance_to(b)), 0.32, 0.012,
					Color("8a7552"), 0.0)
			t += 2.4
	var acc := LowBatch.new_acc()
	LowBatch.add(acc, terrain, Transform3D.IDENTITY, Color.WHITE, false)
	LowBatch.add(acc, strip.commit(), Transform3D.IDENTITY, Color.WHITE, false)
	return LowBatch.build(acc)


static func _seg_dist(p: Vector2, a: Vector2, b: Vector2) -> float:
	var ab := b - a
	var t := clampf((p - a).dot(ab) / maxf(ab.length_squared(), 0.0001), 0.0, 1.0)
	return p.distance_to(a + ab * t)


static func _v(st: SurfaceTool, p: Vector3, c: Color) -> void:
	st.set_color(c)
	st.add_vertex(p)


## Roads for a list of segments [Vector3 a, Vector3 b] (world space, relative to `origin`).
static func road_mesh(segments: Array, origin: Vector3, width: float, asphalt: Color,
		edge: Color, marking: Color, nodes: Array, detail := false) -> ArrayMesh:
	if detail:   # W67: shoulder gravel, asphalt grain, worn paint (terrain.gdshader)
		edge = Color(edge, CODE_SHOULDER)
		asphalt = Color(asphalt, CODE_ASPHALT)
		marking = Color(marking, CODE_MARKING)
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	st.set_normal(Vector3.UP)
	# curb / shoulder outline
	for s in segments:
		_strip(st, s[0] - origin, s[1] - origin, width + 0.5, 0.02, edge, 0.0)
	for p in nodes:
		_disc(st, p - origin, (width + 0.5) * 0.5, 0.021, edge)
	# asphalt
	for s in segments:
		_strip(st, s[0] - origin, s[1] - origin, width, 0.04, asphalt, 0.0)
	for p in nodes:
		_disc(st, p - origin, width * 0.5, 0.041, asphalt)
	# dashed centre line
	for s in segments:
		var a: Vector3 = s[0] - origin
		var b: Vector3 = s[1] - origin
		var l := a.distance_to(b)
		var d := (b - a).normalized()
		var t := 1.6
		while t < l - 1.6:
			_strip(st, a + d * t, a + d * minf(t + 0.7, l - 1.6), 0.14, 0.05, marking, 0.0)
			t += 1.5
	return st.commit()


static func _strip(st: SurfaceTool, a: Vector3, b: Vector3, w: float, y: float, c: Color, extend: float) -> void:
	var d := b - a
	d.y = 0
	if d.length() < 0.001:
		return
	d = d.normalized()
	a -= d * extend
	b += d * extend
	var r := Vector3(-d.z, 0, d.x) * (w * 0.5)
	var p0 := Vector3(a.x, y, a.z) - r
	var p1 := Vector3(a.x, y, a.z) + r
	var p2 := Vector3(b.x, y, b.z) + r
	var p3 := Vector3(b.x, y, b.z) - r
	_v(st, p0, c)
	_v(st, p2, c)
	_v(st, p1, c)
	_v(st, p0, c)
	_v(st, p3, c)
	_v(st, p2, c)


static func _disc(st: SurfaceTool, p: Vector3, r: float, y: float, c: Color) -> void:
	var seg := 12
	var center := Vector3(p.x, y, p.z)
	for i in seg:
		var a0 := TAU * i / seg
		var a1 := TAU * (i + 1) / seg
		_v(st, center, c)
		_v(st, center + Vector3(cos(a0), 0, sin(a0)) * r, c)
		_v(st, center + Vector3(cos(a1), 0, sin(a1)) * r, c)


## Flat rectangle (parking lots, pads). `basis` orients it.
static func pad_mesh(size: Vector2, color: Color, lines: int, line_color: Color) -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	st.set_normal(Vector3.UP)
	var hx := size.x * 0.5
	var hz := size.y * 0.5
	_strip(st, Vector3(-hx, 0, 0), Vector3(hx, 0, 0), size.y, 0.035, color, 0.0)
	if lines > 0:
		var stepx := size.x / lines
		for i in lines + 1:
			var x := -hx + i * stepx
			_strip(st, Vector3(x, 0, -hz + 0.2), Vector3(x, 0, hz * 0.2), 0.08, 0.045, line_color, 0.0)
	return st.commit()


static func water_mesh(size: float) -> PlaneMesh:
	var p := PlaneMesh.new()
	p.size = Vector2(size, size)
	p.subdivide_depth = 0
	p.subdivide_width = 0
	return p
