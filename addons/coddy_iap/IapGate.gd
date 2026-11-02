## coddy_iap — pure entitlement logic (no Node, no network): what is owned, which one-off effects still wait to be
## delivered, store purchase dictionaries → slugs, RevenueCat REST payloads. IapStore (autoload `Iap`) does the talking;
## tests call this directly. Derived from Corrupt State's StoreGate.gd (2026-10-18), catalog-agnostic.
class_name IapGate
extends RefCounted

const STORE_PLATFORMS := ["iOS", "Android"]
const SEEN_MAX := 40
const RC_KNOWN_MAX := 200
const AWAIT_MAX_MS := 14 * 24 * 3600 * 1000 # Play cancels a pending purchase after ~3 days; keep the claim two weeks
const AWAIT_SLACK_MS := 10 * 60 * 1000 # store clocks: a completed transaction may be stamped a little before our "pending" moment
## Features that mean "no forced ads" (games use `no_ads` or `noads`; a game with another key puts `"ad_free_features": [...]` in
## data/iap.json). A product is ad-free when its `grants` list one of them (Remove Ads and the big packs that include it).
const AD_FREE_FEATURES := ["no_ads", "noads"]


## Only phone builds sell anything; desktop, the editor, headless runs and tests have every feature.
static func store_platform(os_name: String) -> bool:
	return os_name in STORE_PLATFORMS


# ------------------------------------------------------------------ entitlement state (offline play)
## {"v": 1, "owned": {slug: true}, "any": bool (purchase history; separate from legacy_ad_free), "pending": [{slug, id}]
## (effects bought but not delivered yet), "seen": [short token hashes already processed],
## "delivered": {slug: true} (one-off effects of permanent products, delivered once per account),
## "await": [{slug, at}] (consumables whose purchase went PENDING — Ask to buy, slow card — at `at` ms; W23),
## "rc_known": [short hashes of RevenueCat non-subscription transactions already looked at], "rc_base": bool (the first
## transaction list was taken as the baseline: what existed before is never delivered again)}

static func empty_state() -> Dictionary:
	return {"v": 1, "owned": {}, "any": false, "ad_free_migrated": true, "legacy_ad_free": false, "pending": [], "seen": [], "delivered": {}, "await": [], "rc_known": [], "rc_base": false}


static func encode(st: Dictionary, source: String = "", when: int = 0) -> String:
	var d := st.duplicate(true)
	d["v"] = 1
	d["source"] = source
	d["at"] = when
	return JSON.stringify(d)


## Reads the cache file text. A broken or foreign file never grants anything. Unknown slugs are dropped.
static func decode(text: String) -> Dictionary:
	var st := empty_state()
	var j := JSON.new()
	if text == "" or j.parse(text) != OK:
		return st
	var d = j.data
	if typeof(d) != TYPE_DICTIONARY:
		return st
	var owned = d.get("owned", {})
	if typeof(owned) == TYPE_DICTIONARY:
		for k in owned:
			if IapCatalog.has(String(k)) and bool(owned[k]):
				st.owned[String(k)] = true
	st.any = bool(d.get("any", false)) or not st.owned.is_empty()
	# Snapshot old buyers once. New states stamp the marker before the first purchase.
	st.legacy_ad_free = bool(d.get("legacy_ad_free", false)) or (not bool(d.get("ad_free_migrated", false)) and st.any)
	var pend = d.get("pending", [])
	if typeof(pend) == TYPE_ARRAY:
		for q in pend:
			if typeof(q) == TYPE_DICTIONARY and IapCatalog.has(String(q.get("slug", ""))):
				st.pending.append({"slug": String(q.slug), "id": String(q.get("id", ""))})
	var seen = d.get("seen", [])
	if typeof(seen) == TYPE_ARRAY:
		for t in seen:
			st.seen.append(String(t))
	var dl = d.get("delivered", {})
	if typeof(dl) == TYPE_DICTIONARY:
		for k in dl:
			if IapCatalog.has(String(k)):
				st.delivered[String(k)] = bool(dl[k])
	var aw = d.get("await", [])
	if typeof(aw) == TYPE_ARRAY:
		for q in aw:
			if typeof(q) == TYPE_DICTIONARY and IapCatalog.is_consumable(String(q.get("slug", ""))):
				st.await.append({"slug": String(q.slug), "at": int(q.get("at", 0))})
	var kn = d.get("rc_known", [])
	if typeof(kn) == TYPE_ARRAY:
		for t in kn:
			st.rc_known.append(String(t))
	st.rc_base = bool(d.get("rc_base", false))
	return st


