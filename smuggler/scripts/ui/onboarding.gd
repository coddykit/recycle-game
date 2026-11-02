class_name Onboarding
extends Control
const Kit = preload("res://addons/coddy_ui/ui_kit.gd")
const UiScroll = preload("res://addons/coddy_ui/ui_scroll.gd")
const Illustration = preload("res://addons/coddy_ui/ui_illustration.gd")
const UiSafeArea = preload("res://addons/coddy_ui/ui_safe_area.gd")
## First-launch story intro with Dot the mentor: cinematic camera beats,
## typewriter dialogue, naming your company and picking its colour.
## Afterwards the contextual tutorial takes over in Dot's voice.
## Onboarding v2: the three beats that describe the game (the garage + hatchback, the regions, the borders) also show
## one real frame of this game in the story card (the kit illustration's rounded cover picture): the hatchback on its
## first run, vans on the roads, the grown empire — earned by the game's own bot and shot by tools/onboarding (README).

signal finished

const COLORS := ["E05A47", "3D8BD9", "5DBB63", "FFD23F", "8E5CD9", "F28C28", "2BB3A8", "F06EAA"]
## Beat camera key → hero frame, 512×384 RGB, the three ≤ 1 MB (manifest.json beside them: source commit, state,
## camera, sha256). A beat with a hero whose file does not load shows NO picture and logs an error (no silent fallback).
const HEROES := {"car": "res://assets/ui/onboarding/01-first-run.png", "map": "res://assets/ui/onboarding/02-routes.png",
		"border": "res://assets/ui/onboarding/03-empire.png"}
const HERO_ASPECT := 16.0 / 9.0   ## the card's picture window (cover crop of the 4:3 frame; the action sits in the middle)

var world: World
var _beats: Array = []
var _index := -1
var _panel: PanelContainer
var _scroll: ScrollContainer
var _content: VBoxContainer
var _text: Label
var _hint: Label
var _continue: Button
var _dots: Control
var _step_label: Label
var _name_box: VBoxContainer
var _name_edit: LineEdit
var _color := "E05A47"
var _typing: Tween
var _swatches: Array[Button] = []
var _hero: Control   ## kit illustration (ui_illustration.gd) holding this beat's hero frame; hidden on the other beats
var _skip_btn: Button
## System-bar gaps (canvas px: left, top, right, bottom; UiSafeArea.overlay_edges): "Skip intro" below the status bar /
## cutout, the story card above the navigation bar (owner 10-27, 3-button phone: Continue sat on the bar). On resize.
var _gap := Vector4.ZERO


func start(w: World) -> void:
	world = w
	_beats = Data.onboarding.get("intro", [])
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	_color = str(ProgressionManager.flags.get("company_color", COLORS.pick_random()))
	_gap = UiSafeArea.overlay_edges(get_viewport_rect().size)
	resized.connect(func() -> void:
		_gap = UiSafeArea.overlay_edges(get_viewport_rect().size)
		_place_skip())
	_build()
	world.spawn_mentor()
	world.rig.input_enabled = false
	_next()


