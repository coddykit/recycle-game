extends RefCounted
## Onboarding v2 evidence for Borderline Empire (smuggler), driven by capture_heroes.py; user:// is a disposable HOME.
##   prepare (headless): the real Main starts a new game; the player taps the title card and skips the story intro with
##     the real buttons; then the game's reference bot (tests/soak_test.gd / tests/test_runner.gd `_bot_step`: best jobs,
##     events auto-resolved, sensible spending) plays in fixed 0.25 s game steps through the real managers. Three ordinary
##     saves are kept when the moments are earned — nothing is granted, no cash / unlock / entitlement is set.
##   hero (native, --index=0..2): the earned save is loaded by the real Main (GameManager.start), the title card is
##     tapped away, the UI CanvasLayer and the world-space words / markers are hidden, one 1024×768 frame → 512×384 RGB.
##   cards (native, Low): the shipped story intro in a fixed-size SubViewport, 4 sizes × 10 languages, all six beats,
##     every beat turned and the last one finished by ONE real press/release on its button.
##   memory (native, Low): first-launch intro peak memory (run on a `git archive` of the commit before for "before"),
##     and Replay intro ×5 node / resource / object counts.

const MOMENTS := ["first_run", "routes", "empire"]
const IDS := ["01-first-run", "02-routes", "03-empire"]
const CAMS := ["car", "map", "border"]   ## the story beats (Data.onboarding.intro[].cam) that show each hero
const HERO_SIZE := Vector2i(512, 384)
const SHOT := Vector2i(1024, 768)   ## rendered at 2× and filtered to 512×384: clean edges, smaller PNG
const SIZES := [Vector2i(720, 1280), Vector2i(720, 1560), Vector2i(960, 1280), Vector2i(1280, 720)]
const BOT_SEED := 20261024
const MAX_GAME_SECONDS := 4 * 3600.0
const HERO_QUALITY := "high"
const ROUTES_REACH := 16.0   ## world units around the centroid of the player's moving vehicles (fits a distance-48 frame)
const ROUTES_AWAY := 30.0    ## …and at least this far from the garage: out on the roads
const ROUTES_DISTANCE := 48.0
const FIRST_RUN_DISTANCE := 26.0
const EMPIRE_T := 0.25
const EMPIRE_DISTANCE := 135.0

var _t: SceneTree
var root: Window
var process_frame: Signal
var _args := {}
var _out := ""


func run(tree: SceneTree, args: Dictionary) -> bool:
	_t = tree
	root = tree.root
	process_frame = tree.process_frame
	_args = args
	_out = OS.get_environment("ONBOARDING_OUT")
	if _out.is_empty() or not _out.is_absolute_path():
		return _fail("ONBOARDING_OUT (absolute) required")
	# user:// must be the wrapper's throw-away HOME: a player / QA save on this Mac is never read or written
	if not OS.get_user_data_dir().contains("smg-onboarding-"):
		return _fail("disposable HOME required (run through capture_heroes.py): " + OS.get_user_data_dir())
	DirAccess.make_dir_recursive_absolute(_out)
	match String(args.get("mode", "")):
		"prepare":
			return await _prepare()
		"hero":
			return await _hero(int(args.get("index", "0")))
		"cards":
			return await _cards()
		"memory":
			return await _memory()
	return _fail("unknown mode")


func _fail(msg: String) -> bool:
	push_error("ONBOARDING_FAIL " + msg)
	return false


func _frames(n: int) -> void:
	for i in n:
		await process_frame


func _write_json(path: String, data: Variant) -> void:
	var f := FileAccess.open(path, FileAccess.WRITE)
	f.store_string(JSON.stringify(data, "  "))
	f.close()


func _quiet_settings(quality: String, language := "en") -> void:
	GameManager.settings.merge({"quality": quality, "quality_user": true, "quality_policy_v": QualityPolicy.POLICY_VERSION,
		"quality_notice": "", "sound": false, "music": false, "voice": false, "haptics": false, "language": language,
		"reduced_motion": false}, true)
	SaveManager.save_settings(GameManager.settings)
	GameManager.apply_language()
	AudioManager.set_sound(false)
	AudioManager.music_on = false
	AudioManager.voice_on = false


