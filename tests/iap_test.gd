extends Node
## W5 (2026-10-19): the shop (coddy_iap on RevenueCat), the paid features, capped cash packs, one-off offers, the ad-free
## entitlement (games/CLAUDE.md §3/§8: Remove Ads / Kingpin Edition / Ultimate Bundle / Empire Pass + migrated old buyers;
## cheap packs keep ads), house-first interstitial wiring, rewarded rules (§4), store rating + feedback rules. Headless; the store is a fake RevenueCat singleton.
## Run: godot --headless --path . res://tests/iap_test.tscn      (last line: IAP checks=N failures=0)
const Gate := preload("res://scripts/monetization/review_gate.gd")
const Feedback := preload("res://scripts/monetization/feedback_client.gd")
const Validator := preload("res://scripts/core/save_validator.gd")
const PREFIX := "com.coddykit.borderlineempire."
const FEATURES := ["empire_pass", "paint_gold", "hq_theme", "auto_plus", "offline_plus", "paint_street", "paint_military", "paint_luxury", "driver_portraits", "no_ads"]
const AD_FREE_PRODUCTS := ["noads", "premium", "bundle", "empire_pass"] ## games/CLAUDE.md §8, smuggler
const FX := ["starter", "legacy_pack", "cash_s", "cash_m", "cash_l"]

var failures := 0
var checks := 0


## RevenueCat's GodotxRevenueCat singleton, replaced by a script: answers on the next frame like the plugin does.
class FakeRc extends Object:
	var iap: Node
	var entitlements := {}
	var calls: Array = []
	var next_transaction := 1
	var purchase_error := ""
	var purchase_cancelled := false

	func purchase(product_id: String) -> void:
		calls.append(["purchase", product_id])
		iap.get_tree().process_frame.connect(func():
			var d := {"product_id": product_id, "transaction_id": "tx-%d" % next_transaction}
			next_transaction += 1
			if purchase_cancelled:
				d = {"cancelled": true}
			elif purchase_error != "":
				d = {"error": purchase_error, "product_id": product_id}
			iap._on_rc_purchase_result(d), CONNECT_ONE_SHOT)

	func restore_purchases() -> void:
		calls.append(["restore"])
		iap.get_tree().process_frame.connect(func(): iap._on_rc_restore_finished({"success": true}), CONNECT_ONE_SHOT)

	func check_entitlement(id: String) -> void:
		calls.append(["check", id])
		iap.get_tree().process_frame.connect(func(): iap._on_rc_entitlement(id, bool(entitlements.get(id, false))), CONNECT_ONE_SHOT)

	func fetch_products(_ids: Array) -> void:
		calls.append(["fetch"])

	func get_customer_info() -> void:
		calls.append(["customer_info"])


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("IAP ", "PASS " if ok else "FAIL ", label)


func frames(n := 3) -> void:
	for i in n:
		await get_tree().process_frame


## A phone build on the fake store that already owns `owned` (permanent slugs); consumables start empty.
func phone(owned := []) -> void:
	var st := IapGate.empty_state()
	for slug in owned:
		IapGate.record(st, str(slug), "")
	Iap._test_setup(st, true, true)
	Iap._rc = null
	Iap._prices = {}
	Iap._fetched = false


func _ready() -> void:
	await get_tree().process_frame
	SaveManager.delete_save()
	UIKit.theme() # initialize bundled fonts for standalone UI fixtures
	IapCatalog.load_from()
	GameManager.new_game()
	_catalog()
	_texts()
	_gate()
	await _revenuecat_flow()
	await _pending_consumable_completed_later()
	_features()
	_packs()
	_offers()
	_ads_and_rewarded()
	_ad_free_policy()
	_rewarded_placements()
	_rewarded_policy()
	_review_and_feedback()
	_portraits_and_paints()
	_sold_looks_are_drawn()
	_save_and_prestige()
	await _shop_ui()
	_no_trial_no_subscription()
	phone()
	TranslationServer.set_locale("en")
	SaveManager.delete_save()
	print("IAP checks=", checks, " failures=", failures)
	await get_tree().create_timer(0.2).timeout
	get_tree().quit(1 if failures else 0)


# ---------------------------------------------------------------- W23: a pending consumable completed later

func _tx(slug: String, id: String, at_ms: int) -> Dictionary:
	return {"product_id": PREFIX + slug, "transaction_id": id, "purchase_date": at_ms}


func _info(txs: Array) -> Dictionary:
	return {"active_entitlements": 0, "non_subscription_transactions": txs}


## Restart: the cache file round trip (encode → decode), like the next launch reads user://iap_entitlement.json.
func _relaunch() -> void:
	Iap._st = IapGate.decode(IapGate.encode(Iap._st, "test", 0))


func _rc_phone() -> FakeRc:
	phone()
	var rc := FakeRc.new()
	rc.iap = Iap
	Iap._rc = rc
	Iap._fetched = true
	Iap.fake = false
	for slug in IapCatalog.slugs():
		Iap._prices[slug] = "$%.2f" % float(IapCatalog.def(slug).usd)
	return rc


func _pending_consumable_completed_later() -> void:
	var now := Iap._now_ms()
	var hour := 3600 * 1000
	# 1. Ask to buy: the purchase goes pending, nothing is delivered, a claim is kept (and survives a restart)
	var rc := _rc_phone()
	Iap._on_rc_customer_info(_info([_tx("cash_m", "GPA.old-1", now - 30 * 24 * hour), _tx("cash_l", "GPA.old-2", now - 2 * 24 * hour)]))
	check(Iap.pending_effects().is_empty() and bool(Iap._st.rc_base), "pending: the first transaction list is a baseline, old packs are not delivered")
	rc.purchase_error = "PaymentPendingError: The payment is pending."
	var ok: bool = await Iap.purchase("cash_m")
	check(not ok and Iap.pending_effects().is_empty() and Iap._st.await.size() == 1, "pending: a pending pack is not delivered yet, its claim is kept")
	_relaunch()
	check(Iap._st.await.size() == 1 and Iap._st.await[0].slug == "cash_m", "pending: the claim survives a restart")
	# 2. next launch: the transaction list has the completed purchase → delivered once
	var done := _tx("cash_m", "GPA.done-1", now + 5 * 60 * 1000)
	var info := _info([_tx("cash_m", "GPA.old-1", now - 30 * 24 * hour), done])
	Iap._on_rc_customer_info(info)
	check(Iap.pending_effects().size() == 1 and Iap.pending_effects()[0].slug == "cash_m" and Iap._st.await.is_empty(), "pending: completed later → the pack is delivered once")
	# 3. the same customer_info again (launch + resume + listener) → still once
	Iap._on_rc_customer_info(info)
	Iap._on_rc_customer_info(info)
	_relaunch()
	Iap._on_rc_customer_info(info)
	check(Iap.pending_effects().size() == 1, "pending: the same transaction list several times (and after a restart) delivers once")
	# 4. a normal purchase delivered by purchase_result, then its transaction shows up in customer_info → no second pack
	rc.purchase_error = ""
	ok = await Iap.purchase("cash_s")
	var after_buy := Iap.pending_effects().size()
	Iap._on_rc_customer_info(_info([done, _tx("cash_s", "GPA.normal-1", now + 6 * 60 * 1000)]))
	check(ok and after_buy == 2 and Iap.pending_effects().size() == 2, "pending: a normal purchase is not delivered again from the transaction list")
	# 5. pending, then the late purchase_result success arrives (it delivers) → the list must not deliver it again
	rc.purchase_error = "PaymentPendingError: pending"
	await Iap.purchase("cash_l")
	check(Iap._st.await.size() == 1, "pending: second claim kept")
	Iap._on_rc_purchase_result({"product_id": PREFIX + "cash_l", "transaction_id": "GPA.late-ok"})
	var after_late := Iap.pending_effects().size()
	Iap._on_rc_customer_info(_info([_tx("cash_l", "GPA.late-tx", now + 7 * 60 * 1000)]))
	check(after_late == 3 and Iap.pending_effects().size() == 3 and Iap._st.await.is_empty(), "pending: a late success settles the claim, the list does not deliver it twice")
	# 6. a claim for one product is not paid by another product's transaction, nor by a transaction older than the claim
	Iap._await_pending("cash_m")
	var claim_at := int(Iap._st.await[0].at)
	Iap._on_rc_customer_info(_info([_tx("cash_s", "GPA.other", claim_at + hour), _tx("cash_m", "GPA.before", claim_at - hour)]))
	check(Iap.pending_effects().size() == 3 and Iap._st.await.size() == 1, "pending: only a later transaction of the same product settles a claim")
	# 7. an expired claim (Play cancelled it after ~3 days; kept 14) is dropped, never paid
	Iap._st.await[0].at = now - 15 * 24 * hour
	Iap._on_rc_customer_info(_info([_tx("cash_m", "GPA.very-late", now)]))
	check(Iap.pending_effects().size() == 3 and Iap._st.await.is_empty(), "pending: a claim older than two weeks is dropped")
	# 8. fresh install / restore on a new phone: the whole history arrives at once → nothing is delivered
	_rc_phone()
	var hist := []
	for i in 8:
		hist.append(_tx(["cash_s", "cash_m", "cash_l", "legacy_pack"][i % 4], "GPA.h-%d" % i, now - (i + 1) * 24 * hour))
	Iap._on_rc_customer_info(_info(hist))
	Iap._on_rc_customer_info(_info(hist))
	check(Iap.pending_effects().is_empty(), "pending: a new install never gets old packs again from the history")
	# 9. damaged cache with an open claim: nothing is paid twice (the claim is lost, never doubled)
	_rc_phone()
	Iap._await_pending("cash_m")
	Iap._st = IapGate.decode("{ broken")
	Iap._on_rc_customer_info(_info([_tx("cash_m", "GPA.after-broken", now + hour)]))
	check(Iap.pending_effects().is_empty(), "pending: a damaged cache never delivers (no double delivery)")
	# 10. an older plugin build (no transaction list) changes nothing and does not crash
	_rc_phone()
	Iap._await_pending("cash_m")
	Iap._on_rc_customer_info({"active_entitlements": 1})
	Iap._on_rc_customer_info({"error": "network"})
	check(Iap.pending_effects().is_empty() and Iap._st.await.size() == 1, "pending: an older plugin's customer_info keeps the claim and grants nothing")
	# 11. resume asks RevenueCat for fresh customer info (that is how a completion while away arrives)
	var rc2 := _rc_phone()
	Iap._last_refresh = -1000.0
	Iap._notification(Node.NOTIFICATION_APPLICATION_RESUMED)
	check(rc2.calls.any(func(c): return c[0] == "customer_info"), "pending: returning to the app asks for the customer info")
	phone()


