## coddy_iap — autoload `Iap`: every in-app product of the game (data/iap.json). The whole game is open from the start;
## players may buy permanent upgrades and repeatable packs. What is owned is cached in user:// so the game plays offline.
## Ad-free entitlement is separate from purchase history; Ads reads it through Perk.ad_free().
## Desktop, editor, headless and tests have every feature and no store. Debug-only flags: `--paywall` (desktop behaves
## like a phone with a fake store), `--fakeiap` (fake purchases succeed), `--unlocked` (everything owned),
## `--owns=slug,slug` (those products owned).
##
## BORDERLINE EMPIRE VARIANT of games/tools/shop/addons/coddy_iap/IapStore.gd: the template sells through godot-iap; this
## game already ships the RevenueCat plugin (godotx_revenue_cat 2.4.0, the engine of its live 1.1.0 Empire Pass), so the
## store backend talks to the `GodotxRevenueCat` singleton instead (initialize / fetch_products / purchase /
## restore_purchases / check_entitlement). Everything else (catalog, gate, offers, texts) is the template unchanged.
## RevenueCat side (store-automation/iap_rc_catalog.ts): one product per catalog entry, permanent products attached to an
## entitlement whose lookup key = the slug, consumables (Play `one_time`) attached to none. A purchase_result without an
## error grants the product (RevenueCat has validated the receipt); a restore sweeps `check_entitlement(<slug>)` for every
## permanent product. A negative answer or a network error NEVER removes something the player already has.
extends Node

signal entitlements_changed
signal effects_pending # a bought pack waits to be delivered into the running game (IapDirector → the game's deliver())
signal notice(text: String, tone: String) # "good" / "warn" / "bad": show it as a toast
signal prices_changed
signal feature_locked(feature: String) # the player tried a paid feature: the shop opens on its product
signal _purchase_done(ok: bool)
signal _restore_done

const CACHE := "user://iap_entitlement.json"
const SINGLETON := "GodotxRevenueCat"
const BUY_TIMEOUT_S := 120.0
const SWEEP_TIMEOUT_S := 20.0

var sells := false # this build sells products (phone, or --paywall in a debug build)
var fake := false # debug: purchases are simulated
var connected := false
var busy := false
var persist := true # false in tests: no cache file is written
## Optional game rule: Callable(slug) -> bool. A product the game does not want to show right now (Legacy Head Start before
## the first prestige) returns false and is hidden from the shop.
var visible_rule: Callable = Callable()
var _st: Dictionary = IapGate.empty_state()
var _all := false
var _prices := {} # slug → displayPrice
var _price_values := {} # slug → {price, currency}
var _fetched := false # the store answered (a product it did not list is not for sale yet)
var _rc: Object
var _buying := ""
var _os := ""
var _buy_left := 0.0
var _sweeping := {} # slug → true while its check_entitlement answer is awaited
var _sweep_left := 0.0
var _sweep_announce := false
var _sweep_found := false
var _last_refresh := -1000.0


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_os = OS.get_name()
	sells = IapGate.store_platform(_os)
	var args := OS.get_cmdline_user_args()
	if OS.is_debug_build():
		if "--paywall" in args and not sells:
			sells = true
			fake = true
		if "--fakeiap" in args:
			fake = true
	if FileAccess.file_exists(CACHE):
		_st = IapGate.decode(FileAccess.get_file_as_string(CACHE))
	if sells:
		_save_state("ad_free_migration") # persist before SDK callbacks can record a new cheap purchase
	if OS.is_debug_build():
		if "--unlocked" in args:
			_all = true
		for a in args:
			if a.begins_with("--owns="):
				for slug in a.substr(7).split(","):
					if IapCatalog.has(slug):
						_st.owned[slug] = true
						_st.any = true
	if not sells or fake:
		return
	var key := IapGate.rc_key(IapCatalog.data().get("revenuecat", {}), _os)
	if key == "" or not Engine.has_singleton(SINGLETON):
		return
	_rc = Engine.get_singleton(SINGLETON)
	_rc.connect("products", _on_rc_products)
	_rc.connect("purchase_result", _on_rc_purchase_result)
	_rc.connect("entitlement", _on_rc_entitlement)
	_rc.connect("restore_finished", _on_rc_restore_finished)
	_rc.connect("customer_info", _on_rc_customer_info)
	if _rc.has_signal("customer_info_changed"):
		_rc.connect("customer_info_changed", _on_rc_customer_info) # godotx_revenue_cat W23 build: pushed by the SDK
	_rc.call("initialize", key, "", OS.is_debug_build())
	connected = true
	_fetch_products()
	refresh_owned(false)


