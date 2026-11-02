extends Node
## coddy_ui TouchScroll — the ONE fleet finger-scrolling module (games/CLAUDE.md §16). Merged from the 10-19 per-game copies
## (trading-sim d078f975e5, moneytycoon 59b88d4938, skiresort 3ca1a4112b, train-baron fbdf72e58b, airport-baron 8f7e259d73,
## cinnamon d9708a3f69; chaiwala / canal lineage) + the W62/W63 "never rebuild under a finger" guard (sky-tower 1a563c5978,
## airport-baron e5eb3e96c3). Never copy this file into a game by hand: `bash games/tools/ui/sync.sh <game>`.
##
## Why: Godot's ScrollContainer only follows a finger when the touch reaches it, and inside a list almost everything is a
## Button / CheckButton / OptionButton / card that takes the touch and never passes it on. This node watches touch events
## BEFORE the controls do (`_input`):
##   - finger (or left mouse) down: find the control Godot would give the touch to (topmost, respecting clips and
##     mouse_filter) and remember the ScrollContainers it sits in. Nothing is consumed → a plain tap works as before;
##   - past DRAG_START px the innermost container that can move that way follows the finger 1:1 (vertical or
##     horizontal), whatever is under the finger; the pressed button gets NOTIFICATION_SCROLL_BEGIN (no click fires);
##   - lift: the list flings on and slows down (inertia).
## A drag on a slider, scroll bar or text field keeps its own behaviour. Because the start point is resolved by GUI
## picking, a list under a modal / scrim / guide shade is never scrolled through it. `allow` / `block` filters stay
## available for game rules.
##
## Use (pick one per input root; both can coexist):
##   TouchScroll.attach(root, allow, block)  → scrolls ScrollContainers below `root` (a Control or a Window)
##   TouchScroll.ensure(viewport)            → scrolls every ScrollContainer of that viewport (UiScroll.make() calls it)
## Rebuild guard (W62/W63): before rebuilding a list, `if TouchScroll.held(list): wait` — true while a finger is down in that
## viewport or a list button is held. `finger_down` / `is_scrolling()` for finer checks.

const SELF_PATH := "res://addons/coddy_ui/touch_scroll.gd"
const DRAG_START := 10.0      ## px (canvas) before a touch becomes a scroll (a tap wobbles a little)
const FLING_MIN := 160.0      ## px/s at lift-off that still flings
const FLING_DECAY := 5.5      ## 1/s: the fling's speed is multiplied by e^(-FLING_DECAY * t)
const FLING_STOP := 24.0      ## px/s: the fling ends below this
const FLING_MAX := 2600.0     ## px/s: the fastest fling
const OWN_DEADZONE := 100000  ## a ScrollContainer's own touch scrolling is parked: this node is the only one that scrolls
const HOLD_SAFETY_MS := 3000  ## held() gives up after a finger has been "down" this long without events (lost release)

var roots: Array = []                    ## Controls / Windows: only the ScrollContainers below them (empty = whole viewport)
var allow: Callable = Callable()         ## (ScrollContainer) -> bool; false = do not scroll it now
var block: Callable = Callable()         ## (Vector2) -> bool; true = a touch at this point belongs to something drawn over the lists
var finger_down := false                 ## a finger / left button is down anywhere in this viewport
var scrolls := 0                         ## tests: drags that turned into a scroll
var _touching := false
var _down_ms := 0
var _start := Vector2.ZERO
var _last := Vector2.ZERO
var _last_ms := 0
var _cands: Array = []                   ## ScrollContainers under the finger, innermost first
var _sc: ScrollContainer = null          ## the container the finger is scrolling now
var _horizontal := false
var _origin := 0.0                       ## its scroll value when the drag took over
var _vel := 0.0                          ## px/s along the scroll axis (smoothed)
var _fling: ScrollContainer = null
var _fling_pos := 0.0
var _mouse := false                      ## the pointer in use is the mouse (desktop), not a finger