# ---------------------------------------------------------------- catalog

func _catalog() -> void:
	check(IapCatalog.slugs().size() == 17 and IapCatalog.data().products.size() == 17, "catalog: 17 products, every one in the display order")
	check(IapCatalog.product_id("empire_pass") == PREFIX + "empire_pass", "catalog: the live Empire Pass id is unchanged")
	var ids := {}
	var groups := {}
	for g in IapCatalog.groups():
		groups[str(g.id)] = true
	for slug in IapCatalog.slugs():
		var d := IapCatalog.def(slug)
		var pid := IapCatalog.product_id(slug)
		check(pid == PREFIX + slug and not ids.has(pid), "catalog: %s has its own id %s" % [slug, pid])
		ids[pid] = true
		check(IapCatalog.slug_of(pid) == slug, "catalog: %s id round-trips" % slug)
		check(str(d.kind) in ["permanent", "consumable"], "catalog: %s kind %s (no subscriptions)" % [slug, d.kind])
		check(groups.has(IapCatalog.group(slug)) and float(d.usd) > 0.0, "catalog: %s has a group and a reference price" % slug)
		for f in IapCatalog.grants(slug):
			check(f in FEATURES, "catalog: %s grants a feature the game knows (%s)" % [slug, f])
		if IapCatalog.is_consumable(slug):
			check(IapCatalog.effect(slug) in FX and IapCatalog.grants(slug).is_empty(), "catalog: consumable %s delivers a known pack" % slug)
		var mp := IapCatalog.bundle_parts(slug)
		for p in mp:
			check(IapCatalog.has(str(p)), "catalog: %s part %s exists" % [slug, p])
		if not mp.is_empty():
			check(float(d.usd) < IapCatalog.separate_usd(slug) * 0.85, "catalog: %s is at least 15%% cheaper than its parts (%.2f vs %.2f)" % [slug, d.usd, IapCatalog.separate_usd(slug)])
	check(IapCatalog.slug_of("com.other.app.premium") == "", "catalog: a foreign id grants nothing")
	# the cash ladder rises (the store script keeps it rising in every PPP tier: iap_catalog_gp.ts `ladder` = the consumable group)
	check(float(IapCatalog.def("cash_s").usd) < float(IapCatalog.def("cash_m").usd) and float(IapCatalog.def("cash_m").usd) < float(IapCatalog.def("cash_l").usd), "catalog: cash_s < cash_m < cash_l in price")
	check(IapCatalog.slug_for_feature("paint_street") == "paint_street" and IapCatalog.slug_for_feature("auto_plus") == "automation_plus" \
			and IapCatalog.slug_for_feature("hq_theme") == "hq_theme" and IapCatalog.slug_for_feature("driver_portraits") == "driver_portraits" \
			and IapCatalog.slug_for_feature("paint_gold") == "cosmetics_all" and IapCatalog.slug_for_feature("offline_plus") == "offline_plus", "catalog: a locked feature opens the cheapest product that grants it")
	var play: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://data/iap.json"))
	check(str(play.revenuecat.android_key).begins_with("goog_") and str(play.revenuecat.ios_key).begins_with("appl_"), "catalog: RevenueCat public SDK keys are present (goog_ / appl_)")


func _texts() -> void:
	var latin := RegEx.create_from_string("[A-Za-z]{4,}")
	for l in IapText.LANGS:
		var missing := IapText.missing(l)
		check(missing.is_empty(), "texts: every shop text exists in %s %s" % [l, str(missing.slice(0, 3))])
		for slug in IapCatalog.slugs():
			var t := str(IapCatalog.def(slug).title.get(l, ""))
			var d := str(IapCatalog.def(slug).desc.get(l, ""))
			if t.length() > 55 or d.length() > 200:
				check(false, "texts: %s %s exceeds Play's title 55 / description 200 limits" % [slug, l])
			if l in ["ja", "ko", "zh"] and latin.search(d.replace("Kingpin", "").replace("HQ", "")) != null and slug not in ["premium", "bundle", "automation_plus"]:
				check(false, "texts: %s %s description is not translated" % [slug, l])
	check(true, "texts: Play title/description limits hold in all ten languages")


# ---------------------------------------------------------------- entitlement gate

func _gate() -> void:
	phone()
	check(not Iap.purchased_any() and not Iap.ad_free() and not Iap.has("empire_pass"), "gate: a fresh phone player owns nothing and sees ads")
	for slug in IapCatalog.slugs():
		phone()
		Iap._record(slug, "t-" + slug, "purchase")
		check(Iap.purchased_any(), "gate: buying %s is recorded as purchase history" % slug)
		check(Iap.ad_free() == (slug in AD_FREE_PRODUCTS), "gate: ad_free after %s is %s" % [slug, slug in AD_FREE_PRODUCTS])
	phone()
	Iap._record("cash_s", "tx", "purchase")
	check(Iap.purchased_any() and not Iap.ad_free() and Iap.pending_effects().size() == 1, "gate: a consumable queues its pack and does NOT end interstitials")
	Iap._record("cash_s", "tx", "purchase")
	check(Iap.pending_effects().size() == 1, "gate: a replayed transaction never delivers a consumable twice")
	Iap._record("cash_s", "tx-2", "purchase")
	check(Iap.pending_effects().size() == 2, "gate: a new transaction is a new pack")
	phone()
	Iap._record("premium", "", "restore")
	check(Iap.covered("empire_pass") and Iap.covered("automation_plus") and Iap.covered("hq_theme") and not Iap.covered("offline_plus") and not Iap.covered("cosmetics_all"), \
			"gate: Kingpin Edition covers Empire Pass, Crisis Manager and HQ Signature, not Offline+ or the whole Collection")
	check(Iap.has("empire_pass") and Iap.has("paint_gold") and Iap.has("hq_theme") and Iap.has("auto_plus") and not Iap.has("offline_plus"), "gate: Kingpin Edition grants exactly its four features")
	phone(["bundle"])
	var all_have := true
	for f in FEATURES:
		all_have = all_have and Iap.has(f)
	check(all_have, "gate: the Ultimate Bundle grants every feature")
	check(Iap.pending_effects().size() == 1 and IapCatalog.effect("bundle") == "starter", "gate: the bundle brings the Quick Start pack once")
	phone(["paint_street", "paint_military", "paint_luxury", "hq_theme", "driver_portraits"])
	check(not Iap.covered("cosmetics_all"), "gate: the Collection is not marked Owned just because its parts are (paint_gold is missing)")
	phone()
	Iap.grant_legacy("empire_pass")
	check(Iap.owns("empire_pass") and Iap.pending_effects().is_empty() and Iap.purchased_any() and Iap.ad_free(), "gate: a 1.1.0 Empire Pass buyer keeps it (and its ads-gone) without any pack")
	var round_trip := IapGate.decode(IapGate.encode(Iap._st))
	check(IapGate.owns(round_trip, "empire_pass"), "gate: the entitlement cache survives a restart")
	check(not IapGate.owns(IapGate.decode("{broken"), "empire_pass") and not IapGate.ad_free(IapGate.decode("")) and not IapGate.ad_free(IapGate.decode("{broken")), "gate: a broken cache grants nothing")


# ---------------------------------------------------------------- RevenueCat flows (fake plugin)

func _revenuecat_flow() -> void:
	phone()
	var rc := FakeRc.new()
	rc.iap = Iap
	Iap._rc = rc
	Iap._fetched = true
	Iap.fake = false
	for slug in IapCatalog.slugs():
		Iap._prices[slug] = "$%.2f" % float(IapCatalog.def(slug).usd)
	# purchase a permanent product
	var ok: bool = await Iap.purchase("offline_plus")
	check(ok and Iap.owns("offline_plus") and rc.calls[0] == ["purchase", PREFIX + "offline_plus"], "rc: buying Longer Offline Earnings grants it and asks the store for the right id")
	check(Iap.purchased_any() and not Iap.ad_free(), "rc: a paid convenience is purchase history only - the ads stay")
	ok = await Iap.purchase("offline_plus")
	check(ok and rc.calls.filter(func(c): return c[0] == "purchase").size() == 1, "rc: an owned product is never charged twice")
	# consumable twice = two packs
	ok = await Iap.purchase("cash_m")
	ok = ok and await Iap.purchase("cash_m")
	check(ok and Iap.pending_effects().size() == 2, "rc: a cash pack can be bought again and again, one delivery per purchase")
	# a replayed result of the same transaction
	Iap._on_rc_purchase_result({"product_id": PREFIX + "cash_m", "transaction_id": "tx-1"})
	var before := Iap.pending_effects().size()
	Iap._on_rc_purchase_result({"product_id": PREFIX + "cash_m", "transaction_id": "tx-1"})
	check(Iap.pending_effects().size() == before, "rc: the same transaction twice delivers once")
	# cancel / error / pending
	phone()
	Iap._rc = rc
	Iap._fetched = true
	Iap.fake = false
	Iap._prices["noads"] = "$2.99"
	rc.purchase_cancelled = true
	ok = await Iap.purchase("noads")
	check(not ok and not Iap.owns("noads") and not Iap.purchased_any(), "rc: a cancelled purchase grants nothing")
	rc.purchase_cancelled = false
	rc.purchase_error = "Network error"
	ok = await Iap.purchase("noads")
	check(not ok and not Iap.owns("noads"), "rc: a failed purchase grants nothing")
	rc.purchase_error = "Payment pending approval"
	ok = await Iap.purchase("noads")
	check(not ok and not Iap.owns("noads"), "rc: a pending payment grants nothing until it is approved")
	rc.purchase_error = ""
	check(not Iap.busy, "rc: the store is free again after every outcome")
	# unknown product / no price → nothing happens
	Iap._prices.erase("noads")
	ok = await Iap.purchase("noads")
	check(not ok and not Iap.owns("noads"), "rc: a product the store does not list cannot be bought")
	# restore: permanent comes back, consumables do not
	phone()
	Iap._rc = rc
	Iap._fetched = true
	Iap.fake = false
	rc.entitlements = {"premium": true, "cash_s": true, "noads": false}
	rc.calls.clear()
	var restored: bool = await Iap.restore()
	check(restored and Iap.owns("premium") and Iap.has("auto_plus") and not Iap.owns("noads"), "rc: restore brings back the permanent products the account owns")
	check(Iap.pending_effects().is_empty(), "rc: restore never hands out a consumable")
	check(rc.calls.filter(func(c): return c[0] == "check").size() == IapCatalog.slugs().filter(func(s): return not IapCatalog.is_consumable(s)).size(), "rc: restore asks about every permanent product and no consumable")
	var again: bool = await Iap.restore()
	check(again and Iap.pending_effects().is_empty(), "rc: restoring twice changes nothing")
	rc.entitlements = {}
	Iap._on_rc_entitlement("premium", false)
	check(Iap.owns("premium"), "rc: a negative answer never removes what the player has")
	phone()
	Iap._rc = rc
	rc.entitlements = {}
	var nothing: bool = await Iap.restore()
	check(not nothing and not Iap.purchased_any(), "rc: an account with nothing to restore restores nothing")
	check(not Iap.busy, "rc: restore leaves the store idle")
	Iap._rc = null
	rc.free()
	phone()
	Iap.fake = false # a real store answers: only what it lists is sold
	# product list → prices, only products the store lists are sold
	Iap._on_rc_products({"products": [{"id": PREFIX + "noads:buy", "price": "₺89,99", "amount": 89.99, "currency": "TRY"}]})
	check(Iap.price_text("noads") == "₺89,99" and Iap.available("noads") and not Iap.available("premium"), "rc: the shop shows the store's own price and hides products the store does not list")
	Iap._on_rc_products({"error": "offline", "products": []})
	check(Iap.available("noads"), "rc: a failed price fetch does not hide anything already known")
	phone()


