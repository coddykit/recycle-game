extends RefCounted
## Shader warm-up (W63 item 4, shared module): the first time a material (or a particle system) is drawn, the GL
## driver compiles its shader on the main thread — a visible hitch in the middle of play, worst on weak phones. Behind
## the loading curtain, draw every SHADER of the built scene once: one small quad per distinct shader (materials that
## share a generated shader share one quad), a one-instance sample of every MultiMesh, a copy of every particle system
## and the caller's warm-up-only models — a few per frame, within a time budget.
## Source of truth: games/tools/perf (copied into each game as addons/coddy_perf by sync.sh).
##
##   # after the world is built, while the curtain still covers the screen:
##   await PerfWarmup.run(world_root, camera)               # sliced draws, safe teardown; returns the counts
##   await PerfWarmup.run(world_root, camera, 2, [holder])  # + warm-up-only nodes of the caller (sliced + torn down)
##   await PerfWarmup.run(world, cam, 2, [], {"hold": true}) # a world built THIS frame: its own geometry sliced too
##   await PerfWarmup.retire([throwaway_world])            # a warm-up-only tree built outside run()
##   opts: batch (start, ≤ MAX_BATCH), budget_ms (BUDGET_MS), limit (max items, e.g. a Low-profile cut), hold
##
## 2026-10-21, BlockForge 1.4.2 GLThread SIGSEGV on cold emulator starts whose warm-up took 9–19 s. Two causes are
## guarded here:
##   A. OVERLOAD — every warm-up material forced to compile in ONE frame (16 + 6 particles in lego, 308 in disaster,
##      42 + 11 in zombi): the GL thread blocks for seconds (PowerVR ANRs in IMGeglSwapBuffers, Corrupt vitals).
##      Now: one item per distinct shader key, items revealed BATCH (4) at a time (adaptive 1…MAX_BATCH 6: a frame
##      > 250 ms halves it), each batch drawn for one frame then hidden, and the whole pass stops after BUDGET_MS
##      (3 s) — what is left compiles lazily in play (most-used shaders go first). Warm-up-only holders passed in
##      `retired` (or a direct child of `world` named "ShaderWarm", the factory games' warm_set()) are sliced the same
##      way: their models are hidden before the first frame and shown a few at a time. {"hold": true} also slices the
##      world's own geometry (the camera's cull mask is held on a warm-up layer, one node per shader key is exposed per
##      batch, as corrupt-tycoon's ShaderWarmup does) — for a world added to the tree in the same frame.
##   B. EARLY FREE — warm-up-only resources freed the frame after their first draw. Now every Material drawn, every
##      resource of the warm-up-only nodes (meshes, MultiMeshes, particle materials, Environment / Sky / camera
##      attributes) and the stage's own quad / MultiMesh samples are KEPT FOR THE APP LIFETIME (a few KB; a kept
##      material also keeps its compiled shader); warm-up-only nodes are hidden, POST_DRAWS (2) frame_post_draw
##      signals pass, the NODES are freed, SETTLE_FRAMES (3) more frames pass — nodes first, resources never.
## Draw inside the real scene's world with its own camera — never a separate camera / World3D / Environment freed right
## after (a warning is printed when the camera does not see `world`; lego 1.4.2 crashed on such a throw-away world
## followed by a scene change even with B fixed, see README). Safe headless (returns at once) and with an empty scene.

const FRAMES := 2 ## at least this many warm-up frames (also with nothing to draw)
const BATCH := 4 ## warm-up items revealed in the first frame
const MAX_BATCH := 6 ## never more items in one frame
const BUDGET_MS := 3000 ## the whole sliced pass; what is left compiles lazily in play
const SLOW_FRAME_MS := 250.0 ## a frame this long halves the batch
const FAST_FRAME_MS := 60.0 ## a frame this short grows it by one
const POST_DRAWS := 2 ## RenderingServer.frame_post_draw signals after the last warm-up draw before any node is freed
const SETTLE_FRAMES := 3 ## frames after the warm-up nodes are freed before run() / retire() return
const WARM_LAYER := 1 << 19 ## render layer 20: the held camera draws only this layer (hold mode)
const HOLDER_NAME := "ShaderWarm" ## factory games' warm_set() holder: sliced + retired automatically
const SLOW_FILE := "user://perf_warmup_slow_ms" ## debug builds only: ms slept after each warm-up frame (slow-GPU repro)

