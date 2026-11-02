extends Control
## coddy_ui coach mark (games/CLAUDE.md §9 "show, make them do it"): the screen dims except a rounded spotlight around the
## one control to touch, a brand ring pulses on a wrong tap, and a bubble with a pointer sits beside the spotlight
## (never over it, always inside the safe area): overline "Step 2 of 12", the instruction, a hand hint and "Skip guide".
## Pure drawing + one PanelContainer; the GAME keeps the guide logic (which step, what counts as done, blocking taps).
##   var coach := UiCoach.new(); guide_root.add_child(coach)
##   coach.setup({"skip": tr("Skip guide"), "hint": tr("Tap the highlighted button")})
##   coach.show_step(tr(text), tr("Step {n} of {total}").format(...), hole_rect_global)   # hole = Rect2() → no spotlight
##   coach.flash()                                 # wrong tap: the ring flashes
## Node names: Coach (the bubble PanelContainer), CoachSkip, CoachText, CoachStep.
## While the bubble is up every UiToast keeps quiet (ordinary messages) or keeps clear of the bubble + spotlight
## (warning / danger / priority "guide"): the coach is in the toast's guide group and pings the toasts when it changes.

signal skip_pressed

const Tokens = preload("ui_tokens.gd")
const Icons = preload("ui_icons.gd")
const UiSafeArea = preload("ui_safe_area.gd")
const UiToast = preload("ui_toast.gd")
const GAP := 26.0
const POINTER := 16.0

var bubble: PanelContainer
var text_label: Label
var step_label: Label
var hint_label: Label
var skip_button: Button
var hole := Rect2()               ## spotlight, global (screen) coordinates
var dim := 0.62
var safe := Rect2()               ## where the bubble may go (global); empty = this control's rect
## Global rects the bubble should not cover besides the hole (a map marker the step text points at, a second control).
## Best effort: when no spot clears them all, the bubble is placed as without them (it never covers the hole).
var avoid: Array[Rect2] = []
var _flash := 0.0
var _side := ""                   ## where the bubble is relative to the hole: left / right / above / below / ""
var _ring: StyleBoxFlat
var _hint_icon: TextureRect
var _with_hint := true
var _told := ""                   ## the bubble + hole the toasts last heard about


func _init() -> void:
	name = "CoachLayer"
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_to_group(UiToast.GUIDE_GROUP)


func setup(opts: Dictionary = {}) -> void:
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	bubble = PanelContainer.new()
	bubble.name = "Coach"
	bubble.theme_type_variation = "Bubble"
	bubble.mouse_filter = Control.MOUSE_FILTER_STOP
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", 8)
	bubble.add_child(v)
	var top := HBoxContainer.new()
	top.add_theme_constant_override("separation", 12)
	step_label = kit.label("", "Overline")
	step_label.name = "CoachStep"
	step_label.add_theme_color_override("font_color", kit.c("primary"))
	step_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	step_label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	top.add_child(step_label)
	skip_button = kit.button(str(opts.get("skip", "Skip")), "ghost", "", func() -> void: skip_pressed.emit())
	skip_button.name = "CoachSkip"
	skip_button.add_theme_font_size_override("font_size", 23)
	skip_button.custom_minimum_size.y = 72
	top.add_child(skip_button)
	v.add_child(top)
	text_label = kit.label("", "Title", true)
	text_label.name = "CoachText"
	text_label.add_theme_font_size_override("font_size", 28)
	v.add_child(text_label)
	var foot := HBoxContainer.new()
	foot.add_theme_constant_override("separation", 10)
	_hint_icon = Icons.rect("hand", 24, kit.c("on_surface_muted"))
	foot.add_child(_hint_icon)
	hint_label = kit.label(str(opts.get("hint", "")), "CaptionMuted", false)
	hint_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	hint_label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	hint_label.clip_text = true
	hint_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	foot.add_child(hint_label)
	v.add_child(foot)
	add_child(bubble)
	_ring = StyleBoxFlat.new()
	_ring.draw_center = false
	_ring.set_border_width_all(4)
	_ring.set_corner_radius_all(18)
	_ring.anti_aliasing = true
	bubble.visible = false
	bubble.visibility_changed.connect(_tell_toasts)   # a game may show / hide the bubble itself


func relabel(skip_text: String, hint_text: String) -> void:
	skip_button.text = skip_text
	hint_label.text = hint_text


## text: the instruction; step: the translated "Step n of N" ("" hides it); spot: global rect of the control (or empty).
func show_step(text: String, step: String, spot: Rect2, with_hint: bool = true, grow: float = 8.0) -> void:
	text_label.text = text
	load("res://addons/coddy_ui/ui_kit.gd").upper_text(step_label, step)   # translated text: never auto-translated back (10-27)
	step_label.visible = step != ""
	_with_hint = with_hint
	bubble.visible = true
	set_hole(spot, grow)


