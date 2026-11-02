extends Node
## QA / screenshot automation, only loaded when the game is launched with
## user command-line args (godot -- --shot=out.png --panel=fleet ...).
## Never active in normal play.

var main: Node


func _ready() -> void:
	var a := GameManager.debug_args
	# store captures: a titled macOS window is clamped to the screen height; a borderless one keeps the device size
	if a.has("winsize"):
		var wp := str(a.winsize).split("x")
		if wp.size() == 2:
			DisplayServer.window_set_flag(DisplayServer.WINDOW_FLAG_BORDERLESS, true)
			DisplayServer.window_set_position(Vector2i(0, 0))
			DisplayServer.window_set_size(Vector2i(int(wp[0]), int(wp[1])))
			get_tree().root.size = Vector2i(int(wp[0]), int(wp[1]))
	if not a.has("intro") and not ProgressionManager.intro_done():
		ProgressionManager.flags["intro_done"] = true
		ProgressionManager.flags["company"] = "Borderline Logistics"
		if main.hud and is_instance_valid(main.hud.onboarding):
			main.hud.onboarding.queue_free()
			main.hud._restore_game_chrome()
			main.world.rig.input_enabled = true
		main.world.spawn_mentor()
		main.world.set_company_color(ProgressionManager.company_color())
	if not a.has("gating"):
		ProgressionManager.flags["tabs_all"] = true
	_debug_hooks()


func _debug_hooks() -> void:
	var args := GameManager.debug_args
	if args.has("shot"):
		var delay := float(args.get("delay", "3"))
		await get_tree().create_timer(delay).timeout
		await _autoplay()
		if args.has("layout") and main.hud and main.hud.current_sheet:
			_dump_wide(main.hud.current_sheet.panel, 0)
		if args.has("perf"):
			print("PERF fps=%d draw_calls=%d objects=%d primitives=%d nodes=%d static_mem=%.1fMB" % [
				Engine.get_frames_per_second(),
				RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME),
				RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_OBJECTS_IN_FRAME),
				RenderingServer.get_rendering_info(RenderingServer.RENDERING_INFO_TOTAL_PRIMITIVES_IN_FRAME),
				Performance.get_monitor(Performance.OBJECT_NODE_COUNT),
				Performance.get_monitor(Performance.MEMORY_STATIC) / 1048576.0])
		if args.has("audit"):
			_audit_controls(main.hud)
		if args.has("verify_tutorial"):
			var passed := EconomyManager.stat("deliveries") > 0 and EconomyManager.stat("vehicle_upgrades") > 0
			print("QA_TUTORIAL: ", "PASS" if passed else "FAIL", " deliveries=", EconomyManager.stat("deliveries"), " upgrades=", EconomyManager.stat("vehicle_upgrades"))
		# draw explicitly: macOS stops presenting occluded / screen-locked windows, which left the first frame in the texture
		# under heavy machine load few frames pass in the timed waits: give thumbnails / pop-in tweens real frames
		for i in int(args.get("frames", "24")):
			await get_tree().process_frame
		if args.has("clean_late"):
			await _clear_late_modals() # store frames: a road event that fired during the waits (auto dispatch) is not the subject
		if args.has("nomodal"):
			_hide_kit_toast() # store frames: a late kit toast (goal / tip) never lands in the shot
		for i in 3:
			RenderingServer.force_draw()
		if args.has("store_state"):
			print("STORE_STATE ", JSON.stringify(_store_state()))
		var img := get_viewport().get_texture().get_image()
		img.save_png(str(args.shot))
		print("screenshot saved ", args.shot)
		get_tree().quit()


## Store frames: resolve road events that came up while the shot waited (as an auto-choice) and drop their cards,
## so the panel / map the shot is about stays in front.
func _clear_late_modals() -> void:
	for j in EventManager.pending_jobs():
		EventManager.resolve(j, EventManager.auto_choice(j), true)
	if main.hud:
		main.hud._modal_queue.clear()
		for m in main.hud.modal_layer.get_children():
			m.queue_free()
		main.hud._modal_open = false
	for i in 2:
		await get_tree().process_frame
	_hide_kit_toast()


