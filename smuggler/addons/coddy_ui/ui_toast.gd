extends Control
## coddy_ui toast / snackbar: a pill card (icon tile + up to 2 lines) that slides in under the HUD, stays ~3 s and fades.
## Never a bare outlined label over the 3D view (W64 audit: 7/10 games). Touches pass through (mouse_filter IGNORE).
##   var toast := UiToast.new(); hud.add_child(toast); toast.setup({"top": 140, "left": 0.0, "right": 0.62})
##   toast.show_toast(tr("Saved!"), "success")      # kinds: info, success, warning, danger (+ optional icon name)
##   toast.text = tr("…")                            # same as show_toast(…, "info"); "" hides it
## The newest message replaces the current one (no queue: a tycoon HUD can fire many; the latest matters).
##
## Kit rules (10-27, W66 fleet sweep — they were patched by hand in ~17 games):
## - Language: a toast belongs to the locale it was shown in. A language switch hides it (only a REAL switch: Godot 4.7.2
##   sends NOTIFICATION_TRANSLATION_CHANGED once after a toast enters the tree, that one keeps it).
## - Guide: while a coach bubble / guide card is on screen (a UiCoach, or a node registered with UiToast.add_guide) it is
##   the feedback — an ordinary toast is skipped (and one already up goes). Warning / danger, and a message shown with
##   priority "guide" (a game's deliberate line during its guide: show_toast(tr("New usta hired!"), "success", "",
##   "guide")), still show, placed clear of the guide card and its spotlight (a "guide" line with no clear spot is
##   dropped; a warning / danger one then keeps its own spot).
## - Panels: toast.keep_clear(panel) — the card never covers that control (a sheet's head, an event card, a tip card)
##   while it is visible. It moves just past it (below / above / beside, the nearest spot inside the screen); a visible
##   toast re-places itself when such a panel opens, closes, moves or resizes, without restarting its fade. Event
##   driven (the panel's and its parents' signals), nothing runs per frame.
##     toast.keep_clear(sheet.head_box); toast.keep_clear(sheet.tab_row)    # a full-height sheet: off its head rows
##     toast.keep_clear(event_card, {"pad": 16})
##     toast.keep_clear(sheet, {"foot": true})      # no room outside it → at the foot inside it (over its last row)
##     toast.keep_clear_rect("ticker", rect)          # a global Rect2 (Rect2() removes it)

const Tokens = preload("ui_tokens.gd")
const Icons = preload("ui_icons.gd")
const UiSafeArea = preload("ui_safe_area.gd")
const KINDS := {"info": "info", "success": "circle-check", "warning": "triangle-alert", "danger": "circle-alert"}
const GROUP := "coddy_toast"          ## every toast (guides call it when they change)
const GUIDE_GROUP := "coddy_guide"    ## guide cards the toast keeps quiet for (UiCoach joins by itself)
const PAD := 12.0                     ## gap kept to a panel / guide card

var text := "":
	set(v):
		if v == "":
			hide_toast()
		else:
			show_toast(v, "info")
	get:
		return _label.text if _card != null and _card.visible else ""
var kind := "info"
var priority := ""               ## of the message on screen: "" (ordinary) or "guide"
var top := 140.0                 ## px below this control's top edge (place it under the HUD bar)
var max_width := 760.0
## Highest spot (px below this control's top) the card may move up to when it dodges a panel / the guide. -1 = auto: a
## toast that sits in the upper half never moves above its own row (the HUD is there), a lower one up to the screen top.
var ceiling := -1.0
var shown := 0                   ## tests: how many messages were shown
var skipped := 0                 ## tests: ordinary messages skipped because a guide card was up
var locale := ""                 ## the locale the message on screen was shown in
var _card: PanelContainer
var _icon: TextureRect
var _label: Label
var _tween: Tween
var _rest := Vector2.ZERO        ## the card's resting spot (local); the slide-in is an offset on top of it
var _slide := 0.0
var _clear: Array = []           ## [{node, pad, foot, bye}]
var _watched: Array = []         ## controls whose rect / visibility re-place a visible toast
var _clear_rects := {}           ## id -> global Rect2
var _place_queued := false
var _dodged := false             ## the card is off its own row (a panel / the guide was there)


func _init() -> void:
	name = "Toast"
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_to_group(GROUP)