static func owns(st: Dictionary, slug: String) -> bool:
	return bool(st.get("owned", {}).get(slug, false))


## A permanent product the player effectively has: owned, or every feature it grants already comes with what they own
## (Premium includes the gold theme → the theme alone is not sold to them again). Products with a one-off effect only
## count when owned.
static func covered(st: Dictionary, slug: String) -> bool:
	if owns(st, slug):
		return true
	if IapCatalog.is_consumable(slug) or IapCatalog.effect(slug) != "":
		return false
	var g := IapCatalog.grants(slug)
	if g.is_empty():
		return false
	for f in g:
		if not has(st, String(f)):
			return false
	return true


## Does any owned product grant this feature?
static func has(st: Dictionary, feature: String) -> bool:
	for slug in st.get("owned", {}):
		if feature in IapCatalog.grants(String(slug)):
			return true
	return false


## Features that count as "no ads" (data/iap.json `ad_free_features`, else AD_FREE_FEATURES).
static func ad_free_features() -> Array:
	var f = IapCatalog.data().get("ad_free_features", AD_FREE_FEATURES)
	return f if typeof(f) == TYPE_ARRAY and not f.is_empty() else AD_FREE_FEATURES


## Does this product end forced ads (its grants include a no-ads feature)?
static func grants_ad_free(slug: String) -> bool:
	for f in ad_free_features():
		if String(f) in IapCatalog.grants(slug):
			return true
	return false


## Approved products grant the no-ads feature; existing buyers keep the pre-policy promise (legacy_ad_free).
static func ad_free(st: Dictionary) -> bool:
	if bool(st.get("legacy_ad_free", false)):
		return true
	for f in ad_free_features():
		if has(st, String(f)):
			return true
	return false


## Save slots a new game may use: base + per owned feature, capped (IapCatalog.slots()). -1 = the game has no slot rule.
static func slot_cap(st: Dictionary) -> int:
	var s := IapCatalog.slots()
	if s.is_empty():
		return -1
	var n := int(s.get("base", 1))
	var per: Dictionary = s.get("per_feature", {})
	for f in per:
		if has(st, String(f)):
			n += int(per[f])
	return mini(n, int(s.get("max", n)))


## A short stable hash of a store token (dedupes restores and replayed transactions).
static func token_id(token: String, slug: String) -> String:
	return (slug + ":" + token).sha256_text().substr(0, 16)


## Record a purchase / restore of `slug` (`tid` = token_id, or "" when the store gave no token). Returns
## {"changed", "new_owner" (a permanent product became owned now), "queued" (a one-off effect was queued)}.
## Consumables are queued once per token; permanent products with `fx` + `once` once per account.
static func record(st: Dictionary, slug: String, tid: String) -> Dictionary:
	var out := {"changed": false, "new_owner": false, "queued": false}
	if not IapCatalog.has(slug):
		return out
	if not bool(st.get("any", false)):
		st.any = true
		out.changed = true
	if IapCatalog.is_consumable(slug):
		if tid != "" and tid in st.seen:
			return out
		if tid != "":
			st.seen.append(tid)
			while st.seen.size() > SEEN_MAX:
				st.seen.pop_front()
		st.pending.append({"slug": slug, "id": tid if tid != "" else "t%d_%d" % [st.pending.size(), Time.get_ticks_usec()]})
		out.changed = true
		out.queued = true
		return out
	if not owns(st, slug):
		st.owned[slug] = true
		out.new_owner = true
		out.changed = true
	if IapCatalog.effect(slug) != "" and IapCatalog.once(slug) and not bool(st.delivered.get(slug, false)):
		st.delivered[slug] = true
		st.pending.append({"slug": slug, "id": slug})
		out.queued = true
		out.changed = true
	return out


## An effect was delivered into the game: forget it.
static func mark_delivered(st: Dictionary, id: String) -> void:
	for i in st.pending.size():
		if String(st.pending[i].id) == id:
			st.pending.remove_at(i)
			return


# ------------------------------------------------------------------ pending consumables completed later (W23)
## A consumable purchase went PENDING (no product yet, money not taken yet): remember the claim, without an id.
static func await_pending(st: Dictionary, slug: String, now_ms: int) -> bool:
	if not IapCatalog.is_consumable(slug):
		return false
	st.await.append({"slug": slug, "at": now_ms})
	return true


## A purchase_result success for `slug` arrived (possibly late, after the pending notice): it settles the oldest claim of
## that product, so the same purchase cannot be delivered again from the transaction list.
static func settle_await(st: Dictionary, slug: String) -> bool:
	for i in st.await.size():
		if String(st.await[i].slug) == slug:
			st.await.remove_at(i)
			return true
	return false


