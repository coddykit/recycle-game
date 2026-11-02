extends RefCounted
## coddy_ui TouchScroll regression suite (games/CLAUDE.md §16 gate 2) — device-like input: every event goes through
## Input.parse_input_event, so the phone's emulated mouse copy of each touch is produced exactly as on Android.
## Kit fixtures (vertical list of buttons / switches / pickers / rows, horizontal strip of cards, a list under a scrim,
## a list rebuilt every frame) run in en + de; a game adds its REAL lists with the same probes:
##   const Suite = preload("res://addons/coddy_ui/tests/touch_scroll_suite.gd")
##   var s := Suite.new(self)                               # self = the SceneTree test script
##   await s.run_fixtures(["en", "de"])                      # kit fixtures
##   await s.drag_scrolls(m.list_scroll, some_button, "invest list")      # the game's own list, drag starting on a button
##   await s.tap_clicks(button, func(): return m.selected == 3, "ship row")
##   await s.tap_survives_refresh(button, func(): m.refresh(), func(): return bought(), "invest during refresh")
##   s.report("TOUCH_SCROLL")                                # prints "TOUCH_SCROLL: n checks, f failures"
## Each probe returns true on success and records a failure line otherwise.

const UiKit = preload("../ui_kit.gd")
const UiScroll = preload("../ui_scroll.gd")
const UiRow = preload("../ui_row.gd")
const UiReconcile = preload("../ui_reconcile.gd")
const TouchScroll = preload("../touch_scroll.gd")

var tree: SceneTree
var checks := 0
var failures: Array = []
const STRINGS := {
	"en": {"item": "Upgrade tug fleet", "toggle": "Music", "pick": "Language", "card": "Container ship"},
	"de": {"item": "Schlepperflotte ausbauen und erweitern", "toggle": "Hintergrundmusik", "pick": "Sprache der Benutzeroberfläche", "card": "Containerschiff Nord"},
}


## Watchdog (Patron 10-25: the suite hung three times in a day, a Godot process at 2 % CPU that never quit and blocked
## check.sh for hours): WATCHDOG_S after the suite is made the whole test run fails loudly and quits(1). Set
## TOUCH_SCROLL_WATCHDOG_S in the environment for a longer game run; 0 switches it off.
const WATCHDOG_S := 300.0


func _init(p_tree: SceneTree) -> void:
	tree = p_tree
	var limit := float(OS.get_environment("TOUCH_SCROLL_WATCHDOG_S")) if OS.get_environment("TOUCH_SCROLL_WATCHDOG_S") != "" else WATCHDOG_S
	if limit > 0.0 and tree != null:
		# process_always + ignore_time_scale: a paused tree or a slowed clock cannot stop it
		tree.create_timer(limit, true, false, true).timeout.connect(func() -> void:
			print("TOUCH_SCROLL_TESTS: TIMEOUT after %.0f s (watchdog) — %d checks, 1 failures" % [limit, checks])
			tree.quit(1))


func check(ok: bool, label: String) -> bool:
	checks += 1
	if not ok:
		failures.append(label)
		print("FAIL TOUCH ", label)
	return ok


func report(tag: String = "TOUCH_SCROLL") -> int:
	print("%s: %d checks, %d failures" % [tag, checks, failures.size()])
	return failures.size()


# ---------------------------------------------------------------- device-like events

func _send(ev: InputEvent) -> void:
	Input.parse_input_event(ev)
	Input.flush_buffered_events()


func touch(pos: Vector2, down: bool) -> void:
	var t := InputEventScreenTouch.new()
	t.index = 0
	t.pressed = down
	t.position = pos
	_send(t)


func drag_to(from: Vector2, to: Vector2, pos: Vector2) -> void:
	var d := InputEventScreenDrag.new()
	d.index = 0
	d.position = pos
	d.relative = (to - from) / 8.0
	_send(d)


## A finger drag: down at `a`, `steps` moves to `b` (one frame each), up. fast = no pause before lifting (flings).
func swipe(a: Vector2, b: Vector2, steps: int = 8, fast: bool = false) -> void:
	touch(a, true)
	await tree.process_frame
	for i in range(1, steps + 1):
		drag_to(a, b, a.lerp(b, float(i) / steps))
		await tree.process_frame
	if not fast:
		await tree.create_timer(0.2).timeout   # a resting finger: no fling on lift
	touch(b, false)
	await tree.process_frame


func tap(at: Vector2) -> void:
	touch(at, true)
	await tree.process_frame
	touch(at, false)
	await tree.process_frame


static func centre(c: Control) -> Vector2:
	return c.get_global_rect().get_center()


## Stops any fling on `sc`'s viewport and puts the list back at the start.
func reset(sc: ScrollContainer) -> void:
	for ts in TouchScroll.instances_for(sc):
		ts._stop_fling()
	sc.scroll_vertical = 0
	sc.scroll_horizontal = 0
	await tree.process_frame