static var _kept: Array[Resource] = []
static var _kept_ids := {}


## Every resource the warm-up keeps alive for the rest of the app (tests; never edit it).
static func kept() -> Array[Resource]:
	return _kept


static func is_kept(r: Resource) -> bool:
	return r != null and _kept_ids.has(r.get_instance_id())


## Keeps `r` (and a material's next_pass chain / shader, an Environment's sky / sky material) for the app lifetime.
static func keep(r: Resource) -> void:
	if r == null or _kept_ids.has(r.get_instance_id()):
		return
	_kept_ids[r.get_instance_id()] = true
	_kept.append(r)
	if r is Material and (r as Material).next_pass != null:
		keep((r as Material).next_pass)
	if r is ShaderMaterial and (r as ShaderMaterial).shader != null:
		keep((r as ShaderMaterial).shader)
	if r is Environment and (r as Environment).sky != null:
		keep((r as Environment).sky)
	if r is Sky and (r as Sky).sky_material != null:
		keep((r as Sky).sky_material)
	if r is Mesh:
		for s in (r as Mesh).get_surface_count():
			keep((r as Mesh).surface_get_material(s))
	if r is MultiMesh and (r as MultiMesh).mesh != null:
		keep((r as MultiMesh).mesh)


## Every distinct Material the 3D / 2D nodes under `root` use: material overrides, surface override materials, the
## meshes' own surface materials (MultiMesh included), particle process + draw-pass materials, CanvasItem materials.
static func collect_materials(root: Node) -> Array[Material]:
	var seen := {}
	var out: Array[Material] = []
	var stack: Array[Node] = [root]
	while not stack.is_empty():
		var n: Node = stack.pop_back()
		for m in _materials_of(n):
			if m != null and not seen.has(m.get_instance_id()):
				seen[m.get_instance_id()] = true
				out.append(m)
		for c in n.get_children():
			stack.append(c)
	return out


static func _materials_of(n: Node) -> Array:
	var out := []
	if n is GeometryInstance3D:
		out.append((n as GeometryInstance3D).material_override)
		out.append((n as GeometryInstance3D).material_overlay)
	if n is MeshInstance3D:
		var mi := n as MeshInstance3D
		if mi.mesh != null:
			for i in mi.mesh.get_surface_count():
				out.append(mi.get_surface_override_material(i))
				out.append(mi.mesh.surface_get_material(i))
	elif n is MultiMeshInstance3D:
		var mm := (n as MultiMeshInstance3D).multimesh
		if mm != null and mm.mesh != null:
			for i in mm.mesh.get_surface_count():
				out.append(mm.mesh.surface_get_material(i))
	elif n is GPUParticles3D:
		var p := n as GPUParticles3D
		out.append(p.process_material)
		for i in p.draw_passes:
			var dm := p.get_draw_pass_mesh(i)
			if dm != null:
				for s in dm.get_surface_count():
					out.append(dm.surface_get_material(s))
	elif n is CPUParticles3D:
		var cm := (n as CPUParticles3D).mesh
		if cm != null:
			for s in cm.get_surface_count():
				out.append(cm.surface_get_material(s))
	if n is CanvasItem:
		out.append((n as CanvasItem).material)
	return out


