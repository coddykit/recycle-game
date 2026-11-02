extends SceneTree
## Tab pages + the windows opened from them, for the 2026-10-26 tab + modal redesign before/after sheets (batch B):
##   HOME=<tmp> godot --path games/smuggler --windowed --resolution 360x640 --rendering-method gl_compatibility \
##     --audio-driver Dummy --script /abs/games/smuggler/tools/qa/redesign_tabs.gd -- <out dir> <WxH[,WxH]> <en,de> [base 720x1280]
## Renders the real main.tscn in an offscreen SubViewport of exactly W×H physical px with the canvas_items stretch
## emulated (size_2d_override = W×H / min(W/base.x, H/base.y)), so a 1080×2400 phone and a 1200×1920 tablet show the
## canvas the device shows. Run it with a throw-away HOME (user:// = a temp dir; the player save is never touched).
## Fixture (seeded): a new empire, intro + tutorial done, every tab open, cash + reputation, regions 1-3 open, HQ level 3,
## two more vehicles, a hired driver, fresh offers, one job on the road, fake store prices, reduce motion, sim paused.
## Shots: hud, tab_<id>[_end] (contracts / fleet / map / upgrades / business), fleet_<sub> (Showroom, Drivers),
## sel_vehicle[_end] (vehicle detail from a Fleet row), win_<id>[_end] (dispatch, daily, shop, settings, how-to-play,
## privacy, reset confirm, feedback, purchase terms, offer, sell confirm, event). Then a Low pass at the first size in the
## first language prints UI_DRAWS <screen> <n> (total − 3D draw calls, averaged over 20 frames); CAPTURE_LOW_SHOTS=1
## also saves low_<screen>.png. Runs on the old and the new code (public hud / sheet calls only, guarded).

const TABS := ["contracts", "fleet", "map", "upgrades", "business"]

var out := ""
var vp: SubViewport
var main: Node
var hud: Node


func _initialize() -> void:
	load("res://addons/coddy_ui/ui_kit.gd").reduce_motion = true
	_run.call_deferred()


func _a(n: String) -> Node:
	return root.get_node(n)


func _frames(n: int) -> void:
	for i in n:
		await process_frame


func _shot(name: String, frames := 8) -> void:
	await _frames(frames)
	await create_timer(0.35).timeout
	await RenderingServer.frame_post_draw
	var img := vp.get_texture().get_image()
	img.save_png(out.path_join(name + ".png"))
	print("SHOT ", name, " ", img.get_size())


func _size(size: Vector2i, base: Vector2i) -> void:
	var scale := minf(float(size.x) / base.x, float(size.y) / base.y)
	vp.size = size
	vp.size_2d_override = Vector2i(roundi(size.x / scale), roundi(size.y / scale))
	vp.size_2d_override_stretch = true
	await _frames(12)


## A fresh game in `lang`, grown so that every list of the tabs has rows. Same seed = same offers in both trees.
func _boot(lang: String, quality: String) -> void:
	if main != null:
		main.free()
		main = null
		await _frames(4)
	var gm := _a("GameManager")
	_a("SaveManager").delete_save()
	gm.settings["language"] = lang
	gm.settings["quality_notice"] = ""
	gm.settings["quality"] = quality
	gm.settings["reduced_motion"] = true
	gm.settings["advisor"] = false
	gm.apply_language()
	seed(26102026)
	gm.new_game()
	seed(26102026)
	main = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	vp.add_child(main)
	await _frames(6)
	hud = main.hud
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if "onboarding" in hud and is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	var prog := _a("ProgressionManager")
	prog.flags["intro_done"] = true
	prog.flags["tabs_all"] = true
	_a("TutorialManager").skip()
	if hud.has_method("_restore_game_chrome"):
		hud._restore_game_chrome()
	hud._offer_toast_cd = 1e12
	var eco := _a("EconomyManager")
	eco.earn(4e6, "shots", false)
	eco.add_reputation(700)
	var regions := _a("RegionManager")
	for r in range(1, 4):
		regions.unlock(r)
	prog.upgrade_garage()
	prog.upgrade_garage()
	var vm := _a("VehicleManager")
	for def in ["small_van", "armored"]:
		vm.buy(def)
	if vm.market.size() > 0:
		vm.hire(vm.market[0])
	var cm := _a("ContractManager")
	for i in 3:
		cm.refresh_offers()
	# one job on the road (the job strip, the busy state of a fleet row)
	if cm.offers.size() > 2:
		var offer: Dictionary = cm.offers[cm.offers.size() - 1]
		var v: Dictionary = cm.best_vehicle(offer)
		if not v.is_empty():
			cm.dispatch(offer, v, "normal")
	eco.cash = 186500.0 if "cash" in eco else eco.cash
	# the shop as a shipped build shows it: store prices, nothing owned
	var iap := _a("Iap")
	iap._test_setup({"v": 1, "owned": {}, "any": false, "pending": [], "seen": [], "delivered": {}}, true, false)
	for slug in IapCatalog.slugs():
		iap._prices[slug] = "$%.2f" % float(IapCatalog.def(slug).get("usd", 0.99))
	iap._fetched = true
	iap.prices_changed.emit()
	await _frames(10)
	gm.running = false   # sim paused: both trees show the same state
	await _frames(4)
	_clean()


