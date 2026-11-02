extends Node
var checks := 0
var failures := 0
func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		push_error("CAMERA_FRAME " + label)
func settle(w: World) -> void:
	await get_tree().process_frame
	await get_tree().process_frame
	w.rig._time = 0.0
	w.rig.yaw = CameraRig.DEFAULT_YAW
	w.rig._update_transform(0.0)
func _ready() -> void:
	SaveManager.delete_save()
	GameManager.start()
	GameManager.settings["quality"] = "low"
	GameManager.settings["reduced_motion"] = true
	ProgressionManager.flags["intro_done"] = true
	TutorialManager.skip()
	var w := World.new()
	add_child(w)
	var rig := w.rig
	for height in [1280, 1560]:
		get_tree().root.size = Vector2i(720, height)
		await get_tree().process_frame
		w._on_camera_viewport_changed()
		rig.frame_home(0.0)
		await settle(w)
		var vp := get_viewport().get_visible_rect().size
		var box := rig._project_box(w.active_camera_points())
		check(box.size.x / vp.x >= 0.72 and box.size.x / vp.x <= 0.81, "opening fills ~80%% width %d" % height)
		check(box.get_center().distance_to(vp * 0.5) < 2.0, "active contents centred %d" % height)
		check(rig.distance < 60.0, "opening closer than old title95 %d" % height)
		check(not rig.bounds.has_point(RegionManager.center(1)),
			"locked regions do not expand pan range")
		for factor in [1000.0, 0.00001]:
			rig.zoom_by(factor)
			check(rig.distance >= rig.min_distance and rig.distance <= rig.max_distance, "wheel/pinch bounds")
		rig.zoom_by(1000.0)
		for motion in [Vector2(100000,0), Vector2(-100000,0), Vector2(0,100000), Vector2(0,-100000)]:
			rig.pan_pixels(motion)
			check(rig.target.distance_to(rig.home_target) < 12.0, "wide view cannot pan into empty sea")
		rig._touches = {0: Vector2(100,100), 1: Vector2(300,100)}
		rig._start_pinch()
		rig._touches[1] = Vector2(101,100)
		rig._update_pinch()
		check(is_equal_approx(rig.distance, rig.max_distance), "real pinch clamps far limit")
		rig._touches[1] = Vector2(100000,100)
		rig._update_pinch()
		check(is_equal_approx(rig.distance, rig.min_distance), "real pinch clamps near limit")
		rig._touches.clear()
		rig.frame_home(0.0)
		await settle(w)
	var intro := Onboarding.new()
	intro.world = w
	intro._camera("map")
	await settle(w)
	check(rig.target.distance_to(rig.home_target) < 0.1 and absf(rig.distance-rig.home_distance) < 0.1,
		"intro map beat stays on active land with new zoom cap")
	intro.free()
	# W66 10-27 (#158-2): far away only the regions you have and the next one you can buy are named — a locked region
	# past the next one stays nameless under its clouds (its name peeked out at the World Map's top edge)
	for rv in w.regions:
		rv.set_label_lod(true)
	for rv in w.regions:
		var want: bool = int(rv.index) <= RegionManager.unlocked_count
		check(rv._name_label.visible == want, "far view: region %d named %s (%s)" % [rv.index, want, rv._name_label.text])
	var start_distance := rig.home_distance
	EconomyManager.earn(1e9, "test", false)
	EconomyManager.add_reputation(10000)
	check(RegionManager.unlock(1), "real second-region unlock")
	await settle(w)
	for rv in w.regions:
		rv.set_label_lod(true)
	check(w.regions.size() < 3 or w.regions[2]._name_label.visible, "after an unlock the new next region gets its name")
	check(w.regions.size() < 4 or not w.regions[3]._name_label.visible, "the region after it is still nameless")
	check(rig.home_distance > start_distance and rig.overview, "unlock expands and frames content")
	check(rig.target.distance_to(rig.home_target) < 0.1, "unlock reaches new centre")
	var second_distance := rig.home_distance
	for i in range(2, RegionManager.count()):
		check(RegionManager.unlock(i), "real unlock %d" % i)
	await settle(w)
	check(rig.home_distance > second_distance, "late frame grows")
	check(rig.camera.far > rig.max_distance, "far plane follows grown empire")
	var late_target := rig.home_target
	var late_distance := rig.home_distance
	w.free()
	w = World.new()
	add_child(w)
	await settle(w)
	check(w.rig.target.distance_to(late_target) < 0.1 and absf(w.rig.distance-late_distance) < 0.2,
		"restored empire opens on same frame")
	# Title and onboarding must not replace this with their former magic distances.
	var title := TitleCard.new()
	add_child(title)
	await settle(w)
	check(absf(w.rig.distance-w.rig.home_distance) < 0.1, "title keeps active frame")
	title.dismiss()
	await settle(w)
	check(absf(w.rig.distance-w.rig.home_distance) < 0.1, "tap to start keeps active frame")
	# A player gesture cancels an in-flight scripted move, while preserving inertia.
	GameManager.settings["reduced_motion"] = false
	w.rig.focus_on(Vector3.ZERO, 40.0, 1.0)
	w.rig._velocity = Vector2(100,0)
	w.rig.pan_pixels(Vector2(10,0))
	check(not w.rig._focus_tween.is_running(), "drag cancels camera tween")
	check(w.rig._velocity == Vector2(100,0), "pan preserves fling velocity")
	w.free()
	SaveManager.delete_save()
	print("CAMERA_FRAME checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)