## Every resource the nodes under `root` hand to the renderer: their materials (collect_materials), meshes, MultiMeshes,
## particle draw passes, and the Environment / Sky / CameraAttributes of WorldEnvironments and cameras.
static func collect_resources(root: Node) -> Array[Resource]:
	var out: Array[Resource] = []
	for m in collect_materials(root):
		out.append(m)
	var nodes: Array[Node] = [root]
	nodes.append_array(root.find_children("*", "", true, false))
	for n in nodes:
		if n is MeshInstance3D:
			out.append((n as MeshInstance3D).mesh)
			out.append((n as MeshInstance3D).skin)
		elif n is MultiMeshInstance3D:
			out.append((n as MultiMeshInstance3D).multimesh)
		elif n is GPUParticles3D:
			for i in (n as GPUParticles3D).draw_passes:
				out.append((n as GPUParticles3D).get_draw_pass_mesh(i))
			out.append((n as GPUParticles3D).draw_skin)
		elif n is CPUParticles3D:
			out.append((n as CPUParticles3D).mesh)
		elif n is WorldEnvironment:
			out.append((n as WorldEnvironment).environment)
			out.append((n as WorldEnvironment).camera_attributes)
		elif n is Camera3D:
			out.append((n as Camera3D).environment)
			out.append((n as Camera3D).attributes)
	return out.filter(func(r): return r != null)


## How many geometry nodes under `root` use each material (instance id → count): the most used shaders warm first.
static func material_uses(root: Node) -> Dictionary:
	var uses := {}
	for n in [root] + root.find_children("*", "", true, false):
		for m in _materials_of(n):
			if m != null:
				uses[m.get_instance_id()] = int(uses.get(m.get_instance_id(), 0)) + 1
	return uses


## Materials that share a compiled shader share a key: a ShaderMaterial by its Shader, a BaseMaterial3D by the
## features that pick its generated shader (corrupt-tycoon ShaderWarmup.shader_key); anything else by itself.
static func shader_key(m: Material) -> String:
	if m == null:
		return "default"
	var k := ""
	if m is ShaderMaterial:
		var sh := (m as ShaderMaterial).shader
		k = "S%d" % (sh.get_instance_id() if sh != null else 0)
	elif m is BaseMaterial3D:
		var b := m as BaseMaterial3D
		k = "B%d.%d.%d.%d.%d.%d.%d.%d.%d" % [b.transparency, b.shading_mode, b.cull_mode, b.depth_draw_mode, b.blend_mode,
			b.billboard_mode, b.diffuse_mode, b.specular_mode, b.texture_filter]
		for i in BaseMaterial3D.FLAG_MAX:
			k += "1" if b.get_flag(i) else "0"
		for i in BaseMaterial3D.FEATURE_MAX:
			k += "1" if b.get_feature(i) else "0"
		for i in BaseMaterial3D.TEXTURE_MAX:
			k += "1" if b.get_texture(i) != null else "0"
	else:
		k = "M%d" % m.get_instance_id()
	if m.next_pass != null:
		k += ">" + shader_key(m.next_pass)
	return k


## One material per shader key, most used first (`uses` from material_uses; ties keep the input order).
static func shader_representatives(materials: Array[Material], uses: Dictionary = {}) -> Array[Material]:
	var seen := {}
	var out: Array[Material] = []
	var order := range(materials.size())
	order.sort_custom(func(a, b):
		var ua := int(uses.get(materials[a].get_instance_id(), 0))
		var ub := int(uses.get(materials[b].get_instance_id(), 0))
		return ua > ub or (ua == ub and a < b))
	for i in order:
		var m: Material = materials[i]
		var k := m.get_class() + ":" + shader_key(m)
		if not seen.has(k):
			seen[k] = true
			out.append(m)
	return out


## Hold mode: one visible GeometryInstance3D per draw kind × shader keys under `root`, outside the `skip` subtrees.
static func geometry_representatives(root: Node, skip: Array = []) -> Array[GeometryInstance3D]:
	var seen := {}
	var out: Array[GeometryInstance3D] = []
	var stack: Array[Node] = [root]
	while not stack.is_empty():
		var n: Node = stack.pop_back()
		if n in skip or (n is Node3D and not (n as Node3D).visible):
			continue
		if n is GeometryInstance3D and not (n is GPUParticles3D or n is CPUParticles3D):
			var kind := n.get_class()
			if n is MultiMeshInstance3D:
				var mm := (n as MultiMeshInstance3D).multimesh
				kind += "" if mm == null else "%d%d%d" % [mm.transform_format, int(mm.use_colors), int(mm.use_custom_data)]
			var keys: PackedStringArray = [kind]
			for m in _materials_of(n):
				if m != null:
					keys.append(shader_key(m))
			var key := "|".join(keys)
			if not seen.has(key):
				seen[key] = true
				out.append(n as GeometryInstance3D)
		for c in n.get_children():
			stack.append(c)
	return out