# ---------------------------------------------------------------- what the paid features do

func _features() -> void:
	GameManager.new_game()
	phone()
	check(is_equal_approx(Monetization.delivery_multiplier(), 1.0), "features: no pass = x1.0 delivery pay")
	phone(["empire_pass"])
	check(is_equal_approx(Monetization.delivery_multiplier(), 1.25), "features: Empire Pass = exactly x1.25 (as sold in 1.1.0)")
	phone(["premium"])
	check(is_equal_approx(Monetization.delivery_multiplier(), 1.25), "features: Kingpin Edition includes the Empire Pass multiplier")
	phone()
	check(GameManager.offline_cap() == 7200.0, "features: offline progress counts 2 hours without the product")
	phone(["offline_plus"])
	check(GameManager.offline_cap() == 28800.0, "features: Longer Offline Earnings = 8 hours")
	phone()
	check(not ProgressionManager.crisis_manager_owned() and not ProgressionManager.crisis_manager_on(), "features: Crisis Manager is locked without the product")
	phone(["automation_plus"])
	check(ProgressionManager.crisis_manager_on(), "features: Crisis Manager is on by default once owned")
	ProgressionManager.set_auto("crisis", false)
	check(ProgressionManager.crisis_manager_owned() and not ProgressionManager.crisis_manager_on(), "features: the player can switch Crisis Manager off")
	ProgressionManager.set_auto("crisis", true)
	# Crisis Manager resolves an event by itself
	GameManager.new_game()
	EconomyManager.cash = 1e6
	var offer: Dictionary = ContractManager.offers[0]
	var job := ContractManager.dispatch(offer, VehicleManager.vehicles[0], "normal")
	job.event_id = "breakdown"
	job.phase = "out"
	phone()
	EventManager.trigger(job)
	var pending_without: bool = job.get("pending_event", false)
	check(pending_without, "features: without the product a road event waits for the player")
	EventManager.resolve(job, 0, true)
	phone(["automation_plus"])
	job.event_id = "breakdown"
	EventManager.trigger(job)
	check(not job.get("pending_event", false), "features: with Crisis Manager the event is handled at once")
	# daily jobs claimed automatically
	DailyManager.reset()
	DailyManager.ensure_day()
	for task in DailyManager.state.tasks:
		EconomyManager.stats[task.metric] = float(task.base) + float(task.target) # every task is done
	var cash := EconomyManager.cash
	DailyManager._auto_claim()
	var claimed := 0
	for task in DailyManager.state.tasks:
		claimed += 1 if task.claimed else 0
	check(claimed == 3 and EconomyManager.cash > cash, "features: Crisis Manager claims finished daily jobs")
	phone()
	DailyManager.reset()
	DailyManager.ensure_day()
	for task in DailyManager.state.tasks:
		EconomyManager.stats[task.metric] = float(task.base) + float(task.target)
	check(not ProgressionManager.crisis_manager_on(), "features: without the product daily jobs stay for the player to claim")
	# cosmetics never touch the economy
	GameManager.new_game()
	var income_a := ProgressionManager.reward_mult()
	phone(["cosmetics_all"])
	check(is_equal_approx(ProgressionManager.reward_mult(), income_a) and is_equal_approx(Monetization.delivery_multiplier(), 1.0) and GameManager.offline_cap() == 7200.0, "features: the Cosmetics Collection changes no number in the game")
	phone()


# ---------------------------------------------------------------- cash packs: capped, scaled, honest

func _packs() -> void:
	GameManager.new_game()
	GameManager.running = true
	ProgressionManager.flags["intro_done"] = true
	phone()
	var s0 := Monetization.pack_cash("cash_s")
	var m0 := Monetization.pack_cash("cash_m")
	var l0 := Monetization.pack_cash("cash_l")
	check(s0 > 0.0 and s0 < m0 and m0 < l0, "packs: S < M < L (%s / %s / %s)" % [Fmt.money(s0), Fmt.money(m0), Fmt.money(l0)])
	check(is_equal_approx(m0 / s0, 2.5) or absf(m0 / s0 - 2.5) < 0.05, "packs: M = 2.5 x S (like Corrupt State's 1 / 2.5 / 5 days)")
	check(absf(l0 / s0 - 5.0) < 0.1, "packs: L = 5 x S")
	check(l0 <= EconomyManager.REF_FLOOR * 150.0 * 1.001 and l0 < 40000.0, "packs: a new empire's biggest pack is a few days of income, not a free game (%s)" % Fmt.money(l0))
	EconomyManager.stats["ipm_ref"] = 50000.0
	var big := Monetization.pack_cash("cash_l")
	check(absf(big - 50000.0 * 150.0) < 1000.0, "packs: the pack scales with income (50k/min -> %s)" % Fmt.money(big))
	EconomyManager.stats["ipm_ref"] = 0.0
	check(Monetization.pack_cash("starter") < s0 and Monetization.pack_cash("starter") > 0.0, "packs: Quick Start is smaller than the smallest pack")
	# delivery
	var cash := EconomyManager.cash
	var earned := EconomyManager.stat("earned")
	check(Monetization.deliver("cash_s", "cash_s") and is_equal_approx(EconomyManager.cash, cash + s0), "packs: a delivered pack adds exactly its amount")
	check(is_equal_approx(EconomyManager.stat("earned"), earned), "packs: purchased cash does not count as earned income (stages and goals stay honest)")
	var drivers := VehicleManager.drivers.size()
	Monetization.deliver("starter", "starter")
	check(EconomyManager.cash > cash + s0, "packs: Quick Start pays cash")
	check(VehicleManager.drivers.size() == drivers + 1 or VehicleManager.drivers.size() >= ProgressionManager.driver_slots(), "packs: Quick Start hires a skilled driver when there is room")
	GameManager.running = false
	check(not Monetization.deliver("cash_s", "cash_s"), "packs: while no game runs the pack waits (saved) instead of being lost")
	GameManager.running = true
	check(Monetization.deliver("x", "some_future_effect"), "packs: an unknown effect is dropped, not kept forever")
	# the director delivers the waiting pack
	phone()
	Iap._record("cash_l", "tx-l", "purchase")
	var director := IapDirector.new()
	director.setup({"deliver": Monetization.deliver})
	add_child(director)
	cash = EconomyManager.cash
	var n := director.try_deliver()
	check(n == 1 and EconomyManager.cash > cash and Iap.pending_effects().is_empty(), "packs: the director delivers a bought pack once and forgets it")
	check(director.try_deliver() == 0, "packs: nothing is delivered twice")
	director.queue_free()
	# Legacy Head Start
	ProgressionManager.flags["legacy"] = 2
	ProgressionManager.flags["last_run_ref"] = 40000.0
	var legacy := Monetization.pack_cash("legacy_pack")
	check(legacy >= 3.0 * ProgressionManager.legacy_start_cash(2) and absf(legacy - 40000.0 * 20.0) < 1000.0, "packs: Legacy Head Start = ~20 minutes of the last run's income (%s)" % Fmt.money(legacy))
	check(Monetization._product_visible("legacy_pack") and Monetization._product_visible("cash_s"), "packs: Legacy Head Start is shown once the empire has been sold")
	ProgressionManager.flags.erase("legacy")
	check(not Monetization._product_visible("legacy_pack"), "packs: and hidden before the first prestige")
	GameManager.running = false


# ---------------------------------------------------------------- one-off offers

func _offers() -> void:
	var rec := IapOffers.new_record()
	var ctx := {"day": 4, "event": "", "tutorial": false, "first_session": false}
	check(IapOffers.pick(rec, ctx, {}) == "starter", "offers: Quick Start is offered on day 4")
	check(IapOffers.pick(rec, {"day": 2, "event": "", "tutorial": false, "first_session": false}, {}) == "", "offers: not before day 3")
	check(IapOffers.pick(rec, {"day": 9, "event": "", "tutorial": false, "first_session": false}, {}) == "", "offers: not after day 7")
	check(IapOffers.pick(rec, {"day": 4, "event": "", "tutorial": true, "first_session": false}, {}) == "", "offers: never in the tutorial")
	check(IapOffers.pick(rec, {"day": 4, "event": "", "tutorial": false, "first_session": true}, {}) == "", "offers: never in the first session")
	check(IapOffers.pick(rec, ctx, {"starter": true}) == "", "offers: never a product the player owns")
	IapOffers.shown(rec, "starter", 4)
	check(IapOffers.pick(rec, {"day": 6, "event": "", "tutorial": false, "first_session": false}, {}) == "", "offers: once per game, and at least three days apart")
	check(IapOffers.pick(rec, {"day": 20, "event": "prestige", "tutorial": false, "first_session": false}, {}) == "legacy_pack", "offers: the Legacy Head Start appears on a prestige")
	check(IapOffers.pick(IapOffers.new_record(), {"day": 20, "event": "bankrupt", "tutorial": false, "first_session": false}, {}) == "", "offers: no event, no offer")
	var tutorial_ctx := Monetization.offer_context("")
	check(tutorial_ctx.tutorial == true or ProgressionManager.intro_done(), "offers: the game reports the tutorial state to the rules")
	# the director shows the due offer once, with the store's price, never to a buyer of the thing
	phone()
	var shown := []
	var director := IapDirector.new()
	director.setup({"show_offer": func(slug): shown.append(slug)})
	add_child(director)
	var drec := IapOffers.new_record()
	var picked := director.check_offer(drec, {"day": 4, "event": "", "tutorial": false, "first_session": false})
	check(picked == "starter" and shown == ["starter"] and Iap.price_text("starter") != "", "offers: the director shows Quick Start on day 4 with a price")
	check(director.check_offer(drec, {"day": 5, "event": "", "tutorial": false, "first_session": false}) == "", "offers: and not again")
	phone(["bundle"])
	check(director.check_offer(IapOffers.new_record(), {"day": 4, "event": "", "tutorial": false, "first_session": false}) == "", "offers: a bundle owner is never offered Quick Start")
	phone()
	Iap._test_setup(IapGate.empty_state(), false, false) # desktop: no store, no offers
	check(director.check_offer(IapOffers.new_record(), {"day": 4, "event": "", "tutorial": false, "first_session": false}) == "", "offers: no store, no offer")
	director.queue_free()
	phone()
	# a purchase in the bundle hides Quick Start
	phone(["bundle"])
	check(not Monetization._product_visible("starter"), "offers: a bundle owner already got Quick Start and is not offered it")
	phone()
	check(Monetization._product_visible("starter"), "offers: Quick Start is shown to everyone else")