func _out_jobs() -> int:
	var n := 0
	for j in ContractManager.jobs:
		if j.phase == "out" and not j.get("pending_event", false):
			n += 1
	return n


func _state() -> Dictionary:
	var fleet := []
	for v in VehicleManager.vehicles:
		fleet.append(str(v.def))
	return {"garage_level": ProgressionManager.garage_level, "regions_unlocked": RegionManager.unlocked_count,
		"depots": RegionManager.depots.duplicate(), "vehicles": fleet, "jobs": ContractManager.jobs.size(),
		"deliveries": int(EconomyManager.stat("deliveries")), "stage": ProgressionManager.stage,
		"cash": snappedf(EconomyManager.cash, 0.01), "reputation": EconomyManager.reputation,
		"businesses": ProgressionManager.business.duplicate(), "intro_done": ProgressionManager.intro_done()}


## The fields a load must reproduce exactly (cash / deliveries move on with the game clock).
const SAME := ["garage_level", "regions_unlocked", "depots", "vehicles", "businesses", "intro_done"]


func _start_main(parent: Node) -> Node:
	var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	parent.add_child(main)
	if parent == root:
		_t.current_scene = main
	return main


## The title card's own "TAP TO START" button, pressed and released once like a finger.
func _tap_title(main: Node, sv: Viewport = null) -> bool:
	var hud: Control = main.hud
	for i in 30:
		if is_instance_valid(hud.title_card):
			break
		await process_frame
	if not is_instance_valid(hud.title_card):
		return false
	var b: Button = hud.title_card.find_child("title_start", true, false)
	await _frames(3)
	if sv != null:
		await _tap(sv, b.get_global_rect().get_center())
	else:
		b.pressed.emit()
	await _frames(4)
	return true


# ------------------------------------------------------------------ prepare: earned saves

## The game's reference bot (tests/soak_test.gd and tests/test_runner.gd `_bot_step`, copied verbatim).
func _bot_step() -> void:
	for j in EventManager.pending_jobs():
		EventManager.resolve(j, EventManager.auto_choice(j), true)
	var offers := ContractManager.offers.duplicate()
	offers.append_array(ContractManager.queue)
	offers.sort_custom(func(a, b): return float(a.reward) > float(b.reward))
	for c in offers:
		var v := ContractManager.best_vehicle(c)
		if v.is_empty():
			continue
		var route := "long" if HeatManager.get_heat(RegionManager.region_of_node(c.dest)) > 60 else "normal"
		ContractManager.dispatch(c, v, route)
	if ProgressionManager.garage_check().ok:
		ProgressionManager.upgrade_garage()
	var nxt := RegionManager.next_locked()
	if nxt >= 0 and RegionManager.unlock_check(nxt).ok:
		RegionManager.unlock(nxt)
	if VehicleManager.vehicles.size() < ProgressionManager.vehicle_slots():
		var best := ""
		for d in Data.vehicles:
			if VehicleManager.buy_check(d.id).ok and EconomyManager.cash > float(d.price) * 1.3:
				best = d.id
		if best != "":
			VehicleManager.buy(best)
	for b in ["automated_dispatch", "fleet_capacity", "contract_office", "warehouse", "dispatch_center",
			"operations_room", "regional_network", "maintenance_bay", "driver_lounge", "advanced_logistics"]:
		if ProgressionManager.business_check(b).ok and EconomyManager.cash > ProgressionManager.business_cost(b) * 2.0:
			ProgressionManager.buy_business(b)
	for cn in Data.connections:
		if ProgressionManager.connection_check(cn.id).ok:
			ProgressionManager.buy_connection(cn.id)
	for i in RegionManager.unlocked_count:
		if RegionManager.depot_check(i).ok and EconomyManager.cash > RegionManager.depot_cost(i) * 2.5:
			RegionManager.build_depot(i)
	for v in VehicleManager.vehicles:
		for u in ["engine", "capacity", "reliability", "navigation", "handling"]:
			if VehicleManager.upgrade_check(v, u).ok and EconomyManager.cash > VehicleManager.upgrade_cost(v, u) * 3.0:
				VehicleManager.upgrade(v, u)


