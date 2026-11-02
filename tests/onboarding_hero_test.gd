extends Node
## Onboarding v2 (owner #48 / #119): the story intro's three describing beats (the garage + hatchback, the regions, the
## borders) show three different real frames of this game (assets/ui/onboarding + manifest.json) in the story card —
## never an icon fallback — while the six beats keep their texts, Continue / "That's the one!" / Skip intro, ONE tap on
## the last beat finishes the intro once and saves intro_done, and Replay intro leaves nothing behind.
## Real taps: mouse press/release, and a phone finger (touch + its emulated mouse copy, as on a device).
## Run: godot --headless --path . res://tests/onboarding_hero_test.tscn   (last line: ONBOARDING checks=N failures=0)

const HERO_SIZE := Vector2(512, 384)
const BUDGET := 1048576   ## the three heroes together ≤ 1 MB
const DIR := "res://assets/ui/onboarding/"

var failures := 0
var checks := 0
var main: Node
var hud: Control


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("ONBOARDING ", "ok   " if ok else "FAIL ", label)


func frames(n := 3) -> void:
	for i in n:
		await get_tree().process_frame


func wait(s: float) -> void:
	await get_tree().create_timer(s, true, false, true).timeout


func _ready() -> void:
	get_tree().create_timer(900.0, true, false, true).timeout.connect(func(): get_tree().quit(2))
	await get_tree().process_frame
	SaveManager.delete_save()
	GameManager.settings.merge({"quality_notice": "", "language": "en", "sound": false, "music": false, "voice": false}, true)
	GameManager.apply_language()
	_assets()
	main = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	get_tree().current_scene = main
	await frames(6)
	hud = main.hud
	# a new game: the title card's own button, one finger tap, opens the first-launch intro
	var title: Control = hud.title_card
	check(is_instance_valid(title), "a new game opens on the title card")
	if is_instance_valid(title):
		await _tap((title.find_child("title_start", true, false) as Control).get_global_rect().get_center(), false)
		await frames(4)
	check(is_instance_valid(hud.onboarding), "the title tap opens the first-launch story intro")
	if is_instance_valid(hud.onboarding):
		await _flow("en first launch", true, false)
	await _replay_flow("en finger", false, false)
	await _replay_flow("en card tap", true, true)
	await _skip()
	for lang in ["de", "ja"]:   # the same texts in another language: the beats keep their heroes
		GameManager.settings["language"] = lang
		GameManager.apply_language()
		await _replay_flow(lang, lang == "de", false)
	GameManager.settings["language"] = "en"
	GameManager.apply_language()
	await _replay_leak()
	print("ONBOARDING checks=%d failures=%d" % [checks, failures])
	SaveManager.delete_save()
	get_tree().quit(0 if failures == 0 else 1)


func _assets() -> void:
	var heroes: Dictionary = Onboarding.HEROES
	var beats: Array = Data.onboarding.get("intro", [])
	var cams := []
	for b in beats:
		cams.append(str(b.get("cam", "")))
	var seen := {}
	var total := 0
	for cam in heroes:
		var path := str(heroes[cam])
		check(cams.count(cam) == 1, "hero beat '%s' is one of the six story beats" % cam)
		check(path.begins_with(DIR) and not seen.has(path), "beat '%s' has its own hero %s" % [cam, path])
		seen[path] = true
		check(ResourceLoader.exists(path), "%s is imported (a missing file must not pass as a hidden picture)" % path)
		var tex: Texture2D = load(path) if ResourceLoader.exists(path) else null
		check(tex != null and tex.get_size() == HERO_SIZE, "%s is a 512×384 texture" % path)
		var img := Image.load_from_file(ProjectSettings.globalize_path(path))
		check(img != null and img.get_format() == Image.FORMAT_RGB8, "%s is an RGB PNG (no alpha)" % path)
		total += FileAccess.get_file_as_bytes(path).size()
	check(seen.size() == 3, "three beats, three different heroes")
	check(total > 0 and total <= BUDGET, "the three heroes are ≤ 1 MB together (%d B)" % total)
	var manifest: Variant = JSON.parse_string(FileAccess.get_file_as_string(DIR + "manifest.json"))
	check(manifest is Dictionary and (manifest as Dictionary).get("images", []).size() == 3, "manifest.json describes three frames")
	if manifest is Dictionary:
		check(str(manifest.get("game", "")) == "smuggler" and str(manifest.source.get("commit", "")).length() == 40,
				"manifest names the game and the source commit")
		var hashes := {}
		for entry in manifest.images:
			var path := DIR + String(entry.file)
			check(seen.has(path), "manifest frame %s is a beat's hero" % entry.file)
			check(FileAccess.get_sha256(path) == String(entry.sha256), "%s matches its manifest sha256" % entry.file)
			hashes[String(entry.sha256)] = true
			check(not String(entry.get("camera", "")).is_empty() and entry.get("state", {}).has("garage_level")
					and String(entry.get("earned_by", "")).contains("_bot_step"), "%s records camera + bot-earned state" % entry.file)
		check(hashes.size() == 3, "three different files (sha256)")