## QA shots: toasts are the UI kit's hud.ui_toast now (the old hud.toast_box stays empty) — hide it at once.
func _hide_kit_toast() -> void:
	if main.hud and main.hud.ui_toast != null and is_instance_valid(main.hud.ui_toast):
		main.hud.ui_toast.hide_toast()


## Store frames (--store_state): the state the frame was saved in, for the set's provenance — house banner held off
## (fleet capture wrapper games/tools/store/capture.py), no modal / toast / guide, store on (--paywall: nothing owned).
func _store_state() -> Dictionary:
	var hud: Node = main.hud
	var cap := get_node_or_null("/root/StoreCaptureBanner")
	var toast_up := false
	if hud and hud.ui_toast != null and is_instance_valid(hud.ui_toast):
		toast_up = hud.ui_toast.is_showing()
	var iap := get_node_or_null("/root/Iap")
	return {
		"banner_inset": float(hud.banner_inset) if hud else -1.0,
		"capture_banner_installed": cap != null,
		"capture_banner_verified": bool(cap.verified()) if cap != null else false,
		"modal_open": bool(hud._modal_open) if hud else false,
		"modal_children": hud.modal_layer.get_child_count() if hud else 0,
		"toast_visible": toast_up,
		"tutorial_active": TutorialManager.is_active(),
		"panel": UIManager.current_panel,
		"regions": RegionManager.unlocked_count,
		"vehicles": VehicleManager.vehicles.size(),
		"garage": ProgressionManager.garage_level,
		"goal_index": ProgressionManager.goal_index,
		"cash": EconomyManager.cash,
		"store_sells": bool(iap.sells) if iap else false,
		"purchased_any": bool(iap.purchased_any()) if iap else false,
		"ad_free": bool(iap.ad_free()) if iap else false,
		"quality": str(GameManager.settings.get("quality", "")),
		"language": TranslationServer.get_locale(),
		"viewport": [get_viewport().get_visible_rect().size.x, get_viewport().get_visible_rect().size.y],
	}


