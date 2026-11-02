## coddy_iap — when a one-off offer may appear (pure, testable). Rules per offer in data/iap.json "offers":
##   {"starter": {"day_min": 3, "day_max": 7}, "crisis": {"event": "big_scandal", "per_game": 1}, "cooldown_days": 3}
## Owner rules (games/tools/IAP_STANDARD.md §1): never during the tutorial or the first session, at most one offer a day,
## `cooldown_days` between two offers, a permanent product already owned is never offered, `per_game` caps repeats.
## The per-game record lives in the game's own save (a Dictionary the game keeps; start with IapOffers.new_record()).
class_name IapOffers
extends RefCounted


static func new_record() -> Dictionary:
	return {"last_day": -999, "count": {}}


## The offer to show now ("" = none). `ctx`: {"day": int (the game's day / session counter), "event": String ("" for the
## daily check), "tutorial": bool, "first_session": bool}. `owned`: slug → true (permanent products the player has).
static func pick(rec: Dictionary, ctx: Dictionary, owned: Dictionary) -> String:
	if bool(ctx.get("tutorial", false)) or bool(ctx.get("first_session", false)):
		return ""
	var day := int(ctx.get("day", 0))
	var rules := IapCatalog.offers()
	if day - int(rec.get("last_day", -999)) < int(rules.get("cooldown_days", 3)):
		return ""
	var ev := String(ctx.get("event", ""))
	for slug in IapCatalog.slugs():
		var r = rules.get(slug)
		if typeof(r) != TYPE_DICTIONARY or not IapCatalog.has(slug):
			continue
		if not IapCatalog.is_consumable(slug) and owned.has(slug):
			continue
		if String(r.get("event", "")) != ev:
			continue
		if ev == "" and (day < int(r.get("day_min", 0)) or day > int(r.get("day_max", 1 << 30))):
			continue
		if int(rec.get("count", {}).get(slug, 0)) >= int(r.get("per_game", 1)):
			continue
		return slug
	return ""


## The offer was shown (whatever the player chose).
static func shown(rec: Dictionary, slug: String, day: int) -> void:
	rec["last_day"] = day
	if not rec.has("count"):
		rec["count"] = {}
	rec.count[slug] = int(rec.count.get(slug, 0)) + 1