# ---------------------------------------------------------------- ads: ad_free only; house-first interstitial; rewarded

func _ads_and_rewarded() -> void:
	phone()
	check(not Ads.ad_free_check.call(), "ads: a player who bought nothing is not ad-free")
	phone(["noads"])
	check(Ads.ad_free_check.call(), "ads: Remove Ads is ad-free")
	phone()
	Iap._record("cash_s", "tx", "purchase")
	check(not Ads.ad_free_check.call() and not Monetization.video_free(), "ads: a cash pack alone does not end interstitials or the rewarded video")
	var src := FileAccess.get_file_as_string("res://scripts/monetization/coddy_ads.gd")
	check(src.contains("ads-config") and src.contains("perAppBudget") and src.contains("InterstitialAdLoader"), "ads: interstitials are house-first (ads-config sequence) then AdMob")
	check(src.contains('bool(_config.get("adsEnabled", false))') and src.contains("_presented_ad") and src.contains("func start_admob") and src.contains("npa"), "ads: kill switch, stale-callback and double-tap guards, lazy AdMob start, non-personalised requests")
	check(str(ProjectSettings.get_setting("coddy_ads/app_id", "")) == "borderline-empire", "ads: the ads-config host is borderline-empire")
	check(str(ProjectSettings.get_setting("coddy_ads/admob_interstitial_android", "")).begins_with("ca-app-pub-5045495642689387/"), "ads: a real interstitial unit is set for release (test unit in debug)")
	var gradle := FileAccess.get_file_as_string("res://addons/coddy_firebase_gradle/plugin.gd")
	check(gradle.contains("coddy_ads/keep_ad_id") and not bool(ProjectSettings.get_setting("coddy_ads/keep_ad_id", false)), "ads: the Android manifest keeps AD_ID stripped (Play 'Advertising ID = No', like Airport / Train after their rejection)")
	check(not Ads.is_showing(), "ads: no ad is showing on desktop (CoddyAds is a no-op there)")
	Ads.interstitial("test")
	check(not Ads.is_showing(), "ads: asking for an interstitial on desktop does nothing")
	# breaks: after enough finished trips, never for an ad-free player
	phone()
	GameManager.new_game()
	ProgressionManager.flags["intro_done"] = true
	TutorialManager.finished = true
	Monetization._returned_since_break = 0
	Monetization._on_panel_closed("fleet")
	check(Monetization._returned_since_break == 0, "ads: no break before enough trips")
	Monetization._returned_since_break = Monetization.BREAK_EVERY_RETURNS
	phone(["noads"])
	Monetization._on_panel_closed("fleet")
	check(Monetization._returned_since_break == Monetization.BREAK_EVERY_RETURNS, "ads: an ad-free player's counter is never spent on a break")
	phone()
	Iap._record("cash_m", "tx-break", "purchase")
	Monetization._on_panel_closed("fleet")
	check(Monetization._returned_since_break == 0, "ads: a cash-pack buyer still reaches the break")
	Monetization._returned_since_break = Monetization.BREAK_EVERY_RETURNS
	phone()
	Monetization._on_panel_closed("fleet")
	check(Monetization._returned_since_break == 0, "ads: a free player reaches the break and the counter restarts")
	TutorialManager.finished = false
	Monetization._returned_since_break = 99
	Monetization._on_panel_closed("fleet")
	check(Monetization._returned_since_break == 99, "ads: never during the tutorial")
	TutorialManager.finished = true
	# rewarded sponsor: an ad-free player takes it without the video
	phone(["noads"])
	GameManager.new_game()
	Monetization.last_reward = 0
	var cash := EconomyManager.cash
	Monetization.claim_sponsor()
	check(EconomyManager.cash > cash and Monetization.video_free(), "ads: an ad-free player claims the sponsor bonus without watching a video")
	phone()
	Monetization.last_reward = 0
	cash = EconomyManager.cash
	Monetization.claim_sponsor()
	check(EconomyManager.cash == cash, "ads: a free player on desktop gets no bonus without an ad (no mock ads)")
	var rw_src := FileAccess.get_file_as_string("res://scripts/monetization/rewarded_ads.gd")
	check(rw_src.contains("when_sdk_ready") and rw_src.contains("_consent_ok()") and FileAccess.get_file_as_string("res://scripts/monetization/coddy_ads.gd").contains("UserMessagingPlatform.consent_information.update"), "ads: the rewarded ad waits for the one UMP consent + SDK start of the Ads autoload and still checks consent")


# ---------------------------------------------------------------- rewarded placements: daily cap + cooldown, grants

func _rewarded_placements() -> void:
	# every placement has a 2-5 daily cap and a cooldown
	for p in Monetization.PLACEMENTS:
		check(Monetization.daily_cap(p) >= 2 and Monetization.daily_cap(p) <= 5, "rewarded: %s has a daily cap of 2-5" % p)
		check(Monetization._limit(p, "cooldown_seconds") >= 60, "rewarded: %s has a cooldown" % p)
	check(Monetization._limit("sponsor", "cooldown_seconds") == 600, "rewarded: the sponsor bonus keeps its ten-minute cooldown")
	# sponsor: an ad-free player claims up to the cap (cooldown skipped by resetting the stamp), then the cap blocks it
	phone(["noads"])
	GameManager.new_game()
	Monetization.ad_log = {}
	Monetization.last_reward = 0
	check(Monetization.placement_ready("sponsor"), "rewarded: an ad-free player's sponsor bonus is ready without any ad")
	var cap := Monetization.daily_cap("sponsor")
	for i in cap:
		Monetization.last_reward = 0
		Monetization.claim_sponsor()
	check(Monetization.uses_today("sponsor") == cap and Monetization.cap_left("sponsor") == 0, "rewarded: the sponsor bonus counts every use of the day")
	Monetization.last_reward = 0
	var cash := EconomyManager.cash
	Monetization.claim_sponsor()
	check(EconomyManager.cash == cash and not Monetization.placement_ready("sponsor"), "rewarded: the daily cap blocks the sponsor bonus")
	# the cap and the cooldown survive a restart (monetization.json) and the cap reopens on a new day
	Monetization.last_reward = Time.get_unix_time_from_system()
	Monetization._persist()
	var saved: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(Monetization._path))
	var back := Monetization._parse_ad_log(saved.get("ad_log", {}))
	check(int(back.get("sponsor", {}).get("count", 0)) == cap and float(saved.get("last_reward", 0.0)) > 0.0, "rewarded: caps and the sponsor stamp are persisted")
	Monetization.ad_log = back
	check(Monetization.cap_left("sponsor") == 0, "rewarded: a restart does not refill the cap")
	Monetization.ad_log["sponsor"]["day"] = "2000-01-01"
	check(Monetization.cap_left("sponsor") == cap, "rewarded: the cap reopens on a new day")
	Monetization.ad_log = {}
	Monetization._record_use("repair_finish")
	check(Monetization.placement_cooldown_left("repair_finish") > 170 and not Monetization.placement_open("repair_finish"), "rewarded: a used placement waits for its cooldown")
	# offline_double: the receipt's earnings once more (capped at two hours' worth), once per receipt
	Monetization.ad_log = {}
	var w32cap := floorf(Monetization.reward_cap()) # W32: never more than 1/5 of cash_s in one reward
	check(is_equal_approx(Monetization.offline_bonus({"seconds": 3600.0, "earned": 1000.0}), minf(1000.0, w32cap)), "rewarded: offline bonus = the earnings again (within the W32 cap)")
	check(is_equal_approx(Monetization.offline_bonus({"seconds": 28800.0, "earned": 8000.0}), minf(2000.0, w32cap)), "rewarded: offline bonus is capped at two hours' worth (and the W32 cap)")
	check(w32cap > 0.0 and w32cap <= Monetization.pack_cash("cash_s") * 0.2, "W32: one cash reward is at most 1/5 of cash_s")
	GameManager.session["receipt"] = {"id": "t1", "seconds": 3600.0, "earned": 1000.0, "deliveries": 1, "manager": 0.0, "applied": true, "acknowledged": true}
	check(Monetization.offline_double_offer(GameManager.session["receipt"]), "rewarded: the welcome-back card offers the double to an ad-free player")
	cash = EconomyManager.cash
	Monetization.claim_placement("offline_double", {"id": "other"})
	check(EconomyManager.cash == cash and Monetization.uses_today("offline_double") == 0, "rewarded: a different receipt id grants nothing and spends no cap")
	Monetization.claim_placement("offline_double", {"id": "t1"})
	check(is_equal_approx(EconomyManager.cash, cash + minf(1000.0, w32cap)) and Monetization.uses_today("offline_double") == 1, "rewarded: offline double grants the bonus and counts the use")
	check(is_equal_approx(Monetization.cash_today(), minf(1000.0, w32cap)), "W32: the offline double is counted in the day's cash budget")
	# the day's budget (1/2 of cash_s) closes every cash placement once spent; repairs (no cash) stay open
	Monetization.ad_log["_cash"] = {"day": Monetization._utc_day(), "count": Monetization.pack_cash("cash_s") * 0.5, "last": 0.0}
	check(not Monetization.placement_open("sponsor") and not Monetization.placement_open("daily_double") and Monetization.placement_open("repair_finish"), "W32: a spent budget closes the cash placements only")
	var restored: Dictionary = Monetization._parse_ad_log(Monetization.ad_log)
	check(restored.has("_cash") and is_equal_approx(float(restored._cash.count), Monetization.pack_cash("cash_s") * 0.5), "W32: the budget survives a restart")
	Monetization.ad_log.erase("_cash")
	# remote kill switch
	Monetization.rewarded_remote = false
	check(not Monetization.ads_available(), "W32: rewardedEnabled=false → no rewarded video")
	Monetization.rewarded_remote = true
	check(bool(GameManager.session["receipt"].get("doubled", false)) and not Monetization.offline_double_offer(GameManager.session["receipt"]), "rewarded: a receipt is doubled only once")
	Monetization.ad_log = {}
	cash = EconomyManager.cash
	Monetization.claim_placement("offline_double", {"id": "t1"})
	check(EconomyManager.cash == cash, "rewarded: a doubled receipt cannot be doubled again")
	# repair_finish: finishes a running repair only
	var v: Dictionary = VehicleManager.vehicles[0]
	Monetization.ad_log = {}
	Monetization.claim_placement("repair_finish", {"vehicle": str(v.id)})
	check(Monetization.uses_today("repair_finish") == 0, "rewarded: nothing to repair = nothing granted and no cap spent")
	VehicleManager.start_repair(v, 120.0)
	check(v.state == "repair", "rewarded: test vehicle is repairing")
	Monetization.claim_placement("repair_finish", {"vehicle": str(v.id)})
	check(v.state == "idle" and float(v.repair_left) == 0.0 and Monetization.uses_today("repair_finish") == 1, "rewarded: repair_finish completes the repair")
	# daily_double: only a claimed task, once
	DailyManager.state = {"day": DailyManager.utc_day(), "tasks": [
		{"id": "a", "metric": "deliveries", "target": 3.0, "reward": 150.0, "text": "Complete %d deliveries", "base": 0.0, "claimed": false},
		{"id": "b", "metric": "deliveries", "target": 3.0, "reward": 200.0, "text": "Complete %d deliveries", "base": 0.0, "claimed": true}]}
	Monetization.ad_log = {}
	cash = EconomyManager.cash
	Monetization.claim_placement("daily_double", {"task": "a", "day": DailyManager.utc_day()})
	check(EconomyManager.cash == cash and Monetization.uses_today("daily_double") == 0, "rewarded: an unclaimed daily task cannot be doubled")
	Monetization.claim_placement("daily_double", {"task": "b", "day": DailyManager.utc_day()})
	check(is_equal_approx(EconomyManager.cash, cash + 200.0) and Monetization.uses_today("daily_double") == 1, "rewarded: daily double pays the reward once more")
	Monetization.ad_log = {}
	Monetization.claim_placement("daily_double", {"task": "b", "day": DailyManager.utc_day()})
	check(is_equal_approx(EconomyManager.cash, cash + 200.0), "rewarded: a task is doubled only once")
	# a free player: nothing without the SDK's earned callback (no mock ads on desktop), the callback with the right token grants
	phone()
	GameManager.new_game()
	Monetization.ad_log = {}
	v = VehicleManager.vehicles[0]
	VehicleManager.start_repair(v, 120.0)
	check(not Monetization.placement_ready("repair_finish"), "rewarded: no ad ready on desktop = the offer is not shown")
	Monetization.claim_placement("repair_finish", {"vehicle": str(v.id)})
	check(v.state == "repair", "rewarded: a free player gets no reward without an ad")
	Monetization._pending_reward = "tok"
	Monetization._pending_placement = "repair_finish"
	Monetization._pending_ctx = {"vehicle": str(v.id)}
	Monetization._on_reward_earned("wrong")
	check(v.state == "repair", "rewarded: an unmatched earned token grants nothing")
	Monetization._on_reward_earned("tok")
	check(v.state == "idle", "rewarded: the earned callback grants the placement")
	Monetization.ad_log = {}
	Monetization.last_reward = 0