func _clean() -> void:
	hud._modal_queue.clear()
	for m in hud.modal_layer.get_children():
		m.free()
	hud._modal_open = false
	if hud.ui_toast != null and hud.ui_toast.has_method("card"):
		var c = hud.ui_toast.card()
		if c is Control:
			(c as Control).visible = false
	if hud.current_sheet != null and is_instance_valid(hud.current_sheet):
		hud.current_sheet.close_instant()
	hud.current_sheet = null
	if "current_panel" in _a("UIManager"):
		_a("UIManager").current_panel = ""


func _sheet_scrolls() -> bool:
	var s = hud.current_sheet
	if s == null or not is_instance_valid(s) or s.scroll == null:
		return false
	var bar: ScrollBar = (s.scroll as ScrollContainer).get_v_scroll_bar()
	return bar != null and bar.max_value - bar.page > 4.0


func _modal() -> Node:
	var kids: Array = hud.modal_layer.get_children()
	return kids[kids.size() - 1] if kids.size() > 0 else null


func _modal_scrolls() -> bool:
	var m := _modal()
	if m == null or not ("scroller" in m) or m.scroller == null:
		return false
	var bar: ScrollBar = (m.scroller as ScrollContainer).get_v_scroll_bar()
	return bar != null and bar.max_value - bar.page > 4.0


func _open(name: String, arg: Variant = null) -> void:
	hud.open_panel(name, arg)


## [name, opener, kind] for every screen: kind "sheet" (scrolls in current_sheet.scroll) or "modal" (modal_layer).
func _screens_list() -> Array:
	var cm := _a("ContractManager")
	var vm := _a("VehicleManager")
	var list: Array = []
	for t in TABS:
		list.append(["tab_" + t, func(): _open(t), "sheet"])
		if t == "fleet":
			list.append(["fleet_showroom", func(): _open("fleet", {"tab": "Showroom"}), "sheet"])
			list.append(["fleet_drivers", func(): _open("fleet", {"tab": "Drivers"}), "sheet"])
			var vid := ""
			for v in vm.vehicles:
				if v.state != "busy":
					vid = str(v.id)
					break
			if vid != "":
				list.append(["sel_vehicle", func(): _open("fleet", {"vehicle": vid}), "sheet"])
	if cm.offers.size() > 0:
		var oid = cm.offers[0].id
		list.append(["win_dispatch", func(): _open("dispatch", oid), "sheet"])
	list.append(["win_daily", func(): _open("daily"), "sheet"])
	list.append(["win_shop", func(): _open("shop"), "sheet"])
	list.append(["win_settings", func(): _open("settings"), "sheet"])
	list.append(["win_howto", func(): _modal_from_sheet("settings", func(s): _press_row(s, tr("How to play"))), "modal"])
	list.append(["win_privacy", func(): _modal_from_sheet("settings", func(s): _press_row(s, tr("How your data is used"))), "modal"])
	list.append(["win_reset", func(): _modal_from_sheet("settings", func(s):
		var b = s.find_child("reset_progress", true, false)
		if b is Button: (b as Button).pressed.emit()), "modal"])
	list.append(["win_feedback", func():
		hud.show_feedback()
		hud._show_next_modal(), "modal"])
	list.append(["win_terms", func(): _modal_from_sheet("shop", func(s): _press_row(s, tr("Purchase terms"))), "modal"])
	list.append(["win_offer", func():
		hud._show_offer("starter")
		hud._show_next_modal(), "modal"])
	list.append(["win_event", func(): _event(), "modal"])
	return list