## One sample per distinct MultiMesh drawing under `root` (mesh + transform format + per-instance colour / custom
## data): a MultiMesh draws with the INSTANCED variant of its materials' shaders, which a plain quad never compiles
## (W63, S2's train-baron finding) — the stage draws a one-instance copy of each.
static func collect_multimeshes(root: Node) -> Array[MultiMesh]:
	var seen := {}
	var out: Array[MultiMesh] = []
	for n in root.find_children("*", "MultiMeshInstance3D", true, false):
		var mm := (n as MultiMeshInstance3D).multimesh
		if mm == null or mm.mesh == null:
			continue
		var key := "%d|%d|%s|%s" % [mm.mesh.get_instance_id(), mm.transform_format, mm.use_colors, mm.use_custom_data]
		if (n as MultiMeshInstance3D).material_override != null:
			key += "|%d" % (n as MultiMeshInstance3D).material_override.get_instance_id()
		if not seen.has(key):
			seen[key] = true
			var one := MultiMesh.new()
			one.transform_format = mm.transform_format
			one.use_colors = mm.use_colors
			one.use_custom_data = mm.use_custom_data
			one.mesh = mm.mesh
			one.instance_count = 1
			if one.transform_format == MultiMesh.TRANSFORM_3D:
				one.set_instance_transform(0, Transform3D(Basis().scaled(Vector3.ONE * 0.01), Vector3.ZERO))
			else:
				one.set_instance_transform_2d(0, Transform2D(0.0, Vector2.ONE * 0.01, 0.0, Vector2.ZERO))
			if one.use_colors:
				one.set_instance_color(0, Color.WHITE)
			if one.use_custom_data:
				one.set_instance_custom_data(0, Color(0, 0, 0, 0))
			one.set_meta("perf_warmup_override", (n as MultiMeshInstance3D).material_override)
			out.append(one)
	return out


## Every particle system under `root` (they compile their process shader on the first emission).
static func collect_particles(root: Node) -> Array[Node3D]:
	var out: Array[Node3D] = []
	for n in root.find_children("*", "GPUParticles3D", true, false):
		out.append(n as Node3D)
	for n in root.find_children("*", "CPUParticles3D", true, false):
		out.append(n as Node3D)
	return out


## The staging node: a grid of small quads (one per 3D material) and copies of the particle systems, 3 m in front of
## `camera`, all visible. ParticleProcessMaterials are only drawn through their particle system, CanvasItem
## materials only on a 2D node: those go into a CanvasLayer under the stage.
static func stage(camera: Camera3D, materials: Array[Material], particles: Array[Node3D], multimeshes: Array[MultiMesh] = []) -> Node3D:
	var root := Node3D.new()
	root.name = "PerfWarmup"
	root.set_meta("perf_warmup_stage", true)
	var quad := QuadMesh.new()
	quad.size = Vector2(0.05, 0.05)
	var cols := maxi(1, int(ceil(sqrt(float(materials.size())))))
	var i := 0
	var canvas: CanvasLayer = null
	for m in materials:
		if m is ParticleProcessMaterial:
			continue
		if m is CanvasItemMaterial or (m is ShaderMaterial and (m as ShaderMaterial).shader != null \
				and (m as ShaderMaterial).shader.get_mode() == Shader.MODE_CANVAS_ITEM):
			if canvas == null:
				canvas = CanvasLayer.new()
				canvas.layer = -100
				root.add_child(canvas)
			var r := ColorRect.new()
			r.size = Vector2(2, 2)
			r.material = m
			canvas.add_child(r)
			continue
		if m is ShaderMaterial and (m as ShaderMaterial).shader != null and (m as ShaderMaterial).shader.get_mode() != Shader.MODE_SPATIAL:
			continue # sky / fog shaders compile with their environment
		var mi := MeshInstance3D.new()
		mi.mesh = quad
		mi.material_override = m
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		mi.position = Vector3((i % cols) * 0.06, -(i / cols) * 0.06, 0.0)
		root.add_child(mi)
		i += 1
	var j := 0
	for mm in multimeshes:
		var mmi := MultiMeshInstance3D.new()
		mmi.multimesh = mm
		mmi.material_override = mm.get_meta("perf_warmup_override", null)
		mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		mmi.position = Vector3(-0.3 - (j % 8) * 0.06, -(j / 8) * 0.06, 0.0)
		root.add_child(mmi)
		j += 1
	for p in particles:
		if not is_instance_valid(p):
			continue
		var c := p.duplicate() as Node3D
		c.position = Vector3.ZERO
		c.visible = true
		c.set_meta("perf_warmup_emit", true) # emitting is switched on once the stage is in the tree (run / emit)
		root.add_child(c)
	if camera != null and camera.is_inside_tree():
		root.transform = camera.global_transform * Transform3D(Basis(), Vector3(-0.5, 0.5, -3.0))
	return root


