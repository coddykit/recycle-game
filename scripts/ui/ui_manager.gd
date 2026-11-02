extends Node
## Thin facade so gameplay code can trigger UI feedback without knowing the HUD.

signal panel_opened(name: String)
signal panel_closed(name: String)
signal focus_requested(world_pos: Vector3, zoom: float)

## scripts/ui/hud.gd and scripts/world/world.gd. Both read null once the registered scene is freed (language switch,
## reset and prestige reload the scene): a stale reference here is a use-after-free in a release build (W63 item 13).
var hud: Node = null:
	get:
		return _hud if is_instance_valid(_hud) else null
	set(v):
		_hud = v
var world: Node = null:
	get:
		return _world if is_instance_valid(_world) else null
	set(v):
		_world = v
var _hud: Node = null
var _world: Node = null
var current_panel := ""
var _targets := {}


## The HUD, or null while it is gone: a scene reload (language switch, reset, prestige) frees it before the new
## one registers, and a late call from a lambda / signal / timer must not touch a HUD that is no longer in the tree.
func _ui() -> Node:
	return hud if hud != null and is_instance_valid(hud) and hud.is_inside_tree() else null


func register_target(key: String, c: Control) -> void:
	_targets[key] = weakref(c)


func get_target(key: String) -> Control:
	if not _targets.has(key):
		return null
	var c: Control = _targets[key].get_ref()
	if c == null or not c.is_inside_tree() or not c.is_visible_in_tree():
		return null
	return c


func open_panel(name: String, arg: Variant = null) -> void:
	var h := _ui()
	if h:
		h.open_panel(name, arg)


func close_panel() -> void:
	var h := _ui()
	if h:
		h.close_panel()


func notify_panel(name: String, opened: bool) -> void:
	if opened:
		current_panel = name
		panel_opened.emit(name)
	else:
		if current_panel == name:
			current_panel = ""
		panel_closed.emit(name)


func toast(text: String, color := Color.WHITE, icon := "") -> void:
	var h := _ui()
	if h:
		h.toast(text, color, icon)


func float_text(world_pos: Vector3, text: String, color := Color.WHITE) -> void:
	var h := _ui()
	if h:
		h.float_text(world_pos, text, color)


func coin_burst(world_pos: Vector3, amount: float) -> void:
	var h := _ui()
	if h:
		h.coin_burst(world_pos, amount)


func modal(title: String, body: String, icon := "", buttons: Array = [], accent := Color("ffc93c")) -> void:
	var h := _ui()
	if h:
		h.show_modal(title, body, icon, buttons, accent)


func screen_pos(world_pos: Vector3) -> Vector2:
	if world and world.has_method("screen_pos"):
		return world.screen_pos(world_pos)
	return Vector2(-1000, -1000)


func focus(world_pos: Vector3, zoom := -1.0) -> void:
	focus_requested.emit(world_pos, zoom)


var _last_haptic := -1000

func feedback(kind: String) -> void:
	if GameManager._suspended: return
	AudioManager.play({"claim":"cash", "upgrade":"upgrade", "invalid_action":"click", "milestone":"unlock"}.get(kind, "click"))
	haptic(30 if kind == "milestone" else 15)

func haptic(ms := 20) -> void:
	var now := Time.get_ticks_msec()
	if GameManager._suspended or now - _last_haptic < 120: return
	_last_haptic = now
	if GameManager.settings.get("haptics", true) and OS.has_feature("mobile"):
		Input.vibrate_handheld(ms)
