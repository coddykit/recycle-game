extends Node
## House-first interstitials for the games. Template: games/tools/ads/CoddyAds.gd — copy unchanged into each game and
## register it as the `Ads` autoload (after Store). Game code only calls:
##   Ads.ad_free_check = func() -> bool: return <player has the ad_free entitlement>
##   Ads.interstitial("placement_name")   # at a natural break; returns at once, the ad (if any) overlays the game
##   if Ads.privacy_options_required(): <Settings button "Privacy options"> → Ads.show_privacy_options()   # Google UMP, EEA/UK (required)
## signal `closed` fires when a shown interstitial goes away.
##
## Order (base skill coddy-ads, same Supabase backend as the RN apps): our OTHER games first — the CoddyAds sequence
## for this host from `ads-config`, counted on-device (perAppBudget × min(sequence, 10), reset after resetDays, wiped when the
## config version changes) — then AdMob once that budget is spent. Creatives without a store link for this platform
## are skipped (a game not yet on the App Store is never promoted on iOS).
## Pacing: never in the first INSTALL_GRACE_S of total play, the first SESSION_GRACE_S of a session, or within
## MIN_GAP_S of the previous interstitial. Mobile only (Android / iOS); desktop/Steam/editor = no-op.
## COPY OF games/tools/ads/CoddyAds.gd (2026-10-19) + the guards train-baron's copy carries that the template lacks:
## ads-config `adsEnabled=false` switches house AND AdMob ads off, a double tap cannot close a house ad twice, and an
## AdMob close callback of a stale ad is ignored. Borderline Empire (2026-10-19) registers it as the `Ads` autoload but
## does NOT start AdMob (UMP consent form + SDK) at boot: Monetization calls `Ads.start_admob()` once the intro and the
## tutorial are over and only for a player who is not ad-free, so a first-time player never meets a consent form
## early. Requests are non-personalised (`npa`), like the game's rewarded sponsor ad. `is_showing()` tells the game
## when an ad covers the screen. House ads work without any of that (no consent needed).
## Settings in project.godot [coddy_ads]: app_id, admob_interstitial_android, admob_interstitial_ios,
## max_ad_content_rating (G / PG / T / MA). Empty unit id → Google's test unit in debug builds, NO AdMob at all in
## release builds (house ads only; no consent form either). AdMob: UMP consent first (EEA form),
## non-personalised without ATT on iOS. Requires the Poing Studios AdMob plugin (addons/admob).

signal closed

const SUPABASE := "https://zkzmcpjowgttetrscpjt.supabase.co"
const ANON_KEY := "sb_publishable_NmKNLcE663RhBtAyKijTQg_p4xDUSmQ"  # public by design (RLS), same as @coddykit/ui
const TEST_UNIT_ANDROID := "ca-app-pub-3940256099942544/1033173712"
const TEST_UNIT_IOS := "ca-app-pub-3940256099942544/4411468910"
const INSTALL_GRACE_S := 600.0
const SESSION_GRACE_S := 300.0
const MIN_GAP_S := 240.0
const DIR := "user://coddyads"
const STATE_PATH := DIR + "/state.json"
const CONFIG_PATH := DIR + "/config.json"

var ad_free_check: Callable = func() -> bool: return false

var _mobile := OS.has_feature("android") or OS.has_feature("ios")
var _platform := "ios" if OS.has_feature("ios") else "android"
var _app_id := ""
var _config: Dictionary = {}
var _state := {"device_id": "", "count": 0, "reset_at": 0.0, "version": "", "play_s": 0.0, "last_shown": 0.0}
var _session_s := 0.0
var quiet_until := 0.0 # session-only extra interval after a completed rewarded video
var _showing := false
var _hero_cache: Dictionary = {}  # url -> Texture2D
var _admob_ad: Object = null      # InterstitialAd
## RevenueCat ad tracking (W51a, games/CLAUDE.md §5b; scripts/monetization/rc_ad_tracking.gd = shared RcAdTracking.gd):
## real AdMob callbacks of the interstitial (here) and of the rewarded video (rewarded_ads.gd) → RevenueCat. House ads
## are never sent; no manual Firebase ad_impression. ads-config `rcAdTracking: false` = off.
var rc_ads := RcAdTracking.new()
var _admob_loading := false
var _admob_ready := false
var _presented_ad: Object = null
var _paused_before := false
var _save_timer := 0.0


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	if not _mobile:
		return
	_app_id = str(ProjectSettings.get_setting("coddy_ads/app_id", ""))
	DirAccess.make_dir_recursive_absolute(DIR)
	_load_state()
	_load_cached_config()
	_fetch_config()