## Adds a TouchScroll that scrolls everything below `root` (a Control or a Window). Safe to call twice. Returns it.
## ONE instance per viewport: when the viewport already has one (UiScroll lists call ensure()), that one gets the filters
## and is returned — two instances on one viewport would fight over the same drag.
static func attach(root: Node, allow_filter: Callable = Callable(), block_filter: Callable = Callable()) -> Node:
	var vp: Viewport = root as Viewport if root is Viewport else (root.get_viewport() if root.is_inside_tree() else null)
	var existing: Node = null
	for ch in root.get_children():
		if ch.get_script() != null and ch.get_script().resource_path == SELF_PATH:
			existing = ch
	if existing == null and vp != null and vp.has_meta("coddy_touch_scroll") and is_instance_valid(vp.get_meta("coddy_touch_scroll")):
		existing = vp.get_meta("coddy_touch_scroll")
	if existing != null:
		if allow_filter.is_valid():
			existing.allow = allow_filter
		if block_filter.is_valid():
			existing.block = block_filter
		return existing
	var node: Node = load(SELF_PATH).new()
	node.name = "TouchScroll"
	node.roots = [root]
	node.allow = allow_filter
	node.block = block_filter
	root.add_child(node)
	if vp != null:
		vp.set_meta("coddy_touch_scroll", node)
	return node


## The viewport-wide instance (no roots: every ScrollContainer of `vp`). Created on first use (added deferred: safe to
## call while the tree is busy adding children), one per viewport.
static func _ensure_later(n) -> void:
	if is_instance_valid(n) and (n as Node).is_inside_tree():
		ensure(n)


static func ensure(vp: Viewport) -> Node:
	if vp == null:
		return null
	if vp.has_meta("coddy_touch_scroll"):
		var known = vp.get_meta("coddy_touch_scroll")
		if is_instance_valid(known):
			known.roots = []   # an attached instance now covers the whole viewport (lists outside its root exist)
			return known
	var node: Node = load(SELF_PATH).new()
	node.name = "CoddyTouchScroll"
	vp.set_meta("coddy_touch_scroll", node)
	vp.add_child.call_deferred(node)
	return node


## Every TouchScroll that hears `n`'s viewport (the viewport-wide one and any attached to an ancestor).
static func instances_for(n: Node) -> Array:
	var out: Array = []
	if n == null or not n.is_inside_tree():
		return out
	var vp := n.get_viewport()
	var g = vp.get_meta("coddy_touch_scroll") if vp.has_meta("coddy_touch_scroll") else null
	if g != null and is_instance_valid(g):
		out.append(g)
	var up: Node = n
	while up != null:
		for ch in up.get_children():
			if ch.get_script() != null and ch.get_script().resource_path == SELF_PATH and not ch in out:
				out.append(ch)
		if up == vp:
			break
		up = up.get_parent()
	return out


## W62/W63 rebuild guard: true while a finger is down in `list`'s viewport (or any TouchScroll that hears it is mid-drag)
## or a (non-toggle) button under `list` is held. Rebuild the list only when this is false; update texts in place otherwise.
static func held(list: Node) -> bool:
	if list == null:
		return false
	var area := _area(list)
	for ts in instances_for(list):
		if ts._sc != null and (area.size == Vector2.ZERO or ts._sc == list or ts._sc.is_ancestor_of(list) or list.is_ancestor_of(ts._sc)):
			return true
		# #183: only a finger that went down ON this list protects it. A tap on the tab bar / HUD left a kept list
		# "held" (finger_down is viewport-wide) → the new tab's rows waited for the next 1 Hz refresh, and up to
		# HOLD_SAFETY_MS + 1 s (owner 10-24: "tab content 3–4 s late") when a game's _input ate the release.
		if area.size != Vector2.ZERO and not area.has_point(ts._start):
			continue
		if ts._touching:
			return true
		if ts.finger_down and Time.get_ticks_msec() - int(ts._down_ms) < HOLD_SAFETY_MS:
			return true
	for b in list.find_children("*", "BaseButton", true, false):
		var bb := b as BaseButton
		if not bb.toggle_mode and bb.is_pressed():
			return true
	return false


