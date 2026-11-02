extends Node
## Thin bridge that lets a game with its OWN ad layer (an older CoddyAds copy or a custom Ads autoload) show the kit
## CoddyBanner (games/tools/ads/CoddyBanner.gd) at the bottom — without touching the game's interstitial / rewarded /
## save / IAP / economy code (Emir #112). Copy CoddyBanner.gd + this file side by side into the game, then e.g. in the
## root UI script:
##
##   var host = preload("res://scripts/ads/CoddyBannerHost.gd").new()
##   host.source = Ads                                   # the game's ad autoload (reads _config / _track / _open_store …)
##   host.ad_free = func() -> bool: return Store.is_ad_free()   # optional; default source._ad_free()
##   host.play_s = func() -> float: return GameState.play_seconds # optional; default source._state.play_s
##   host.gate = func() -> bool: return ui.modal_open() == ""   # modal / onboarding / shop / payment / pager
##   host.lang = func() -> String: return I18n.lang               # optional; default TranslationServer locale
##   host.banner_height_changed.connect(func(px): ui.set_bottom_inset(px))
##   add_child(host)
##
## The CoddyBanner sees this node as its `ads`: every member it reads is forwarded to `source` when the source has it,
## otherwise the bridge supplies it (and fetches ads-config itself — same endpoint, X-App-ID = [coddy_ads] app_id).
## AdMob banner stays OFF here (`_admob_inited` is always false): these games have no AdMob banner unit → house only.
## Relatedness (owner 2026-10-26): the banner orders `_config` in place with CoddyHouseRelated.gd (copy it next to these
## two files) — `_config` IS the game layer's dictionary, so the game's own house interstitial rotation (`seq[count % n]`)
## follows the same most-related-first order without touching its code.

signal banner_height_changed(px: float)

const SUPABASE := "https://zkzmcpjowgttetrscpjt.supabase.co"
const ANON_KEY := "sb_publishable_NmKNLcE663RhBtAyKijTQg_p4xDUSmQ"  # public by design (RLS), same as CoddyAds.gd

var source: Object = null                 ## the game's own ad layer (may be null → bridge fetches config, no telemetry)
var ad_free: Callable                     ## () -> bool  (Remove Ads / big pack / legacy payer)
var play_s: Callable                      ## () -> float (total play seconds) — legacy: the banner no longer waits for play time
var gate: Callable                        ## () -> bool: false while a modal / onboarding / shop / payment / pager is up
var lang: Callable                        ## () -> String (2-letter UI language)
var font: Font = null                     ## optional: the game's text font for the banner (e.g. a weighted FontVariation when
                                          ## the kit theme would draw a variable font at its thin default instance)
var fetch_enabled := true                 ## tests switch the network off
var banner = null                         ## the CoddyBanner child
var banner_height := 0.0

var _own_config: Dictionary = {}
var _own_state: Dictionary = {"play_s": 0.0}

var _config: Dictionary:
	get:
		if source != null and "_config" in source and source._config is Dictionary and not source._config.is_empty():
			return source._config
		return _own_config
	set(v):
		_own_config = v
		if source != null and "_config" in source:
			source._config = v
var _state: Dictionary:
	get:
		if play_s.is_valid():
			_own_state["play_s"] = float(play_s.call())
			return _own_state
		if source != null and "_state" in source and source._state is Dictionary:
			return source._state
		return _own_state
var _mobile: bool:
	get:
		if source != null and "_mobile" in source:
			return bool(source._mobile)
		return OS.has_feature("android") or OS.has_feature("ios")
var _platform: String:
	get:
		if source != null and "_platform" in source:
			return str(source._platform)
		return "ios" if OS.has_feature("ios") else "android"
var _app_id: String:
	get:
		if source != null and "_app_id" in source and str(source._app_id) != "":
			return str(source._app_id)
		return str(ProjectSettings.get_setting("coddy_ads/app_id", ""))
## What CoddyBanner calls every CHECK_S: the game's gate AND no interstitial / rewarded of the game's layer on screen.
var banner_allowed: Callable:
	get:
		return _allowed_now
var _admob_inited: bool:
	get:
		return false   # no AdMob banner unit in these games: the AdMob branch stays closed


