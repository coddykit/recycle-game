extends Sheet
## HQ garage progression + business-level tycoon upgrades.


func _init() -> void:
	super._init()
	title = "Upgrades"
	icon_name = "circle-arrow-up"
	height_frac = 0.64


func on_opened() -> void:
	EconomyManager.cash_changed.connect(func(_a, _b): refresh())
	EconomyManager.reputation_changed.connect(func(_a, _b): refresh())
	ProgressionManager.garage_upgraded.connect(func(_l): refresh())
	ProgressionManager.business_upgraded.connect(func(_a, _b): refresh())


func build(b: VBoxContainer) -> void:
	var caps := "%s  ·  %s  ·  %s" % [UIKit.plural(ProgressionManager.vehicle_slots(), "vehicle"), UIKit.plural(ProgressionManager.offer_slots(), "offer"),
			UIKit.plural(ProgressionManager.driver_slots(), "driver")]
	set_sub(["%s  ·  %s" % [tr(str(ProgressionManager.garage_def().name)), caps], caps])
	b.add_child(section("Headquarters"))
	b.add_child(_garage_card())
	b.add_child(section("Business"))
	for d in Data.business_upgrades:
		b.add_child(_business_card(d))


## The HQ: [garage] current level name + what it holds [price pill for the next level]; under it the next level and
## its perks (or why it cannot be bought yet). Medium / High: the HQ level as the card's bar.
func _garage_card() -> Control:
	var lvl := ProgressionManager.garage_level
	var cur := ProgressionManager.garage_def()
	var maxed := lvl >= ProgressionManager.max_garage()
	var nxt := {} if maxed else ProgressionManager.garage_def(lvl + 1)
	var sub: Array = [tr("Your underground empire is complete.")] if maxed else [tr("Next: %s") % tr(str(nxt.name))]
	var card := row("garage", str(cur.name), sub, {"selected": not maxed and ProgressionManager.garage_check().ok})
	card.name = "garage"
	progress(card, float(lvl), float(ProgressionManager.max_garage()))
	if maxed:
		return card
	var check := ProgressionManager.garage_check()
	var btn := pill(card, Fmt.money(float(nxt.cost)), func():
		if ProgressionManager.upgrade_garage():
			close(), bool(check.ok))
	btn.name = "upgrade"
	UIManager.register_target("garage_upgrade_btn", btn)
	var col := Kit.list_card_body(card)
	var perks: Array = []
	for p in nxt.perks:
		perks.append(tr(str(p)))
	col.add_child(UIKit.wlabel("✓ " + "   ✓ ".join(perks), "Caption"))
	if not check.ok:
		var why := UIKit.wlabel(tr(str(check.reason)), "Caption")
		why.add_theme_color_override("font_color", UIKit.ORANGE)
		col.add_child(why)
	return card


func _business_card(d: Dictionary) -> Control:
	var lvl := ProgressionManager.business_level(d.id)
	var desc := tr(str(d.desc))
	if d.id == "automated_dispatch":
		if lvl < int(d.max):
			desc = tr("Next: %s - %s") % [tr(d.levels[lvl]), tr(d.level_desc[lvl])]
		else:
			desc = tr("Fully automated!")
	var check := ProgressionManager.business_check(d.id)
	var locked: bool = check.get("locked", false)
	var card := row(str(d.icon), str(d.name), [str(check.reason)] if locked else [desc], {"locked": locked})
	card.name = "biz_" + str(d.id)
	if locked:
		var line: Label = card.get_node_or_null("Col/Head/Texts/Sub")
		if line != null:
			line.add_theme_color_override("font_color", UIKit.ORANGE)
		return card
	progress(card, float(lvl), float(d.max))
	var btn: Button
	if check.get("maxed", false):
		btn = pill(card, "MAX", Callable(), false, "tonal")
	else:
		btn = pill(card, Fmt.money(ProgressionManager.business_cost(d.id)), func():
			if ProgressionManager.buy_business(d.id):
				AudioManager.play("upgrade")
				UIKit.bounce(card, 1.05), bool(check.ok))
	btn.name = "buy"
	return card
