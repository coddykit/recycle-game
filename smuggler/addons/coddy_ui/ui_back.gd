extends Node
## coddy_ui Android Back, one layer at a time (2026-10-28: pharmacy-rush closed the whole app when Back was pressed over
## the open Shop panel — Godot's default application/config/quit_on_go_back = true quits whatever is on screen).
## One router per game, added once under the game's root (it turns quit_on_go_back off for the tree):
##   const UiBack = preload("res://addons/coddy_ui/ui_back.gd")
##   var back := UiBack.new(); add_child(back)
##   back.setup({"game": _back, "main": _quit_confirm})   # both optional
##   UiBack.layer(drawer, close_panel)                     # a game panel / sheet that Back closes while it is visible
## Every Back (the root Window's go_back_requested = Android Back; Esc / ui_cancel runs steps 1-4 only, never quits):
##   1. a full-screen ad owns the screen (/root/Ads._showing, or a visible node in group "coddy_back_block") → nothing
##   2. the top open UiModal: its "back" opt, else close_requested when it has a close; a window with neither keeps
##      Back (nothing happens) so the game never quits from under it
##   3. the top visible layer() panel → its close function
##   4. opts.game: Callable → bool, the game's own layers (true = it closed something)
##   5. the main screen: opts.main (a "press Back again" toast / quit confirm) — else the game is told to save
##      (NOTIFICATION_WM_CLOSE_REQUEST to the tree) and quits
## press() returns what it did: "blocked" | "window" | "layer" | "game" | "main" | "quit". Tests: UiBack.send(tree)
## presses Back exactly like Android (the notification to every node, then the signal); quit_hook replaces the quit.

const GROUP := "coddy_back"
const BLOCK_GROUP := "coddy_back_block"
const CLOSE_META := "coddy_back_close"

var _opts := {}
var last := ""                    ## what the last Back did (tests, analytics)
var quit_hook := Callable()       ## tests: called instead of the close request (save) + quit


func _init() -> void:
	name = "UiBack"


func setup(opts: Dictionary = {}) -> void:
	_opts = opts


func _enter_tree() -> void:
	get_tree().quit_on_go_back = false
	var win := get_tree().root
	if not win.go_back_requested.is_connected(_on_go_back):
		win.go_back_requested.connect(_on_go_back)


func _exit_tree() -> void:
	var win := get_tree().root
	if win.go_back_requested.is_connected(_on_go_back):
		win.go_back_requested.disconnect(_on_go_back)


## Registers a game panel / sheet / card: while it is visible in the tree, Back calls close (and nothing else).
static func layer(node: CanvasItem, close: Callable) -> void:
	node.set_meta(CLOSE_META, close)
	node.add_to_group(GROUP)


## Sends Back the way Android does (Window: NOTIFICATION_WM_GO_BACK_REQUEST to the tree, then go_back_requested).
static func send(tree: SceneTree) -> void:
	tree.root.propagate_notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
	tree.root.go_back_requested.emit()


func _on_go_back() -> void:
	press()


func _unhandled_input(ev: InputEvent) -> void:
	if ev.is_action_pressed("ui_cancel") and not ev.is_echo():
		if press(false) != "main":
			get_viewport().set_input_as_handled()


## One Back. may_quit = false (Esc): the main screen does nothing.
func press(may_quit: bool = true) -> String:
	last = _press(may_quit)
	return last


func _press(may_quit: bool) -> String:
	var tree := get_tree()
	if blocked(tree):
		return "blocked"
	var top := top_layer(tree)
	if top != null:
		if top.has_method("back"):
			top.call("back")
			return "window"
		var close: Callable = top.get_meta(CLOSE_META, Callable())
		if close.is_valid():
			close.call()
		return "layer"
	var game: Callable = _opts.get("game", Callable())
	if game.is_valid() and bool(game.call()):
		return "game"
	if not may_quit:
		return "main"
	var main: Callable = _opts.get("main", Callable())
	if main.is_valid():
		main.call()
		return "main"
	if quit_hook.is_valid():
		quit_hook.call()   # tests: no close request either (a game's autoload may save AND quit on it)
		return "quit"
	tree.root.propagate_notification(Node.NOTIFICATION_WM_CLOSE_REQUEST)   # the game saves, as on a window close
	tree.quit()
	return "quit"


static func blocked(tree: SceneTree) -> bool:
	var ads := tree.root.get_node_or_null("Ads")
	if ads != null and ads.get("_showing") == true:
		return true
	for n in tree.get_nodes_in_group(BLOCK_GROUP):
		if _open(n):
			return true
	return false


## The layer Back closes first: open windows (UiModal) above game panels, then the higher canvas layer, then the later
## node in the tree (drawn on top). null = nothing open.
static func top_layer(tree: SceneTree) -> Node:
	var best: Node = null
	var best_key := []
	for n in tree.get_nodes_in_group(GROUP):
		if not _open(n):
			continue
		var cl := (n as CanvasItem).get_canvas_layer_node()
		var key := [1 if n.has_method("back") else 0, cl.layer if cl != null else 0]
		if best == null or key > best_key or (key == best_key and n.is_greater_than(best)):
			best = n
			best_key = key
	return best


static func _open(n: Node) -> bool:
	return n is CanvasItem and n.is_inside_tree() and not n.is_queued_for_deletion() and (n as CanvasItem).is_visible_in_tree()
