extends Node
## Local JSON save with an automatic backup and corruption recovery.

signal saved
signal loaded
signal health_changed(ok: bool)
## A save file exists but cannot be read (games/CLAUDE.md §14, LESSONS B1): code = which file, recovered = the backup
## was loaded instead. Also sent to Analytics as 'save_unreadable'.
signal save_unreadable(code: String, recovered: bool)
const KEEP_UNREADABLE := 7 # <save>.unreadable, .unreadable.1 … .7 — never deleted by the game
const Validator = preload("res://scripts/core/save_validator.gd")
var write_protected := false
var fail_next_write := false # QA fault injection only, ignored outside isolated tests

# Test scenes and explicit QA runs use a separate directory, before any autoload reads.
var qa_mode: bool = "--qa" in OS.get_cmdline_user_args() or _is_test_scene()
var root_path: String = "user://qa/tests/" if _is_test_scene() else ("user://qa/" if qa_mode else "user://")
var SAVE_PATH: String = root_path + "savegame.json"
var BACKUP_PATH: String = root_path + "savegame.bak.json"
var TEMP_PATH: String = root_path + "savegame.tmp.json"
var SETTINGS_PATH: String = root_path + "settings.json"


func _is_test_scene() -> bool:
	for arg in OS.get_cmdline_args():
		if arg.contains("tests/"):
			return true
	return false


func _ready() -> void:
	DirAccess.make_dir_recursive_absolute(root_path)

const VERSION := 2

var last_error := ""
const SECTION_TYPES := {
	"economy": {"stats": TYPE_DICTIONARY},
	"regions": {"depots": TYPE_ARRAY},
	"progression": {"business": TYPE_DICTIONARY, "auto": TYPE_DICTIONARY, "flags": TYPE_DICTIONARY, "connections": TYPE_DICTIONARY},
	"vehicles": {"vehicles": TYPE_ARRAY, "drivers": TYPE_ARRAY, "market": TYPE_ARRAY},
	"contracts": {"offers": TYPE_ARRAY, "queue": TYPE_ARRAY, "jobs": TYPE_ARRAY},
}


func has_save() -> bool:
	return FileAccess.file_exists(SAVE_PATH) or FileAccess.file_exists(BACKUP_PATH)


func snapshot() -> Dictionary:
	return {
		"version": VERSION, "game": Data.title(), "time": Time.get_unix_time_from_system(),
		"economy": EconomyManager.to_dict().duplicate(true), "regions": RegionManager.to_dict().duplicate(true),
		"heat": HeatManager.to_dict().duplicate(true), "progression": ProgressionManager.to_dict().duplicate(true),
		"vehicles": VehicleManager.to_dict().duplicate(true), "contracts": ContractManager.to_dict().duplicate(true),
		"tutorial": TutorialManager.to_dict().duplicate(true), "settings": GameManager.settings.duplicate(true),
		"session": GameManager.session.duplicate(true), "daily": DailyManager.to_dict(),
	}


func _failure(reason: String) -> bool:
	last_error = reason
	if qa_mode: print("SAVE_FAILURE: ", reason)
	health_changed.emit(false)
	return false


func atomic_json(path: String, data: Dictionary) -> bool:
	if qa_mode and fail_next_write:
		fail_next_write = false
		return _failure("Injected storage failure")
	var temp := path + ".tmp"
	var text := JSON.stringify(data)
	var f := FileAccess.open(temp, FileAccess.WRITE)
	if f == null: return _failure("Cannot open temporary save")
	f.store_string(text)
	f.flush()
	var err := f.get_error()
	f.close()
	if err != OK or FileAccess.get_file_as_string(temp) != text:
		return _failure("Cannot verify temporary save")
	if DirAccess.rename_absolute(temp, path) != OK: return _failure("Cannot replace save")
	return true


func save_game() -> bool:
	if write_protected: return _failure("A newer save is protected")
	var data := snapshot()
	if not Validator.valid(data): return _failure("Invalid game state: " + Validator.last_reason)
	# Copy the last valid primary atomically. Never rotate a corrupt primary over recovery.
	var primary := _read(SAVE_PATH)
	if write_protected: return _failure("A newer save is protected")
	if not primary.is_empty():
		if not atomic_json(BACKUP_PATH, primary): return false
	elif FileAccess.file_exists(SAVE_PATH) and not keep_unreadable(SAVE_PATH):
		# a damaged primary is the player's only copy when the backup is gone too: never write over it (§14)
		return _failure("Cannot preserve damaged save")
	if not atomic_json(SAVE_PATH, data): return false
	last_error = ""
	health_changed.emit(true)
	saved.emit()
	return true


