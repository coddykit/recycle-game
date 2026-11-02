class_name TitleCard
extends Control
const Kit = preload("res://addons/coddy_ui/ui_kit.gd")

signal dismissed
## First impression: logo over the living diorama. Tap to play.

var _t := 0.0
var _tap: Button
var _done := false


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	var grad := TextureRect.new()
	var g := Gradient.new()
	g.colors = PackedColorArray([Color(0.05, 0.06, 0.14, 0.85), Color(0.05, 0.06, 0.14, 0.0), Color(0.05, 0.06, 0.14, 0.75)])
	g.offsets = PackedFloat32Array([0.0, 0.55, 1.0])
	var gt := GradientTexture2D.new()
	gt.gradient = g
	gt.fill_from = Vector2(0, 0)
	gt.fill_to = Vector2(0, 1)
	grad.texture = gt
	grad.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	grad.stretch_mode = TextureRect.STRETCH_SCALE
	grad.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	grad.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(grad)
	var col := UIKit.vbox(2)
	col.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	col.offset_top = 210
	col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(col)
	var words := UIKit.upper(Data.title()).split(" ")
	for i in words.size():
		var l := Kit.label(words[i], "Display")
		l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		l.add_theme_font_size_override("font_size", 92 if i == 0 else 84)
		l.add_theme_color_override("font_color", Kit.c("primary") if i == 0 else Kit.c("on_surface"))
		l.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.5))
		l.add_theme_constant_override("shadow_offset_y", 5)
		l.add_theme_constant_override("shadow_offset_x", 0)
		col.add_child(l)
		UIKit.pop_in(l, 0.15 * i)
	var sub := Kit.label(UIKit.upper(tr(str(Data.config.get("subtitle", "")))), "Overline")
	sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	sub.add_theme_font_size_override("font_size", 26)
	sub.add_theme_color_override("font_color", Kit.c("accent"))
	sub.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.5))
	sub.add_theme_constant_override("shadow_offset_y", 3)
	col.add_child(sub)
	# the whole card is the tap target; the button is its visible call to action
	_tap = Kit.button(tr("TAP TO START"), "primary", "circle-play", dismiss)
	_tap.name = "title_start"
	_tap.custom_minimum_size = Vector2(440, 96)
	_tap.add_theme_font_size_override("font_size", 30)
	_tap.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	_tap.offset_left = -220
	_tap.offset_right = 220
	_tap.offset_top = -330
	_tap.offset_bottom = -234
	add_child(_tap)
	var ver := Kit.label("v" + str(ProjectSettings.get_setting("application/config/version", "")), "CaptionMuted")   # the release train bumps project.godot, not data/config.json (was stuck on 1.2.4)
	ver.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	ver.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	ver.offset_top = -190
	ver.offset_bottom = -150
	add_child(ver)
	if UIManager.world:
		UIManager.world.rig.frame_home(0.0)
		UIManager.world.rig.input_enabled = false
		UIManager.world.mute_labels(true)   # W66 round 2: town names (Rustwater) sat under the subtitle


func _exit_tree() -> void:
	if UIManager.world:
		UIManager.world.mute_labels(false)


func _process(delta: float) -> void:
	_t += delta
	_tap.modulate.a = 0.88 + 0.12 * sin(_t * 4.0)


func _gui_input(event: InputEvent) -> void:
	if _done:
		return
	if (event is InputEventMouseButton and event.pressed) or (event is InputEventScreenTouch and event.pressed):
		dismiss()


func dismiss() -> void:
	_done = true
	AudioManager.play("whoosh")
	AudioManager.start_music()
	if EconomyManager.stat("deliveries") == 0:
		AudioManager.voice("welcome", 1.0)
	if UIManager.world:
		var rig: CameraRig = UIManager.world.rig
		rig.input_enabled = true
		rig.frame_home(0.6)
		UIManager.world.mute_labels(false)
	dismissed.emit()
	var t := create_tween()
	t.tween_property(self, "modulate:a", 0.0, 0.4)
	t.tween_callback(queue_free)
