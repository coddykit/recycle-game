extends RefCounted
## Performance gate scenes (games/tools/perf/perf_gate.gd, owner rule 2026-10-22): Low chosen before the world is built
## (as on a weak phone), a fresh save — "start" (new game, the opening framing), "midgame_near" (4 of the 8 regions
## unlocked, garage 3, depots, 5 vehicles on the road; close up on the home region) and "lategame_far" (the
## tests/tool_perf_tiers.gd late-game empire: every region + depot, garage maxed, the whole fleet on the road; zoomed out
## over the whole map — the densest view).

static var _main: Node
static var _built := ""


static func scenes() -> Array:
	return ["start", "midgame_near", "lategame_far", "tab_shop"]   # tab_shop: the shop sheet (list cards) on Low


static func _settle(tree: SceneTree, n: int) -> void:
	for i in n:
		await tree.process_frame


static func setup(tree: SceneTree, scene: String) -> void:
	var stage := scene.get_slice("_", 0)
	if _main == null:
		SaveManager.delete_save()
		# W66 (10-29): SMUGGLER_PERF_QUALITY=medium|high measures the HD tier; the gate itself stays on Low
		var q := OS.get_environment("SMUGGLER_PERF_QUALITY")
		GameManager.settings["quality"] = q if q in ["medium", "high"] else "low"
		GameManager.settings["quality_policy_v"] = QualityPolicy.POLICY_VERSION
		GameManager.settings["quality_notice"] = ""
		_main = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
		tree.root.add_child(_main)
		tree.current_scene = _main
		await _settle(tree, 10)
		var hud: Node = _main.hud
		if is_instance_valid(hud.title_card):
			hud.title_card.free()
		if is_instance_valid(hud.onboarding):
			hud.onboarding.free()
		ProgressionManager.flags["intro_done"] = true
		ProgressionManager.flags["tabs_all"] = true
		TutorialManager.skip()
		hud._restore_game_chrome()
		hud._modal_queue.clear()
	if stage == "midgame" and _built == "":
		await _build(tree, 4, 3, 5)
		_built = "midgame"
	elif stage == "lategame" and _built != "lategame":
		await _build(tree, RegionManager.count(), 6, 99)
		_built = "lategame"
	if scene == "tab_shop":
		_main.hud.open_panel("shop")
		await _settle(tree, 60)
		return
	var w: Node = _main.world
	if scene == "lategame_far":
		w.rig.focus_on(RegionManager.center(maxi(0, RegionManager.unlocked_count / 2)), w.rig.max_distance * 0.9, 0.05)
	elif stage == "midgame":
		w.rig.focus_on(RegionManager.center(0), 34.0, 0.05)
	await _settle(tree, 60)
	await tree.create_timer(2.0).timeout # the region unlock pop-in (1.6 s) has landed: the steady state is what is measured
	await _settle(tree, 10)


## Unlocks regions up to `regions`, upgrades the garage to `garage`, builds the depots, tops the fleet up to `fleet`
## vehicles and puts every idle vehicle on a contract.
static func _build(tree: SceneTree, regions: int, garage: int, fleet: int) -> void:
	for r in range(RegionManager.unlocked_count, regions):
		EconomyManager.earn(1e8, "perf", false)
		EconomyManager.add_reputation(400)
		RegionManager.unlock(r)
		await tree.process_frame
	EconomyManager.earn(1e9, "perf", false)
	for i in garage - ProgressionManager.garage_level:
		ProgressionManager.upgrade_garage()
	for d in range(RegionManager.unlocked_count):
		RegionManager.build_depot(d)
	for def in Data.vehicles:
		if VehicleManager.vehicles.size() >= fleet:
			break
		EconomyManager.earn(1e8, "perf", false)
		VehicleManager.buy(str(def.id))
	await _settle(tree, 10)
	var guard := 0
	while guard < 12:
		guard += 1
		ContractManager.refresh_offers()
		var sent := 0
		for c in ContractManager.offers.duplicate():
			var v: Dictionary = ContractManager.best_vehicle(c)
			if not v.is_empty() and ContractManager.dispatch_check(c, v) == "":
				ContractManager.dispatch(c, v, "normal")
				sent += 1
		if sent == 0:
			break


static func teardown(_tree: SceneTree) -> void:
	pass # one Main for the scenes (the gate quits afterwards); tab_shop is the last one
