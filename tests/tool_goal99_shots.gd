extends Node
## W64 before/after phone screenshots of the real screens (needs a GPU window, not headless):
##   W64_OUT=/abs/dir W64_LANGS=en,de godot --path . --windowed --resolution 540x960 --audio-driver Dummy res://tests/tool_w64_shots.tscn
## Writes <lang>_<screen>.png: loading, title, onb1..onb3 (+name step), coach, hud, contracts, shop, settings (+scrolled),
## dialog, toast. The language is switched like the in-game picker (GameManager.apply_language), not with --language.
## Wipes the dev save of this machine (like tool_job_strip_shot). Re-run before the shop of the game is checked.
const BootScript := preload("res://scripts/boot.gd")
var out := ""


func _ready() -> void:
	out = OS.get_environment("W64_OUT") if OS.get_environment("W64_OUT") != "" else "user://w64"
	DirAccess.make_dir_recursive_absolute(out)
	var langs := (OS.get_environment("W64_LANGS") if OS.get_environment("W64_LANGS") != "" else "en,de").split(",")
	for lang in langs:
		await _run_lang(lang)
	get_tree().quit()


func _frames(n: int) -> void:
	for i in n:
		await get_tree().process_frame


func _shot(name: String, lang: String, wait := 0.5) -> void:
	await get_tree().create_timer(wait).timeout
	for i in 3:
		RenderingServer.force_draw()
	get_viewport().get_texture().get_image().save_png("%s/%s_%s.png" % [out, lang, name])
	print("saved ", name, " ", lang)


func _dump(hud: Node) -> void:
	print("P99 toast top=", hud.ui_toast.top)


func _run_lang(lang: String) -> void:
	SaveManager.delete_save()
	GameManager.settings["language"] = lang
	GameManager.settings["quality_notice"] = ""
	GameManager.apply_language()
	GameManager.new_game()
	# loading curtain
	var cur = BootScript.Curtain.new()
	cur.load_progress = 0.62
	get_tree().root.add_child.call_deferred(cur)
	await _frames(20)
	cur.queue_free()
	await _frames(2)
	var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	await _frames(10)
	var hud: Node = main.hud
	hud._modal_queue.clear()
	hud._offer_toast_cd = 1e12
	await get_tree().create_timer(1.0).timeout
	if is_instance_valid(hud.title_card):
		hud.title_card.dismiss()
	await get_tree().create_timer(1.0).timeout
	for i in range(1, 4):
		if is_instance_valid(hud.onboarding):
			if hud.onboarding._typing:
				hud.onboarding._typing.kill()
			hud.onboarding._text.visible_ratio = 1.0
		await _frames(2)
		if is_instance_valid(hud.onboarding):
			hud.onboarding._next()
	# the naming step (a later beat) then finish
	var guard := 0
	while is_instance_valid(hud.onboarding) and not hud.onboarding._name_box.visible and guard < 12:
		hud.onboarding._next()
		guard += 1
		await _frames(3)
	if is_instance_valid(hud.onboarding):
		if hud.onboarding._typing:
			hud.onboarding._typing.kill()
		hud.onboarding._text.visible_ratio = 1.0
		hud.onboarding._confirm_name()
		while is_instance_valid(hud.onboarding):
			hud.onboarding._next()
			await _frames(3)
	await get_tree().create_timer(1.0).timeout
	hud._modal_queue.clear()
	for m in hud.modal_layer.get_children():
		m.queue_free()
	hud._modal_open = false
	EconomyManager.earn(4e4, "shot", false)
	ContractManager.refresh_offers()
	await _frames(30)
	TutorialManager.skip()
	hud._update_tabs(true)
	for i in 2:
		var offer: Dictionary = ContractManager.offers[0] if ContractManager.offers.size() > 0 else {}
		if not offer.is_empty():
			var v: Dictionary = ContractManager.best_vehicle(offer)
			if not v.is_empty():
				ContractManager.dispatch(offer, v, "normal")
	await _frames(60)
	_dump(hud)
	ProgressionManager.goal_completed.emit(str(ProgressionManager.current_goal().get("text", "Reach $1,000 cash")) if ProgressionManager.has_method("current_goal") else "Reach $1,000 cash", 1500.0)
	await _shot("goal_toast", lang, 0.7)
	main.queue_free()
	await _frames(5)
