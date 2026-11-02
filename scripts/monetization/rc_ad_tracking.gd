## RcAdTracking — W36 (2026-10-20): forwards real AdMob (Poing) ad callbacks to RevenueCat Ad Monetization through the
## godotx_revenue_cat W36 plugin (track_ad_loaded / displayed / opened / revenue / failed_to_load). Design and rules:
## games/tools/REVENUECAT_AD_TRACKING.md. Copy of games/tools/godotx_revenue_cat_src/gd/RcAdTracking.gd (keep equal).
##
## One context per loaded ad: `var ctx := tracker.new_context(ad_unit_id, "rewarded")` when the load starts, then
## `tracker.loaded(ctx, ad.get_response_info())` → bind the callbacks BEFORE show → `tracker.set_placement(ctx, "offline_bonus")`
## when the ad is picked for a slot → on_ad_impression: `tracker.displayed(ctx)` (not on_ad_showed_full_screen_content),
## on_ad_clicked: `tracker.opened(ctx)`, on_ad_paid: `tracker.revenue(ctx, ad_value)`, load failure: `tracker.failed_to_load(...)`.
## The reward callback stays the game's; nothing here grants, blocks or delays a reward, an ad or a purchase.
## Not sent: house/CoddyAds promos, mock/sponsor ads, the ad_free video-less reward, kids_mode (set `enabled = false`).
##
## AdMob value_micros are already micros and go out unchanged (never ×/÷ 1e6); the currency comes from the callback.
## Each (user, ad unit, impression, event) is sent once; a second paid callback with a different amount is not sent (no
## invented correction) and only counted in `conflicts`. Before RevenueCat is configured events wait in a small queue
## (MAX_QUEUE, overflow counted in `dropped`), only for the same user; `set_user()` with another id clears it.
## Nothing (ids, amounts) is logged.
class_name RcAdTracking
extends RefCounted

const SINGLETON := "GodotxRevenueCat"
const MAX_QUEUE := 20
const MAX_SENT := 512
## AdMob AdValue.PrecisionType → RevenueCat AdRevenuePrecision (explicit table, never the enum ordinal).
const PRECISION := {0: "unknown", 1: "estimated", 2: "publisher_defined", 3: "exact"}
const EVENTS := {
	"loaded": "track_ad_loaded",
	"displayed": "track_ad_displayed",
	"opened": "track_ad_opened",
	"revenue": "track_ad_revenue",
	"failed_to_load": "track_ad_failed_to_load",
}

## Tracking kill switch — separate from ad serving (W32 rewardedEnabled / ad_free stay the game's).
var enabled := true
var plugin: Object
var user_id := ""
var queue: Array = []        ## [[event, payload, sent_key]]
var dropped := 0
var conflicts := 0
var _sent := {}              ## sent_key → payload (insertion order = age)


func _init(p: Object = null) -> void:
	plugin = p
	if plugin == null and Engine.has_singleton(SINGLETON):
		plugin = Engine.get_singleton(SINGLETON)


## The plugin build carries the W36 methods (an older .aar has none: nothing is sent, nothing breaks).
func supported() -> bool:
	return plugin != null and plugin.has_method("track_ad_revenue") and plugin.has_method("ad_tracking_ready")


func ready() -> bool:
	return enabled and supported() and bool(plugin.call("ad_tracking_ready"))


## Same RevenueCat app user as the IAP side. Another id drops what the previous user left queued or sent.
func set_user(id: String) -> void:
	if id == user_id:
		return
	user_id = id
	queue.clear()
	_sent.clear()


static func precision_from_admob(p: int) -> String:
	return PRECISION.get(p, "unknown")


## Ad network that served the ad, from Poing ResponseInfo.loaded_adapter_response_info; "" when unknown (never invented).
static func network_from_response(response_info: Object) -> String:
	if response_info == null:
		return ""
	var a = response_info.get("loaded_adapter_response_info")
	if a == null or not (a is Object):
		return ""
	return str(a.get("ad_source_name") if a.get("ad_source_name") != null else "").strip_edges()


static func _uuid() -> String:
	var b := PackedByteArray()
	for i in 16:
		b.append(randi() & 0xff)
	b[6] = (b[6] & 0x0f) | 0x40
	b[8] = (b[8] & 0x3f) | 0x80
	var h := b.hex_encode()
	return "%s-%s-%s-%s-%s" % [h.substr(0, 8), h.substr(8, 4), h.substr(12, 4), h.substr(16, 4), h.substr(20, 12)]


## ad_format: "interstitial" | "rewarded" | "rewarded_interstitial" | "banner" | "native" | "app_open" | "other".
func new_context(ad_unit_id: String, ad_format: String, placement := "") -> Dictionary:
	return {
		"ad_unit_id": ad_unit_id,
		"ad_format": ad_format,
		"placement": placement,
		"mediator_name": "AdMob",
		"network_name": "",
		"impression_id": _uuid(),
	}


## Placement is the game slot (offline_bonus, contract_retry), not the ad unit id; fixed once the ad is picked.
func set_placement(ctx: Dictionary, placement: String) -> void:
	ctx.placement = placement


