extends RefCounted
## RcAdTracking (W36) against a fake GodotxRevenueCat plugin: the acceptance rows of REVENUECAT_AD_TRACKING.md that can
## run without a device (value/precision, one impression, repeated callbacks, offline/queue, identity, kill switch).

var T
var r
const UNIT := "ca-app-pub-0000000000000000/1111111111"


class FakeRc:
	extends Object
	var configured := true
	var fail := false
	var calls: Array = []     ## [method, payload]

	func ad_tracking_ready() -> bool:
		return configured

	func _rec(m: String, d: Dictionary) -> bool:
		if fail:
			return false
		calls.append([m, d.duplicate()])
		return true

	func track_ad_loaded(d: Dictionary) -> bool:
		return _rec("loaded", d)

	func track_ad_displayed(d: Dictionary) -> bool:
		return _rec("displayed", d)

	func track_ad_opened(d: Dictionary) -> bool:
		return _rec("opened", d)

	func track_ad_revenue(d: Dictionary) -> bool:
		return _rec("revenue", d)

	func track_ad_failed_to_load(d: Dictionary) -> bool:
		return _rec("failed_to_load", d)

	func names() -> Array:
		return calls.map(func(c): return c[0])


## A plugin built before W36 (W23 .aar): has the store methods, none of the ad ones.
class OldRc:
	extends Object
	func get_customer_info() -> void:
		pass


class FakeAdapter:
	extends RefCounted
	var ad_source_name := ""


class FakeResponse:
	extends RefCounted
	var response_id := ""
	var loaded_adapter_response_info: FakeAdapter


class FakeCallbacks:
	extends RefCounted
	var on_ad_impression: Callable = func() -> void: pass
	var on_ad_clicked: Callable = func() -> void: pass
	var on_ad_dismissed_full_screen_content: Callable = func() -> void: pass


## Poing InterstitialAd / RewardedAd shape: callbacks object, on_ad_paid, get_response_info().
class FakeAd:
	extends RefCounted
	var full_screen_content_callback := FakeCallbacks.new()
	var on_ad_paid: Callable = func(_v) -> void: pass
	var info: RefCounted
	func get_response_info():
		return info


class FakeError:
	extends RefCounted
	var code := 3


class FakeValue:
	extends RefCounted
	var value_micros := 0
	var currency_code := ""
	var precision := 0


func _h():
	return T if T != null else r


func _ok(c: bool, msg: String) -> void:
	var h = _h()
	if h.has_method("ok"):
		h.ok(c, msg)
	else:
		h.check(c, msg)


func _resp(rid: String, network: String) -> FakeResponse:
	var x := FakeResponse.new()
	x.response_id = rid
	x.loaded_adapter_response_info = FakeAdapter.new()
	x.loaded_adapter_response_info.ad_source_name = network
	return x


func _value(micros: int, cur: String, prec: int) -> FakeValue:
	var v := FakeValue.new()
	v.value_micros = micros
	v.currency_code = cur
	v.precision = prec
	return v


func test_ad_value_and_precision() -> void:
	var rc := FakeRc.new()
	var t := RcAdTracking.new(rc)
	var ctx := t.new_context(UNIT, "rewarded")
	t.loaded(ctx, _resp("resp-1", "AdMob Network"))
	_ok(t.revenue(ctx, _value(5000, "TRY", 3)), "a paid callback is handed to the plugin")
	var p: Dictionary = rc.calls[-1][1]
	_ok(p.revenue_micros == 5000 and typeof(p.revenue_micros) == TYPE_INT, "5000 micros stay 5000 (int, never scaled)")
	_ok(p.currency == "TRY", "the callback's currency is kept (no fixed USD)")
	_ok(p.precision == "exact" and p.impression_id == "resp-1" and p.network_name == "AdMob Network", "precise → exact, response id + network carried")
	var want := {0: "unknown", 1: "estimated", 2: "publisher_defined", 3: "exact", 9: "unknown", -1: "unknown"}
	var all := true
	for k in want:
		all = all and RcAdTracking.precision_from_admob(k) == want[k]
	_ok(all, "explicit AdMob → RevenueCat precision table (unknown values → unknown)")
	var big := t.new_context(UNIT, "interstitial")
	t.revenue_values(big, 9_000_000_000_123, "USD", 1)
	_ok(rc.calls[-1][1].revenue_micros == 9_000_000_000_123, "a value above 32 bits survives (Long, no truncation)")
	_ok(not t.revenue_values(t.new_context(UNIT, "rewarded"), 10, "", 1), "no currency → nothing sent")
	rc.free()


