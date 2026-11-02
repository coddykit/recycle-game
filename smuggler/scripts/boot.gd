extends Node
## First scene (run/main_scene). The native launch screen (application/boot_splash: the game's night-blue background, no
## Godot logo) is continued by a Curtain that starts identical to it, fades in the game's own icon and shows a real
## progress bar plus rotating tips while main.tscn and the model GLBs load on worker threads. Main is then built behind
## the curtain; the curtain fades once Main is in the tree AND 3 frames in a row took < 40 ms (hard cap 20 s after boot).
## Pattern: aircraft / zombi boot.gd. Also applies the phone power profile (60 fps cap).
## Headless runs (tests), QA drivers (`-- --qa`, `--shot=`) and `-- --no-curtain` switch to Main at once.
## `-- --loadshots=/abs/dir` saves the curtain every 0.5 s and `-- --loadhold=S` keeps it at least S s (review).
## `[boot]` lines print the timings.

const MAIN := "res://scenes/main/main.tscn"
const MODELS_DIR := "res://assets/models/"
const SPLASH_BG := Color(0.105882, 0.117647, 0.192157)   ## = application/boot_splash/bg_color
const LOGO := "res://store/icons/icon_512.png"
const HARD_CAP_MS := 20000
const SMOOTH_MS := 40.0
const SMOOTH_FRAMES := 3
## Battery: phones and tablets are capped at 60 fps (a 120 Hz ProMotion / high-refresh Android panel would otherwise
## redraw this scene twice as often). 0 = engine default (desktop: vsync only).
const MOBILE_MAX_FPS := 60

var _curtain: Curtain
var _requested: Array = []


## The frame cap for a platform: MOBILE_MAX_FPS on iOS / Android (or any `mobile` feature build), else 0.
static func max_fps_for(mobile: bool) -> int:
	return MOBILE_MAX_FPS if mobile else 0


static func apply_power_profile() -> void:
	var mobile := OS.has_feature("mobile") or OS.has_feature("ios") or OS.has_feature("android")
	Engine.max_fps = max_fps_for(mobile)


static func instant() -> bool:
	if DisplayServer.get_name() == "headless":
		return true
	for a in OS.get_cmdline_user_args():
		if a == "--no-curtain" or a == "--qa" or a.begins_with("--shot="):
			return true
	return false


## Every model GLB (resource paths as the loader knows them; also works in an export where files are remapped).
static func model_paths() -> Array:
	var out: Array = []
	for f in ResourceLoader.list_directory(MODELS_DIR):
		var n := String(f).trim_suffix(".remap")
		if n.ends_with(".glb"):
			out.append(MODELS_DIR + n)
	return out


func _ready() -> void:
	apply_power_profile()
	print("[boot] start at %d ms" % Time.get_ticks_msec())
	if instant():
		_go_main_now.call_deferred()
		return
	_curtain = Curtain.new()
	get_tree().root.add_child.call_deferred(_curtain)
	# use_sub_threads = false: Main's scripts compile on ONE worker thread, never several at once (Godot 4.7.2 GDScript
	# compile race on phones, Play Vitals 10-30 / games/tools/vitals/check_threaded_load.py)
	for p in [MAIN] + model_paths():
		if ResourceLoader.exists(p) and ResourceLoader.load_threaded_request(p, "", false) == OK:
			_requested.append(p)


func _process(_dt: float) -> void:
	if _curtain == null:
		return
	var total := 0.0
	var busy := false
	for p in _requested:
		var prog := []
		var st := ResourceLoader.load_threaded_get_status(p, prog)
		if st == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
			busy = true
			total += float(prog[0]) if prog.size() > 0 else 0.0
		else:
			total += 1.0
	_curtain.load_progress = total / maxf(1.0, float(_requested.size()))
	if busy:
		return
	set_process(false)
	var main_ps: PackedScene = null
	for p in _requested:
		var r: Resource = ResourceLoader.load_threaded_get(p) if ResourceLoader.load_threaded_get_status(p) == ResourceLoader.THREAD_LOAD_LOADED else null
		if r != null:
			_curtain.keep.append(r)
			if p == MAIN:
				main_ps = r as PackedScene
	print("[boot] %d resources loaded at %d ms" % [_requested.size(), Time.get_ticks_msec()])
	# one more frame with the bar near the end before the (long) Main build frame
	_curtain.building = true
	await get_tree().process_frame
	await get_tree().process_frame
	_go_main(main_ps if main_ps != null else load(MAIN) as PackedScene)