# ---------------------------------------------------------------- probes (usable on a game's real lists)

## A drag that starts on `start` (a button / card / switch inside `sc`) scrolls `sc` and presses nothing.
func drag_scrolls(sc: ScrollContainer, start: Control, label: String, horizontal: bool = false) -> bool:
	var presses := [0]
	var cb := func() -> void: presses[0] += 1
	var b := start as BaseButton
	if b != null:
		b.pressed.connect(cb)
	var was_on := b.button_pressed if b != null and b.toggle_mode else false
	var before := sc.scroll_horizontal if horizontal else sc.scroll_vertical
	var a := centre(start)
	var dist := minf(260.0, (sc.size.x if horizontal else sc.size.y) * 0.45)
	var to := a - (Vector2(dist, 0) if horizontal else Vector2(0, dist))
	await swipe(a, to)
	var after := sc.scroll_horizontal if horizontal else sc.scroll_vertical
	if b != null and b.pressed.is_connected(cb):
		b.pressed.disconnect(cb)
	var ok := check(after > before + 40, "%s: drag on %s scrolls (%d → %d)" % [label, start.get_class(), before, after])
	ok = check(presses[0] == 0, "%s: the drag presses nothing (%d)" % [label, presses[0]]) and ok
	if b != null and b.toggle_mode:
		ok = check(b.button_pressed == was_on, "%s: the drag does not flip the switch" % label) and ok
	return ok


## A quick flick keeps the list moving after the finger is up (inertia).
func flick_coasts(sc: ScrollContainer, start: Control, label: String, horizontal: bool = false) -> bool:
	await reset(sc)
	var a := centre(start)
	var to := a - (Vector2(160, 0) if horizontal else Vector2(0, 160))
	touch(a, true)
	await tree.process_frame
	for i in range(1, 5):
		drag_to(a, to, a.lerp(to, i / 4.0))
		await tree.process_frame
	var at_lift := sc.scroll_horizontal if horizontal else sc.scroll_vertical
	touch(to, false)
	var flinging := false
	for ts in TouchScroll.instances_for(sc):
		if ts._fling != null:
			flinging = true
			ts.settle()   # run the coast deterministically (headless frames are not 1/60 s)
	await tree.process_frame
	var later := sc.scroll_horizontal if horizontal else sc.scroll_vertical
	check(flinging, "%s: a quick flick starts a fling" % label)
	return check(later > at_lift + 8, "%s: a flick coasts on after lift (%d → %d)" % [label, at_lift, later])


## A short tap on `b` fires it once and `verify` (optional) sees the effect.
func tap_clicks(b: BaseButton, verify: Callable, label: String) -> bool:
	var presses := [0]
	var cb := func() -> void: presses[0] += 1
	b.pressed.connect(cb)
	await tap(centre(b))
	if is_instance_valid(b) and b.pressed.is_connected(cb):
		b.pressed.disconnect(cb)
	var ok := check(presses[0] == 1, "%s: a tap presses once (%d)" % [label, presses[0]])
	if verify.is_valid():
		ok = check(bool(verify.call()), "%s: the tap did its job" % label) and ok
	return ok


## Finger down on `b`, `refresh` runs every frame for `frames` frames (the game's 1–2 Hz list refresh, money ticking),
## finger up: the press must land (verify) and the pressed node must still be the one in the tree.
func tap_survives_refresh(b: BaseButton, refresh: Callable, verify: Callable, label: String, frames: int = 6) -> bool:
	var at := centre(b)
	touch(at, true)
	await tree.process_frame
	for i in frames:
		refresh.call()
		await tree.process_frame
	var alive := is_instance_valid(b) and b.is_inside_tree()
	touch(at, false)
	await tree.process_frame
	var ok := check(alive, "%s: the held button is not freed by the refresh" % label)
	return check(bool(verify.call()), "%s: the tap held across refreshes lands" % label) and ok


## A list under a full-screen scrim / modal must not scroll when the finger drags on the scrim.
func blocked_under(sc: ScrollContainer, over: Control, label: String) -> bool:
	var before := sc.scroll_vertical
	var a := centre(sc)
	await swipe(a, a - Vector2(0, 200))
	return check(sc.scroll_vertical == before, "%s: the list under %s does not scroll" % [label, over.name])


# ---------------------------------------------------------------- kit fixtures

## Canvas = window pixels for the fixture (so a portrait fixture gets portrait-sized events). Games keep their own setup.
func _host(size: Vector2i) -> Control:
	tree.root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	tree.root.size = size
	var host := Control.new()
	host.name = "TouchFixture"
	host.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	host.theme = UiKit.use("fleet")
	tree.root.add_child(host)
	return host


