## coddy_iap — the product catalog of a game, read from `res://data/iap.json` (games/tools/shop/README.md). Pure data +
## lookups, no Node, no store. Every other piece of the template (IapGate, IapStore, IapOffers, IapScreen, IapDirector)
## asks this class. Prices are never kept here: the game always shows the store's displayPrice; `usd` is the reference
## price (desktop mock, tests, the store-side scripts). Derived from Corrupt State's Catalog.gd (2026-10-18).
class_name IapCatalog
extends RefCounted

const FILE := "res://data/iap.json"
const KIND_PERMANENT := "permanent" # non-consumable: owned forever, restorable
const KIND_CONSUMABLE := "consumable" # bought again and again: delivered once, never restored

static var _d: Dictionary = {}
static var _loaded_from := ""


## Load (or reload) the catalog. Tests point it at their own file; the game never needs to call it (first use loads FILE).
static func load_from(path: String = FILE) -> bool:
	_d = {}
	_loaded_from = path
	if not FileAccess.file_exists(path):
		push_error("coddy_iap: no catalog at " + path)
		return false
	var j = JSON.parse_string(FileAccess.get_file_as_string(path))
	if typeof(j) != TYPE_DICTIONARY or typeof(j.get("products")) != TYPE_DICTIONARY:
		push_error("coddy_iap: broken catalog " + path)
		return false
	_d = j
	return true


static func data() -> Dictionary:
	if _loaded_from == "":
		load_from()
	return _d


static func prefix() -> String:
	return String(data().get("prefix", ""))


static func has(slug: String) -> bool:
	return data().get("products", {}).has(slug)


static func def(slug: String) -> Dictionary:
	return data().get("products", {}).get(slug, {})


## Display order (every slug once). Falls back to the products' own order.
static func slugs() -> Array:
	var o: Array = data().get("order", [])
	return o.duplicate() if not o.is_empty() else data().get("products", {}).keys()


## The store product id: `id` of the product when it has one (an old id kept for its buyers), else prefix + slug.
static func product_id(slug: String) -> String:
	var d := def(slug)
	return String(d.get("id", prefix() + slug))


static func product_ids() -> Array[String]:
	var out: Array[String] = []
	for s in slugs():
		out.append(product_id(s))
	return out


## Store product id → slug ("" for a foreign or unknown id). `legacy` maps retired ids onto today's slugs.
static func slug_of(pid: String) -> String:
	for s in data().get("products", {}):
		if product_id(String(s)) == pid:
			return String(s)
	return String(data().get("legacy", {}).get(pid, ""))


static func is_consumable(slug: String) -> bool:
	return String(def(slug).get("kind", "")) == KIND_CONSUMABLE


static func grants(slug: String) -> Array:
	return def(slug).get("grants", [])


## The one-off effect key a purchase delivers into the running game ("" = none): the game's `deliver` callback.
static func effect(slug: String) -> String:
	return String(def(slug).get("fx", ""))


static func once(slug: String) -> bool:
	return bool(def(slug).get("once", false))


static func group(slug: String) -> String:
	return String(def(slug).get("group", "perm"))


## [{id, title{lang: text}}] in shop order.
static func groups() -> Array:
	return data().get("groups", [])


## The cheapest permanent product that grants this feature: the shop opens on it when a locked feature is tapped.
static func slug_for_feature(feature: String) -> String:
	var best := ""
	for s in slugs():
		if is_consumable(s) or not feature in grants(s):
			continue
		if best == "" or float(def(s).get("usd", 0.0)) < float(def(best).get("usd", 0.0)):
			best = s
	return best


## Reference USD of the products a bundle stands for ("separately $x" line, tests).
static func bundle_parts(slug: String) -> Array:
	return def(slug).get("parts", [])


static func separate_usd(slug: String) -> float:
	var t := 0.0
	for s in bundle_parts(slug):
		t += float(def(String(s)).get("usd", 0.0))
	return t


## Save slots: {"base": 2, "per_feature": {"premium": 1, "slots": 3}, "max": 6}; empty = the game has no slot products.
static func slots() -> Dictionary:
	return data().get("slots", {})


## Offer rules (IapOffers): {slug: {day_min, day_max, event, per_game, once}} + "cooldown_days".
static func offers() -> Dictionary:
	return data().get("offers", {})