# ---------------------------------------------------------------- ad_free policy (games/CLAUDE.md §3 + §8)

class FakeFa extends RefCounted:
	var events: Array = []

	func log_event(event_name: String, params: Dictionary) -> void:
		events.append([event_name, params])


func _ad_free_policy() -> void:
	# §8: each product (and the bundle that contains it) is ad-free; cheap ones are not; the catalog says so
	for slug in IapCatalog.slugs():
		var grants_no_ads := "no_ads" in IapCatalog.grants(slug)
		check(grants_no_ads == (slug in AD_FREE_PRODUCTS), "policy: %s %s the no_ads feature" % [slug, "grants" if slug in AD_FREE_PRODUCTS else "does not grant"])
	for slug in AD_FREE_PRODUCTS:
		phone([slug])
		check(Iap.ad_free() and Ads.ad_free_check.call() and Monetization.ad_free() and Monetization.video_free(), "policy: %s => ad_free (interstitials off, rewarded without video)" % slug)
	for slug in ["automation_plus", "offline_plus", "paint_street", "paint_military", "paint_luxury", "hq_theme", "driver_portraits", "cosmetics_all", "starter", "legacy_pack", "cash_s", "cash_m", "cash_l"]:
		phone([slug])
		check(not Iap.ad_free() and not Ads.ad_free_check.call() and not Monetization.video_free(), "policy: %s alone keeps the ads and the rewarded video" % slug)
	phone(["paint_street", "hq_theme", "cash_l", "starter", "offline_plus"])
	check(Iap.purchased_any() and not Iap.ad_free(), "policy: many cheap purchases together still show ads")
	phone()
	check(not Iap.ad_free() and not Monetization.video_free(), "policy: a fresh install is not ad-free (legacy_ad_free=false)")
	# bought anything on desktop/QA never counts as ad-free either (the store only sells on phones)
	Iap._test_setup(IapGate.empty_state(), false, false)
	check(not Iap.ad_free(), "policy: a build without a store is not ad-free")
	phone()
	# the ad-free sources are the only way an ad decision is made: no purchased_any() in the ad scripts
	var mon := FileAccess.get_file_as_string("res://scripts/monetization/monetization.gd")
	check(not mon.contains("Iap.purchased_any()") and mon.contains("Iap.ad_free()"), "policy: Monetization decides ads with Iap.ad_free() only")
	# legacy migration: an old "bought anything" cache => legacy_ad_free once; the marker keeps later cheap buys out
	var old_cash := IapGate.decode('{"v":1,"owned":{},"any":true,"pending":[]}')
	check(bool(old_cash.legacy_ad_free) and bool(old_cash.ad_free_migrated) and IapGate.ad_free(old_cash), "legacy: an old cache with any=true migrates to legacy_ad_free")
	var old_owned := IapGate.decode('{"v":1,"owned":{"paint_street":true}}')
	check(bool(old_owned.legacy_ad_free), "legacy: an old cache that only owns a look (no any flag) migrates too")
	var old_none := IapGate.decode('{"v":1,"owned":{},"any":false}')
	check(not bool(old_none.legacy_ad_free) and not IapGate.ad_free(old_none), "legacy: an old cache with no purchase does not migrate")
	var fresh := IapGate.decode("")
	check(not bool(fresh.legacy_ad_free) and bool(fresh.ad_free_migrated), "legacy: a fresh install is false and carries the marker")
	var marked := IapGate.decode(IapGate.encode(old_cash))
	check(bool(marked.legacy_ad_free), "legacy: the migrated flag survives save/load")
	var stamped := IapGate.decode('{"v":1,"owned":{},"any":true,"ad_free_migrated":true}')
	check(not bool(stamped.legacy_ad_free) and not IapGate.ad_free(stamped), "legacy: with the marker set, any=true alone never grants it (a cheap purchase of the new version)")
	var later := IapGate.empty_state()
	IapGate.record(later, "cash_s", "tx-later")
	later = IapGate.decode(IapGate.encode(later))
	check(not bool(later.legacy_ad_free) and not IapGate.ad_free(later), "legacy: a cheap purchase after the migration never sets legacy_ad_free")
	# the store persists the marker on the first launch (before any purchase callback can run)
	var file := FileAccess.open(Iap.CACHE, FileAccess.WRITE)
	file.store_string('{"v":1,"owned":{},"any":true}')
	file.close()
	Iap._test_setup(IapGate.decode(FileAccess.get_file_as_string(Iap.CACHE)), true, true)
	Iap.persist = true # _test_setup switched the cache file off
	Iap._save_state("ad_free_migration")
	Iap.persist = false
	var disk := IapGate.decode(FileAccess.get_file_as_string(Iap.CACHE))
	check(bool(JSON.parse_string(FileAccess.get_file_as_string(Iap.CACHE)).get("ad_free_migrated", false)) and bool(disk.legacy_ad_free), "legacy: the first launch writes legacy_ad_free + the marker to disk")
	check(Iap.ad_free() and Ads.ad_free_check.call() and Monetization.video_free(), "legacy: a migrated old buyer is ad-free and takes rewarded without video")
	DirAccess.remove_absolute(ProjectSettings.globalize_path(Iap.CACHE))
	# a 1.1.0 Empire Pass holder (monetization.json owned=true) arrives as an owner of the Pass => ads gone
	phone()
	Iap.grant_legacy("empire_pass")
	check(Iap.ad_free() and Monetization.video_free(), "legacy: a 1.1.0 Empire Pass buyer is ad-free through the Pass")
	phone()


# ---------------------------------------------------------------- rewarded rules (games/CLAUDE.md §4)

