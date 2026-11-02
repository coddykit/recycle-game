extends RefCounted
## coddy_anim · FigureRig — skeleton-less procedural fallback (generalised from lifeguard-hq figure_anim.gd, 70c3fd3a78).
## The people are one flat mesh each (no skeleton, no separate parts), so a figure is split ONCE into islands
## (welded-vertex connected components: legs, feet, arms, hands, held props, the rest) and posed by rotating the
## limb islands about hip / shoulder pivots, and the lower leg / forearm again about knee / elbow pivots (W68: straight
## scissor legs read as sliding; a bent knee on the swing leg and bent elbows read as walking). A pose is a plain ArrayMesh with the SAME shared material: walking,
## running, swimming and flailing are a handful of pre-built poses that a node swaps (mesh = ...) — still ONE draw
## per figure on every tier. Body bob / sway / lean / breathing is node transform on top.
## Gaits are driven by the distance actually moved per frame (Walker), so feet never slide faster or slower than
## the figure travels, and standing still stops the cycle.

const POSES := 8              # frames per walk / run / swim / flail cycle
const TREAD_POSES := 4
const WALK_STRIDE := 1.15     # world units per full cycle (two steps)
const RUN_STRIDE := 2.1
const SWIM_STRIDE := 1.5
const RUN_SPEED := 1.5        # u/s above which the gait is a run
const MOVE_SPEED := 0.12      # u/s below which a figure counts as standing
const TURN_RATE := 10.0       # 1/s: heading ease towards the movement direction
const SWELL := 0.045          # the sea shader's vertical swell (water.gdshader `swell`)
const BODY := 0
const LEG_R := 1
const LEG_L := 2
const ARM_R := 3
const ARM_L := 4
const LEG_R_LO := 5           # shin + foot (below the knee), forearm + hand (below the elbow)
const LEG_L_LO := 6
const ARM_R_LO := 7
const ARM_L_LO := 8
const GROUPS := 9
const ELBOW_AT := 0.55        # an arm split by length: the elbow at this share of the shoulder -> hand reach
const CARRY_DEPTH := 0.3      # m: an arm island set deeper than this (and 1.25× the other arm) holds a prop
const CARRY_SWING := 0.25     # share of the gait's arm swing a loaded arm keeps
const DROWNING := ["rip_current", "drifting_float"]   # incident types whose victim is in the water, in trouble

static var _rigs := {}


## The pre-built poses of a figure for a cycle kind (walk | run | swim | flail: 8, tread: 4); [] when it has no rig.
static func meshes(id: String, mesh: ArrayMesh, kind: String) -> Array:
	var r := rig(id, mesh)
	if r == null:
		return []
	var list: Array = []
	for i in (TREAD_POSES if kind == "tread" else POSES):
		var a := angles(kind, i)
		if kind == "walk" or kind == "run":
			a = r.carry(a)
		list.append(a)
	return r.cycle(kind, list)


## Reference wave (lifeguard-hq water.gdshader two-sine swell) — pass your own shader's maths to Swimmer as a Callable.
static func wave_h(x: float, z: float, t: float) -> float:
	var h := sin(x * 0.85 + t * 1.25) * 0.6 + sin(z * 1.15 - t * 1.6 + x * 0.3) * 0.4
	return h * SWELL * clampf(-z * 0.35, 0.15, 1.0)


## The rig of a person model, cached by `id` (null when the model has no arms: loungers, sunbathers...). The mesh must be
## ONE flat ArrayMesh in metres, +Y up, feet at y=0, facing +Z, x = left/right (the Meshy / Blender export convention).
static func rig(id: String, mesh: ArrayMesh) -> Rig:
	if _rigs.has(id):
		return _rigs[id]
	var r: Rig = null
	if mesh != null:
		r = Rig.new()
		if not r.build(mesh):
			r = null
	_rigs[id] = r
	return r