## The ad arrived. AdMob response_id (when present) becomes the impression id that ties loaded/displayed/opened/revenue.
func loaded(ctx: Dictionary, response_info: Object = null) -> bool:
	if response_info != null:
		var rid = response_info.get("response_id")
		if rid != null and str(rid) != "":
			ctx.impression_id = str(rid)
		ctx.network_name = network_from_response(response_info)
	return _track("loaded", ctx, {})


## Real impression (on_ad_impression).
func displayed(ctx: Dictionary) -> bool:
	return _track("displayed", ctx, {})


## Ad clicked (on_ad_clicked) — not the game's "watch" button.
func opened(ctx: Dictionary) -> bool:
	return _track("opened", ctx, {})


## on_ad_paid(AdValue): value_micros / currency_code / precision.
func revenue(ctx: Dictionary, ad_value: Object) -> bool:
	if ad_value == null:
		return false
	return revenue_values(ctx, int(ad_value.get("value_micros")), str(ad_value.get("currency_code")), int(ad_value.get("precision")))


func revenue_values(ctx: Dictionary, value_micros: int, currency: String, admob_precision: int) -> bool:
	if currency.strip_edges() == "" or value_micros < 0:
		return false
	return _track("revenue", ctx, {
		"revenue_micros": value_micros,
		"currency": currency.strip_edges(),
		"precision": precision_from_admob(admob_precision),
	})


## Load failure: no network and no impression (none exists); error_code = LoadAdError.code.
func failed_to_load(ad_unit_id: String, ad_format: String, placement := "", error_code = null) -> bool:
	if not enabled or ad_unit_id == "":
		return false
	var p := {"ad_unit_id": ad_unit_id, "ad_format": ad_format, "placement": placement, "mediator_name": "AdMob"}
	if error_code != null:
		p["mediator_error_code"] = int(error_code)
	return _send("failed_to_load", p, "")


## Poing InterstitialAd / RewardedAd, right after on_ad_loaded and BEFORE show: sends loaded and binds the real callbacks
## (impression → displayed, clicked → opened, paid → revenue) to this ad's context. The game keeps its own dismiss /
## failed-to-show / reward handlers (other fields). Nothing is bound when the plugin build has no ad methods.
func track_fullscreen_ad(ad: Object, ctx: Dictionary) -> void:
	if ad == null or not supported():
		return
	ad.set_meta("rc_ad_ctx", ctx)
	var info: Object = ad.call("get_response_info") if ad.has_method("get_response_info") else null
	loaded(ctx, info)
	var fs = ad.get("full_screen_content_callback")
	if fs != null:
		fs.on_ad_impression = func() -> void: displayed(ctx)
		fs.on_ad_clicked = func() -> void: opened(ctx)
	if "on_ad_paid" in ad:
		ad.on_ad_paid = func(v) -> void: revenue(ctx, v)


## The game slot of a tracked ad, set when it is picked for a show.
func placement_for(ad: Object, placement: String) -> void:
	if ad != null and ad.has_meta("rc_ad_ctx"):
		set_placement(ad.get_meta("rc_ad_ctx"), placement)


## Load failure of a Poing load callback (LoadAdError or null).
func failed_load_error(ad_unit_id: String, ad_format: String, error: Object) -> bool:
	var code = error.get("code") if error != null else null
	return failed_to_load(ad_unit_id, ad_format, "", code)


## Sends what waited for RevenueCat. Call after the IAP side configured RC (and on any later ready check).
func flush() -> int:
	if not ready():
		return 0
	var n := 0
	var pending := queue
	queue = []
	for q in pending:
		if _deliver(q[0], q[1], q[2]):
			n += 1
	return n


func _track(event: String, ctx: Dictionary, extra: Dictionary) -> bool:
	if not enabled or str(ctx.get("ad_unit_id", "")) == "" or str(ctx.get("impression_id", "")) == "":
		return false
	var p := {}
	for k in ["ad_unit_id", "ad_format", "placement", "mediator_name", "network_name", "impression_id"]:
		p[k] = ctx.get(k, "")
	p.merge(extra, true)
	var key := "%s|%s|%s|%s" % [user_id, p.ad_unit_id, p.impression_id, event]
	if _sent.has(key):
		if _sent[key] != p:
			conflicts += 1
		return false
	for q in queue:
		if q[2] == key:
			if q[1] != p:
				conflicts += 1
			return false
	return _send(event, p, key)


func _send(event: String, p: Dictionary, key: String) -> bool:
	if ready():
		return _deliver(event, p, key)
	if not enabled or not supported():
		return false
	if queue.size() >= MAX_QUEUE:
		dropped += 1
		return false
	queue.append([event, p, key])
	return false


func _deliver(event: String, p: Dictionary, key: String) -> bool:
	var ok := bool(plugin.call(EVENTS[event], p))
	if ok and key != "":
		_sent[key] = p
		if _sent.size() > MAX_SENT:
			_sent.erase(_sent.keys()[0])
	return ok
