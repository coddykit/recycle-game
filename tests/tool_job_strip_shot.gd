extends Node
## Phone screenshots of the running-jobs strip with more jobs than fit the width, before and after a finger drag
## (owner report 2026-10-19). Needs a GPU window (not headless):
##   SHOT_LANG=tr SHOT_OUT=/tmp/strip godot --path . --resolution 540x960 res://tests/tool_job_strip_shot.tscn
## writes <SHOT_OUT>_a.png (start) and <SHOT_OUT>_b.png (after a sideways drag on a job card).
const T := preload("res://tests/touch_scroll_test.gd")


func _ready() -> void:
	var lang := OS.get_environment("SHOT_LANG") if OS.get_environment("SHOT_LANG") != "" else "en"
	var out := OS.get_environment("SHOT_OUT") if OS.get_environment("SHOT_OUT") != "" else "user://strip"
	await get_tree().process_frame
	SaveManager.delete_save()
	GameManager.settings["language"] = lang
	GameManager.settings["quality_notice"] = ""
	GameManager.apply_language()
	GameManager.new_game()
	var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	for i in 8:
		await get_tree().process_frame
	var hud: Node = main.hud
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
	EconomyManager.earn(5e7, "shot", false)
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
	for i in 9:
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
	await get_tree().create_timer(1.2).timeout
	hud._modal_queue.clear()
	for m in hud.modal_layer.get_children():
		m.queue_free()
	hud._modal_open = false
	hud._rebuild_jobs()
	await get_tree().create_timer(1.0).timeout
	_save(out + "_a.png")
	var t := T.new()
	var card: Control = hud.jobs_box.get_child(0)
	var from := card.get_global_rect().get_center()
	# a finger like a phone's: 12 steps of 16 ms (the drag ends in a stop, no fling)
	var vp := get_tree().root
	vp.push_input(t._mouse(from, true), true)
	vp.push_input(t._touch(from, true), true)
	var prev := from
	for i in range(1, 13):
		var p := from + Vector2(-330, 0) * (float(i) / 12.0)
		vp.push_input(t._motion(p, p - prev), true)
		vp.push_input(t._drag(p, p - prev), true)
		prev = p
		await get_tree().create_timer(0.016).timeout
	await get_tree().create_timer(0.3).timeout # held still: no fling
	_save(out + "_b.png")
	vp.push_input(t._mouse(prev, false), true)
	vp.push_input(t._touch(prev, false), true)
	await get_tree().create_timer(1.0).timeout
	print("job strip: ", hud.jobs_box.get_child_count(), " cards, scrolled ", hud.jobs_scroll.scroll_horizontal, " px")
	t.free()
	SaveManager.delete_save()
	get_tree().quit()


func _save(path: String) -> void:
	for i in 3:
		RenderingServer.force_draw()
	get_viewport().get_texture().get_image().save_png(path)
	print("saved ", path)