## opts: top (px), left / right (anchors 0..1 of the area the toast centres in), max_width (px), ceiling (px, see above).
func setup(opts: Dictionary = {}) -> void:
	top = float(opts.get("top", top))
	max_width = float(opts.get("max_width", max_width))
	ceiling = float(opts.get("ceiling", ceiling))
	anchor_left = float(opts.get("left", 0.0))
	anchor_right = float(opts.get("right", 1.0))
	offset_left = 0
	offset_right = 0
	_build()


func _build() -> void:
	if _card != null:
		return
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	_card = PanelContainer.new()
	_card.name = "ToastCard"
	_card.theme_type_variation = "Toast"
	_card.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_card.visible = false
	var h := HBoxContainer.new()
	h.add_theme_constant_override("separation", 18)
	h.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_icon = TextureRect.new()
	_icon.custom_minimum_size = Vector2(56, 56)
	_icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_icon.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	_icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	h.add_child(_icon)
	_label = kit.label("", "LabelStrong", true)
	_label.add_theme_font_size_override("font_size", Tokens.TYPE.body.size)
	_label.max_lines_visible = 2
	_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	_label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	h.add_child(_label)
	_card.add_child(h)
	add_child(_card)
	resized.connect(_place)


## priority "guide": the game shows this line during its guide on purpose (else an info / success toast waits the guide
## out). Returns false when the message was skipped.
func show_toast(message: String, toast_kind: String = "info", icon_name: String = "", toast_priority: String = "") -> bool:
	_build()
	if message == "":
		hide_toast()
		return false
	var k := toast_kind if KINDS.has(toast_kind) else "info"
	if not _speaks_over_guide(k, toast_priority) and guide_up(self):
		skipped += 1
		return false
	kind = k
	priority = toast_priority
	locale = TranslationServer.get_locale()
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	var fb: Array = kit.tile_colors(kind)
	_icon.texture = Icons.tile(icon_name if icon_name != "" else KINDS[kind], 56, fb[0], fb[1], true)
	_label.text = message
	shown += 1
	_card.visible = true
	if _tween != null:
		_tween.kill()
	_slide = 0.0
	_place()
	if not _card.visible:
		return false   # nowhere clear of the guide card
	if not is_inside_tree():
		_card.modulate.a = 1.0
		return true
	_card.modulate.a = 0.0
	# the slide-in comes from above its row; a card moved off a panel / the guide only fades in (the slide would cross it)
	var drop := 0.0 if _dodged else -18.0
	_set_slide(drop)
	_tween = create_tween()
	_tween.set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	_tween.tween_property(_card, "modulate:a", 1.0, Tokens.MOTION.base)
	_tween.parallel().tween_method(_set_slide, drop, 0.0, Tokens.MOTION.base)
	_tween.tween_interval(Tokens.MOTION.toast_hold + clampf(message.length() * 0.03, 0.0, 2.0))
	_tween.tween_property(_card, "modulate:a", 0.0, Tokens.MOTION.slow)
	_tween.tween_callback(func() -> void: _card.visible = false)
	return true


## A guide-priority message (shown over the guide, clear of its card and spotlight).
func show_guide_toast(message: String, toast_kind: String = "success", icon_name: String = "") -> bool:
	return show_toast(message, toast_kind, icon_name, "guide")


func hide_toast() -> void:
	if _tween != null:
		_tween.kill()
	if _card != null:
		_card.visible = false


func is_showing() -> bool:
	return _card != null and _card.visible


func card() -> PanelContainer:
	return _card


static func _speaks_over_guide(k: String, p: String) -> bool:
	return p == "guide" or k == "warning" or k == "danger"


# ---------------------------------------------------------------- language

func _notification(what: int) -> void:
	# Only a real switch: 4.7.2 sends one TRANSLATION_CHANGED after the toast enters the tree (that one keeps the toast)
	if what == NOTIFICATION_TRANSLATION_CHANGED and is_showing() and TranslationServer.get_locale() != locale:
		hide_toast()


# ---------------------------------------------------------------- guide

## A guide card (not a UiCoach) the toast keeps quiet for while it is visible. Optional on the node: guide_rects() ->
## Array of global Rect2 (empty = not up); else its global rect while visible.
static func add_guide(node: CanvasItem) -> void:
	if node.is_in_group(GUIDE_GROUP):
		return
	node.add_to_group(GUIDE_GROUP)
	var ping := func() -> void: guide_changed(node)
	node.visibility_changed.connect(ping)
	if node is Control:
		(node as Control).item_rect_changed.connect(ping)
	node.tree_entered.connect(ping)
	node.tree_exited.connect(ping)
	guide_changed(node)


