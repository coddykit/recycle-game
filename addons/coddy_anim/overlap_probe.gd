extends RefCounted
## coddy_anim · OverlapProbe — the "vehicles never drive through each other" check (W68, owner 10-24: "buses, minibuses
## and cars must not pass through each other"). Each vehicle is a footprint rectangle on the ground (length along its
## own +Z or −Z, width across) at the node's position and yaw; two footprints intersect when the separating-axis test
## finds no gap. Games feed it their moving nodes every simulated step and assert `worst_depth` stays 0 (or under a
## small tolerance for parked rows that touch). Pure maths, no physics server.
##   var probe := OverlapProbe.new()
##   probe.add(node, 4.2, 1.8)            # metres; optional `shrink` keeps rounded bumpers from counting
##   ... each step: probe.sample()        # records the deepest overlap seen and the pair
##   assert(probe.worst_depth <= 0.0)

var items: Array = []          ## [{node, half: Vector2(half width, half length)}]
var worst_depth := 0.0         ## deepest overlap seen (m along the separating axis), 0 = never touched
var worst_pair := ""           ## "name_a × name_b @ position" of the deepest overlap
var samples := 0


func add(node: Node3D, length: float, width: float, shrink: float = 0.9) -> void:
	items.append({"node": node, "half": Vector2(width, length) * 0.5 * shrink})


func clear() -> void:
	items.clear()
	worst_depth = 0.0
	worst_pair = ""
	samples = 0


## One pass over every pair of visible vehicles (O(n²): fine for test fleets of a few dozen).
func sample() -> float:
	samples += 1
	var deepest := 0.0
	var boxes: Array = []
	for it in items:
		var n: Node3D = it.node
		if not is_instance_valid(n) or not (n.is_visible_in_tree() if n.is_inside_tree() else n.visible):
			continue
		boxes.append([n, _corners(n, it.half)])
	for i in boxes.size():
		for j in range(i + 1, boxes.size()):
			var d := depth(boxes[i][1], boxes[j][1])
			if d > deepest:
				deepest = d
			if d > worst_depth:
				worst_depth = d
				var a: Node3D = boxes[i][0]
				var b: Node3D = boxes[j][0]
				var at := _xf(a).origin
				worst_pair = "%s × %s @ (%.1f, %.1f)" % [a.name, b.name, at.x, at.z]
	return deepest


## Ground-plane corners of a footprint (x, z).
static func _corners(n: Node3D, half: Vector2) -> PackedVector2Array:
	var t := _xf(n)
	var fwd := Vector2(t.basis.z.x, t.basis.z.z).normalized()
	var side := Vector2(t.basis.x.x, t.basis.x.z).normalized()
	var c := Vector2(t.origin.x, t.origin.z)
	return PackedVector2Array([c + fwd * half.y + side * half.x, c + fwd * half.y - side * half.x,
		c - fwd * half.y - side * half.x, c - fwd * half.y + side * half.x])


static func _xf(n: Node3D) -> Transform3D:
	return n.global_transform if n.is_inside_tree() else n.transform   # (tests drive nodes outside a tree)


## Overlap depth of two convex quads (0 = apart or just touching): the smallest projection overlap over all edge normals.
static func depth(a: PackedVector2Array, b: PackedVector2Array) -> float:
	var best := INF
	for poly in [a, b]:
		for k in 4:
			var e: Vector2 = poly[(k + 1) % 4] - poly[k]
			var axis := Vector2(-e.y, e.x).normalized()
			var ra := _span(a, axis)
			var rb := _span(b, axis)
			var o := minf(ra.y, rb.y) - maxf(ra.x, rb.x)
			if o <= 0.0:
				return 0.0
			best = minf(best, o)
	return best


static func _span(p: PackedVector2Array, axis: Vector2) -> Vector2:
	var lo := INF
	var hi := -INF
	for q in p:
		var d := q.dot(axis)
		lo = minf(lo, d)
		hi = maxf(hi, d)
	return Vector2(lo, hi)