## The game says AdMob may start now (consent form first where Google requires it). Idempotent.
func start_admob() -> void:
	if _mobile and not _admob_requested and not _admob_started and not _ad_free() and _admob_available() and not ("debug" if OS.is_debug_build() else str(ProjectSettings.get_setting("coddy_ads/admob_rewarded_" + _platform, ""))).is_empty():   # AdMob = rewarded only (Emir #125)
		_admob_requested = true
		_start_admob()


## A house ad or an AdMob interstitial is on screen.
func is_showing() -> bool:
	return _showing


func _process(delta: float) -> void:
	if not _mobile or get_tree().paused:
		return
	_session_s += delta
	_state.play_s += delta
	_save_timer += delta
	if _save_timer > 30.0:
		_save_timer = 0.0
		_save_state()


func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_PAUSED or what == NOTIFICATION_WM_CLOSE_REQUEST:
		if _mobile:
			_save_state()


## Ask for an interstitial at a natural break. Never blocks the caller; does nothing when pacing, ad-free or no fill.
func interstitial(placement: String = "") -> void:
	if not _mobile or _showing or _ad_free() or not bool(_config.get("adsEnabled", false)):
		return
	if _state.play_s < INSTALL_GRACE_S or _session_s < SESSION_GRACE_S:
		return
	if _now() - float(_state.last_shown) < MIN_GAP_S or _now() < quiet_until:
		return
	var creative := _next_house_creative()
	if creative.is_empty():
		return   # house only (Emir #125): no house creative = no ad, never an AdMob fallback
	var frames := _house_frames(creative)
	if frames.is_empty():
		_prefetch_hero(creative)  # not cached yet: no ad this break, ready for the next one
		return
	_show_house(creative, frames[0], placement)


# ---------------------------------------------------------------- house ads (CoddyAds)

func _house_cfg() -> Dictionary:
	var h = _config.get("houseAds", {})
	return h.get("interstitial", {}) if h is Dictionary else {}


func _eligible_sequence() -> Array:
	var cfg := _house_cfg()
	var out: Array = []
	if not bool(cfg.get("enabled", false)):
		return out
	for c in cfg.get("sequence", []):
		if c is Dictionary and str(c.get("promotedAppId", "")) != _app_id and not _store_url(c).is_empty() \
				and not str(c.get("interstitialImageUrl", "")).is_empty():
			out.append(c)
	return out


## Interstitials per cycle = perAppBudget × min(sequence, HOUSE_CAP_GAMES) (owner 2026-10-28; kit games/tools/ads/CoddyAds.gd):
## the sequence is now every one of our other games (CoddyHouseRelated TOP_N = 0), but the per-host interstitial total
## stays at the old 10-game maximum (budget 3 → 30 per resetDays, not 60). The walk still starts at the most related game.
const HOUSE_CAP_GAMES := 10


func _next_house_creative() -> Dictionary:
	var seq := _eligible_sequence()
	var cfg := _house_cfg()
	var budget := int(cfg.get("perAppBudget", 0))
	if seq.is_empty() or budget <= 0:
		return {}
	var now := _now()
	if now >= float(_state.reset_at):
		var days := int(cfg.get("resetDays", 14))
		_state.count = 0
		_state.reset_at = now + days * 86400.0 if days > 0 else 1.0e15
	var total := budget * mini(seq.size(), HOUSE_CAP_GAMES)
	var count := int(_state.count)
	if count >= total:
		return {}
	var rotation := count / total if total > 0 else 0
	return seq[(count + rotation) % seq.size()]