## Starts the particle copies of a stage that is now in the tree (a particle system reads its global transform when it
## starts emitting).
static func emit(st: Node) -> void:
	for c in st.get_children():
		if c.has_meta("perf_warmup_emit"):
			if c is GPUParticles3D:
				(c as GPUParticles3D).one_shot = true
				(c as GPUParticles3D).emitting = true
			elif c is CPUParticles3D:
				(c as CPUParticles3D).one_shot = true
				(c as CPUParticles3D).emitting = true


## Draws every shader under `world` once in front of `camera` (+ the caller's warm-up-only `retired` nodes, already in
## the tree, and any direct child named HOLDER_NAME), a few items per frame within the budget, then tears the stage and
## the holders down safely (see the top). Returns {materials, shaders, particles, multimeshes, items, drawn, skipped,
## ms}. A no-op headless (no GPU to compile for) or without a camera — the holders are then freed at once.
static func run(world: Node, camera: Camera3D, frames: int = FRAMES, retired: Array = [], opts: Dictionary = {}) -> Dictionary:
	if world == null or camera == null or not world.is_inside_tree() or DisplayServer.get_name() == "headless":
		for n in _holders(world, retired):
			(n as Node).queue_free()
		return {"materials": 0, "shaders": 0, "particles": 0, "multimeshes": 0, "items": 0, "drawn": 0, "skipped": 0, "ms": 0}
	return await _run(world, camera, frames, retired, true, [], opts)


## Safe teardown of warm-up-only nodes built outside run() (a throw-away diorama, effect samples): their resources are
## kept for the app lifetime, the nodes are hidden, POST_DRAWS frame_post_draw signals pass, the nodes are freed, then
## SETTLE_FRAMES frames pass. Headless: freed at once.
static func retire(nodes: Array) -> void:
	var tree: SceneTree = null
	for n in nodes:
		if n is Node and is_instance_valid(n) and (n as Node).is_inside_tree():
			tree = (n as Node).get_tree()
			break
	if tree == null or DisplayServer.get_name() == "headless":
		for n in nodes:
			if n is Node and is_instance_valid(n):
				(n as Node).queue_free()
		return
	await _retire(tree, nodes, true, [])


## `retired` + the direct children of `world` named HOLDER_NAME (valid nodes, once each).
static func _holders(world: Node, retired: Array) -> Array:
	var out: Array = []
	for n in retired:
		if n is Node and is_instance_valid(n) and not out.has(n):
			out.append(n)
	if world != null and is_instance_valid(world):
		for c in world.get_children():
			if str(c.name) == HOLDER_NAME and not out.has(c):
				out.append(c)
	return out


