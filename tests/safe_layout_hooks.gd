extends RefCounted
## Safe-area layout test hooks (kit runner res://addons/coddy_ui/tests/safe_layout_test.gd, Patron 10-28): a fresh game
## on the plain map — title card, intro and guide gone — on simulated phones with the status bar, cutout and 3-button
## navigation bar. S1 audit 10-28: the stat bar and Settings sat under the status bar (and, on a simulated phone, the tab
## row on the navigation bar: the HUD read the safe area only on a real phone).


func build(tree: SceneTree) -> Node:
	SaveManager.delete_save()
	GameManager.new_game()
	TranslationServer.set_locale("en")
	var main: Node = load("res://scenes/main/main.tscn").instantiate()
	tree.root.add_child(main)
	for _i in 2:
		await tree.process_frame
	var hud = main.hud
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	hud._restore_game_chrome()
	TutorialManager.skip()
	hud._modal_queue.clear()
	return main


## World labels (town names, floating income) follow the 3D map, not the screen.
func skip(scene: Node) -> Array:
	return [scene.hud.float_layer]


func show_toast(scene: Node) -> Control:
	scene.hud.toast("Delivery complete")
	await scene.get_tree().process_frame
	return scene.hud.ui_toast.card()


func tab_bar(scene: Node) -> Control:
	return scene.hud.get_node("NavBar")


func teardown(scene: Node) -> void:
	scene.free()