func _store_url(c: Dictionary) -> String:
	var v = c.get("appStoreUrl" if _platform == "ios" else "playUrl")
	return "" if v == null else str(v)


func _pick_i18n(m) -> String:
	if not (m is Dictionary) or m.is_empty():
		return ""
	var lang := TranslationServer.get_locale().substr(0, 2).to_lower()
	for k in [lang, "en"]:
		if m.has(k) and str(m[k]) != "":
			return str(m[k])
	return str(m.values()[0])


func _show_house(c: Dictionary, tex: Texture2D, placement: String) -> void:
	_showing = true
	_state.count = int(_state.count) + 1
	_mark_shown()
	_track("impression", {"promotedAppId": c.get("promotedAppId"), "format": "interstitial", "level": 1, "placement": placement})
	_pause_game()
	# House interstitial card (kit games/tools/ads/CoddyInterstitial.gd, Emir #125 + round 2): the promoted game's
	# screenshots in the player's language for this orientation, icon + title + slogan + Install, 3 s countdown → X.
	var Card = load("res://scripts/monetization/ads/CoddyInterstitial.gd")
	var frames := _house_frames(c)
	if frames.is_empty() and tex != null:
		frames = [tex]
	var icon: Texture2D = _hero_cache.get(str(c.get("iconUrl", "")))
	var card = Card.new(self, c, frames, icon, not Card.low_device())
	card.finished.connect(func(opened: bool) -> void:
		if opened:
			_open_store(c)
		_resume_game()
		_showing = false
		_save_state()
		_trim_house_cache()
		_prefetch_next()
		closed.emit())
	add_child(card)


## Cached frames of `c` for this screen (player's language + orientation; Low device: the first only).
func _house_frames(c: Dictionary) -> Array:
	var Card = load("res://scripts/monetization/ads/CoddyInterstitial.gd")
	var out: Array = []
	for u in Card.screen_heroes(self, c):
		if _hero_cache.has(u):
			out.append(_hero_cache[u])
	if out.size() > 1 and Card.low_device():
		out = [out[0]]
	return out


## Memory: after a card only the next creative's frames + icons stay in memory (disk cache keeps the rest).
func _trim_house_cache() -> void:
	var Card = load("res://scripts/monetization/ads/CoddyInterstitial.gd")
	var nxt := _next_house_creative()
	var keep: Array = Card.screen_heroes(self, nxt) if not nxt.is_empty() else []
	for k in _hero_cache.keys():
		if not keep.has(k) and not str(k).contains("icon"):
			_hero_cache.erase(k)


func _open_store(c: Dictionary) -> void:
	var url := _store_url(c)
	var req := HTTPRequest.new()
	add_child(req)
	req.timeout = 4.0
	req.request_completed.connect(_on_click_tracked.bind(req, url))
	var body := {"deviceId": _state.device_id, "promotedAppId": c.get("promotedAppId"), "destinationType": "store",
		"format": "interstitial", "level": 1, "platform": _platform, "deviceModel": OS.get_model_name()}
	if req.request(SUPABASE + "/functions/v1/ads-track/click", _headers(), HTTPClient.METHOD_POST, JSON.stringify(body)) != OK:
		req.queue_free()
		OS.shell_open(url)


func _on_click_tracked(_r: int, code: int, _h: PackedStringArray, body: PackedByteArray, req: HTTPRequest, url: String) -> void:
	req.queue_free()
	var final_url := url
	if code == 200 and _platform == "android":
		var j = JSON.parse_string(body.get_string_from_utf8())
		if j is Dictionary and j.get("androidReferrer") != null:
			final_url += ("&" if "?" in url else "?") + "referrer=" + str(j.androidReferrer).uri_encode()
	OS.shell_open(final_url)


func _prefetch_next() -> void:
	var c := _next_house_creative()
	if not c.is_empty():
		_prefetch_hero(c)


func _prefetch_hero(c: Dictionary) -> void:
	_prefetch_url(str(c.get("iconUrl", "")))   # the card draws the icon too
	for u in load("res://scripts/monetization/ads/CoddyInterstitial.gd").screen_heroes(self, c):
		_prefetch_url(u)


