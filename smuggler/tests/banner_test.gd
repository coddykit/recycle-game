extends Node
## Bottom house banner (Emir #112/#125): kit CoddyBanner through CoddyBannerHost (scripts/ui/hud.gd _attach_banner).
## Visible on the plain map with the nav bar ABOVE it; hidden (inset 0) under the title card / onboarding / a modal /
## any sheet (shop …) / the guided tutorial; never for an ad-free player.
## Run: godot --headless --path . res://tests/banner_test.tscn
var failures := 0
var checks := 0
var _paid := [false]


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("BANNER ", "PASS " if ok else "FAIL ", label)


func _frames(n: int) -> void:
	for i in n:
		await get_tree().process_frame


func _cfg() -> Dictionary:
	var seq := []
	for id in ["pharmacy-rush", "train-baron", "borderline-empire"]:
		seq.append({"promotedAppId": id, "titleI18n": {"en": id.capitalize()}, "iconUrl": "https://cdn.test/%s.png" % id,
			"playUrl": "https://play.google.com/store/apps/details?id=com.coddykit." + id})
	return {"adsEnabled": true, "houseAds": {"banner": {"enabled": true, "sequence": seq}}}


func _ready() -> void:
	await get_tree().process_frame
	SaveManager.delete_save()
	GameManager.new_game()
	TranslationServer.set_locale("en")
	var main: Node = load("res://scenes/main/main.tscn").instantiate()
	add_child(main)
	await _frames(2)
	var hud = main.hud
	var h = hud.banner_host
	check(h != null and h.get_parent() == hud, "banner host attached to the HUD")
	h.source = null
	h.fetch_enabled = false
	h._config = _cfg()
	h.ad_free = func() -> bool: return _paid[0]
	h.play_s = func() -> float: return 600.0
	var b = h.banner
	b.force_desktop = true
	b.prefetch_enabled = false
	var img := Image.create(8, 8, false, Image.FORMAT_RGBA8)
	for c in h._config.houseAds.banner.sequence:
		b._icon_cache[c.iconUrl] = ImageTexture.create_from_image(img)
	b.update()
	check(b.wanted() == "" and hud.banner_inset == 0.0, "title card / onboarding up → no banner, inset 0")
	if is_instance_valid(hud.title_card):
		hud.title_card.free()
	if is_instance_valid(hud.onboarding):
		hud.onboarding.free()
	hud._restore_game_chrome()
	TutorialManager.skip()
	hud._modal_queue.clear()
	await _frames(2)
	b.update()
	await _frames(3)
	check(b.wanted() == "house" and b.current_creative().promotedAppId != "borderline-empire", "plain map → house banner, never itself")
	check(hud.banner_inset >= b.height and b.height > 0.0, "HUD told the banner height (%.0f)" % hud.banner_inset)
	var top: float = hud.get_viewport_rect().size.y - b.height
	var nav: Control = hud.get_node("NavBar")
	check(nav.get_global_rect().end.y <= top + 0.5, "nav bar bottom (%.0f) above the banner top (%.0f)" % [nav.get_global_rect().end.y, top])
	check(hud.jobs_scroll.get_global_rect().end.y <= top + 0.5, "job strip above the banner")
	for s in ["shop", "upgrades"]:
		hud.open_panel(s)
		await _frames(1)
		b.update()
		check(b.wanted() == "" and hud.banner_inset == 0.0, s + " sheet open → no banner, inset 0")
		hud.close_panel()
		await _frames(1)
		hud.current_sheet = null
		b.update()
		check(b.wanted() == "house", s + " closed → banner back")
	hud.show_modal("Test", "body")
	await _frames(2)
	b.update()
	check(b.wanted() == "" and hud.banner_inset == 0.0, "modal open → no banner, inset 0")
	hud._dismiss_top_modal()
	hud._modal_open = false
	hud._modal_queue.clear()
	b.update()
	check(b.wanted() == "house", "modal closed → banner back")
	_paid[0] = true
	b.update()
	check(b.wanted() == "" and hud.banner_inset == 0.0, "ad-free player → no banner, no inset")
	_paid[0] = false
	main.queue_free()
	await _frames(2)
	print("BANNER checks=%d failures=%d" % [checks, failures])
	get_tree().quit(1 if failures > 0 else 0)