## #183: the on-screen area a finger must start in to protect `list` (its nearest ScrollContainer, else the list), in
## viewport input coordinates. Zero size = unknown (not a laid-out Control) → every finger counts, as before.
static func _area(list: Node) -> Rect2:
	var c: Control = null
	var up: Node = list
	while up != null and not (up is Viewport):
		if up is ScrollContainer:
			c = up as Control
			break
		up = up.get_parent()
	if c == null and list is Control:
		c = list as Control
	if c == null or not c.is_inside_tree() or c.size == Vector2.ZERO:
		return Rect2()
	return c.get_global_transform_with_canvas() * Rect2(Vector2.ZERO, c.size)


func _ready() -> void:
	set_process(false)
	process_mode = Node.PROCESS_MODE_ALWAYS


func _input(ev: InputEvent) -> void:
	if handle(ev):
		get_viewport().set_input_as_handled()


func is_scrolling() -> bool:
	return _sc != null or _fling != null


## Feeds one input event; true = consumed (a scroll drag has taken the pointer).
func handle(ev: InputEvent) -> bool:
	if ev is InputEventScreenTouch:
		var t := ev as InputEventScreenTouch
		if t.index != 0:
			_cancel()
			return false
		_mouse = false
		finger_down = t.pressed
		if t.pressed:
			_down_ms = Time.get_ticks_msec()
			_begin(t.position)
		else:
			return _end()
	elif ev is InputEventScreenDrag:
		var d := ev as InputEventScreenDrag
		if d.index == 0:
			_down_ms = Time.get_ticks_msec()
			if _touching and not _mouse:
				return _move(d.position)
	elif ev is InputEventMouseButton:
		var b := ev as InputEventMouseButton
		if b.device == InputEvent.DEVICE_ID_EMULATION:
			return _sc != null and not _mouse # a phone's emulated mouse copy of a finger that is scrolling: swallow it
		if b.button_index == MOUSE_BUTTON_LEFT:
			if b.pressed:
				_mouse = true
				finger_down = true
				_down_ms = Time.get_ticks_msec()
				_begin(b.position)
			elif _mouse:
				finger_down = false
				return _end()
	elif ev is InputEventMouseMotion:
		var m := ev as InputEventMouseMotion
		if m.device == InputEvent.DEVICE_ID_EMULATION:
			return _sc != null and not _mouse
		if _mouse and _touching and (m.button_mask & MOUSE_BUTTON_MASK_LEFT) != 0:
			_down_ms = Time.get_ticks_msec()
			return _move(m.position)
		elif _mouse and _touching:
			_cancel()
			finger_down = false
	return false


func _search_roots() -> Array:
	if not roots.is_empty():
		return roots
	return [get_viewport()] if is_inside_tree() else []


func _begin(pos: Vector2) -> void:
	_stop_fling()
	_touching = true
	_sc = null
	_start = pos
	_last = pos
	_last_ms = Time.get_ticks_msec()
	_vel = 0.0
	_cands = []
	if block.is_valid() and bool(block.call(pos)):
		_touching = false
		return
	var top: Control = null
	for r in _search_roots():
		if not is_instance_valid(r) or not _shown(r):
			continue
		if _blocked_at(r, pos):
			_touching = false
			return
		var hit := pick_control(r, pos)
		if hit != null and (top == null or hit.is_greater_than(top)):
			top = hit
	if top == null:
		_touching = false
		return
	var up: Node = top
	while up != null and not up is Viewport:
		if up is ScrollContainer and _under_roots(up):
			_cands.append(up)
		up = up.get_parent()
	if _cands.is_empty():
		_touching = false
		return
	for c in _cands:
		var sc: ScrollContainer = c
		if sc.scroll_deadzone < OWN_DEADZONE:
			sc.scroll_deadzone = OWN_DEADZONE # the container's own touch scroll would fight this one
		_press_on_release(sc)