func _prepare() -> bool:
	seed(BOT_SEED)
	_quiet_settings("low")
	SaveManager.delete_save()
	var main := _start_main(root)
	await _frames(10)
	# a new player: taps the title card, then "Skip intro" (the real buttons; the intro names the company itself)
	if not await _tap_title(main):
		return _fail("no title card on a new game")
	var hud: Control = main.hud
	if not is_instance_valid(hud.onboarding):
		return _fail("the story intro did not open")
	var skip: Button = hud.onboarding.find_child("intro_skip", true, false)
	skip.pressed.emit()
	await _frames(30)
	if not ProgressionManager.intro_done():
		return _fail("Skip intro did not finish the intro")
	GameManager.time_scale = 0.0001   # game time is driven below in fixed steps (as tests/soak_test.gd)
	var done := {}
	var t := 0.0
	var log_t := 0.0
	var steps := 0
	while t < MAX_GAME_SECONDS and done.size() < 3:
		GameManager.tick(0.25)
		t += 0.25
		steps += 1
		if steps % 4 == 0:
			_bot_step()
			if not done.has("first_run") and ContractManager.jobs.size() == 1 and ContractManager.jobs[0].phase == "out" \
					and ContractManager.progress(ContractManager.jobs[0]) >= 0.35:
				await _snapshot("first_run", done, t)
			elif done.has("routes") and not done.has("empire") and ProgressionManager.garage_level >= 4 \
					and RegionManager.unlocked_count >= 4 and RegionManager.depots.size() >= 2 \
					and VehicleManager.vehicles.size() >= 6 and _out_jobs() >= 4:
				await _snapshot("empire", done, t)
		if steps % 8 == 0:
			await process_frame   # the vehicle actors place themselves from their jobs' progress
			# activity: three or more of the player's own vehicles on the road close together, out in the regions (not
			# at the garage) — one frame of "clients who need things moved"
			if done.has("first_run") and not done.has("routes") and RegionManager.unlocked_count >= 2 and _out_jobs() >= 3:
				var spot := _busiest(main.world, ROUTES_REACH)
				if int(spot.vehicles) >= 3 and _from_hq(spot.at) > ROUTES_AWAY:
					await _snapshot("routes", done, t)
		if t - log_t >= 300.0:
			log_t = t
			print("BOT t=%ds %s" % [int(t), JSON.stringify(_state())])
	if done.size() != 3:
		return _fail("moments not earned in %d game s: %s" % [int(MAX_GAME_SECONDS), str(done.keys())])
	main.queue_free()
	await _frames(4)
	print("ONBOARDING_EARNED_PASS")
	return true


func _snapshot(id: String, done: Dictionary, t: float) -> void:
	await _frames(2)   # deferred signal handlers (world, HUD) run first, as after a real tap
	if not GameManager.save_now():
		push_error("ONBOARDING_FAIL save failed for " + id)
		return
	var text := FileAccess.get_file_as_string(SaveManager.SAVE_PATH)
	var f := FileAccess.open(_out.path_join(id + ".json"), FileAccess.WRITE)
	f.store_string(text)
	f.close()
	var meta := _state()
	meta["game_seconds"] = t
	meta["out_jobs"] = _out_jobs()
	meta["bot"] = "reference bot (tests/soak_test.gd _bot_step) every 1 game s, GameManager.tick(0.25), seed %d; title tapped, Skip intro" % BOT_SEED
	_write_json(_out.path_join(id + ".meta.json"), meta)
	done[id] = true
	print("EARNED ", id, " ", JSON.stringify(meta))


# ------------------------------------------------------------------ hero: one native frame per earned save

