extends SceneTree
## coddy_ui visual gallery: every kit component, landscape (1600x720, 20:9) and portrait (720x1560, 19.5:9), in each
## language given, rendered to PNG (needs a renderer: run windowed, never headless; on the Mini through shot-lock):
##   godot --path games/<game> --windowed --resolution 640x360 --script res://addons/coddy_ui/gallery/gallery.gd -- <out dir> [palette] [en,de]
## Inside a game it uses the game's own bundled fonts (CJK / Devanagari); in the kit project the engine font.
## Pages: components, settings (modal, two columns), onboarding (pager), coach (spotlight + bubble), loading, toast.

const UiKit = preload("../ui_kit.gd")
const UiToast = preload("../ui_toast.gd")
const UiModal = preload("../ui_modal.gd")
const UiPager = preload("../ui_pager.gd")
const UiCoach = preload("../ui_coach.gd")
const UiSegmented = preload("../ui_segmented.gd")
const UiRow = preload("../ui_row.gd")
const UiShop = preload("../ui_shop.gd")
const UiSettings = preload("../ui_settings.gd")
const UiLoading = preload("../ui_loading.gd")
const UiScroll = preload("../ui_scroll.gd")

const T := {
	"en": {"primary": "Dispatch convoy", "secondary": "Secondary", "tonal": "Tonal", "ghost": "Ghost", "danger": "Rescue", "disabled": "Disabled",
		"low": "Low", "medium": "Medium", "high": "High", "music": "Music", "effects": "Effects", "language": "Language", "lang": "English",
		"replay": "Replay guide", "restore": "Restore purchases", "privacy": "Privacy", "options": "Privacy options", "policy": "Privacy policy",
		"sound": "Sound", "graphics": "Graphics", "help": "Help", "settings": "Settings", "battery": "Higher quality uses more battery.",
		"tabs": ["Traffic", "Invest", "Canals", "Shop"], "chip": "Tugs 3", "best": "Best value", "owned": "Owned",
		"toast": "Maintenance funded. Future incidents can be prevented.", "warn": "No ship can leave yet. Check tide, direction and crew.",
		"empty_t": "Nothing here yet", "empty_b": "Finish 10 passages to unlock new canals.", "empty_a": "Show me",
		"p1t": "Welcome, canal controller", "p1b": "Ships wait at both ends of your canal. Send them through and every ship pays a toll.",
		"next": "Next", "last": "Let's go!", "skip": "Skip", "skip_guide": "Skip guide", "hint": "Tap the highlighted spot",
		"coach": "Tap Dispatch ready convoy. It sends every ship that can go at once.", "step": "Step 12 of 12",
		"prod1": "Complete Harbor", "desc1": "Every edition perk, golden tower, no forced ads.", "prod2": "Remove Ads", "desc2": "No forced ads. Capped rewards without videos.",
		"intro": "The whole game is free. Only Remove Ads and the editions turn off forced ads.", "loading": "Opening the harbor…",
		"notice_t": "Graphics for this device", "notice_b": "Graphics start at Medium. You can raise quality in Settings.", "continue": "Continue"},
	"de": {"primary": "Konvoi abfertigen", "secondary": "Sekundär", "tonal": "Tonal", "ghost": "Dezent", "danger": "Rettung", "disabled": "Deaktiviert",
		"low": "Niedrig", "medium": "Mittel", "high": "Hoch", "music": "Musik", "effects": "Effekte", "language": "Sprache", "lang": "Deutsch",
		"replay": "Anleitung wiederholen", "restore": "Käufe wiederherstellen", "privacy": "Datenschutz", "options": "Datenschutzoptionen", "policy": "Datenschutzerklärung",
		"sound": "Ton", "graphics": "Grafik", "help": "Hilfe", "settings": "Einstellungen", "battery": "Höhere Qualität verbraucht mehr Akku.",
		"tabs": ["Verkehr", "Ausbau", "Kanäle", "Shop"], "chip": "Schlepper 3", "best": "Bestes Angebot", "owned": "Gekauft",
		"toast": "Wartung finanziert. Künftige Zwischenfälle lassen sich verhindern.", "warn": "Noch kann kein Schiff los. Prüfe Gezeiten, Richtung und Crew.",
		"empty_t": "Noch nichts hier", "empty_b": "Schließe 10 Durchfahrten ab, um neue Kanäle freizuschalten.", "empty_a": "Zeig es mir",
		"p1t": "Willkommen, Kanal-Controller", "p1b": "An beiden Enden deines Kanals warten Schiffe. Schick sie durch, jedes Schiff zahlt Maut.",
		"next": "Weiter", "last": "Los geht's!", "skip": "Überspringen", "skip_guide": "Anleitung überspringen", "hint": "Tippe auf die markierte Stelle",
		"coach": "Tippe auf Alle abfertigen. Das schickt jedes Schiff los, das fahren kann.", "step": "Schritt 12 von 12",
		"prod1": "Komplette Hafen-Kollektion", "desc1": "Alle Editionsvorteile, goldener Turm, keine Pflichtwerbung.", "prod2": "Werbung entfernen", "desc2": "Keine Pflichtwerbung. Begrenzte Belohnungen ohne Videos.",
		"intro": "Das ganze Spiel ist kostenlos. Nur Werbung entfernen und die Editionen entfernen Pflichtwerbung.", "loading": "Hafen wird geöffnet…",
		"notice_t": "Grafik für dieses Gerät", "notice_b": "Die Grafik startet auf Mittel. In den Einstellungen kannst du sie erhöhen.", "continue": "Weiter"},
}