## The body of run(): `gpu` false = the headless test drives it (frame_post_draw does not fire without a renderer, a
## process frame stands in for it); `trace` gets [event, process frame, …] entries ("draw" carries the batch size).
static func _run(world: Node, camera: Camera3D, frames: int, retired: Array, gpu: bool, trace: Array, opts: Dictionary = {}) -> Dictionary:
	var t0 := Time.get_ticks_msec()
	var tree := world.get_tree()
	if world is Node3D and camera.is_inside_tree() and camera.get_world_3d() != (world as Node3D).get_world_3d():
		push_warning("PerfWarmup: the camera does not draw `world` (separate World3D / viewport) — warm up inside the real scene")
	var holders := _holders(world, retired)
	for h in holders:
		(h as Node).set_meta("perf_warmup_holder", true)
	var mats := collect_materials(world)
	var reps := shader_representatives(mats, material_uses(world))
	var parts := collect_particles(world)
	var mms := collect_multimeshes(world)
	for m in mats:
		keep(m)
	for mm in mms:
		keep(mm)
	var st := stage(camera, reps, parts, mms)
	world.add_child(st)
	st.global_transform = camera.global_transform * Transform3D(Basis(), Vector3(-0.5, 0.5, -3.0))
	for r in collect_resources(st): # the quad, the MultiMesh samples, the particle copies' meshes / materials
		keep(r)
	var hold := bool(opts.get("hold", false))
	var held_mask := camera.cull_mask
	# the items, cheapest-to-skip last: world geometry (hold), shader quads (most used first), canvas rects, MultiMesh
	# samples, particle copies, then the holders' models — all hidden before the first frame is drawn
	var items: Array = []
	if hold:
		camera.cull_mask = WARM_LAYER
		trace.append(["hold", Engine.get_process_frames(), camera.cull_mask])
		for g in geometry_representatives(world, [st] + holders):
			items.append(g)
		for n in [st] + holders:
			for g in [n] + (n as Node).find_children("*", "GeometryInstance3D", true, false):
				if g is GeometryInstance3D:
					(g as GeometryInstance3D).layers |= WARM_LAYER
	for c in st.get_children():
		if c is CanvasLayer:
			items.append_array(c.get_children())
		else:
			items.append(c)
	for h in holders:
		if h is Node3D and (h as Node3D).visible: # a hidden holder (effect samples) only feeds the collection
			for c in (h as Node).get_children():
				if c != st and (c is Node3D or c is CanvasItem): # (the stage itself may sit in a holder used as `world`)
					items.append(c)
	var limit := int(opts.get("limit", 0))
	var dropped: Array = items.slice(limit) if limit > 0 and items.size() > limit else []
	if not dropped.is_empty():
		items = items.slice(0, limit)
	for it in items + dropped:
		_show_item(it, false, hold)
	var budget := int(opts.get("budget_ms", BUDGET_MS))
	var batch := clampi(int(opts.get("batch", BATCH)), 1, MAX_BATCH)
	var slow := _slow_ms()
	var i := 0
	var n_frames := 0
	var last := Time.get_ticks_usec()
	var pass_t0 := Time.get_ticks_msec()
	while i < items.size() or n_frames < maxi(1, frames):
		if i < items.size() and Time.get_ticks_msec() - pass_t0 > budget:
			break # over budget: the rest compiles lazily in play
		var cur: Array = items.slice(i, i + batch)
		for it in cur:
			_show_item(it, true, hold)
		await tree.process_frame # the frame that draws this batch (and compiles its shaders) has been processed
		if slow > 0:
			OS.delay_msec(slow) # debug repro of a slow GPU: every warm-up frame takes seconds, as on a weak phone
		trace.append(["draw", Engine.get_process_frames(), cur.size()])
		n_frames += 1
		i += cur.size()
		var now := Time.get_ticks_usec()
		var ms := (now - last) / 1000.0
		last = now
		if ms > SLOW_FRAME_MS:
			batch = maxi(1, batch / 2)
		elif ms < FAST_FRAME_MS:
			batch = mini(MAX_BATCH, batch + 1)
		for it in cur:
			if not (it is GPUParticles3D or it is CPUParticles3D): # a particle copy keeps emitting until the teardown
				_show_item(it, false, hold)
	if hold and is_instance_valid(camera):
		camera.cull_mask = held_mask
		trace.append(["release", Engine.get_process_frames(), camera.cull_mask])
	var counts := {"materials": mats.size(), "shaders": reps.size(), "particles": parts.size(), "multimeshes": mms.size(),
		"items": items.size() + dropped.size(), "drawn": mini(i, items.size()),
		"skipped": items.size() - mini(i, items.size()) + dropped.size(), "ms": Time.get_ticks_msec() - t0}
	var nodes: Array = [st]
	nodes.append_array(holders)
	await _retire(tree, nodes, gpu, trace)
	counts.ms = Time.get_ticks_msec() - t0
	return counts


