## coddy_iap — glue between the Iap autoload and the game, as a child node of the game's UI:
##   var d := IapDirector.new()
##   d.setup({"open_shop": func(focus): my_ui.open_shop(focus),          # the shop window, scrolled to `focus` ("" = top)
##            "deliver": func(slug, fx) -> bool: return my_game.apply(fx), # put a bought pack into the running game
##            "show_offer": func(slug): my_ui.open_offer(slug),          # optional: a modal with IapScreen.offer()
##            "toast": func(text, tone): my_ui.toast(text, tone)})       # optional: purchase / restore messages
##   add_child(d)
## - A locked feature (`Iap.locked("speed")`) opens the shop on the cheapest product that grants it.
## - Bought packs are delivered when the game can take them (`deliver` returns false while no game is running: they wait,
##   saved, and `try_deliver()` is called again on the next new game / load).
## - Offers: the game calls `check_offer(record, ctx)` at a calm moment (day start, after a report, on an event); the rules
##   are IapOffers (never in the tutorial / first session, cooldown, caps, never an owned product).
class_name IapDirector
extends Node

var hooks: Dictionary = {}


func setup(h: Dictionary) -> void:
	hooks = h


func _ready() -> void:
	var iap := _iap()
	if iap == null:
		return
	iap.feature_locked.connect(_on_locked)
	iap.effects_pending.connect(try_deliver)
	iap.notice.connect(func(text: String, tone: String):
		var t: Callable = hooks.get("toast", Callable())
		if t.is_valid():
			t.call(text, tone))
	try_deliver.call_deferred()


func _on_locked(feature: String) -> void:
	var iap := _iap()
	var open: Callable = hooks.get("open_shop", Callable())
	if iap == null or not iap.sells or not open.is_valid():
		return
	open.call(IapCatalog.slug_for_feature(feature))


## Deliver every bought pack the game can take now. Returns how many were delivered.
func try_deliver() -> int:
	var iap := _iap()
	var deliver: Callable = hooks.get("deliver", Callable())
	if iap == null or not deliver.is_valid():
		return 0
	var n := 0
	for e in iap.pending_effects():
		var slug := String(e.slug)
		if bool(deliver.call(slug, IapCatalog.effect(slug))):
			iap.effect_delivered(String(e.id))
			n += 1
	return n


## Show an offer if one is due (IapOffers rules + the store lists it with a price). Returns the slug shown or "".
## `rec`: the game's saved offer record (IapOffers.new_record()); `ctx`: {"day", "event", "tutorial", "first_session"}.
func check_offer(rec: Dictionary, ctx: Dictionary) -> String:
	var iap := _iap()
	var show: Callable = hooks.get("show_offer", Callable())
	if iap == null or not iap.sells or not show.is_valid():
		return ""
	var slug := IapOffers.pick(rec, ctx, iap.owned())
	if slug == "" or not iap.available(slug) or iap.price_text(slug) == "":
		return ""
	IapOffers.shown(rec, slug, int(ctx.get("day", 0)))
	show.call(slug)
	return slug


func _iap() -> Node:
	return get_node_or_null("/root/Iap")
