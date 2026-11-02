extends Node
## API sanity (2026-10-23): EVERY catalog product through the fake RevenueCat store: buy -> entitlement really held ->
## restart (cache encode/decode) -> restore -> still owned; consumables delivered once (restart/restore never repeat);
## ad_free only for games/CLAUDE.md §8 products; bundles/Kingpin deliver every part. Complements iap_test.gd (looks drawn).
## Run: godot --headless --path . res://tests/iap_matrix_test.tscn   (last line: IAPMATRIX checks=N failures=0)
const PREFIX := "com.coddykit.borderlineempire."
const AD_FREE := ["noads", "premium", "bundle", "empire_pass"]

var failures := 0
var checks := 0


class FakeRc extends Object:
	var iap: Node
	var entitlements := {}
	var n := 1

	func purchase(product_id: String) -> void:
		var d := {"product_id": product_id, "transaction_id": "m-%d" % n}
		n += 1
		iap.get_tree().process_frame.connect(func(): iap._on_rc_purchase_result(d), CONNECT_ONE_SHOT)

	func restore_purchases() -> void:
		iap.get_tree().process_frame.connect(func(): iap._on_rc_restore_finished({"success": true}), CONNECT_ONE_SHOT)

	func check_entitlement(id: String) -> void:
		iap.get_tree().process_frame.connect(func(): iap._on_rc_entitlement(id, bool(entitlements.get(id, false))), CONNECT_ONE_SHOT)

	func fetch_products(_ids: Array) -> void:
		pass

	func get_customer_info() -> void:
		pass


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
	print("IAPMATRIX ", "PASS " if ok else "FAIL ", label)


func _rc_phone() -> FakeRc:
	Iap._test_setup(IapGate.empty_state(), true, true)
	var rc := FakeRc.new()
	rc.iap = Iap
	Iap._rc = rc
	Iap._prices = {}
	Iap._fetched = true
	Iap.fake = false
	for slug in IapCatalog.slugs():
		Iap._prices[slug] = "$%.2f" % float(IapCatalog.def(slug).usd)
	return rc


func _restart() -> void:
	Iap._st = IapGate.decode(IapGate.encode(Iap._st, "test", 0))


func _ready() -> void:
	await get_tree().process_frame
	SaveManager.delete_save()
	IapCatalog.load_from()
	GameManager.new_game()
	for slug in IapCatalog.slugs():
		if IapCatalog.is_consumable(slug):
			await _consumable(slug)
		else:
			await _permanent(slug)
	Iap._rc = null
	Iap._test_setup(IapGate.empty_state(), true, true)
	SaveManager.delete_save()
	print("IAPMATRIX checks=", checks, " failures=", failures)
	await get_tree().create_timer(0.2).timeout
	get_tree().quit(1 if failures else 0)


func _permanent(slug: String) -> void:
	var rc := _rc_phone()
	check(not Iap.owns(slug), "%s: not owned on a fresh phone" % slug)
	var ok: bool = await Iap.purchase(slug)
	# a permanent with a one-off pack (starter, bundle) queues that pack exactly once per account
	var has_fx: bool = IapCatalog.effect(slug) != "" and IapCatalog.once(slug)
	check(ok and Iap.owns(slug) and Iap.pending_effects().size() == (1 if has_fx else 0), "%s: buy -> owned, %s queued" % [slug, "one pack" if has_fx else "nothing"])
	var feats: Array = IapCatalog.grants(slug)
	var all_f := true
	for f in feats:
		all_f = all_f and Iap.has(str(f))
	check(all_f and (not feats.is_empty() or has_fx), "%s: every granted feature is really held (%s)" % [slug, feats])
	for p in IapCatalog.bundle_parts(slug):
		var pf: Array = IapCatalog.grants(str(p))
		var held := true
		for f in pf:
			held = held and Iap.has(str(f))
		check(held and (not pf.is_empty() or str(p) == "starter" and has_fx), "%s: bundle part %s delivered (features held / pack queued)" % [slug, p])
	if has_fx:
		Iap.effect_delivered(str(Iap.pending_effects()[0].id))
	check(Iap.ad_free() == (slug in AD_FREE), "%s: ad_free == %s (§8)" % [slug, slug in AD_FREE])
	check(bool(Iap._st.legacy_ad_free) == false, "%s: legacy_ad_free stays false" % slug)
	_restart()
	check(Iap.owns(slug) and Iap.ad_free() == (slug in AD_FREE), "%s: survives a restart" % slug)
	# fresh install on the same account: the store entitlement brings it back
	var keep := Iap._st
	Iap._test_setup(IapGate.empty_state(), true, true)
	Iap._rc = rc
	rc.entitlements = {slug: true}
	var r: bool = await Iap.restore()
	check(r and Iap.owns(slug) and Iap.ad_free() == (slug in AD_FREE) and not bool(Iap._st.legacy_ad_free), "%s: restore on a new install -> owned, ad_free unchanged" % slug)
	# By design a fresh install cannot know the pack was already delivered (a pending permanent purchase also arrives
	# through this path, so skipping it would lose a paid pack): at most ONE pack, and a second restore adds nothing.
	check(Iap.pending_effects().size() == (1 if has_fx else 0), "%s: restore on a new install queues %s" % [slug, "the one-off pack once" if has_fx else "no pack"])
	var r2: bool = await Iap.restore()
	check(r2 and Iap.pending_effects().size() == (1 if has_fx else 0), "%s: a second restore adds nothing" % slug)
	rc.free()
	Iap._rc = null
	keep = {}


func _consumable(slug: String) -> void:
	var rc := _rc_phone()
	var fx: String = IapCatalog.effect(slug)
	var ok: bool = await Iap.purchase(slug)
	var pend := Iap.pending_effects()
	check(ok and pend.size() == 1 and str(pend[0].slug) == slug, "%s: buy -> exactly one pending delivery" % slug)
	check(not Iap.ad_free() and not Iap.owns(slug), "%s: consumable is never an ownership and never ad_free" % slug)
	# deliver into the running game (IapDirector's hook): cash really arrives
	var cash_before: float = EconomyManager.cash
	var deliverable := true
	if not GameManager.running or not ProgressionManager.intro_done():
		deliverable = false
	if deliverable:
		var d: bool = Monetization.deliver(slug, fx)
		check(d and EconomyManager.cash > cash_before, "%s: delivery adds cash (%s)" % [slug, fx])
	else:
		check(Monetization.pack_cash(fx) > 0.0 or fx == "starter", "%s: pack value > 0 (game not in a state to deliver headless)" % slug)
	Iap.effect_delivered(str(pend[0].id))
	check(Iap.pending_effects().is_empty(), "%s: delivered pack is forgotten" % slug)
	_restart()
	check(Iap.pending_effects().is_empty(), "%s: restart does not repeat it" % slug)
	rc.entitlements = {slug: true}
	var r: bool = await Iap.restore()
	check(Iap.pending_effects().is_empty(), "%s: restore does not repeat it (restore=%s)" % [slug, r])
	# the same transaction replayed (double callback) does nothing
	var before := Iap.pending_effects().size()
	Iap._on_rc_purchase_result({"product_id": PREFIX + slug, "transaction_id": "m-1"})
	Iap._on_rc_purchase_result({"product_id": PREFIX + slug, "transaction_id": "m-1"})
	check(Iap.pending_effects().size() <= before + 1, "%s: a replayed transaction adds at most one" % slug)
	rc.free()
	Iap._rc = null
