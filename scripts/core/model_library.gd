extends Node
## Loads Blender-exported GLB models, caches meshes and shares materials.
## Every model renders with ONE palette ShaderMaterial (or a cached paint variant).

const MODEL_DIR := "res://assets/models/"
## W66 (10-29): Medium / High variants of the most-seen weak models (tools/blender/build_w66.py). Same object names,
## pivot and bounding box as the canonical file (data/model_meta.json describes both). `hd` follows the tier
## (World sets it before the world is built and on every tier change); Low never loads a file from hd/.
const HD_DIR := "res://assets/models/hd/"
var hd := false
var _hd_names := {}   # name -> an hd/ file exists

var palette_tex: Texture2D
var base_material: ShaderMaterial
var locked_material: ShaderMaterial
var terrain_material: ShaderMaterial
var terrain_locked_material: ShaderMaterial
var water_material: ShaderMaterial

var _scenes := {}
var _meshes := {}
var _paints := {}
var _flat := {}


func _ready() -> void:
	palette_tex = load("res://assets/textures/palette.png")
	base_material = ShaderMaterial.new()
	base_material.shader = load("res://assets/shaders/palette.gdshader")
	base_material.set_shader_parameter("palette_tex", palette_tex)
	base_material.set_shader_parameter("paint_color", Color.WHITE)
	locked_material = base_material.duplicate()
	locked_material.set_shader_parameter("desaturate", 0.85)
	locked_material.set_shader_parameter("brightness", 0.72)
	terrain_material = ShaderMaterial.new()
	terrain_material.shader = load("res://assets/shaders/terrain.gdshader")
	terrain_locked_material = terrain_material.duplicate()
	terrain_locked_material.set_shader_parameter("desaturate", 0.35)
	terrain_locked_material.set_shader_parameter("brightness", 0.95)
	water_material = ShaderMaterial.new()
	water_material.shader = load("res://assets/shaders/water.gdshader")


## An HD file exists for `name` (ResourceLoader: exported builds keep the imported GLB, not hd/manifest.json).
func has_hd(name: String) -> bool:
	if not _hd_names.has(name):
		_hd_names[name] = ResourceLoader.exists(HD_DIR + name + ".glb")
	return _hd_names[name]


## The file `name` resolves to on the current tier.
func path_of(name: String) -> String:
	return (HD_DIR if hd and has_hd(name) else MODEL_DIR) + name + ".glb"


func scene(name: String) -> PackedScene:
	var path := path_of(name)
	if _scenes.has(path):
		return _scenes[path]
	var s: PackedScene = null
	if ResourceLoader.exists(path):
		s = load(path) as PackedScene
	if s == null:
		push_warning("Model missing: " + path)
	_scenes[path] = s
	return s


## Instantiates a model with the shared palette material applied.
func instance(name: String, material: Material = null) -> Node3D:
	var s := scene(name)
	var node: Node3D
	if s:
		node = s.instantiate() as Node3D
	if node == null:
		node = _fallback(name)
	apply_material(node, material if material else base_material)
	return node


func apply_material(node: Node, material: Material) -> void:
	if node is MeshInstance3D:
		(node as MeshInstance3D).material_override = material
		(node as MeshInstance3D).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	for c in node.get_children():
		apply_material(c, material)


## Single merged mesh for MultiMesh use (props are exported as one object).
func mesh(name: String) -> Mesh:
	var key := path_of(name)
	if _meshes.has(key):
		return _meshes[key]
	var m: Mesh = null
	var s := scene(name)
	if s:
		var inst := s.instantiate()
		var mi := _find_mesh(inst)
		if mi:
			m = mi.mesh
		inst.free()
	if m == null:
		var b := BoxMesh.new()
		b.size = Vector3(0.8, 0.8, 0.8)
		m = b
	_meshes[key] = m
	return m


## W66: puts the current tier's meshes of `name` into an instance made by instance(name) — in place (same nodes,
## materials, transforms, wheels), matched by node path. Returns how many meshes changed.
func swap_meshes(node: Node, name: String) -> int:
	var s := scene(name)
	if s == null or node == null:
		return 0
	var src := s.instantiate()
	var n := 0
	for mi in node.find_children("*", "MeshInstance3D", true, false):
		var other := src.get_node_or_null(node.get_path_to(mi)) as MeshInstance3D
		if other != null and other.mesh != (mi as MeshInstance3D).mesh:
			(mi as MeshInstance3D).mesh = other.mesh
			n += 1
	src.free()
	return n


func _find_mesh(n: Node) -> MeshInstance3D:
	if n is MeshInstance3D:
		return n
	for c in n.get_children():
		var r := _find_mesh(c)
		if r:
			return r
	return null


func _fallback(name: String) -> Node3D:
	var root := Node3D.new()
	root.name = name
	var mi := MeshInstance3D.new()
	var b := BoxMesh.new()
	var m: Dictionary = Data.meta(name)
	var sz: Array = m.size
	b.size = Vector3(sz[0], sz[1], sz[2])
	mi.mesh = b
	mi.position.y = float(sz[1]) * 0.5
	root.add_child(mi)
	return root


func paint_material(color: Color) -> ShaderMaterial:
	var key := color.to_html(false)
	if _paints.has(key):
		return _paints[key]
	var m: ShaderMaterial = base_material.duplicate()
	m.set_shader_parameter("paint_color", color)
	_paints[key] = m
	return m


## Unshaded glowing colour (markers, beacons).
func flat_material(color: Color, pulse := 0.0) -> ShaderMaterial:
	var key := "%s_%.2f" % [color.to_html(), pulse]
	if _flat.has(key):
		return _flat[key]
	var m := ShaderMaterial.new()
	m.shader = load("res://assets/shaders/unshaded_color.gdshader")
	m.set_shader_parameter("color", color)
	m.set_shader_parameter("pulse", pulse)
	_flat[key] = m
	return m
