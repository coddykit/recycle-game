extends SceneTree
## W68 overlap evidence frames (dev tool, not a suite; needs a GPU — run WINDOWED with --fixed-fps 60): the
## late-game empire of tests/fleet_overlap_test.gd (every region + depot, the whole fleet on contracts, ambient cars),
## HUD hidden. After 30 game s the camera frames the fleet vehicle with the most cars around it; a second frame 3 s later.
##   SM_QUALITY=low|medium|high SM_SHOTS=<dir> SM_PREFIX=before|after godot --path . --rendering-method \
##     gl_compatibility --resolution 1600x720 --fixed-fps 60 --audio-driver Dummy -s tools/overlap_shots.gd
var _main: Node


func _initialize() -> void:
	_run.call_deferred()


func _frames(n: int) -> void:
	for i in n:
		await process_frame


func _run() -> void:
	var q := OS.get_environment("SM_QUALITY") if OS.get_environment("SM_QUALITY") != "" else "low"
	var out := OS.get_environment("SM_SHOTS") if OS.get_environment("SM_SHOTS") != "" else "/private/tmp/sm-overlap-shots"
	var prefix := OS.get_environment("SM_PREFIX") if OS.get_environment("SM_PREFIX") != "" else "after"
	DirAccess.make_dir_recursive_absolute(out)
	seed(42)
	var sm: Node = root.get_node("SaveManager")
	var gm: Node = root.get_node("GameManager")
	sm.delete_save()
	gm.settings["quality"] = q
	gm.settings["quality_policy_v"] = load("res://scripts/core/quality_policy.gd").POLICY_VERSION if ResourceLoader.exists("res://scripts/core/quality_policy.gd") else 99
	gm.settings["quality_notice"] = ""
	_main = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	root.add_child(_main)
	await _frames(10)
	var hud: Node = _main.hud
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	var prog: Node = root.get_node("ProgressionManager")
	var eco: Node = root.get_node("EconomyManager")
	var rm: Node = root.get_node("RegionManager")
	var vm: Node = root.get_node("VehicleManager")
	var cm: Node = root.get_node("ContractManager")
	prog.flags["intro_done"] = true
	prog.flags["tabs_all"] = true
	root.get_node("TutorialManager").skip()
	hud._restore_game_chrome()
	hud._modal_queue.clear()
	for r in range(rm.unlocked_count, rm.count()):
		eco.earn(1e8, "shots", false)
		eco.add_reputation(400)
		rm.unlock(r)
		await process_frame
	eco.earn(1e9, "shots", false)
	for i in 6 - prog.garage_level:
		prog.upgrade_garage()
	for d in range(rm.unlocked_count):
		rm.build_depot(d)
	for def in root.get_node("Data").vehicles:
		eco.earn(1e8, "shots", false)
		vm.buy(str(def.id))
	await _frames(10)
	hud.visible = false
	var w: Node = _main.world
	for f in 60 * 30:
		if f % 60 == 0:
			cm.refresh_offers()
			for c in cm.offers.duplicate():
				var v: Dictionary = cm.best_vehicle(c)
				if not v.is_empty() and cm.dispatch_check(c, v) == "":
					cm.dispatch(c, v, "normal")
		await process_frame
	# the fleet vehicle on the road with the most cars / vehicles within 10 m
	var best: Node3D = null
	var most := -1
	var near_all: Array = w.traffic.positions()
	for id in w.actors:
		near_all.append((w.actors[id] as Node3D).global_position)
	for id in w.actors:
		var a: Node3D = w.actors[id]
		if cm.get_job_for_vehicle(str(id)).is_empty():
			continue
		var n := 0
		for p in near_all:
			if (p as Vector3).distance_to(a.global_position) < 10.0:
				n += 1
		if n > most:
			most = n
			best = a
	for shot in 2:
		if best != null:
			w.rig.focus_on(best.global_position, 30.0, 0.0)
		await _frames(8)
		await RenderingServer.frame_post_draw
		var path := "%s/%s_%s_road%d.png" % [out, prefix, q, shot + 1]
		root.get_texture().get_image().save_png(path)
		print("OVERLAP_SHOT ", path, " cars_near=", most)
		await _frames(60 * 3)
	quit()
