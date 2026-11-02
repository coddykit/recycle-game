extends RefCounted
## coddy_anim · AnimLod — the weak-phone guard table (W68). Tiers match coddy_perf: 0 Low, 1 Medium, 2 High.
##   update rate     Low 15 Hz · Medium 30 Hz · High 60 Hz (one AnimManager time-slices every figure)
##   skeletal cap    Low 12 · Medium 30 · High 60 figures with a live skeleton; the rest freeze in a static pose or
##                   swap to the game's impostor / billboard (CrowdAnimator.on_impostor)
##   bones           ≤ 24 per skeleton on Low (bone_warning)
##   off screen      no update at all

const LOW := 0
const MEDIUM := 1
const HIGH := 2
const HZ := [15, 30, 60]
const SKELETAL_CAP := [12, 30, 60]
const LOW_MAX_BONES := 24


static func hz(tier: int) -> int:
	return HZ[clampi(tier, LOW, HIGH)]


static func skeletal_cap(tier: int) -> int:
	return SKELETAL_CAP[clampi(tier, LOW, HIGH)]


## "" when fine, else a warning line (print it in the game's tests / debug build, re-rig the model in Meshy/Blender).
static func bone_warning(bone_count: int, tier: int, model: String = "") -> String:
	if tier == LOW and bone_count > LOW_MAX_BONES:
		return "coddy_anim: %s has %d bones (> %d on Low) — use a lighter Meshy rig" % [model, bone_count, LOW_MAX_BONES]
	return ""


## Bones of the first Skeleton3D under `node` (0 when there is none).
static func bone_count(node: Node) -> int:
	var sk := find_skeleton(node)
	return sk.get_bone_count() if sk != null else 0


static func find_skeleton(node: Node) -> Skeleton3D:
	if node == null:
		return null
	if node is Skeleton3D:
		return node
	for c in node.get_children():
		var s := find_skeleton(c)
		if s != null:
			return s
	return null