var out := ""
var vp: SubViewport
var count := 0


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	var a := OS.get_cmdline_user_args()
	out = a[0] if a.size() > 0 else "user://coddy_ui_gallery"
	var palette := a[1] if a.size() > 1 else "fleet"
	var locales := (a[2] if a.size() > 2 else "en,de").split(",")
	DirAccess.make_dir_recursive_absolute(out)
	vp = SubViewport.new()
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	vp.transparent_bg = false
	root.add_child(vp)
	for loc in locales:
		TranslationServer.set_locale(loc)
		UiKit.use(palette)
		UiKit.clear_caches()
		var s: Dictionary = T.get(loc, T.en)
		for size in [Vector2i(1600, 720), Vector2i(720, 1560)]:
			var tag := "%s_%s" % ["land" if size.x > size.y else "port", loc]
			await _page("components_" + tag, size, func(h: Control) -> void: _components(h, s, size))
			await _page("settings_" + tag, size, func(h: Control) -> void: _settings(h, s))
			await _page("notice_" + tag, size, func(h: Control) -> void: _notice(h, s))
			await _page("onboarding_" + tag, size, func(h: Control) -> void: _pager(h, s))
			await _page("coach_" + tag, size, func(h: Control) -> void: _coach(h, s, size))
			await _page("loading_" + tag, size, func(h: Control) -> void: _loading(h, s))
	print("CODDY_UI_GALLERY: %d pages → %s" % [count, out])
	quit()


func _page(name: String, size: Vector2i, build: Callable) -> void:
	vp.size = size
	for ch in vp.get_children():
		ch.free()
	var host := Control.new()
	host.theme = UiKit.theme()
	vp.add_child(host)
	host.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var bg := ColorRect.new()
	bg.color = UiKit.c("background")
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	host.add_child(bg)
	build.call(host)
	for i in 6:
		await process_frame
	await RenderingServer.frame_post_draw
	var img := vp.get_texture().get_image()
	img.save_png(out.path_join(name + ".png"))
	count += 1
	print("GALLERY ", name, " ", img.get_size())


func _pad(h: Control, m: int = 32) -> MarginContainer:
	var mc := MarginContainer.new()
	mc.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side in ["left", "right", "top", "bottom"]:
		mc.add_theme_constant_override("margin_" + side, m)
	h.add_child(mc)
	return mc


