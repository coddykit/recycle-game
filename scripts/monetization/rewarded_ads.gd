extends Node
## Optional native rewarded ads. Desktop mocks are deliberately never used.
signal earned(token: String)
signal finished
signal failed
var _token := ""
var _unit := ""
var _ad: RewardedAd
var _load_callback := RewardedAdLoadCallback.new()
var _reward_listener := OnUserEarnedRewardListener.new()
var _content_callback := FullScreenContentCallback.new()
var _consent := ConsentInformation.new()
var _placement := "" ## the game slot of this video (sponsor, offline_double ...), the RevenueCat placement (W51a)

func available(unit: String) -> bool:
	return OS.get_name() in ["Android", "iOS"] and Engine.has_singleton("PoingGodotAdMob") and (OS.is_debug_build() or not unit.is_empty())

func show_reward(unit: String, token: String, placement := "") -> void:
	if not available(unit) or not _token.is_empty():
		failed.emit()
		return
	_token = token
	_unit = unit
	_placement = placement
	if OS.is_debug_build():
		_unit = "ca-app-pub-3940256099942544/5224354917" if OS.get_name() == "Android" else "ca-app-pub-3940256099942544/1712485313"
	_serial += 1
	var serial := _serial
	_waiting_sdk = true
	# ONE owner of the SDK: the Ads (CoddyAds) autoload runs the UMP consent and MobileAds.initialize exactly once; this
	# node never initialises it again and never loads before it reported completion (IllegalStateException otherwise).
	var house := _house()
	if house == null or not house.call("when_sdk_ready", _on_sdk_ready.bind(serial)):
		_waiting_sdk = false
		_fail()
		return
	if _waiting_sdk and serial == _serial and is_inside_tree():
		get_tree().create_timer(SDK_WAIT_S, true, false, true).timeout.connect(_on_sdk_timeout.bind(serial))

const SDK_WAIT_S := 20.0
var _serial := 0
var _waiting_sdk := false

func _house() -> Node:
	var n := get_node_or_null("/root/Ads")
	return n if n != null and n.has_method("when_sdk_ready") else null

## The SDK finished initialising (the Ads autoload's callback). A request that already timed out or was replaced is ignored,
## so a late callback can never show a video the game no longer waits for.
func _on_sdk_ready(serial: int) -> void:
	if serial != _serial or not _waiting_sdk or _token.is_empty():
		return
	_waiting_sdk = false
	if not _consent_ok():
		_fail()
		return
	_load()

func _on_sdk_timeout(serial: int) -> void:
	if serial == _serial and _waiting_sdk:
		_waiting_sdk = false
		_fail()

## Same rule as before: the video plays only once Google UMP says consent is not required or was given.
func _consent_ok() -> bool:
	return _consent.get_consent_status() in [ConsentInformation.ConsentStatus.NOT_REQUIRED, ConsentInformation.ConsentStatus.OBTAINED]

## The RevenueCat ad tracker of the Ads (CoddyAds) autoload (W51a), or null (tests, no autoload).
func _rc_ads() -> RcAdTracking:
	var house := get_node_or_null("/root/Ads")
	return house.get("rc_ads") if house != null and house.get("rc_ads") is RcAdTracking else null

func _load() -> void:
	var rc := _rc_ads()
	var rc_ctx := rc.new_context(_unit, "rewarded", _placement) if rc != null else {}
	if rc != null: rc.flush()
	var unit := _unit
	_load_callback.on_ad_failed_to_load = func(_error):
		if rc != null: rc.failed_load_error(unit, "rewarded", _error)
		_fail()
	_load_callback.on_ad_loaded = func(ad: RewardedAd):
		_ad = ad
		_reward_listener.on_user_earned_reward = func(_item):
			if not _token.is_empty():
				var token := _token
				_token = ""
				earned.emit(token)
		_content_callback.on_ad_dismissed_full_screen_content = _close
		_content_callback.on_ad_failed_to_show_full_screen_content = func(_error): _fail()
		_ad.full_screen_content_callback = _content_callback
		if rc != null: rc.track_fullscreen_ad(_ad, rc_ctx)  # W51a: after the game's callbacks are set, before show
		_ad.show(_reward_listener)
	var request := AdRequest.new()
	request.extras = {"npa": "1"}
	RewardedAdLoader.new().load(_unit, request, _load_callback)

func privacy_options() -> void:
	if not Engine.has_singleton("PoingGodotAdMobUserMessagingPlatform"):
		return
	UserMessagingPlatform.show_privacy_options_form(func(_error): pass)

func _fail() -> void:
	_close()
	failed.emit()

func _close() -> void:
	_token = ""
	if _ad:
		_ad.destroy()
		_ad = null
	finished.emit()
