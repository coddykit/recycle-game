extends SceneTree
## Performance gate (owner rule 2026-10-22, games/CLAUDE.md §0): every game, on the Low profile, in 2–3 typical scenes
## with a fixed seed, must stay within 3D primitives ≤ MAX_PRIMS and 3D draw calls ≤ MAX_DRAWS per frame (the 2D UI is
## reported apart) and static memory
## ≤ MAX_STATIC_MB; the median process time and the frame time are reported (informative: the process monitor can lag
## behind a scene set up in one long frame, and a game may cap its own frame rate). Needs a real renderer — run it
## WINDOWED (games/tools/perf/perf_gate.sh does: Compatibility renderer, phone-sized window, shot-lock); under
## --headless there is nothing to count, so it prints "PERF_GATE SKIP" and passes.
## Source of truth: games/tools/perf (copied into each game as addons/coddy_perf by sync.sh).
##
## The game supplies res://tests/perf_gate_hook.gd (or --hook=<path>):
##   static func scenes() -> Array                    # names of the 2–3 typical scenes, e.g. ["start", "late"]
##   static func setup(tree: SceneTree, scene: String) # may await: build the scene with Low applied BEFORE the world
##                                                     # is built (as on a weak phone), camera at the typical view
##   static func teardown(tree: SceneTree)            # optional: free what setup made
##   static func budget_on_reported() -> bool         # optional, TEMPORARY: true = budget primitives on the old
##                                                     # (reported) number while a real over-budget is being fixed
## Prints one "PERF_GATE <scene> …" line per scene and "PERF_GATE: n scenes, k failures"; exit code 1 on a failure.
##
## Real primitives (#181, 2026-10-31): Godot 4.7.2's GLES3 counter adds a MultiMesh surface that has LODs ONCE at the
## LOD it picked, not once per instance (its LOD branch skips `*= instance_count`; LOD-less surfaces are multiplied), so a
## forest of 1,000 LOD'd trees counts as one tree (skiresort W66: Medium read +8k, really +311k). `prims=` keeps that
## reported number; `real_prims=` re-multiplies, for every visible MultiMeshInstance3D whose mesh has LODs, the LOD'd
## part of its counted share by its instances — the share is measured by hiding that one node on a paused frame, so it
## is the LOD the renderer really drew (real_prims()). `lod0_prims=` is an informative upper bound (mesh LOD off
## everywhere). The budget uses real_prims (README § Real primitives).

const MAX_PRIMS := 300000
const MAX_DRAWS := 150
const MAX_STATIC_MB := 250.0
const SETTLE_FRAMES := 60
const FRAMES := 120

var _args := {}


func _initialize() -> void:
	for a in OS.get_cmdline_user_args():
		var kv := a.trim_prefix("--").split("=", true, 1)
		_args[kv[0]] = kv[1] if kv.size() > 1 else "1"
	_run.call_deferred()


