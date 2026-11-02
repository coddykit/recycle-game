extends Sheet
const UiRow = preload("res://addons/coddy_ui/ui_row.gd")
## Empire overview: stage progression, goal, automation and statistics.


func _init() -> void:
	super._init()
	title = "Business"
	icon_name = "briefcase"
	height_frac = 0.64


func on_opened() -> void:
	EconomyManager.influence_changed.connect(func(_a, _b): refresh())
	ProgressionManager.automation_changed.connect(refresh)
	ProgressionManager.stage_changed.connect(func(_s): refresh())
	ProgressionManager.goal_changed.connect(refresh)


func build(b: VBoxContainer) -> void:
	var stage := ProgressionManager.stage
	var st: Dictionary = Data.stages[stage]
	set_sub(["%s  ·  %s  ·  %s" % [tr(str(st.name)), tr("%d influence") % EconomyManager.influence, tr("Income / minute") + " " + Fmt.money(EconomyManager.income_per_minute())],
			"%s  ·  %s" % [tr(str(st.name)), tr("%d influence") % EconomyManager.influence], tr(str(st.name))])
	# the two windows of this tab: kit navigation rows (body children 0 and 1: the tab probe taps the second)
	var shop := UiRow.nav("shopping-bag", tr("Shop & sponsor bonus"), "", func(): UIManager.open_panel("shop"), "primary")
	shop.name = "business_shop"
	UIManager.register_target("business_shop", shop)
	b.add_child(shop)
	var daily := UiRow.nav("file-text", tr("Daily logistics"), "", func(): UIManager.open_panel("daily"))
	daily.name = "business_daily"
	b.add_child(daily)
	# stage
	var card := Kit.card("Card")
	b.add_child(card)
	var col := UIKit.vbox(8)
	card.add_child(col)
	var h := UIKit.hbox(12)
	col.add_child(h)
	h.add_child(UIKit.icon("trophy", 68))
	var nc := UIKit.vbox(2)
	nc.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	h.add_child(nc)
	nc.add_child(UIKit.label(tr("EMPIRE STAGE %d / %d") % [stage + 1, Data.stages.size()], 20, UIKit.DIM, true))
	nc.add_child(UIKit.label(tr(str(st.name)), 32, UIKit.GOLD, true))
	if stage + 1 < Data.stages.size():
		var nxt: Dictionary = Data.stages[stage + 1]
		col.add_child(UIKit.label(tr("Next: %s") % tr(nxt.name), 22, UIKit.TEXT, true))
		col.add_child(UIKit.bar(ProgressionManager.stage_progress(stage + 1), UIKit.GOLD, 14))
		for r in nxt.req:
			var have := ProgressionManager.metric(r[0])
			var done := have >= float(r[1])
			col.add_child(UIKit.stat_chip("check" if done else "target", "%s: %s / %s" % [_metric_name(r[0]),
					Fmt.commas(int(have)), Fmt.commas(int(r[1]))], UIKit.GREEN if done else UIKit.TEXT, 20))
	b.add_child(_prestige_card())
	# goal
	var g := ProgressionManager.current_goal()
	var gc := row("target", str(g.text), ["+" + Fmt.money(float(g.reward))])
	gc.name = "goal"
	var gsub: Label = gc.get_node_or_null("Col/Head/Texts/Sub")
	if gsub != null:
		gsub.add_theme_color_override("font_color", UIKit.GOLD)
	progress(gc, ProgressionManager.goal_progress())
	b.add_child(gc)
	# connections (Influence perks)
	b.add_child(section("Connections", tr("%d influence") % EconomyManager.influence))
	var hint := UIKit.wlabel(tr("Earn Influence from cross-border jobs, goals, new regions and empire stages."), "CaptionMuted")
	b.add_child(hint)
	_conn_anchor = hint
	for d in Data.connections:
		b.add_child(_connection_card(d))
	if typeof(arg) == TYPE_DICTIONARY and arg.get("section", "") == "connections":
		arg = null
		_scroll_to_conn()
	# automation
	b.add_child(section("Automation"))
	var names: Array = Data.business("automated_dispatch").get("levels", [])
	var descs: Array = Data.business("automated_dispatch").get("level_desc", [])
	for i in ProgressionManager.AUTO_KEYS.size():
		var key: String = ProgressionManager.AUTO_KEYS[i]
		b.add_child(_auto_row(key, str(names[i]) if i < names.size() else key, str(descs[i]) if i < descs.size() else ""))
	b.add_child(_crisis_row())
	# stats
	b.add_child(section("Statistics"))
	var sc := Kit.card("Card")
	b.add_child(sc)
	var sv := UIKit.vbox(6)
	sc.add_child(sv)
	_stat(sv, "Income / minute", Fmt.money(EconomyManager.income_per_minute()))
	_stat(sv, "Total earned", Fmt.money(EconomyManager.stat("earned")))
	_stat(sv, "Deliveries", Fmt.commas(int(EconomyManager.stat("deliveries"))))
	_stat(sv, "Failed jobs", Fmt.commas(int(EconomyManager.stat("failed"))))
	_stat(sv, "Best payout", Fmt.money(EconomyManager.stat("best_payout")))
	_stat(sv, "Events handled", Fmt.commas(int(EconomyManager.stat("events"))))
	_stat(sv, "Fleet", tr("%d vehicles · %d drivers") % [VehicleManager.vehicles.size(), VehicleManager.drivers.size()])
	_stat(sv, "Network", tr("%d regions · %d depots") % [RegionManager.unlocked_count, RegionManager.depots.size()])
	_stat(sv, "Time as boss", Fmt.time(EconomyManager.stat("play_time")))


var _conn_anchor: Control


