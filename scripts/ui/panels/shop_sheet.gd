extends Sheet
const UiRow = preload("res://addons/coddy_ui/ui_row.gd")
const UiSettings = preload("res://addons/coddy_ui/ui_settings.gd")
const UiShop = preload("res://addons/coddy_ui/ui_shop.gd")
## The shop: the optional sponsor bonus, then every product of data/iap.json (coddy_iap catalog) grouped, restore, privacy.
## The whole game is playable for free; every price shown is the platform store's own. A locked feature opens this sheet
## on its product: arg = {"focus": slug}. Product cards are named prod_<slug> (QA scans, review screenshots).
var _cooldown_label: Label
var _reward_button: Button
var _focus := ""
var _focus_card: Control


func _init() -> void:
	super._init()
	title = "Shop"
	icon_name = "shopping-bag"
	height_frac = 0.84


func on_opened() -> void:
	Monetization.changed.connect(refresh)
	Iap.prices_changed.connect(refresh)
	Iap.entitlements_changed.connect(refresh)
	if arg is Dictionary:
		_focus = str(arg.get("focus", ""))
	elif arg is String:
		_focus = str(arg)


func build(b: VBoxContainer) -> void:
	# the old intro paragraph is the header's summary line (its sentences = shorter alternatives)
	set_sub(UiShop.sub_alts(IapText.t("shop_intro"), true))
	b.add_child(_sponsor_card())
	_focus_card = null
	for g in IapCatalog.groups():
		var gid := String(g.get("id", ""))
		var rows: Array = []
		for slug in IapCatalog.slugs():
			if IapCatalog.group(slug) == gid and Iap.available(slug):
				rows.append(slug)
		if rows.is_empty():
			continue
		var head := Kit.section(IapText.group_title(gid))
		head.name = "group_" + gid
		b.add_child(head)
		for slug in rows:
			var card := _product_card(slug)
			b.add_child(card)
			if slug == _focus:
				_focus_card = card
	var restore := UIKit.button(IapText.t("restore"), "", "refresh")
	restore.theme_type_variation = "OutlineButton"
	restore.name = "iap_restore"
	restore.disabled = Monetization.busy or Iap.busy
	restore.pressed.connect(func(): Iap.restore())
	UIManager.register_target("shop_restore", restore)
	b.add_child(restore)
	var terms := UiRow.nav("file-text", tr("Purchase terms"), "", func(): UIManager.modal("Purchase terms", tr("Upgrades, looks and bundles are one-time purchases, not subscriptions, and come back with Restore Purchases on the same store account. Cash packs are used up when they arrive and are not restored. Remove Ads, Kingpin Edition, Ultimate Bundle and Empire Pass remove ads for good. The store shows your local price before you confirm; purchases and refunds are handled by your platform store. Sponsor bonuses have a ten-minute cooldown."), "contract"))
	UiSettings.privacy(b, Ads, {"title": tr("Privacy"), "options": tr("Privacy options"), "policy": tr("Privacy policy")}, [terms])
	UiShop.style(b, {"icon_for": _icon_for, "featured": ["bundle", "premium"], "cards": true,
			"low": str(GameManager.settings.get("quality", "")) == DeviceTier.LOW, "tag_for": tag_for, "tag_short_for": tag_short_for})
	live_update(0)
	if _focus_card != null:
		var wr: WeakRef = weakref(_focus_card)
		_focus = ""
		(func(): _scroll_to(wr.get_ref())).call_deferred()


const ICONS := {"star": "crown", "trophy": "trophy", "shield": "ban", "robot": "zap", "clock": "clock", "truck": "truck", "garage": "house",
		"driver": "users", "bolt": "gift", "cash": "coins"}


func _icon_for(slug: String) -> String:
	return str(ICONS.get(str(IapCatalog.def(slug).get("icon", "")), "package"))


## The optional rewarded ad as a compact row: tile | title + rules + status | Claim / Watch button.
func _sponsor_card() -> Control:
	var card := Kit.card("Card")
	card.name = "sponsor_card"
	var h := UIKit.hbox(14)
	card.add_child(h)
	h.add_child(Kit.tile("coins", "primary", 48))
	var col := UIKit.vbox(0)
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	col.alignment = BoxContainer.ALIGNMENT_CENTER
	h.add_child(col)
	var t := UIKit.wlabel(tr("Sponsor bonus"), "LabelStrong")
	col.add_child(t)
	card.tooltip_text = tr("Watch an optional rewarded ad to collect supplies. Closing an ad early gives no reward.")
	_cooldown_label = UIKit.wlabel("", "Caption")
	_cooldown_label.add_theme_color_override("font_color", Kit.c("primary"))
	col.add_child(_cooldown_label)
	var free := Monetization.video_free()
	_reward_button = UIKit.button(tr("Claim %s") % Fmt.money(Monetization.reward_value()) if free else tr("Watch ad · %s") % Fmt.money(Monetization.reward_value()), "BtnPrimary", "", Vector2(0, 84), 24)
	_reward_button.name = "sponsor_claim"
	_reward_button.autowrap_mode = TextServer.AUTOWRAP_OFF
	_reward_button.custom_minimum_size.x = 190
	_reward_button.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	_reward_button.pressed.connect(Monetization.claim_sponsor)
	h.add_child(_reward_button)
	return card


