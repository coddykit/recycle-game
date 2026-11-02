extends Node
## One owner of the Google Mobile Ads SDK (Play Vitals 2026-10-20: Train Baron 1.1.2 died 19× with IllegalStateException
## "MobileAds.initialize must be called before using the Google Mobile Ads SDK" — an interstitial was loaded before the
## SDK finished initialising). State machine with a fake SDK (no plugin, no network): the Ads autoload starts consent +
## MobileAds.initialize at most once whoever asks (interstitial start, rewarded tap, both at once); nothing is loaded
## before the init callback; the rewarded request waits for that callback, gives up after SDK_WAIT_S and a late callback
## never plays a video nobody waits for; source rule: no other script calls MobileAds.initialize or loads before init.
## Run: godot --headless --path . res://tests/ads_sdk_init_test.tscn      (last line: ADS SDK INIT checks=N failures=0)

class FakeAds extends "res://scripts/monetization/coddy_ads.gd":
	var starts := 0
	var interstitial_loads := 0
	func _ready() -> void: pass
	func _process(_d: float) -> void: pass
	func _admob_available() -> bool: return true
	func _ad_free() -> bool: return false
	func _save_state() -> void: pass
	func _start_admob() -> void:
		starts += 1            # the real one: UMP update → consent form → _init_admob → MobileAds.initialize
	func _load_admob() -> void:
		if not _admob_inited or _admob_loading or _admob_ready or _ad_free() or not bool(_config.get("adsEnabled", false)) or _unit_id().is_empty():
			return
		interstitial_loads += 1
		_admob_loading = true

class FakeRewarded extends "res://scripts/monetization/rewarded_ads.gd":
	var loads := 0
	var consent := true
	var house: Node
	func available(_unit: String) -> bool: return true
	func _house() -> Node: return house
	func _consent_ok() -> bool: return consent
	func _load() -> void: loads += 1

var checks := 0
var failures := 0

func ok(c: bool, label: String) -> void:
	checks += 1
	if not c:
		failures += 1
		print("FAIL: ", label)

func _pair() -> Array:
	var ads := FakeAds.new()
	ads._mobile = true
	ads._config = {"adsEnabled": true}
	add_child(ads)
	var rw := FakeRewarded.new()
	rw.house = ads
	add_child(rw)
	var fails := [0]
	rw.failed.connect(func(): fails[0] += 1)
	return [ads, rw, fails]

func _drop(p: Array) -> void:
	p[1].queue_free()
	p[0].queue_free()

func _ready() -> void:
	await get_tree().process_frame
	# 1. interstitial start first, then a rewarded tap while the SDK is still initialising
	var p := _pair()
	var ads: FakeAds = p[0]
	var rw: FakeRewarded = p[1]
	ads.start_admob()
	ok(ads.starts == 1, "Ads.start_admob starts consent + SDK once")
	ads._load_admob()
	ok(ads.interstitial_loads == 0, "no interstitial load before the SDK init callback")
	rw.show_reward("unit", "tok1", "sponsor")
	ok(ads.starts == 1, "a rewarded tap while the SDK starts does NOT initialise it again")
	ok(rw.loads == 0, "no rewarded load before the SDK init callback")
	ads.start_admob()
	ok(ads.starts == 1, "a second start_admob is a no-op")
	ads._on_admob_initialized()
	ok(rw.loads == 1 and ads.interstitial_loads == 0, "init callback releases the waiting rewarded load; no AdMob interstitial load (Emir #125: interstitials are house-only)")
	ads._on_admob_initialized()
	ok(rw.loads == 1, "a duplicate init callback does not load the rewarded again")
	rw._close()
	rw.show_reward("unit", "tok2", "sponsor")
	ok(rw.loads == 2 and ads.starts == 1, "after init a rewarded tap loads at once, no new init")
	rw._close()
	_drop(p)

	# 2. rewarded tap first (interstitial never started): the rewarded starts the ONE SDK init itself
	p = _pair()
	ads = p[0]
	rw = p[1]
	rw.show_reward("unit", "tok", "sponsor")
	ok(ads.starts == 1 and rw.loads == 0, "rewarded-first: SDK starts once through Ads, no load yet")
	ads.start_admob()
	ok(ads.starts == 1, "the later interstitial start does not initialise again")
	ads._on_admob_initialized()
	ok(rw.loads == 1, "rewarded loads after the callback")
	rw._close()
	_drop(p)

	# 3. consent not given → no video, the game is told
	p = _pair()
	ads = p[0]
	rw = p[1]
	rw.consent = false
	rw.show_reward("unit", "tok", "sponsor")
	ads._on_admob_initialized()
	ok(rw.loads == 0 and p[2][0] == 1 and rw._token.is_empty(), "consent missing → failed, nothing loaded")
	_drop(p)

	# 4. the SDK never answers → the request fails after SDK_WAIT_S; a late callback plays nothing
	p = _pair()
	ads = p[0]
	rw = p[1]
	rw.show_reward("unit", "tok", "sponsor")
	rw._on_sdk_timeout(rw._serial)
	ok(p[2][0] == 1 and rw._token.is_empty(), "timeout → failed, request dropped")
	ads._on_admob_initialized()
	ok(rw.loads == 0, "a late init callback after the timeout never loads a video")
	rw.show_reward("unit", "tok3", "sponsor")
	ok(rw.loads == 1, "the next tap works normally")
	rw._close()
	_drop(p)

	# 5. desktop / no plugin → fails at once, never waits
	p = _pair()
	ads = p[0]
	rw = p[1]
	ads._mobile = false
	rw.show_reward("unit", "tok", "sponsor")
	ok(p[2][0] == 1 and ads.starts == 0 and not rw._waiting_sdk, "no SDK here → failed at once")
	_drop(p)

	# 6. source rules (the whole game): ONE MobileAds.initialize call site, in the Ads autoload; every ad load is gated
	var inits := []
	var stack := ["res://scripts"]
	while not stack.is_empty():
		var dir: String = stack.pop_back()
		for d in DirAccess.get_directories_at(dir):
			stack.append(dir + "/" + d)
		for f in DirAccess.get_files_at(dir):
			if f.ends_with(".gd"):
				var src := FileAccess.get_file_as_string(dir + "/" + f)
				for line in src.split("\n"):
					var code := line.strip_edges()
					if not code.begins_with("#") and not code.begins_with("##") and code.contains("MobileAds.initialize("):
						inits.append(dir + "/" + f)
	ok(inits == ["res://scripts/monetization/coddy_ads.gd"], "MobileAds.initialize is called only in coddy_ads.gd (%s)" % str(inits))
	var ca := FileAccess.get_file_as_string("res://scripts/monetization/coddy_ads.gd")
	var lf := ca.substr(ca.find("func _load_admob()"), 260)
	ok(lf.contains("not _admob_inited") and lf.contains("_unit_id().is_empty()"), "_load_admob is gated on init and a unit")
	var rs := FileAccess.get_file_as_string("res://scripts/monetization/rewarded_ads.gd")
	ok(rs.contains("when_sdk_ready") and not rs.contains("MobileAds.initialize("), "rewarded_ads waits for the Ads autoload's SDK start")
	print("ADS SDK INIT checks=", checks, " failures=", failures)
	await get_tree().create_timer(0.2).timeout
	get_tree().quit(1 if failures else 0)