func run_fixtures(locales: Array = ["en", "de"]) -> int:
	for loc in locales:
		for size in [Vector2i(1280, 720), Vector2i(720, 1280)]:
			await _fixture(str(loc), size)
	return failures.size()


func _fixture(loc: String, size: Vector2i) -> void:
	var s: Dictionary = STRINGS.get(loc, STRINGS.en)
	var tag := "%s %dx%d" % [loc, size.x, size.y]
	var host := _host(size)
	var parts: Array = UiScroll.list(12)
	var sc: ScrollContainer = parts[0]
	var box: VBoxContainer = parts[1]
	sc.position = Vector2(40, 40)
	sc.size = Vector2(minf(560.0, size.x - 80.0), size.y * 0.6)
	host.add_child(sc)
	var taps := [0]
	var first_button: Button = null
	var a_toggle: Button = null
	var a_pick: OptionButton = null
	var a_card: Button = null
	for i in 24:
		match i % 4:
			0:
				var b := UiKit.button("%s %d" % [s.item, i], "secondary", "trending-up", func() -> void: taps[0] += 1)
				b.name = "Item_%d" % i
				box.add_child(b)
				if first_button == null:
					first_button = b
			1:
				var t := UiRow.toggle("music", "%s %d" % [s.toggle, i], false)
				t.name = "Toggle_%d" % i
				box.add_child(t)
				if a_toggle == null:
					a_toggle = t
			2:
				var o := OptionButton.new()
				o.name = "Pick_%d" % i
				o.add_item(s.pick)
				o.add_item("English")
				o.add_item("Deutsch")
				o.custom_minimum_size.y = 84
				box.add_child(o)
				if a_pick == null:
					a_pick = o
			3:
				var r := UiRow.item("ship", "accent", "%s %d" % [s.card, i], "North port")
				r.name = "Card_%d" % i
				box.add_child(r)
				if a_card == null:
					a_card = r
	for i in 4:
		await tree.process_frame
	check(UiScroll.wired(sc), tag + ": UiScroll list is wired")
	check(sc.get_v_scroll_bar().modulate.a <= 0.01 or sc.get_v_scroll_bar().visible, tag + ": thumb starts faded")
	for c in [first_button, a_toggle, a_pick, a_card]:
		await reset(sc)
		await drag_scrolls(sc, c, "%s vertical (%s)" % [tag, c.name], false)
	check(not a_pick.get_popup().visible, tag + ": a drag on a picker does not open it")
	await flick_coasts(sc, first_button, tag + " vertical")
	await reset(sc)
	await tap_clicks(first_button, func() -> bool: return taps[0] == 1, tag + " button")
	await tap_clicks(a_toggle, func() -> bool: return a_toggle.on, tag + " switch row")
	# a list rebuilt (same structure → patched in place) every frame while the finger is down
	var count := [0]
	var target: Button = box.get_node("Item_0")
	target.pressed.connect(func() -> void: count[0] += 1)
	var rebuild := func() -> void:
		var fresh := VBoxContainer.new()
		for ch in box.get_children():
			var copy: Node
			if ch is OptionButton:
				copy = OptionButton.new()
			elif ch.has_method("copy_from"):
				copy = UiRow.item("ship", "accent", "x", "y") if ch.style == "card" else UiRow.toggle("music", "x", false)
			else:
				copy = Button.new()
				(copy as Button).text = (ch as Button).text + "·"
				for cn in (ch as Button).pressed.get_connections():
					(copy as Button).pressed.connect(cn.callable)
			copy.name = ch.name
			fresh.add_child(copy)
		UiReconcile.apply(box, fresh, sc)
	await tap_survives_refresh(target, rebuild, func() -> bool: return count[0] == 1, tag + " refresh")
	# horizontal strip of cards
	var hp: Array = UiScroll.strip(14)
	var hs: ScrollContainer = hp[0]
	var hb: HBoxContainer = hp[1]
	hs.position = Vector2(40, size.y * 0.6 + 70)
	hs.size = Vector2(size.x - 80, 150)
	host.add_child(hs)
	var first_card: Button = null
	for i in 14:
		var r := UiRow.item("fish", "primary", "%s %d" % [s.card, i], "", Callable(), "none")
		r.custom_minimum_size = Vector2(300, 120)
		r.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
		hb.add_child(r)
		if first_card == null:
			first_card = r
	for i in 3:
		await tree.process_frame
	await reset(hs)
	await drag_scrolls(hs, first_card, tag + " horizontal", true)
	await flick_coasts(hs, first_card, tag + " horizontal", true)
	# a scrim (open window) over the list: dragging on it must not scroll the list behind
	await reset(sc)
	var shade := UiKit.scrim()
	host.add_child(shade)
	await tree.process_frame
	await blocked_under(sc, shade, tag)
	host.queue_free()
	await tree.process_frame
	await tree.process_frame
