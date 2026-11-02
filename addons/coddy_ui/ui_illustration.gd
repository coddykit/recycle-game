extends Control
## coddy_ui illustration-lite for onboarding pages and empty states: a soft radial glow, two thin rings, a big brand
## disc with the page's icon, and two small satellite tiles. Pure vector (draw_circle + cached icon textures), redrawn
## only while a page enters: the composition slides in from the side the page comes from and fades up, the disc
## "breathes in", the satellites swing in on their orbit and pop (one tween, then nothing). A game can pass a real picture
## instead (`art`: Texture2D) — e.g. a hero render of the game's own scene: it fills the area (cover: aspect kept, the
## overflow cropped) with the area's rounded corners, drawn as one textured polygon (no shader, no clip pass); the icon
## disc and satellites are not drawn then. It fades in and settles from 106 %. No picture = the icon composition.

const Tokens = preload("ui_tokens.gd")
const Icons = preload("ui_icons.gd")

var icon_name := "sparkles"
var satellites: Array = ["coins", "check"]   ## two Lucide names (or fewer)
var tone := "accent"                          ## glow / ring colour role
var art: Texture2D = null
var _pop := 1.0:
	set(v):
		_pop = v
		queue_redraw()
var _enter := 1.0:   ## 0 → 1 while a page comes in: slide + fade of the composition
	set(v):
		_enter = v
		queue_redraw()
var _orbit := 1.0:   ## 0 → 1 a beat later: the satellites swing in and pop
	set(v):
		_orbit = v
		queue_redraw()
var _dir := 1.0      ## +1 = the page comes from the right (Next), -1 = from the left (back)
var _tw: Tween
var corner_radius := Vector4.ZERO   ## top-left, top-right, bottom-right, bottom-left (match the card it sits in)
var _zoom := 1.0:    ## picture scale while it settles (1.06 → 1)
	set(v):
		_zoom = v
		queue_redraw()
var _bg: StyleBoxFlat


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	clip_contents = true


## direction: +1 the page comes from the right (Next; mirrored in RTL), -1 from the left (back), 0 = no slide (open).
func show_page(icon: String, sats: Array = [], page_tone: String = "", picture: Texture2D = null, animate: bool = true, direction: int = 0) -> void:
	icon_name = icon
	satellites = sats
	if page_tone != "":
		tone = page_tone
	art = picture
	if _tw != null:
		_tw.kill()
		_tw = null
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	if animate and is_inside_tree() and not kit.reduce_motion:
		_dir = float(direction) * (-1.0 if is_layout_rtl() else 1.0)
		_pop = 0.86
		_enter = 0.0
		_orbit = 0.0
		_zoom = 1.06
		_tw = create_tween().set_parallel(true)
		_tw.tween_property(self, "_enter", 1.0, Tokens.MOTION.slow).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
		_tw.tween_property(self, "_pop", 1.0, Tokens.MOTION.slow).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_BACK).set_delay(0.04)
		_tw.tween_property(self, "_orbit", 1.0, Tokens.MOTION.slow + 0.08).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_BACK).set_delay(Tokens.MOTION.fast * 0.75)
		_tw.tween_property(self, "_zoom", 1.0, Tokens.MOTION.slow * 1.5).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	else:
		_pop = 1.0
		_enter = 1.0
		_orbit = 1.0
		_zoom = 1.0
	queue_redraw()


## True while a picture replaces the icon composition (tests / hosts).
func has_picture() -> bool:
	return art != null


## True while a page is still coming in (tests: transitions end, nothing keeps animating).
func animating() -> bool:
	return _tw != null and _tw.is_valid() and _tw.is_running()