func _prefetch_url(url: String) -> void:
	if url.is_empty() or _hero_cache.has(url):
		return
	var path := DIR + "/hero_" + url.md5_text()
	if FileAccess.file_exists(path):
		var tex := _texture_from(FileAccess.get_file_as_bytes(path))
		if tex:
			_hero_cache[url] = tex
			return
	var req := HTTPRequest.new()
	add_child(req)
	req.timeout = 15.0
	req.request_completed.connect(_on_hero_loaded.bind(req, url, path))
	if req.request(url) != OK:
		req.queue_free()


func _on_hero_loaded(_r: int, code: int, _h: PackedStringArray, body: PackedByteArray, req: HTTPRequest, url: String, path: String) -> void:
	req.queue_free()
	if code != 200:
		return
	var tex := _texture_from(body)
	if tex:
		var img := tex.get_image()   # ≤ 512 px in memory (weak devices)
		if img != null and maxi(img.get_width(), img.get_height()) > 512:
			var k := 512.0 / maxi(img.get_width(), img.get_height())
			img.resize(maxi(1, int(img.get_width() * k)), maxi(1, int(img.get_height() * k)), Image.INTERPOLATE_LANCZOS)
			tex = ImageTexture.create_from_image(img)
		_hero_cache[url] = tex
		var f := FileAccess.open(path, FileAccess.WRITE)
		if f:
			f.store_buffer(body)


func _texture_from(bytes: PackedByteArray) -> Texture2D:
	if bytes.size() < 12:
		return null
	var img := Image.new()
	var err := ERR_FILE_UNRECOGNIZED
	if bytes[0] == 0x89 and bytes[1] == 0x50:
		err = img.load_png_from_buffer(bytes)
	elif bytes[0] == 0xFF and bytes[1] == 0xD8:
		err = img.load_jpg_from_buffer(bytes)
	elif bytes.slice(8, 12).get_string_from_ascii() == "WEBP":
		err = img.load_webp_from_buffer(bytes)
	return ImageTexture.create_from_image(img) if err == OK else null


# ---------------------------------------------------------------- config + telemetry

func _headers() -> PackedStringArray:
	return PackedStringArray(["Content-Type: application/json", "X-App-ID: " + _app_id, "apikey: " + ANON_KEY,
		"Authorization: Bearer " + ANON_KEY])


func _fetch_config() -> void:
	if _app_id.is_empty():
		return
	var req := HTTPRequest.new()
	add_child(req)
	req.timeout = 10.0
	req.request_completed.connect(_on_config_loaded.bind(req))
	if req.request(SUPABASE + "/functions/v1/ads-config", _headers()) != OK:
		req.queue_free()


func _on_config_loaded(_r: int, code: int, _h: PackedStringArray, body: PackedByteArray, req: HTTPRequest) -> void:
	req.queue_free()
	if code != 200:
		return
	var text := body.get_string_from_utf8()
	var j = JSON.parse_string(text)
	if j is Dictionary:
		_apply_config(j)
		var f := FileAccess.open(CONFIG_PATH, FileAccess.WRITE)
		if f:
			f.store_string(text)


func _load_cached_config() -> void:
	if FileAccess.file_exists(CONFIG_PATH):
		var j = JSON.parse_string(FileAccess.get_file_as_string(CONFIG_PATH))
		if j is Dictionary:
			_config = j if bool(j.get("adsEnabled", false)) else {}
			rc_ads.enabled = bool(j.get("rcAdTracking", true))
	_prefetch_next()


func _apply_config(j: Dictionary) -> void:
	var m := get_node_or_null("/root/Monetization")
	if m != null and "rewarded_remote" in m:
		m.rewarded_remote = bool(j.get("rewardedEnabled", true)) # W32 remote kill switch (absent = on)
	rc_ads.enabled = bool(j.get("rcAdTracking", true)) # W51a kill switch (absent = on)
	if not bool(j.get("adsEnabled", false)):
		_config = {}
		return
	_config = j
	var v := str(j.get("version", ""))
	if v != "" and v != str(_state.version):
		_state.version = v
		_state.count = 0
		_state.reset_at = 0.0
		_save_state()
	_prefetch_next()


