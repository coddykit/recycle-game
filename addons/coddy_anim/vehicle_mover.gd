extends RefCounted
## coddy_anim · VehicleMover — cars / buses / trucks on a RoadGraph. Right-hand lane with rounded corners, following
## distance (hard floor `min_gap`, centre to centre, never undercut), smooth acceleration / braking (stopping distance
## v²/2a), slower through corners, heading eased towards the direction of motion, wheels spun by distance / radius,
## first-come junction order (one vehicle inside a junction at a time) and ParkingLot slot reservation (one vehicle per
## slot, FIFO queue on the road before the gate).
## Pure maths on plain data: step(dt) moves every vehicle; a vehicle's node (optional) gets position / yaw / wheels.
##   var vm := VehicleMover.new(); vm.graph = g
##   var v = vm.add(car_node, "depot", "hospital")            # drive to a node (state DONE on arrival)
##   var v2 = vm.add(bus_node, "depot", "", {"lot": lot})      # drive to a lot, park, later vm.leave(v2)
## Cost: O(n²) per step for the leader search — fine up to ~80 vehicles; above that bucket by grid (TODO when needed).
## W68 (Money District proof 10-30, Patron #72 upstream): opts `body` (m, drawn length) — the vehicle waits with its
## front, not its centre, at the junction circle (0 = the centre, as before); `bend_leader` (mover) — on a corner arc the
## leader is also searched along the vehicle's own path (off by default: other games keep their tested behaviour).

const CoddyRoadGraph = preload("road_graph.gd")   # not "RoadGraph": games may own a global class of that name (smuggler)

const DRIVING := 0
const WAIT_SLOT := 1
const PARKING := 2
const PARKED := 3
const DONE := 4

var graph: CoddyRoadGraph
var lane_offset := 1.6          ## m from the centre line (right-hand traffic)
var corner_r := 4.0             ## corner arc reach along each road
var junction_r := 0.0           ## 0 = corner_r + 2 × lane_offset
var lookahead := 25.0           ## m: leader search range
var bend_leader := false       ## W68: also search the leader along the own path on a corner arc (Money District)
var vehicles: Array = []        ## Array[Vehicle]
var min_gap_seen := INF         ## smallest follower gap / min_gap measured after any step (≥ 1 = floor held)
var measure := true             ## keep min_gap_seen up to date (a second O(n²) pass: tests / tools; games turn it off)
var _next_id := 0
var _junctions: Dictionary = {} ## node id -> Array[int] FIFO, head = the vehicle allowed in


class Vehicle:
	var id := 0
	var node: Node3D
	var wheels: Array = []
	var wheel_radius := 0.35
	var max_speed := 12.0
	var accel := 2.5
	var decel := 5.0
	var min_gap := 6.0          ## centre to centre, m (a car ≈ 4.5 m long + 1.5 m)
	var body := 0.0             ## m, drawn length: its stop line sits body / 2 before the junction circle
	var headway := 1.0          ## s: the comfortable gap grows with speed
	var corner_speed := 0.45    ## × max_speed on a corner arc
	var turn_rate := 6.0
	var pts := PackedVector3Array()
	var cum := PackedFloat32Array()
	var curve := PackedByteArray()
	var spans: Array = []       ## [s_enter, s_exit, junction id]
	var s := 0.0
	var v := 0.0
	var p := Vector3.ZERO
	var heading := Vector3(0, 0, 1)
	var yaw := 0.0
	var state := DRIVING
	var lot = null
	var slot := -1
	var wheel_angle := 0.0
	var holding := ""          ## junction this vehicle is allowed into (queue head)
	var waiting_for := ""      ## junction whose stop line it is held at

	func length() -> float:
		return cum[cum.size() - 1] if cum.size() > 0 else 0.0

	func at(sv: float) -> Vector3:
		if pts.size() == 1:
			return pts[0]
		var i := cum.bsearch(sv)
		i = clampi(i, 1, pts.size() - 1)
		var seg := maxf(0.0001, cum[i] - cum[i - 1])
		return pts[i - 1].lerp(pts[i], clampf((sv - cum[i - 1]) / seg, 0.0, 1.0))

	func dir_at(sv: float) -> Vector3:
		var a := at(maxf(0.0, sv - 0.3))
		var b := at(minf(length(), sv + 0.3))
		var d := b - a
		d.y = 0.0
		return d.normalized() if d.length() > 0.0001 else heading

	func on_curve(s0: float, s1: float) -> bool:
		for i in pts.size():
			if cum[i] >= s0 and cum[i] <= s1 and curve[i] == 1:
				return true
		return false


