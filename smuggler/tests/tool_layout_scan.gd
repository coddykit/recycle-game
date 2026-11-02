extends SceneTree
## Text-overflow scan over every language x screen (not a gate test; headless is fine, layout does not need a GPU).
## Opens every panel / fleet tab / vehicle detail / dispatch / event modal in every language on a 720-wide canvas and reports
## visible controls that stick out of the screen sideways, plus labels that clip their own text. Names of the offending
## controls and their text are printed as `LAYOUT <lang> <screen> | ...`.
##   godot --headless --path . -s tests/tool_layout_scan.gd        (env SCAN_LANGS="de ru", default all ten)
var GameManager: Node
var UIManager: Node
var ContractManager: Node
var EconomyManager: Node
var RegionManager: Node
var ProgressionManager: Node
var VehicleManager: Node
var EventManager: Node
var Data: Node
var found := {}
var scanned := 0


func _initialize() -> void:
	for n in ["GameManager", "UIManager", "ContractManager", "EconomyManager", "RegionManager", "ProgressionManager", "VehicleManager", "EventManager", "Data"]:
		set(n, root.get_node(n))
	_run()


func _frames(n := 12) -> void:
	for i in n:
		await process_frame


## Tweens (sheet slide-in, pop-ins) run on real time: wait for them before measuring.
func _settle(seconds := 0.5) -> void:
	await create_timer(seconds).timeout
	await process_frame


func _scan(hud: Node, lang: String, screen: String) -> void:
	scanned += 1
	var vp: Rect2 = hud.get_viewport_rect()
	var stack: Array = [hud]
	while not stack.is_empty():
		var n: Node = stack.pop_back()
		if n is CanvasItem and not (n as CanvasItem).visible:
			continue
		stack.append_array(n.get_children())
		if not (n is Control) or not (n as Control).is_visible_in_tree():
			continue
		var c := n as Control
		var text := ""
		if c is Label:
			text = (c as Label).text
		elif c is Button:
			text = (c as Button).text
		var r := c.get_global_rect()
		if c.size.x > 1.0 and (r.end.x > vp.end.x + 1.5 or r.position.x < -1.5) and not (c is ScrollContainer) and c.get_parent() != null \
				and not (c.get_parent() is ScrollContainer and false):
			# the sheet slide-in leaves nothing off-screen once settled; thin full-width backdrops are fine when they equal the viewport
			if absf(r.size.x - vp.size.x) > 1.5 or c is Label or c is Button:
				var widest := ""
				var cur: Control = c
				var guard := 0
				while cur is Container and guard < 12:
					guard += 1
					var wm := 0.0
					var nxt: Control = null
					for ch in cur.get_children():
						if ch is Control and (ch as Control).visible and (ch as Control).get_combined_minimum_size().x > wm:
							wm = (ch as Control).get_combined_minimum_size().x
							nxt = ch
					if nxt == null:
						break
					widest += " > %s(%s) %.0f" % [nxt.get_class(), (nxt.text if (nxt is Label or nxt is Button) else ""), wm]
					cur = nxt
				found["%s|%s|%s|%s" % [lang, screen, c.get_class(), text.left(40)]] = "x=%.0f..%.0f (viewport %.0f)%s" % [r.position.x, r.end.x, vp.end.x, widest]
		if c is Label and (c as Label).clip_text and (c as Label).text_overrun_behavior == TextServer.OVERRUN_NO_TRIMMING and text != "":
			var f: Font = c.get_theme_font("font")
			var fs: int = c.get_theme_font_size("font_size")
			if f.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x > c.size.x + 1.0:
				found["%s|%s|clipped|%s" % [lang, screen, text.left(40)]] = "text %.0f > box %.0f" % [f.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x, c.size.x]