func _process(delta: float) -> void:
	if busy and _buying != "" and _buy_left > 0.0:
		_buy_left -= delta
		if _buy_left <= 0.0:
			# A purchase can still complete later (the entitlement sweep on resume grants permanent products, the transaction
			# list delivers a consumable once — W23); never grant speculatively.
			_await_pending(_buying)
			notice.emit(IapText.t("pending"), "warn")
			_purchase_done.emit(false)
	if _sweep_left > 0.0:
		_sweep_left -= delta
		if _sweep_left <= 0.0:
			_finish_sweep()


func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_RESUMED and _rc != null:
		if not _fetched:
			_fetch_products()
		if Time.get_ticks_msec() / 1000.0 - _last_refresh > 30.0:
			_rc.call("get_customer_info") # a pending consumable completed while away → customer_info → delivered once
			refresh_owned(false)


# ------------------------------------------------------------------ queries

## Does the player have this feature? Desktop builds have all.
func has(feature: String) -> bool:
	return _all or not sells or IapGate.has(_st, feature)


func owns(slug: String) -> bool:
	return _all or IapGate.owns(_st, slug) or (not sells and not IapCatalog.is_consumable(slug) and IapCatalog.has(slug))


## Owned, or everything it grants is already owned (IapGate.covered): the shop shows "Owned", offers skip it.
func covered(slug: String) -> bool:
	return owns(slug) or (sells and IapGate.covered(_st, slug))


## Purchase history for restore flows; never use it as an ad entitlement.
func purchased_any() -> bool:
	return _all or bool(_st.get("any", false))


func ad_free() -> bool:
	return sells and (_all or IapGate.ad_free(_st))


## Save slots a new game may use (-1 = the game has no slot products). Desktop: the catalog's max.
func slot_cap() -> int:
	var s := IapCatalog.slots()
	if s.is_empty():
		return -1
	return int(s.get("max", 99)) if (_all or not sells) else IapGate.slot_cap(_st)


## Permanent products owned or covered (IapOffers / the shop screen).
func owned() -> Dictionary:
	var o := {}
	for slug in IapCatalog.slugs():
		if not IapCatalog.is_consumable(slug) and covered(slug) and sells:
			o[slug] = true
	return o


## The price as the store formats it ("₺199,99"), "" until the store answered.
func price_text(slug: String) -> String:
	var p := String(_prices.get(slug, ""))
	if p == "" and fake and IapCatalog.has(slug):
		return "$%.2f (test)" % float(IapCatalog.def(slug).get("usd", 0.0))
	return p


## For sale right now? Before the store answered everything shows (price pending); afterwards only what the store lists,
## so a product that is not live in the store yet never appears as a dead button.
func available(slug: String) -> bool:
	if visible_rule.is_valid() and not bool(visible_rule.call(slug)):
		return false
	if fake or not _fetched:
		return IapCatalog.has(slug)
	return _prices.has(slug)


## The player tapped a paid feature: the UI (IapDirector) opens the shop on its product.
func locked(feature: String) -> void:
	feature_locked.emit(feature)


# ------------------------------------------------------------------ pending effects (packs, one-time offers)

func pending_effects() -> Array:
	return _st.pending.duplicate(true)


## The game delivered this pack: forget it (saved at once, so a crash can't deliver it twice).
func effect_delivered(id: String) -> void:
	IapGate.mark_delivered(_st, id)
	_save_state("delivered")


