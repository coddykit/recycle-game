class_name TrafficSystem
extends Node3D
## Ambient traffic on the unlocked road network (W68, games/docs/W68_ROLLOUT_TABLE.md): cars roam on the shared
## coddy_anim AmbientTraffic — right-hand lane with rounded corners, a following distance they never undercut (before,
## cars drove through each other), one car at a time through a junction, eased speed and heading, wheels by distance.
## The simulation steps at the graphics tier's coddy_anim rate (Low 15 / Medium 30 / High 60 Hz) and the cars are drawn
## between their last two steps every frame. No physics.

const MODELS := ["sedan", "sedan", "taxi", "sedan", "minibus", "box_truck"]
const COLORS := ["e5484d", "3d8bd9", "f4f1ea", "5dbb63", "ffd23f", "8e5cd9", "2b2f36", "f28c28", "9fe2bf"]
const LANE := 0.55   # road mesh 2.0 m wide: two 0.92 m cars pass with ~18 cm between them, both inside the edge
const AmbientTraffic = preload("res://addons/coddy_anim/ambient_traffic.gd")
const AnimLod = preload("res://addons/coddy_anim/anim_lod.gd")
const CoddyRoadGraph = preload("res://addons/coddy_anim/road_graph.gd")
const VehicleMover = preload("res://addons/coddy_anim/vehicle_mover.gd")
const OverlapProbe = preload("res://addons/coddy_anim/overlap_probe.gd")
const SIZED_MOVER_PATH := "res://scripts/world/ambient_vehicle_mover.gd"
## W68 (owner 10-24: vehicles never drive through each other) — the player's fleet drives the same lanes on its contract
## clock (it never waits). Each fleet vehicle stands in the ambient simulation as a still "phantom" vehicle, so a car
## behind it (or meeting it in a junction exit) brakes and queues like behind any car. Where a fleet vehicle closes in on
## a car anyway (from behind, faster; across a junction) the car shrinks away in FADE_S and comes back later at a clear
## spot; one held FADE_STUCK_S behind a fleet vehicle stopped for an event does the same (no queue for 30 s).
const FADE_S := 0.35
const FADE_CLEAR := 0.3       ## m between a car's box and a fleet vehicle's box before the car makes way (+ CLOSING_MS
const CLOSING_MS := 6.0       ## × the step: a car and a fleet vehicle closing at up to 6 m/s between two 15 Hz steps on Low)
const FADE_STUCK_S := 8.0
## This world is small (a car is 1.5 m, the shortest road 4 m): every distance of the module scaled to it.
const SIM := {"lane_offset": LANE, "corner_r": 1.0, "junction_r": 3.0, "lookahead": 8.0, "spawn_clear": 3.4, "chain_reach": 1.4,
	"junction_guard": 1.6, "tour_steps": 16}

## {node (drawn holder), proxy (moved by the sim, never drawn), car (sim handle or null), low, opts, from / to}
var cars: Array = []
var traffic: AmbientTraffic
var _region := -1
var _sized_traffic := false
var _acc := 0.0
var _retry := 0.0
var fleet: Dictionary = {}     ## vehicle id -> VehicleActor (World's live dictionary): the cars make way for these
var _phantoms := {}           ## VehicleActor -> VehicleMover.Vehicle standing in for it in `traffic`
var _phantom_sim: AmbientTraffic
var faded := 0                ## cars that made way so far (tests)


func refresh_count() -> void:
	var want := mini(4 + 3 * RegionManager.unlocked_count, 26)
	want = maxi(1, int(round(want * float(World.profile(GameManager.quality()).traffic))))
	var low := GameManager.quality() == "low"
	if RegionManager.max_region() != _region or (traffic != null and _sized_traffic == low):
		_rebuild_roads()
	for i in range(cars.size() - 1, -1, -1): # quality changed: cars built in the other form are replaced
		if bool(cars[i].low) != low:
			_drop(cars[i])
			cars.remove_at(i)
	while cars.size() < want:
		_spawn()
	while cars.size() > want:
		_drop(cars.pop_back())


