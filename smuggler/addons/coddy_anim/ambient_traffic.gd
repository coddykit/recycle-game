extends RefCounted
## coddy_anim · AmbientTraffic — city cars that keep roaming a RoadGraph (W68 rollout, first user Money District):
## each car drives a long random tour (no U-turns), and when it ends the next tour starts from the road it is on, so
## speed, heading and position carry over (no stop, no jump at the seam). Lanes, corner arcs, following distance and
## junction order come from VehicleMover. Cars spawn spaced out along their first tour, never on top of another car.
##   var at := AmbientTraffic.new(); at.setup(graph, 42, {"lane_offset": 1.45})
##   var car = at.add(node, {"max_speed": 7.0})   # node: position + yaw (+Z forward) are written each step
##   at.step(delta) · at.remove(car) (hidden / pooled)
## Cost: VehicleMover's O(n²) leader search — fine up to ~80 cars.

const VehicleMover = preload("vehicle_mover.gd")
const CoddyRoadGraph = preload("road_graph.gd")   # not "RoadGraph": games may own a global class of that name (smuggler)

var vm := VehicleMover.new()
var graph: CoddyRoadGraph
var rng := RandomNumberGenerator.new()
var tour_steps := 24             ## nodes per tour
var spawn_clear := 9.0           ## m: a car only appears where no other car is this close
var chain_reach := 6.0           ## m before the end of a tour where the next one is chained (inside the last straight:
                                 ## keep it under the shortest road minus 2 × corner_r)
var junction_guard := 8.0        ## m before a junction circle where no car is born
var seams := 0                   ## tours chained so far (tests)
var _cars: Array = []            ## [{ve, node, opts, last: Array (the tour's nodes)}]


func setup(g: CoddyRoadGraph, seed: int, opts: Dictionary = {}) -> void:
	graph = g
	vm.graph = g
	rng.seed = seed
	for k in ["lane_offset", "corner_r", "junction_r", "lookahead"]:
		if opts.has(k):
			vm.set(k, float(opts[k]))
	if opts.has("tour_steps"):
		tour_steps = int(opts.tour_steps)
	for k in ["spawn_clear", "chain_reach", "junction_guard"]:   # small worlds (smuggler: 1.5 m cars, 4 m roads)
		if opts.has(k):
			set(k, float(opts[k]))
	vm.measure = bool(opts.get("measure", false))   # games: no second O(n²) pass per step


## A roaming car (null when no spawn spot was clear this time: try again on a later frame).
func add(node: Node3D, opts: Dictionary = {}) -> Variant:
	var ids: Array = graph.pos.keys()
	if ids.size() < 2:
		return null   # no road to drive (e.g. a network still being unlocked)
	for attempt in 12:
		var start: String = ids[rng.randi() % ids.size()]
		var tour := _walk([start])
		if tour.size() < 2:
			continue
		var o := opts.duplicate()
		o["nodes"] = tour
		var ve = vm.add(node, start, "", o)
		# somewhere along the first stretch (not all on the nodes), clear of every other car
		var s0 := rng.randf_range(0.0, minf(ve.length() * 0.5, 40.0))
		var p0: Vector3 = ve.at(s0)
		if _clear(ve, p0) and not _near_junction(ve, s0):
			ve.s = s0
			ve.p = p0
			ve.heading = ve.dir_at(s0)
			ve.yaw = atan2(ve.heading.x, ve.heading.z)
			vm._apply_node(ve, 0.0)
			var car := {"ve": ve, "node": node, "opts": opts, "last": tour}
			_cars.append(car)
			return car
		vm.vehicles.erase(ve)
	return null


func remove(car: Dictionary) -> void:
	_release(car["ve"])
	vm.vehicles.erase(car["ve"])
	_cars.erase(car)


func count() -> int:
	return _cars.size()


func step(dt: float) -> void:
	vm.step(dt)
	for car in _cars:
		var ve = car["ve"]
		# chain on the last straight, before the new tour's corner arc starts (both lanes coincide there); DONE = a
		# frame long enough to skip that window
		var left: float = ve.length() - ve.s
		if ve.state == VehicleMover.DONE or (left <= vm.corner_r + chain_reach and left > vm.corner_r + minf(0.5, chain_reach * 0.25)):
			_next_tour(car)


## The next tour starts one node BACK (the road the car is on), so the lane, the corner into the next road and the
## car's spot on it all line up; the car is put at the point of the new path nearest to where it stands (on the shared
## straight: the same point).
func _next_tour(car: Dictionary) -> void:
	var old = car["ve"]
	var last: Array = car["last"]
	var prev: String = last[last.size() - 2]
	var end: String = last[last.size() - 1]
	var tour := _walk([prev, end])
	var o: Dictionary = (car["opts"] as Dictionary).duplicate()
	o["nodes"] = tour
	o["v"] = old.v
	o["yaw"] = old.yaw
	_release(old)
	vm.vehicles.erase(old)
	var ve = vm.add(car["node"], prev, "", o)
	ve.wheel_angle = old.wheel_angle
	# nearest point on the first road (prev → end) to where the car is
	var first_len: float = (graph.pos[prev] as Vector3).distance_to(graph.pos[end]) + vm.corner_r
	var best_s := 0.0
	var best_d := INF
	var sv := 0.0
	while sv <= minf(first_len, ve.length()):
		var d: float = ve.at(sv).distance_squared_to(old.p)
		if d < best_d:
			best_d = d
			best_s = sv
		sv += 0.1
	ve.s = best_s
	ve.p = ve.at(best_s)
	ve.heading = old.heading
	vm._apply_node(ve, 0.0)
	car["ve"] = ve
	car["last"] = tour
	seams += 1


## A random walk of `tour_steps` nodes from `head` (its last node), never straight back the way it came.
func _walk(head: Array) -> Array:
	var tour := head.duplicate()
	while tour.size() < tour_steps:
		var at: String = tour[tour.size() - 1]
		var back: String = tour[tour.size() - 2] if tour.size() >= 2 else ""
		var opts: Array = []
		for nb in graph._adj.get(at, []):
			if nb[0] != back:
				opts.append(nb[0])
		if opts.is_empty():
			if back == "":
				break
			opts = [back]   # a dead end: the only way out
		tour.append(opts[rng.randi() % opts.size()])
	return tour


## Inside (or just before) a junction circle: a car born there would sit in it without its turn in the queue.
func _near_junction(ve, sv: float) -> bool:
	for sp in ve.spans:
		if sv >= float(sp[0]) - junction_guard and sv <= float(sp[1]) + 1.0:
			return true
	return false


func _clear(ve, p: Vector3) -> bool:
	for o in vm.vehicles:
		if o != ve and o.state != VehicleMover.DONE and (o.p as Vector3).distance_to(p) < spawn_clear:
			return false
	return true


## A vehicle leaving the simulation drops its place in every junction queue (else the junction stays blocked).
func _release(ve) -> void:
	for jid in vm._junctions.keys():
		(vm._junctions[jid] as Array).erase(ve.id)
	ve.state = VehicleMover.DONE
