extends Node
## Boots the game, ticks every system in a fixed order, autosaves and applies
## offline progress.

signal game_started(is_new: bool)
signal offline_report(report: Dictionary)

const LANGUAGES := ["en", "tr", "de", "es", "fr", "ja", "ko", "pt", "ru", "zh"]
const LANGUAGE_NAMES := ["English", "Türkçe", "Deutsch", "Español", "Français", "日本語", "한국어", "Português", "Русский", "简体中文"]

var session: Dictionary = {}
var _save_warning_at := -100000
var running := false
var settings := {"sound": true, "music": true, "voice": true, "haptics": true, "quality": "", "quality_policy_v": 0, "quality_notice": "", "language": "auto", "theme": "system", "reduced_motion": false, "advisor": true, "hq_signature": true, "speed": 1}
## Debug / test multiplier of the game clock (debug_tools, monkey and soak tests). The PLAYER's ×1 / ×2 is
## settings.speed (game_speed() below); both scale the same single dt in _process, so every system that ticks from it
## (economy, heat, vehicles and their trips, contracts, the tutorial / advisor) speeds up together.
var time_scale := 1.0
## Player game speeds (review 10-30 D3, tr 2★ "nice but too slow"): ×1 / ×2, offered once the tutorial is finished.
const SPEEDS := [1, 2]
var _save_timer := 0.0
var _dirty_timer := -1.0
var debug_args := {}
var _paused_at := 0.0
var _suspended := false


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	for a in OS.get_cmdline_user_args():
		var parts := a.trim_prefix("--").split("=")
		debug_args[parts[0]] = parts[1] if parts.size() > 1 else "1"
	load_settings(DeviceTier.default_quality())
	if debug_args.has("theme"):
		settings["theme"] = str(debug_args.theme)
	if debug_args.has("lang"):
		settings["language"] = str(debug_args.lang)
	apply_language()
	AudioManager.set_sound(bool(settings.sound))
	AudioManager.music_on = bool(settings.music)
	AudioManager.voice_on = bool(settings.get("voice", true))
	get_tree().set_auto_accept_quit(false)
	# significant actions trigger a (debounced) save
	for sig in [EconomyManager.reputation_changed, RegionManager.region_unlocked, RegionManager.depot_built,
			ProgressionManager.garage_upgraded, ProgressionManager.business_upgraded, VehicleManager.vehicle_added,
			VehicleManager.vehicle_upgraded, VehicleManager.drivers_changed, ContractManager.job_started,
			ContractManager.job_returned]:
		sig.connect(func(_a = null, _b = null): request_save())


## Reads settings.json and applies the one-time graphics policy (owner rule 2026-10-14): on the first
## launch of this policy every player moves to `device_default`, and the player's own choice is kept after.
func load_settings(device_default: String) -> void:
	var s := SaveManager.load_settings()
	for k in s:
		settings[k] = s[k]
	if QualityPolicy.migrate(settings, device_default):
		SaveManager.save_settings(settings)
	if not str(settings.get("quality", "")) in DeviceTier.TIERS:
		settings.quality = device_default


func start() -> void:
	if SaveManager.qa_mode and debug_args.has("fresh"):
		SaveManager.delete_save()
	var d := SaveManager.load_data()
	var is_new := d.is_empty()
	if SaveManager.write_protected:
		new_game()
		running = false
		return
	if is_new:
		new_game()
	else:
		SaveManager.apply(d)
		ProgressionManager.recompute_stage()
		_apply_offline(float(Time.get_unix_time_from_system()) - float(d.get("time", 0)), d)
	running = true
	_update_music()
	if not ProgressionManager.stage_changed.is_connected(_on_stage_changed):
		ProgressionManager.stage_changed.connect(_on_stage_changed)
	game_started.emit(is_new)


func _on_stage_changed(_stage: int) -> void:
	_update_music()


func _update_music() -> void:
	AudioManager.set_music_track("music_empire" if ProgressionManager.stage >= 4 else "music_main")