func _scroll_to(c: Variant) -> void:
	if not is_instance_valid(c) or not is_instance_valid(scroll):
		return
	await get_tree().process_frame
	if is_instance_valid(c) and is_instance_valid(scroll):
		scroll.ensure_control_visible(c)


## One product (the shape UiShop.style expects): tile | title + what it does (+ "separately" / "worth right now") | price button.
## Named prod_<slug>; the price button is "buy".
func _product_card(slug: String) -> Control:
	var owned := not IapCatalog.is_consumable(slug) and Iap.sells and Iap.covered(slug)
	var card := Kit.card("CardRaised" if owned else "Card")
	card.name = "prod_" + slug
	var h := UIKit.hbox(14)
	card.add_child(h)
	var col := UIKit.vbox(0)
	h.add_child(col)
	col.add_child(UIKit.wlabel(IapText.title(slug), "LabelStrong"))
	col.add_child(UIKit.wlabel(IapText.desc(slug), "CaptionMuted"))
	if not IapCatalog.bundle_parts(slug).is_empty():
		col.add_child(UIKit.wlabel(IapText.t("separately", {"p": "$%.2f" % IapCatalog.separate_usd(slug)}), "Caption"))
	var fx := IapCatalog.effect(slug)
	if IapCatalog.is_consumable(slug) or slug == "starter":
		col.add_child(UIKit.wlabel(tr("Right now: about %s") % Fmt.money(Monetization.pack_cash(fx)), "Caption"))
	var buy: Button
	if owned:
		buy = UIKit.button(IapText.t("owned"), "BtnDark", "", Vector2(0, 84), 24)
		buy.disabled = true
	else:
		var price := Iap.price_text(slug)
		buy = UIKit.button(price if price != "" else IapText.t("price_pending"), "BtnGold", "", Vector2(0, 84), 24)
		buy.disabled = price == "" or Monetization.busy or Iap.busy
		buy.pressed.connect(func(): confirm(slug))
	buy.name = "buy"
	h.add_child(buy)
	return card


func live_update(_delta: float) -> void:
	if not is_instance_valid(_reward_button):
		return
	var seconds := Monetization.cooldown_left()
	var ready := Monetization.placement_ready("sponsor")
	_reward_button.disabled = not ready
	if ready:
		Monetization.note_offer("sponsor", Monetization.reward_value())
	var status := tr("Daily limit reached - back tomorrow") if Monetization.cap_left("sponsor") <= 0 \
			else (tr("Next bonus in %s") % Fmt.time(seconds) if seconds > 0 else tr("Bonus ready"))
	_cooldown_label.text = status + "  ·  " + tr("Today: %d/%d") % [Monetization.uses_today("sponsor"), Monetization.daily_cap("sponsor")]


## Store policy (Patron 10-25): what changes the buying decision is on the card itself (consumable / one-time, no
## subscription / ads not removed) - short on the card, whole in the confirm window.
static func tag_for(slug: String, whole: bool = false) -> String:
	if not IapCatalog.has(slug):
		return ""
	var parts := PackedStringArray()
	if IapCatalog.is_consumable(slug):
		parts.append(tr_static("Consumable"))
	else:
		parts.append(tr_static("One-time, no subscription") if whole else tr_static("One-time"))
	if not "no_ads" in IapCatalog.grants(slug):
		parts.append(tr_static("Ads not removed"))
	return " · ".join(parts)


static func tag_short_for(slug: String) -> String:
	return UiShop.short_tag(IapCatalog.is_consumable(slug), not "no_ads" in IapCatalog.grants(slug)) if IapCatalog.has(slug) else ""


static func tr_static(key: String) -> String:
	return TranslationServer.translate(key)


## The buy confirm window: the product's FULL description, its tags and the price; then the store's own sheet.
static func confirm(slug: String) -> void:
	var body: String = IapText.desc(slug) + "\n\n" + tag_for(slug, true)
	var fx := IapCatalog.effect(slug)
	if IapCatalog.is_consumable(slug) or slug == "starter":
		body += "\n" + tr_static("Right now: about %s") % Fmt.money(Monetization.pack_cash(fx))
	var price := Iap.price_text(slug)
	UIManager.modal(IapText.title(slug), body, str(IapCatalog.def(slug).get("icon", "star")),
			[[tr_static("Buy · %s") % price if price != "" else IapText.t("price_pending"), "BtnGold", func(): Iap.purchase(slug)],
			[tr_static("Cancel"), "BtnDark", Callable()]], UIKit.GOLD)