func _build() -> void:
	var skip := Kit.button(tr("Skip intro"), "secondary", "", _skip)
	skip.name = "intro_skip"
	skip.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	_skip_btn = skip
	_place_skip()
	skip.grow_horizontal = Control.GROW_DIRECTION_BEGIN   # a longer label (ru) widens it leftwards, never off the right edge
	skip.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(skip)
	# the story card: a kit bottom sheet with Dot, the typed line, step dots and the big primary button
	_panel = UIKit.card("SheetBottom")
	_panel.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	_panel.offset_top = -500 - _gap.w
	_panel.offset_bottom = -24 - _gap.w
	_panel.offset_left = _gap.x
	_panel.offset_right = -_gap.z
	_panel.grow_vertical = Control.GROW_DIRECTION_BEGIN
	_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_panel)
	_scroll = UiScroll.make(true, false)
	_panel.add_child(_scroll)
	var col := UIKit.vbox(14)
	_content = col
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_scroll.add_child(col)
	var top := UIKit.hbox(12)
	top.mouse_filter = Control.MOUSE_FILTER_IGNORE
	col.add_child(top)
	_dots = Kit.dots(maxi(1, _beats.size()), 0)
	_dots.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	top.add_child(_dots)
	_step_label = Kit.label("", "Overline")
	_step_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_step_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_step_label.clip_text = true
	_step_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	top.add_child(_step_label)
	_hero = Illustration.new()
	_hero.name = "onboarding_hero"
	_hero.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	_hero.corner_radius = Vector4.ONE * 18.0
	_hero.visible = false
	col.add_child(_hero)
	var head := UIKit.hbox(16)
	head.mouse_filter = Control.MOUSE_FILTER_IGNORE
	col.add_child(head)
	var portrait := UIKit.icon("mentor", 96)
	head.add_child(portrait)
	var nc := UIKit.vbox(0)
	nc.mouse_filter = Control.MOUSE_FILTER_IGNORE
	nc.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	nc.alignment = BoxContainer.ALIGNMENT_CENTER
	head.add_child(nc)
	nc.add_child(Kit.label(UIKit.upper(TutorialManager.mentor_name()), "Brand"))
	nc.add_child(UIKit.wlabel(tr("Retired dispatcher, your mentor"), "CaptionMuted"))
	_text = UIKit.wlabel("", "Title")
	_text.custom_minimum_size.y = 120
	_text.vertical_alignment = VERTICAL_ALIGNMENT_TOP
	col.add_child(_text)
	_name_box = UIKit.vbox(12)
	_name_box.visible = false
	col.add_child(_name_box)
	var row := UIKit.hbox(8)
	_name_box.add_child(row)
	_name_edit = LineEdit.new()
	_name_edit.max_length = 26
	_name_edit.custom_minimum_size = Vector2(0, 84)
	_name_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_name_edit.alignment = HORIZONTAL_ALIGNMENT_CENTER
	row.add_child(_name_edit)
	var dice := Kit.icon_button("rotate-cw", func(): _name_edit.text = _random_name(), tr("Random name"))
	row.add_child(dice)
	var sw_row := GridContainer.new()
	sw_row.columns = 4
	sw_row.add_theme_constant_override("h_separation", 12)
	sw_row.add_theme_constant_override("v_separation", 12)
	sw_row.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	_name_box.add_child(sw_row)
	for hex in COLORS:
		var sw := Button.new()
		sw.custom_minimum_size = Vector2(88, 88)
		sw.focus_mode = Control.FOCUS_NONE
		sw.pressed.connect(func():
			_color = hex
			AudioManager.play("pop")
			_refresh_swatches()
			world.set_company_color(Data.color(hex)))
		sw.set_meta("hex", hex)
		sw_row.add_child(sw)
		_swatches.append(sw)
	_refresh_swatches()
	var ok := Kit.button(tr("That's the one!"), "primary", "check", _confirm_name)
	UIManager.register_target("intro_name_ok", ok)
	_name_box.add_child(ok)
	_hint = Kit.label(tr("Tap to continue"), "CaptionMuted", false)
	_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(_hint)
	_continue = Kit.button(tr("Continue"), "primary", "arrow-right", _advance)
	_continue.name = "intro_continue"
	_continue.custom_minimum_size.y = 96
	UIManager.register_target("intro_continue", _continue)
	col.add_child(_continue)


func _refresh_swatches() -> void:
	for sw in _swatches:
		var st := StyleBoxFlat.new()
		st.bg_color = Data.color(str(sw.get_meta("hex")))
		st.set_corner_radius_all(30)
		if str(sw.get_meta("hex")) == _color:
			st.set_border_width_all(6)
			st.border_color = Color.WHITE
		for k in ["normal", "hover", "pressed"]:
			sw.add_theme_stylebox_override(k, st)


func _random_name() -> String:
	var a: Array = Data.onboarding.get("company_first", ["Borderline"])
	var b: Array = Data.onboarding.get("company_last", ["Logistics"])
	return "%s %s" % [a.pick_random(), b.pick_random()]


func _gui_input(event: InputEvent) -> void:
	if _name_box.visible:
		return
	# one tap = one _advance (touch audit 2026-10-27): the touch and its emulated click both arrived, so a tap that
	# finished the typing also jumped to the next beat before it could be read.
	if Kit.is_tap(event):
		_advance()


func _advance() -> void:
	if _name_box.visible:
		return
	if _typing and _typing.is_running():
		_typing.kill()
		_text.visible_ratio = 1.0
	else:
		_next()


func _next() -> void:
	_index += 1
	if _index >= _beats.size():
		_finish()
		return
	if _typing and _typing.is_running():
		_typing.kill()
	_step_label.text = tr("YOUR FIRST EMPIRE · %d / %d") % [_index + 1, _beats.size()]
	_dots.current = _index
	var beat: Dictionary = _beats[_index]
	var txt := tr(str(beat.get("text", ""))).replace("{company}", ProgressionManager.company_name())
	_text.text = txt
	_text.visible_ratio = 0.0
	_typing = create_tween()
	_typing.tween_property(_text, "visible_ratio", 1.0, 0.0 if UIKit.reduced_motion() else clampf(txt.length() * 0.028, 0.6, 2.6))
	AudioManager.play("tap_soft")
	AudioManager.voice(str(beat.get("voice", "")), 0.0)
	_name_box.visible = bool(beat.get("name_step", false))
	_hint.visible = not _name_box.visible
	_continue.visible = not _name_box.visible
	_continue.text = tr("Start playing") if _index == _beats.size() - 1 else tr("Continue")
	if _name_box.visible:
		_name_edit.text = _random_name() if not ProgressionManager.flags.has("company") else ProgressionManager.company_name()
	world.mentor_anim(str(beat.get("anim", "idle")))
	_camera(str(beat.get("cam", "hq")))
	_show_hero(str(beat.get("cam", "hq")))
	UIKit.pop_in(_panel)