## A purchase made before the shop existed (1.1.0's Empire Pass kept in monetization.json): own it, no effects.
func grant_legacy(slug: String) -> void:
	if IapCatalog.has(slug) and not IapCatalog.is_consumable(slug):
		_record(slug, "", "legacy")


# ------------------------------------------------------------------ store connection (RevenueCat)

func _fetch_products() -> void:
	if _rc == null:
		return
	var ids: Array = []
	for pid in IapCatalog.product_ids():
		ids.append(pid)
	_rc.call("fetch_products", ids)


func _on_rc_products(data: Dictionary) -> void:
	if not str(data.get("error", "")).is_empty():
		return # stay "price pending"; retried on resume
	var products: Variant = data.get("products", [])
	var items: Array = []
	if products is Array:
		items = products
	elif products is Object and products.get_class() == "JavaObject":
		for i in range(int(products.call("size"))):
			items.append(products.call("get", i))
	for p in items:
		if typeof(p) != TYPE_DICTIONARY:
			continue
		var slug := IapCatalog.slug_of(str(p.get("id", "")).get_slice(":", 0))
		if slug == "":
			continue
		var price := str(p.get("price", ""))
		if price == "":
			continue
		_prices[slug] = price
		_price_values[slug] = {"price": float(p.get("amount", 0.0)), "currency": str(p.get("currency", ""))}
	_fetched = true
	prices_changed.emit()


## Ask RevenueCat which permanent products this store account owns (entitlement lookup key = the slug). Never removes
## a cached entitlement because of an error (offline play).
func refresh_owned(announce: bool) -> void:
	if _rc == null:
		return
	_last_refresh = Time.get_ticks_msec() / 1000.0
	_sweeping.clear()
	for slug in IapCatalog.slugs():
		if not IapCatalog.is_consumable(slug):
			_sweeping[slug] = true
	_sweep_announce = announce
	_sweep_found = false
	_sweep_left = SWEEP_TIMEOUT_S
	for slug in _sweeping.keys():
		_rc.call("check_entitlement", slug)


func _on_rc_entitlement(id: String, active: bool) -> void:
	if not IapCatalog.has(id) or IapCatalog.is_consumable(id):
		return
	if active:
		_sweep_found = true
		_record(id, "", "restore")
	if _sweeping.has(id):
		_sweeping.erase(id)
		if _sweeping.is_empty():
			_finish_sweep()


func _finish_sweep() -> void:
	_sweeping.clear()
	_sweep_left = 0.0
	if _sweep_announce:
		_sweep_announce = false
		var any := _sweep_found or purchased_any()
		notice.emit(IapText.t("restored") if any else IapText.t("nothing_restored"), "good" if any else "warn")
		_restore_done.emit()


# ------------------------------------------------------------------ buy / restore

## Buy a product. True when bought (a permanent product: when it is owned afterwards).
func purchase(slug: String) -> bool:
	if not IapCatalog.has(slug):
		return false
	if not IapCatalog.is_consumable(slug) and covered(slug):
		return true
	if busy:
		return false
	if fake:
		busy = true
		await get_tree().create_timer(0.3).timeout
		busy = false
		_record(slug, "fake:%d" % Time.get_ticks_usec(), "fake")
		notice.emit(IapText.t("thanks"), "good")
		return true
	if _rc == null or not _prices.has(slug):
		notice.emit(IapText.t("store_closed"), "bad")
		if _rc != null:
			_fetch_products()
		return false
	busy = true
	_buying = slug
	_buy_left = BUY_TIMEOUT_S
	_rc.call("purchase", IapCatalog.product_id(slug))
	var ok: bool = await _purchase_done
	_buying = ""
	_buy_left = 0.0
	busy = false
	return ok


## Restore purchases (reachable from the shop AND from Settings).
func restore() -> bool:
	if busy:
		return false
	busy = true
	var ok := false
	if _rc != null:
		_rc.call("restore_purchases")
		await _restore_done
		ok = purchased_any()
	else:
		if fake:
			await get_tree().create_timer(0.2).timeout
		var any := purchased_any()
		notice.emit(IapText.t("restored") if any else IapText.t("nothing_restored" if fake else "store_closed"), "good" if any else "warn")
		ok = any
	busy = false
	return ok