## opts: max_speed, accel, decel, min_gap, headway, wheel_radius, lot (ParkingLot), path (PackedVector3Array: drive
## this polyline instead of a route), nodes (Array of graph node ids: drive this tour — lanes, corners and junction order
## as for a route; ambient traffic that keeps driving: chain tours with `v` / `yaw` carried over), v / yaw (start speed
## and heading, a tour continuing the previous one).
func add(node: Node3D, from: String, to: String, opts: Dictionary = {}) -> Vehicle:
	var ve := Vehicle.new()
	ve.id = _next_id
	_next_id += 1
	ve.node = node
	for k in ["max_speed", "accel", "decel", "min_gap", "headway", "wheel_radius", "corner_speed", "turn_rate", "body"]:
		if opts.has(k):
			ve.set(k, float(opts[k]))
	ve.lot = opts.get("lot", null)
	if opts.has("path"):
		var pp: PackedVector3Array = opts.path
		var cv := PackedByteArray()
		cv.resize(pp.size())
		cv.fill(0)
		_set_path(ve, pp, cv, [])
	else:
		var dest := to if ve.lot == null else String(ve.lot.road_node)
		var nodes: Array = opts.nodes if opts.has("nodes") else graph.route(from, dest)
		var lp := graph.lane_path(nodes, lane_offset, corner_r)
		var pp: PackedVector3Array = lp.pts
		var cv: PackedByteArray = lp.curve
		if ve.lot != null:
			pp.append(ve.lot.gate)
			cv.append(0)
		_set_path(ve, pp, cv, nodes)
	ve.p = ve.at(0.0)
	ve.heading = ve.dir_at(0.0)
	ve.yaw = float(opts.yaw) if opts.has("yaw") else atan2(ve.heading.x, ve.heading.z)
	ve.v = clampf(float(opts.get("v", 0.0)), 0.0, ve.max_speed)
	if node != null:
		for c in node.find_children("*", "Node3D", true, false):
			if String(c.name).to_lower().contains("wheel"):
				ve.wheels.append(c)
	vehicles.append(ve)
	_apply_node(ve, 0.0)
	return ve


func _set_path(ve: Vehicle, pp: PackedVector3Array, cv: PackedByteArray, nodes: Array) -> void:
	ve.pts = pp
	ve.curve = cv
	ve.cum = PackedFloat32Array()
	ve.cum.resize(pp.size())
	var acc := 0.0
	for i in pp.size():
		if i > 0:
			acc += pp[i - 1].distance_to(pp[i])
		ve.cum[i] = acc
	ve.spans.clear()
	var jr := junction_r if junction_r > 0.0 else corner_r + 2.0 * lane_offset
	var seen := {}
	for n in nodes:
		if not graph.is_junction(n) or seen.has(n):
			continue
		seen[n] = true
		var c: Vector3 = graph.pos[n]
		var c2 := Vector2(c.x, c.z)
		var s0 := -1.0
		var s1 := -1.0
		# only the segments that come near the circle are sampled (0.25 m: the corner points alone are too sparse to find
		# its edge); one span per pass — a tour may cross the same junction twice, and one span from the first entry to
		# the last exit would hold it the whole way. Cost per path: segments + a few metres per pass (was the whole path).
		for i in range(1, ve.pts.size()):
			var a: Vector3 = ve.pts[i - 1]
			var b: Vector3 = ve.pts[i]
			var near := Geometry2D.get_closest_point_to_segment(c2, Vector2(a.x, a.z), Vector2(b.x, b.z)).distance_to(c2) < jr
			if not near:
				if s0 >= 0.0:
					ve.spans.append([maxf(0.0, s0 - 0.5), s1 + 0.5, n])
					s0 = -1.0
				continue
			var sv: float = ve.cum[i - 1]
			while sv <= ve.cum[i]:
				var q := ve.at(sv)
				if Vector2(q.x - c.x, q.z - c.z).length() < jr:
					if s0 < 0.0:
						s0 = sv
					s1 = sv
				elif s0 >= 0.0 and sv > s1 + 0.6:
					ve.spans.append([maxf(0.0, s0 - 0.5), s1 + 0.5, n])
					s0 = -1.0
				sv += 0.5
		if s0 >= 0.0:
			ve.spans.append([maxf(0.0, s0 - 0.5), s1 + 0.5, n])
	if ve.body > 0.0:
		# the front, not the centre, stops at the circle
		for sp in ve.spans:
			sp[0] = maxf(0.0, float(sp[0]) - ve.body * 0.5)
	ve.spans.sort_custom(func(x, y): return float(x[0]) < float(y[0]))