func _settled(ob) -> void:
	for i in 120:   # ≤ 6 s of real time (headless frames are not paced)
		var busy: bool = (ob._typing != null and ob._typing.is_running()) or ob._hero.animating()
		if not busy:
			break
		await wait(0.05)
	await wait(0.45)


func _button(ob) -> Button:
	if ob._name_box.visible:
		for b in ob._name_box.find_children("*", "Button", false, false):
			if (b as Button).text == tr("That's the one!"):
				return b
		return null
	return ob._continue


## One pass over the six beats. card_tap: a finger on the story text (the whole card advances), else the beat's button.
func _flow(how: String, mouse: bool, card_tap: bool) -> void:
	var ob = hud.onboarding
	var done := [0]
	ob.finished.connect(func(): done[0] += 1)
	var beats: Array = Data.onboarding.get("intro", [])
	var shown := {}
	for i in beats.size():
		await _settled(ob)
		var beat: Dictionary = beats[i]
		var cam := str(beat.get("cam", ""))
		var last: bool = i == beats.size() - 1
		check(ob._index == i, "%s: beat %d is showing" % [how, i + 1])
		check(ob._text.text == tr(str(beat.text)).replace("{company}", ProgressionManager.company_name()),
				"%s: beat %d keeps its text" % [how, i + 1])
		var hero = ob._hero
		var pic: Texture2D = hero.art
		if Onboarding.HEROES.has(cam):
			var path := str(Onboarding.HEROES[cam])
			check(hero.visible and hero.has_picture() and pic != null and pic.resource_path == path and pic.get_size() == HERO_SIZE,
					"%s: beat %d (%s) draws its own hero (%s)" % [how, i + 1, cam, pic.resource_path if pic else "none"])
			var r: Rect2 = hero.get_global_rect()
			check(r.size.y >= 140.0 and r.size.x > r.size.y and ob._panel.get_global_rect().grow(1).encloses(r),
					"%s: beat %d hero sits inside the story card %s" % [how, i + 1, r])
			if pic:
				shown[pic.resource_path] = true
		else:
			check(not hero.visible and pic == null, "%s: beat %d (%s) shows no picture and holds no texture" % [how, i + 1, cam])
		var b := _button(ob)
		var label := tr("That's the one!") if bool(beat.get("name_step", false)) else (tr("Start playing") if last else tr("Continue"))
		check(b != null and b.is_visible_in_tree() and b.text == label, "%s: beat %d button '%s'" % [how, i + 1, label])
		var skip: Button = ob.find_child("intro_skip", true, false)
		var view := Rect2(Vector2.ZERO, ob.get_viewport_rect().size)
		check(skip != null and skip.is_visible_in_tree() and view.grow(1).encloses(skip.get_global_rect()),
				"%s: beat %d Skip intro is on screen" % [how, i + 1])
		if card_tap and not bool(beat.get("name_step", false)):
			await _tap(ob._text.get_global_rect().get_center(), mouse)
		elif b:
			await _tap(b.get_global_rect().get_center(), mouse)
		await frames(3)
		if not last:
			check(is_instance_valid(ob) and ob._index == i + 1 and done[0] == 0,
					"%s: one tap on beat %d = exactly one beat (now %d)" % [how, i + 1, ob._index + 1 if is_instance_valid(ob) else -1])
	var gone: WeakRef = weakref(ob)
	await wait(0.6)   # the 0.35 s fade, then queue_free
	await frames(2)
	check(shown.size() == 3, "%s: three different heroes were shown" % how)
	check(done[0] == 1, "%s: ONE tap on the last beat finished the intro once (%d)" % [how, done[0]])
	check(gone.get_ref() == null and not is_instance_valid(hud.onboarding), "%s: the intro is gone" % how)
	var flags: Dictionary = SaveManager.load_data().get("progression", {}).get("flags", {})
	check(ProgressionManager.intro_done() and bool(flags.get("intro_done", false)) and main.world.rig.input_enabled,
			"%s: intro_done saved, the camera is the player's again" % how)