func _hero(index: int) -> bool:
	if DisplayServer.get_name() == "headless":
		return _fail("native renderer required")
	var moments := OS.get_environment("ONBOARDING_MOMENTS")
	var data: Variant = JSON.parse_string(FileAccess.get_file_as_string(moments.path_join(MOMENTS[index] + ".json")))
	var meta: Variant = JSON.parse_string(FileAccess.get_file_as_string(moments.path_join(MOMENTS[index] + ".meta.json")))
	if not (data is Dictionary) or not (meta is Dictionary):
		return _fail("earned moment missing: " + MOMENTS[index])
	# Loaded exactly as saved: a fresh timestamp keeps the minutes between prepare and capture from adding offline work.
	data["time"] = Time.get_unix_time_from_system()
	_quiet_settings(HERO_QUALITY)
	seed(BOT_SEED + index)   # the ambient traffic / walkers draw from the global RNG: the same frame on a rerun
	var f := FileAccess.open(SaveManager.SAVE_PATH, FileAccess.WRITE)
	f.store_string(JSON.stringify(data))
	f.close()
	var sv := SubViewport.new()
	sv.size = SHOT
	sv.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(sv)
	root.min_size = Vector2i.ZERO
	Engine.max_fps = 30
	var main := _start_main(sv)   # Main._ready → GameManager.start() loads the earned save
	await _frames(2)
	var now := _state()
	var loaded: Dictionary = JSON.parse_string(JSON.stringify(now))   # same number types as the parsed meta
	for k in SAME:
		if JSON.stringify(loaded[k]) != JSON.stringify(meta[k]):
			return _fail("the save did not load as earned: %s %s != %s" % [k, now[k], meta[k]])
	if not await _tap_title(main):
		return _fail("no title card")
	var world: World = main.world
	(main.get_node("UI") as CanvasLayer).visible = false
	await _frames(10)
	var rig := world.rig
	var note := ""
	match MOMENTS[index]:
		"first_run":
			# the hatchback on its first contract, followed like a tapped vehicle (World.select_vehicle)
			var job: Dictionary = ContractManager.jobs[0]
			var actor: VehicleActor = world.actors[job.vehicle_id]
			rig.follow(actor, FIRST_RUN_DISTANCE)
			note = "follows the hatchback on its first job (World.select_vehicle framing), distance %.0f" % FIRST_RUN_DISTANCE
		"empire":
			# the grown map: home valley towards the newest region, at a zoom a player reaches with two fingers
			var c := RegionManager.center(0).lerp(RegionManager.center(RegionManager.unlocked_count - 1), EMPIRE_T)
			_frame(rig, c, EMPIRE_DISTANCE)
			note = "home valley → region %d at %.2f, distance %.0f" % [RegionManager.unlocked_count - 1, EMPIRE_T, EMPIRE_DISTANCE]
	await _frames(60)   # traffic, walkers and the fleet move on with the game's own clock
	if MOMENTS[index] == "routes":
		# aimed right before the shot: the centroid of the player's moving vehicles that are close together
		var spot := _busiest(world, ROUTES_REACH)
		_frame(rig, spot.at, ROUTES_DISTANCE)
		note = "the player's busiest stretch of road (%s, %.0f from the garage), distance %.0f" % [str(spot), _from_hq(spot.at), ROUTES_DISTANCE]
		await _frames(2)
	var hidden := _hide_world_ui(world)
	await _frames(4)
	await RenderingServer.frame_post_draw
	var pixels := sv.get_texture().get_image()
	if pixels.get_size() != SHOT:
		return _fail("wrong viewport " + str(pixels.get_size()))
	pixels.convert(Image.FORMAT_RGB8)
	pixels.save_png(_out.path_join(IDS[index] + "-native.png"))   # the native source frame (evidence)
	pixels.resize(HERO_SIZE.x, HERO_SIZE.y, Image.INTERPOLATE_LANCZOS)
	if pixels.save_png(_out.path_join(IDS[index] + ".png")) != OK:
		return _fail("hero PNG failed")
	var moving := 0
	for id in world.actors:
		if (world.actors[id] as VehicleActor).moving:
			moving += 1
	var record := {"id": IDS[index], "moment": MOMENTS[index], "intro_beat": CAMS[index], "quality": HERO_QUALITY,
		"viewport": "%dx%d" % [SHOT.x, SHOT.y], "camera": str(rig.camera.global_transform),
		"camera_distance": snappedf(rig.distance, 0.01), "camera_note": note, "state": _state(), "earned_state": meta,
		"earned_by": meta.get("bot", ""), "moving_fleet": moving,
		"ui_hidden": "UI CanvasLayer (HUD / modals / tutorial) + %d world words / job pins / route ribbon / bubbles" % hidden}
	_write_json(_out.path_join("hero-%d.json" % index), record)
	main.queue_free()
	await _frames(4)
	sv.queue_free()
	await _frames(2)
	print("ONBOARDING_HERO_PASS ", IDS[index], " ", note)
	return true