func _on_rc_restore_finished(data: Dictionary) -> void:
	if not bool(data.get("success", false)):
		notice.emit(IapText.t("unreachable"), "bad")
		_restore_done.emit()
		return
	refresh_owned(true) # emits _restore_done after the sweep


func _on_rc_purchase_result(data: Dictionary) -> void:
	var slug := IapCatalog.slug_of(str(data.get("product_id", "")).get_slice(":", 0))
	if slug == "":
		slug = _buying
	var err := str(data.get("error", ""))
	if bool(data.get("cancelled", false)):
		_purchase_done.emit(false)
		return
	if err != "":
		var low := err.to_lower()
		if low.contains("pending") or low.contains("deferred") or low.contains("ask to buy"):
			_await_pending(slug)
			notice.emit(IapText.t("pending"), "warn")
		elif (low.contains("already") or low.contains("owned")) and slug != "" and not IapCatalog.is_consumable(slug):
			_record(slug, "", "owned") # the store account already owns it: recover it
			_purchase_done.emit(true)
			return
		else:
			notice.emit(IapText.t("failed"), "bad")
		_purchase_done.emit(false)
		return
	if slug == "":
		_purchase_done.emit(false)
		return
	if IapCatalog.is_consumable(slug) and IapGate.settle_await(_st, slug):
		_save_state("settled") # a late success of a pending purchase: the transaction list must not deliver it again
	_grant(slug, str(data.get("transaction_id", "")))
	notice.emit(IapText.t("thanks"), "good")
	_purchase_done.emit(true)
	if not IapCatalog.is_consumable(slug):
		refresh_owned(false)


## W23: a consumable went pending (Ask to buy, slow card): keep a claim so its later completion is delivered exactly once.
func _await_pending(slug: String) -> void:
	if slug != "" and IapGate.await_pending(_st, slug, _now_ms()):
		_save_state("pending")


## customer_info / customer_info_changed: permanent products → entitlement sweep; consumables that completed after being
## pending → delivered once (IapGate.reconcile_transactions). Older plugin builds send no transaction list: nothing changes.
func _on_rc_customer_info(info) -> void:
	refresh_owned(false)
	if typeof(info) != TYPE_DICTIONARY or not info.has("non_subscription_transactions"):
		return
	var txs = info.non_subscription_transactions
	if typeof(txs) != TYPE_ARRAY:
		return
	var had_base := bool(_st.get("rc_base", false))
	var delivered := IapGate.reconcile_transactions(_st, txs, _now_ms())
	if not delivered.is_empty() or not had_base:
		_save_state("transactions")
	if not delivered.is_empty():
		entitlements_changed.emit()
		effects_pending.emit()
		notice.emit(IapText.t("thanks"), "good")


func _now_ms() -> int:
	return int(Time.get_unix_time_from_system() * 1000.0)


## Persist first (a crash afterwards cannot lose or repeat it: the token hash dedupes a consumable).
func _grant(slug: String, token: String) -> void:
	_record(slug, token, "purchase")


func _record(slug: String, token: String, source: String) -> void:
	var r := IapGate.record(_st, slug, IapGate.token_id(token, slug) if token != "" else "")
	if bool(r.changed):
		_save_state(source)
		entitlements_changed.emit()
		if bool(r.queued):
			effects_pending.emit()


func _save_state(source: String) -> void:
	if not persist:
		return
	var f := FileAccess.open(CACHE, FileAccess.WRITE)
	if f != null:
		f.store_string(IapGate.encode(_st, source, int(Time.get_unix_time_from_system())))
		f.close()


# ------------------------------------------------------------------ test hooks

## Tests: behave like a phone build (fake store) with this state, no cache file.
func _test_setup(state: Dictionary, sells_v: bool = true, fake_v: bool = true) -> void:
	_st = state
	sells = sells_v
	fake = fake_v
	persist = false
	_all = false
	_fetched = false
	_prices = {}
	_buying = ""
	busy = false