## Shows / hides one warm-up item: a world node in hold mode by its warm-up layer (it stays visible to the real camera
## mask), a particle copy by emitting, anything else by `visible`.
static func _show_item(it: Variant, on: bool, hold: bool) -> void:
	if not is_instance_valid(it):
		return
	if hold and it is GeometryInstance3D and not (it as Node).has_meta("perf_warmup_stage") and _is_world_rep(it):
		var g := it as GeometryInstance3D
		if on:
			g.set_meta("perf_warmup_layers", g.layers)
			g.layers |= WARM_LAYER
		elif g.has_meta("perf_warmup_layers"):
			g.layers = int(g.get_meta("perf_warmup_layers"))
			g.remove_meta("perf_warmup_layers")
		else:
			g.layers &= ~WARM_LAYER
		return
	if it is Node3D:
		(it as Node3D).visible = on
	elif it is CanvasItem:
		(it as CanvasItem).visible = on
	if it is GPUParticles3D:
		(it as GPUParticles3D).one_shot = true
		(it as GPUParticles3D).emitting = on
	elif it is CPUParticles3D:
		(it as CPUParticles3D).one_shot = true
		(it as CPUParticles3D).emitting = on


static func _is_world_rep(n: Node) -> bool:
	var p := n.get_parent()
	while p != null:
		if p.has_meta("perf_warmup_stage") or str(p.name) == HOLDER_NAME or p.has_meta("perf_warmup_holder"):
			return false
		p = p.get_parent()
	return true


static func _retire(tree: SceneTree, nodes: Array, gpu: bool, trace: Array) -> void:
	var live: Array[Node] = []
	for n in nodes:
		if n is Node and is_instance_valid(n):
			live.append(n as Node)
	for n in live: # 1. keep every resource they drew with — for good
		for r in collect_resources(n):
			keep(r)
	for n in live: # 2. hide them: from the next frame on nothing of theirs is drawn
		_hide(n)
	trace.append(["hidden", Engine.get_process_frames()])
	for i in POST_DRAWS: # 3. the frames that did draw them are through the renderer
		await _post_draw(tree, gpu)
		trace.append(["post_draw", Engine.get_process_frames()])
	for n in live: # 4. nodes first …
		if is_instance_valid(n):
			n.queue_free()
	trace.append(["nodes_freed", Engine.get_process_frames()])
	for i in SETTLE_FRAMES: # 5. … and a few frames for the renderer to drop their instances; resources stay kept
		await tree.process_frame
		trace.append(["settle", Engine.get_process_frames()])
	await _post_draw(tree, gpu)
	trace.append(["done", Engine.get_process_frames()])


static func _hide(n: Node) -> void:
	if n is Node3D:
		(n as Node3D).visible = false # hides the whole 3D subtree
	elif n is CanvasItem:
		(n as CanvasItem).visible = false
	for c in [n] + n.find_children("*", "", true, false):
		if c is CanvasLayer:
			(c as CanvasLayer).visible = false # a CanvasLayer does not inherit a Node3D parent's visibility
		elif c is GPUParticles3D:
			(c as GPUParticles3D).emitting = false
		elif c is CPUParticles3D:
			(c as CPUParticles3D).emitting = false


static func _post_draw(tree: SceneTree, gpu: bool) -> void:
	if gpu:
		await RenderingServer.frame_post_draw
	else:
		await tree.process_frame


## Debug builds only: `user://perf_warmup_slow_ms` (an integer) makes every warm-up frame that much longer.
static func _slow_ms() -> int:
	if not OS.is_debug_build() or not FileAccess.file_exists(SLOW_FILE):
		return 0
	return clampi(FileAccess.get_file_as_string(SLOW_FILE).strip_edges().to_int(), 0, 20000)