## The point on the roads with the most of the player's own moving vehicles around it.
func _busiest(world: World, reach: float) -> Dictionary:
	var pts := []
	for id in world.actors:
		var a: VehicleActor = world.actors[id]
		var job := ContractManager.get_job_for_vehicle(str(id))
		if not job.is_empty() and not job.get("pending_event", false):
			pts.append(a.global_position)
	var best := {"vehicles": 0, "at": RegionManager.center(0)}
	for at in pts:
		var n := 0
		var sum := Vector3.ZERO
		for p in pts:
			if Vector2(p.x - at.x, p.z - at.z).length() < reach:
				n += 1
				sum += p
		if n > int(best.vehicles):
			best = {"vehicles": n, "at": Vector3(sum.x / n, 0, sum.z / n)}
	return best


## Camera at `at` / `dist` right away (the rig's own target + distance, no tween; its lerp state set to the same values).
func _frame(rig: CameraRig, at: Vector3, dist: float) -> void:
	rig.follow_node = null
	rig.target = Vector3(at.x, 0, at.z)
	rig.distance = clampf(dist, rig.min_distance, rig.max_distance)
	rig._cur_target = rig.target
	rig._cur_distance = rig.distance


func _from_hq(p: Vector3) -> float:
	var g := Sites.garage_transform(ProgressionManager.garage_level).origin
	return Vector2(p.x - g.x, p.z - g.z).length()


## World-space words and tap targets are UI too: region / town / HQ labels, job pins, the route ribbon, event bubbles,
## the selection ring.
func _hide_world_ui(world: World) -> int:
	var n := 0
	# render layers, not `visible`: RegionView.set_label_lod() re-shows its labels every frame
	for l in world.find_children("*", "Label3D", true, false):
		if (l as Label3D).visible:
			n += 1
		(l as Label3D).layers = 0
	for m in world.markers.values():
		(m as Node3D).visible = false
		n += 1
	world.route_line.visible = false
	for id in world.actors:
		var a: VehicleActor = world.actors[id]
		if a.bubble:
			a.bubble.layers = 0
		if a.selection_ring:
			a.selection_ring.visible = false
	return n


# ------------------------------------------------------------------ cards: the shipped intro, 10 languages × 4 sizes

static func logical(px: Vector2i) -> Vector2i:
	var s := minf(px.x / 720.0, px.y / 1280.0)   # project stretch: canvas_items / expand, 720×1280 base
	return Vector2i(int(round(px.x / s)), int(round(px.y / s)))


