extends Node
## Graphics quality policy (owner rule 2026-10-14): classifier, first-run default, one-time
## migration, persistence of an explicit choice, one-time notice.
## Run: godot --headless --path . res://tests/graphics_policy_test.tscn
var failures := 0
var checks := 0
const GIB := 1073741824


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("GFX ", "PASS " if ok else "FAIL ", label)


func _android(ram_gib: float, cores: int, adapter: String, vendor := "Qualcomm") -> Dictionary:
	return {"platform": "android", "ram_bytes": int(ram_gib * GIB), "cores": cores, "adapter": adapter,
			"vendor": vendor}


func _ready() -> void:
	await get_tree().process_frame
	_classifier()
	_policy_pure()
	_game_manager()
	_policy_v2()
	_ram_fallback()
	print("GFX checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)


func _classifier() -> void:
	check(DeviceTier.classify({"platform": "desktop", "ram_bytes": 0}) == "high", "desktop keeps High")
	check(DeviceTier.classify(_android(8, 8, "Adreno (TM) 660")) == "medium", "strong Android -> Medium")
	check(DeviceTier.classify(_android(12, 8, "Mali-G710 MC10", "ARM")) == "medium", "Mali-G710 is strong")
	check(DeviceTier.classify(_android(12, 8, "Mali-G610 MC4", "ARM")) == "medium", "Mali-G610 is not G6x-weak")
	check(DeviceTier.classify(_android(5.5, 8, "Adreno (TM) 660")) == "medium", "marketed 6 GB (5.5 GiB reported) passes")
	check(DeviceTier.classify(_android(5.21, 8, "Adreno (TM) 660")) == "medium", "just over the 5.2 GiB floor -> Medium")
	check(DeviceTier.classify(_android(5.19, 8, "Adreno (TM) 660")) == "low", "just under the 5.2 GiB floor -> Low")
	check(DeviceTier.classify(_android(3.7, 8, "Adreno (TM) 660")) == "low", "marketed 4 GB Android (3.7 GiB) -> Low")
	check(DeviceTier.classify(_android(8, 6, "Adreno (TM) 660")) == "low", "Android 6 cores -> Low")
	check(DeviceTier.classify(_android(8, 8, "Mali-G57 MC2", "ARM")) == "low", "Mali-G57 weak")
	check(DeviceTier.classify(_android(8, 8, "Mali-G68", "ARM")) == "low", "Mali-G68 weak")
	check(DeviceTier.classify(_android(8, 8, "Mali-T880", "ARM")) == "low", "Mali-T880 weak")
	check(DeviceTier.classify(_android(8, 8, "PowerVR Rogue GE8320", "Imagination Technologies")) == "low", "PowerVR weak")
	check(DeviceTier.classify(_android(8, 8, "Adreno (TM) 540")) == "low", "Adreno 5xx weak")
	check(DeviceTier.classify(_android(8, 8, "Adreno (TM) 619")) == "low", "Adreno 61x weak")
	check(DeviceTier.classify(_android(8, 8, "Adreno (TM) 620")) == "medium", "Adreno 620 not in the weak list")
	check(DeviceTier.classify(_android(8, 8, "", "")) == "low", "unknown GPU -> Low")
	check(DeviceTier.classify({"platform": "android"}) == "low", "missing inputs -> Low")
	check(DeviceTier.classify({"platform": "ios", "ram_bytes": 4 * GIB}) == "medium", "iOS 4 GB -> Medium")
	check(DeviceTier.classify({"platform": "ios", "ram_bytes": int(3.7 * GIB)}) == "medium", "iOS marketed 4 GB (3.7 GiB) -> Medium")
	check(DeviceTier.classify({"platform": "ios", "ram_bytes": int(2.8 * GIB)}) == "low", "iOS marketed 3 GB (2.8 GiB) -> Low")
	check(DeviceTier.classify({"platform": "ios", "ram_bytes": int(3.39 * GIB)}) == "low", "iOS just under the 3.4 GiB floor -> Low")
	check(DeviceTier.classify({"platform": "ios"}) == "low", "iOS unknown RAM -> Low")
	check(DeviceTier.classify({"platform": "web"}) == "low", "unrecognised platform -> Low")
	check(DeviceTier.classify(DeviceTier.detect()) in DeviceTier.TIERS, "detect() returns usable inputs")


func _policy_pure() -> void:
	var s := {"quality": "high"}
	check(QualityPolicy.migrate(s, "low"), "existing High player is migrated")
	check(s.quality == "low" and s.quality_policy_v == QualityPolicy.POLICY_VERSION and QualityPolicy.pending_notice(s) == "low", "moved to Low with flag + notice")
	s.quality = "high"   # player raises it afterwards
	check(not QualityPolicy.migrate(s, "low") and s.quality == "high", "migration happens once; explicit choice kept")
	var fresh := {}
	QualityPolicy.migrate(fresh, "medium")
	check(fresh.quality == "medium" and QualityPolicy.pending_notice(fresh) == "medium", "first-run default = device tier")
	var desk := {}
	QualityPolicy.migrate(desk, "high")
	check(desk.quality == "high" and QualityPolicy.pending_notice(desk) == "", "desktop: High, no popup")
	check(QualityPolicy.consume_notice(fresh) and not QualityPolicy.consume_notice(fresh), "notice is consumed exactly once")
	var idx := {}
	QualityPolicy.migrate(idx, "medium", "quality", true)
	check(idx.quality == 1, "index mode stores 0/1/2")


## Policy v2 (2026-10-23, S25 Ultra on Low): a v1 player on a strong phone on a Low he did not pick → Medium once,
## silently; the player's own pick, weak phones, Medium / High and desktop stand.
func _policy_v2() -> void:
	var s := {"quality": "low", "quality_policy_v": 1, "quality_notice": ""}
	check(QualityPolicy.migrate(s, "medium") and s.quality == "medium", "v2: strong phone on a wrong Low -> Medium once")
	check(QualityPolicy.pending_notice(s) == "" and s.quality_policy_v == 2, "v2: silently (no popup), flag stored")
	s.quality = "low"
	s.quality_user = true
	check(not QualityPolicy.migrate(s, "medium") and s.quality == "low", "v2 runs once: the player's later Low stands")
	var mine := {"quality": "low", "quality_policy_v": 1, "quality_user": true}
	QualityPolicy.migrate(mine, "medium")
	check(mine.quality == "low", "v2: a Low picked by the player in Settings is never changed")
	var weak := {"quality": "low", "quality_policy_v": 1}
	QualityPolicy.migrate(weak, "low")
	check(weak.quality == "low", "v2: a weak phone stays Low")
	for t in ["medium", "high"]:
		var o := {"quality": t, "quality_policy_v": 1}
		QualityPolicy.migrate(o, "medium")
		check(o.quality == t, "v2: %s stays %s" % [t, t])
	var desk := {"quality": "low", "quality_policy_v": 1}
	QualityPolicy.migrate(desk, "high")
	check(desk.quality == "low", "v2: a desktop is never touched")
	var idx := {"quality": 0, "quality_policy_v": 1}
	QualityPolicy.migrate(idx, "medium", "quality", true)
	check(idx.quality == 1, "v2: index mode 0 -> 1")
	# the game: Settings pick vs automatic downgrade recorded on disk
	SaveManager.save_settings({"quality": "low", "quality_policy_v": 1, "sound": true, "language": "auto"})
	GameManager.settings = {"quality": "", "quality_policy_v": 0, "quality_notice": "", "sound": true, "language": "auto"}
	GameManager.load_settings("medium")
	check(GameManager.quality() == "medium" and int(SaveManager.load_settings().quality_policy_v) == 2, "v2 in the game: Medium, saved")
	GameManager.set_quality("low")
	check(SaveManager.load_settings().get("quality_user") == true, "a Settings pick is recorded as the player's")
	GameManager.set_quality("low", false)
	check(SaveManager.load_settings().get("quality_user") == false, "an automatic downgrade is not the player's pick")
	DirAccess.remove_absolute(SaveManager.SETTINGS_PATH)


## Godot 4.7.2 reports physical RAM −1 on Android: the probe falls back to /proc/meminfo (PerfDevice.meminfo_mb).
func _ram_fallback() -> void:
	var path := ProjectSettings.globalize_path(SaveManager.SETTINGS_PATH.get_base_dir() + "/test_meminfo.txt")
	var f := FileAccess.open(path, FileAccess.WRITE)
	f.store_string("MemTotal:       11534336 kB\nMemFree:  100 kB\n")
	f.close()
	check(DeviceTier.ram_bytes(-1, path) == 11264 * 1048576, "physical -1 (Android) -> MemTotal from /proc/meminfo")
	check(DeviceTier.ram_bytes(8 * GIB, path) == 8 * GIB, "Godot's own figure wins when it has one")
	check(DeviceTier.ram_bytes(-1, path + ".missing") == 0, "no figure anywhere -> 0 (unknown)")
	var i := _android(8, 8, "Adreno (TM) 830")
	i.ram_bytes = DeviceTier.ram_bytes(-1, path)
	check(DeviceTier.classify(i) == "high", "an S25-class phone with physical -1 is a flagship via meminfo -> High (owner 10-23)")
	check(int(DeviceTier.detect().get("ram_bytes", 0)) > 0, "this machine's probe reads a RAM figure")
	DirAccess.remove_absolute(path)


func _game_manager() -> void:
	# real settings file under user://qa/tests/
	SaveManager.save_settings({"quality": "high", "sound": true, "language": "tr"})
	GameManager.settings = {"quality": "", "quality_policy_v": 0, "quality_notice": "", "sound": true, "language": "auto"}
	GameManager.load_settings("low")
	check(GameManager.quality() == "low", "existing High moved to device default on first launch")
	check(GameManager.settings.language == "tr" and GameManager.settings.sound == true, "other settings untouched")
	var on_disk := SaveManager.load_settings()
	check(int(on_disk.quality_policy_v) == QualityPolicy.POLICY_VERSION and on_disk.quality == "low", "migration flag persisted in settings storage")
	GameManager.consume_quality_notice()
	check(QualityPolicy.pending_notice(SaveManager.load_settings()) == "", "popup flag cleared once shown")
	GameManager.set_setting("quality", "high")
	check(GameManager.quality() == "high", "player picks High")
	GameManager.settings = {"quality": "", "quality_policy_v": 0, "quality_notice": "", "sound": true, "language": "auto"}
	GameManager.load_settings("low")   # next launch
	check(GameManager.quality() == "high", "explicit choice survives the next launch")
	check(QualityPolicy.pending_notice(GameManager.settings) == "", "no second popup on the next launch")
	GameManager.set_setting("quality", "medium")
	GameManager.settings = {"quality": "", "quality_policy_v": 0, "quality_notice": "", "sound": true, "language": "auto"}
	GameManager.load_settings("low")
	check(GameManager.quality() == "medium", "Medium choice persists too")
	DirAccess.remove_absolute(SaveManager.SETTINGS_PATH)