## Guides call this when their card shows, moves or hides: visible toasts re-check (deferred, once per frame).
static func guide_changed(from: Node) -> void:
	var tree := from.get_tree() if from != null and from.is_inside_tree() else Engine.get_main_loop() as SceneTree
	if tree != null:
		tree.call_group_flags(SceneTree.GROUP_CALL_DEFERRED | SceneTree.GROUP_CALL_UNIQUE, GROUP, "_on_guide_changed")


## Global rects of every guide card / spotlight on screen (empty = no guide up).
static func guide_rects(from: Node) -> Array:
	var out: Array = []
	if from == null or not from.is_inside_tree():
		return out
	for g in from.get_tree().get_nodes_in_group(GUIDE_GROUP):
		if g.has_method("guide_rects"):
			out.append_array(g.call("guide_rects"))
		elif g is Control and (g as Control).is_visible_in_tree():
			out.append((g as Control).get_global_rect())
	return out


static func guide_up(from: Node) -> bool:
	return not guide_rects(from).is_empty()


func _on_guide_changed() -> void:
	if not is_showing():
		return
	if not _speaks_over_guide(kind, priority) and guide_up(self):
		hide_toast()   # the guide is the feedback now
		return
	_place()


# ---------------------------------------------------------------- panels

func keep_clear(panel: Control, opts: Dictionary = {}) -> void:
	if panel == null:
		return
	for e in _clear:
		if e.node == panel:
			e.pad = float(opts.get("pad", PAD))
			e.foot = bool(opts.get("foot", false))
			_queue_place()
			return
	var bye := release.bind(panel)
	panel.tree_exiting.connect(bye)
	_clear.append({"node": panel, "pad": float(opts.get("pad", PAD)), "foot": bool(opts.get("foot", false)), "bye": bye})
	_rewatch()
	_queue_place()


## Stop keeping clear of `panel` (also automatic when it leaves the tree).
func release(panel: Control) -> void:
	for i in range(_clear.size() - 1, -1, -1):
		var e: Dictionary = _clear[i]
		if e.node != panel:
			continue
		if is_instance_valid(panel) and panel.tree_exiting.is_connected(e.bye):
			panel.tree_exiting.disconnect(e.bye)
		_clear.remove_at(i)
	_rewatch()
	_queue_place()


## The panels and their parents (a sheet that slides / resizes moves its head row without that row's own rect
## changing): one connection per node, re-made when the list changes.
func _rewatch() -> void:
	for w in _watched:
		if is_instance_valid(w):
			if (w as Control).item_rect_changed.is_connected(_queue_place):
				(w as Control).item_rect_changed.disconnect(_queue_place)
			if (w as Control).visibility_changed.is_connected(_queue_place):
				(w as Control).visibility_changed.disconnect(_queue_place)
	_watched.clear()
	for e in _clear:
		var n: Node = e.node
		var depth := 0
		while is_instance_valid(n) and n is Control and n != self and not n.is_ancestor_of(self) and depth < 12:
			if not _watched.has(n):
				(n as Control).item_rect_changed.connect(_queue_place)
				(n as Control).visibility_changed.connect(_queue_place)
				_watched.append(n)
			n = n.get_parent()
			depth += 1


func keep_clear_rect(id: String, r: Rect2) -> void:
	if r.has_area():
		_clear_rects[id] = r
	else:
		_clear_rects.erase(id)
	_queue_place()


func _queue_place() -> void:
	if _place_queued or not is_showing():
		return
	_place_queued = true
	_place_deferred.call_deferred()


func _place_deferred() -> void:
	_place_queued = false
	if is_showing():
		_place()


## [global rect, foot] of every visible panel to keep clear.
func _panel_rects() -> Array:
	var out: Array = []
	for e in _clear:
		var c: Control = e.node
		if is_instance_valid(c) and c.is_inside_tree() and c.is_visible_in_tree():
			var r := c.get_global_rect()
			if r.has_area():
				out.append([r.grow(float(e.pad)), bool(e.foot)])
	for id in _clear_rects:
		out.append([(_clear_rects[id] as Rect2).grow(PAD), false])
	return out


# ---------------------------------------------------------------- placement

func _set_slide(v: float) -> void:
	_slide = v
	if _card != null:
		_card.position = _rest + Vector2(0.0, _slide)


