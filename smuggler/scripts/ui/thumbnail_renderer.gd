class_name ThumbnailRenderer
extends Node
const _MsaaGuard := preload("res://addons/coddy_perf/perf_profiles.gd")
## Renders each vehicle model once into a texture for UI cards (off-screen
## SubViewport). Falls back to an SVG icon on headless / failure.

static var instance: ThumbnailRenderer

var _cache := {}
var _waiting := {}   # key -> Array[Callable]
var _queue: Array = []
var _busy := false
var _vp: SubViewport
var _stage: Node3D
var _enabled := true


func _ready() -> void:
	instance = self
	_enabled = DisplayServer.get_name() != "headless"
	if not _enabled:
		return
	_vp = SubViewport.new()
	_vp.size = Vector2i(320, 220)
	_vp.transparent_bg = true
	_vp.own_world_3d = true
	_vp.msaa_3d = Viewport.MSAA_4X
	_MsaaGuard.guard_msaa_soon(_vp)   # Godot 4.7.2 Android GLES3 MSAA framebuffer leak (coddy_perf)
	_vp.render_target_update_mode = SubViewport.UPDATE_DISABLED
	add_child(_vp)
	var cam := Camera3D.new()
	cam.fov = 26
	_vp.add_child(cam)
	cam.position = Vector3(5.2, 4.0, -6.4)
	cam.look_at_from_position(cam.position, Vector3(0, 0.55, 0), Vector3.UP)
	var light := DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-50, 140, 0)
	light.light_energy = 1.1
	_vp.add_child(light)
	var env := WorldEnvironment.new()
	var e := Environment.new()
	e.background_mode = Environment.BG_CLEAR_COLOR
	e.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	e.ambient_light_color = Color("d6e2f5")
	e.ambient_light_energy = 0.6
	env.environment = e
	_vp.add_child(env)
	_stage = Node3D.new()
	_vp.add_child(_stage)


func request(def_id: String, paint: String, cb: Callable) -> Texture2D:
	var key := def_id + "_" + paint
	if _cache.has(key):
		return _cache[key]
	if not _enabled:
		return null
	if not _waiting.has(key):
		_waiting[key] = []
		_queue.append([key, def_id, paint])
	_waiting[key].append(cb)
	if not _busy:
		_process_queue()
	return null


func _process_queue() -> void:
	_busy = true
	while _queue.size() > 0:
		var item: Array = _queue.pop_front()
		for c in _stage.get_children():
			c.queue_free()
		var m := Models.instance(item[1], Models.paint_material(Data.color(item[2])))
		_stage.add_child(m)
		var meta := Data.meta(item[1])
		var length := float(meta.size[2])
		var s := 2.2 / maxf(1.4, length) * 1.35
		m.scale = Vector3.ONE * s
		m.rotation.y = PI
		_vp.render_target_update_mode = SubViewport.UPDATE_ONCE
		await RenderingServer.frame_post_draw
		if not is_inside_tree(): return
		await RenderingServer.frame_post_draw
		if not is_inside_tree(): return
		var img := _vp.get_texture().get_image()
		var tex: Texture2D = null
		if img and not img.is_empty():
			tex = ImageTexture.create_from_image(img)
		else:
			push_warning("Thumbnail render failed for " + str(item[1]))
		_cache[item[0]] = tex
		for cb in _waiting.get(item[0], []):
			if (cb as Callable).is_valid():
				(cb as Callable).call(tex)
		_waiting.erase(item[0])
	_busy = false


## A TextureRect that shows the vehicle thumbnail as soon as it's rendered.
static func rect(def_id: String, paint: String, size: Vector2) -> TextureRect:
	var r := TextureRect.new()
	r.custom_minimum_size = size
	r.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	r.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	r.texture = UIKit.tex("truck")
	if is_instance_valid(instance):
		var wr: WeakRef = weakref(r)
		var t := instance.request(def_id, paint, func(tx: Texture2D):
			var rr: TextureRect = wr.get_ref()
			if rr and tx:
				rr.texture = tx)
		if t:
			r.texture = t
	return r


func _exit_tree() -> void:
	if instance == self:
		instance = null
	_waiting.clear()
	_queue.clear()
	_cache.clear()