func _under_roots(sc: Node) -> bool:
	if roots.is_empty():
		return true
	for r in roots:
		if is_instance_valid(r) and (r == sc or (r as Node).is_ancestor_of(sc)):
			return true
	return false


func _move(pos: Vector2) -> bool:
	var now := Time.get_ticks_msec()
	if _sc == null:
		var d := pos - _start
		if d.length() < DRAG_START:
			return false
		var horiz := absf(d.x) > absf(d.y)
		var sc := _pick(horiz, d.x if horiz else d.y)
		if sc == null:
			_touching = false # nothing here scrolls that way: the touch belongs to whatever is under it (camera, tap)
			return false
		_sc = sc
		_horizontal = horiz
		_origin = float(sc.scroll_horizontal if horiz else sc.scroll_vertical)
		scrolls += 1
		_cancel_press()
	var span := (pos.x - _start.x) if _horizontal else (pos.y - _start.y)
	_set_scroll(_sc, _origin - span, _horizontal)
	var dt := maxf(1.0, float(now - _last_ms)) / 1000.0
	var step := (pos.x - _last.x) if _horizontal else (pos.y - _last.y)
	_vel = lerpf(_vel, -step / dt, 0.5) # scroll speed (a finger moving up scrolls forward)
	_last = pos
	_last_ms = now
	return true


func _end() -> bool:
	var was := _sc != null
	if was and absf(_vel) >= FLING_MIN and Time.get_ticks_msec() - _last_ms < 120:
		_fling = _sc
		_vel = clampf(_vel, -FLING_MAX, FLING_MAX)
		_fling_pos = float(_sc.scroll_horizontal if _horizontal else _sc.scroll_vertical)
		set_process(true)
	if was:
		_sc.propagate_notification(Control.NOTIFICATION_SCROLL_END)
	_touching = false
	_sc = null
	_cands = []
	return was # the lift ends a drag: the release is ours too (the press was cancelled)


func _cancel() -> void:
	_touching = false
	_sc = null
	_cands = []


func _stop_fling() -> void:
	_fling = null
	set_process(false)


func _process(delta: float) -> void:
	if _fling == null or not is_instance_valid(_fling) or not _shown(_fling):
		_stop_fling()
		return
	_vel *= exp(-FLING_DECAY * delta)
	if absf(_vel) < FLING_STOP:
		_stop_fling()
		return
	_fling_pos = _set_scroll(_fling, _fling_pos + _vel * delta, _horizontal)
	var lim := _limit(_fling, _horizontal)
	if (_fling_pos <= 0.0 and _vel < 0.0) or (_fling_pos >= lim and _vel > 0.0):
		_stop_fling()


## Tests: run the fling to its end without waiting real frames.
func settle(max_steps: int = 600, dt: float = 1.0 / 60.0) -> void:
	for i in max_steps:
		if _fling == null:
			return
		_process(dt)


## The innermost container under the finger that can still move in the direction the finger is going.
func _pick(horiz: bool, finger: float) -> ScrollContainer:
	for c in _cands:
		var sc: ScrollContainer = c
		if not is_instance_valid(sc) or not _shown(sc):
			continue
		if allow.is_valid() and not bool(allow.call(sc)):
			continue
		var lim := _limit(sc, horiz)
		if lim <= 1.0:
			continue
		var cur := float(sc.scroll_horizontal if horiz else sc.scroll_vertical)
		if finger < 0.0 and cur < lim - 0.5: # finger goes up / left: the content moves on
			return sc
		if finger > 0.0 and cur > 0.5:
			return sc
	return null


## How far a container can scroll along an axis (0 = it cannot: nothing hidden, or that direction is switched off).
static func _limit(sc: ScrollContainer, horiz: bool) -> float:
	if horiz:
		if sc.horizontal_scroll_mode == ScrollContainer.SCROLL_MODE_DISABLED:
			return 0.0
		var hb := sc.get_h_scroll_bar()
		return maxf(0.0, hb.max_value - hb.page)
	if sc.vertical_scroll_mode == ScrollContainer.SCROLL_MODE_DISABLED:
		return 0.0
	var vb := sc.get_v_scroll_bar()
	return maxf(0.0, vb.max_value - vb.page)