func _rewarded_policy() -> void:
	var pack := Monetization.pack_cash("cash_s")
	var fa := FakeFa.new()
	var previous_consent := Analytics.consent
	Analytics.consent = true # this event-order fixture represents an explicitly opted-in player
	Analytics._fa = fa
	Analytics._ready_ok = true
	# a cash-pack buyer sees the offer only when an ad is ready (none on desktop) and never claims without the video
	phone(["cash_m"])
	GameManager.new_game()
	Monetization.ad_log = {}
	Monetization.last_reward = 0
	check(not Monetization.video_free() and not Monetization.placement_ready("sponsor"), "rewarded: a cash-pack buyer needs the video (no ad ready on desktop = no offer)")
	var cash := EconomyManager.cash
	Monetization.claim_sponsor()
	check(EconomyManager.cash == cash and fa.events.filter(func(e): return e[0] == "rewarded_skipped_paid_user").is_empty(), "rewarded: a cash-pack buyer gets no video-free claim")
	fa.events.clear()
	# an ad-free player: video-free claim, same amount and caps, events with the three parameters
	phone(["noads"])
	GameManager.new_game()
	Monetization.ad_log = {}
	Monetization._offer_logged = {}
	Monetization.last_reward = 0
	Monetization.note_offer("sponsor", Monetization.reward_value())
	Monetization.claim_sponsor()
	var names := fa.events.map(func(e): return e[0])
	check(names == ["rewarded_offer_shown", "rewarded_offer_accepted", "rewarded_skipped_paid_user", "rewarded_completed"], "events: shown / accepted / skipped_paid_user / completed, in order (got %s)" % str(names))
	var all_params := true
	for e in fa.events:
		var d: Dictionary = e[1]
		all_params = all_params and d.get("placement") == "sponsor" and d.has("reward_amount") and d.has("daily_index")
	check(all_params, "events: every rewarded event carries placement, reward_amount and daily_index")
	var done: Dictionary = fa.events[3][1]
	check(int(done.reward_amount) > 0 and int(done.reward_amount) == int(fa.events[1][1].reward_amount), "events: rewarded_completed reports the cash that was offered and paid")
	check(int(done.daily_index) == 1 and int(fa.events[0][1].daily_index) == 1, "events: the first sponsor reward of the day has daily_index 1")
	Monetization.last_reward = 0
	fa.events.clear()
	Monetization.claim_sponsor()
	check(int(fa.events[-1][1].daily_index) == 2, "events: the second use of the day has daily_index 2")
	check(not names.has("ad_reward_offer") and not names.has("ad_reward_earned"), "events: the old ad_reward_* names are gone")
	var src := FileAccess.get_file_as_string("res://scripts/monetization/monetization.gd")
	check(not src.contains("ad_reward_offer") and not src.contains("ad_reward_earned"), "events: no ad_reward_* event name is left in the code")
	Analytics._fa = null
	Analytics._ready_ok = false
	Analytics.consent = previous_consent
	# caps: one reward <= 1/5 and the UTC day <= 1/2 of the cheapest consumable (cash_s)
	phone(["noads"])
	Monetization.ad_log = {}
	Monetization.last_reward = 0
	check(Monetization.reward_value() <= pack * 0.2 + 0.001, "caps: one sponsor reward is at most 1/5 of cash_s")
	var total := 0.0
	for round_i in 3:
		for p in ["sponsor", "offline_double", "daily_double"]:
			Monetization.ad_log.erase(p)
			Monetization.last_reward = 0
			var before := EconomyManager.cash
			match p:
				"sponsor":
					Monetization.claim_sponsor()
				"offline_double":
					GameManager.session["receipt"] = {"id": "cap%d" % round_i, "seconds": 3600.0, "earned": 1.0e6, "deliveries": 0, "manager": 0.0, "applied": true, "acknowledged": true}
					Monetization.claim_placement(p, {"id": "cap%d" % round_i})
				"daily_double":
					DailyManager.state = {"day": DailyManager.utc_day(), "tasks": [{"id": "q", "metric": "deliveries", "target": 1.0, "reward": 250.0, "text": "x", "base": 0.0, "claimed": true}]}
					Monetization.claim_placement(p, {"task": "q", "day": DailyManager.utc_day()})
			var gained := EconomyManager.cash - before
			check(gained <= pack * 0.2 + 0.001, "caps: a single %s reward never exceeds 1/5 of cash_s" % p)
			total += gained
	GameManager.session["receipt"] = {}
	check(total <= pack * 0.5 + 0.001 and is_equal_approx(Monetization.cash_today(), total), "caps: all cash rewards of a UTC day share one budget of 1/2 of cash_s")
	check(not Monetization.placement_open("sponsor") and Monetization.reward_cap() < 1.0, "caps: once the day's budget is spent the cash placements close")
	# the budget is per UTC day
	check(Monetization._utc_day() == Time.get_date_string_from_system(true), "caps: the counting day is the UTC day")
	Monetization.ad_log["_cash"]["day"] = "2000-01-01"
	check(Monetization.cash_today() == 0.0 and Monetization.reward_cap() > 0.0, "caps: the budget reopens on the next UTC day")
	# a nearly spent budget shortens the daily double instead of overshooting it
	Monetization.ad_log = {"_cash": {"day": Monetization._utc_day(), "count": pack * 0.5 - 100.0, "last": 0.0}}
	DailyManager.state = {"day": DailyManager.utc_day(), "tasks": [{"id": "q", "metric": "deliveries", "target": 1.0, "reward": 250.0, "text": "x", "base": 0.0, "claimed": true}]}
	check(is_equal_approx(Monetization.daily_double_amount("q"), 100.0), "caps: the daily double is limited to what is left of the day's budget")
	var before_cash := EconomyManager.cash
	Monetization.claim_placement("daily_double", {"task": "q", "day": DailyManager.utc_day()})
	check(is_equal_approx(EconomyManager.cash - before_cash, 100.0) and Monetization.cash_today() <= pack * 0.5 + 0.001, "caps: the shortened daily double pays exactly the remainder")
	Monetization.ad_log = {}
	# remote kill switch: video offers hide, ad-free players are unaffected
	Monetization.rewarded_remote = false
	check(not Monetization.ads_available(), "kill switch: rewardedEnabled=false hides the video")
	phone(["premium"])
	Monetization.last_reward = 0
	check(Monetization.placement_ready("sponsor"), "kill switch: an ad-free player still takes the reward without the video")
	phone()
	check(not Monetization.placement_ready("sponsor"), "kill switch: a free player sees no offer")
	Monetization.rewarded_remote = true
	# the remote config reaches the flag (coddy_ads.gd reads `rewardedEnabled`, absent = on)
	var ads_src := FileAccess.get_file_as_string("res://scripts/monetization/coddy_ads.gd")
	check(ads_src.contains('j.get("rewardedEnabled", true)') and ads_src.contains("m.rewarded_remote"), "kill switch: ads-config rewardedEnabled (absent = on) drives rewarded_remote")
	# a watched video postpones the next interstitial by one interval; the video-free claim does not
	Ads.quiet_until = 0.0
	phone()
	GameManager.new_game()
	var v: Dictionary = VehicleManager.vehicles[0]
	VehicleManager.start_repair(v, 120.0)
	Monetization._pending_reward = "tokA"
	Monetization._pending_placement = "repair_finish"
	Monetization._pending_ctx = {"vehicle": str(v.id)}
	var t0 := Time.get_unix_time_from_system()
	Monetization._on_reward_earned("tokA")
	var q1: float = Ads.quiet_until
	check(q1 >= t0 + Ads.MIN_GAP_S - 1.0 and q1 < t0 + 2.0 * Ads.MIN_GAP_S + 5.0, "interstitial: a watched rewarded video postpones the next break by an interval (+%ds)" % int(q1 - t0))
	Monetization._pending_reward = "tokB"
	Monetization._pending_placement = "repair_finish"
	Monetization._pending_ctx = {"vehicle": "none"}
	Monetization._on_reward_earned("tokB", false)
	check(is_equal_approx(Ads.quiet_until, q1), "interstitial: a video-free claim does not postpone anything")
	Monetization._on_reward_earned("wrong")
	check(is_equal_approx(Ads.quiet_until, q1), "interstitial: an unmatched token does not postpone anything")
	Ads.quiet_until = 0.0
	Monetization.ad_log = {}
	Monetization.last_reward = 0


# ---------------------------------------------------------------- store rating (owner standard 7) + feedback

## The live asker (fakes for the store call and the calm probe): a brand-new player is asked in the first session, once
## ~4 minutes of play are in and the tutorial is over; never before. Restores everything it touched.
func _review_first_session() -> void:
	var ra: Node = Monetization.review
	if ra == null:
		return
	var keep_st: Dictionary = ra.st
	var keep_path: String = ra.path
	var keep_finished: bool = TutorialManager.finished
	var keep_intro = ProgressionManager.flags.get("intro_done", null)
	var sent := [0]
	ra.path = "user://review_first_session_test.json"
	ra.available_hook = func() -> bool: return true
	ra.calm_hook = func() -> bool: return true
	ra.request_hook = func() -> bool:
		sent[0] += 1
		return true
	ra.st = Gate.empty_state()
	ra.st.sessions = 1
	ra.st.play_s = Gate.MIN_PLAY_S - 30.0
	TutorialManager.finished = true
	ProgressionManager.flags["intro_done"] = true
	ra._on_stage(1)
	check(ra._pending == "", "review: first session, no ask before MIN_PLAY_S")
	ra.st.play_s = Gate.MIN_PLAY_S + 1.0
	TutorialManager.finished = false
	ra._on_stage(2)
	check(ra._pending == "", "review: first session, never while the tutorial runs")
	TutorialManager.finished = true
	ra._on_stage(3)
	check(ra._pending == "stage", "review: first session + enough play + a promotion -> the ask is queued")
	ra._process(1.1)
	check(sent[0] == 1 and int(ra.st.asks) == 1 and ra._pending == "", "review: first session -> the OS sheet is requested once and counted")
	ra._on_stage(4)
	check(ra._pending == "", "review: not again within 30 days")
	DirAccess.remove_absolute(ProjectSettings.globalize_path(ra.path))
	ra.available_hook = Callable()
	ra.calm_hook = Callable()
	ra.request_hook = Callable()
	ra.st = keep_st
	ra.path = keep_path
	ra._pending = ""
	TutorialManager.finished = keep_finished
	if keep_intro == null:
		ProgressionManager.flags.erase("intro_done")
	else:
		ProgressionManager.flags["intro_done"] = keep_intro


