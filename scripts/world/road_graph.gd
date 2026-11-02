class_name RoadGraph
extends RefCounted
## Abstract road network connecting towns, the HQ and border crossings.
## Pure data: used both by gameplay (travel estimates) and by the world view.

var nodes := {}        # id -> {id, pos: Vector3, region: int, kind: String, name: String}
var edges: Array = []  # {a, b, type, len}
var adj := {}          # id -> Array[int] edge indices
var crossings: Array = [] # {index, type, a, b, axis}
var _cache := {}


func build(regions: Array, spacing: float, tile: float) -> void:
	nodes.clear()
	edges.clear()
	adj.clear()
	crossings.clear()
	_cache.clear()
	var half := tile * 0.5
	for i in regions.size():
		var r: Dictionary = regions[i]
		var c := region_center(r, spacing)
		var towns: Array = r.get("towns", [])
		var ids := ["r%d_hub" % i, "r%d_a" % i, "r%d_b" % i]
		for t in mini(3, towns.size()):
			var lp: Array = towns[t][1]
			_add_node(ids[t], c + Vector3(lp[0], 0, lp[1]), i, "hub" if t == 0 else "town", towns[t][0])
		_add_edge(ids[0], ids[1], "road")
		_add_edge(ids[0], ids[2], "road")
		if i == 0:
			_add_node("hq", c + Vector3(6, 0, -5), 0, "hq", "Headquarters")
			_add_edge("hq", ids[0], "road")
	for i in regions.size() - 1:
		var ra: Dictionary = regions[i]
		var rb: Dictionary = regions[i + 1]
		var ca := region_center(ra, spacing)
		var cb := region_center(rb, spacing)
		var dx: int = int(rb.grid[0]) - int(ra.grid[0])
		var types := ["highway", "backroad"]
		for k in 2:
			var off := -9.0 if k == 0 else 9.0
			var pa: Vector3
			var pb: Vector3
			var axis := "x"
			if dx != 0:
				pa = ca + Vector3(half * dx, 0, off)
				pb = cb + Vector3(-half * dx, 0, off)
			else:
				var dz: int = int(rb.grid[1]) - int(ra.grid[1])
				pa = ca + Vector3(off, 0, half * dz)
				pb = cb + Vector3(off, 0, -half * dz)
				axis = "z"
			var ida := "x%d_%s_a" % [i, types[k]]
			var idb := "x%d_%s_b" % [i, types[k]]
			_add_node(ida, pa, i, "cross", "")
			_add_node(idb, pb, i + 1, "cross", "")
			_add_edge(ida, idb, types[k])
			_add_edge(ida, _nearest_town(i, pa), types[k])
			_add_edge(idb, _nearest_town(i + 1, pb), types[k])
			crossings.append({"index": i, "type": types[k], "a": ida, "b": idb, "axis": axis})


static func region_center(r: Dictionary, spacing: float) -> Vector3:
	var g: Array = r.get("grid", [0, 0])
	return Vector3(float(g[0]) * spacing, 0, float(g[1]) * spacing)


func _nearest_town(region: int, p: Vector3) -> String:
	var best := ""
	var best_d := INF
	for suffix in ["hub", "a", "b"]:
		var id := "r%d_%s" % [region, suffix]
		if nodes.has(id):
			var d: float = nodes[id].pos.distance_to(p)
			if d < best_d:
				best_d = d
				best = id
	return best


func _add_node(id: String, pos: Vector3, region: int, kind: String, name: String) -> void:
	nodes[id] = {"id": id, "pos": pos, "region": region, "kind": kind, "name": name}
	adj[id] = []


func _add_edge(a: String, b: String, type: String) -> void:
	if a == "" or b == "" or not nodes.has(a) or not nodes.has(b):
		return
	var e := {"a": a, "b": b, "type": type, "len": nodes[a].pos.distance_to(nodes[b].pos)}
	edges.append(e)
	adj[a].append(edges.size() - 1)
	adj[b].append(edges.size() - 1)


func pos(id: String) -> Vector3:
	return nodes[id].pos if nodes.has(id) else Vector3.ZERO


func _weight(e: Dictionary, pref: String) -> float:
	var m := 1.0
	match pref:
		"fast":
			m = 0.6 if e.type == "highway" else (1.8 if e.type == "backroad" else 1.0)
		"long":
			m = 0.6 if e.type == "backroad" else (1.8 if e.type == "highway" else 1.0)
	return float(e.len) * m


## Returns the node id list from a to b using only regions <= max_region.
func find_path(a: String, b: String, pref := "normal", max_region := 99) -> Array:
	if a == b:
		return [a]
	var key := "%s|%s|%s|%d" % [a, b, pref, max_region]
	if _cache.has(key):
		return _cache[key]
	var dist := {a: 0.0}
	var prev := {}
	var open := [a]
	var done := {}
	while open.size() > 0:
		var best_i := 0
		for i in open.size():
			if dist[open[i]] < dist[open[best_i]]:
				best_i = i
		var cur: String = open[best_i]
		open.remove_at(best_i)
		if cur == b:
			break
		done[cur] = true
		for ei in adj.get(cur, []):
			var e: Dictionary = edges[ei]
			var nxt: String = e.b if e.a == cur else e.a
			if done.has(nxt) or int(nodes[nxt].region) > max_region:
				continue
			var nd: float = dist[cur] + _weight(e, pref)
			if not dist.has(nxt) or nd < dist[nxt]:
				dist[nxt] = nd
				prev[nxt] = cur
				if not open.has(nxt):
					open.append(nxt)
	if not prev.has(b):
		return []
	var path := [b]
	var c: String = b
	while prev.has(c):
		c = prev[c]
		path.push_front(c)
	_cache[key] = path
	return path


func path_points(path: Array) -> PackedVector3Array:
	var pts := PackedVector3Array()
	for id in path:
		pts.append(pos(id))
	return pts


func path_length(path: Array) -> float:
	var total := 0.0
	for i in range(1, path.size()):
		total += pos(path[i - 1]).distance_to(pos(path[i]))
	return total


## Concatenates several legs (e.g. home -> origin -> destination).
func multi_path(stops: Array, pref: String, max_region: int) -> Array:
	var out := []
	for i in range(1, stops.size()):
		var leg := find_path(stops[i - 1], stops[i], pref, max_region)
		if leg.is_empty():
			return []
		if out.size() > 0:
			leg = leg.slice(1)
		out.append_array(leg)
	return out