func _draw() -> void:
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	if _bg == null:
		_bg = StyleBoxFlat.new()
		_bg.anti_aliasing = true
	_bg.bg_color = kit.c("surface_sunken").lerp(kit.c(tone), 0.10)
	_bg.corner_radius_top_left = int(corner_radius.x)
	_bg.corner_radius_top_right = int(corner_radius.y)
	_bg.corner_radius_bottom_right = int(corner_radius.z)
	_bg.corner_radius_bottom_left = int(corner_radius.w)
	draw_style_box(_bg, Rect2(Vector2.ZERO, size))
	if art != null:
		_draw_picture()
		return
	var a := _enter                                   # the composition fades up while it slides in
	var c := size / 2.0 + Vector2(_dir * (1.0 - _enter) * minf(size.x, size.y) * 0.16, 0.0)
	var glow_r := minf(size.x, size.y) * 0.62
	for i in 10:   # soft radial glow: 10 stacked translucent discs (no texture, no shader)
		var k := 1.0 - i / 10.0
		draw_circle(c, glow_r * k * lerpf(0.9, 1.0, _enter), Color(kit.c(tone), 0.035 * a), true, -1.0, true)
	var r0 := roundf(minf(size.x, size.y) * 0.21)
	var r := r0 * _pop
	var ring: Color = kit.c(tone)
	draw_arc(c, r * 2.25 * lerpf(0.92, 1.0, _orbit), 0, TAU, 96, Color(ring, 0.20 * a), 2.0, true)
	draw_arc(c, r * 1.62, 0, TAU, 96, Color(ring, 0.32 * a), 2.0, true)
	for i in 6:
		draw_circle(c + Vector2(0, r * 0.10), r + 14 - i * 2.4, Color(0, 0, 0, 0.05 * a), true, -1.0, true)
	draw_circle(c, r, Color(kit.c("primary"), a), true, -1.0, true)
	var ipx := maxi(16, int(r0 * 1.04))   # texture size from the resting radius: the pop tween never rasterises new sizes
	var tex := Icons.texture(icon_name, ipx, kit.c("on_primary"), 1.75)
	var dpx := ipx * _pop
	draw_texture_rect(tex, Rect2(c - Vector2(dpx, dpx) / 2.0, Vector2(dpx, dpx)), false, Color(1, 1, 1, a))
	# satellites ride their orbit in (a quarter turn against the slide) and pop to size
	var swing := (1.0 - _orbit) * -0.9 * (_dir if _dir != 0.0 else 1.0)
	var spots := [c + Vector2(-r * 1.75, -r * 0.95).rotated(swing), c + Vector2(r * 1.62, r * 1.05).rotated(swing)]
	var kinds := ["accent", "warning"]
	var sa := clampf(_orbit * 1.6, 0.0, 1.0) * a
	for i in mini(satellites.size(), 2):
		var sr := r * 0.40 * maxf(0.0, _orbit)
		if sr < 1.0:
			continue
		draw_circle(spots[i] + Vector2(0, 3), sr + 3, Color(0, 0, 0, 0.18 * sa), true, -1.0, true)
		draw_circle(spots[i], sr, Color(kit.c("surface_high"), sa), true, -1.0, true)
		var spx := maxi(12, int(r0 * 0.40))
		var st := Icons.texture(str(satellites[i]), spx, kit.c(kinds[i]), 2.0)
		var sd := spx * _pop * maxf(0.0, _orbit)
		draw_texture_rect(st, Rect2(spots[i] - Vector2(sd, sd) / 2.0, Vector2(sd, sd)), false, Color(1, 1, 1, sa))


## The picture as a rounded cover: a polygon along the rounded rect, UVs from the cover mapping (+ the settle zoom).
func _draw_picture() -> void:
	var t := art.get_size()
	if t.x <= 0.0 or t.y <= 0.0 or size.x <= 0.0 or size.y <= 0.0:
		return
	var k := maxf(size.x / t.x, size.y / t.y) * maxf(1.0, _zoom)   # cover: the smaller overflow side is cropped
	var pts := PackedVector2Array()
	var uvs := PackedVector2Array()
	var radii := [corner_radius.x, corner_radius.y, corner_radius.z, corner_radius.w]
	var centres := [Vector2(1, 1), Vector2(-1, 1), Vector2(-1, -1), Vector2(1, -1)]
	var starts := [PI, PI * 1.5, 0.0, PI * 0.5]
	var corners := [Vector2.ZERO, Vector2(size.x, 0.0), size, Vector2(0.0, size.y)]
	for i in 4:
		var r := minf(float(radii[i]), minf(size.x, size.y) / 2.0)
		var c: Vector2 = corners[i] + (centres[i] as Vector2) * r
		if r <= 1.0:
			pts.append(corners[i])
			continue
		var n := 8
		for j in n + 1:
			var ang: float = starts[i] + (PI / 2.0) * float(j) / float(n)
			pts.append(c + Vector2(cos(ang), sin(ang)) * r)
	for p in pts:
		uvs.append(Vector2(0.5, 0.5) + (p - size / 2.0) / (t * k))
	draw_colored_polygon(pts, Color(1, 1, 1, _enter), uvs, art)