func new_game(prestige := false) -> void:
	if not prestige:
		session = {"generation": Crypto.new().generate_random_bytes(12).hex_encode(), "settled_at": Time.get_unix_time_from_system(), "receipt": {}}
		DailyManager.reset()
	EconomyManager.reset()
	RegionManager.reset()
	HeatManager.reset()
	ProgressionManager.reset()
	VehicleManager.reset()
	ContractManager.reset()
	TutorialManager.reset()
	VehicleManager.add_start_driver()
	VehicleManager.add_vehicle(str(Data.config.get("start_vehicle", "hatchback")), true)
	if not prestige:
		ContractManager.add_tutorial_offer()
	VehicleManager.refresh_market()
	if not prestige:
		save_now()


## Sell the empire and start over with Legacy bonuses (rebuilds the scene).
func prestige() -> int:
	running = false
	var stars := ProgressionManager.apply_prestige()
	set_meta("prestige_stars", stars)
	get_tree().reload_current_scene()
	return stars


func reset_progress() -> void:
	running = false
	SaveManager.delete_save()
	get_tree().reload_current_scene()


func _process(delta: float) -> void:
	if not running or _suspended or get_tree().paused: # paused = a full-screen ad is showing
		return
	var dt := minf(delta, 0.1) * time_scale * game_speed()
	tick(dt)
	_save_timer += delta
	if _save_timer >= float(Data.config.get("save_interval", 20.0)):
		save_now()
	if _dirty_timer >= 0:
		_dirty_timer -= delta
		if _dirty_timer < 0:
			save_now()


func tick(dt: float) -> void:
	EconomyManager.tick(dt)
	HeatManager.tick(dt)
	VehicleManager.tick(dt)
	ContractManager.tick(dt)
	TutorialManager.tick(dt)


func request_save() -> void:
	if running:
		_dirty_timer = 1.0


func save_now() -> bool:
	_save_timer = 0.0
	_dirty_timer = -1.0
	var ok := SaveManager.save_game()
	if not ok and Time.get_ticks_msec() - _save_warning_at > 10000:
		_save_warning_at = Time.get_ticks_msec()
		UIManager.toast("Could not save. Check available storage and try again.", UIKit.RED, "risk")
	return ok


## Follow the device language, with English for unsupported locales.
func language() -> String:
	var l := str(settings.get("language", "auto"))
	if l == "auto":
		l = OS.get_locale_language()
	l = l.replace("-", "_").get_slice("_", 0)
	return l if l in LANGUAGES else "en"


func apply_language() -> void:
	TranslationServer.set_locale(language())


## Particle amount multiplier per tier (fewer particles on Low / Medium).
const PARTICLE_SCALE := {"low": 0.4, "medium": 0.7, "high": 1.0}


func particle_scale() -> float:
	return float(PARTICLE_SCALE.get(quality(), 1.0))


## Called when the one-time graphics notice is actually shown.
func consume_quality_notice() -> void:
	var next := settings.duplicate(true)
	if QualityPolicy.consume_notice(next) and SaveManager.save_settings(next):
		settings = next


func quality() -> String:
	return str(settings.get("quality", "high"))


## The graphics tier: `user` = the player's own pick in Settings (true) or an automatic PerfGuard downgrade (false) —
## recorded as quality_user so policy v2 never overrides the player's pick.
func set_quality(tier: String, user := true) -> void:
	settings[QualityPolicy.KEY_USER] = user
	set_setting("quality", tier)


## The player's game speed: ×2 only when chosen AND the tutorial is over (the first session is never sped up; a save
## that replays the guide drops back to ×1 until it ends). Offline progress, saves, ads and review timers stay on
## real time; the pause (full-screen ad / app in the background) still stops the clock: _process returns early.
func speed_unlocked() -> bool:
	return TutorialManager.finished and ProgressionManager.intro_done()


func game_speed() -> float:
	return 2.0 if speed_unlocked() and int(settings.get("speed", 1)) == 2 else 1.0


## HUD ×1 / ×2 toggle; persisted in settings.json like every other setting. Returns the new speed.
func toggle_game_speed() -> int:
	if not speed_unlocked():
		return 1
	var nxt := 1 if int(settings.get("speed", 1)) == 2 else 2
	set_setting("speed", nxt)
	Analytics.event("game_speed", {"speed": int(settings.get("speed", 1))})
	return int(settings.get("speed", 1))