## Limb angles (radians) of pose `i` of a cycle: [leg R, leg L, arm R, arm L, arm R out, arm L out, knee R, knee L,
## elbow R, elbow L] — swing positive = forward / outward; knee positive = the shin folds back, elbow positive = the
## forearm folds forward. kind: walk | run | swim | tread | flail.
## Gait: the knee bends most while its leg swings forward (foot lifted clear of the sand), nearly straight in stance;
## the arm swing is opposite to the legs with a soft (walk) or ~80° (run) elbow.
static func angles(kind: String, i: int) -> PackedFloat32Array:
	match kind:
		"walk":
			var a := TAU * float(i) / float(POSES)
			var s := sin(a)
			var c := cos(a)
			return PackedFloat32Array([0.45 * s, -0.45 * s, -0.4 * s, 0.4 * s, 0.06, 0.06,
					0.08 + 0.75 * maxf(0.0, c), 0.08 + 0.75 * maxf(0.0, -c), 0.3 + 0.15 * maxf(0.0, -s), 0.3 + 0.15 * maxf(0.0, s)])
		"run":
			var a := TAU * float(i) / float(POSES)
			var s := sin(a)
			var c := cos(a)
			return PackedFloat32Array([0.8 * s, -0.8 * s, -0.75 * s, 0.75 * s, 0.1, 0.1,
					0.2 + 1.3 * maxf(0.0, c), 0.2 + 1.3 * maxf(0.0, -c), 1.35, 1.35])
		"swim":   # crawl: the arms circle forward, opposite each other (bent on the pull); the legs flutter from the hip
			var a := TAU * float(i) / float(POSES)
			return PackedFloat32Array([0.3 * sin(a * 2.0), -0.3 * sin(a * 2.0), a, a + PI, 0.12, 0.12,
					0.15 + 0.2 * maxf(0.0, sin(a * 2.0)), 0.15 + 0.2 * maxf(0.0, -sin(a * 2.0)),
					0.5 * maxf(0.0, sin(a)), 0.5 * maxf(0.0, -sin(a))])
		"tread":   # treading water: the arms scull in and out, alternating; the legs cycle (eggbeater)
			var s := sin(TAU * float(i) / float(TREAD_POSES))
			var c := cos(TAU * float(i) / float(TREAD_POSES))
			return PackedFloat32Array([0.15 * s, -0.15 * s, 0.45 * s, -0.45 * s, 0.35 + 0.3 * c, 0.35 - 0.3 * c,
					0.6 + 0.4 * s, 0.6 - 0.4 * s, 0.7 + 0.2 * c, 0.7 - 0.2 * c])
		_:   # flail: both arms thrash high and wide, never in step
			var k := float(i) * 2.399   # irregular but fixed per pose (relative to the figure's rest pose)
			return PackedFloat32Array([0.5 * sin(k * 3.1), 0.5 * sin(k * 2.3 + 1.0), 1.5 * sin(k * 1.7 + 0.4),
					1.5 * sin(k * 2.9 + 0.8), 0.3 + 0.9 * absf(sin(k * 1.3)), 0.3 + 0.9 * absf(cos(k * 2.1)),
					0.4 + 0.6 * absf(sin(k * 1.9)), 0.4 + 0.6 * absf(cos(k * 1.1)), 0.3 + 1.0 * absf(sin(k * 2.7)),
					0.3 + 1.0 * absf(cos(k * 3.3))])


## Pose index of a Walker cycle phase (cycles).
static func pose_of(phase: float, count: int = POSES) -> int:
	return int(fposmod(phase, 1.0) * float(count)) % count


## Irregular head / body motion of a drowning person: always moving, never periodic. -> [bob (m), roll (rad)].
static func flail_body(t: float, seed_: float) -> Vector2:
	var bob := 0.07 + 0.06 * sin(t * 7.3 + seed_) + 0.045 * sin(t * 11.9 + seed_ * 2.1) + 0.03 * sin(t * 3.1 + seed_ * 0.7)
	var roll := 0.22 * sin(t * 5.3 + seed_ * 1.3) + 0.15 * sin(t * 9.1 + seed_)
	return Vector2(bob, roll)


## Pose index of a flailing arm set at time t (≈ 9 changes per second, irregular).
static func flail_pose(t: float, seed_: float) -> int:
	var step := int(t * 9.0)
	return int(absf(sin(float(step) * 12.9898 + seed_ * 78.233) * 43758.5453)) % POSES