func _cards() -> bool:
	if DisplayServer.get_name() == "headless":
		return _fail("native renderer required")
	_quiet_settings("low")
	SaveManager.delete_save()
	root.min_size = Vector2i.ZERO
	root.size = Vector2i(480, 640)
	Engine.max_fps = 30
	var sv := SubViewport.new()
	sv.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	sv.size_2d_override_stretch = true
	_size_view(sv, SIZES[0])
	root.add_child(sv)
	var main := _start_main(sv)   # a brand-new game: title card, then the first-launch story intro
	await _frames(10)
	if not await _tap_title(main, sv):
		return _fail("no title card on a new game")
	var hud: Control = main.hud
	if not is_instance_valid(hud.onboarding):
		return _fail("the first-launch intro did not open after the title tap")
	var heroes: Dictionary = hud.onboarding.HEROES
	var beats: Array = Data.onboarding.get("intro", [])
	var records := []
	var shots := 0
	var taps := 0
	var last_taps := 0
	var first := true
	GameManager.settings["language"] = "en"
	var langs: Array = GameManager.LANGUAGES
	if OS.get_environment("ONBOARDING_LANGS") != "":   # a quick re-check of some languages (the evidence run is all ten)
		langs = Array(OS.get_environment("ONBOARDING_LANGS").split(","))
	for lang in langs:
		for extent in SIZES:
			var where := "%s %dx%d" % [lang, extent.x, extent.y]
			if first and lang != "en":   # a language subset: the English first-launch intro is skipped first
				(hud.onboarding.find_child("intro_skip", true, false) as Button).pressed.emit()
				await _frames(20)
				first = false
			if not first:
				GameManager.settings["language"] = lang
				GameManager.apply_language()
				_size_view(sv, extent)
				await _frames(3)
				ProgressionManager.flags["intro_done"] = false   # Settings → Replay intro (settings_sheet.gd)
				hud._maybe_onboard()
			first = false
			var ob = hud.onboarding
			var gone: WeakRef = weakref(ob)
			var finished := [0]
			ob.finished.connect(func(): finished[0] += 1)
			var folder := _out.path_join(lang).path_join("%dx%d" % [extent.x, extent.y])
			DirAccess.make_dir_recursive_absolute(folder)
			var view := Rect2(Vector2.ZERO, Vector2(logical(extent)))
			var seen := {}
			for i in beats.size():
				await _settle(ob)
				var beat: Dictionary = beats[i]
				var last: bool = i == beats.size() - 1
				if ob._index != i:
					return _fail("%s: beat %d is not showing (index %d)" % [where, i + 1, ob._index])
				if ob._step_label.text != tr("YOUR FIRST EMPIRE · %d / %d") % [i + 1, beats.size()]:
					return _fail("%s beat %d: step label '%s'" % [where, i + 1, ob._step_label.text])
				var want := tr(str(beat.text)).replace("{company}", ProgressionManager.company_name())
				if ob._text.text != want or ob._text.visible_ratio < 1.0:
					return _fail("%s beat %d: text changed or not fully shown" % [where, i + 1])
				var card: Rect2 = ob._panel.get_global_rect()
				if not view.grow(1).encloses(card):
					await RenderingServer.frame_post_draw
					sv.get_texture().get_image().save_png(folder.path_join("overflow-%d.png" % (i + 1)))
					return _fail("%s beat %d: card %s outside %s" % [where, i + 1, card, view])
				var hero = ob._hero
				var pic: Texture2D = hero.art if hero.visible else null
				var cam := str(beat.get("cam", ""))
				if heroes.has(cam):
					if not hero.visible or not hero.has_picture() or pic == null or pic.resource_path != heroes[cam] \
							or pic.get_size() != Vector2(HERO_SIZE):
						return _fail("%s beat %d: the visible hero is not %s (%s)" % [where, i + 1, heroes[cam], pic.resource_path if pic else "none"])
					var hr: Rect2 = hero.get_global_rect()
					if not card.grow(1).encloses(hr) or hr.size.y < 120.0 or hr.size.x < hr.size.y:
						return _fail("%s beat %d: hero %s not inside the card %s" % [where, i + 1, hr, card])
					seen[pic.resource_path] = true
				elif hero.visible:
					return _fail("%s beat %d: a hero on a beat without one" % [where, i + 1])
				var skip: Button = ob.find_child("intro_skip", true, false)
				if skip == null or not skip.is_visible_in_tree() or skip.text != tr("Skip intro") or not view.grow(1).encloses(skip.get_global_rect()):
					return _fail("%s beat %d: Skip intro missing or off screen (%s '%s' in %s)" % [where, i + 1,
						skip.get_global_rect() if skip else "none", skip.text if skip else "", view])
				var button: Button = _beat_button(ob)
				var label := tr("That's the one!") if bool(beat.get("name_step", false)) else (tr("Start playing") if last else tr("Continue"))
				if button == null or not button.is_visible_in_tree() or button.text != label:
					return _fail("%s beat %d: button '%s' != '%s'" % [where, i + 1, button.text if button else "none", label])
				if not view.grow(1).encloses(button.get_global_rect()):
					return _fail("%s beat %d: button outside the screen" % [where, i + 1])
				await RenderingServer.frame_post_draw
				var img := sv.get_texture().get_image()
				if img.get_size() != extent or img.save_png(folder.path_join("beat-%d.png" % (i + 1))) != OK:
					return _fail("card pixels failed " + where)
				shots += 1
				records.append({"locale": lang, "size": "%dx%d" % [extent.x, extent.y], "logical": str(view.size), "beat": i + 1,
					"cam": cam, "hero": pic.resource_path if pic else "", "card": str(card),
					"hero_rect": str(hero.get_global_rect()) if pic else "", "button": button.text})
				# ONE real press + release on the beat's button (the last one: "Start playing")
				await _tap(sv, button.get_global_rect().get_center())
				taps += 1
				await _frames(3)
				if not last and (not is_instance_valid(ob) or ob._index != i + 1 or finished[0] != 0):
					return _fail("%s: one tap on beat %d did not turn exactly one beat" % [where, i + 1])
			last_taps += 1
			await _frames(20)   # the 0.35 s fade, then queue_free
			var saved: Dictionary = SaveManager.load_data()
			var flags: Dictionary = saved.get("progression", {}).get("flags", {})
			if finished[0] != 1 or gone.get_ref() != null or not ProgressionManager.intro_done() \
					or not bool(flags.get("intro_done", false)) or not main.world.rig.input_enabled:
				return _fail("%s: one last-beat tap did not close the intro once (finished=%d freed=%s saved=%s)" % [where,
					finished[0], gone.get_ref() == null, flags.get("intro_done")])
			if seen.size() != 3:
				return _fail("%s: %d distinct heroes shown, want 3" % [where, seen.size()])
		print("ONBOARDING_CARDS ", lang, " PASS")
	_write_json(_out.path_join("cards.json"), records)
	main.queue_free()
	await _frames(4)
	sv.queue_free()
	await _frames(2)
	print("ONBOARDING_CARDS_PASS %d taps=%d last_beat_single_taps=%d" % [shots, taps, last_taps])
	return true


