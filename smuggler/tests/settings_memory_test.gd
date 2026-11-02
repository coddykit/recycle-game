extends Node
## E49 (W61 memory): the first Han / Hangul text makes the text server load its CJK line-break data (~55 MB each). An
## English player must not pay it for the Settings language buttons (日本語 / 한국어 / 简体中文): those names are pictures
## (tools/make_lang_labels.py) unless the UI runs in that language; no bundled font falls back to the OS.
## Prints MEMORY_STATIC around opening Settings.   Run: godot --headless --path . res://tests/settings_memory_test.tscn
var failures := 0
var checks := 0
var main: Node


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("SETTINGS-MEM ", "PASS " if ok else "FAIL ", label)


func frames(n := 3) -> void:
	for i in n:
		await get_tree().process_frame


static func _heavy(t: String) -> bool:
	for i in t.length():
		var c := t.unicode_at(i)
		if (c >= 0x3040 and c <= 0x30ff) or (c >= 0x4e00 and c <= 0x9fff) or (c >= 0xac00 and c <= 0xd7af):
			return true
	return false


func _buttons() -> Array:
	var out: Array = []
	if UIManager.current_panel == "settings" and is_instance_valid(main) and main.hud.current_sheet:
		out = main.hud.current_sheet.find_children("*", "Button", true, false)
	return out


func _ready() -> void:
	await get_tree().process_frame
	SaveManager.delete_save()
	GameManager.settings["quality_notice"] = ""
	for f in DirAccess.get_files_at("res://assets/fonts"):
		if f.ends_with(".import"):
			check(not FileAccess.get_file_as_string("res://assets/fonts/" + f).contains("allow_system_fallback=true"), f + ": no OS font fallback")
	GameManager.settings["language"] = "en"
	GameManager.apply_language()
	main = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	get_tree().current_scene = main
	await frames(4)
	var hud = main.hud
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	ProgressionManager.flags["intro_done"] = true
	hud._restore_game_chrome()
	await frames(2)
	var before := float(Performance.get_monitor(Performance.MEMORY_STATIC)) / 1048576.0
	UIManager.open_panel("settings")
	await frames(14)
	var after := float(Performance.get_monitor(Performance.MEMORY_STATIC)) / 1048576.0
	print("MEMORY_STATIC en: before %.1f MB, after Settings %.1f MB, delta %.1f MB" % [before, after, after - before])
	var heavy := 0
	var pics := 0
	var names := 0
	for b in _buttons():
		if _heavy((b as Button).text):
			heavy += 1
		if (b as Button).icon != null and (b as Button).icon.resource_path.begins_with("res://assets/ui/lang_"):
			pics += 1
		if (b as Button).text in ["Deutsch", "Español", "Français"]:
			names += 1
	check(names == 3, "the Settings sheet is up (%d Latin language buttons)" % names)
	check(heavy == 0, "English UI: no CJK text on any Settings button (%d)" % heavy)
	check(pics == 3, "the ja / ko / zh names are pictures (%d)" % pics)
	GameManager.set_setting("language", "ja")
	await frames(8)
	main = get_tree().current_scene
	var ja := false
	for b in _buttons():
		ja = ja or (b as Button).text == "日本語"
	check(ja, "Japanese UI: its own name stays text")
	GameManager.set_setting("language", "en")
	await frames(6)
	SaveManager.delete_save()
	print("SETTINGS-MEM checks=%d failures=%d" % [checks, failures])
	get_tree().quit(1 if failures > 0 else 0)