func _run() -> void:
	if DisplayServer.get_name() == "headless" or RenderingServer.get_current_rendering_method() == "dummy":
		print("PERF_GATE SKIP: no renderer (run games/tools/perf/perf_gate.sh, windowed)")
		quit(0)
		return
	var path := String(_args.get("hook", "res://tests/perf_gate_hook.gd"))
	if not ResourceLoader.exists(path):
		print("PERF_GATE FAIL: no hook %s" % path)
		quit(1)
		return
	var hook: GDScript = load(path)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	var only := String(_args.get("scene", ""))
	var frames := int(_args.get("frames", str(FRAMES)))
	var fails := 0
	var n := 0
	for scene in hook.scenes():
		if only != "" and scene != only:
			continue
		n += 1
		await hook.setup(self, scene)
		Engine.max_fps = 0
		for i in SETTLE_FRAMES:
			await process_frame
		var r := await measure(self, frames)
		r.merge(await real_prims(self, int(r.prims)))
		r["budget_on_reported"] = hook.has_method("budget_on_reported") and bool(hook.budget_on_reported())
		var bad := failures(r)
		if not bad.is_empty():
			fails += 1
		# old keys first and unchanged (prims = the reported counter); real_prims / lod0_prims / lod_mm appended
		print("PERF_GATE %s prims=%d draws=%d ui_draws=%d process_ms=%.2f (p95 %.2f) frame_ms=%.2f static_mb=%d real_prims=%d lod0_prims=%d lod_mm=%d%s %s" % [scene,
			r.prims, r.draws, r.ui_draws, r.process_ms, r.process_p95_ms, r.frame_ms, r.static_mb, r.real_prims, r.lod0_prims,
			r.lod_mm.size(), _note(r), "PASS" if bad.is_empty() else "FAIL " + ", ".join(bad)])
		for e in r.lod_mm.slice(0, 5):
			print("PERF_GATE_LOD_MM %s %s instances=%d counted=%d real=%d lod0=%d lod_tris=%s" % [scene, e.path, e.instances,
				e.counted, e.real, e.lod0, e.lod_tris])
		if hook.has_method("teardown"):
			hook.teardown(self)
			await process_frame
	print("PERF_GATE: %d scenes, %d failures (limits: primitives %d, draws %d, static %.0f MB)" % [n, fails,
		MAX_PRIMS, MAX_DRAWS, MAX_STATIC_MB])
	quit(1 if fails > 0 or n == 0 else 0)


## 3D primitives / draw calls (visible + shadow passes of the root viewport and every SubViewport — the 2D UI is not
## counted, `ui_draws` reports it) averaged over `frames` frames; process time as the median and p95 of the frames (one
## long setup frame must not count as the scene's cost). frame_ms is informative only: a game may cap its own FPS.
static func measure(tree: SceneTree, frames: int) -> Dictionary:
	var vps: Array = [tree.root]
	for n in tree.root.find_children("*", "SubViewport", true, false):
		if not (n as SubViewport).disable_3d:
			vps.append(n)
	var prims := 0.0
	var draws := 0.0
	var total := 0.0
	var proc := PackedFloat32Array()
	var t0 := Time.get_ticks_usec()
	for i in frames:
		await tree.process_frame
		for vp in vps:
			var rid: RID = (vp as Viewport).get_viewport_rid()
			for pass_type in [RenderingServer.VIEWPORT_RENDER_INFO_TYPE_VISIBLE, RenderingServer.VIEWPORT_RENDER_INFO_TYPE_SHADOW]:
				prims += RenderingServer.viewport_get_render_info(rid, pass_type, RenderingServer.VIEWPORT_RENDER_INFO_PRIMITIVES_IN_FRAME)
				draws += RenderingServer.viewport_get_render_info(rid, pass_type, RenderingServer.VIEWPORT_RENDER_INFO_DRAW_CALLS_IN_FRAME)
		total += Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)
		proc.append(Performance.get_monitor(Performance.TIME_PROCESS) * 1000.0)
	var ms := (Time.get_ticks_usec() - t0) / 1000.0 / frames
	proc.sort()
	return {"prims": int(prims / frames), "draws": int(draws / frames), "ui_draws": maxi(0, int((total - draws) / frames)),
		"process_ms": proc[proc.size() / 2], "process_p95_ms": proc[int(proc.size() * 0.95)], "frame_ms": ms,
		"static_mb": int(OS.get_static_memory_usage() / 1048576)}


## The budgeted primitive count: real_prims when measured (perf_gate), the reported counter otherwise (callers of
## measure() alone) or while the hook's budget_on_reported() is true.
static func budget_prims(r: Dictionary) -> int:
	if r.has("real_prims") and not bool(r.get("budget_on_reported", false)):
		return int(r.real_prims)
	return int(r.prims)


static func _note(r: Dictionary) -> String:
	if bool(r.get("budget_on_reported", false)) and int(r.get("real_prims", 0)) > MAX_PRIMS:
		return " REAL_OVER=%d>%d(budget_on_reported:TODO)" % [r.real_prims, MAX_PRIMS]
	return ""