func set_setting(key: String, value: Variant) -> void:
	if key in ["language", "theme", "reduced_motion"] and not save_now(): return
	var next := settings.duplicate(true)
	next[key] = value
	if not SaveManager.save_settings(next):
		UIManager.toast("Could not save. Check available storage and try again.", UIKit.RED, "risk")
		return
	settings = next
	match key:
		"sound":
			AudioManager.set_sound(bool(value))
		"music":
			AudioManager.set_music(bool(value))
		"voice":
			AudioManager.set_voice(bool(value))
		"language", "theme", "reduced_motion":
			# rebuild every screen in the new language
			apply_language()
			set_meta("reopen_settings", true)
			running = false
			get_tree().reload_current_scene()
		"quality":
			if UIManager.world and UIManager.world.has_method("apply_quality"):
				UIManager.world.apply_quality()


func _apply_offline(seconds: float, d: Dictionary) -> void:
	var now := Time.get_unix_time_from_system()
	var last := float(session.get("settled_at", 0))
	var from := float(d.get("time", now - seconds))
	# Test callers may explicitly provide only an interval; real resume/load always supply a timestamp.
	if d.has("time"):
		seconds = maxf(0.0, now - maxf(from, last))
	seconds = clampf(seconds, 0.0, offline_cap())
	if seconds < 30.0: return
	var before := SaveManager.snapshot()
	var cash_before := EconomyManager.cash
	var deliveries_before := EconomyManager.stat("deliveries")
	ContractManager.offline = true
	for j in ContractManager.jobs.duplicate():
		if j.get("pending_event", false): EventManager.resolve(j, EventManager.auto_choice(j), true)
	var elapsed := 0.0
	while elapsed < seconds and not ContractManager.jobs.is_empty():
		var dt := minf(1.0, seconds - elapsed)
		# Existing repairs advance before returns start new repairs at the end of this step.
		VehicleManager.tick(dt)
		ContractManager.tick(dt)
		elapsed += dt
	ContractManager.offline = false
	VehicleManager.tick(maxf(0.0, seconds - elapsed))
	HeatManager.tick(seconds)
	var manager_income := 0.0
	if ProgressionManager.auto_enabled("regional_manager"):
		var ipm := float(d.get("economy", {}).get("ipm", 0.0))
		manager_income = round(maxf(0, ipm) * maxf(0, seconds - elapsed) / 60.0 * 0.5)
		EconomyManager.earn(manager_income, "offline", false)
	ContractManager.refresh_offers()
	var report := {"id": "%s:%d" % [session.get("generation", ""), int(now)],
		"seconds": seconds, "earned": EconomyManager.cash - cash_before,
		"deliveries": int(EconomyManager.stat("deliveries") - deliveries_before), "manager": manager_income,
		"applied": true, "acknowledged": false}
	session["settled_at"] = maxf(now, last)
	session["receipt"] = report
	if not save_now():
		SaveManager.apply(before)
		return
	if UIManager.hud != null and is_instance_valid(UIManager.hud):
		offline_report.emit(report)


## How long away time still counts: 2 h, 8 h with the Longer Offline Earnings product.
func offline_cap() -> float:
	var cap := float(Data.config.get("offline_cap_seconds", 7200))
	if Iap.has("offline_plus"):
		cap = maxf(cap, float(Data.config.get("offline_plus_cap_seconds", 28800)))
	return cap


func pending_return() -> Dictionary:
	var receipt: Dictionary = session.get("receipt", {})
	return receipt if not receipt.get("acknowledged", true) else {}


func acknowledge_return(id: String) -> void:
	var receipt: Dictionary = session.get("receipt", {})
	if receipt.get("id", "") != id: return
	receipt["acknowledged"] = true
	if not save_now(): receipt["acknowledged"] = false


func _notification(what: int) -> void:
	match what:
		NOTIFICATION_WM_CLOSE_REQUEST:
			if running and not save_now(): return
			get_tree().quit()
		NOTIFICATION_APPLICATION_PAUSED, NOTIFICATION_APPLICATION_FOCUS_OUT:
			if running:
				save_now()
				if not _suspended:
					_paused_at = Time.get_unix_time_from_system()
				_suspended = true
		NOTIFICATION_APPLICATION_RESUMED, NOTIFICATION_APPLICATION_FOCUS_IN:
			_suspended = false
			# phone came back from the background: catch up on the time away
			if running and _paused_at > 0.0:
				var away := Time.get_unix_time_from_system() - _paused_at
				_paused_at = 0.0
				if away > 30.0:
					_apply_offline(away, {"time": Time.get_unix_time_from_system() - away, "economy": EconomyManager.to_dict()})
