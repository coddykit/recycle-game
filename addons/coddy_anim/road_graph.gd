extends RefCounted
## coddy_anim · RoadGraph — road centre lines as a node graph (x, z in metres, y = road height) and the drivable lane
## polylines on it: right-hand lane (offset from the centre line), quadratic corner arcs at every bend (from
## disaster-command traffic.gd W67). Nodes with ≥ 3 roads are junctions (or mark_junction()).

var pos: Dictionary = {}        ## id -> Vector3
var _adj: Dictionary = {}       ## id -> Array of [other id, length]
var _junction: Dictionary = {}
var _routes: Dictionary = {}


func add_node(id: String, p: Vector3) -> void:
	pos[id] = p
	if not _adj.has(id):
		_adj[id] = []
	_routes.clear()


func add_road(a: String, b: String) -> void:
	var l: float = (pos[a] as Vector3).distance_to(pos[b])
	_adj[a].append([b, l])
	_adj[b].append([a, l])
	_routes.clear()


func mark_junction(id: String) -> void:
	_junction[id] = true


func is_junction(id: String) -> bool:
	return _junction.has(id) or (_adj.get(id, []) as Array).size() >= 3


## Node ids from a to b along the shortest roads; [a] when unreachable.
func route(a: String, b: String) -> Array:
	var key := a + ">" + b
	if _routes.has(key):
		return _routes[key]
	var dist := {a: 0.0}
	var prev := {}
	var open: Array = [a]
	var done := {}
	while not open.is_empty():
		var best := 0
		for i in open.size():
			if float(dist[open[i]]) < float(dist[open[best]]):
				best = i
		var cur: String = open[best]
		open.remove_at(best)
		if done.has(cur):
			continue
		done[cur] = true
		if cur == b:
			break
		for nb in _adj.get(cur, []):
			var nd: float = float(dist[cur]) + float(nb[1])
			if not dist.has(nb[0]) or nd < float(dist[nb[0]]):
				dist[nb[0]] = nd
				prev[nb[0]] = cur
				open.append(nb[0])
	var out: Array = []
	if dist.has(b) and a != b:
		var n := b
		while n != a:
			out.push_front(n)
			n = prev[n]
		out.push_front(a)
	else:
		out = [a]
	_routes[key] = out
	return out


static func right_of(d: Vector3) -> Vector3:
	return Vector3(-d.z, 0.0, d.x)


## The lane polyline along `nodes`: {pts: PackedVector3Array, curve: PackedByteArray (1 = on a corner arc)}.
func lane_path(nodes: Array, lane_offset: float, corner_r: float) -> Dictionary:
	var pts := PackedVector3Array()
	var curve := PackedByteArray()
	var c: Array = []
	for n in nodes:
		c.append(pos[n])
	if c.size() == 1:
		pts.append(c[0])
		curve.append(0)
		return {"pts": pts, "curve": curve}
	var dirs: Array = []
	for i in c.size() - 1:
		var d: Vector3 = (c[i + 1] as Vector3) - (c[i] as Vector3)
		d.y = 0.0
		dirs.append(d.normalized())
	for i in c.size() - 1:
		var d: Vector3 = dirs[i]
		var o := right_of(d) * lane_offset
		var a: Vector3 = (c[i] as Vector3) + o
		var b: Vector3 = (c[i + 1] as Vector3) + o
		var r := minf(corner_r, a.distance_to(b) * 0.3)
		pts.append(a + d * (r if i > 0 else 0.0))
		curve.append(0)
		var end := b - d * (r if i < c.size() - 2 else 0.0)
		pts.append(end)
		curve.append(0)
		if i < c.size() - 2:
			var d2: Vector3 = dirs[i + 1]
			if d2.dot(d) < -0.99:   # U-turn: no arc, the lane switch is the next segment start
				continue
			var o2 := right_of(d2) * lane_offset
			var r2 := minf(corner_r, ((c[i + 2] as Vector3) - (c[i + 1] as Vector3)).length() * 0.3)
			var p_out := (c[i + 1] as Vector3) + o2 + d2 * r2
			var ctrl := (c[i + 1] as Vector3) + (o + o2) * 0.9
			# control point on the intersection of the two lane lines keeps the arc tangent to both
			var den := d.x * d2.z - d.z * d2.x
			if absf(den) > 0.001:
				var w := p_out - end
				var t := (w.x * d2.z - w.z * d2.x) / den
				ctrl = end + d * t
			for k in range(1, 6):
				var tt := k / 6.0
				pts.append(end.lerp(ctrl, tt).lerp(ctrl.lerp(p_out, tt), tt))
				curve.append(1)
	return {"pts": pts, "curve": curve}