## The beat's one primary button: "That's the one!" on the naming beat, else Continue / Start playing.
func _beat_button(ob) -> Button:
	if ob._name_box.visible:
		for b in ob._name_box.find_children("*", "Button", false, false):
			if (b as Button).text == tr("That's the one!"):
				return b
		return null
	return ob._continue


func _size_view(sv: SubViewport, extent: Vector2i) -> void:
	sv.size = extent
	sv.size_2d_override = logical(extent)


## A player reads the beat: the typewriter line is complete, the card has popped in, the hero has faded in.
func _settle(ob) -> void:
	for i in 120:   # ≤ 6 s of real time
		var typing: bool = ob._typing != null and ob._typing.is_running()
		var hero = ob.get("_hero")
		if not typing and not (hero != null and hero.animating()):
			break
		await _t.create_timer(0.05).timeout
	await _t.create_timer(0.45).timeout
	await _frames(2)


# ------------------------------------------------------------------ memory: Low, first launch + replay

func _memory() -> bool:
	if DisplayServer.get_name() == "headless":
		return _fail("native renderer required")
	_quiet_settings("low")
	SaveManager.delete_save()
	Engine.max_fps = 30
	var main := _start_main(root)
	await _frames(30)
	var title := _mem()
	if not await _tap_title(main, root):
		return _fail("no title card on a new game")
	var hud: Control = main.hud
	if not is_instance_valid(hud.onboarding):
		return _fail("the first-launch intro did not open")
	var has_heroes: bool = hud.onboarding.get("_hero") != null
	# 1. what a weak phone holds at its first start: the story intro over the new game, every beat read and tapped on
	var peak := title.duplicate()
	var beats: Array = Data.onboarding.get("intro", [])
	for i in beats.size():
		var ob = hud.onboarding
		await _settle(ob)
		await _frames(10)
		var m := _mem()
		peak.static_mb = maxf(peak.static_mb, m.static_mb)
		peak.texture_mb = maxf(peak.texture_mb, m.texture_mb)
		await _tap(root, _beat_button(ob).get_global_rect().get_center())
		await _frames(3)
	await _frames(20)
	if is_instance_valid(hud.onboarding) or not ProgressionManager.intro_done():
		return _fail("the first-launch intro did not finish")
	var report := {"heroes_in_intro": has_heroes, "title_static_mb": title.static_mb, "title_texture_mb": title.texture_mb,
		"start_peak_static_mb": peak.static_mb, "start_peak_texture_mb": peak.texture_mb}
	# 2. Settings → Replay intro five times, every beat (the real handlers): nothing left behind
	await _replay(hud)
	var warm := _counts(hud)
	for i in 5:
		await _replay(hud)
	var cold := _counts(hud)
	report["replay_counts_before"] = warm
	report["replay_counts_after_5"] = cold
	_write_json(_out.path_join("memory.json"), report)
	print("ONBOARDING_MEMORY low ", JSON.stringify(report))
	# the whole tree also holds the ambient traffic, which comes and goes (±2 nodes between samples, before and after
	# this change alike): the gate is the HUD subtree the intro lives in, the resources and the orphans
	if cold.hud_nodes != warm.hud_nodes or cold.resources > warm.resources or cold.orphans > warm.orphans \
			or cold.nodes > warm.nodes + 4:   # one node per replay would be +5
		return _fail("Replay intro leaks: %s -> %s" % [warm, cold])
	main.queue_free()
	await _frames(6)
	print("ONBOARDING_MEMORY_PASS")
	return true