## Extend the drive with more points (e.g. gate -> aisle -> slot).
func _extend(ve: Vehicle, more: Array) -> void:
	for q in more:
		ve.cum.append(ve.length() + ve.pts[ve.pts.size() - 1].distance_to(q))
		ve.pts.append(q)
		ve.curve.append(0)


## Gap (centre to centre, m) to the nearest vehicle ahead of position `at` facing `dir`, within the lane width.
## same_dir: only vehicles heading the same way (a follower's gap; cross traffic passing a stop line is not a leader).
## held_at: a junction this vehicle waits at — the one turning through it in front is cross traffic, not a leader (the
## measure; driving still brakes for it).
func gap_ahead(ve: Vehicle, at: Vector3, dir: Vector3, same_dir: bool = false, held_at: String = "") -> float:
	var best := INF
	var right := CoddyRoadGraph.right_of(dir)
	for o in vehicles:
		if o == ve or o.state == PARKED or o.state == DONE:
			continue
		if ve.holding != "" and o.waiting_for == ve.holding and (o.heading as Vector3).dot(dir) < 0.7:
			continue   # cross traffic held at the stop line of the junction we are crossing (never one ahead in our lane)
		if same_dir and (o.heading as Vector3).dot(dir) < 0.7:
			continue
		if held_at != "" and o.holding == held_at:
			continue
		var rel: Vector3 = o.p - at
		rel.y = 0.0
		var lon := rel.dot(dir)
		if lon <= 0.0 or lon > lookahead:
			continue
		if absf(rel.dot(right)) >= lane_offset:
			continue
		best = minf(best, lon)
	return best


## Distance along ve's own path from s_from to the first vehicle standing on it (INF = none within lookahead / 2): round a
## bend the straight-ahead check of gap_ahead misses the car just round the corner.
func _path_gap(ve: Vehicle, s_from: float) -> float:
	var best := INF
	var reach := minf(lookahead * 0.5, ve.length() - s_from)
	var here := ve.at(s_from)
	for o in vehicles:
		if o == ve or o.state == PARKED or o.state == DONE:
			continue
		if ve.holding != "" and o.waiting_for == ve.holding:
			continue   # held at the stop line of the junction we are crossing
		var rel: Vector3 = o.p - here
		if Vector2(rel.x, rel.z).length() > reach + lane_offset:
			continue
		var d := 0.5
		while d <= reach and d < best:
			var q := ve.at(s_from + d)
			if Vector2(q.x - o.p.x, q.z - o.p.z).length() < lane_offset:
				best = d
				break
			d += 0.75
	return best


