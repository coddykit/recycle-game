extends RefCounted
## coddy_ui icons: Lucide line icons (ISC, icons/LICENSE-lucide.txt) rasterised once from SVG text at the size and colour
## asked for (Image.load_svg_from_string, no import step, no shader) and cached for the app lifetime. Textures are drawn at
## SUPERSAMPLE x the canvas size so they stay sharp on phones whose screen is larger than the 720 px baseline; show them
## with UiIcons.rect() (expand + keep aspect) or as a Button icon with `icon_max_width`.
## Also builds the kit's other small pictures the same way: icon tiles (rounded square / circle behind an icon) and the
## toggle switch (track + knob) for CheckButton / rows.

const Data = preload("ui_icons_data.gd")
const SUPERSAMPLE := 2.0

static var _cache := {}
static var _hooked := false
static var _closing := false     ## the tree is ending: pictures made now are not cached (they die with their users)


## Static caches would outlive the renderer at shutdown (headless "RID leaked" errors): drop them when the tree ends.
static func _hook() -> void:
	if _hooked:
		return
	var tree := Engine.get_main_loop() as SceneTree
	if tree == null or tree.root == null:
		return
	_hooked = true
	tree.root.tree_exiting.connect(func() -> void:
		_closing = true
		var kit = load("res://addons/coddy_ui/ui_kit.gd")
		kit.clear_caches())


static func clear_cache() -> void:
	_cache.clear()


static func has(name: String) -> bool:
	return Data.ICONS.has(name)


static func names() -> Array:
	return Data.ICONS.keys()


static func _hex(c: Color) -> String:
	return "#" + c.to_html(false)


static func _tex(key: String, svg: String, scale: float = SUPERSAMPLE) -> Texture2D:
	if _cache.has(key):
		return _cache[key]
	_hook()
	var img := Image.new()
	var err := img.load_svg_from_string(svg, scale)
	if err != OK:
		push_warning("coddy_ui: svg failed " + key)
		img = Image.create(2, 2, false, Image.FORMAT_RGBA8)
	img.generate_mipmaps()
	var t := ImageTexture.create_from_image(img)
	if not _closing:
		_cache[key] = t
	return t


## A stroked icon of `px` canvas px (texture is px * SUPERSAMPLE). Unknown names draw an empty square (and warn).
static func texture(name: String, px: int, color: Color = Color.WHITE, stroke: float = 2.0) -> Texture2D:
	var key := "i|%s|%d|%s|%.2f" % [name, px, color.to_html(), stroke]
	if _cache.has(key):
		return _cache[key]
	if not Data.ICONS.has(name):
		push_warning("coddy_ui: unknown icon " + name)
	var body: String = Data.ICONS.get(name, "<rect x=\"4\" y=\"4\" width=\"16\" height=\"16\" />")
	var svg := "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"%d\" height=\"%d\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"%s\" stroke-opacity=\"%.3f\" stroke-width=\"%.2f\" stroke-linecap=\"round\" stroke-linejoin=\"round\">%s</svg>" % [px, px, _hex(color), color.a, stroke, body]
	return _tex(key, svg)


## The same icon at 1x: for theme slots Godot draws at texture size (OptionButton arrow, PopupMenu checks).
static func texture_1x(name: String, px: int, color: Color = Color.WHITE, stroke: float = 2.0) -> Texture2D:
	var key := "i1|%s|%d|%s|%.2f" % [name, px, color.to_html(), stroke]
	if _cache.has(key):
		return _cache[key]
	var body: String = Data.ICONS.get(name, "")
	var svg := "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"%d\" height=\"%d\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"%s\" stroke-opacity=\"%.3f\" stroke-width=\"%.2f\" stroke-linecap=\"round\" stroke-linejoin=\"round\">%s</svg>" % [px, px, _hex(color), color.a, stroke, body]
	return _tex(key, svg, 1.0)