func _place() -> void:
	if _card == null:
		return
	var w := minf(max_width, size.x - 32.0)
	var extras := 56.0 + 18.0 + 46.0 + 2.0   # icon + gap + card margins
	var f: Font = _label.get_theme_font("font")
	var fs := _label.get_theme_font_size("font_size")
	# measure the text the label DRAWS: a game that hands over an English key gets its translation drawn (auto-translate),
	# and sizing the column from the key cut "Stromversorgung wiederhergestellt" to 4 lines + "…" (skiresort W66 10-27)
	var text_w := f.get_string_size(_label.atr(_label.text), HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x + 4.0
	_label.custom_minimum_size.x = minf(text_w, maxf(120.0, w - extras))
	_card.reset_size()
	_card.size = _card.get_combined_minimum_size()
	# below the status bar / cutout even when the host layer is full-screen (10-27; max, never added twice)
	var y := top
	if is_inside_tree():
		var gap := UiSafeArea.inner_gaps(UiSafeArea.overlay_edges(get_viewport_rect().size), get_global_rect(), get_viewport_rect().size)
		y = maxf(top, gap.y + 12.0) if gap.y > 0.0 else top
	var pos := Vector2(roundf((size.x - _card.size.x) / 2.0), y)
	var own := pos
	if is_inside_tree() and _card.visible:
		var guide: Array = guide_rects(self) if _speaks_over_guide(kind, priority) else []
		var panels := _panel_rects()
		if not guide.is_empty() or not panels.is_empty():
			var clear = _clear_spot(pos, guide, panels)
			if clear != null:
				pos = clear
			elif priority == "guide" and not guide.is_empty():
				hide_toast()   # a guide-priority line never covers the guide card / spotlight: with no clear spot it waits
				return
	_dodged = not pos.is_equal_approx(own)
	if _dodged and _slide != 0.0 and (_rest - pos).length() > 0.5:
		_slide = 0.0   # moved off a panel mid slide-in: no slide across it (the fade goes on)
	_rest = pos
	_card.position = _rest + Vector2(0.0, _slide)


## The nearest card spot (local) that covers no guide rect and no panel, inside the screen; null = none.
## Unchanged when the default spot is already clear (a game's own placement stays as it was).
func _clear_spot(pos: Vector2, guide: Array, panels: Array):
	var origin := get_global_rect().position
	var sz := _card.size
	var vp := get_viewport_rect().size
	var e := UiSafeArea.overlay_edges(vp)
	var screen := Rect2(e.x, e.y, vp.x - e.x - e.z, vp.y - e.y - e.w).grow(-4.0)
	var home := Rect2(origin + pos, sz)
	var lid := screen.position.y
	if ceiling >= 0.0:
		lid = maxf(lid, origin.y + ceiling)
	elif home.get_center().y < screen.get_center().y:
		lid = maxf(lid, home.position.y)   # an upper toast never climbs over the HUD above its row
	var blocks: Array = []   # [rect, foot]
	for g in guide:
		blocks.append([(g as Rect2).grow(PAD), false])
	blocks.append_array(panels)
	var ok := func(r: Rect2, skip_foot: Rect2) -> bool:
		if r.position.y < lid - 0.5 or not screen.grow(0.5).encloses(r):
			return false
		for b in blocks:
			if (b[0] as Rect2) == skip_foot:
				continue
			if r.intersects(b[0]):
				return false
		return true
	if ok.call(home, Rect2()):
		return pos
	var cands: Array = []
	for b in blocks:
		var o: Rect2 = b[0]
		cands.append(Vector2(home.position.x, o.end.y))
		cands.append(Vector2(home.position.x, o.position.y - sz.y))
		cands.append(Vector2(o.position.x - sz.x, home.position.y))
		cands.append(Vector2(o.end.x, home.position.y))
	# a candidate may also slide along the screen to clear a second block (centred under / over the first)
	var more: Array = []
	for c in cands:
		more.append(Vector2(clampf((c as Vector2).x, screen.position.x, screen.end.x - sz.x), (c as Vector2).y))
	cands.append_array(more)
	cands.sort_custom(func(a: Vector2, b: Vector2) -> bool: return a.distance_squared_to(home.position) < b.distance_squared_to(home.position))
	for c in cands:
		if ok.call(Rect2(c, sz), Rect2()):
			return (c as Vector2 - origin).round()
	# no room outside: a "foot" panel takes the toast at its foot, inside it
	for b in blocks:
		if b[1]:
			var o2: Rect2 = b[0]
			var c2 := Vector2(clampf(home.position.x, screen.position.x, screen.end.x - sz.x), o2.end.y - sz.y - PAD * 2.0)
			if ok.call(Rect2(c2, sz), o2):
				return (c2 - origin).round()
	return null