## The beat's real game frame (or none). Only one hero texture is referenced at a time: the previous one is released.
func _show_hero(cam: String) -> void:
	var path := str(HEROES.get(cam, ""))
	var pic: Texture2D = null
	if path != "":
		pic = load(path) as Texture2D
		if pic == null:
			push_error("onboarding hero missing: " + path)
	_hero.show_page("map", [], "", pic, pic != null, 1 if pic != null else 0)
	_hero.visible = pic != null
	_size_hero()


func _size_hero() -> void:
	var vp := get_viewport_rect().size
	var h := clampf(vp.y * 0.2, 140.0, 300.0)
	_hero.custom_minimum_size = Vector2(minf(vp.x - 120.0, h * HERO_ASPECT), h)


func _camera(key: String) -> void:
	var rig := world.rig
	match key:
		"hq":
			rig.focus_on(Sites.garage_transform(ProgressionManager.garage_level).origin + Sites.hq_frame().away * -3.0, 20.0, 1.0)
		"car":
			if VehicleManager.vehicles.size() > 0 and world.actors.has(VehicleManager.vehicles[0].id):
				rig.focus_on(world.actors[VehicleManager.vehicles[0].id].global_position, 13.0, 1.0)
		"map":
			rig.frame_home(1.6)
		"border":
			if world.regions.size() > 1:
				rig.focus_on(world.regions[1].checkpoint_pos, 24.0, 1.4)


func _confirm_name() -> void:
	# once per naming beat: on a phone the finger's press fires the button, and hiding the still-held button in _next()
	# fires it again — that second call skipped the last beat and closed the intro (S3, onboarding v2 test)
	if not _name_box.visible or _index < 0 or _index >= _beats.size() or not bool(_beats[_index].get("name_step", false)):
		return
	_name_edit.release_focus()
	if DisplayServer.has_feature(DisplayServer.FEATURE_VIRTUAL_KEYBOARD):
		DisplayServer.virtual_keyboard_hide()
	var n := _name_edit.text.strip_edges()
	if n.is_empty():
		n = _random_name()
	ProgressionManager.flags["company"] = n
	ProgressionManager.flags["company_color"] = _color
	world.set_company_color(Data.color(_color))
	if VehicleManager.vehicles.size() > 0:
		VehicleManager.set_paint(VehicleManager.vehicles[0], _color)
	AudioManager.play("upgrade")
	FX.confetti(world, Sites.garage_transform(ProgressionManager.garage_level).origin + Vector3.UP * 2.0)
	_next()


func _skip() -> void:
	if not ProgressionManager.flags.has("company"):
		ProgressionManager.flags["company"] = _random_name()
		ProgressionManager.flags["company_color"] = _color
		world.set_company_color(Data.color(_color))
	_finish()


func _finish() -> void:
	ProgressionManager.flags["intro_done"] = true
	world.mentor_anim("wave")
	world.rig.input_enabled = true
	world.rig.frame_home(1.0)
	GameManager.save_now()
	finished.emit()
	var t := create_tween()
	t.tween_property(self, "modulate:a", 0.0, 0.35)
	t.tween_callback(queue_free)


func _place_skip() -> void:
	if _skip_btn == null:
		return
	_skip_btn.offset_left = -330 - _gap.z
	_skip_btn.offset_right = -16 - _gap.z
	_skip_btn.offset_top = 24 + _gap.y
	_skip_btn.offset_bottom = 108 + _gap.y


func _process(_delta: float) -> void:
	if not is_instance_valid(_panel):
		return
	if _hero.visible:
		_size_hero()
	var keyboard := DisplayServer.virtual_keyboard_get_height() if DisplayServer.has_feature(DisplayServer.FEATURE_VIRTUAL_KEYBOARD) else 0
	var scale_y := get_viewport_rect().size.y / maxf(1.0, DisplayServer.window_get_size().y)
	_panel.offset_bottom = -maxf(24.0 + _gap.w, keyboard * scale_y + 16.0)
	var available := get_viewport_rect().size.y + _panel.offset_bottom - 160.0 - _gap.y
	_panel.offset_top = _panel.offset_bottom - minf(available, _content.get_combined_minimum_size().y + 40.0)
