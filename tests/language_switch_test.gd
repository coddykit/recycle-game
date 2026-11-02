extends SceneTree
## W51 (fleet, pharmacy P8 emulator finding: an open panel's Close stayed in the old language until a restart).
## Settings → Language (en → de) rebuilds every screen: afterwards nothing drawn may stay English — the reopened
## Settings, the HUD and anything the UIManager autoload keeps across the scene reload. A drawn text that is an English
## string of translations/strings.csv whose German differs is a leftover of the old language.
## godot --headless --path . -s tests/language_switch_test.gd   (last line: LANGUAGE checks=n failures=0)
var checks := 0
var failures := 0


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		print("FAIL ", label)


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	var english := _english()
	check(english.size() > 100, "German table read (%d English strings)" % english.size())
	await process_frame
	var gm: Node = root.get_node("GameManager")
	var ui: Node = root.get_node("UIManager")
	root.get_node("SaveManager").delete_save()
	gm.settings["language"] = "en"
	gm.apply_language()
	gm.new_game()
	var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	root.add_child(main)
	current_scene = main
	for i in 6:
		await process_frame
	ui.open_panel("settings")
	for i in 10:
		await process_frame
	var seen := []
	_leftovers(root, english, seen)
	check(not seen.is_empty(), "the scan sees the English UI first")
	gm.set_setting("language", "de")
	for i in 30:
		await process_frame
	check(TranslationServer.get_locale().begins_with("de"), "German selected")
	check(current_scene != null and current_scene != main, "every screen rebuilt (scene reloaded)")
	var left := []
	_leftovers(root, english, left)
	check(left.is_empty(), "after en → de nothing drawn stays English: %s" % [left])
	gm.settings["language"] = "auto"
	gm.apply_language()
	root.get_node("SaveManager").delete_save()
	print("LANGUAGE checks=%d failures=%d" % [checks, failures])
	quit(0 if failures == 0 else 1)


func _english() -> Dictionary:
	var out := {}
	var f := FileAccess.open("res://translations/strings.csv", FileAccess.READ)
	var head := Array(f.get_csv_line())
	var en := head.find("en")
	var de := head.find("de")
	while not f.eof_reached():
		var row := f.get_csv_line()
		if row.size() == head.size() and row[en].length() >= 3 and row[en] != row[de]:
			out[row[en]] = row[0]
	return out


func _leftovers(node: Node, english: Dictionary, out: Array) -> void:
	if node is CanvasItem and not (node as CanvasItem).is_visible_in_tree():
		return
	if node.is_queued_for_deletion():
		return
	if node is Label or node is Button or node is RichTextLabel:
		var text := node.atr(str(node.get("text"))).strip_edges()   # what is drawn (auto-translate included)
		if english.has(text):
			out.append("%s '%s' (%s)" % [node.name, text.left(40), english[text]])
	for c in node.get_children():
		_leftovers(c, english, out)