static func failures(r: Dictionary) -> Array:
	var out := []
	var prims := budget_prims(r)
	if int(r.prims) <= 0:
		out.append("nothing drawn (the scene did not build?)")
	if prims > MAX_PRIMS:
		out.append("%s %d > %d" % ["real primitives" if r.has("real_prims") and prims == int(r.real_prims) else "primitives",
			prims, MAX_PRIMS])
	if int(r.draws) > MAX_DRAWS:
		out.append("draws %d > %d" % [r.draws, MAX_DRAWS])
	if float(r.static_mb) > MAX_STATIC_MB:
		out.append("static %d MB > %.0f" % [r.static_mb, MAX_STATIC_MB])
	return out


## True if any surface of `mesh` has LOD index arrays (imported .glb meshes get them by default).
static func has_lods(mesh: Mesh) -> bool:
	if mesh == null:
		return false
	for i in mesh.get_surface_count():
		if not (RenderingServer.mesh_get_surface(mesh.get_rid(), i).get("lods", []) as Array).is_empty():
			return true
	return false


## Triangles of every LOD of `mesh`, summed over its surfaces: [LOD0, LOD1, …] (a surface with fewer LODs keeps its last).
static func lod_tris(mesh: Mesh) -> Array:
	var per := []
	for i in mesh.get_surface_count():
		var d := RenderingServer.mesh_get_surface(mesh.get_rid(), i)
		var bytes := 2 if int(d.get("vertex_count", 0)) <= 65536 else 4
		var t := [int(d.get("index_count", 0)) / 3]
		for l in d.get("lods", []):
			t.append((l["index_data"] as PackedByteArray).size() / bytes / 3)
		per.append(t)
	var out := []
	var depth := 0
	for t in per:
		depth = maxi(depth, t.size())
	for k in depth:
		var sum := 0
		for t in per:
			sum += int(t[mini(k, t.size() - 1)])
		out.append(sum)
	return out


## Instances the renderer draws for a MultiMesh (visible_instance_count, -1 = all).
static func drawn_instances(mm: MultiMesh) -> int:
	return mm.instance_count if mm.visible_instance_count < 0 else mini(mm.visible_instance_count, mm.instance_count)


## Real primitives (see the header). Pauses the tree so the scene holds still, then for every visible
## MultiMeshInstance3D with ≥ 2 drawn instances, an LOD'd mesh and mesh LOD on in its viewport, hides its own render
## instance for a few frames: its share of the counter, per pass type (visible / shadow), = reported − reported
## without it. Godot multiplies the mesh's LOD-less surfaces by the instances but counts its LOD'd surfaces once (at the
## LOD it drew), so: share = NL × N × k + L, NL = triangles of the LOD-less surfaces, N = drawn instances, k = how many
## times that pass type drew the node (viewports, shadow splits), L = the LOD'd surfaces as drawn once. k comes from
## the same share with mesh LOD off (every surface multiplied: share0 = T0 × N × k, T0 = all surfaces at LOD0), so
## real share = NL × N × k + L × N. `base` = the scene's reported prims (measure()).
## Also lod0_prims: every viewport with mesh_lod_threshold 0 (LOD0 everywhere — over-states every mesh that really
## drew a coarser LOD). Restores visibility, thresholds and pause.
static func real_prims(tree: SceneTree, base: int, frames := 3) -> Dictionary:
	var vps := _viewports(tree)
	var was_paused := tree.paused
	tree.paused = true
	var mms := []
	for n in tree.root.find_children("*", "MultiMeshInstance3D", true, false):
		var mmi := n as MultiMeshInstance3D
		if not mmi.is_visible_in_tree() or mmi.multimesh == null or drawn_instances(mmi.multimesh) < 2:
			continue
		if mmi.get_viewport().mesh_lod_threshold <= 0.0 or not has_lods(mmi.multimesh.mesh):
			continue
		mms.append(mmi)
	var th := []
	for vp in vps:
		th.append((vp as Viewport).mesh_lod_threshold)
	# pass 1: LOD off — the whole scene (lod0_prims) and each node's share (gives k)
	for vp in vps:
		(vp as Viewport).mesh_lod_threshold = 0.0
	var shown0 := await _sample(tree, vps, frames)
	var share0 := []
	for mmi in mms:
		# paused = a still scene: one frame per hidden sample is enough (airport-baron: 7 scenes × ~80 nodes)
		share0.append(_minus(shown0, await _hidden(tree, vps, 1, mmi)))
	for i in vps.size():
		(vps[i] as Viewport).mesh_lod_threshold = th[i]
	# pass 2: LOD as the game runs it — each node's real share
	var entries := []
	var extra := 0.0
	if not mms.is_empty():
		var shown := await _sample(tree, vps, frames)
		for j in mms.size():
			var mmi: MultiMeshInstance3D = mms[j]
			if share0[j][0] + share0[j][1] <= 0.0:
				continue # not drawn even at LOD0 (outside the view): costs nothing
			var share := _minus(shown, await _hidden(tree, vps, 1, mmi))
			var n := drawn_instances(mmi.multimesh)
			var tris := surface_tris(mmi.multimesh.mesh)
			var counted := 0.0
			var real := 0.0
			for p in 2:
				var k := roundf(share0[j][p] / maxf(1.0, tris.all_lod0 * n))
				var lodded := maxf(0.0, share[p] - tris.no_lod * n * k)
				counted += share[p]
				real += tris.no_lod * n * k + lodded * n
			extra += real - counted
			entries.append({"path": str(tree.root.get_path_to(mmi)), "instances": n, "counted": int(counted),
				"real": int(real), "lod0": int(share0[j][0] + share0[j][1]), "lod_tris": lod_tris(mmi.multimesh.mesh)})
		entries.sort_custom(func(a, b): return a.real > b.real)
	await _sample(tree, vps, 1)
	tree.paused = was_paused
	return {"real_prims": base + int(extra), "lod0_prims": int(shown0[0] + shown0[1]), "lod_mm": entries}