func _read(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	var text := FileAccess.get_file_as_string(path)
	if text.is_empty():
		return {}
	# Damaged input is handled by load_data's recovery/warning path. The static
	# parse_string helper also emits an engine ERROR, although recovery succeeds.
	var parser := JSON.new()
	if parser.parse(text) != OK:
		return {}
	var parsed: Variant = parser.data
	if typeof(parsed) != TYPE_DICTIONARY:
		return {}
	var d: Dictionary = parsed
	if Validator.number(d.get("version", 1)) and float(d.get("version", 1)) > VERSION:
		write_protected = true
		last_error = "future_version"
		return {}
	if not Validator.valid(d): return {}
	# Version 1 has no receipt/daily section. Migrate in memory; original remains until successful commit.
	if int(d.get("version", 1)) < VERSION:
		d["version"] = VERSION
		d["session"] = {"generation": str(d.get("time", 0)), "settled_at": float(d.get("time", 0)), "receipt": {}}
		d["daily"] = {}
	return d



## Returns the save dictionary, falling back to the backup if the main file is corrupt.
func load_data() -> Dictionary:
	write_protected = false
	var d := _read(SAVE_PATH)
	if write_protected: return {}
	if d.is_empty():
		var damaged := FileAccess.file_exists(SAVE_PATH)
		if damaged:
			push_warning("Save file corrupted, trying backup")
			last_error = "corrupt"
		d = _read(BACKUP_PATH)
		if write_protected: return {}
		if damaged:
			var bak_bad := FileAccess.file_exists(BACKUP_PATH) and d.is_empty()
			_report_unreadable("primary" + ("+backup" if bak_bad else ""), not d.is_empty())
	return d


## Moves a damaged save aside as <path>.unreadable (older copies → .unreadable.1 … .KEEP_UNREADABLE). Moved, never
## deleted, so a new game or an autosave cannot write over it. True when nothing is left at `path`.
func keep_unreadable(path: String) -> bool:
	if not FileAccess.file_exists(path):
		return true
	var base := ProjectSettings.globalize_path(path + ".unreadable")
	for i in range(KEEP_UNREADABLE, 0, -1):
		var older := base + ("" if i == 1 else "." + str(i - 1))
		if FileAccess.file_exists(older):
			DirAccess.rename_absolute(older, base + "." + str(i))
	return DirAccess.rename_absolute(ProjectSettings.globalize_path(path), base) == OK


func _report_unreadable(code: String, recovered: bool) -> void:
	print("save_unreadable code=%s recovered=%d" % [code, int(recovered)])
	save_unreadable.emit(code, recovered)
	var a := get_node_or_null("/root/Analytics")
	if a and a.has_method("event"):
		a.event("save_unreadable", {"code": code, "recovered": int(recovered)})


func apply(d: Dictionary) -> bool:
	if d.is_empty() or not Validator.valid(d):
		return false
	# each section is restored defensively; a broken section falls back to defaults
	EconomyManager.from_dict(_section(d, "economy"))
	RegionManager.from_dict(_section(d, "regions"))
	HeatManager.from_dict(_section(d, "heat"))
	ProgressionManager.from_dict(_section(d, "progression"))
	VehicleManager.from_dict(_section(d, "vehicles"))
	ContractManager.from_dict(_section(d, "contracts"))
	TutorialManager.from_dict(_section(d, "tutorial"))
	if VehicleManager.vehicles.is_empty():
		# never leave the player without a way to play
		if VehicleManager.drivers.is_empty():
			VehicleManager.add_start_driver()
		VehicleManager.add_vehicle(str(Data.config.get("start_vehicle", "hatchback")), true)
	GameManager.session = d.get("session", {}).duplicate(true)
	DailyManager.from_dict(d.get("daily", {}))
	loaded.emit()
	return true


func _section(d: Dictionary, key: String) -> Dictionary:
	var s: Variant = d.get(key, {})
	if typeof(s) != TYPE_DICTIONARY:
		return {}
	var copy: Dictionary = s.duplicate(true)
	for field in SECTION_TYPES.get(key, {}):
		if copy.has(field) and typeof(copy[field]) != SECTION_TYPES[key][field]:
			copy.erase(field)
	return copy


func delete_save() -> void:
	write_protected = false
	var dir := DirAccess.open(root_path)
	if dir == null:
		return
	for p in [SAVE_PATH, BACKUP_PATH, TEMP_PATH]:
		if dir.file_exists(p.get_file()):
			dir.remove(p.get_file())


func load_settings() -> Dictionary:
	if not FileAccess.file_exists(SETTINGS_PATH):
		return {}
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(SETTINGS_PATH))
	return parsed if typeof(parsed) == TYPE_DICTIONARY else {}


func save_settings(s: Dictionary) -> bool:
	return atomic_json(SETTINGS_PATH, s)