func _track(kind: String, body: Dictionary) -> void:
	var req := HTTPRequest.new()
	add_child(req)
	req.timeout = 8.0
	req.request_completed.connect(func(_r, _c, _h, _b) -> void: req.queue_free(), CONNECT_ONE_SHOT)
	body["deviceId"] = _state.device_id
	if req.request(SUPABASE + "/functions/v1/ads-track/" + kind, _headers(), HTTPClient.METHOD_POST, JSON.stringify(body)) != OK:
		req.queue_free()


# ---------------------------------------------------------------- AdMob fallback

func _admob_available() -> bool:
	return ResourceLoader.exists("res://addons/admob/gdscript/src/api/MobileAds.gd")


func _start_admob() -> void:
	var rc := RequestConfiguration.new()
	rc.max_ad_content_rating = str(ProjectSettings.get_setting("coddy_ads/max_ad_content_rating", "PG"))
	MobileAds.set_request_configuration(rc)
	var params := ConsentRequestParameters.new()
	UserMessagingPlatform.consent_information.update(params, _on_consent_info, _on_consent_error)


func _on_consent_info() -> void:
	if not UserMessagingPlatform.consent_information.get_is_consent_form_available():
		_init_admob()
		return
	UserMessagingPlatform.load_consent_form(_on_consent_form_loaded, _on_consent_error)


func _on_consent_form_loaded(form) -> void:
	if UserMessagingPlatform.consent_information.get_consent_status() == ConsentInformation.ConsentStatus.REQUIRED:
		form.show(_on_consent_error)
	else:
		_init_admob()


func _on_consent_error(_error = null) -> void:
	_init_admob()


## Google UMP: a player in the EEA / UK must be able to change the ad consent later. True once the consent info says so
## (AdMob started on a phone); the game then shows Settings → "Privacy options".
func privacy_options_required() -> bool:
	if not _mobile or not _admob_started or not _admob_available():
		return false
	return UserMessagingPlatform.consent_information.get_privacy_options_requirement_status() == ConsentInformation.PrivacyOptionsRequirementStatus.REQUIRED


## Opens Google's privacy options form (the consent choices) again.
func show_privacy_options() -> void:
	if privacy_options_required():
		UserMessagingPlatform.show_privacy_options_form(func(_form_error): pass)


var _admob_started := false
var _admob_requested := false
var _admob_inited := false # Google Mobile Ads SDK (plugin 5.x) throws IllegalStateException — the app dies — if an ad
# is loaded before MobileAds.initialize has completed (Play rejection of corrupt-tycoon 1.0.2, 2026-10-16)


func _init_admob() -> void:
	if _admob_started:
		return
	_admob_started = true
	var l := OnInitializationCompleteListener.new()
	l.on_initialization_complete = _on_admob_initialized
	MobileAds.set_publisher_first_party_id_enabled(false)  # was set by the rewarded ad's own init (one SDK owner now)
	MobileAds.initialize(l)


func _on_admob_initialized(_status = null) -> void:
	_admob_inited = true
	var waiters := _sdk_waiters.duplicate()
	_sdk_waiters.clear()
	for w in waiters:
		(w as Callable).call()


var _sdk_waiters: Array = []
## This autoload is the ONLY owner of the Google Mobile Ads SDK in the game (UMP consent + MobileAds.initialize, once).
## `done` runs when the SDK has finished initialising (at once when it already has); a caller that arrives while it is
## starting just waits. The rewarded sponsor ad (rewarded_ads.gd) goes through here instead of initialising the SDK a
## second time: the Poing plugin keeps ONE static init listener, so a second MobileAds.initialize swallowed the first
## caller's callback, and loading any ad before initialisation completes kills the app with IllegalStateException
## "MobileAds.initialize must be called before using the Google Mobile Ads SDK" (Train Baron 1.1.2 on Play Vitals,
## 2026-10-20). Returns false when AdMob can never start here (desktop / editor / no plugin).
func when_sdk_ready(done: Callable) -> bool:
	if not _mobile or not _admob_available():
		return false
	if _admob_inited:
		done.call()
		return true
	_sdk_waiters.append(done)
	if not _admob_requested and not _admob_started:
		_admob_requested = true  # the rewarded ad needs the SDK even where the interstitial unit is empty
		_start_admob()
	return true