func test_one_impression() -> void:
	var rc := FakeRc.new()
	var t := RcAdTracking.new(rc)
	var ctx := t.new_context(UNIT, "interstitial")
	t.loaded(ctx, _resp("", ""))
	_ok(str(ctx.impression_id).length() == 36, "no response id → a per-load UUID ties the events")
	t.set_placement(ctx, "offline_bonus")
	t.displayed(ctx)
	t.revenue(ctx, _value(1200, "EUR", 1))
	_ok(rc.names() == ["loaded", "displayed", "revenue"], "one load + one impression + one paid callback, no opened without a click")
	var ids := {}
	for c in rc.calls:
		ids[c[1].impression_id] = true
	_ok(ids.size() == 1, "all events of one ad share one impression id")
	_ok(rc.calls[0][1].placement == "" and rc.calls[1][1].placement == "offline_bonus", "placement is the game slot set when the ad is picked")
	_ok(rc.calls[0][1].network_name == "" and rc.calls[0][1].mediator_name == "AdMob", "unknown network stays empty (null in Kotlin), mediator AdMob")
	t.opened(ctx)
	_ok(rc.names()[-1] == "opened", "a click sends opened")
	rc.free()


func test_repeated_callbacks() -> void:
	var rc := FakeRc.new()
	var t := RcAdTracking.new(rc)
	t.set_user("u1")
	var ctx := t.new_context(UNIT, "rewarded")
	t.loaded(ctx, _resp("r-9", "x"))
	t.displayed(ctx)
	t.displayed(ctx)
	t.revenue(ctx, _value(700, "USD", 1))
	t.revenue(ctx, _value(700, "USD", 1))
	_ok(rc.names() == ["loaded", "displayed", "revenue"], "a re-delivered impression / paid callback is sent once")
	_ok(t.conflicts == 0, "an identical repeat is not a conflict")
	_ok(not t.revenue(ctx, _value(900, "USD", 1)) and t.conflicts == 1 and rc.names().count("revenue") == 1, "a second paid amount is not sent, only counted")
	var ctx2 := t.new_context(UNIT, "rewarded")
	t.loaded(ctx2, _resp("r-10", "x"))
	t.revenue(ctx2, _value(700, "USD", 1))
	_ok(rc.names().count("revenue") == 2, "the next ad (new impression) is its own revenue")
	rc.free()


func test_offline_and_queue() -> void:
	var rc := FakeRc.new()
	rc.configured = false
	var t := RcAdTracking.new(rc)
	t.set_user("u1")
	var ctx := t.new_context(UNIT, "rewarded")
	_ok(not t.loaded(ctx) and not t.displayed(ctx) and not t.revenue(ctx, _value(5, "USD", 0)), "RC not configured → nothing sent, game goes on")
	_ok(rc.calls.is_empty() and t.queue.size() == 3, "events wait in the queue")
	t.displayed(ctx)
	_ok(t.queue.size() == 3, "a repeat while queued is not queued twice")
	rc.configured = true
	_ok(t.flush() == 3 and rc.names() == ["loaded", "displayed", "revenue"] and t.queue.is_empty(), "flush sends them once, in order")
	_ok(t.flush() == 0 and not t.displayed(ctx), "after flush they count as sent")
	rc.configured = false
	for i in RcAdTracking.MAX_QUEUE + 5:
		t.loaded(t.new_context(UNIT, "interstitial"))
	_ok(t.queue.size() == RcAdTracking.MAX_QUEUE and t.dropped == 5, "the queue is bounded, overflow counted")
	t.set_user("u2")
	_ok(t.queue.is_empty(), "another user never inherits the previous user's queued ads")
	rc.configured = true
	rc.fail = true
	_ok(not t.displayed(t.new_context(UNIT, "rewarded")), "the plugin refusing an event returns false, no error")
	rc.free()