func _junction_free(ve: Vehicle, leader_gap: float) -> float:
	## distance to the stop line of the first junction this vehicle is not allowed into (INF = none)
	ve.waiting_for = ""
	ve.holding = ""
	for sp in ve.spans:
		var s0: float = sp[0]
		var s1: float = sp[1]
		var jid: String = sp[2]
		var q: Array = _junctions.get(jid, [])
		var braking := ve.v * ve.v / (2.0 * ve.decel) + ve.min_gap
		if ve.s > s1:
			# out of it — unless the tour comes straight back into the same junction (a later pass already queued)
			if q.has(ve.id) and not _passes_again(ve, jid, braking):
				q.erase(ve.id)
			continue
		if s0 - ve.s > braking + 5.0:
			return INF   # not near yet; later junctions are farther still
		if ve.s > s0 + 0.75:
			# already inside the circle (past the stop line, where a held vehicle waits at s0 exactly) (it entered on its turn, a tour began in it): it drives out first — waiting in
			# there would put it on the crossing lanes, where the vehicle holding the junction does not look
			if q.is_empty() or q[0] != ve.id:
				q.erase(ve.id)
				q.push_front(ve.id)
				_junctions[jid] = q
			ve.holding = jid
			return INF
		# don't block the box: a vehicle not yet inside only goes in when its exit has room for it (else a full road
		# beyond holds it inside the junction and the cross traffic of every junction around locks up — gridlock)
		if ve.s <= s0 + 0.75 and not _exit_clear(ve, s1):
			# keep its place in the queue (a follower cannot pass it anyway) but wait at the line
			ve.waiting_for = jid
			return maxf(0.0, s0 - ve.s)
		if not q.has(ve.id):
			if leader_gap < s0 - ve.s + 1.0:   # (+1 m: a leader standing on the stop line is still ahead of us)
				ve.waiting_for = jid
				return maxf(0.0, s0 - ve.s)   # someone ahead of us reaches it first: join the queue after them
			q.append(ve.id)
			_junctions[jid] = q
		if q[0] == ve.id and leader_gap < s0 - ve.s + 1.0:
			# we head the queue but a car physically ahead of us in our lane is queued for it too (it joined later: a
			# tour that begins at this junction queues on its first step) — it goes first, else neither ever moves
			var ahead := _queued_ahead(ve, q, s0 - ve.s + 1.0)
			if ahead >= 0:
				q.erase(ahead)
				q.push_front(ahead)
		if q[0] != ve.id:
			ve.waiting_for = jid
			return maxf(0.0, s0 - ve.s)
		ve.holding = jid
		return INF   # we hold this one; the next one is checked when we get near it
	return INF


## The id of a vehicle in queue q standing in our lane between us and `reach` metres ahead (-1 = none).
func _queued_ahead(ve: Vehicle, q: Array, reach: float) -> int:
	var right := CoddyRoadGraph.right_of(ve.heading)
	for o in vehicles:
		if o == ve or not q.has(o.id) or (o.heading as Vector3).dot(ve.heading) < 0.7:
			continue
		var rel: Vector3 = o.p - ve.p
		rel.y = 0.0
		var lon := rel.dot(ve.heading)
		if lon > 0.0 and lon <= reach and absf(rel.dot(right)) < lane_offset:
			return o.id
	return -1


## Room for this vehicle on its own path just past the junction (s1 … s1 + min_gap), whichever way it turns there.
func _exit_clear(ve: Vehicle, s1: float) -> bool:
	var sv := s1
	var end := minf(ve.length(), s1 + ve.min_gap)
	while sv <= end:
		var q := ve.at(sv)
		var d_here := ve.dir_at(sv)
		for o in vehicles:
			if o == ve or o.state == PARKED or o.state == DONE:
				continue
			if (o.heading as Vector3).dot(d_here) < 0.7:
				continue   # cross traffic waiting at its own stop line next to our exit is not in our way
			var d: Vector3 = o.p - q
			d.y = 0.0
			if d.length() < lane_offset:
				return false
		sv += 1.0
	return true


func _passes_again(ve: Vehicle, jid: String, braking: float) -> bool:
	for sp in ve.spans:
		if sp[2] == jid and float(sp[1]) >= ve.s and float(sp[0]) - ve.s <= braking + 5.0:   # coming up or under way
			return true
	return false


func step(dt: float) -> void:
	if dt <= 0.0:
		return
	for ve in vehicles:
		if ve.state == PARKED or ve.state == DONE:
			continue
		_drive(ve, dt)
	if measure:
		_measure()