func _scroll_to_conn() -> void:
	await get_tree().process_frame
	if is_instance_valid(_conn_anchor) and is_instance_valid(scroll):
		scroll.scroll_vertical = int(_conn_anchor.position.y - 80)


func _connection_card(d: Dictionary) -> Control:
	var lvl := ProgressionManager.conn(d.id)
	var card := row(str(d.icon), str(d.name), [tr(str(d.desc))])
	card.name = "conn_" + str(d.id)
	progress(card, float(lvl), float(d.max))
	var check := ProgressionManager.connection_check(d.id)
	var btn: Button
	if check.get("maxed", false):
		btn = pill(card, "MAX", Callable(), false, "tonal")
	else:
		btn = Kit.action_pill(str(ProgressionManager.connection_cost(d.id)), func():
			if ProgressionManager.buy_connection(d.id):
				AudioManager.play("upgrade")
				UIManager.toast(conn_toast(d, ProgressionManager.conn(d.id)), Color("d9c2ff"), "influence")
				refresh(), bool(check.ok), "primary", "gem", low())
		btn.custom_minimum_size.x = 84
		Kit.list_card_side(card, btn)
	btn.name = "buy"
	return card


func _prestige_card() -> Control:
	var card := UIKit.card("Card", Color("2b1f4a"))
	var col := UIKit.vbox(8)
	card.add_child(col)
	var h := UIKit.hbox(12)
	col.add_child(h)
	h.add_child(UIKit.icon("star", 56))
	var nc := UIKit.vbox(2)
	nc.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	h.add_child(nc)
	nc.add_child(UIKit.label("SELL THE EMPIRE", 22, UIKit.GOLD, true))
	var leg := ProgressionManager.legacy()
	nc.add_child(UIKit.label(tr("Legacy %d  ·  income +%d%%") % [leg, leg * 10], 20, Color("d9c2ff"), true))
	if not ProgressionManager.prestige_available():
		var need: Dictionary = Data.stages[ProgressionManager.PRESTIGE_STAGE]
		var l := UIKit.label(tr("Reach stage %d (%s) to sell your empire for permanent Legacy stars.") % [
				ProgressionManager.PRESTIGE_STAGE + 1, tr(need.name)], 19, UIKit.DIM)
		UIKit.wrap(l)
		col.add_child(l)
		return card
	var stars := ProgressionManager.prestige_stars()
	var info := UIKit.label(tr("Cash out now for %d Legacy star%s: +%d%% income forever, a bigger head start, +%d influence. Connections and stats are kept; the rest starts over.") % [
			stars, UIKit.pl(stars), stars * 10, stars * 5], 19, UIKit.TEXT)
	UIKit.wrap(info)
	col.add_child(info)
	var btn := UIKit.button(tr("SELL FOR %d STAR%s") % [stars, UIKit.pl(stars).to_upper()], "BtnGold", "star", Vector2(0, 80), 26)
	btn.pressed.connect(func():
		UIManager.modal("Sell the Empire?", "You keep Legacy stars, Connections and Influence.\nVehicles, regions and the HQ start over.",
				"star", [["Not yet", "BtnDark", Callable()], ["Sell!", "BtnGold", func(): GameManager.prestige()]], UIKit.GOLD))
	col.add_child(btn)
	return card


func _metric_name(t: String) -> String:
	return tr({"vehicles": "Vehicles", "garage": "Garage level", "deliveries": "Deliveries", "regions": "Regions",
			"automation": "Automation", "depots": "Depots"}.get(t, t.capitalize()))


func _auto_row(key: String, name: String, desc: String) -> Control:
	var card := row("robot", name, [tr(desc)])
	card.name = "auto_" + key
	if not ProgressionManager.auto_unlocked(key):
		# just the lock (as on the Crisis Manager row): the word "Locked" broke letter by letter in Russian in this width
		_lock_side(card, func(): hud.open_panel("upgrades"))
	else:
		var on := ProgressionManager.auto_enabled(key)
		pill(card, "ON" if on else "OFF", func(): ProgressionManager.set_auto(key, not on), true, "primary" if on else "tonal")
	return card


## The fifth, paid automation line (Crisis Manager): handles road events and claims the daily jobs. Locked = tap opens the shop.
func _crisis_row() -> Control:
	var owned := ProgressionManager.crisis_manager_owned()
	var card := row("robot", "Crisis Manager", [tr("Handles road events and claims your daily jobs for you.")])
	card.name = "auto_crisis"
	if not owned:
		_lock_side(card, func(): Iap.locked("auto_plus"))
	else:
		var on := ProgressionManager.crisis_manager_on()
		pill(card, "ON" if on else "OFF", func(): ProgressionManager.set_auto("crisis", not on), true, "primary" if on else "tonal")
	return card


## A locked automation: the lock glyph on the right (the word is the tooltip), the tap leads to where it unlocks.
func _lock_side(card: Control, cb: Callable) -> void:
	var btn := Kit.icon_button("lock", cb, tr("Locked"), true)
	btn.name = "Locked"
	Kit.list_card_side(card, btn)


func _stat(parent: Control, a: String, b: String) -> void:
	var h := UIKit.hbox(8)
	parent.add_child(h)
	h.add_child(UIKit.label(a, 22, UIKit.DIM))
	h.add_child(UIKit.spacer())
	h.add_child(UIKit.label(b, 22, UIKit.TEXT, true))


## "<connection> Lv n" after buying a connection level, the name in the player's language (S1 #170: the data name went
## in raw, so a Turkish player read the English name before Sv 2).
static func conn_toast(d: Dictionary, level: int) -> String:
	return TranslationServer.translate("%s Lv %d") % [TranslationServer.translate(str(d.get("name", ""))), level]