func _go_main_now() -> void:
	if get_tree().current_scene == self:
		_go_main(load(MAIN) as PackedScene)


## Replaces the Boot scene with Main. Returns the Main node.
func _go_main(ps: PackedScene) -> Node:
	var t0 := Time.get_ticks_msec()
	var main := ps.instantiate()
	var tree := get_tree()
	if tree.current_scene == self:
		tree.root.add_child(main)
		tree.current_scene = main
		queue_free()
	else:
		get_parent().add_child(main)
	print("[boot] Main built in %d ms, in the tree at %d ms" % [Time.get_ticks_msec() - t0, Time.get_ticks_msec()])
	return main


## Tests: the synchronous path Boot takes in headless runs.
func start_main_now() -> Node:
	return _go_main(load(MAIN) as PackedScene)


## The loading screen. Starts exactly like the native launch screen (same background colour), fades the game's own icon
## in and shows a progress bar and rotating gameplay tips (translated) under it.
class Curtain extends CanvasLayer:
	const TIPS := [
		"Accept easy contracts first: low risk means steady cash.",
		"Highways are fast but risky; back roads are slow but safe.",
		"Heat adds risk but also better pay. Spread your work across regions.",
		"Upgrade the engine to shorten trips, then the garage for more vehicles.",
		"Good drivers cut risk. Hire the best you can afford.",
		"Automation keeps the empire working while you are away.",
	]
	const ACCENT := Color("ffc93c")
	var keep: Array = []           ## threaded loads kept alive until Main has used them (cache hits)
	var load_progress := 0.0       ## threaded loads 0..1 (set by Boot)
	var building := false          ## Main is being instanced
	var _rect: ColorRect
	var _logo: Panel
	var _bar: ProgressBar
	var _title: Label
	var _tip: Label
	var _shown := 0.0
	var _t := 0.0
	var _tip_t := 0.0
	var _tip_i := 0
	var _smooth := 0
	var _main_seen_ms := -1
	var _last_us := 0
	var _fading := false
	var _shot_dir := ""
	var _hold_s := 0.0
	var _shot_t := 0.0
	var _shot_n := 0

	func _ready() -> void:
		layer = 128
		process_mode = Node.PROCESS_MODE_ALWAYS
		for a in OS.get_cmdline_user_args():
			if a.begins_with("--loadshots="):
				_shot_dir = a.substr(12)
			elif a.begins_with("--loadhold="):
				_hold_s = float(a.substr(11))
		_rect = ColorRect.new()
		_rect.color = SPLASH_BG
		_rect.set_anchors_preset(Control.PRESET_FULL_RECT)
		_rect.mouse_filter = Control.MOUSE_FILTER_STOP
		_rect.theme = UIKit.theme()
		add_child(_rect)
		var tex: Texture2D = load(LOGO) if ResourceLoader.exists(LOGO) else null
		# the icon with rounded corners (a clipping panel: the artwork itself is a plain square)
		_logo = _bar_panel(Color.WHITE)
		(_logo.get_theme_stylebox("panel") as StyleBoxFlat).set_corner_radius_all(62)
		_logo.clip_children = CanvasItem.CLIP_CHILDREN_AND_DRAW
		_center(_logo, -150, 150, -330, -30)
		_logo.modulate.a = 0.0
		var art := TextureRect.new()
		art.texture = tex
		art.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		art.stretch_mode = TextureRect.STRETCH_SCALE
		art.set_anchors_preset(Control.PRESET_FULL_RECT)
		art.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_logo.add_child(art)
		_rect.add_child(_logo)
		var kit = load("res://addons/coddy_ui/ui_kit.gd")
		_title = kit.label(UIKit.upper(_say("Borderline Empire")), "Headline")
		_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_title.add_theme_color_override("font_color", kit.c("primary"))
		_title.add_theme_font_size_override("font_size", 46)
		_center(_title, -330, 330, -10, 56)
		_rect.add_child(_title)
		_bar = kit.progress(0.0, 1.0)   # the kit's progress pill
		_bar.custom_minimum_size = Vector2(460, 16)
		_center(_bar, -230, 230, 84, 100)
		_rect.add_child(_bar)
		_tip = kit.label("", "BodyMuted", true)
		_tip.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_center(_tip, -300, 300, 130, 250)
		_rect.add_child(_tip)
		_tip_i = randi() % TIPS.size()
		_tip.text = _say(TIPS[_tip_i])
		_last_us = Time.get_ticks_usec()

	## Translated through the engine (the locale is already set by GameManager, an autoload).
	func _say(text: String) -> String:
		return TranslationServer.translate(text)

	func _center(c: Control, l: float, r: float, t: float, b: float) -> void:
		c.anchor_left = 0.5
		c.anchor_right = 0.5
		c.anchor_top = 0.5
		c.anchor_bottom = 0.5
		c.offset_left = l
		c.offset_right = r
		c.offset_top = t
		c.offset_bottom = b
		c.mouse_filter = Control.MOUSE_FILTER_IGNORE

	func _bar_panel(col: Color) -> Panel:
		var p := Panel.new()
		var sb := StyleBoxFlat.new()
		sb.bg_color = col
		sb.set_corner_radius_all(8)
		p.add_theme_stylebox_override("panel", sb)
		p.mouse_filter = Control.MOUSE_FILTER_IGNORE
		return p

	## Where loading really is: threaded loads -> Main being built -> Main running -> smooth frames.
	func _target() -> float:
		if _main_seen_ms < 0:
			return 0.9 if building else 0.85 * load_progress
		return 0.97 if _smooth < SMOOTH_FRAMES else 1.0

	func _process(dt: float) -> void:
		var now := Time.get_ticks_usec()
		var frame_ms := (now - _last_us) / 1000.0
		_last_us = now
		_t += minf(dt, 0.1)
		_shown = move_toward(_shown, maxf(_shown, _target()), minf(dt, 0.1) * 1.2)
		_bar.value = _shown
		_logo.modulate.a = minf(1.0, _t / 0.5)
		_tip_t += minf(dt, 0.1)
		if _tip_t > 3.2:
			_tip_t = 0.0
			_tip_i = (_tip_i + 1) % TIPS.size()
			_tip.text = _say(TIPS[_tip_i])
		_debug_shot()
		if _fading:
			return
		var tree := get_tree()
		var main_in := tree.current_scene != null and tree.current_scene.scene_file_path == MAIN and tree.current_scene.is_inside_tree()
		if main_in and _main_seen_ms < 0:
			_main_seen_ms = Time.get_ticks_msec()
			_smooth = 0
			return
		if _main_seen_ms >= 0:
			_smooth = _smooth + 1 if frame_ms < SMOOTH_MS else 0
		var capped := Time.get_ticks_msec() > HARD_CAP_MS
		if _t < _hold_s:
			return
		if (_main_seen_ms >= 0 and _smooth >= SMOOTH_FRAMES and _shown > 0.95) or (capped and (main_in or _main_seen_ms >= 0)):
			_lift()

	func _lift() -> void:
		_fading = true
		print("[boot] playable at %d ms (Main in the tree at %d ms)" % [Time.get_ticks_msec(), _main_seen_ms])
		_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
		var tw := create_tween()
		tw.tween_property(_rect, "modulate:a", 0.0, 0.45)
		tw.tween_callback(queue_free)

	func _debug_shot() -> void:
		if _shot_dir == "" or _t < _shot_t:
			return
		_shot_t = _t + 0.5
		_shot_n += 1
		DirAccess.make_dir_recursive_absolute(_shot_dir)
		RenderingServer.force_draw(false)
		get_viewport().get_texture().get_image().save_png("%s/load_%02d.png" % [_shot_dir, _shot_n])