func _run() -> void:
	var langs: Array = GameManager.LANGUAGES
	if OS.get_environment("SCAN_LANGS") != "":
		langs = OS.get_environment("SCAN_LANGS").split(" ")
	await process_frame
	root.size = Vector2i(540, 960)   # a phone: the 720 x 1280 canvas
	await process_frame
	root.get_node("SaveManager").delete_save()
	for lang in langs:
		GameManager.settings["language"] = lang
		GameManager.settings["quality_notice"] = ""
		GameManager.apply_language()
		GameManager.new_game()
		var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
		root.add_child(main)
		current_scene = main
		await _frames(6)
		var hud: Node = main.hud
		if is_instance_valid(hud.title_card):
			hud.title_card.free()
		if is_instance_valid(hud.onboarding):
			hud.onboarding.free()
		ProgressionManager.flags["intro_done"] = true
		ProgressionManager.flags["tabs_all"] = true
		TutorialManager_skip()
		hud._restore_game_chrome()
		hud._modal_queue.clear()
		hud._offer_toast_cd = 1e12
		EconomyManager.earn(5e7, "scan", false)
		EconomyManager.add_reputation(900)
		for r in range(1, 5):
			RegionManager.unlock(r)
		ProgressionManager.upgrade_garage()
		ProgressionManager.upgrade_garage()
		for def in ["small_van", "armored"]:
			VehicleManager.buy(def)
		for i in 3:
			ContractManager.refresh_offers()
		await _settle(0.6)
		hud._modal_queue.clear()
		for m in hud.modal_layer.get_children():
			m.queue_free()
		hud._modal_open = false
		await _scan_state(hud, lang, "home")
		# the shop as a phone sees it: fake store with prices, nothing owned (locked paint sets, "Loading"-free price buttons)
		root.get_node("Iap")._test_setup({"v": 1, "owned": {}, "any": false, "pending": [], "seen": [], "delivered": {}}, true, true)
		ProgressionManager.flags["legacy"] = 2 # the Legacy Head Start product is shown after a first prestige
		var screens := ["contracts", "fleet", "fleet:Showroom", "fleet:Drivers", "fleet:detail", "map", "upgrades", "business", "settings", "shop", "daily"]
		for oi in mini(3, ContractManager.offers.size()):
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
			await _scan_state(hud, lang, s)
			UIManager.close_panel()
			await _settle(0.4)
		# the opt-in rewarded buttons (repair, daily double, welcome-back double) with an ad "ready" (stub SDK) -> the long labels
		var mon: Node = root.get_node("Monetization")
		var daily: Node = root.get_node("DailyManager")
		var real_ads: Node = mon._ads
		var stub := GDScript.new()
		stub.source_code = "extends Node\nsignal earned(token)\nsignal finished\nsignal failed\nfunc available(_u): return true\nfunc show_reward(_u, _t): pass\nfunc privacy_options(): pass\n"
		stub.reload()
		mon._ads = stub.new()
		mon.ad_log = {}
		var rv: Dictionary = VehicleManager.vehicles[VehicleManager.vehicles.size() - 1]
		VehicleManager.start_repair(rv, 600.0)
		daily.state = {"day": daily.utc_day(), "tasks": [
			{"id": "a", "metric": "deliveries", "target": 3.0, "reward": 150.0, "text": "Complete %d deliveries", "base": 0.0, "claimed": true},
			{"id": "b", "metric": "earned", "target": 1200.0, "reward": 200.0, "text": "Earn %s from deliveries", "base": 0.0, "claimed": true},
			{"id": "c", "metric": "deliveries", "target": 8.0, "reward": 250.0, "text": "Complete %d deliveries", "base": 0.0, "claimed": true}]}
		for rs in ["fleet:detail", "daily", "shop"]:
			UIManager.open_panel("fleet" if rs == "fleet:detail" else rs, {"vehicle": rv.id} if rs == "fleet:detail" else null)
			await _settle(0.55)
			await _scan_state(hud, lang, rs + "+rewarded")
			UIManager.close_panel()
			await _settle(0.4)
		GameManager.session["receipt"] = {"id": "scan", "seconds": 3600.0, "earned": 123456.0, "deliveries": 12, "manager": 4000.0, "applied": true, "acknowledged": false}
		hud._on_offline(GameManager.session["receipt"])
		hud._show_next_modal()
		await _settle(0.8)
		await _scan_state(hud, lang, "offline+rewarded")
		for m in hud.modal_layer.get_children():
			m.queue_free()
		hud._modal_queue.clear()
		hud._modal_open = false
		VehicleManager.free_repair(rv)
		daily.state = {}
		GameManager.session["receipt"] = {}
		mon._ads.free()
		mon._ads = real_ads
		await _frames(3)
		# the shop's one-off offers and the feedback form (modals)
		for modal_case in ["offer:starter", "offer:legacy_pack", "feedback"]:
			if modal_case == "feedback":
				hud.show_feedback()
			else:
				hud._show_offer(modal_case.get_slice(":", 1))
			hud._show_next_modal()
			await _settle(0.8)
			await _scan_state(hud, lang, modal_case)
			for m in hud.modal_layer.get_children():
				m.queue_free()
			hud._modal_queue.clear()
			hud._modal_open = false
			await _frames(3)
		# every event modal
		for e in Data.events:
			var offer: Dictionary = ContractManager.offers[0]
			var v: Dictionary = ContractManager.best_vehicle(offer)
			if v.is_empty():
				break
			var job: Dictionary = ContractManager.dispatch(offer, v, "normal")
			if job.is_empty():
				break
			job.event_id = str(e.id)
			job.phase = "out"
			EventManager.trigger(job)
			await _settle(0.8)
			await _scan_state(hud, lang, "event:" + str(e.id))
			EventManager.resolve(job, 0, true)
			hud._modal_queue.clear()
			for m in hud.modal_layer.get_children():
				m.queue_free()
			hud._modal_open = false
			await _frames(4)
			ContractManager.refresh_offers()
			if ContractManager.offers.is_empty():
				ContractManager.refresh_offers()
		main.queue_free()
		await _frames(4)
	var keys := found.keys()
	keys.sort()
	for k in keys:
		print("LAYOUT ", k, " | ", found[k])
	print("LAYOUT-SCAN viewport ", root.get_visible_rect().size)
	print("LAYOUT-SCAN done: ", scanned, " screens, ", found.size(), " findings, languages ", langs)
	root.get_node("SaveManager").delete_save()
	quit(1 if found.size() > 0 else 0)   # gate: Jenkins runs it for a few long languages (SCAN_LANGS)


func _scan_state(hud: Node, lang: String, screen: String) -> void:
	_scan(hud, lang, screen)


func TutorialManager_skip() -> void:
	root.get_node("TutorialManager").skip()