func set_hole(spot: Rect2, grow: float = 8.0) -> void:
	hole = spot.grow(grow) if spot.has_area() else Rect2()
	if hint_label != null:
		(hint_label.get_parent() as Control).visible = _with_hint and hole.has_area() and hint_label.text != ""   # "tap here" only with a spotlight
	_place()
	queue_redraw()


func hide_step() -> void:
	bubble.visible = false
	hole = Rect2()
	queue_redraw()
	_tell_toasts()


func flash() -> void:
	_flash = 0.6
	set_process(true)
	queue_redraw()


func _ready() -> void:
	set_process(false)
	resized.connect(_place)
	visibility_changed.connect(_tell_toasts)
	tree_exited.connect(_tell_toasts)


func _process(delta: float) -> void:
	_flash = maxf(0.0, _flash - delta)
	queue_redraw()
	if _flash <= 0.0:
		set_process(false)


func _area() -> Rect2:
	var r := safe if safe.has_area() else get_global_rect()
	# never under the system bars (status bar / cutout on top, navigation bar), whatever rect the game passed (10-27)
	var vp := get_viewport_rect().size
	var e := UiSafeArea.overlay_edges(vp)
	if e != Vector4.ZERO:
		var dev := Rect2(e.x, e.y, vp.x - e.x - e.z, vp.y - e.y - e.w)
		if dev.has_area() and r.intersects(dev):
			r = r.intersection(dev)
	return r.grow(-16.0)


## Bubble beside the spotlight: the side with the most room that keeps it whole inside the safe area and off the hole.
func _place() -> void:
	if bubble == null or not bubble.visible or not is_inside_tree():
		return   # #100: same family as the pager — no global rect outside the tree
	var area := _area()
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	var portrait: bool = kit.portrait(self)
	var tries: Array = []
	if hole.has_area():
		var left_room := hole.position.x - area.position.x - GAP
		var right_room := area.end.x - hole.end.x - GAP
		# beside the spotlight the bubble is a narrow column (≤ 45 % of the screen, text wraps), above / below it may be wide
		var cap := minf(600.0, area.size.x * 0.45)
		if not portrait:
			var sides := [["left", left_room], ["right", right_room]]
			sides.sort_custom(func(x: Array, y: Array) -> bool: return x[1] > y[1])
			for sd in sides:
				if sd[1] >= 300.0:
					tries.append([sd[0], minf(cap, sd[1])])
		var vertical := ["above", "below"] if hole.get_center().y > area.get_center().y else ["below", "above"]
		for v in vertical:
			tries.append([v, minf(720.0, area.size.x)])
	tries.append(["", minf(720.0, area.size.x * (1.0 if portrait else 0.58))])
	for pass_i in (2 if not avoid.is_empty() else 1):
		for t in tries:
			if _try_place(t, area, pass_i == 0 and not avoid.is_empty()):
				return
	_side = ""
	var sz2 := Vector2(maxf(minf(720.0, area.size.x), bubble.get_combined_minimum_size().x), _bubble_height())
	_apply(Vector2(area.position.x, area.end.y - sz2.y), sz2)


## One placement try [side, width]: the natural spot first, then (only while dodging `avoid`) the same side slid to the
## area's other edges. True when the bubble was placed.
func _try_place(t: Array, area: Rect2, dodge: bool) -> bool:
	var w: float = t[1]
	# An autowrap label reports the height of its LAST width and parents cache it: give it the new width first
	# (size, then invalidate), so the measurement below is for the width the bubble will really have.
	var tw := w - 56.0
	text_label.custom_minimum_size = Vector2.ZERO
	text_label.size = Vector2(tw, 1.0)
	text_label.update_minimum_size()
	text_label.custom_minimum_size = Vector2(tw, 0.0)
	(bubble.get_child(0) as Control).update_minimum_size()
	bubble.update_minimum_size()
	var sz := Vector2(maxf(w, bubble.get_combined_minimum_size().x), _bubble_height())   # the REAL width: a game font can make the rows wider than asked
	var pos := _pos_for(str(t[0]), sz, area)
	var spots: Array = [pos]
	if dodge:
		if str(t[0]) in ["left", "right"]:
			spots.append(Vector2(pos.x, area.position.y))
			spots.append(Vector2(pos.x, area.end.y - sz.y))
		else:
			spots.append(Vector2(area.position.x, pos.y))
			spots.append(Vector2(area.end.x - sz.x, pos.y))
	for p in spots:
		var r := Rect2(p, sz)
		if not area.grow(1.0).encloses(r) or (hole.has_area() and r.intersects(hole.grow(GAP * 0.5))):
			continue
		if dodge and avoid.any(func(a: Rect2) -> bool: return r.intersects(a)):
			continue
		_side = str(t[0])
		_apply(p, sz)
		return true
	return false