## Opens `sheet`, runs `act` on it (presses a row that opens a window), closes the sheet behind the window.
func _modal_from_sheet(sheet: String, act: Callable) -> void:
	_open(sheet)
	var s = hud.current_sheet
	act.call(s)
	hud._show_next_modal()
	if is_instance_valid(s):
		s.close_instant()
	hud.current_sheet = null


func _press_row(s: Node, text: String) -> void:
	for n in s.find_children("*", "Control", true, false):
		if n.has_signal("pressed") and "text" in n and str(n.text) == text:
			n.emit_signal("pressed")
			return
		if n.has_method("get") and n.get("title") is Label and (n.get("title") as Label).text == text and n.has_signal("pressed"):
			n.emit_signal("pressed")
			return
	# kit rows: a Label with the text inside a clickable row
	for l in s.find_children("*", "Label", true, false):
		if (l as Label).text == text:
			var p: Node = l.get_parent()
			while p != null and p != s:
				if p.has_signal("pressed"):
					p.emit_signal("pressed")
					return
				if p.has_signal("activated"):
					p.emit_signal("activated")
					return
				p = p.get_parent()
	print("WARN row not found: ", text)


func _event() -> void:
	var cm := _a("ContractManager")
	var vm := _a("VehicleManager")
	var em := _a("EventManager")
	var data := _a("Data")
	for c in cm.offers:
		var v: Dictionary = cm.best_vehicle(c)
		if v.is_empty():
			continue
		var job: Dictionary = cm.dispatch(c, v, "normal")
		if job.is_empty():
			continue
		job.event_id = str(data.events[0].id)
		job.phase = "out"
		em.trigger(job)
		hud._show_next_modal()
		return


func _screens(prefix: String) -> void:
	_clean()
	await _shot(prefix + "hud", 16)
	for s in _screens_list():
		(s[1] as Callable).call()
		await _shot(prefix + str(s[0]), 12)
		if s[2] == "sheet" and _sheet_scrolls():
			hud.current_sheet.scroll.scroll_vertical = 100000
			await _shot(prefix + str(s[0]) + "_end", 8)
		elif s[2] == "modal" and _modal_scrolls():
			_modal().scroller.scroll_vertical = 100000
			await _shot(prefix + str(s[0]) + "_end", 8)
		_clean()
		await _frames(3)


func _ui_draws(name: String) -> int:
	await _frames(8)
	await create_timer(0.3).timeout
	var total := 0.0
	var d3 := 0.0
	for i in 20:
		await RenderingServer.frame_post_draw
		total += Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)
		for v in [root, vp]:
			var rid: RID = (v as Viewport).get_viewport_rid()
			for pass_type in [RenderingServer.VIEWPORT_RENDER_INFO_TYPE_VISIBLE, RenderingServer.VIEWPORT_RENDER_INFO_TYPE_SHADOW]:
				d3 += RenderingServer.viewport_get_render_info(rid, pass_type, RenderingServer.VIEWPORT_RENDER_INFO_DRAW_CALLS_IN_FRAME)
		await process_frame
	var n := roundi((total - d3) / 20.0)
	print("UI_DRAWS %s %d" % [name, n])
	if OS.get_environment("CAPTURE_LOW_SHOTS") == "1":
		await _shot("low_" + name, 1)
	return n


func _draws_pass() -> void:
	_clean()
	await _ui_draws("hud")
	for s in _screens_list():
		(s[1] as Callable).call()
		await _ui_draws(str(s[0]))
		_clean()
		await _frames(3)


func _run() -> void:
	var a := OS.get_cmdline_user_args()
	out = a[0]
	var sizes: Array = []
	for s in String(a[1]).split(","):
		var wh := s.split("x")
		sizes.append(Vector2i(int(wh[0]), int(wh[1])))
	var langs := String(a[2]).split(",")
	var base := Vector2i(720, 1280)
	if a.size() > 3:
		var bw := String(a[3]).split("x")
		base = Vector2i(int(bw[0]), int(bw[1]))
	DirAccess.make_dir_recursive_absolute(out)
	vp = SubViewport.new()
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(vp)
	if OS.get_environment("CAPTURE_SHOTS") != "0":
		for size in sizes:
			await _size(size, base)
			for lang in langs:
				await _boot(lang, "medium")
				await _screens("%dx%d_%s_" % [size.x, size.y, lang])
	if OS.get_environment("CAPTURE_DRAWS") != "0":
		await _size(sizes[0], base)
		await _boot(langs[0], "low")
		await _draws_pass()
	if main != null:
		main.free()
	quit()