func _components(h: Control, s: Dictionary, size: Vector2i) -> void:
	var land := size.x > size.y
	var parts: Array = UiScroll.list(22)
	var sc: ScrollContainer = parts[0]
	_pad(h, 28).add_child(sc)
	var root_col: VBoxContainer = parts[1]
	var grid: BoxContainer = HBoxContainer.new() if land else VBoxContainer.new()
	grid.add_theme_constant_override("separation", 40)
	root_col.add_child(grid)
	var a := VBoxContainer.new()
	a.add_theme_constant_override("separation", 18)
	var b := VBoxContainer.new()
	b.add_theme_constant_override("separation", 18)
	if land:   # two fixed columns side by side
		a.custom_minimum_size.x = 740
		b.custom_minimum_size.x = 740
	else:
		a.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	grid.add_child(a)
	grid.add_child(b)
	a.add_child(UiKit.label("coddy_ui · " + UiKit.palette_name.get_slice("{", 0), "Headline"))
	var flow := HFlowContainer.new()
	flow.add_theme_constant_override("h_separation", 12)
	flow.add_theme_constant_override("v_separation", 12)
	flow.add_child(UiKit.button(s.primary, "primary", "route"))
	flow.add_child(UiKit.button(s.secondary, "secondary"))
	flow.add_child(UiKit.button(s.tonal, "tonal"))
	flow.add_child(UiKit.button(s.ghost, "ghost"))
	flow.add_child(UiKit.button(s.danger, "danger", "life-buoy"))
	var dis := UiKit.button(s.disabled, "primary")
	dis.disabled = true
	flow.add_child(dis)
	flow.add_child(UiKit.icon_button("settings", Callable(), s.settings))
	flow.add_child(UiKit.icon_button("pause"))
	a.add_child(flow)
	var seg := UiSegmented.new()
	a.add_child(seg)
	seg.setup([s.low, s.medium, s.high], 1, "Q_")
	var track := UiKit.card("Track")
	var tabs := HBoxContainer.new()
	tabs.add_theme_constant_override("separation", 6)
	track.add_child(tabs)
	var icons := ["ship", "trending-up", "waves", "shopping-bag"]
	for i in 4:
		var t := UiKit.tab_button(str(s.tabs[i]), icons[i])
		t.button_pressed = i == 0
		tabs.add_child(t)
	a.add_child(track)
	var bits := HFlowContainer.new()
	bits.add_theme_constant_override("h_separation", 12)
	bits.add_theme_constant_override("v_separation", 12)
	bits.add_child(UiKit.chip("anchor", s.chip))
	bits.add_child(UiKit.badge(s.best))
	bits.add_child(UiKit.badge(s.owned, "success"))
	bits.add_child(UiKit.dots(3, 1))
	a.add_child(bits)
	var pb := UiKit.progress(0.62)
	a.add_child(pb)
	var g := UiKit.group()
	UiKit.group_add(g, UiRow.toggle("music", s.music, true))
	UiKit.group_add(g, UiRow.toggle("volume-2", s.effects, false))
	UiKit.group_add(g, UiRow.nav("globe", s.language, s.lang))
	b.add_child(g)
	b.add_child(UiRow.item("ship", "accent", "Tanker #3 · North", "Tugs 2 · Pilot 1 · 12 s"))
	var sel := UiRow.item("ship", "primary", "Yacht #2 · South", "", Callable(), "none")
	sel.selected = true
	b.add_child(sel)
	var shop := VBoxContainer.new()
	shop.name = "iap_shop"
	shop.add_theme_constant_override("separation", 12)
	var intro := Label.new()
	intro.text = s.intro
	shop.add_child(intro)
	for pair in [["bundle", s.prod1, s.desc1, "$9.99"], ["noads", s.prod2, s.desc2, "$2.99"]]:
		var card := PanelContainer.new()
		card.name = "prod_" + pair[0]
		var hb := HBoxContainer.new()
		card.add_child(hb)
		var v := VBoxContainer.new()
		for t in [pair[1], pair[2]]:
			var l := Label.new()
			l.text = t
			v.add_child(l)
		hb.add_child(v)
		var buy := Button.new()
		buy.name = "buy"
		buy.text = pair[3]
		hb.add_child(buy)
		shop.add_child(card)
	b.add_child(shop)
	UiShop.style(shop, {"best": s.best})
	b.add_child(UiKit.empty_state("package", s.empty_t, s.empty_b, s.empty_a))
	var t1 := UiToast.new()
	h.add_child(t1)
	t1.setup({"top": 600.0 if land else 1420.0})
	t1.show_toast(s.toast, "success")