func _mem() -> Dictionary:
	return {"static_mb": snappedf(OS.get_static_memory_usage() / 1048576.0, 0.001),
		"texture_mb": snappedf(Performance.get_monitor(Performance.RENDER_TEXTURE_MEM_USED) / 1048576.0, 0.001)}


func _replay(hud: Control) -> void:
	ProgressionManager.flags["intro_done"] = false
	hud._maybe_onboard()
	for guard in 40:
		var ob = hud.onboarding
		if not is_instance_valid(ob) or ob.is_queued_for_deletion():
			break
		if ob._name_box.visible:
			ob._confirm_name()
		else:
			ob._advance()
		await _frames(4)
	await _frames(20)
	for i in 3:   # walkers / confetti of the naming beat are timed effects
		await _t.create_timer(2.5).timeout


func _counts(hud: Control) -> Dictionary:
	return {"hud_nodes": hud.find_children("*", "", true, false).size(),
		"nodes": int(Performance.get_monitor(Performance.OBJECT_NODE_COUNT)),
		"resources": int(Performance.get_monitor(Performance.OBJECT_RESOURCE_COUNT)),
		"objects": int(Performance.get_monitor(Performance.OBJECT_COUNT)),
		"orphans": int(Performance.get_monitor(Performance.OBJECT_ORPHAN_NODE_COUNT))}


func _tap(sv: Viewport, at: Vector2) -> void:
	for down in [true, false]:
		var ev := InputEventMouseButton.new()
		ev.position = at
		ev.global_position = at
		ev.button_index = MOUSE_BUTTON_LEFT
		ev.button_mask = MOUSE_BUTTON_MASK_LEFT if down else 0
		ev.pressed = down
		sv.push_input(ev, true)
		await process_frame
