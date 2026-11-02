extends Node
## Finger scrolling of the real Borderline Empire screens (owner phone report 2026-10-19: lists do not follow a finger).
## Sends what a phone sends (for every touch event the emulated mouse event first, then the touch, as Godot does on Android)
## through the viewport to the REAL sheets, modals and the job strip and checks that a drag that starts on a button or card
## scrolls the list, a tap still presses, a scroll never ends in a press, and every job of the strip can be reached.
## Run: godot --headless --path . res://tests/touch_scroll_test.tscn -- --lang=tr
## check.sh runs en/tr/de/ja in separate processes, so a previous scene/language
## cannot leave delayed UI refreshes pointing at a freed scroll list.
const TestTouchScroll = preload("res://addons/coddy_ui/touch_scroll.gd")

var failures := 0
var checks := 0
var presses := 0


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		print("TOUCHSCROLL FAIL ", label)
	elif "--verbose-checks" in OS.get_cmdline_user_args():
		print("TOUCHSCROLL ok ", label)


func _frames(n := 6) -> void:
	for i in n:
		await get_tree().process_frame


func _settle(seconds := 0.5) -> void:
	await get_tree().create_timer(seconds, true, false, true).timeout
	await get_tree().process_frame


func _ready() -> void:
	get_tree().create_timer(280.0).timeout.connect(func(): get_tree().quit(2))
	await get_tree().process_frame
	get_tree().root.size = Vector2i(540, 960)   # a phone: the 720 x 1280 canvas
	await get_tree().process_frame
	SaveManager.delete_save()
	var langs := ["en"]
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--lang="): langs = [arg.trim_prefix("--lang=")]
	for lang in langs:
		await _one_language(lang)
	SaveManager.delete_save()
	TranslationServer.set_locale("en")
	print("TOUCHSCROLL checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)


func _one_language(lang: String) -> void:
	seed(12345)
	GameManager.settings["language"] = lang
	GameManager.settings["quality_notice"] = ""
	GameManager.apply_language()
	GameManager.new_game()
	var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	await _frames(6)
	var hud: Node = main.hud
	# Gesture fixture: deliveries/events must not replace cards or open modals
	# between a forward and reverse swipe. UI frames/animations keep running.
	GameManager.running = false
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	ProgressionManager.flags["intro_done"] = true
	ProgressionManager.flags["tabs_all"] = true
	TutorialManager.skip()
	hud._restore_game_chrome()
	hud._modal_queue.clear()
	hud._offer_toast_cd = 1e12
	EconomyManager.earn(5e7, "test", false)
	EconomyManager.add_reputation(900)
	for r in range(1, 5):
		RegionManager.unlock(r)
	for i in 4:
		ProgressionManager.upgrade_garage()
	for i in 12:
		if VehicleManager.buy(["small_van", "armored"][i % 2]).is_empty():
			break
	for i in 3:
		ContractManager.refresh_offers()
	await _settle(0.6)
	hud._modal_queue.clear()
	for m in hud.modal_layer.get_children():
		m.queue_free()
	hud._modal_open = false
	Iap._test_setup({"v": 1, "owned": {}, "any": false, "pending": [], "seen": [], "delivered": {}}, true, true)
	ProgressionManager.flags["legacy"] = 2
	var screens := ["contracts", "fleet", "fleet:Showroom", "fleet:Drivers", "fleet:detail", "map", "upgrades", "business", "settings", "shop", "daily"]
	for oi in mini(2, ContractManager.offers.size()):
		screens.append("dispatch:%d" % oi)
	for s in screens:
		var arg: Variant = null
		var name: String = s
		if s.begins_with("dispatch:"):
			name = "dispatch"
			arg = ContractManager.offers[int(s.get_slice(":", 1))].id
		elif s == "fleet:Showroom" or s == "fleet:Drivers":
			name = "fleet"
			arg = {"tab": s.get_slice(":", 1)}
		elif s == "fleet:detail":
			name = "fleet"
			arg = {"vehicle": VehicleManager.vehicles[0].id}
		UIManager.open_panel(name, arg)
		await _settle(0.55)
		await _lists(lang + " " + s)
		UIManager.close_panel()
		await _settle(0.4)
	for modal_case in ["offer:starter", "offer:legacy_pack", "feedback"]:
		if modal_case == "feedback":
			hud.show_feedback()
		else:
			hud._show_offer(modal_case.get_slice(":", 1))
		hud._show_next_modal()
		await _settle(0.8)
		await _lists(lang + " " + modal_case)
		for m in hud.modal_layer.get_children():
			m.queue_free()
		hud._modal_queue.clear()
		hud._modal_open = false
		await _frames(3)
	await _job_strip(hud, lang)
	main.queue_free()
	await _frames(4)


# ------------------------------------------------------------ events ---

func _touch(pos: Vector2, pressed: bool) -> InputEventScreenTouch:
	var t := InputEventScreenTouch.new()
	t.index = 0
	t.position = pos
	t.pressed = pressed
	return t


func _drag(pos: Vector2, rel: Vector2) -> InputEventScreenDrag:
	var d := InputEventScreenDrag.new()
	d.index = 0
	d.position = pos
	d.relative = rel
	return d


func _mouse(pos: Vector2, pressed: bool) -> InputEventMouseButton:
	var m := InputEventMouseButton.new()
	m.device = InputEvent.DEVICE_ID_EMULATION
	m.button_index = MOUSE_BUTTON_LEFT
	m.pressed = pressed
	m.position = pos
	m.global_position = pos
	if pressed:
		m.button_mask = MOUSE_BUTTON_MASK_LEFT
	return m


func _motion(pos: Vector2, rel: Vector2) -> InputEventMouseMotion:
	var m := InputEventMouseMotion.new()
	m.device = InputEvent.DEVICE_ID_EMULATION
	m.position = pos
	m.global_position = pos
	m.relative = rel
	m.button_mask = MOUSE_BUTTON_MASK_LEFT
	return m


func _swipe(a: Vector2, delta: Vector2, steps: int = 8) -> void:
	_swipe_on(get_tree().root, a, delta, steps)


## Also used by tests/tool_job_strip_shot.gd.
func _swipe_on(vp: Viewport, a: Vector2, delta: Vector2, steps: int = 8) -> void:
	vp.push_input(_mouse(a, true), true)
	vp.push_input(_touch(a, true), true)
	var prev := a
	for i in range(1, steps + 1):
		var p := a + delta * (float(i) / steps)
		vp.push_input(_motion(p, p - prev), true)
		vp.push_input(_drag(p, p - prev), true)
		prev = p
	vp.push_input(_mouse(prev, false), true)
	vp.push_input(_touch(prev, false), true)


# -------------------------------------------------------------- lists ---

## The control a finger lands on at `p`: the last one drawn (highest canvas layer, then latest in tree order) that takes mouse input.
func _top_hit(p: Vector2) -> Control:
	var best: Control = null
	var best_key := Vector2(-1e9, -1e9)
	var order := 0
	var stack: Array = [get_tree().root]
	while not stack.is_empty():
		var n: Node = stack.pop_back()
		order += 1
		if n is Control:
			var c := n as Control
			if c.is_visible_in_tree() and c.mouse_filter != Control.MOUSE_FILTER_IGNORE and c.get_global_rect().has_point(p):
				var layer := 0
				var a: Node = c.get_parent()
				while a != null:
					if a is CanvasLayer:
						layer = (a as CanvasLayer).layer
						break
					a = a.get_parent()
				if layer > best_key.x or (layer == best_key.x and order > best_key.y):
					best_key = Vector2(layer, order)
					best = c
		var kids := n.get_children()
		kids.reverse()
		stack.append_array(kids)
	return best


func _button_in_view(sc: ScrollContainer, n: Node) -> BaseButton:
	if n is BaseButton and (n as BaseButton).is_visible_in_tree() and not (n as BaseButton).disabled and not (n is OptionButton):
		var c := (n as BaseButton).get_global_rect().get_center()
		if sc.get_global_rect().has_point(c) and (n as Control).size.x > 8:
			return n as BaseButton
	for ch in n.get_children():
		var b := _button_in_view(sc, ch)
		if b != null:
			return b
	return null


func _count_presses(n: Node) -> void:
	if n is BaseButton and not (n as BaseButton).has_meta("probe"):
		(n as BaseButton).set_meta("probe", true)
		(n as BaseButton).pressed.connect(func(): presses += 1)
	for ch in n.get_children():
		_count_presses(ch)


## Every list on screen right now (vertical, or sideways like the vehicle chooser): a finger that starts on a button (or on
## the middle of a card) scrolls it.
func _lists(where: String) -> void:
	var found := 0
	for c in get_tree().root.find_children("*", "ScrollContainer", true, false):
		var sc := c as ScrollContainer
		if not sc.is_visible_in_tree() or sc.size.y < 40 or sc.mouse_filter == Control.MOUSE_FILTER_IGNORE:
			continue
		var horiz := sc.vertical_scroll_mode == ScrollContainer.SCROLL_MODE_DISABLED
		if horiz and sc.horizontal_scroll_mode == ScrollContainer.SCROLL_MODE_DISABLED:
			continue
		var bar: ScrollBar = sc.get_h_scroll_bar() if horiz else sc.get_v_scroll_bar()
		var span := float(bar.max_value - bar.page)
		if span < 60.0:
			continue
		# Sheets restore their previous scroll offset; a forward drag needs room.
		for ts in TestTouchScroll.instances_for(sc): ts._stop_fling()
		sc.scroll_horizontal = 0
		sc.scroll_vertical = 0
		await _frames(3)
		_count_presses(sc)
		var b := _button_in_view(sc, sc)
		var from := sc.get_global_rect().get_center() if b == null else b.get_global_rect().get_center()
		var hit := _top_hit(from)
		if hit == null or not (sc.is_ancestor_of(hit) or hit == sc):
			continue # another screen is drawn over this list: a finger cannot reach it
		found += 1
		var label := "%s [%s]" % [where, sc.get_parent().name]
		var room := mini(int(span), 240)
		var axis := Vector2(1, 0) if horiz else Vector2(0, 1)
		var before := sc.scroll_horizontal if horiz else sc.scroll_vertical
		presses = 0
		_swipe(from, -axis * float(room))
		await _frames(3)
		check(is_instance_valid(sc), label + ": gesture keeps its scroll list alive")
		if not is_instance_valid(sc): return
		var moved := (sc.scroll_horizontal if horiz else sc.scroll_vertical) - before
		check(moved >= room * 0.6, "%s: drag from %s scrolls the %s list (%d of %d px)" % [label, "a button" if b != null else "a card", "sideways" if horiz else "vertical", moved, room])
		check(presses == 0, label + ": a scroll drag presses nothing")
		var back := sc.scroll_horizontal if horiz else sc.scroll_vertical
		# The geometric centre can land on a slider/text field after the first drag;
		# those intentionally own their gestures. Start on a visible button again.
		var reverse_button := _button_in_view(sc, sc)
		var reverse_from := reverse_button.get_global_rect().get_center() if reverse_button != null else sc.get_global_rect().position + sc.size * Vector2(0.08, 0.5)
		_swipe(reverse_from, axis * float(mini(back, 200)))
		await _frames(3)
		check(back == 0 or (sc.scroll_horizontal if horiz else sc.scroll_vertical) < back, label + ": a drag the other way scrolls back")
		if horiz:
			sc.scroll_horizontal = 0
		else:
			sc.scroll_vertical = 0
		await _frames(2)
	if found == 0:
		print("TOUCHSCROLL note: %s has no list that needs scrolling" % where)


# ---------------------------------------------------------- job strip ---

## The strip of running jobs above the nav bar: with more jobs than fit the screen width, the later ones must be reachable
## with a finger that starts on a job card (a drag on an empty part of the strip still belongs to the 3D camera).
func _job_strip(hud: Node, lang: String) -> void:
	var made := 0
	for i in 12:
		var job: Dictionary = {}
		for pass_n in 4:
			for offer in ContractManager.offers.duplicate():
				var v: Dictionary = ContractManager.best_vehicle(offer)
				if not v.is_empty():
					job = ContractManager.dispatch(offer, v, "normal")
					if not job.is_empty():
						break
			if not job.is_empty():
				break
			ContractManager.refresh_offers()
		if job.is_empty():
			break
		made += 1
	await _settle(0.8)
	hud._rebuild_jobs()
	await _settle(0.6)
	var sc: ScrollContainer = hud.jobs_scroll
	var hb := sc.get_h_scroll_bar()
	var span := float(hb.max_value - hb.page)
	print("TOUCHSCROLL note: %s %d vehicles, %d jobs running, %d cards, %d px of the strip hidden" % [lang, VehicleManager.vehicles.size(), made, hud.jobs_box.get_child_count(), int(span)])
	if span < 40.0:
		print("TOUCHSCROLL note: %s all jobs fit the strip" % lang)
		return
	var card: Control = hud.jobs_box.get_child(0)
	var from := card.get_global_rect().get_center()
	var before := sc.scroll_horizontal
	hud.world.select_vehicle("", false)
	_swipe(from, Vector2(-300, 0))
	await _settle(0.7)
	check(hud.world.selected_vehicle == "", lang + " job strip: a scroll drag does not open the job under the finger") # a fast flick glides on: wait for it
	check(sc.scroll_horizontal - before >= 150, "%s job strip: a sideways drag on a job card shows the later jobs (%d px)" % [lang, sc.scroll_horizontal - before])
	check(sc.scroll_horizontal <= int(hb.max_value - hb.page) + 1, lang + " job strip: stays inside its content")
	var mid := sc.scroll_horizontal
	_swipe(sc.get_global_rect().get_center() if false else from, Vector2(300, 0))
	await _settle(0.7)
	check(mid - sc.scroll_horizontal >= 150, "%s job strip: a drag the other way scrolls back (%d px)" % [lang, mid - sc.scroll_horizontal])
	# a tap on a card (no drag) still opens the job: the camera follows the vehicle
	var tap_card: Control = hud.jobs_box.get_child(0)
	await _settle(1.2)   # the kit's fling of the swipe above has stopped
	sc.scroll_horizontal = 0
	await _frames(2)
	var c0 := tap_card.get_global_rect().get_center()
	var vp := get_tree().root
	vp.push_input(_mouse(c0, true), true)
	vp.push_input(_touch(c0, true), true)
	vp.push_input(_mouse(c0, false), true)
	vp.push_input(_touch(c0, false), true)
	await _frames(2)
	check(sc.scroll_horizontal == 0, lang + " job strip: a tap does not scroll the strip")
	check(hud.world.selected_vehicle == str(ContractManager.jobs[0].vehicle_id), lang + " job strip: a tap on a job card follows its vehicle")
	sc.scroll_horizontal = 0