## The roads of the unlocked regions as a coddy_anim graph (dead ends included: a car turns round there in its lane).
## Cars already driving are put back on the new network.
func _rebuild_roads() -> void:
	_region = RegionManager.max_region()
	_sized_traffic = GameManager.quality() != "low"
	var g := RegionManager.graph
	var links := {}
	for e in g.edges:
		if int(g.nodes[e.a].region) <= _region and int(g.nodes[e.b].region) <= _region:
			(links.get_or_add(e.a, []) as Array).append(e.b)
			(links.get_or_add(e.b, []) as Array).append(e.a)
	var roads := CoddyRoadGraph.new()
	for id in links:
		var p := g.pos(id)
		roads.add_node(id, Vector3(p.x, p.y, p.z))
	for id in links:
		for other in links[id]:
			if str(id) < str(other):
				roads.add_road(id, other)
	if _sized_traffic:
		# A long body also sweeps across the other lane at a bend or a dead-end U-turn.
		for id in links:
			var neighbours: Array = links[id]
			if neighbours.size() != 2 or (roads.pos[neighbours[0]] - roads.pos[id]).normalized().dot((roads.pos[neighbours[1]] - roads.pos[id]).normalized()) > -0.99:
				roads.mark_junction(id)
	traffic = AmbientTraffic.new()
	if _sized_traffic:
		traffic.vm = load(SIZED_MOVER_PATH).new() # Low does not load the additional mover at all
	traffic.setup(roads, 42, SIM)
	if _sized_traffic:
		var max_half := Vector2.ZERO
		for model_name in MODELS:
			max_half = max_half.max(_model_half(model_name))
		traffic.vm.lane_offset = maxf(LANE, max_half.x + 0.1) # oncoming real-width bodies also need space
		traffic.vm.max_body_radius = max_half.length()
		traffic.vm.junction_r = traffic.vm.corner_r + 2.0 * traffic.vm.lane_offset # body radius is added per car
		traffic.junction_guard = 0.0 # the sized mover already expands the stop line by the body's corner radius
		traffic.vm.group_junctions()
	for c in cars:
		c.car = null
		(c.node as Node3D).visible = false


func _spawn() -> void:
	var model_name: String = MODELS.pick_random()
	var holder := Node3D.new()
	holder.visible = false
	add_child(holder)
	var node := Models.instance(model_name, Models.paint_material(Color(COLORS.pick_random())))
	node.rotation.y = PI   # the models face −Z; the simulation drives along +Z
	holder.add_child(node)
	var low := GameManager.quality() == "low"
	if low and LowBatch.merge_tree(node) != null:
		pass # one baked mesh per car on Low, the wheels stay still
	var proxy := Node3D.new()   # what the simulation moves (and spins the wheels of); drawn through the holder
	if not low:
		proxy.set_meta("traffic_half", _model_half(model_name))
	add_child(proxy)
	var heavy := model_name in ["minibus", "box_truck"]
	var c := {"node": holder, "proxy": proxy, "car": null, "low": low, "model": model_name, "from": [Vector3.ZERO, 0.0], "to": [Vector3.ZERO, 0.0],
		"opts": {"max_speed": randf_range(2.2, 3.4) * (0.8 if heavy else 1.0), "min_gap": 3.0 if heavy else 2.4,
			"accel": 0.9 if heavy else 1.3, "decel": 3.0, "headway": 0.6, "wheel_radius": 0.17}}
	cars.append(c)
	_enter(c)
	if not low:   # wheels: the sim spins the proxy's copies' angle; the holder's model reads it each frame
		c["wheels"] = node.find_children("Wheel*", "Node3D", true, false)


static func _model_half(model_name: String) -> Vector2:
	var meta := Data.meta(model_name)
	# Include off-centre bodies (box_truck's rear and front are not symmetric about its origin).
	return Vector2(maxf(absf(float(meta.min[0])), absf(float(meta.max[0]))),
		maxf(absf(float(meta.min[2])), absf(float(meta.max[2]))))


## Into the simulation when a clear spot exists (else tried again shortly).
func _enter(c: Dictionary) -> void:
	if traffic == null:
		return
	c.car = traffic.add(c.proxy, c.opts)
	if c.car != null:
		var p: Node3D = c.proxy
		c.from = [p.position, p.rotation.y]
		c.to = [p.position, p.rotation.y]
		_draw(c, 1.0)
	(c.node as Node3D).visible = c.car != null


func _drop(c: Dictionary) -> void:
	if c.car != null and traffic != null:
		traffic.remove(c.car)
	c.car = null
	(c.node as Node3D).queue_free()
	(c.proxy as Node3D).queue_free()


func _draw(c: Dictionary, f: float) -> void:
	var n: Node3D = c.node
	n.position = (c.from[0] as Vector3).lerp(c.to[0], f)
	n.rotation.y = lerp_angle(float(c.from[1]), float(c.to[1]), f)


func _process(delta: float) -> void:
	if traffic == null or cars.is_empty():
		return
	var tier := {"low": AnimLod.LOW, "medium": AnimLod.MEDIUM}.get(GameManager.quality(), AnimLod.HIGH) as int
	var period := 1.0 / float(AnimLod.hz(tier))
	_acc += delta
	if _acc >= period:
		var steps := floorf(_acc / period)
		_acc -= steps * period
		_sync_fleet()
		traffic.step(period * minf(steps, 3.0))   # a long frame never moves a car more than three steps at once
		for c in cars:
			if c.car != null:
				c.from = c.to
				c.to = [(c.proxy as Node3D).position, (c.proxy as Node3D).rotation.y]
		if not _phantoms.is_empty():
			_make_way(period * minf(steps, 3.0))
	for c in cars:   # making way: the car shrinks where it stands, then waits hidden for a clear spot (re-entered below)
		var fade := float(c.get("fade", 0.0))
		if fade > 0.0:
			fade -= delta
			c["fade"] = fade
			var n: Node3D = c.node
			n.scale = Vector3.ONE * clampf(fade / FADE_S, 0.01, 1.0)
			if fade <= 0.0:
				n.visible = false
				n.scale = Vector3.ONE
	var f := clampf(_acc / period, 0.0, 1.0)
	for c in cars:
		if c.car == null:
			continue
		_draw(c, f)
		for w in c.get("wheels", []):
			(w as Node3D).rotation.x = -float(c.car.ve.wheel_angle)
	_retry -= delta
	if _retry <= 0.0:
		_retry = 0.25
		var budget := 2   # a region unlock re-enters every car over a few frames (no hitch)
		for c in cars:
			if c.car == null and budget > 0 and float(c.get("fade", 0.0)) <= 0.0:
				budget -= 1
				_enter(c)