func _ready() -> void:
	var path: String = get_script().resource_path.get_base_dir().path_join("CoddyBanner.gd")
	banner = load(path).new(self)
	banner.height_changed.connect(func(px: float) -> void:
		banner_height = px
		banner_height_changed.emit(px))
	add_child(banner)
	if fetch_enabled and _config.is_empty():
		_fetch_config.call_deferred()


func _process(_d: float) -> void:
	if font != null and banner != null and banner._house != null and not banner._house.has_meta("host_font"):
		apply_font(banner._house)


## The game's font on every banner label + the Install button (sizes untouched).
func apply_font(bar: Control) -> void:
	bar.set_meta("host_font", true)
	for n in bar.find_children("*", "Label", true, false) + bar.find_children("*", "Button", true, false):
		(n as Control).add_theme_font_override("font", font)
		if n is Label:
			(n as Label).resized.emit()   # CoddyBanner re-fits title / slogan to one line with the new font


func _allowed_now() -> bool:
	if source != null and "_showing" in source and bool(source._showing):
		return false
	return bool(gate.call()) if gate.is_valid() else true


func _ad_free() -> bool:
	if ad_free.is_valid():
		return bool(ad_free.call())
	if source != null and source.has_method("_ad_free"):
		return bool(source._ad_free())
	return false


func _admob_available() -> bool:
	return false


func ui_lang() -> String:
	if lang.is_valid():
		return str(lang.call()).substr(0, 2).to_lower()
	if source != null and source.has_method("ui_lang"):
		return str(source.ui_lang())
	return TranslationServer.get_locale().substr(0, 2).to_lower()


func _pick_i18n(m) -> String:
	if not (m is Dictionary) or m.is_empty():
		return ""
	for k in [ui_lang(), "en"]:
		if m.has(k) and str(m[k]) != "":
			return str(m[k])
	return str(m.values()[0])


func _store_url(c: Dictionary) -> String:
	if source != null and source.has_method("_store_url"):
		return str(source._store_url(c))
	return str(c.get("appStoreUrl" if _platform == "ios" else "playUrl", ""))


func _open_store(c: Dictionary) -> void:
	if source != null and source.has_method("_open_store"):
		source._open_store(c)
	else:
		var u := _store_url(c)
		if u != "":
			OS.shell_open(u)


func _track(kind: String, body: Dictionary) -> void:
	if source != null and source.has_method("_track"):
		source._track(kind, body)


func _texture_from(bytes: PackedByteArray) -> Texture2D:
	if source != null and source.has_method("_texture_from"):
		return source._texture_from(bytes)
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


## Only when the game's own layer has no config: GET ads-config (same endpoint / headers as CoddyAds.gd).
## The game's own layer (older CoddyAds copy) fetches once at launch and never retries; when that fetch fails
## (network not up yet, Kaiten Sushi 2026-10-24) the bridge keeps trying with backoff (CONFIG_RETRY_S, doubling up
## to CONFIG_RETRY_MAX_S) until a 200 arrives or the game's layer got a config meanwhile. One timer, no per-frame cost.
const CONFIG_RETRY_S := 10.0
const CONFIG_RETRY_MAX_S := 300.0
var config_retry_s := CONFIG_RETRY_S   ## next retry delay (tests read it)
var config_retries := 0


func _fetch_config() -> void:
	if not _mobile or _app_id == "" or not _config.is_empty():
		return
	var req := HTTPRequest.new()
	add_child(req)
	req.timeout = 10.0
	req.request_completed.connect(_on_config_loaded.bind(req))
	if req.request(SUPABASE + "/functions/v1/ads-config",
			["Content-Type: application/json", "X-App-ID: " + _app_id, "apikey: " + ANON_KEY, "Authorization: Bearer " + ANON_KEY]) != OK:
		req.queue_free()
		_retry_config()


func _retry_config() -> void:
	if not is_inside_tree():
		return
	var delay := config_retry_s
	config_retry_s = minf(config_retry_s * 2.0, CONFIG_RETRY_MAX_S)
	config_retries += 1
	get_tree().create_timer(delay, true, false, true).timeout.connect(_fetch_config)


func _on_config_loaded(_r: int, code: int, _h: PackedStringArray, body: PackedByteArray, req: HTTPRequest) -> void:
	req.queue_free()
	var j = JSON.parse_string(body.get_string_from_utf8()) if code == 200 else null
	if not j is Dictionary:
		_retry_config()
		return
	config_retry_s = CONFIG_RETRY_S
	if _config.is_empty():
		_own_config = j
