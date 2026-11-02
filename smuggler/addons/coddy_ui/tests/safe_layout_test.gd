extends SceneTree
## Safe-area layout test (Patron 2026-10-28): the game's screen on simulated phones — status bar + cutout + 3-button
## navigation bar, four aspects, the game's orientation (sensor landscape: the bar on the right AND the left) — and no
## text or button may sit under a system bar; a toast never covers the tab bar. See safe_layout.gd.
##   godot --headless --path games/<game> -s res://addons/coddy_ui/tests/safe_layout_test.gd
## The game says how to build its screen in res://tests/safe_layout_hooks.gd (extends RefCounted):
##   func build(tree: SceneTree) -> Node        fresh game, first-run screens skipped, HUD on screen (may await)
##   func skip(scene: Node) -> Array            optional: subtrees not checked (labels that follow the 3D map)
##   func show_toast(scene: Node) -> Control    optional: shows a toast, returns its card (null = none)
##   func tab_bar(scene: Node) -> Control       optional: the bar the toast must not cover
##   func teardown(scene: Node) -> void         optional: default queue_free()
##   func orientation() -> String               optional: "portrait" / "landscape" (default: project setting)
##   func backgrounds(scene: Node) -> Array     optional: bottom-anchored backgrounds that must reach the screen bottom
##                                              (the house banner is always checked when it shows)
## `-- --generic` (fleet scan, no hooks): the main scene from a fresh user://, first-run screens pressed through, every
## phone — prints the findings, never fails.
## Prints "SAFE_LAYOUT: n checks, 0 failures"; exit 1 on a failure.

const SafeLayout := preload("res://addons/coddy_ui/tests/safe_layout.gd")
const HOOKS := "res://tests/safe_layout_hooks.gd"
const AUTO_WORDS := ["skip", "tap to start", "play", "got it", "continue", "next", "start", "let's go", "begin", "ok", "done", "close", "later",
	"not now", "no thanks", "accept", "agree", "finish", "understood", "×", "✕", "new game", "let's build", "english",
	"only what is necessary", "prefer not to answer", "build a kit"]

var checks := 0
var failures := 0
var hooks = null
var generic := false
var _pressed := {}   # generic: a button already pressed is not pressed again (a picker that stays up)


func _initialize() -> void:
	_run.call_deferred()


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("SAFE_LAYOUT ", "PASS " if ok else "FAIL ", label)


func frames(n: int) -> void:
	for _i in n:
		await process_frame


func _landscape() -> bool:
	if hooks != null and hooks.has_method("orientation"):
		return str(hooks.orientation()) == "landscape"
	return int(ProjectSettings.get_setting("display/window/handheld/orientation", 1)) in [0, 2, 4]


func _run() -> void:
	generic = "--generic" in OS.get_cmdline_user_args()
	if not generic:
		if not ResourceLoader.exists(HOOKS):
			print("SAFE_LAYOUT: no ", HOOKS)
			quit(1)
			return
		var hs = load(HOOKS)
		hooks = hs.new() if hs is GDScript and (hs as GDScript).can_instantiate() else null
		if hooks == null:
			print("SAFE_LAYOUT: %s does not load (parse error?)" % HOOKS)
			quit(1)
			return
	var landscape := _landscape()
	for p in SafeLayout.phones(landscape):
		SafeLayout.set_phone(root, p)
		await frames(2)
		var scene: Node = null
		if generic:
			scene = await _generic_build()
		else:
			scene = await hooks.build(self)
		await frames(12)
		if not generic:
			check(scene != null and is_instance_valid(scene), "%s: the game screen was built" % p.name)
			if scene == null or not is_instance_valid(scene):
				continue
		var skip: Array = hooks.skip(scene) if hooks != null and hooks.has_method("skip") else []
		var bad := SafeLayout.scan(root, {"skip": skip})
		var tag := "%s %s (canvas %s)" % [p.name, "landscape" if landscape else "portrait", root.get_visible_rect().size]
		if generic:
			print("SAFE_LAYOUT GEN %s: %d findings" % [tag, bad.size()])
		else:
			check(bad.is_empty(), "%s: no text or button under a system bar (%d)" % [tag, bad.size()])
		for b in bad.slice(0, 16):
			print("SAFE_LAYOUT   ", b)
		if not generic:
			var hb := SafeLayout.hidden_band_error(root, p)
			if str(p.get("nav", "")) != "":
				check(hb == "", "%s: a hidden navigation bar leaves no empty band (%s)" % [tag, hb])
			var bgs: Array = hooks.backgrounds(scene) if hooks.has_method("backgrounds") else []
			var banner := root.find_child("HouseBanner", true, false)
			if banner is Control:
				bgs.append(banner)
			for bgc in bgs:
				var why := SafeLayout.reaches_bottom(root, bgc)
				check(why == "", "%s: bottom background reaches the screen edge %s" % [tag, why])
		if hooks != null and hooks.has_method("show_toast"):
			var t = await hooks.show_toast(scene)
			if t != null:
				await frames(6)
				var tabs = hooks.tab_bar(scene) if hooks.has_method("tab_bar") else null
				check(SafeLayout.shown(t), "%s: the toast shows" % tag)
				check(not SafeLayout.overlaps(t, tabs), "%s: the toast never covers the tab bar (toast %s, tabs %s)" % [tag,
					SafeLayout.screen_rect(t), SafeLayout.screen_rect(tabs) if tabs != null else Rect2()])
				var tb := SafeLayout.scan(root, {"from": t})
				check(tb.is_empty(), "%s: the toast stays between the system bars %s" % [tag, tb])
		if hooks != null and hooks.has_method("teardown"):
			await hooks.teardown(scene)
		elif is_instance_valid(scene) and scene != current_scene:
			scene.queue_free()
		await frames(4)
	SafeLayout.clear(root)
	print("SAFE_LAYOUT: %d checks, %d failures" % [checks, failures])
	quit(1 if failures > 0 else 0)


## Fleet scan: the main scene (fresh user:// on the first phone), first-run screens pressed through for up to 25 s.
func _generic_build() -> Node:
	var main_scene := str(ProjectSettings.get_setting("application/run/main_scene", ""))
	if main_scene.begins_with("uid://"):
		main_scene = ResourceUID.get_id_path(ResourceUID.text_to_id(main_scene))
	change_scene_to_file(main_scene)
	await frames(3)
	var until := Time.get_ticks_msec() + 25000
	var idle := 0
	while Time.get_ticks_msec() < until and idle < 6:
		await create_timer(0.5).timeout
		var bt := _find_button(root)
		if bt == null:
			idle += 1
			continue
		idle = 0
		print("SAFE_LAYOUT GEN press ", bt.get_path(), " '", str(bt.get("text")) if "text" in bt else "", "'")
		_pressed[bt.get_instance_id()] = true
		bt.pressed.emit()
	return current_scene


func _find_button(n: Node) -> BaseButton:
	if n is BaseButton and (n as CanvasItem).is_visible_in_tree() and not (n as BaseButton).disabled \
			and not _pressed.has(n.get_instance_id()):
		var txt := str(n.get("text")).strip_edges().to_lower() if "text" in n else ""
		for key in AUTO_WORDS:
			if txt != "" and (txt == key or txt.begins_with(key)):
				return n
	for c in n.get_children():
		if str(c.name) == "HouseBanner":
			continue
		var r := _find_button(c)
		if r != null:
			return r
	return null