func _review_and_feedback() -> void:
	var st := Gate.empty_state()
	var now := 1.8e9
	st.sessions = 1
	st.play_s = Gate.MIN_PLAY_S
	check(Gate.may_ask(st, "stage", 1, false, now), "review: first session + ~4 min of play + the 1st promotion -> may ask (owner 2026-10-27)")
	check(not Gate.may_ask(st, "stage", 0, false, now), "review: no promotion, no ask")
	check(not Gate.may_ask(st, "stage", 1, true, now), "review: never during the tutorial")
	st.play_s = Gate.MIN_PLAY_S - 1.0
	check(not Gate.may_ask(st, "stage", 1, false, now), "review: not just before MIN_PLAY_S of play")
	st.play_s = Gate.MIN_PLAY_S
	Gate.goal_done(st)
	Gate.goal_done(st)
	check(not Gate.may_ask(st, "goal", 0, false, now), "review: three goals are needed for the goal moment")
	Gate.goal_done(st)
	check(Gate.may_ask(st, "goal", 0, false, now), "review: first session, asks after the 3rd finished goal")
	check(not Gate.may_ask(st, "goal", 0, true, now), "review: first session, still never during the tutorial")
	check(Gate.MIN_SESSIONS == 1 and Gate.MIN_PLAY_S >= 180.0 and Gate.MIN_PLAY_S <= 300.0 and Gate.MAX_ASKS == 3 and Gate.GAP_S >= 30.0 * 86400.0, "review: first session after 3-5 min, <= 3 asks, >= 30 days apart")
	Gate.asked(st, now)
	check(not Gate.may_ask(st, "stage", 3, false, now + 10 * 86400.0), "review: not again within 30 days")
	check(Gate.may_ask(st, "stage", 3, false, now + 31 * 86400.0), "review: again after 30 days")
	Gate.asked(st, now + 31 * 86400.0)
	Gate.asked(st, now + 62 * 86400.0)
	check(not Gate.may_ask(st, "stage", 3, false, now + 200 * 86400.0), "review: at most 3 asks in all")
	check(Gate.promoted(Gate.empty_state(), 2) and not Gate.promoted({"stage": 5}, 3), "review: a promotion counts once")
	check(Gate.decode(Gate.encode(st)).asks == 3 and Gate.decode("garbage").asks == 0, "review: its counters survive a restart; a broken file starts fresh")
	# policy scan: the game never builds its own "do you like it" gate or pays for a rating
	var banned := RegEx.create_from_string("(?i)do you (like|enjoy) |rate (us|this game)\\??\"|how many stars|enjoying the game|reward.{0,24}(rating|review)")
	var offenders := []
	for path in ["res://scripts/monetization/review_ask.gd", "res://scripts/monetization/review_gate.gd", "res://scripts/monetization/review_native.gd", "res://scripts/ui/feedback_modal.gd", "res://scripts/ui/panels/settings_sheet.gd"]:
		var text := FileAccess.get_file_as_string(path)
		for line in text.split("\n"):
			if not line.strip_edges().begins_with("##") and not line.strip_edges().begins_with("#") and banned.search(line) != null:
				offenders.append(path)
	check(offenders.is_empty(), "review: no own star card / pre-question / reward for rating %s" % str(offenders))
	check(FileAccess.get_file_as_string("res://scripts/monetization/review_native.gd").contains("ReviewManagerFactory"), "review: the Google Play In-App Review API is used on Android")
	check(FileAccess.get_file_as_string("res://addons/coddy_firebase_gradle/plugin.gd").contains("com.google.android.play:review"), "review: the Play review library is added to the Android export")
	check(Monetization.review != null and not Monetization.review._available(), "review: desktop has no native review call, so it never asks")
	_review_first_session()
	# feedback
	check(Feedback.problem("nope", "a long enough message here") == "email" and Feedback.problem("me@example.com", "short") == "message" and Feedback.problem(" me@example.com ", "this is a long enough message") == "", "feedback: the form validates e-mail and message length")
	var body := Feedback.body("", "Me@Example.com", "BUG", "something broke here")
	check(body.email == "me@example.com" and str(body.subject).begins_with("Borderline Empire") and Feedback.APP_SLUG == "borderline-empire", "feedback: the note carries the app and a lower-cased e-mail")
	check(Feedback.query("BUG", "tr").contains("appSlug=borderline-empire") and Feedback.query("BUG", "tr").contains("lang=tr"), "feedback: the query names app, version and language")
	check(Feedback.device_key().length() == 64, "feedback: a 64-hex device key identifies the phone only to the API")
	var modal := FeedbackModal.new()
	add_child(modal)
	modal.setup()
	await frames(2)
	check(modal.email != null and modal.message != null and modal.category_buttons.size() == 4, "feedback: the modal has category, e-mail and message")
	var sent := []
	modal.send_hook = func(c, e, m):
		sent.append([c, e, m])
		return true
	modal.email.text = "bad"
	modal.message.text = "this message is long enough"
	modal.submit()
	check(sent.is_empty() and modal.note.text != "", "feedback: a bad e-mail is not sent")
	modal.email.text = "me@example.com"
	modal.category_buttons["bug"].pressed.emit()
	modal.submit()
	await frames(2)
	check(sent.size() == 1 and sent[0][0] == "bug" and not modal.busy, "feedback: a valid note is sent with its category")
	modal.queue_free()


# ---------------------------------------------------------------- looks: portraits, paints, signature

func _portraits_and_paints() -> void:
	var names := ["Rita Redline", "Tom Quickstep", "Lev Ghost", "Ana Biscuit", "Kofi Velvet", "Mila Static", "Jax Boomer", "Nina Lucky", "Oskar Dash", "Priya Hush", "Bo Rocket", "Zed Patch"]
	var seen := {}
	var valid := true
	for n in names:
		var t := DriverPortrait.traits(n)
		valid = valid and t == DriverPortrait.traits(n) and int(t.skin) < DriverPortrait.SKIN.size() and int(t.hair) < DriverPortrait.HAIR.size() \
				and int(t.style) < DriverPortrait.STYLES and int(t.acc) < DriverPortrait.ACCESSORIES and int(t.shirt) < DriverPortrait.SHIRT.size()
		seen[str(t)] = true
	check(valid and seen.size() >= 9, "looks: every driver has one stable, valid face and the faces differ (%d of 12 distinct)" % seen.size())
	var p := DriverPortrait.make({"name": "Rita Redline", "rarity": "elite"}, 72)
	add_child(p)
	await frames(2)
	check(p.custom_minimum_size == Vector2(72, 72), "looks: the portrait draws without errors")
	p.queue_free()
	var fleet := load("res://scripts/ui/panels/fleet_sheet.gd")
	var sets: Array = fleet.PAINT_SETS
	var ok_sets := sets.size() == 4
	for s in sets:
		ok_sets = ok_sets and s[0] in FEATURES and not (s[2] as Array).is_empty()
		for hex in s[2]:
			ok_sets = ok_sets and str(hex).length() == 6 and str(hex).is_valid_hex_number()
	check(ok_sets, "looks: four paint sets, each tied to a catalog feature, with valid colours")
	var free_colors: Array = fleet.PAINTS
	var clash := false
	for s in sets:
		for hex in s[2]:
			clash = clash or hex in free_colors
	check(not clash, "looks: a paid colour is never one of the free colours")
	# a locked tap opens the shop on the right product
	phone()
	var focus := []
	var cb := func(f): focus.append(f)
	Iap.feature_locked.connect(cb)
	Iap.locked("paint_luxury")
	Iap.feature_locked.disconnect(cb)
	check(focus == ["paint_luxury"] and not Iap.has("paint_luxury"), "looks: tapping a locked paint set asks for its product")
	# painting with an owned set is a plain paint change (saved as a hex string)
	GameManager.new_game()
	phone(["paint_street"])
	check(Iap.has("paint_street") and not Iap.has("paint_gold"), "looks: Street Paint Set unlocks only Street colours")
	VehicleManager.set_paint(VehicleManager.vehicles[0], "FF3D81")
	check(Validator.valid(SaveManager.snapshot()), "looks: a paid colour is a valid saved paint")
	phone()


# ---------------------------------------------------------------- W13: every sold look is really drawn

const WORLD_STUB := """extends Node
func parking_transform(_id) -> Transform3D:
	return Transform3D()
"""


## The paint colour a model is drawn with (the first mesh's paint shader).
func _paint_of(node: Node) -> Color:
	if node is MeshInstance3D and (node as MeshInstance3D).material_override is ShaderMaterial:
		return ((node as MeshInstance3D).material_override as ShaderMaterial).get_shader_parameter("paint_color")
	for c in node.get_children():
		var col := _paint_of(c)
		if col.a > 0.0:
			return col
	return Color(0, 0, 0, 0)


## W13 (2026-10-20): every look the shop sells changes what the game draws — the vehicle's paint material, the HQ sign and
## garage, the driver's face — unlike the free look and unlike the other looks, for every product that grants it.
func _sold_looks_are_drawn() -> void:
	var looks := ["paint_street", "paint_military", "paint_luxury", "paint_gold", "hq_theme", "driver_portraits"]
	var sold := {}
	for slug in IapCatalog.slugs():
		for g in IapCatalog.def(slug).get("grants", []):
			if str(g) in looks:
				sold[str(g)] = sold.get(str(g), []) + [slug]
	check(sold.size() == looks.size(), "W13: the six looks are on sale (%s)" % [sold.keys()])
	var granted := true
	for f in sold:
		granted = granted and sold[f].has("cosmetics_all") and sold[f].has("bundle")
		for slug in sold[f]:
			phone([slug])
			granted = granted and Iap.has(f)
	check(granted, "W13: every product that sells a look (single set, Kingpin, Collection, Bundle) grants it")
	# paints: the vehicle as the world draws it (VehicleActor.refresh after VehicleManager.set_paint)
	GameManager.new_game()
	var v: Dictionary = VehicleManager.vehicles[0]
	var free_paint := Data.color(str(v.paint))
	var gs := GDScript.new()
	gs.source_code = WORLD_STUB
	gs.reload()
	var stub: Node = gs.new()
	add_child(stub)
	var actor := VehicleActor.new()
	stub.add_child(actor)
	actor.setup(v, stub, false)
	check(_paint_of(actor.model).is_equal_approx(free_paint), "W13: the vehicle starts in its free paint")
	var fleet := load("res://scripts/ui/panels/fleet_sheet.gd")
	var seen := {}
	var drawn := true
	for s in fleet.PAINT_SETS:
		phone([s[0]])
		for hex in s[2]:
			VehicleManager.set_paint(v, str(hex))
			actor.refresh(v)
			var col := _paint_of(actor.model)
			drawn = drawn and col.is_equal_approx(Data.color(str(hex))) and not col.is_equal_approx(free_paint) and not seen.has(col.to_html(false))
			seen[col.to_html(false)] = s[0]
	check(drawn and seen.size() == 21, "W13: each of the 21 paid colours is drawn on the vehicle, none equal to the free paint or another (%d)" % seen.size())
	stub.queue_free()
	# HQ Signature: the HQ sign and garage as the world draws them (HQSite)
	phone()
	var keep_flags: Dictionary = ProgressionManager.flags.duplicate(true)
	var keep_sig = GameManager.settings.get("hq_signature", true)
	ProgressionManager.flags["company"] = "Test Freight"
	GameManager.settings["hq_signature"] = true
	var hq := HQSite.new()
	add_child(hq)
	hq.setup(self)
	var plain := [hq.sign_label.font_size, hq.sign_label.modulate, hq.sign_label.text]
	check(plain[0] == 48 and not str(plain[2]).contains("★"), "W13: without HQ Signature the HQ sign is the plain one")
	Iap._record("hq_theme", "t-hq-w13", "purchase")
	check(hq.sign_label.font_size == 62 and hq.sign_label.modulate != plain[1] and hq.sign_label.text.contains("★"), "W13: buying HQ Signature turns the sign into the gold one at once")
	var garage_free := _paint_of(hq.garage)
	var settings := load("res://scripts/ui/panels/settings_sheet.gd")
	var hq_seen := {}
	for hex in settings.HQ_COLORS:
		ProgressionManager.flags["company_color"] = str(hex)
		hq.set_company(ProgressionManager.company_name(), ProgressionManager.company_color())
		var gc := _paint_of(hq.garage)
		hq_seen[gc.to_html(false)] = true
		drawn = drawn and gc.is_equal_approx(Data.color(str(hex)))
	check(drawn and hq_seen.size() == 6 and not hq_seen.has(garage_free.to_html(false)), "W13: the six HQ Signature colours paint the garage, none equal to the default")
	GameManager.settings["hq_signature"] = false
	hq.refresh_sign()
	check(hq.sign_label.font_size == 48, "W13: switched off in Settings, the plain sign is back")
	hq.queue_free()
	ProgressionManager.flags = keep_flags
	GameManager.settings["hq_signature"] = keep_sig
	# Driver Portraits: the payroll card as the Fleet sheet builds it
	var d: Dictionary = VehicleManager.drivers[0] if not VehicleManager.drivers.is_empty() else VehicleManager.market[0]
	var faces := {}
	# The portrait is a Medium/High look (Sheet.low() hides it); pin the tier so a 4 GB CI box (auto Low) tests the same card.
	var keep_quality = GameManager.settings.get("quality", "high")
	GameManager.settings["quality"] = "high"
	for owned in [[], ["driver_portraits"]]:
		phone(owned)
		var sheet = fleet.new()
		var card: PanelContainer = sheet._driver_card(d, true)
		faces[str(owned)] = card.get_child(0).get_child(0).get_child(0)
		faces[str(owned) + "_card"] = card
		sheet.free()
	check(not (faces["[]"] is DriverPortrait) and faces["[\"driver_portraits\"]"] is DriverPortrait, "W13: Driver Portraits puts a drawn face on the driver card (the free card shows the icon)")
	GameManager.settings["quality"] = keep_quality
	faces["[]_card"].free()
	faces["[\"driver_portraits\"]_card"].free()
	phone()
	GameManager.new_game()