static func _set_scroll(sc: ScrollContainer, v: float, horiz: bool) -> float:
	var c := clampf(v, 0.0, _limit(sc, horiz))
	if horiz:
		sc.scroll_horizontal = int(roundf(c))
	else:
		sc.scroll_vertical = int(roundf(c))
	return c


## Visible itself and through every parent (like is_visible_in_tree, which tests outside the tree cannot use).
static func _shown(c: Node) -> bool:
	var n: Node = c
	while n != null:
		if n is CanvasItem and not (n as CanvasItem).visible:
			return false
		if n is Window and not (n as Window).visible:
			return false
		n = n.get_parent()
	return true


## The control Godot's GUI would hand a touch at `pos` (topmost visible, clips respected, IGNORE skipped), or null.
## Does not enter SubViewports (their own viewport) or embedded Windows.
static func pick_control(n: Node, pos: Vector2) -> Control:
	var count := n.get_child_count()
	for i in range(count - 1, -1, -1):
		var ch := n.get_child(i)
		if ch is Window or ch is SubViewport:
			continue
		if ch is CanvasItem and not (ch as CanvasItem).visible:
			continue
		if ch is Control:
			var c := ch as Control
			if c.clip_contents and not c.get_global_rect().has_point(pos):
				continue
			var inner := pick_control(c, pos)
			if inner != null:
				return inner
			if c.mouse_filter != Control.MOUSE_FILTER_IGNORE and c.get_global_rect().has_point(pos):
				return c
		elif ch is CanvasLayer or ch is Node2D or not ch is CanvasItem:
			if ch is CanvasLayer and not (ch as CanvasLayer).visible:
				continue
			var deeper := pick_control(ch, pos)
			if deeper != null:
				return deeper
	return null


## A slider, a scroll bar or a text field under the finger keeps its own drag.
static func _blocked_at(n: Node, pos: Vector2) -> bool:
	for ch in n.get_children():
		if ch is Window or ch is SubViewport:
			continue
		if not (ch is Control):
			if ch is CanvasLayer and _blocked_at(ch, pos):
				return true
			continue
		var c := ch as Control
		if not c.visible:
			continue
		if c.clip_contents and not c.get_global_rect().has_point(pos):
			continue
		if (c is Slider or c is ScrollBar or c is LineEdit or c is TextEdit) and c.get_global_rect().has_point(pos) and (c as Control).modulate.a > 0.05:
			return true
		if _blocked_at(c, pos):
			return true
	return false


## OptionButton / MenuButton open their list on touch-down, which a scroll could not take back: they act on release
## inside a scroll view (the scroll then cancels them like any other button).
static func _press_on_release(sc: ScrollContainer) -> void:
	for n in sc.find_children("*", "BaseButton", true, false):
		var b := n as BaseButton
		if (b is OptionButton or b is MenuButton) and b.action_mode == BaseButton.ACTION_MODE_BUTTON_PRESS:
			b.action_mode = BaseButton.ACTION_MODE_BUTTON_RELEASE


## A ScrollContainer that takes over a drag sends NOTIFICATION_SCROLL_BEGIN to everything inside it: every button drops
## its pending press (no click, nothing stays "pressed"). Same call here.
func _cancel_press() -> void:
	if _sc != null:
		_sc.propagate_notification(Control.NOTIFICATION_SCROLL_BEGIN)


## Safety net for dialogs nobody wired: every Window (dialog) entering the tree gets its own viewport-wide TouchScroll.
## Call once (Main does).
static func install_for_windows(tree: SceneTree) -> void:
	var hook := func(n: Node) -> void:
		if n is Window and n != n.get_tree().root and not (n is Popup and not (n is AcceptDialog)):
			_ensure_later.call_deferred(n)
	tree.node_added.connect(hook)