## Scripted session used by automated QA screenshots (debug only).
func _autoplay() -> void:
	var a := GameManager.debug_args
	var steps := str(a.get("autoplay", ""))
	if a.has("nomodal") and main.hud:
		main.hud._offer_toast_cd = 1e12
	if a.has("notut"):
		TutorialManager.finished = true
		TutorialManager.step = TutorialManager.steps.size()
	if a.has("cash"):
		EconomyManager.earn(float(a.cash), "debug", false)
	if a.has("rep"):
		EconomyManager.add_reputation(int(a.rep))
	if a.has("unlock"):
		for i in range(1, int(a.unlock)):
			EconomyManager.earn(1e7, "debug", false)
			EconomyManager.add_reputation(200)
			RegionManager.unlock(i)
	if a.has("garage"):
		for i in int(a.garage) - ProgressionManager.garage_level:
			EconomyManager.earn(1e7, "debug", false)
			EconomyManager.add_reputation(300)
			ProgressionManager.upgrade_garage()
	if a.has("buy"):
		for id in str(a.buy).split(","):
			EconomyManager.earn(1e7, "debug", false)
			VehicleManager.buy(id)
	if a.has("auto"):
		for i in int(a.auto):
			EconomyManager.earn(1e7, "debug", false)
			ProgressionManager.buy_business("automated_dispatch")
	if a.has("biz"):
		for pair in str(a.biz).split(","):
			var kv := pair.split(":")
			for i in int(kv[1]) if kv.size() > 1 else 1:
				EconomyManager.earn(1e7, "debug", false)
				ProgressionManager.buy_business(kv[0])
	if a.has("depot"):
		for d in str(a.depot).split(","):
			EconomyManager.earn(1e7, "debug", false)
			RegionManager.build_depot(int(d))
	if a.has("heat"):
		for i in RegionManager.unlocked_count:
			HeatManager.add_heat(i, float(a.heat))
	if steps.contains("dispatch") and ContractManager.offers.size() > 0:
		var c: Dictionary = ContractManager.offers[0]
		var v: Dictionary = VehicleManager.vehicles[0]
		ContractManager.dispatch(c, v, "normal")
	if a.has("sim"):
		# fast-forward game logic in 0.1s steps
		for i in int(float(a.sim) * 10):
			GameManager.tick(0.1)
	if a.has("nomodal") and main.hud:
		await get_tree().process_frame
		main.hud._modal_queue.clear()
		for m in main.hud.modal_layer.get_children():
			m.queue_free()
		main.hud._modal_open = false
		main.hud._offer_toast_cd = 1e12
		for t in main.hud.toast_box.get_children():
			t.queue_free()
		_hide_kit_toast()
		for j in EventManager.pending_jobs():
			EventManager.resolve(j, EventManager.auto_choice(j), true)
		ContractManager.offers_changed.emit()
	if steps.contains("event"):
		for j in ContractManager.jobs:
			if j.phase == "out" and not j.get("pending_event", false):
				j.event_id = str(a.get("event_id", "breakdown"))
				EventManager.trigger(j)
				break
	if a.has("stage"):
		ProgressionManager.stage = int(a.stage)
		EconomyManager.stats["earned"] = float(a.get("earned", "30000000"))
	if a.has("goal"):
		ProgressionManager.goal_index = int(a.goal)
		ProgressionManager.goal_changed.emit()
	if a.has("store_price"):
		# QA/store art only: the localized price string the platform store returns on a device (desktop has no store)
		get_node("/root/Monetization").set("price", str(a.store_price))
	if a.has("setcash"):
		EconomyManager.cash = float(a.setcash)
		EconomyManager.cash_changed.emit(EconomyManager.cash, 0)
	if a.has("nohud") and main.hud:
		main.hud.get_parent().visible = false
		if str(a.nohud) == "all":
			# also hide the floating region / town labels (clean store art)
			for l in main.world.find_children("*", "Label3D", true, false):
				if not (l.get_parent() is HQSite):
					(l as Label3D).visible = false
	if a.has("quality"):
		GameManager.set_setting("quality", str(a.quality))
	if a.has("speed"):
		GameManager.time_scale = float(a.speed)
	if a.has("review_modal"):
		await get_tree().process_frame
		if str(a.review_modal) == "privacy":
			main.hud.show_modal("Privacy", tr("Game progress stays on your device. Optional ads use Google AdMob to process ad interactions and device information. Purchases use the platform store and RevenueCat to validate receipts and restore ownership. No account is required. You can manage advertising consent in Privacy choices.") + "\n\n" + tr("Firebase Analytics measures sessions, screen views and gameplay milestones using an app instance identifier. Your company name and saved game are not sent. Analytics advertising storage and personalization are disabled."), "shield")
		elif str(a.review_modal) == "advisor": TutorialManager.show_advice()
		elif str(a.review_modal) == "offline": main.hud._on_offline({"id":"qa", "seconds":3600, "earned":1200, "deliveries":4, "manager":500})
		elif str(a.review_modal) == "reset": await _qa_open_reset(main)
		await get_tree().create_timer(0.5).timeout
		if main.hud._modal_open:
			main.hud._dismiss_top_modal()
			await get_tree().create_timer(0.3).timeout
			print("QA_BACK_DISMISS:", not main.hud._modal_open)
			if str(a.review_modal) == "reset":
				print("QA_RESET_KEPT:", GameManager.running) # Back on the real dialog must not erase the empire
			# Re-open the same descriptor for capture after the dismissal assertion.
			if str(a.review_modal) == "advisor": TutorialManager.show_advice()
			elif str(a.review_modal) == "offline": main.hud._on_offline({"id":"qa", "seconds":3600, "earned":1200, "deliveries":4, "manager":500})
			elif str(a.review_modal) == "reset": await _qa_open_reset(main)
			else: main.hud.show_modal("Privacy", tr("Game progress stays on your device. Optional ads use Google AdMob to process ad interactions and device information. Purchases use the platform store and RevenueCat to validate receipts and restore ownership. No account is required. You can manage advertising consent in Privacy choices.") + "\n\n" + tr("Firebase Analytics measures sessions, screen views and gameplay milestones using an app instance identifier. Your company name and saved game are not sent. Analytics advertising storage and personalization are disabled."), "shield")
		await get_tree().create_timer(0.4).timeout
		if str(a.review_modal) == "reset":
			print("QA_RESET_DIALOG:", _qa_reset_dialog_shown(main)) # the capture shows the real confirmation, not another modal
	if a.has("matrix_dir"):
		var folder := str(a.matrix_dir)
		DirAccess.make_dir_recursive_absolute(folder)
		for pn in ["contracts", "dispatch", "fleet", "map", "upgrades", "business", "settings", "shop", "daily"]:
			var parg: Variant = null
			if pn == "dispatch" and ContractManager.offers.size() > 0: parg = ContractManager.offers[0].id
			UIManager.open_panel(pn, parg)
			await get_tree().create_timer(0.5).timeout
			_audit_controls(main.hud)
			RenderingServer.force_draw()
			get_viewport().get_texture().get_image().save_png(folder.path_join(pn + ".png"))
			print("QA_PANEL: ", pn)
	if a.has("cycle"):
		# open every panel in turn to shake out UI errors
		for pn in ["contracts", "fleet", "map", "upgrades", "business", "settings", "dispatch"]:
			var parg: Variant = null
			if pn == "dispatch" and ContractManager.offers.size() > 0:
				parg = ContractManager.offers[0].id
			UIManager.open_panel(pn, parg)
			await get_tree().create_timer(float(a.cycle)).timeout
		UIManager.close_panel()
	if a.has("focus"):
		await get_tree().create_timer(1.3).timeout
		var fz := float(a.get("zoom", "40"))
		if str(a.focus).begins_with("cp"):
			main.world.rig.focus_on(main.world.regions[int(str(a.focus).substr(2))].checkpoint_pos, fz, 0.3)
		elif a.focus == "hq":
			main.world.rig.focus_on(Sites.garage_transform(ProgressionManager.garage_level).origin, fz, 0.3)
		else:
			main.world.rig.focus_on(RegionManager.center(int(a.focus)), fz, 0.3)
		if a.has("pan"):
			# shift the framed subject on screen, in pixels ("300x0" moves it right)
			await get_tree().create_timer(0.45).timeout
			var pp := str(a.pan).split("x")
			main.world.rig.pan_pixels(Vector2(float(pp[0]), float(pp[1]) if pp.size() > 1 else 0.0))
	if steps.contains("zoomout"):
		main.world.rig.distance = 110.0
		main.world.rig.target = RegionManager.center(1) * 0.5
	if a.has("taps"):
		for key in str(a.taps).split(","):
			await get_tree().create_timer(0.9).timeout
			await _tap_target(key)
	if a.has("panel"):
		var arg: Variant = null
		if a.panel == "dispatch" and ContractManager.offers.size() > 0:
			arg = ContractManager.offers[0].id
			for c in ContractManager.offers:
				if not ContractManager.best_vehicle(c).is_empty():
					arg = c.id
					break
		if a.panel == "fleet" and a.has("detail"):
			arg = {"vehicle": VehicleManager.vehicles[0].id}
		if a.has("tab"):
			arg = {"tab": a.tab}
		if a.has("product"):
			arg = {"focus": str(a.product)} # shop: scroll to this product card
		UIManager.open_panel(str(a.panel), arg)
	if a.has("legacy"):
		ProgressionManager.flags["legacy"] = int(a.legacy) # shows the Legacy Head Start product / offer
	if a.has("ui_modal"):
		# QA / review shots of the shop's modals: ui_modal=offer_starter | offer_legacy | feedback
		await get_tree().create_timer(0.4).timeout
		match str(a.ui_modal):
			"offer_starter": main.hud._show_offer("starter")
			"offer_legacy": main.hud._show_offer("legacy_pack")
			"feedback": main.hud.show_feedback()
		main.hud._show_next_modal()
	await get_tree().create_timer(float(a.get("wait", "1.5"))).timeout
	if a.has("scrollto") and main.hud.current_sheet:
		# scroll a named control of the open sheet into view (QA shots): scrollto=hq_signature | auto_crisis | paint_FF3D81 | locked_portraits
		var node: Node = main.hud.current_sheet.find_child(str(a.scrollto), true, false)
		if node is Control:
			main.hud.current_sheet.scroll.ensure_control_visible(node as Control)
			await get_tree().create_timer(0.3).timeout
	if a.has("scroll") and main.hud.current_sheet:
		# QA shots of the lower part of a long sheet: scroll=<canvas px>
		main.hud.current_sheet.scroll.scroll_vertical = int(a.scroll)
		await get_tree().create_timer(0.3).timeout