## The split figure: per surface the vertices regrouped as body / leg R / leg L / arm R / arm L (indices remapped), plus
## the hip and shoulder pivots. `pose_mesh(angles)` assembles a posed ArrayMesh from native array transforms.
class Rig:
	var surfaces: Array = []      # {tv: Array[PackedVector3Array], tn: Array, colors, idx, mat}
	var pivots: Array = []        # per group: hip / shoulder (upper groups), knee / elbow (the _LO groups)
	var split_y: Array = []       # per upper limb group: vertices below this y belong to its _LO group (INF = none)
	var split_d: Array = []       # per arm without an elbow ring: vertices farther than this from the shoulder = forearm
	var counts: Array = []
	var loaded: Array = []        # per group: an arm that holds a prop (bag, rescue tube): it barely swings, no elbow bend
	var flat: ArrayMesh
	var _cache := {}

	func has_legs() -> bool:
		return int(counts[LEG_R]) > 0 and int(counts[LEG_L]) > 0

	## The figure has a knee (legs) / elbow (arms) to bend.
	func has_knees() -> bool:
		return int(counts[LEG_R_LO]) > 0 and int(counts[LEG_L_LO]) > 0

	func has_elbows() -> bool:
		return int(counts[ARM_R_LO]) > 0 and int(counts[ARM_L_LO]) > 0

	func has_arms() -> bool:
		return int(counts[ARM_R]) > 0 and int(counts[ARM_L]) > 0

	func build(mesh: ArrayMesh) -> bool:
		flat = mesh
		for _g in GROUPS:
			pivots.append(Vector3.ZERO)
			counts.append(0)
			split_y.append(-INF)
			loaded.append(false)
			split_d.append(0.0)
		var isl: Array = []   # per surface: {verts, root: PackedInt32Array, boxes: Dictionary root -> AABB}
		for s in mesh.get_surface_count():
			var arrays := mesh.surface_get_arrays(s)
			var verts: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var index: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
			var parent := PackedInt32Array()
			parent.resize(verts.size())
			var key_id := {}
			var vid := PackedInt32Array()
			vid.resize(verts.size())
			for k in verts.size():
				var key := Vector3i((verts[k] * 1000.0).round())
				if not key_id.has(key):
					key_id[key] = key_id.size()
				vid[k] = key_id[key]
			var up := PackedInt32Array()
			up.resize(key_id.size())
			for k in up.size():
				up[k] = k
			for t in range(0, index.size(), 3):
				var r0 := _find(up, vid[index[t]])
				for j in 2:
					var rj := _find(up, vid[index[t + 1 + j]])
					if rj != r0:
						up[rj] = r0
			var boxes := {}
			var root := PackedInt32Array()
			root.resize(verts.size())
			for k in verts.size():
				var r := _find(up, vid[k])
				root[k] = r
				if boxes.has(r):
					boxes[r] = (boxes[r] as AABB).expand(verts[k])
				else:
					boxes[r] = AABB(verts[k], Vector3.ZERO)
			isl.append({"arrays": arrays, "root": root, "boxes": boxes})
		# classification: legs (tube from the floor up, its feet, the shorts), arms (off-axis, shoulder height)
		var hip_y := 0.0
		for e in isl:
			for r in e.boxes:
				var b: AABB = e.boxes[r]
				if b.position.y < 0.12 and b.end.y > 0.3 and b.end.y < 1.0 and b.size.x < 0.24 and absf(_cx(b)) > 0.03:
					hip_y = maxf(hip_y, b.end.y)
		var group_of := []   # per surface: Dictionary root -> group
		for e in isl:
			var g := {}
			for r in e.boxes:
				var b: AABB = e.boxes[r]
				var cx := _cx(b)
				var side := 1 if cx > 0.0 else -1
				var gr := BODY
				if hip_y > 0.0 and absf(cx) > 0.03 and b.size.x < 0.24 and (b.end.y < 0.12 or (b.position.y < 0.12 and b.end.y > 0.3 and b.end.y < 1.0)):
					gr = LEG_R if side > 0 else LEG_L
				elif hip_y > 0.0 and absf(cx) > 0.03 and absf(cx) < 0.2 and b.size.x < 0.24 and b.position.y > 0.3 \
						and b.end.y <= hip_y + 0.03 and b.end.y > hip_y - 0.35:
					gr = LEG_R if side > 0 else LEG_L
				elif absf(cx) > 0.1 and b.size.x < 0.45 and b.position.y > 0.3 and b.size.y < 1.0 and (hip_y <= 0.0 or b.end.y > hip_y * 0.9):
					gr = ARM_R if side > 0 else ARM_L
				g[r] = gr
			group_of.append(g)
		# pivots: hip = top of the leg tube at its x centre; shoulder = top inner corner of the arm islands
		var leg_box := [AABB(), AABB(), AABB(), AABB(), AABB()]
		var seen := [false, false, false, false, false]
		for si in isl.size():
			for r in isl[si].boxes:
				var gr: int = group_of[si][r]
				if gr == BODY:
					continue
				var b: AABB = isl[si].boxes[r]
				leg_box[gr] = (leg_box[gr] as AABB).merge(b) if seen[gr] else b
				seen[gr] = true
		for gr in [LEG_R, LEG_L]:
			if seen[gr]:
				var top := AABB()   # the leg tube that reaches the hip
				for si in isl.size():
					for r in isl[si].boxes:
						var tb: AABB = isl[si].boxes[r]
						if group_of[si][r] == gr and tb.size.y > 0.3 and (top.size == Vector3.ZERO or tb.end.y > top.end.y):
							top = tb
				pivots[gr] = Vector3(_cx(top), hip_y, _cz(top))
		# shoulder = where the arm touches the body: the arm vertex nearest to any body vertex (ring-centred)
		var body_v := PackedVector3Array()
		var arm_v := [PackedVector3Array(), PackedVector3Array(), PackedVector3Array(), PackedVector3Array(), PackedVector3Array()]
		for si in isl.size():
			var verts: PackedVector3Array = isl[si].arrays[Mesh.ARRAY_VERTEX]
			var rootv: PackedInt32Array = isl[si].root
			for k in verts.size():
				var gr: int = group_of[si][rootv[k]]
				if gr == BODY:
					if k % 3 == 0:
						body_v.append(verts[k])
				elif gr == ARM_R or gr == ARM_L:
					arm_v[gr].append(verts[k])   # NOT (arm_v[gr] as PackedVector3Array).append: the cast appends to a copy
		for gr in [ARM_R, ARM_L]:
			var av: PackedVector3Array = arm_v[gr]
			if av.is_empty():
				continue
			var best := av[0]
			var best_d := INF
			for v in av:
				for bv in body_v:
					var d := v.distance_squared_to(bv)
					if d < best_d:
						best_d = d
						best = v
			var sum := Vector3.ZERO
			var n := 0
			for v in av:
				if v.distance_to(best) < 0.07:
					sum += v
					n += 1
			pivots[gr] = sum / float(maxi(1, n))
		# knee / elbow: the vertex ring nearest the middle of the limb (legs ~52 % of the hip height up from the sole,
		# arms half way from the shoulder to the hand); vertices below it form the _LO group, pivot = the ring centre.
		# A limb without a ring there (one box from hip to foot) simply does not bend.
		var by_length: Array = []
		for gr in [LEG_R, LEG_L, ARM_R, ARM_L]:
			var ys := {}
			var lo := INF
			for si in isl.size():
				var verts: PackedVector3Array = isl[si].arrays[Mesh.ARRAY_VERTEX]
				var rootv: PackedInt32Array = isl[si].root
				for k in verts.size():
					if int(group_of[si][rootv[k]]) == gr:
						ys[snappedf(verts[k].y, 0.01)] = true
						lo = minf(lo, verts[k].y)
			if ys.is_empty():
				continue
			var top: float = (pivots[gr] as Vector3).y
			var want := top * 0.52 if gr == LEG_R or gr == LEG_L else (top + lo) * 0.5
			var best := INF
			for y in ys:
				if absf(float(y) - want) < absf(best - want):
					best = float(y)
			var span := top - lo
			var ring := not (best == INF or absf(best - want) > span * 0.2 or best - lo < span * 0.2 or top - best < span * 0.2)
			if not ring:
				if gr == ARM_R or gr == ARM_L:
					by_length.append(gr)
				continue
			var sum := Vector3.ZERO
			var n := 0
			var n_lo := 0
			var n_all := 0
			for si in isl.size():
				var verts: PackedVector3Array = isl[si].arrays[Mesh.ARRAY_VERTEX]
				var rootv: PackedInt32Array = isl[si].root
				for k in verts.size():
					if int(group_of[si][rootv[k]]) != gr:
						continue
					n_all += 1
					if verts[k].y < best - 0.006:
						n_lo += 1
					if absf(verts[k].y - best) < 0.02:
						sum += verts[k]
						n += 1
			if n_lo < 8 or n_lo * 5 < n_all:   # a stray corner, not a forearm / shin: leave the limb straight
				if gr == ARM_R or gr == ARM_L:
					by_length.append(gr)
				continue
			split_y[gr] = best - 0.006
			pivots[gr + 4] = sum / float(maxi(1, n))
		# an arm with no usable elbow ring (one box from shoulder to hand, or a forearm held out level — the child model):
		# split it by length instead — the forearm = the vertices farther than ELBOW_AT of the arm's reach from the
		# shoulder, elbow pivot = the centre of the vertices around that distance (or the point on the reach line)
		for gr in by_length:
			var sh: Vector3 = pivots[gr]
			var far := 0.0
			var far_v := sh
			for si in isl.size():
				var verts: PackedVector3Array = isl[si].arrays[Mesh.ARRAY_VERTEX]
				var rootv: PackedInt32Array = isl[si].root
				for k in verts.size():
					if int(group_of[si][rootv[k]]) == gr and verts[k].distance_to(sh) > far:
						far = verts[k].distance_to(sh)
						far_v = verts[k]
			if far < 0.15:
				continue
			var cut := far * ELBOW_AT
			var sum := Vector3.ZERO
			var n := 0
			var n_lo := 0
			for si in isl.size():
				var verts: PackedVector3Array = isl[si].arrays[Mesh.ARRAY_VERTEX]
				var rootv: PackedInt32Array = isl[si].root
				for k in verts.size():
					if int(group_of[si][rootv[k]]) != gr:
						continue
					var d := verts[k].distance_to(sh)
					if d > cut:
						n_lo += 1
					if absf(d - cut) < far * 0.12:
						sum += verts[k]
						n += 1
			if n_lo < 4:
				continue
			split_d[gr] = cut
			pivots[gr + 4] = sum / float(n) if n > 0 else sh.lerp(far_v, ELBOW_AT)
		# a held prop (medic bag, rescue tube) makes its arm much deeper (z) than the other arm: that arm is `loaded`
		var depth := [0.0, 0.0, 0.0, 0.0, 0.0]
		for gr in [ARM_R, ARM_L]:
			var zlo := INF
			var zhi := -INF
			for si in isl.size():
				var verts: PackedVector3Array = isl[si].arrays[Mesh.ARRAY_VERTEX]
				var rootv: PackedInt32Array = isl[si].root
				for k in verts.size():
					if int(group_of[si][rootv[k]]) == gr:
						zlo = minf(zlo, verts[k].z)
						zhi = maxf(zhi, verts[k].z)
			depth[gr] = maxf(0.0, zhi - zlo)
		for gr in [ARM_R, ARM_L]:
			var other: float = depth[ARM_L if gr == ARM_R else ARM_R]
			loaded[gr] = float(depth[gr]) > CARRY_DEPTH and float(depth[gr]) > other * 1.25
		# reorder every surface by group, remap indices
		for si in isl.size():
			var arrays: Array = isl[si].arrays
			var verts: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var norms = arrays[Mesh.ARRAY_NORMAL]
			var cols = arrays[Mesh.ARRAY_COLOR]
			var index: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
			var root: PackedInt32Array = isl[si].root
			var tv: Array = []
			var tn: Array = []
			var remap := PackedInt32Array()
			remap.resize(verts.size())
			var offset := 0
			var colors := PackedColorArray()
			var vg := PackedInt32Array()
			vg.resize(verts.size())
			for k in verts.size():
				var g0 := int(group_of[si][root[k]])
				if g0 >= LEG_R and g0 <= ARM_L and (verts[k].y < float(split_y[g0]) \
						or (float(split_d[g0]) > 0.0 and verts[k].distance_to(pivots[g0]) > float(split_d[g0]))):
					g0 += 4
				vg[k] = g0
			for gr in GROUPS:
				var pv := PackedVector3Array()
				var pn := PackedVector3Array()
				for k in verts.size():
					if vg[k] != gr:
						continue
					remap[k] = offset + pv.size()
					pv.append(verts[k])
					pn.append(norms[k] if norms is PackedVector3Array else Vector3.UP)
					if cols is PackedColorArray:
						colors.append(cols[k])
				tv.append(pv)
				tn.append(pn)
				counts[gr] = int(counts[gr]) + pv.size()
				offset += pv.size()
			var idx := PackedInt32Array()
			idx.resize(index.size())
			for k in index.size():
				idx[k] = remap[index[k]]
			surfaces.append({"tv": tv, "tn": tn, "colors": colors, "idx": idx, "mat": mesh.surface_get_material(si)})
		return has_arms()

	static func _find(up: PackedInt32Array, k: int) -> int:
		while up[k] != k:
			up[k] = up[up[k]]
			k = up[k]
		return k

	static func _cx(b: AABB) -> float:
		return b.position.x + b.size.x * 0.5

	static func _cz(b: AABB) -> float:
		return b.position.z + b.size.z * 0.5

	## Walk / run angles for a figure that carries something: a loaded arm swings at CARRY_SWING of the gait and keeps
	## its elbow as modelled, so the prop hangs steady instead of being flung about.
	func carry(a: PackedFloat32Array) -> PackedFloat32Array:
		var out := a.duplicate()
		for gr in [ARM_R, ARM_L]:
			if not bool(loaded[gr]):
				continue
			out[gr - 1] = float(a[gr - 1]) * CARRY_SWING
			if out.size() >= 10:
				out[gr + 5] = 0.0
		return out

	func has_load() -> bool:
		return bool(loaded[ARM_R]) or bool(loaded[ARM_L])

	## A posed copy: a = [leg R, leg L, arm R, arm L, arm R out, arm L out, knee R, knee L, elbow R, elbow L] (see
	## angles(); a 6-value set leaves knees / elbows straight).
	func pose_mesh(a: PackedFloat32Array) -> ArrayMesh:
		var out := ArrayMesh.new()
		for sf in surfaces:
			var v: PackedVector3Array = sf.tv[BODY]
			var n: PackedVector3Array = sf.tn[BODY]
			var ups := [Transform3D.IDENTITY, Transform3D.IDENTITY, Transform3D.IDENTITY, Transform3D.IDENTITY, Transform3D.IDENTITY]
			for gr in [LEG_R, LEG_L, ARM_R, ARM_L]:   # vertex order = group order (the remapped indices rely on it)
				var swing := float(a[gr - 1])
				var out_z := 0.0
				var sgn := 1.0 if gr == LEG_R or gr == ARM_R else -1.0
				if gr == ARM_R:
					out_z = float(a[4])
				elif gr == ARM_L:
					out_z = float(a[5])
				var basis := Basis(Vector3(0, 0, 1), out_z * sgn) * Basis(Vector3.RIGHT, -swing)
				var piv: Vector3 = pivots[gr]
				ups[gr] = Transform3D(basis, piv - basis * piv)
				var pv: PackedVector3Array = sf.tv[gr]
				if not pv.is_empty():
					v += (ups[gr] as Transform3D) * pv
					n += Transform3D(basis, Vector3.ZERO) * (sf.tn[gr] as PackedVector3Array)
			for gr in [LEG_R, LEG_L, ARM_R, ARM_L]:
				var lv: PackedVector3Array = sf.tv[gr + 4]
				if lv.is_empty():
					continue
				var bend := float(a[gr + 5]) if a.size() >= 10 else 0.0
				# knee folds the shin back (+x rotation), elbow folds the forearm forward
				var kb := Basis(Vector3.RIGHT, bend if gr == LEG_R or gr == LEG_L else -bend)
				var kp: Vector3 = pivots[gr + 4]
				var lo: Transform3D = (ups[gr] as Transform3D) * Transform3D(kb, kp - kb * kp)
				v += lo * lv
				n += Transform3D(lo.basis, Vector3.ZERO) * (sf.tn[gr + 4] as PackedVector3Array)
			var arrays := []
			arrays.resize(Mesh.ARRAY_MAX)
			arrays[Mesh.ARRAY_VERTEX] = v
			arrays[Mesh.ARRAY_NORMAL] = n
			if not (sf.colors as PackedColorArray).is_empty():
				arrays[Mesh.ARRAY_COLOR] = sf.colors
			arrays[Mesh.ARRAY_INDEX] = sf.idx
			out.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
			out.surface_set_material(out.get_surface_count() - 1, sf.mat)
		return out

	## The pre-built poses of a cycle (built once, on first use): `list` = one angle set per pose.
	func cycle(kind: String, list: Array) -> Array:
		if _cache.has(kind):
			return _cache[kind]
		var out: Array = []
		for a in list:
			out.append(pose_mesh(a))
		_cache[kind] = out
		return out