## RevenueCat's customer_info.non_subscription_transactions ([{product_id, transaction_id, purchase_date ms}]) arrived.
## A transaction is delivered ONLY when it settles an open pending claim of the same product made no later than the
## transaction (+ slack) — so the number of deliveries can never exceed the number of pending purchases, whatever ids the
## store uses, and a normal purchase (already delivered by purchase_result, no claim) is only marked as known.
## The first list ever (fresh install, update, damaged cache) is the baseline: everything in it is marked known, except
## what settles a claim. Returns the slugs to deliver now (already queued in st.pending through record()).
static func reconcile_transactions(st: Dictionary, txs: Array, now_ms: int) -> Array:
	var out: Array = []
	st.await = st.await.filter(func(q): return now_ms - int(q.at) <= AWAIT_MAX_MS)
	var rows: Array = []
	for t in txs:
		if typeof(t) != TYPE_DICTIONARY:
			continue
		var slug := IapCatalog.slug_of(String(t.get("product_id", "")).get_slice(":", 0))
		var tx := String(t.get("transaction_id", ""))
		if slug == "" or tx == "" or not IapCatalog.is_consumable(slug):
			continue
		rows.append({"slug": slug, "kid": ("rc:" + slug + ":" + tx).sha256_text().substr(0, 16), "at": int(t.get("purchase_date", 0))})
	rows.sort_custom(func(a, b): return int(a.at) < int(b.at))
	for r in rows:
		if r.kid in st.rc_known:
			continue
		st.rc_known.append(r.kid)
		for i in st.await.size():
			var q: Dictionary = st.await[i]
			if String(q.slug) == r.slug and int(q.at) <= int(r.at) + AWAIT_SLACK_MS:
				st.await.remove_at(i)
				record(st, r.slug, r.kid)
				out.append(r.slug)
				break
	while st.rc_known.size() > RC_KNOWN_MAX:
		st.rc_known.pop_front()
	st.rc_base = true
	return out


# ------------------------------------------------------------------ store purchase dictionaries (godot-iap / OpenIAP)

## The slug a purchase grants: "" for pending (Ask to Buy, slow card), failed, or foreign products.
static func purchase_slug(p: Dictionary) -> String:
	var slug := IapCatalog.slug_of(String(p.get("productId", p.get("id", ""))))
	if slug == "":
		return ""
	var state := str(p.get("purchaseState", p.get("purchaseStateAndroid", "purchased"))).to_lower()
	if state.contains("pending") or state == "2" or state.contains("fail") or state.contains("unknown"):
		return ""
	return slug


## The token RevenueCat validates: the purchase token on Android, the StoreKit 2 JWS on iOS.
static func purchase_token(p: Dictionary) -> String:
	for k in ["purchaseToken", "jwsRepresentationIOS", "jwsRepresentation", "purchaseTokenAndroid"]:
		if p.get(k) is String and String(p[k]) != "":
			return String(p[k])
	return ""


# ------------------------------------------------------------------ RevenueCat REST (v1), best effort

## Public SDK key for the platform ("" while a placeholder or when it looks like a secret key).
static func rc_key(cfg: Dictionary, os_name: String) -> String:
	var k := String(cfg.get("ios_key" if os_name == "iOS" else "android_key", ""))
	var want := "appl_" if os_name == "iOS" else "goog_"
	if not k.begins_with(want) or k.contains("REPLACE") or k.length() < 12:
		return ""
	return k


static func new_app_user_id(bytes: PackedByteArray) -> String:
	return "$RCAnonymousID:" + bytes.hex_encode().substr(0, 32).to_lower()


static func valid_app_user_id(id: String) -> bool:
	return id.begins_with("$RCAnonymousID:") and id.length() == 15 + 32


## Body of POST /v1/receipts: the game buys through Play/StoreKit itself, RevenueCat only records it (observer mode).
static func rc_receipt_body(app_user_id: String, product: String, token: String, is_restore: bool, price: float = 0.0, currency: String = "") -> Dictionary:
	var b := {"app_user_id": app_user_id, "fetch_token": token, "product_id": product, "is_restore": is_restore,
		"observer_mode": true, "initiation_source": "restore" if is_restore else "purchase"}
	if price > 0.0 and currency != "":
		b["price"] = price
		b["currency"] = currency
	return b


static func rc_headers(key: String, os_name: String) -> PackedStringArray:
	return PackedStringArray(["Authorization: Bearer " + key, "X-Platform: " + ("ios" if os_name == "iOS" else "android"),
		"Content-Type: application/json", "Accept: application/json"])
