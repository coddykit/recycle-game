class_name ContractCard
## Builds the contract rows of the Contracts sheet (2026-10-26 redesign): kit list card [category icon] category +
## ONE grey line (route) [Accept pill]; under it the pay in gold with the trip facts, the modifier / requirement tags,
## the reason a job cannot go yet, and the two quiet chores (reject, priority). Medium / High: the offer's time left as
## the card's thin bar. Low: no icon, a flat row, coloured-text actions.

const Kit = preload("res://addons/coddy_ui/ui_kit.gd")


static func build(c: Dictionary, queued: bool, first: bool, on_accept: Callable, on_reject: Callable,
		on_priority: Callable) -> Dictionary:
	var low := GameManager.quality() == "low"
	var tl := TranslationServer
	var cat := Data.category(c.category)
	var route := "%s  ›  %s" % [RegionManager.node_name(c.origin), RegionManager.node_name(c.dest)]
	var tag := ""
	if c.get("tutorial", false):
		tag = tl.translate("TUTORIAL")
	elif c.get("cross", false):
		tag = tl.translate("CROSS-BORDER")
	var card := Kit.list_card("package", tl.translate(str(cat.name)), {"sub": [route, RegionManager.node_name(c.dest)],
			"badge": tag, "badge_kind": "open", "low": low, "flat_low": true, "selected": bool(c.get("prioritized", false))})
	var tile := card.get_node_or_null("Col/Head/Tile")
	if tile != null:
		var ic := UIKit.icon(str(cat.get("icon", "box")), Kit.CARD_TILE)
		ic.name = "Tile"
		ic.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		tile.get_parent().add_child(ic)
		tile.get_parent().move_child(ic, 0)
		tile.free()
	var col := Kit.list_card_body(card)
	# requirement problems
	var problem := ""
	if not ContractManager.rep_ok(c):
		problem = tl.translate("Needs %d reputation") % int(c.rep_required)
	else:
		var any_fit := false
		var reason := ""
		for veh in VehicleManager.vehicles:
			var r := VehicleManager.fit_reason(veh, c)
			if r == "":
				any_fit = true
				break
			reason = r
		if not any_fit:
			problem = tl.translate(reason if reason != "" else "No suitable vehicle")
	# the one call to action
	var accept_btn: Button
	if problem != "":
		var can_queue: bool = ContractManager.rep_ok(c) and not queued
		accept_btn = Kit.action_pill(tl.translate("Queue" if can_queue else "Accept"), on_accept, can_queue, "tonal", "", low)
	else:
		accept_btn = Kit.action_pill(tl.translate("Dispatch" if queued else "Accept"), on_accept, true, "primary", "", low)
	accept_btn.custom_minimum_size.x = 84
	accept_btn.name = "accept"
	Kit.list_card_side(card, accept_btn)
	if first:
		UIManager.register_target("offer_accept_first", accept_btn)
	# pay + trip facts, then the chores on the right
	var best := ContractManager.best_vehicle(c)
	var est := ContractManager.estimate(c, best, "normal")
	var facts := UIKit.hbox(14)
	facts.mouse_filter = Control.MOUSE_FILTER_IGNORE
	col.add_child(facts)
	var pay := Kit.label(Fmt.money(float(c.reward)), "Title")
	pay.add_theme_color_override("font_color", UIKit.GOLD)
	pay.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	facts.add_child(pay)
	var unit_word := "pax" if cat.get("kind", "cargo") == "passenger" else ("unit" if int(c.units) == 1 else "units")
	var risk := float(est.risk)
	var units := "%d %s" % [int(c.units), tl.translate(unit_word)]
	var risk_txt := "%s %d%%" % [Fmt.risk_label(risk), int(risk)]
	var line := Kit.fit_line(["%s  ·  %s  ·  %s" % [units, Fmt.time(float(est.time)), risk_txt], "%s  ·  %s" % [Fmt.time(float(est.time)), risk_txt], risk_txt])
	line.name = "Facts"
	line.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	line.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	if risk >= 25.0:
		line.add_theme_color_override("font_color", Fmt.risk_color(risk))
	facts.add_child(line)
	if not c.get("tutorial", false):
		var rej := Kit.icon_button("x", on_reject, tl.translate("Close"), true)
		rej.name = "reject"
		rej.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		facts.add_child(rej)
		var pri := Kit.icon_button("star", on_priority, "", true)
		pri.name = "priority"
		pri.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		if c.get("prioritized", false):
			pri.add_theme_color_override("icon_normal_color", UIKit.GOLD)
			pri.add_theme_color_override("icon_hover_color", UIKit.GOLD)
			pri.add_theme_color_override("icon_pressed_color", UIKit.GOLD)
		facts.add_child(pri)
	# modifier / requirement tags (wrap instead of widening the card)
	var mod := Data.modifier(str(c.get("modifier", "")))
	if not mod.is_empty() or str(cat.get("requires", "")) != "":
		var mrow := UIKit.flow(8, 6)
		mrow.mouse_filter = Control.MOUSE_FILTER_IGNORE
		col.add_child(mrow)
		if not mod.is_empty():
			mrow.add_child(Kit.card_badge(UIKit.upper(tl.translate(str(mod.name))), "warning"))
		if str(cat.get("requires", "")) != "":
			mrow.add_child(Kit.card_badge(tl.translate("NEEDS %s") % UIKit.upper(tl.translate(str(cat.requires))), "level"))
		if not mod.is_empty() and not low:
			col.add_child(UIKit.wlabel(tl.translate(str(mod.desc)), "CaptionMuted"))
	if problem != "":
		var pl := UIKit.wlabel(problem, "Caption")
		pl.add_theme_color_override("font_color", UIKit.ORANGE)
		col.add_child(pl)
	# expiry: the card's thin bar (Medium / High)
	var bar: ProgressBar = null
	if not queued and not c.get("prioritized", false) and not c.get("tutorial", false) and not low:
		bar = Kit.card_progress(card, 1.0, 1.0)
	return {"card": card, "bar": bar, "contract": c}