func _settings(h: Control, s: Dictionary) -> void:
	var m := UiModal.new()
	h.add_child(m)
	m.setup(s.settings, {"icon": "settings", "width": 1240, "close": true})
	var cols := m.columns(2)
	UiSettings.section(cols[0], s.sound, [UiRow.toggle("music", s.music, true), UiRow.toggle("volume-2", s.effects, true)])
	cols[0].add_child(UiKit.section(s.graphics))
	var gfx := UiKit.card("Group")
	var gpad := MarginContainer.new()
	for side in ["left", "right", "top", "bottom"]:
		gpad.add_theme_constant_override("margin_" + side, 18)
	var gcol := VBoxContainer.new()
	gpad.add_child(gcol)
	gfx.add_child(gpad)
	var seg := UiSegmented.new()
	gcol.add_child(seg)
	seg.setup([s.low, s.medium, s.high], 1, "Q_")
	gcol.add_child(UiKit.label(s.battery, "CaptionMuted", true))
	cols[0].add_child(gfx)
	UiSettings.section(cols[1], s.language, [UiRow.nav("globe", s.language, s.lang)])
	UiSettings.section(cols[1], s.help, [UiRow.nav("book-open", s.replay, ""), UiRow.nav("rotate-ccw", s.restore, "")])
	UiSettings.privacy(cols[1], null, {"title": s.privacy, "options": s.options, "policy": s.policy})


func _notice(h: Control, s: Dictionary) -> void:
	var m := UiModal.new()
	h.add_child(m)
	m.setup(s.notice_t, {"icon": "gauge", "icon_kind": "accent", "icon_round": true})
	m.body.add_child(UiKit.label(s.notice_b, "BodyMuted", true))
	m.add_action(s.continue, "primary", Callable(), "Continue")
	var t := UiToast.new()
	h.add_child(t)
	t.setup({"top": 20.0})
	t.show_toast(s.warn, "warning")


func _pager(h: Control, s: Dictionary) -> void:
	var p := UiPager.new()
	h.add_child(p)
	p.setup([{"title": s.p1t, "body": s.p1b, "icon": "ship", "sats": ["coins", "anchor"]}, {"title": "2", "body": "2"}, {"title": "3", "body": "3"}],
		{"next": s.next, "last": s.last, "skip": s.skip})


func _coach(h: Control, s: Dictionary, size: Vector2i) -> void:
	var land := size.x > size.y
	var target := UiKit.button(s.primary, "primary", "route")
	target.position = Vector2(size.x - 600, 380) if land else Vector2(40, size.y - 400)
	target.size = Vector2(560 if land else size.x - 80, 84)
	h.add_child(target)
	var c := UiCoach.new()
	h.add_child(c)
	c.setup({"skip": s.skip_guide, "hint": s.hint})
	c.show_step(s.coach, s.step, Rect2(target.position, target.size))


func _loading(h: Control, s: Dictionary) -> void:
	var l := UiLoading.new()
	l.game_title = "Coddy\nGame"
	if ResourceLoader.exists("res://assets/ui/loading.png"):
		l.art = load("res://assets/ui/loading.png")
	h.add_child(l)
	l.set_progress(2, 3, s.loading)