# ---------------------------------------------------------------- save + prestige

func _save_and_prestige() -> void:
	GameManager.new_game()
	phone(["automation_plus"])
	ProgressionManager.set_auto("crisis", false)
	EconomyManager.stats["ipm_ref"] = 1234.0
	var snap := SaveManager.snapshot()
	check(Validator.valid(snap), "save: the Crisis Manager switch and the income reference pass the save schema check")
	SaveManager.apply(snap)
	check(ProgressionManager.auto_toggles.get("crisis", true) == false and EconomyManager.stat("ipm_ref") == 1234.0, "save: both survive a reload")
	phone()
	# the income reference grows with steady income and fades
	GameManager.new_game()
	EconomyManager.stats["ipm_ref"] = 0.0
	for i in 400:
		EconomyManager.earn(100.0, "t", true)
		EconomyManager.tick(1.0)
	check(EconomyManager.stat("ipm_ref") > 1000.0, "economy: steady income raises the reference (%.0f / min)" % EconomyManager.stat("ipm_ref"))
	var r := EconomyManager.ref_income()
	EconomyManager._income_log.clear()
	check(EconomyManager.ref_income() >= r - 0.001, "economy: idling for three minutes does not zero the reference")
	GameManager.new_game()
	check(EconomyManager.ref_income() == EconomyManager.REF_FLOOR, "economy: a new empire has the floor reference")
	# prestige measures the finished run once and starts the new run from zero
	GameManager.new_game()
	EconomyManager.stats["ipm_ref"] = 9000.0
	EconomyManager.stats["earned"] = 5e9
	ProgressionManager.stage = 9
	ProgressionManager.apply_prestige()
	check(is_equal_approx(float(ProgressionManager.flags.get("last_run_ref", 0.0)), 9000.0), "prestige: the finished run's income is kept for the Legacy Head Start")
	check(EconomyManager.stat("ipm_ref") == 0.0, "prestige: the new run's packs are measured against the new run")
	GameManager.new_game()


# ---------------------------------------------------------------- the shop screen

func _shop_ui() -> void:
	GameManager.new_game()
	ProgressionManager.flags["intro_done"] = true
	phone()
	var langs := ["en", "tr", "de", "es", "fr", "ja", "ko", "pt", "ru", "zh"]
	for lang in langs:
		TranslationServer.set_locale(lang)
		var sheet: Sheet = load("res://scripts/ui/panels/shop_sheet.gd").new()
		sheet.header_extra = HBoxContainer.new()
		var body := VBoxContainer.new()
		add_child(body)
		sheet.build(body)
		var cards := body.find_children("prod_*", "PanelContainer", true, false)
		var expected := IapCatalog.slugs().filter(func(s): return Iap.available(s))
		check(cards.size() == expected.size() and cards.size() >= 16, "shop %s: %d product cards (legacy pack hidden before a prestige)" % [lang, cards.size()])
		var buttons_ok := true
		var seen_buy := 0
		for c in cards:
			var b: Button = c.get_node("buy") if c.has_node("buy") else c.find_child("buy", true, false)
			if b == null or b.text == "" or b.disabled:
				buttons_ok = false
			else:
				seen_buy += 1
		check(buttons_ok and seen_buy == cards.size(), "shop %s: every card has a price button" % lang)
		var restore := body.find_child("iap_restore", true, false)
		check(restore != null and str(restore.text) == IapText.t("restore"), "shop %s: Restore purchases is in the shop (%s)" % [lang, restore.text if restore else "-"])
		if lang != "en":
			var first: Label = body.find_child("prod_premium", true, false).find_children("*", "Label", true, false)[0]
			check(first.text == IapText.title("premium") and (lang not in ["ja", "ko", "zh", "ru"] or first.text != "Kingpin Edition"), "shop %s: product texts come in the player's language (%s)" % [lang, first.text])
		body.queue_free()
		sheet.free()
	TranslationServer.set_locale("en")
	# owned state
	phone(["premium"])
	var s2: Sheet = load("res://scripts/ui/panels/shop_sheet.gd").new()
	s2.header_extra = HBoxContainer.new()
	var b2 := VBoxContainer.new()
	add_child(b2)
	s2.build(b2)
	var owned_card: Control = b2.find_child("prod_premium", true, false)
	check(owned_card.find_child("buy", true, false).text == IapText.t("owned") and b2.find_child("prod_empire_pass", true, false).find_child("buy", true, false).disabled, "shop: Kingpin Edition shows Owned and covers Empire Pass")
	var worth: Array = b2.find_child("prod_cash_l", true, false).find_children("*", "Label", true, false).filter(func(l): return str(l.text).begins_with("Right now"))
	check(worth.size() == 1, "shop: a cash pack shows what it is worth right now")
	b2.queue_free()
	s2.free()
	phone()
	# restore reachable from Settings too
	var settings: Sheet = load("res://scripts/ui/panels/settings_sheet.gd").new()
	settings.header_extra = HBoxContainer.new()
	var b3 := VBoxContainer.new()
	add_child(b3)
	settings.build(b3)
	check(b3.find_child("settings_restore", true, false) != null and b3.find_child("settings_feedback", true, false) != null and b3.find_child("hq_signature", true, false) != null, "settings: Restore purchases, Send feedback and the HQ Signature card are there")
	b3.queue_free()
	settings.free()
	var biz: Sheet = load("res://scripts/ui/panels/business_sheet.gd").new()
	biz.header_extra = HBoxContainer.new()
	var b4 := VBoxContainer.new()
	add_child(b4)
	biz.build(b4)
	check(b4.find_child("auto_crisis", true, false) != null, "business: the Crisis Manager row is shown (locked without the product)")
	b4.queue_free()
	biz.free()
	await frames(2)
	# W18-2: in Russian the old 4 automation rows' "Locked" broke letter by letter → every locked automation button is the lock
	# icon only (word in the tooltip), and no button text in the sheet is wider than its button
	var keep_locale := TranslationServer.get_locale()
	TranslationServer.set_locale("ru")
	var keep_auto = ProgressionManager.business.get("automated_dispatch", 0)
	ProgressionManager.business["automated_dispatch"] = 0 # nothing researched: the four old rows are locked
	var biz_ru: Sheet = load("res://scripts/ui/panels/business_sheet.gd").new()
	biz_ru.header_extra = HBoxContainer.new()
	var b5 := VBoxContainer.new()
	b5.size = Vector2(1080, 1800)
	add_child(b5)
	biz_ru.build(b5)
	await frames(2)
	var locked := b5.find_children("Locked", "Button", true, false)
	var rows := 0
	for key in ProgressionManager.AUTO_KEYS + ["crisis"]:
		if b5.find_child("auto_" + key, true, false) != null:
			rows += 1
	check(rows >= 2 and locked.size() >= 2, "business ru: the automation rows are built (%d rows, %d locked)" % [rows, locked.size()])
	check(locked.all(func(b): return (b as Button).text == "" and (b as Button).tooltip_text != ""), "business ru: every locked automation button is the lock only, the word is in the tooltip")
	var too_wide := []
	for b in b5.find_children("*", "Button", true, false):
		var bt := b as Button
		if bt.text != "" and bt.is_visible_in_tree() and bt.get_theme_font("font").get_string_size(bt.text, HORIZONTAL_ALIGNMENT_LEFT, -1, bt.get_theme_font_size("font_size")).x > bt.size.x + 1:
			too_wide.append(bt.text)
	check(too_wide.is_empty(), "business ru: no button text is wider than its button %s" % str(too_wide))
	TranslationServer.set_locale(keep_locale)
	ProgressionManager.business["automated_dispatch"] = keep_auto
	b5.queue_free()
	biz_ru.free()
	await frames(2)


# ---------------------------------------------------------------- no trial, no subscription

func _no_trial_no_subscription() -> void:
	var bad := RegEx.create_from_string("(?i)free[ -]?trial|try (it )?free|trial period|\\btrial\\b|subscri|ücretsiz dene|deneme sürüm|abonelik|abo\\b|prueba gratis|essai gratuit|無料トライアル|무료 체험|免费试用|пробн")
	var hits := []
	for slug in IapCatalog.slugs():
		for l in IapText.LANGS:
			for f in ["title", "desc"]:
				if bad.search(str(IapCatalog.def(slug).get(f, {}).get(l, ""))) != null:
					hits.append("%s.%s.%s" % [slug, f, l])
	check(hits.is_empty(), "policy: no product text promises a trial or a subscription %s" % str(hits))
	var gp := FileAccess.get_file_as_string("res://data/iap.json")
	check(not gp.contains('"subscription"') and not gp.contains("introductory") and not gp.contains('"trial'), "policy: the catalog has no subscription or trial product")
	check(IapText.t("shop_intro").length() > 20, "policy: the shop says the whole game is free")
