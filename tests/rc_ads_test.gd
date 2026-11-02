extends Node
## W51a: RevenueCat ad tracking in Borderline Empire (games/CLAUDE.md §5b) — the shared RcAdTracking suite
## (tests/rc_ad_tracking_suite.gd = games/tools/godotx_revenue_cat_src/gd/test_rc_ad_tracking.gd) plus the wiring: the Ads
## (CoddyAds) autoload's REAL AdMob interstitial sends loaded / displayed (impression only) / opened / revenue (paid) with
## the game placement, load failures carry the error code, ads-config rcAdTracking switches it off; the rewarded video
## (rewarded_ads.gd) binds the same tracking with its game slot; house ads are never sent.
## Run: godot --headless --path . res://tests/rc_ads_test.tscn      (last line: RC ADS checks=N failures=0)

class FakeRc extends Object:
	var calls: Array = []
	func ad_tracking_ready() -> bool: return true
	func _rec(m: String, d: Dictionary) -> bool:
		calls.append([m, d.duplicate()])
		return true
	func track_ad_loaded(d: Dictionary) -> bool: return _rec("loaded", d)
	func track_ad_displayed(d: Dictionary) -> bool: return _rec("displayed", d)
	func track_ad_opened(d: Dictionary) -> bool: return _rec("opened", d)
	func track_ad_revenue(d: Dictionary) -> bool: return _rec("revenue", d)
	func track_ad_failed_to_load(d: Dictionary) -> bool: return _rec("failed_to_load", d)
	func names() -> Array: return calls.map(func(c): return c[0])

class FakeValue extends RefCounted:
	var value_micros := 4200
	var currency_code := "EUR"
	var precision := 1

class FakeLoadError extends RefCounted:
	var code := 2

class FakeInterstitial extends InterstitialAd:
	var shows := 0
	func _init(): super(-1)
	func register_callbacks() -> void: pass
	func get_response_info() -> ResponseInfo: return null
	func show() -> void: shows += 1
	func destroy() -> void: pass

var checks := 0
var failures := 0

func ok(c: bool, label: String) -> void:
	checks += 1
	if not c:
		failures += 1
		print("FAIL: ", label)

func check(c: bool, label: String) -> void:
	ok(c, label)

func eq(a, b, label: String) -> void:
	ok(a == b, "%s (%s vs %s)" % [label, str(a), str(b)])

func _ready() -> void:
	await get_tree().process_frame
	var suite = load("res://tests/rc_ad_tracking_suite.gd").new()
	suite.r = self
	for m in suite.get_method_list():
		if str(m.name).begins_with("test_"):
			suite.call(m.name)
	var ads: Node = get_node("/root/Ads")
	var keep: RcAdTracking = ads.rc_ads
	var rc := FakeRc.new()
	ads.rc_ads = RcAdTracking.new(rc)
	var ad := FakeInterstitial.new()
	ads._on_admob_loaded(ad, ads.rc_ads.new_context("ca-app-pub-1111111111111111/2222222222", "interstitial"))
	ok(rc.names() == ["loaded"], "interstitial loaded → RC loaded")
	ads._show_admob("trip_break")
	ad.full_screen_content_callback.on_ad_impression.call()
	ad.full_screen_content_callback.on_ad_clicked.call()
	ad.on_ad_paid.call(FakeValue.new())
	ok(rc.names() == ["loaded", "displayed", "opened", "revenue"], "real impression, click and paid callbacks")
	var rev: Dictionary = rc.calls[3][1]
	ok(rev.revenue_micros == 4200 and rev.currency == "EUR" and rev.precision == "estimated" and rev.placement == "trip_break" and rev.ad_format == "interstitial", "revenue payload: micros unchanged, currency, precision, placement")
	ads._on_admob_failed(FakeLoadError.new(), "ca-app-pub-1111111111111111/2222222222")
	ok(rc.names()[-1] == "failed_to_load" and rc.calls[-1][1].mediator_error_code == 2, "load failure carries the error code")
	ads._apply_config({"adsEnabled": false, "rcAdTracking": false})
	ok(not ads.rc_ads.enabled, "ads-config rcAdTracking=false turns tracking off")
	ads._apply_config({"adsEnabled": false})
	ok(ads.rc_ads.enabled, "absent = on")
	ads._showing = false
	ads._admob_loading = false
	ad.full_screen_content_callback = FullScreenContentCallback.new()
	ads.rc_ads = keep
	rc.free()
	var rw := FileAccess.get_file_as_string("res://scripts/monetization/rewarded_ads.gd")
	ok(rw.contains("rc.track_fullscreen_ad(_ad, rc_ctx)") and rw.find("rc.track_fullscreen_ad(_ad, rc_ctx)") < rw.find("_ad.show(_reward_listener)") and rw.contains("rc.failed_load_error(unit, \"rewarded\""), "the rewarded video binds tracking before show and reports load failures")
	ok(FileAccess.get_file_as_string("res://scripts/monetization/monetization.gd").contains("_pending_reward, _pending_placement)"), "the rewarded placement is the game slot")
	var src := FileAccess.get_file_as_string("res://scripts/monetization/coddy_ads.gd")
	var hs := src.find("func _show_house(")
	var he := src.find("\nfunc ", hs + 10)
	ok(hs > 0 and not src.substr(hs, he - hs).contains("rc_ads"), "house ads are never sent to RevenueCat")
	ok(not src.contains("\"ad_impression\"") and not rw.contains("\"ad_impression\""), "no manual Firebase ad_impression")
	print("RC ADS checks=", checks, " failures=", failures)
	await get_tree().create_timer(0.2).timeout
	get_tree().quit(1 if failures else 0)