func _drive(ve: Vehicle, dt: float) -> void:
	var dir := ve.dir_at(ve.s)
	var stop := ve.length() - ve.s
	var g := gap_ahead(ve, ve.p, dir)
	var bend := bend_leader and ve.on_curve(ve.s, ve.s + lookahead * 0.5)
	if bend:
		g = minf(g, _path_gap(ve, ve.s))
	var want_gap := ve.min_gap + ve.headway * ve.v * 0.5
	if g < INF:
		stop = minf(stop, g - want_gap)
	stop = minf(stop, _junction_free(ve, g))
	var target := ve.max_speed
	var brake_d := ve.v * ve.v / (2.0 * ve.decel)
	if ve.on_curve(ve.s, ve.s + brake_d + 2.0):
		target *= ve.corner_speed
	if ve.state == PARKING:
		target = minf(target, 3.0)
	var allow := sqrt(2.0 * ve.decel * maxf(0.0, stop))
	target = minf(target, allow)
	if ve.v < target:
		ve.v = minf(target, ve.v + ve.accel * dt)
	else:
		ve.v = maxf(target, ve.v - ve.decel * dt * 1.5)
	# hard floor: never move closer than min_gap to the leader
	var hard := ve.length() - ve.s
	if g < INF:
		hard = minf(hard, g - ve.min_gap)
	var ds := clampf(ve.v * dt, 0.0, maxf(0.0, hard))
	var ns := ve.s + ds
	var np := ve.at(ns)
	if ds > 0.0 and (gap_ahead(ve, np, ve.dir_at(ns)) < ve.min_gap or (bend and _path_gap(ve, ns) < ve.min_gap)):
		ds = 0.0
		ve.v = 0.0
		ns = ve.s
		np = ve.p
	if ds <= 0.0 and ve.v * dt > 0.0 and hard <= 0.0:
		ve.v = 0.0
	var moved := np - ve.p
	moved.y = 0.0
	ve.s = ns
	ve.p = np
	if moved.length() > 0.001:
		ve.heading = moved.normalized()
	ve.yaw = lerp_angle(ve.yaw, atan2(ve.heading.x, ve.heading.z), 1.0 - exp(-ve.turn_rate * dt))
	_apply_node(ve, ds)
	if ve.s >= ve.length() - 0.01:
		_arrive(ve)


func _arrive(ve: Vehicle) -> void:
	match ve.state:
		DRIVING, WAIT_SLOT:
			if ve.lot == null:
				ve.state = DONE
				ve.v = 0.0
				return
			ve.state = WAIT_SLOT
			ve.v = 0.0
			var k: int = ve.lot.request(ve.id)
			if k >= 0:
				ve.slot = k
				ve.state = PARKING
				_extend(ve, [ve.lot.aisle[k], ve.lot.slots[k]])
		PARKING:
			ve.state = PARKED
			ve.v = 0.0


## The parked vehicle leaves: its slot goes to the queue head. (Removed from the simulation; to drive it on, add() a new
## trip from the lot's road node.)
func leave(ve: Vehicle) -> void:
	if ve.lot != null:
		ve.lot.release(ve.id)
	ve.state = DONE


func remove_done() -> void:
	vehicles = vehicles.filter(func(x): return x.state != DONE)


func _apply_node(ve: Vehicle, ds: float) -> void:
	ve.wheel_angle += ds / maxf(0.05, ve.wheel_radius)
	if ve.node == null:
		return
	ve.node.position = ve.p
	ve.node.rotation.y = ve.yaw
	for w in ve.wheels:
		w.rotation.x = ve.wheel_angle


func _measure() -> void:
	for ve in vehicles:
		if ve.state == PARKED or ve.state == DONE:
			continue
		var g := gap_ahead(ve, ve.p, ve.heading, true, ve.waiting_for)
		if g < INF:
			min_gap_seen = minf(min_gap_seen, g / ve.min_gap)   # as a fraction of the floor: ≥ 1 = never undercut