func _apply(pos: Vector2, sz: Vector2) -> void:
	bubble.update_minimum_size()
	bubble.size = sz
	bubble.global_position = pos
	load("res://addons/coddy_ui/ui_kit.gd").settle(bubble)
	queue_redraw()
	_tell_toasts()


## Height from the parts (Control minimum-size caches of parents refresh deferred; the label was just re-measured).
func _bubble_height() -> float:
	var v := bubble.get_child(0) as VBoxContainer
	var h := bubble.get_theme_stylebox("panel").get_minimum_size().y
	var n := 0
	for ch in v.get_children():
		if (ch as Control).visible:
			h += (ch as Control).get_combined_minimum_size().y
			n += 1
	return h + maxi(0, n - 1) * v.get_theme_constant("separation")


func _pos_for(side: String, sz: Vector2, area: Rect2) -> Vector2:
	var c := hole.get_center()
	match side:
		"left":
			return Vector2(hole.position.x - GAP - sz.x, clampf(c.y - sz.y / 2.0, area.position.y, area.end.y - sz.y))
		"right":
			return Vector2(hole.end.x + GAP, clampf(c.y - sz.y / 2.0, area.position.y, area.end.y - sz.y))
		"above":
			return Vector2(clampf(c.x - sz.x / 2.0, area.position.x, area.end.x - sz.x), hole.position.y - GAP - sz.y)
		"below":
			return Vector2(clampf(c.x - sz.x / 2.0, area.position.x, area.end.x - sz.x), hole.end.y + GAP)
	# no spotlight: bottom-left of the play area (never centred over the HUD)
	return Vector2(area.position.x, area.end.y - sz.y)


func bubble_rect() -> Rect2:
	return bubble.get_global_rect() if bubble != null and bubble.visible else Rect2()


## UiToast: the guide card and its spotlight on screen (global); empty while the bubble is not up.
func guide_rects() -> Array:
	if bubble == null or not is_inside_tree() or not bubble.is_visible_in_tree():
		return []
	var out: Array = [bubble.get_global_rect()]
	if hole.has_area():
		out.append(hole)
	return out


## Visible toasts re-check (only when the bubble / hole really changed: a game may re-send the same step every tick).
func _tell_toasts() -> void:
	var key := str(guide_rects()) if is_inside_tree() else ""
	if key == _told:
		return
	_told = key
	UiToast.guide_changed(self)


func _draw() -> void:
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	var off := -global_position
	if hole.has_area():
		var full := Rect2(off, get_viewport_rect().size)
		var h := Rect2(hole.position + off, hole.size).intersection(full)
		var col := Color(kit.c("scrim"), dim)
		draw_rect(Rect2(full.position, Vector2(full.size.x, h.position.y - full.position.y)), col)
		draw_rect(Rect2(Vector2(full.position.x, h.end.y), Vector2(full.size.x, full.end.y - h.end.y)), col)
		draw_rect(Rect2(Vector2(full.position.x, h.position.y), Vector2(h.position.x - full.position.x, h.size.y)), col)
		draw_rect(Rect2(Vector2(h.end.x, h.position.y), Vector2(full.end.x - h.end.x, h.size.y)), col)
		var ring: Color = kit.c("primary")
		if _flash > 0.0:
			ring = ring.lerp(Color.WHITE, absf(sin(_flash * 20.0)) * 0.7)
		_ring.border_color = ring
		draw_style_box(_ring, h.grow(4.0))
	if bubble == null or not bubble.visible or _side == "":
		return
	var b := Rect2(bubble.global_position + off, bubble.size)
	var c := hole.get_center() + off
	var bg: Color = kit.c("surface_high")
	var tri: PackedVector2Array
	match _side:
		"left":
			var y := clampf(c.y, b.position.y + 28, b.end.y - 28)
			tri = PackedVector2Array([Vector2(b.end.x - 1, y - POINTER), Vector2(b.end.x + POINTER, y), Vector2(b.end.x - 1, y + POINTER)])
		"right":
			var y2 := clampf(c.y, b.position.y + 28, b.end.y - 28)
			tri = PackedVector2Array([Vector2(b.position.x + 1, y2 - POINTER), Vector2(b.position.x - POINTER, y2), Vector2(b.position.x + 1, y2 + POINTER)])
		"above":
			var x := clampf(c.x, b.position.x + 32, b.end.x - 32)
			tri = PackedVector2Array([Vector2(x - POINTER, b.end.y - 1), Vector2(x, b.end.y + POINTER), Vector2(x + POINTER, b.end.y - 1)])
		"below":
			var x2 := clampf(c.x, b.position.x + 32, b.end.x - 32)
			tri = PackedVector2Array([Vector2(x2 - POINTER, b.position.y + 1), Vector2(x2, b.position.y - POINTER), Vector2(x2 + POINTER, b.position.y + 1)])
	if tri.size() == 3:
		draw_colored_polygon(tri, bg)
