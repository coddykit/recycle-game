## W63 Low-profile shading (fleet rule from doner-night 57d4e6f320 / pest-patrol f4a165b390, Patron #69): on Low every
## lit 3D surface is lit per vertex with no specular; Medium / High keep the game's per-pixel PBR untouched.
## Source of truth: games/tools/perf/low_shading.gd — copied (never edited) to <game>/addons/coddy_low_shading/.
##
##   const LowShading := preload("res://addons/coddy_low_shading/low_shading.gd")
##   LowShading.install(get_tree())          # once; registers every GeometryInstance3D that enters the tree
##   LowShading.set_cheap(quality == low)    # whenever the graphics tier is applied
##
## StandardMaterial3D / ORMMaterial3D: shading_mode PER_VERTEX + specular_mode DISABLED (originals kept in meta).
## ShaderMaterial (spatial): the material's shader is swapped for a cached Low variant whose render_mode adds
## vertex_lighting + specular_disabled; parameters live on the material and survive the swap. Skipped: unshaded
## surfaces, shaders with a custom light() (not run under vertex lighting), canvas / particle / sky / fog shaders.
##
## Optional per-game knobs (defaults = the behaviour above): `keep` — a Callable(BaseMaterial3D) -> bool; true leaves
## that material as authored (e.g. a game's own glow test); `shaders = false` leaves every ShaderMaterial alone.
extends RefCounted

static var cheap := false
static var keep := Callable()
static var shaders := true
static var _inst: RefCounted
static var _tree: SceneTree
static var _base := {}        # material instance id -> WeakRef(BaseMaterial3D)
static var _shader_mats := {} # material instance id -> WeakRef(ShaderMaterial)
static var _variants := {}    # original Shader instance id -> Low Shader, or null when it must stay as it is
static var _pending: Array = []
static var _flush_queued := false


static func install(tree: SceneTree) -> void:
	if _tree == tree:
		return
	_tree = tree
	if _inst == null:
		_inst = (load("res://addons/coddy_low_shading/low_shading.gd") as GDScript).new()
	tree.node_added.connect(_inst._on_node_added)
	register(tree.root)


## Switches every registered material (and anything in the tree not seen yet) to the Low look or back.
static func set_cheap(on: bool) -> void:
	cheap = on
	if _tree != null:
		register(_tree.root)
	for id in _base.keys():
		var m: BaseMaterial3D = _base[id].get_ref()
		if m == null:
			_base.erase(id)
		else:
			_shade_base(m)
	for id in _shader_mats.keys():
		var m: ShaderMaterial = _shader_mats[id].get_ref()
		if m == null:
			_shader_mats.erase(id)
		else:
			_shade_shader(m)


## Registers (and shades for the live tier) every material under `root`.
static func register(root: Node) -> void:
	if root == null:
		return
	_register_node(root)
	for c in root.get_children(true):
		register(c)


static func _register_node(n: Node) -> void:
	if n is MeshInstance3D:
		var mi := n as MeshInstance3D
		for i in mi.get_surface_override_material_count():
			_register_material(mi.get_surface_override_material(i))
		if mi.mesh != null:
			for i in mi.mesh.get_surface_count():
				_register_material(mi.mesh.surface_get_material(i))
	elif n is MultiMeshInstance3D:
		var mm := (n as MultiMeshInstance3D).multimesh
		if mm != null and mm.mesh != null:
			for i in mm.mesh.get_surface_count():
				_register_material(mm.mesh.surface_get_material(i))
	elif n is CSGShape3D and n.get("material") != null:
		_register_material(n.get("material"))
	if n is GeometryInstance3D:
		_register_material((n as GeometryInstance3D).material_override)
		_register_material((n as GeometryInstance3D).material_overlay)