## The fleet vehicles as still phantoms in the ambient simulation (position + heading refreshed every step).
func _sync_fleet() -> void:
	if _phantom_sim != traffic:   # a new network (region unlock): the old phantoms went with the old simulation
		_phantoms.clear()
		_phantom_sim = traffic
	for a in _phantoms.keys():
		if not is_instance_valid(a) or not fleet.values().has(a) or not (a as Node3D).is_visible_in_tree():
			traffic.vm.vehicles.erase(_phantoms[a])
			_phantoms.erase(a)
	for a in fleet.values():
		var n := a as Node3D
		if not is_instance_valid(n) or not n.is_visible_in_tree():
			continue
		var ve: VehicleMover.Vehicle = _phantoms.get(n)
		if ve == null:
			ve = VehicleMover.Vehicle.new()
			ve.id = -1 - _phantoms.size()   # never a junction queue's id (those are ≥ 0)
			ve.max_speed = 0.0
			ve.min_gap = 0.01
			ve.curve = PackedByteArray([0, 0])
			ve.cum = PackedFloat32Array([0.0, 1.0])
			_phantoms[n] = ve
			traffic.vm.vehicles.append(ve)
		var p := n.global_position
		var d := -n.global_basis.z
		d.y = 0.0
		d = d.normalized() if d.length() > 0.01 else Vector3(0, 0, 1)
		ve.p = p
		ve.heading = d
		ve.pts = PackedVector3Array([p, p + d])
		ve.s = 0.0
		ve.v = 0.0


## A car about to be touched by a fleet vehicle (or stuck behind a stopped one) makes way.
func _make_way(dt: float) -> void:
	for c in cars:
		if c.car == null:
			continue
		var proxy: Node3D = c.proxy
		var meta := Data.meta(str(c.get("model", "sedan")))
		var mine := _box(proxy.position, proxy.rotation.y, meta, FADE_CLEAR + CLOSING_MS * dt)
		var go := false
		var behind := false
		for a in _phantoms:
			var n := a as Node3D
			var rel: Vector3 = n.global_position - proxy.position
			if rel.x * rel.x + rel.z * rel.z > 25.0:
				continue
			var fwd := Vector3(sin(proxy.rotation.y), 0.0, cos(proxy.rotation.y))
			if rel.dot(fwd) > 0.0 and absf(rel.dot(Vector3(fwd.z, 0.0, -fwd.x))) < LANE * 1.5:
				behind = true   # a fleet vehicle right ahead in our lane
			var theirs := _box(n.global_position, atan2(-n.global_basis.z.x, -n.global_basis.z.z),
				{"size": [float(n.get_meta("width", 1.0)), 1.0, float(n.get_meta("length", 2.0))]}, 0.0)
			if OverlapProbe.depth(mine, theirs) > 0.0:
				go = true
				break
		var stuck := float(c.get("stuck", 0.0))
		stuck = stuck + dt if behind and float(c.car.ve.v) < 0.05 else 0.0
		c["stuck"] = stuck
		if go or stuck > FADE_STUCK_S:
			traffic.remove(c.car)
			c.car = null
			c["fade"] = FADE_S
			c["stuck"] = 0.0
			faded += 1


## A ground box (x, z corners) of a model_meta size around p, turned by yaw, grown by `grow` m on every side.
static func _box(p: Vector3, yaw: float, meta: Dictionary, grow: float) -> PackedVector2Array:
	var size: Array = meta.get("size", [1.0, 1.0, 1.5])
	var fwd := Vector2(sin(yaw), cos(yaw))
	var side := Vector2(fwd.y, -fwd.x)
	var c := Vector2(p.x, p.z)
	var hl := float(size[2]) * 0.5 + grow
	var hw := float(size[0]) * 0.5 + grow
	return PackedVector2Array([c + fwd * hl + side * hw, c + fwd * hl - side * hw, c - fwd * hl - side * hw, c - fwd * hl + side * hw])


func positions() -> Array:
	var out := []
	for c in cars:
		if (c.node as Node3D).visible:
			out.append((c.node as Node3D).global_position)
	return out