func _unit_id() -> String:
	var key := "coddy_ads/admob_interstitial_" + _platform
	var id := str(ProjectSettings.get_setting(key, ""))
	if id.is_empty() and OS.is_debug_build():
		return TEST_UNIT_IOS if _platform == "ios" else TEST_UNIT_ANDROID
	return id


func _load_admob() -> void:
	if not _admob_inited or _admob_loading or _admob_ready or _ad_free() or not bool(_config.get("adsEnabled", false)) or _unit_id().is_empty():
		return
	_admob_loading = true
	rc_ads.flush()
	var cb := InterstitialAdLoadCallback.new()
	cb.on_ad_loaded = _on_admob_loaded.bind(rc_ads.new_context(_unit_id(), "interstitial"))
	cb.on_ad_failed_to_load = _on_admob_failed.bind(_unit_id())
	var request := AdRequest.new()
	request.extras = {"npa": "1"}
	InterstitialAdLoader.new().load(_unit_id(), request, cb)


func _on_admob_loaded(ad, rc_ctx := {}) -> void:
	_admob_loading = false
	_admob_ready = true
	_admob_ad = ad
	if not rc_ctx.is_empty():
		rc_ads.track_fullscreen_ad(ad, rc_ctx)  # W51a: RevenueCat events, bound before show


func _on_admob_failed(_err, unit := "") -> void:
	_admob_loading = false
	if unit != "":
		rc_ads.failed_load_error(unit, "interstitial", _err)
	get_tree().create_timer(90.0, true, false, true).timeout.connect(_load_admob)


func _show_admob(_placement: String) -> void:
	if not _admob_ready or _admob_ad == null:
		if _admob_started:
			_load_admob()
		return
	var ad = _admob_ad
	rc_ads.placement_for(ad, _placement)
	_presented_ad = ad
	_admob_ad = null
	_admob_ready = false
	_showing = true
	_mark_shown()
	_pause_game()
	ad.full_screen_content_callback.on_ad_dismissed_full_screen_content = _on_admob_done.bind(ad)
	ad.full_screen_content_callback.on_ad_failed_to_show_full_screen_content = func(_e) -> void: _on_admob_done(ad)
	ad.show()


func _on_admob_done(ad) -> void:
	if ad == null or ad != _presented_ad:
		return
	_presented_ad = null
	ad.destroy()
	_resume_game()
	_showing = false
	_save_state()
	_load_admob()
	closed.emit()


# ---------------------------------------------------------------- helpers

func _ad_free() -> bool:
	return bool(ad_free_check.call()) if ad_free_check.is_valid() else false


func _pause_game() -> void:
	_paused_before = get_tree().paused
	get_tree().paused = true


func _resume_game() -> void:
	get_tree().paused = _paused_before


func postpone_after_rewarded() -> void:
	quiet_until = maxf(maxf(_now(), float(_state.last_shown) + MIN_GAP_S), quiet_until) + MIN_GAP_S


func _mark_shown() -> void:
	_state.last_shown = _now()
	_save_state()


func _now() -> float:
	return Time.get_unix_time_from_system()


func _load_state() -> void:
	if FileAccess.file_exists(STATE_PATH):
		var j = JSON.parse_string(FileAccess.get_file_as_string(STATE_PATH))
		if j is Dictionary:
			for k in j:
				_state[k] = j[k]
	if str(_state.device_id).is_empty():
		var c := Crypto.new()
		_state.device_id = c.generate_random_bytes(16).hex_encode()
		_save_state()


func _save_state() -> void:
	var f := FileAccess.open(STATE_PATH, FileAccess.WRITE)
	if f:
		f.store_string(JSON.stringify(_state))
