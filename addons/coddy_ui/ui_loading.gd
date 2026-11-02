extends Control
## coddy_ui loading screen (the game's own splash, never Godot's): the game's key art full-bleed, a soft shade on the
## text side, the game's name, a 12 px rounded progress pill and a translated status line. Same API as the factory
## LoadingCurtain (title, progress, status, set_progress), so a game's curtain can simply extend this script:
##   extends "res://addons/coddy_ui/ui_loading.gd"
##   func _init(): art = preload("res://assets/ui/loading.png"); game_title = "Canal\nControl"
## Portrait: text at the bottom over a bottom-up shade; landscape: text on the left over a left-to-right shade.

const Tokens = preload("ui_tokens.gd")

var art: Texture2D = null
var game_title := ""
var progress: ProgressBar
var status: Label
var title: Label
var _completed := 0.0
var _total := 0.0
var _status := ""
var _shade: TextureRect
var _col: VBoxContainer


func _ready() -> void:
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	if theme == null:
		theme = kit.theme()   # the loading screen often runs before the HUD (Boot scene): it carries the kit theme itself
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	var bg := ColorRect.new()
	bg.color = kit.c("background")
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(bg)
	if art != null:
		var pic := TextureRect.new()
		pic.texture = art
		pic.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		pic.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		pic.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		pic.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(pic)
	_shade = TextureRect.new()
	_shade.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_shade.stretch_mode = TextureRect.STRETCH_SCALE
	_shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_shade)
	_col = VBoxContainer.new()
	_col.add_theme_constant_override("separation", 12)
	_col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_col)
	title = kit.label(game_title, "Display", false)   # the name breaks where the game puts "\n"
	title.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.35))
	title.add_theme_constant_override("shadow_offset_y", 3)
	_col.add_child(title)
	var spacer := Control.new()
	spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_col.add_child(spacer)
	progress = kit.progress(0.0, 1.0)
	_col.add_child(progress)
	status = kit.label("", "Body", true)
	status.add_theme_color_override("font_color", kit.c("on_surface"))
	_col.add_child(status)
	resized.connect(_layout)
	_layout()
	set_progress(_completed, _total, _status)


func _layout() -> void:
	if _col == null:
		return
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	var portrait: bool = size.y > size.x
	var g := Gradient.new()
	var base: Color = kit.c("background")
	g.colors = PackedColorArray([Color(base, 0.97), Color(base, 0.55), Color(base, 0.0)])
	g.offsets = PackedFloat32Array([0.0, 0.45, 1.0])
	var t := GradientTexture2D.new()
	t.gradient = g
	t.width = 64
	t.height = 64
	t.fill_from = Vector2(0, 1) if portrait else Vector2(0, 0)
	t.fill_to = Vector2(0, 0.35) if portrait else Vector2(0.62, 0)
	_shade.texture = t
	var fs := clampf(minf(size.x, size.y) * 0.1, 30.0, 96.0)
	title.add_theme_font_size_override("font_size", int(fs))
	status.add_theme_font_size_override("font_size", int(clampf(minf(size.x, size.y) * 0.035, 21.0, 28.0)))
	_col.size = Vector2.ZERO
	kit.settle(_col)   # the column's cached minimum still holds the old font sizes
	if portrait:
		_col.position = Vector2(size.x * 0.07, size.y * 0.52)
		_col.size = Vector2(size.x * 0.86, size.y * 0.40)
	else:
		_col.position = Vector2(size.x * 0.06, size.y * 0.12)
		_col.size = Vector2(size.x * 0.38, size.y * 0.80)
	kit.settle(_col)


func set_progress(completed: float, total: float, localized_status: String = "") -> void:
	_completed = completed
	_total = total
	_status = localized_status
	if progress == null:
		return
	progress.value = 0.0 if total <= 0.0 else clampf(completed / total, 0.0, 1.0)
	status.text = localized_status
	# (kept visible when empty: it holds its place and stays laid out)