func test_kill_switch_and_old_plugin() -> void:
	var rc := FakeRc.new()
	var t := RcAdTracking.new(rc)
	t.enabled = false
	var ctx := t.new_context(UNIT, "rewarded")
	t.loaded(ctx)
	t.displayed(ctx)
	t.revenue(ctx, _value(5, "USD", 0))
	t.failed_to_load(UNIT, "rewarded", "x", 3)
	_ok(rc.calls.is_empty() and t.queue.is_empty(), "enabled=false (kids_mode, kill switch) sends and queues nothing")
	t.enabled = true
	t.failed_to_load(UNIT, "rewarded", "contract_retry", 3)
	var f: Dictionary = rc.calls[-1][1]
	_ok(rc.names() == ["failed_to_load"] and f.mediator_error_code == 3 and not f.has("impression_id") and not f.has("network_name"), "load failure: error code, no invented impression/network")
	var old := OldRc.new()
	var t2 := RcAdTracking.new(old)
	var c2 := t2.new_context(UNIT, "rewarded")
	_ok(not t2.supported() and not t2.loaded(c2) and t2.queue.is_empty(), "a pre-W36 plugin: unsupported, nothing called or queued")
	var t3 := RcAdTracking.new(null)
	_ok(not t3.ready() and not t3.revenue(t3.new_context(UNIT, "rewarded"), _value(1, "USD", 1)), "no plugin (desktop/iOS) → false, no crash")
	_ok(not t.revenue(ctx, null) and not t.displayed({}), "null value / empty context ignored")
	rc.free()
	old.free()


func test_poing_ad_binding() -> void:
	var rc := FakeRc.new()
	var t := RcAdTracking.new(rc)
	var ad := FakeAd.new()
	ad.info = _resp("resp-7", "AdMob Network")
	var dismissed := [0]
	ad.full_screen_content_callback.on_ad_dismissed_full_screen_content = func() -> void: dismissed[0] += 1
	t.track_fullscreen_ad(ad, t.new_context(UNIT, "interstitial"))
	_ok(rc.names() == ["loaded"] and rc.calls[0][1].impression_id == "resp-7", "binding sends loaded with the response id")
	t.placement_for(ad, "day_break")
	ad.full_screen_content_callback.on_ad_impression.call()
	ad.on_ad_paid.call(_value(2500, "USD", 2))
	ad.full_screen_content_callback.on_ad_impression.call()
	ad.full_screen_content_callback.on_ad_dismissed_full_screen_content.call()
	_ok(rc.names() == ["loaded", "displayed", "revenue"] and dismissed[0] == 1, "impression + paid tracked once; the game's own dismiss handler untouched")
	_ok(rc.calls[2][1].placement == "day_break" and rc.calls[2][1].precision == "publisher_defined", "placement set at show; publisher_provided → publisher_defined")
	ad.full_screen_content_callback.on_ad_clicked.call()
	_ok(rc.names()[-1] == "opened", "click → opened")
	_ok(t.failed_load_error(UNIT, "rewarded", FakeError.new()) and rc.calls[-1][1].mediator_error_code == 3, "LoadAdError code carried")
	var old := OldRc.new()
	var ad2 := FakeAd.new()
	RcAdTracking.new(old).track_fullscreen_ad(ad2, {})
	_ok(not ad2.has_meta("rc_ad_ctx"), "pre-W36 plugin: nothing is bound")
	rc.free()
	old.free()