## Settings → Replay intro (settings_sheet.gd), then one pass.
func _replay_flow(how: String, mouse: bool, card_tap: bool) -> void:
	ProgressionManager.flags["intro_done"] = false
	hud._maybe_onboard()
	await frames(3)
	check(is_instance_valid(hud.onboarding), how + ": Replay intro opens the story")
	if is_instance_valid(hud.onboarding):
		await _flow(how, mouse, card_tap)


func _skip() -> void:
	ProgressionManager.flags["intro_done"] = false
	hud._maybe_onboard()
	await frames(3)
	var ob = hud.onboarding
	var done := [0]
	ob.finished.connect(func(): done[0] += 1)
	await _settled(ob)
	# move to a hero beat first (Continue), then Skip there: the hero goes with the intro
	await _tap(ob._continue.get_global_rect().get_center(), true)
	await _settled(ob)
	check(ob._hero.visible, "skip: beat 2 shows its hero before Skip")
	await _tap((ob.find_child("intro_skip", true, false) as Control).get_global_rect().get_center(), false)
	await wait(0.6)
	await frames(2)
	check(done[0] == 1 and not is_instance_valid(hud.onboarding) and ProgressionManager.intro_done(),
			"Skip intro: one tap closes the story once and saves intro_done (%d)" % done[0])


## Replay intro through the intro's own handlers (Continue / name OK), no waiting for the typewriter.
func _replay(n: int) -> void:
	for k in n:
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
			await frames(2)
		await wait(0.6)
		await frames(2)


func _replay_leak() -> void:
	await _replay(2)   # warm: fonts / heroes / lazily made kit objects exist once
	var before := _counts()
	await _replay(8)
	await wait(6.0)   # the naming beat's confetti / walkers are timed effects
	var after := _counts()
	check(after.hud == before.hud and after.resources <= before.resources and after.orphans <= before.orphans,
			"Replay intro ×8 leaves nothing behind in the HUD / resources / orphans %s -> %s" % [before, after])
	check(after.nodes <= before.nodes + 4, "Replay intro ×8: the scene tree does not grow (ambient traffic ±2) %s -> %s" % [before, after])


func _counts() -> Dictionary:
	return {"hud": hud.find_children("*", "", true, false).size(),
		"nodes": int(Performance.get_monitor(Performance.OBJECT_NODE_COUNT)),
		"resources": int(Performance.get_monitor(Performance.OBJECT_RESOURCE_COUNT)),
		"orphans": int(Performance.get_monitor(Performance.OBJECT_ORPHAN_NODE_COUNT))}


## mouse: a desktop click. else a phone finger: the touch and its emulated mouse copy, as a device delivers them.
func _tap(at: Vector2, mouse: bool) -> void:
	var vp := get_tree().root
	for down in [true, false]:
		var m := InputEventMouseButton.new()
		m.button_index = MOUSE_BUTTON_LEFT
		m.pressed = down
		m.button_mask = MOUSE_BUTTON_MASK_LEFT if down else 0
		m.position = at
		m.global_position = at
		if not mouse:
			m.device = InputEvent.DEVICE_ID_EMULATION
			var t := InputEventScreenTouch.new()
			t.index = 0
			t.position = at
			t.pressed = down
			vp.push_input(t, true)
		vp.push_input(m, true)
		await get_tree().process_frame