static func _register_material(m: Material) -> void:
	var depth := 0
	while m != null and depth < 4:
		var id := m.get_instance_id()
		if m is BaseMaterial3D:
			var b := m as BaseMaterial3D
			if not _base.has(id) and (b.has_meta("low_shading_orig") or b.shading_mode != BaseMaterial3D.SHADING_MODE_UNSHADED) \
					and not (keep.is_valid() and keep.call(b)):
				if not b.has_meta("low_shading_orig"):
					b.set_meta("low_shading_orig", [b.shading_mode, b.specular_mode])
				_base[id] = weakref(b)
				_shade_base(b)
		elif m is ShaderMaterial and shaders and not _shader_mats.has(id):
			var s := m as ShaderMaterial
			var orig: Shader = s.get_meta("low_shading_orig", s.shader)
			if orig != null and _variant(orig) != null:
				s.set_meta("low_shading_orig", orig)
				_shader_mats[id] = weakref(s)
				_shade_shader(s)
		m = m.next_pass
		depth += 1


static func _shade_base(b: BaseMaterial3D) -> void:
	var orig: Array = b.get_meta("low_shading_orig")
	var mode: int = BaseMaterial3D.SHADING_MODE_PER_VERTEX if cheap else int(orig[0])
	var spec: int = BaseMaterial3D.SPECULAR_DISABLED if cheap else int(orig[1])
	if b.shading_mode != mode:
		b.shading_mode = mode
	if b.specular_mode != spec:
		b.specular_mode = spec


static func _shade_shader(s: ShaderMaterial) -> void:
	var orig: Shader = s.get_meta("low_shading_orig")
	var want: Shader = _variant(orig) if cheap else orig
	if want != null and s.shader != want:
		s.shader = want


## The cached Low variant of a spatial shader (null = leave it: not spatial, unshaded, custom light(), already per
## vertex, or an include-only / empty shader).
static func _variant(sh: Shader) -> Shader:
	var id := sh.get_instance_id()
	if _variants.has(id):
		return _variants[id]
	var low: Shader = null
	var code := low_code(sh.code)
	if code != "":
		low = Shader.new()
		low.code = code
		low.resource_name = sh.resource_name + " (Low)"
	_variants[id] = low
	return low


## The render_mode rewrite (public for tests): "" when the shader must stay as it is.
static func low_code(code: String) -> String:
	var head := RegEx.create_from_string("shader_type\\s+(\\w+)\\s*;")
	var t := head.search(code)
	if t == null or t.get_string(1) != "spatial":
		return ""
	if RegEx.create_from_string("void\\s+light\\s*\\(").search(code) != null:
		return ""
	var rm := RegEx.create_from_string("render_mode\\s+([^;]*);").search(code)
	if rm == null:
		return code.substr(0, t.get_end()) + "\nrender_mode vertex_lighting, specular_disabled;" + code.substr(t.get_end())
	var modes := []
	for x in rm.get_string(1).split(","):
		modes.append(x.strip_edges())
	if "unshaded" in modes or "vertex_lighting" in modes:
		return ""
	for x in ["vertex_lighting", "specular_disabled"]:
		if not x in modes:
			modes.append(x)
	for x in ["specular_schlick_ggx", "specular_toon"]:
		modes.erase(x)
	return code.substr(0, rm.get_start()) + "render_mode " + ", ".join(modes) + ";" + code.substr(rm.get_end())


## How many registered materials are lit per pixel / per vertex now (tests).
static func counts() -> Dictionary:
	var out := {"base": 0, "base_cheap": 0, "shader": 0, "shader_cheap": 0}
	for id in _base.keys():
		var b: BaseMaterial3D = _base[id].get_ref()
		if b != null:
			out.base += 1
			if b.shading_mode == BaseMaterial3D.SHADING_MODE_PER_VERTEX and b.specular_mode == BaseMaterial3D.SPECULAR_DISABLED:
				out.base_cheap += 1
	for id in _shader_mats.keys():
		var s: ShaderMaterial = _shader_mats[id].get_ref()
		if s != null:
			out.shader += 1
			if s.shader != s.get_meta("low_shading_orig"):
				out.shader_cheap += 1
	return out


func _on_node_added(n: Node) -> void:
	if not n is GeometryInstance3D:
		return
	_pending.append(weakref(n))
	if not _flush_queued:
		_flush_queued = true
		_flush.call_deferred()


## Deferred: a node's mesh / materials are usually set right after add_child.
func _flush() -> void:
	_flush_queued = false
	var batch := _pending
	_pending = []
	for w in batch:
		var n: Node = w.get_ref()
		if n != null and n.is_inside_tree():
			_register_node(n)
