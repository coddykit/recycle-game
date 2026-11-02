extends RefCounted
## A 3D scene in a SubViewportContainer renders at the PHONE's resolution (owner 2026-10-23: "Galaxy S25 Ultra, the
## sushi game started at the lowest resolution"). With the fleet's canvas_items stretch (a 720-wide canvas) a stretched
## SubViewportContainer sizes its SubViewport in CANVAS units, and Godot refuses any other size while `stretch` is on: on
## a 1440 × 3120 phone the 3D was drawn 720 px wide and blown up 2× — half the resolution on Medium, 0.35 on Low (scale
## 0.7 on top).
## attach() moves the SubViewport under a full-rect TextureRect in the same container (the container keeps its layout,
## size and `resized` in canvas units), sizes it to the container in PHYSICAL pixels and sets size_2d_override to the
## canvas size — the visible rect and Camera3D project / unproject stay in canvas units (markers, picking, set_view
## unchanged). The tier's scaling_3d_scale is then the only thing that lowers the 3D resolution; the 2D UI is never
## scaled (canvas_items). Only for containers that do not forward input (mouse_filter IGNORE): a SubViewport outside a
## container gets no pushed input — the games' gestures read the root's events.
## Source of truth: games/tools/perf (copied into each game as addons/coddy_perf by sync.sh).
##
##   NativeView.attach(map_view, scene_view)   # once, after scene_view was added to map_view

const MAX_PIXELS := 3200 * 3200 ## a safety bound on the render target (tablets / large desktop windows)
const HOLDER := "NativeView"


## Physical pixels per canvas unit for `c` (content scale × the canvas transform); 1 on a 1:1 window, never below 1.
static func pixel_scale(c: Control) -> float:
	if c == null or not c.is_inside_tree():
		return 1.0
	var s: Vector2 = c.get_viewport().get_final_transform().get_scale() * c.get_global_transform_with_canvas().get_scale()
	return maxf(1.0, minf(absf(s.x), absf(s.y)))


## The physical size for a canvas size at pixel scale `k` (bounded by MAX_PIXELS, never below the canvas size).
static func physical_size(canvas: Vector2i, k: float) -> Vector2i:
	var px := Vector2i(ceili(canvas.x * k), ceili(canvas.y * k))
	if px.x * px.y > MAX_PIXELS:
		var f := sqrt(float(MAX_PIXELS) / float(px.x * px.y))
		px = Vector2i(maxi(canvas.x, floori(px.x * f)), maxi(canvas.y, floori(px.y * f)))
	return px


## Sizes `vp` for `c` (attach() calls it on every resize). Returns the physical size used (ZERO while `c` is empty).
static func fit(c: Control, vp: SubViewport) -> Vector2i:
	if c == null or vp == null or not is_instance_valid(vp) or not is_instance_valid(c):
		return Vector2i.ZERO
	var canvas := Vector2i(ceili(c.size.x), ceili(c.size.y))
	if canvas.x < 1 or canvas.y < 1:
		return Vector2i.ZERO
	var px := physical_size(canvas, pixel_scale(c))
	vp.size_2d_override = canvas if px != canvas else Vector2i.ZERO
	vp.size_2d_override_stretch = px != canvas
	if vp.size != px:
		vp.size = px
	return px


## The holder attach() made in `c` (null before).
static func holder(c: Control) -> TextureRect:
	return c.get_node_or_null(HOLDER) as TextureRect if c != null else null


## Keeps `vp` (a child of the container `c`) at native resolution from now on. Returns the holder TextureRect.
static func attach(c: SubViewportContainer, vp: SubViewport) -> TextureRect:
	if c == null or vp == null or holder(c) != null:
		return holder(c)
	c.stretch = false
	var tr := TextureRect.new()
	tr.name = HOLDER
	tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tr.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	tr.stretch_mode = TextureRect.STRETCH_SCALE
	tr.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	if vp.get_parent() == c:
		c.remove_child(vp)
	c.add_child(tr)
	tr.add_child(vp)
	tr.texture = vp.get_texture()
	var refit := func() -> void: fit(c, vp)
	c.resized.connect(refit)
	var hook := func() -> void:
		if not c.get_viewport().size_changed.is_connected(refit):
			c.get_viewport().size_changed.connect(refit)
		refit.call()
	if c.is_inside_tree():
		hook.call()
	else:
		refit.call()
	c.tree_entered.connect(hook)
	c.tree_exiting.connect(func() -> void: # the window outlives the container: never leave a callback to a freed one
		if c.get_viewport() != null and c.get_viewport().size_changed.is_connected(refit):
			c.get_viewport().size_changed.disconnect(refit))
	return tr
