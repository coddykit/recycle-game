extends SceneTree
## Lists visible UI text that is still English in a non-English locale (hard-coded strings that skip translation).
## Not a gate test: names such as "Rustwater" or "Dot" are legitimately identical. Run: -s tests/tool_untranslated_scan.gd
## (env SCAN_LOCALE=ja, default ja)
var GameManager: Node
var UIManager: Node
var ContractManager: Node
var EconomyManager: Node
var RegionManager: Node
var ProgressionManager: Node
var VehicleManager: Node
var EventManager: Node


func _initialize() -> void:
	for n in ["GameManager", "UIManager", "ContractManager", "EconomyManager", "RegionManager", "ProgressionManager", "VehicleManager", "EventManager"]:
		set(n, root.get_node(n))
	_run()


func _walk(n: Node, out: Array) -> void:
	if n is CanvasItem and not (n as CanvasItem).visible:
		return
	out.append(n)
	for c in n.get_children():
		_walk(c, out)


func _run() -> void:
	var locale := OS.get_environment("SCAN_LOCALE") if OS.get_environment("SCAN_LOCALE") != "" else "ja"
	await process_frame
	root.get_node("SaveManager").delete_save()
	GameManager.settings["language"] = locale
	GameManager.apply_language()
	GameManager.new_game()
	var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	root.add_child(main)
	current_scene = main
	for i in 6:
		await process_frame
	var hud: Node = main.hud
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	ProgressionManager.flags["intro_done"] = true
	ProgressionManager.flags["tabs_all"] = true
	hud._restore_game_chrome()
	hud._modal_queue.clear()
	EconomyManager.earn(5e7, "scan", false)
	EconomyManager.add_reputation(500)
	for r in range(1, 4):
		RegionManager.unlock(r)
	for def in ["small_van", "armored"]:
		VehicleManager.buy(def)
	for i in 3:
		ContractManager.refresh_offers()
	var seen := {}
	var re := RegEx.new()
	re.compile("[A-Za-z]{3,}")
	var panels := ["contracts", "dispatch", "fleet", "map", "upgrades", "business", "settings", "shop", "daily"]
	for pn in panels:
		var arg: Variant = null
		if pn == "dispatch":
			arg = ContractManager.offers[0].id
		UIManager.open_panel(pn, arg)
		for i in 15:
			await process_frame
		if pn == "fleet":
			for tab in ["Showroom", "Drivers"]:
				pass
		var nodes: Array = []
		_walk(hud, nodes)
		for n in nodes:
			var t := ""
			if n is Label:
				t = (n as Label).text
			elif n is Button:
				t = (n as Button).text
			if t.strip_edges() == "":
				continue
			var shown: String = (n as Control).atr(t) if (n as Control).auto_translate_mode != Node.AUTO_TRANSLATE_MODE_DISABLED else t
			if re.search(shown) != null and shown == t and locale != "en":
				seen["%s | %s" % [pn, t.replace("\n", " / ")]] = true
		UIManager.close_panel()
		for i in 10:
			await process_frame
	for k in seen:
		print("UNTRANSLATED ", k)
	print("UNTRANSLATED-SCAN done, ", seen.size(), " candidates in ", locale)
	root.get_node("SaveManager").delete_save()
	quit(0)