## Simulates a finger tap on a registered UI target (QA only).
func _tap_target(key: String) -> void:
	if key.begins_with("sim"):
		for i in int(float(key.substr(3)) * 10):
			GameManager.tick(0.1)
		await get_tree().process_frame
		return
	if key.begins_with("wait"):
		await get_tree().create_timer(float(key.substr(4))).timeout
		return
	if key.begins_with("at"):
		var xy := key.substr(2).split("x")
		await _tap_pos(Vector2(float(xy[0]), float(xy[1])))
		return
	var scroll_only := key.begins_with("show_")
	if scroll_only:
		key = key.substr(5)   # "show_<target>": scroll the target into view without pressing it (review video)
	var c := UIManager.get_target(key)
	if c == null:
		print("QA tap: target not found ", key)
		return
	var sc := c.get_parent()
	while sc and not (sc is ScrollContainer):
		sc = sc.get_parent()
	if sc:
		(sc as ScrollContainer).ensure_control_visible(c)
		await get_tree().process_frame
		await get_tree().process_frame
	if scroll_only:
		print("QA show: ", key)
		return
	var pos := c.get_global_rect().get_center()
	for pressed in [true, false]:
		var ev := InputEventMouseButton.new()
		ev.button_index = MOUSE_BUTTON_LEFT
		ev.pressed = pressed
		ev.position = pos
		ev.global_position = pos
		get_viewport().push_input(ev, true)
		await get_tree().process_frame
	print("QA tap: ", key)