## {all_lod0, no_lod}: triangles of every surface at LOD0, and of the surfaces without LODs only.
static func surface_tris(mesh: Mesh) -> Dictionary:
	var all := 0
	var plain := 0
	for i in mesh.get_surface_count():
		var d := RenderingServer.mesh_get_surface(mesh.get_rid(), i)
		var t := int(d.get("index_count", 0)) / 3
		if t == 0:
			t = int(d.get("vertex_count", 0)) / 3
		all += t
		if (d.get("lods", []) as Array).is_empty():
			plain += t
	return {"all_lod0": all, "no_lod": plain}


static func _hidden(tree: SceneTree, vps: Array, frames: int, mmi: MultiMeshInstance3D) -> Array:
	# the node's own render instance only (children stay drawn, no visibility notifications to the game)
	RenderingServer.instance_set_visible(mmi.get_instance(), false)
	var r := await _sample(tree, vps, frames)
	RenderingServer.instance_set_visible(mmi.get_instance(), true)
	return r


static func _minus(a: Array, b: Array) -> Array:
	return [maxf(0.0, a[0] - b[0]), maxf(0.0, a[1] - b[1])]


static func _viewports(tree: SceneTree) -> Array:
	var vps: Array = [tree.root]
	for n in tree.root.find_children("*", "SubViewport", true, false):
		if not (n as SubViewport).disable_3d:
			vps.append(n)
	return vps


## Mean 3D primitives per pass type [visible, shadow] (every viewport) over `frames` frames after 2 settle frames.
static func _sample(tree: SceneTree, vps: Array, frames: int) -> Array:
	for i in 2:
		await tree.process_frame
	var prims := [0.0, 0.0]
	for i in frames:
		await tree.process_frame
		for vp in vps:
			var rid: RID = (vp as Viewport).get_viewport_rid()
			for p in 2:
				var pass_type := RenderingServer.VIEWPORT_RENDER_INFO_TYPE_VISIBLE if p == 0 else RenderingServer.VIEWPORT_RENDER_INFO_TYPE_SHADOW
				prims[p] += RenderingServer.viewport_get_render_info(rid, pass_type, RenderingServer.VIEWPORT_RENDER_INFO_PRIMITIVES_IN_FRAME)
	return [prims[0] / frames, prims[1] / frames]
