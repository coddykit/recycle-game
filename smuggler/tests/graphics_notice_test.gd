extends Node
## The one-time graphics notice really appears (once) in the HUD, in the player's language.
## Run: godot --headless --path . res://tests/graphics_notice_test.tscn
const LowShading := preload("res://addons/coddy_low_shading/low_shading.gd")
var failures := 0
var checks := 0


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("GFXNOTICE ", "PASS " if ok else "FAIL ", label)


func _boot() -> Node:
	var main: Node = load("res://scenes/main/main.tscn").instantiate()
	add_child(main)
	await get_tree().process_frame
	var hud = main.hud
	# the first screen is up: skip the title card / onboarding like a returning player
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	hud._restore_game_chrome()
	hud._show_next_modal()
	await get_tree().process_frame
	return main


func _modal(main: Node) -> BasicModal:
	for n in main.hud.modal_layer.get_children():
		if n is BasicModal:
			return n
	return null


func _texts(node: Node, out: Array) -> void:
	if node is Label or node is Button:
		out.append(node.text)
	for c in node.get_children():
		_texts(c, out)


func _ready() -> void:
	await get_tree().process_frame
	SaveManager.delete_save()
	GameManager.new_game()
	GameManager.settings["quality"] = "low"
	GameManager.settings["quality_policy_v"] = QualityPolicy.POLICY_VERSION
	GameManager.settings["quality_notice"] = "low"
	TranslationServer.set_locale("de")
	var main: Node = await _boot()
	var m := _modal(main)
	check(m != null, "notice modal appears after the first screen")
	var texts: Array = []
	if m:
		_texts(m, texts)
	print(texts)
	check(texts.any(func(t): return str(t).begins_with("Die Grafik ist auf Niedrig")), "text is localized (de)")
	check(texts.has("OK") and texts.has("Settings") and tr("Settings") == "Einstellungen", "buttons OK + Settings (button text auto-translates)")
	var buttons := m.find_children("*", "Button", true, false) if m else []
	check(buttons.size() == 2 and buttons.all(func(b): return b.custom_minimum_size.y >= 84), "two buttons, tap targets >= 84 px (kit 48 dp)")
	check(QualityPolicy.pending_notice(GameManager.settings) == "", "flag cleared when shown (appears once)")
	# W63 Low profile (doner-night 57d4e6f320 rule): on Low every lit region material is lit per vertex without
	# specular (palette / terrain / water shaders on their vertex_lighting variant); Medium puts per-pixel back
	var lsc: Dictionary = LowShading.counts()
	check(LowShading.cheap and lsc.base >= 10 and lsc.shader >= 3 and lsc.base_cheap == lsc.base and lsc.shader_cheap == lsc.shader, "Low: every lit material per vertex, no specular (%s)" % str(lsc))
	GameManager.settings["quality"] = "medium"
	UIManager.world.apply_quality()
	lsc = LowShading.counts()
	check(not LowShading.cheap and lsc.base_cheap == 0 and lsc.shader_cheap == 0, "Medium: the per-pixel look is back (%s)" % str(lsc))
	GameManager.settings["quality"] = "low"
	main.queue_free()
	await get_tree().process_frame
	var main2: Node = await _boot()
	check(_modal(main2) == null, "does not appear again on the next launch")
	main2.queue_free()
	TranslationServer.set_locale("en")
	SaveManager.delete_save()
	print("GFXNOTICE checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)