## An icon tile: a rounded square (or circle when `round`) filled with `bg`, the icon in `fg` at ~52 % of the tile.
static func tile(name: String, px: int, fg: Color, bg: Color, round: bool = false, radius: int = -1) -> Texture2D:
	var key := "t|%s|%d|%s|%s|%s|%d" % [name, px, fg.to_html(), bg.to_html(), round, radius]
	if _cache.has(key):
		return _cache[key]
	var r := px / 2.0 if round else (float(radius) if radius >= 0 else px * 0.24)
	var body: String = Data.ICONS.get(name, "")
	var s := px * 0.52 / 24.0
	var o := (px - 24.0 * s) / 2.0
	var svg := "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"%d\" height=\"%d\" viewBox=\"0 0 %d %d\"><rect width=\"%d\" height=\"%d\" rx=\"%.1f\" fill=\"%s\" fill-opacity=\"%.3f\"/><g transform=\"translate(%.2f %.2f) scale(%.4f)\" fill=\"none\" stroke=\"%s\" stroke-opacity=\"%.3f\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\">%s</g></svg>" % [px, px, px, px, px, px, r, _hex(bg), bg.a, o, o, s, _hex(fg), fg.a, body]
	return _tex(key, svg)


## The toggle switch picture (92 x 52 canvas px at the baseline): track + knob. `on` = brand fill, knob on the right.
static func switch(on: bool, track_on: Color, knob_on: Color, track_off: Color, outline_off: Color, knob_off: Color, disabled: bool = false, scale: float = SUPERSAMPLE) -> Texture2D:
	var key := "s|%s|%s|%s|%s|%s|%s|%s|%.1f" % [on, track_on.to_html(), knob_on.to_html(), track_off.to_html(), outline_off.to_html(), knob_off.to_html(), disabled, scale]
	if _cache.has(key):
		return _cache[key]
	var w := 92
	var h := 52
	var track := track_on if on else track_off
	var stroke := track_on if on else outline_off
	var knob := knob_on if on else knob_off
	var alpha := 0.45 if disabled else 1.0
	var cx := w - 26.0 if on else 26.0
	var svg := "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"%d\" height=\"%d\" viewBox=\"0 0 %d %d\" opacity=\"%.2f\"><rect x=\"1\" y=\"1\" width=\"%d\" height=\"%d\" rx=\"%d\" fill=\"%s\" stroke=\"%s\" stroke-width=\"2\"/><circle cx=\"%.1f\" cy=\"26\" r=\"18\" fill=\"%s\"/></svg>" % [w, h, w, h, alpha, w - 2, h - 2, (h - 2) / 2, _hex(track), _hex(stroke), cx, _hex(knob)]
	return _tex(key, svg, scale)


## The same switch at 1x: CheckButton draws its check icons at texture size (theme use).
static func switch_1x(on: bool, track_on: Color, knob_on: Color, track_off: Color, outline_off: Color, knob_off: Color, disabled: bool = false) -> Texture2D:
	return switch(on, track_on, knob_on, track_off, outline_off, knob_off, disabled, 1.0)


## Slider knob (40 px, 1x: sliders draw the grabber unscaled): filled disc with a brand ring.
static func knob(fill: Color, ring: Color) -> Texture2D:
	var key := "k|%s|%s" % [fill.to_html(), ring.to_html()]
	var svg := "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"40\" viewBox=\"0 0 40 40\"><circle cx=\"20\" cy=\"20\" r=\"17\" fill=\"%s\" stroke=\"%s\" stroke-width=\"5\"/></svg>" % [_hex(fill), _hex(ring)]
	return _tex(key, svg, 1.0)


## A TextureRect showing `name` at px x px (sharp: the texture is supersampled and shrunk to the rect).
static func rect(name: String, px: int, color: Color = Color.WHITE, stroke: float = 2.0) -> TextureRect:
	var r := TextureRect.new()
	r.texture = texture(name, px, color, stroke)
	r.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	r.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	r.custom_minimum_size = Vector2(px, px)
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	r.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	return r


static func tile_rect(name: String, px: int, fg: Color, bg: Color, round: bool = false) -> TextureRect:
	var r := TextureRect.new()
	r.texture = tile(name, px, fg, bg, round)
	r.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	r.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	r.custom_minimum_size = Vector2(px, px)
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	r.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	return r


## Tests / memory reports: how many pictures were rasterised.
static func cached_count() -> int:
	return _cache.size()