func _dump_wide(c: Node, depth: int) -> void:
	if c is Control:
		var w := (c as Control).get_combined_minimum_size().x
		if w > 600:
			print("WIDE ", "  ".repeat(depth), c.name, " ", c.get_class(), " min=", w, " size=", (c as Control).size.x)
	for ch in c.get_children():
		_dump_wide(ch, depth + 1)


func _tap_pos(pos: Vector2) -> void:
	for pressed in [true, false]:
		var ev := InputEventMouseButton.new()
		ev.button_index = MOUSE_BUTTON_LEFT
		ev.pressed = pressed
		ev.position = pos
		ev.global_position = pos
		get_viewport().push_input(ev, true)
		await get_tree().process_frame



func _audit_controls(node: Node) -> void:
	if node is Button and node.is_visible_in_tree():
		var button := node as Button
		if button.size.x < 107 or button.size.y < 107:
			print("QA_SMALL_BUTTON: ", button.get_path(), " ", button.size)
		var rect := button.get_global_rect()
		var viewport: Rect2 = main.hud.get_viewport_rect()
		if rect.position.x < -1 or rect.end.x > viewport.end.x + 1:
			print("QA_HORIZONTAL_OVERFLOW: ", button.get_path(), " ", rect)
		# Scroll children may lie outside the viewport; their ancestor handles reachability.
		var parent := button.get_parent()
		var scrolling := false
		while parent:
			if parent is ScrollContainer: scrolling = true
			parent = parent.get_parent()
		if not scrolling and (rect.position.y < -1 or rect.end.y > viewport.end.y + 1):
			print("QA_VERTICAL_OVERFLOW: ", button.get_path(), " ", rect)
	for child in node.get_children():
		_audit_controls(child)


## QA: open the REAL reset confirmation (Settings → Reset progress), never press Erase.
func _qa_open_reset(main: Node) -> void:
	var btn := get_tree().root.find_child("reset_progress", true, false) as Button
	if btn == null or not btn.is_visible_in_tree(): # opening an already open panel would close it
		UIManager.open_panel("settings")
		await get_tree().create_timer(0.3).timeout
		btn = get_tree().root.find_child("reset_progress", true, false) as Button
	if btn == null:
		print("QA_RESET_DIALOG: false (no Reset progress button)")
		return
	btn.pressed.emit()
	await get_tree().process_frame


func _qa_reset_dialog_shown(main: Node) -> bool:
	if not main.hud._modal_open:
		return false
	var want := tr("Start over?")
	for l in main.hud.modal_layer.find_children("*", "Label", true, false):
		if (l as Label).text in [want, "Start over?"] and (l as Label).is_visible_in_tree(): # Labels keep the key and auto-translate
			return true
	return false
