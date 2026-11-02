extends "res://addons/coddy_anim/vehicle_mover.gd"
## Medium/High ambient cars use their model footprint. Low keeps the shared mover unchanged.
## Dimensions live on the proxy, so they also survive AmbientTraffic's tour replacement.
const CLEARANCE := 0.12
var junction_groups := {}
var max_body_radius := 1.5


func group_junctions() -> void:
	# Adjacent bends on the short highway/backroad connectors share one reservation.
	# Otherwise two oncoming trucks can each own half of the same swept corner area.
	for id in graph.pos:
		if not graph.is_junction(id) or junction_groups.has(id):
			continue
		var todo := [id]
		junction_groups[id] = id
		while not todo.is_empty():
			var at: String = todo.pop_back()
			for edge in graph._adj[at]:
				var other: String = edge[0]
				if graph.is_junction(other) and float(edge[1]) < 2.0 * (junction_r + max_body_radius) and not junction_groups.has(other):
					junction_groups[other] = id
					todo.append(other)


func _half(ve: Vehicle) -> Vector2:
	return ve.node.get_meta("traffic_half", Vector2(0.46, 0.76)) if ve.node != null else Vector2(0.5, 1.0)


func _extent(ve: Vehicle, axis: Vector3) -> float:
	var h := _half(ve)
	var forward := Vector3(sin(ve.yaw), 0.0, cos(ve.yaw))
	return absf(forward.dot(axis)) * h.y + absf(CoddyRoadGraph.right_of(forward).dot(axis)) * h.x


func _set_path(ve: Vehicle, pp: PackedVector3Array, cv: PackedByteArray, nodes: Array) -> void:
	super._set_path(ve, pp, cv, nodes)
	# Reserve until the rear corners have left; stop before the front corners enter.
	var reach := _half(ve).length() + CLEARANCE
	for sp in ve.spans:
		sp[0] = maxf(0.0, float(sp[0]) - reach)
		sp[1] = float(sp[1]) + reach
		sp[2] = junction_groups.get(sp[2], sp[2])
	var merged: Array = []
	for sp in ve.spans:
		if not merged.is_empty() and merged[-1][2] == sp[2] and float(sp[0]) <= float(merged[-1][1]):
			merged[-1][1] = maxf(float(merged[-1][1]), float(sp[1]))
		else:
			merged.append(sp)
	ve.spans = merged


func gap_ahead(ve: Vehicle, at: Vector3, dir: Vector3, same_dir: bool = false, held_at: String = "") -> float:
	var best := INF
	var right := CoddyRoadGraph.right_of(dir)
	for o in vehicles:
		if o == ve or o.state == PARKED or o.state == DONE:
			continue
		if ve.holding != "" and o.waiting_for == ve.holding and o.heading.dot(dir) < 0.7:
			continue
		if same_dir and o.heading.dot(dir) < 0.7:
			continue
		if held_at != "" and o.holding == held_at:
			continue
		var rel: Vector3 = o.p - at
		var lon := rel.dot(dir)
		if lon <= 0.0 or lon > lookahead:
			continue
		if absf(rel.dot(right)) >= _extent(ve, right) + _extent(o, right):
			continue
		var needed := maxf(ve.min_gap, _extent(ve, dir) + _extent(o, dir) + CLEARANCE)
		# The shared integrator subtracts min_gap; encode this pair's actual bumper gap in that convention.
		best = minf(best, lon - needed + ve.min_gap)
	return best


func _exit_clear(ve: Vehicle, s1: float) -> bool:
	var jid := ""
	for sp in ve.spans:
		if is_equal_approx(float(sp[1]), s1):
			jid = str(sp[2])
			break
	var sv := s1
	var end := minf(ve.length(), s1 + ve.min_gap)
	while sv <= end:
		var p := ve.at(sv)
		var dir := ve.dir_at(sv)
		var side := CoddyRoadGraph.right_of(dir)
		for o in vehicles:
			if o == ve or o.state == PARKED or o.state == DONE or o.heading.dot(dir) < 0.7:
				continue
			if o.waiting_for == jid and o.heading.dot(ve.heading) < 0.7:
				continue # cross traffic is behind its body-aware stop line, outside our reserved area
			var rel: Vector3 = o.p - p
			if absf(rel.dot(side)) < _half(ve).x + _extent(o, side) and absf(rel.dot(dir)) < _half(ve).y + _extent(o, dir) + CLEARANCE:
				return false
		sv += 0.5
	return true


func _junction_free(ve: Vehicle, leader_gap: float) -> float:
	# A tour may revisit a group soon after crossing a different junction. Never hold
	# that future reservation while waiting for the intervening junction.
	var next := ""
	for sp in ve.spans:
		if ve.s <= float(sp[1]):
			next = str(sp[2])
			break
	for jid in _junctions:
		if jid != next:
			(_junctions[jid] as Array).erase(ve.id)
	var stop := super._junction_free(ve, leader_gap)
	if ve.waiting_for != "":
		var q: Array = _junctions.get(ve.waiting_for, [])
		if not q.is_empty() and q[0] == ve.id:
			# The head's exit is full. Let a queued car with a free exit pass, which can
			# also clear that road; keeping the blocked head would lock the whole box.
			q.erase(ve.id)
	return stop
