class_name Sites
## Layout math for the HQ garage, parking spots and regional depots.
## Shared by the world view and the region builder (which keeps the space clear).

const SPOT_SPACING := 1.7
const HQ_SPOTS := 14

static var _cache := {}


static func hq_frame() -> Dictionary:
	var g := RegionManager.graph
	var p := g.pos("hq")
	var away := (p - g.pos("r0_hub"))
	away.y = 0
	away = away.normalized()
	return {"pos": p, "away": away, "side": Vector3(-away.z, 0, away.x)}


static func garage_transform(level: int) -> Transform3D:
	var f := hq_frame()
	var m := Data.meta("garage_l%d" % clampi(level, 1, 5))
	var depth := float(m.size[2])
	var c: Vector3 = f.pos + f.away * (3.4 + depth * 0.5)
	return Transform3D(Basis.looking_at(-f.away, Vector3.UP), c)


static func hq_spots() -> Array:
	if _cache.has("hq"):
		return _cache["hq"]
	var f := hq_frame()
	var out := []
	for i in HQ_SPOTS:
		var k := i / 2 + 1
		var sgn := 1.0 if i % 2 == 0 else -1.0
		var p: Vector3 = f.pos + f.away * 1.3 + f.side * sgn * (0.6 + k * SPOT_SPACING)
		out.append(Transform3D(Basis.looking_at(-f.away, Vector3.UP), p))
	_cache["hq"] = out
	return out


static func depot_frame(index: int) -> Dictionary:
	var g := RegionManager.graph
	var hub_id := "r%d_hub" % index
	var hub := g.pos(hub_id)
	var road_dirs := []
	for ei in g.adj.get(hub_id, []):
		var e: Dictionary = g.edges[ei]
		var other: String = e.b if e.a == hub_id else e.a
		var d := g.pos(other) - hub
		d.y = 0
		road_dirs.append(d.normalized())
	var best := Vector3.RIGHT
	var best_score := -INF
	for i in 16:
		var a := TAU * i / 16.0
		var d := Vector3(cos(a), 0, sin(a))
		var score := INF
		for rd in road_dirs:
			score = minf(score, d.angle_to(rd))
		if score > best_score:
			best_score = score
			best = d
	return {"pos": hub, "away": best, "side": Vector3(-best.z, 0, best.x)}


static func depot_transform(index: int) -> Transform3D:
	var f := depot_frame(index)
	return Transform3D(Basis.looking_at(-f.away, Vector3.UP), f.pos + f.away * 5.6)


static func depot_spots(index: int) -> Array:
	if _cache.has(index):
		return _cache[index]
	var f := depot_frame(index)
	var out := []
	for i in 6:
		var k := i / 2
		var sgn := 1.0 if i % 2 == 0 else -1.0
		var p: Vector3 = f.pos + f.away * 2.4 + f.side * sgn * (1.1 + k * SPOT_SPACING)
		out.append(Transform3D(Basis.looking_at(-f.away, Vector3.UP), p))
	_cache[index] = out
	return out
