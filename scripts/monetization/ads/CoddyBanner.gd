extends CanvasLayer
## Bottom banner for the Godot games (owner 2026-10-23: "banner reklamı da ekle"; games/CLAUDE.md §2).
## Created by CoddyAds (`Ads.banner`). HOUSE ONLY (owner 2026-10-25, Emir #125): our own games from ads-config
## `houseAds.banner` (icon + title + slogan + Install, rotating) — there is no AdMob banner any more (no AdView, no unit).
## Two games side by side (Emir #125) in landscape and on wide screens (canvas width ≥ DUAL_MIN_DP dp): two tiles with a
## thin divider, each its own tap target, its own impression / click and its own perAppBudget count; never the same game
## twice, never the host game. Narrow portrait = one game. Same bar height either way.
## STANDARD (owner 2026-10-26): every non-ad-free player sees it at the bottom as soon as the first-run onboarding is over —
## no play-time wait (the old 3-minute rule is gone; no store / AdMob policy needs it for a house banner outside loading
## screens). Never shown: ad_free players (hidden the moment the right arrives), kids games (`[coddy_ads] kids = true`),
## boot / splash / loading screens (loading_active), and whenever the game says no (`Ads.banner_allowed` → false:
## onboarding pager + guide / tutorial, purchase / payment dialogs, full-screen modals). Order: our other games, most
## related first (CoddyHouseRelated.gd, copied next to this file; banner + interstitial creatives pooled), one step
## further on every rotation and every launch (persisted cursor). The game lifts its UI by `Ads.banner_height` (canvas px) —
## connect `Ads.banner_height_changed`; the game's bottom inset = max(safe-area bottom, banner height).
## Bottom safe area (owner 2026-10-26, Döner under the 3-button nav bar): the bar sits ABOVE the bottom safe area incl. the
## Android navigation bar (coddy_ui `UiSafeArea`, visible or not), its background runs on down to the screen edge, and
## `height` counts that gap too — so `height` is everything covered from the screen bottom and max(safe, height) is right.
## House creatives (Patron #108): icon and title come from the SAME creative, and a creative is only eligible once its
## own icon has downloaded (HTTP 200, decodable) and sits in the cache — icons are prefetched (disk-cached, downscaled);
## a creative whose icon fails is skipped, never drawn with an empty box / placeholder / another game's icon. No
## eligible creative → no house banner. Install = the coddy_ui kit primary button (the game's palette accent) when the
## game ships addons/coddy_ui; its label is the game's own "Install" translation, else the INSTALL table, in the game's
## language (ads.ui_lang(), the same source as the creative title).
## No accidental taps (Patron 2026-10-27, W66 Sky Tower: the bar appeared right after the guide, the game's tab bar jumped up
## by its height and a tap meant for a tab opened the store): (1) a NEW bar appears only in a calm moment — no finger down
## and CALM_S without any input — so the layout never moves under a finger; (2) for TAP_GRACE_S after the bar appears or the
## layout under it shifts, taps on it are swallowed (no store, no click); (3) `reserve_slot` (DEFAULT ON since 2026-10-28,
## #165 fleet rollout; a game may set it false) reports the bar height as soon as the bar is allowed, so the game reserves
## the slot and nothing jumps when it fills — also after every window / shop / pager closes (the gate frees the bar, and the
## next bar waits for its calm moment again).
## On a phone only (tests / desktop previews: tap_grace_s / calm_s < 0 = off; set them to test).
## Subtitle (Patron #110): ONLY the creative's `sloganI18n` in the game's language — no other language, no subtitleI18n.
## Missing, longer than SLOGAN_MAX (ja/ko/zh SLOGAN_MAX_CJK) or promising free / trial / a store → no subtitle; the title
## then sits alone, vertically centred. Never a cut "…" text: a slogan that does not fit at SUB_MIN_PX is hidden.

signal height_changed(px: float)

const ROTATE_S := 45.0            ## house banner rotation
const CHECK_S := 0.5              ## visibility re-check (cheap: a few bools)
## House bar size (S2 2026-10-24: the fixed 120 canvas px covered ~1/3 of landscape screens): sized like an AdMob anchored
## adaptive banner — HOUSE_DP physical dp tall, never more than MAX_SHARE of the canvas height; Install ≥ TOUCH_DP dp
## whenever that cap allows it (else it fills the bar, whose whole area is the tap target anyway). Everything inside
## (icon, fonts, paddings) scales with the bar. dp → physical px via the screen dpi, physical px → canvas px via
## viewport visible_rect.y / window_get_size().y. Off-device (force_desktop / previews) the canvas short side = 360 dp.
const HOUSE_DP := 58.0
const MAX_SHARE := 0.12
const TOUCH_DP := 44.0
const TITLE_DP := 16.0            ## title font at full height …
const TITLE_MIN_DP := 14.0        ## … never below this
const SUB_DP := 12.0              ## slogan font at full height …
const SUB_MIN_DP := 11.0          ## … never below this (a slogan that does not fit there is hidden)
const DESKTOP_DPI := 320.0       ## off-device dpi when a test overrides only the canvas scale
const DESKTOP_SHORT_DP := 360.0   ## off-device: the canvas short side is a typical phone's 360 dp
const DUAL_MIN_DP := 600.0        ## canvas width (dp) from which a PORTRAIT screen shows two games; landscape always does
const CHIP_DP := 30.0             ## the small Install chip inside a dual tile (the whole tile is the tap target)
const MIN_TILE_DP := 48.0
const TAP_GRACE_S := 1.5          ## taps on a bar that just appeared / moved are swallowed this long
const CALM_S := 3.0               ## a new bar waits for this long without input (and no finger down)         ## every tap target ≥ 48 dp tall (bar height permitting)

var ads: Node                     ## the CoddyAds autoload (config, ad-free, telemetry, store opening)
var height := 0.0                 ## canvas px currently covered at the bottom (0 = nothing shown)
var kids := false
var force_desktop := false        ## tests / desktop previews: run the house banner off-device
var dpi_override := 0.0           ## tests: screen dpi (0 = DisplayServer / DESKTOP_DPI)
var canvas_per_px_override := 0.0 ## tests: canvas px per physical px (0 = visible_rect / window size)
var canvas_size_override := Vector2.ZERO  ## tests: canvas size (ZERO = the viewport's visible rect)
var tap_grace_s := -1.0          ## tests: < 0 = TAP_GRACE_S on a phone, 0 off-device
var calm_s := -1.0               ## tests: < 0 = CALM_S on a phone, 0 off-device
var clock_offset_s := 0.0        ## tests: moves the banner's clock forward
var reserve_slot := true         ## report the bar height while the bar waits for its calm moment (no layout jump; #165 default)
var _visible_since := -1.0e9
var _last_input := -1.0e9
var _fingers := {}
var bottom_gap_override := -1.0   ## tests: canvas px kept free under the bar (< 0 = the device's bottom safe area)
var side_gap_override := Vector2(-1.0, -1.0)  ## tests: canvas px the content keeps free left / right (x < 0 = the device's)
var _house_sides := Vector2.ZERO
var _house_h := 0.0

var _check := 0.0
var _rot := 0.0
var _index := 0
var _shown_id := ""
var _house: Control = null
var _icon_cache: Dictionary = {}   # url -> Texture2D (only icons that loaded: HTTP 200 + decodable)
var _icon_failed: Dictionary = {}  # url -> true (non-200 / undecodable / empty url: that creative is skipped)
var _icon_loading: Dictionary = {} # url -> true (request in flight)
var prefetch_enabled := true       ## tests switch the network off
var _shown_ids: Array = []         ## promotedAppIds on screen now (1 or 2)
var counts: Dictionary = {}        ## promotedAppId -> impressions this round (perAppBudget per tile/creative)
var _counts_loaded := false
var counts_path := "user://coddy_banner_counts.json"


func _init(owner_ads: Node = null) -> void:
	ads = owner_ads
	layer = 90
	process_mode = Node.PROCESS_MODE_ALWAYS
	name = "CoddyBanner"
	_index = _load_index()
	var rel_path := ""
	if get_script() != null and get_script().resource_path != "":
		rel_path = get_script().resource_path.get_base_dir().path_join("CoddyHouseRelated.gd")
	if rel_path != "" and ResourceLoader.exists(rel_path):
		related = load(rel_path)


var related: Script = null        ## CoddyHouseRelated.gd next to this file (null: backend order, as before)
const INDEX_PATH := "user://coddy_banner_index.txt"
const ICON_DIR := "user://coddyads"
const ICON_PX := 168               ## icons are downscaled to this before caching (2 × the 84 px box)
const KIT_PATH := "res://addons/coddy_ui/ui_kit.gd"
const SAFE_PATH := "res://addons/coddy_ui/ui_safe_area.gd"
const SUB_MIN_PX := 18             ## fit_one_line default floor (house bar passes its own dp-scaled floor)
const SLOGAN_MAX := 34             ## characters, every language …
const SLOGAN_MAX_CJK := 17         ## … except ja / ko / zh (double-width glyphs)
const CJK := ["ja", "ko", "zh"]
## Install label when the game has no "Install" translation of its own (the game's TranslationServer wins).
const INSTALL := {"en": "Install", "de": "Laden", "tr": "Yükle", "fr": "Installer", "es": "Instalar", "pt": "Instalar",
	"ru": "Скачать", "ja": "インストール", "ko": "설치", "zh": "安装", "hi": "इंस्टॉल",
	"id": "Instal"}   # id = Google Play's own Indonesian button word (Corrupt 11th language, 2026-10-27)
## A banner subtitle never promises price / trial / store (store policy + owner rule: no "free" claims). Lower-case match.
const SUB_BANNED := ["free", "trial", "google play", "play store", "app store", "gratis", "gratuit", "kostenlos", "grátis",
	"ücretsiz", "bedava", "deneme", "бесплат", "无料", "無料", "免费", "免費", "무료", "मुफ़्त", "मुफ्त", "gratuito", "uji coba"]


func _ready() -> void:
	kids = bool(ProjectSettings.get_setting("coddy_ads/kids", false))


## Rotation cursor persists across launches so short sessions do not always show the same first creatives. A fresh
## install starts at 0 = the most related game.
func _load_index() -> int:
	var f := FileAccess.open(INDEX_PATH, FileAccess.READ)
	if f == null:
		return 0
	return maxi(0, int(f.get_as_text().strip_edges()))


func _save_index(v: int) -> void:
	var f := FileAccess.open(INDEX_PATH, FileAccess.WRITE)
	if f != null:
		f.store_string(str(v))


func _process(delta: float) -> void:
	_check += delta
	_rot += delta
	if _check < CHECK_S:
		return
	_check = 0.0
	update()


## Re-evaluates what may be shown and shows / hides it. Idempotent; called every CHECK_S and from tests.
func update() -> void:
	_prefetch_icons()
	var waiting := false
	if wanted() == "house":
		if _house == null and not calm():
			waiting = true      # a new bar never pops in under a finger / mid-interaction
		else:
			_show_house()
	else:
		_hide_house()
	var h := _current_height()
	if waiting and reserve_slot:
		h = house_height() + bottom_gap()
	_set_height(h)


func _now() -> float:
	return Time.get_ticks_msec() / 1000.0 + clock_offset_s


func _on_phone() -> bool:
	return ads != null and bool(ads._mobile)


func tap_grace() -> float:
	return tap_grace_s if tap_grace_s >= 0.0 else (TAP_GRACE_S if _on_phone() else 0.0)


## A calm moment for a NEW bar: no finger down and CALM_S without input (off-device: always, unless calm_s is set).
func calm() -> bool:
	var need: float = calm_s if calm_s >= 0.0 else (CALM_S if _on_phone() else 0.0)
	if need <= 0.0:
		return true
	return _fingers.is_empty() and _now() - _last_input >= need


## Watches input without consuming it (the calm moment + the tap grace).
func _input(ev: InputEvent) -> void:
	if ev.device == InputEvent.DEVICE_ID_EMULATION:
		return
	if ev is InputEventScreenTouch:
		if (ev as InputEventScreenTouch).pressed:
			_fingers[(ev as InputEventScreenTouch).index] = true
		else:
			_fingers.erase((ev as InputEventScreenTouch).index)
		_last_input = _now()
	elif ev is InputEventMouseButton:
		if (ev as InputEventMouseButton).pressed:
			_fingers[-1] = true
		else:
			_fingers.erase(-1)
		_last_input = _now()
	elif ev is InputEventScreenDrag or (ev is InputEventMouseMotion and (ev as InputEventMouseMotion).button_mask != 0) or ev is InputEventKey:
		_last_input = _now()


## "house" or "" (nothing). Pure: reads only the rules below. House only — no AdMob banner (Emir #125).
func wanted() -> String:
	if not _allowed():
		return ""
	if not ready_sequence().is_empty():
		return "house"
	return ""


## Emir #139 (Disaster 1.1.7: the 2-tile bar covered the "Loading" text): the banner never shows over a boot / splash /
## loading screen (with no play-time wait since 2026-10-26 this is the only thing between launch and the bar, besides
## the game's onboarding gate). Loading = a game-set flag (`loading`), a boot /
## splash / loading scene (name or file), a coddy_ui loading curtain (ui_loading.gd / LoadingCurtain.gd) visible near
## the top of the tree, or the first SCENE_SETTLE_S after a scene change (Boot -> game).
const SCENE_SETTLE_S := 1.0
const LOADING_WORDS := ["boot", "splash", "loading", "curtain"]
var loading := false              ## a game may set this true while it loads in the background (tests too)
var _scene_id := 0
var _scene_since_ms := 0


func loading_active() -> bool:
	if loading:
		return true
	if not is_inside_tree():
		return false
	var scene := get_tree().current_scene
	if scene != null:
		var id := scene.get_instance_id()
		if id != _scene_id:
			if _scene_id != 0:
				_scene_since_ms = Time.get_ticks_msec()
			_scene_id = id
		if _scene_since_ms > 0 and Time.get_ticks_msec() - _scene_since_ms < int(SCENE_SETTLE_S * 1000.0):
			return true
		var key := (str(scene.name) + " " + scene.scene_file_path.get_file()).to_lower()
		for w in LOADING_WORDS:
			if key.contains(w):
				return true
	return _curtain_visible(get_tree().root, 0)


func _curtain_visible(n: Node, depth: int) -> bool:
	if n == self or n == ads:
		return false
	if n is CanvasItem and not (n as CanvasItem).is_visible_in_tree():
		return false
	var sc = n.get_script()
	while sc != null:
		var path: String = sc.resource_path
		if path.ends_with("ui_loading.gd") or path.ends_with("LoadingCurtain.gd"):
			return true
		sc = sc.get_base_script()
	if depth >= 4:
		return false
	for c in n.get_children():
		if _curtain_visible(c, depth + 1):
			return true
	return false


func _allowed() -> bool:
	if ads == null or kids:
		return false
	if not (ads._mobile or force_desktop):
		return false
	if ads._ad_free():
		return false
	if loading_active():
		return false
	var cb: Callable = ads.banner_allowed
	if cb.is_valid() and not bool(cb.call()):
		return false
	return true


# ---------------------------------------------------------------- house banner

func _house_cfg() -> Dictionary:
	apply_related()
	var h = ads._config.get("houseAds", {}) if ads != null else {}
	var b = h.get("banner", {}) if h is Dictionary else {}
	return b if b is Dictionary else {}


## Orders the ads layer's config by relatedness (CoddyHouseRelated.apply, in place, idempotent): the banner AND the host's
## interstitial layer (CoddyAds or a bridged game's own layer: CoddyBannerHost._config is that layer's dictionary) read it.
func apply_related() -> void:
	if ads == null or related == null:
		return
	var cfg = ads._config
	if cfg is Dictionary and not cfg.is_empty():
		related.apply(cfg, str(ads._app_id))


func _house_sequence() -> Array:
	var cfg := _house_cfg()
	var out: Array = []
	if not bool(cfg.get("enabled", false)):
		return out
	for c in cfg.get("sequence", []):
		if c is Dictionary and str(c.get("promotedAppId", "")) != ads._app_id and not ads._store_url(c).is_empty():
			out.append(c)
	return out


## Creatives whose own icon is cached — the only ones that may be shown.
func ready_sequence() -> Array:
	var out: Array = []
	for c in _house_sequence():
		if _icon_cache.has(str(c.get("iconUrl", ""))):
			out.append(c)
	return out


func current_creative() -> Dictionary:
	var cs := current_creatives()
	return cs[0] if not cs.is_empty() else {}


## Two tiles when the screen is landscape or at least DUAL_MIN_DP dp wide (and ≥ 2 other games are ready).
func dual() -> bool:
	var cs := _canvas_size()
	var u := canvas_px_per_dp()
	return cs.x > cs.y or cs.x / maxf(u, 0.05) >= DUAL_MIN_DP


func tiles_wanted() -> int:
	return mini(2 if dual() else 1, ready_sequence().size())


## The creatives on screen now (1 or 2, never the same game twice, never the host): walk the persistent rotation index
## and skip creatives whose perAppBudget is spent this round; when fewer games than tiles have budget left, a new round
## starts (the budget is a fair share per game, it never empties the bar).
## Advances by the number of tiles every ROTATE_S.
func current_creatives() -> Array:
	var seq := ready_sequence()
	if seq.is_empty():
		return []
	var n := tiles_wanted()
	if _rot >= ROTATE_S:
		_rot = 0.0
		_index += maxi(n, 1)
	_load_counts()
	var budget := int(_house_cfg().get("perAppBudget", 0))
	if budget > 0 and seq.filter(func(c): return int(counts.get(str(c.get("promotedAppId", "")), 0)) < budget).size() < n:
		counts.clear()   # not enough games with budget left for every tile: next round
	var out: Array = []
	var ids: Array = []
	for k in seq.size():
		var c: Dictionary = seq[(_index + k) % seq.size()]
		var id := str(c.get("promotedAppId", ""))
		if ids.has(id) or (budget > 0 and int(counts.get(id, 0)) >= budget):
			continue
		out.append(c)
		ids.append(id)
		if out.size() >= n:
			break
	return out


func _show_house() -> void:
	var cs := current_creatives()
	if cs.is_empty():
		_hide_house()
		return
	var ids: Array = cs.map(func(c): return str(c.get("promotedAppId", "")))
	if _house != null and ids == _shown_ids and _house.theme == _kit_theme() and absf(_house_h - house_height() - bottom_gap()) < 0.5 \
			and _house_sides == side_gaps():
		return
	var fresh: Array = ids.filter(func(id): return not _shown_ids.has(id))
	if _house == null:
		_visible_since = _now()   # the bar appears: its first taps are a grace period
	_hide_house()
	_house = _build_house(cs)
	add_child(_house)
	_shown_ids = ids
	_shown_id = ids[0]
	_save_index(_index + ids.size())  # next launch starts after these creatives
	for i in ids.size():
		if not fresh.has(ids[i]):
			continue   # a relayout (rotation of the screen, theme) is not a new impression
		counts[ids[i]] = int(counts.get(ids[i], 0)) + 1
		ads._track("impression", {"promotedAppId": ids[i], "format": "banner", "level": 3, "slot": i})
	_save_counts()


func _load_counts() -> void:
	if _counts_loaded:
		return
	_counts_loaded = true
	var f := FileAccess.open(counts_path, FileAccess.READ)
	if f != null:
		var j = JSON.parse_string(f.get_as_text())
		if j is Dictionary:
			counts = j


func _save_counts() -> void:
	var f := FileAccess.open(counts_path, FileAccess.WRITE)
	if f != null:
		f.store_string(JSON.stringify(counts))


func _hide_house() -> void:
	if _house != null and is_instance_valid(_house):
		if _house.get_parent() == self:
			remove_child(_house)  # free the name now: the next bar is "HouseBanner" again
		_house.queue_free()
	_house = null
	_shown_id = ""
	_shown_ids = []


func _build_house(cs) -> Control:
	if cs is Dictionary:
		cs = [cs]
	var m := metrics()
	var two: bool = cs.size() >= 2
	var bar := PanelContainer.new()
	bar.name = "HouseBanner"
	bar.set_meta("metrics", m)
	bar.set_meta("tiles", cs.size())
	bar.anchor_left = 0.0
	bar.anchor_right = 1.0
	bar.anchor_top = 1.0
	bar.anchor_bottom = 1.0
	var gap := bottom_gap()
	bar.offset_bottom = -gap               # above the bottom safe area / navigation bar
	bar.offset_top = -float(m.height) - gap
	_house_h = float(m.height) + gap
	var sides := side_gaps()             # landscape phones: the 3-button navigation bar sits at the SIDE (#165)
	_house_sides = sides
	bar.mouse_filter = Control.MOUSE_FILTER_STOP if not two else Control.MOUSE_FILTER_PASS
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(0.07, 0.09, 0.12, 0.96)
	sb.border_width_top = 1
	sb.border_color = Color(1, 1, 1, 0.12)
	sb.content_margin_left = m.hpad + sides.x    # tiles / Install stay off the side safe area; the background runs on
	sb.content_margin_right = m.hpad + sides.y
	sb.content_margin_top = 0 if two else m.vpad
	sb.content_margin_bottom = 0 if two else m.vpad
	sb.expand_margin_bottom = gap          # the bar's background reaches the screen edge (no taps down there)
	bar.add_theme_stylebox_override("panel", sb)
	var kt := _kit_theme()
	if kt != null:
		bar.theme = kt  # kit fonts (all scripts) + PrimaryButton style in the game's palette
	if not two:
		var row := _build_tile(cs[0], m, false)
		bar.add_child(row)
		bar.gui_input.connect(_tile_input.bind(cs[0]))
		return bar
	var tiles := HBoxContainer.new()
	tiles.name = "Tiles"
	tiles.add_theme_constant_override("separation", int(m.gap))
	bar.add_child(tiles)
	for i in 2:
		if i == 1:
			var div := ColorRect.new()
			div.name = "divider"
			div.color = Color(1, 1, 1, 0.14)
			div.custom_minimum_size = Vector2(maxf(1.0, roundf(float(m.u))), 0)
			div.size_flags_vertical = Control.SIZE_FILL
			div.mouse_filter = Control.MOUSE_FILTER_IGNORE
			var dm := MarginContainer.new()
			dm.add_theme_constant_override("margin_top", int(m.vpad + m.u * 4.0))
			dm.add_theme_constant_override("margin_bottom", int(m.vpad + m.u * 4.0))
			dm.mouse_filter = Control.MOUSE_FILTER_IGNORE
			dm.add_child(div)
			tiles.add_child(dm)
		var tile := PanelContainer.new()
		tile.name = "Tile%d" % i
		tile.set_meta("promotedAppId", str(cs[i].get("promotedAppId", "")))
		tile.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		tile.custom_minimum_size.y = minf(roundf(MIN_TILE_DP * float(m.u)), float(m.height))
		tile.mouse_filter = Control.MOUSE_FILTER_STOP
		var tsb := StyleBoxEmpty.new()
		tsb.content_margin_top = m.vpad
		tsb.content_margin_bottom = m.vpad
		tile.add_theme_stylebox_override("panel", tsb)
		tile.add_child(_build_tile(cs[i], m, true))
		tile.gui_input.connect(_tile_input.bind(cs[i]))
		tiles.add_child(tile)
	return bar


func _tile_input(ev: InputEvent, c: Dictionary) -> void:
	# a phone sends the touch AND an emulated click (DEVICE_ID_EMULATION): act on the real one only (W66 10-27: the store
	# opened twice)
	if ev.device == InputEvent.DEVICE_ID_EMULATION:
		return
	var up: bool = (ev is InputEventMouseButton and not ev.pressed and ev.button_index == MOUSE_BUTTON_LEFT) \
		or (ev is InputEventScreenTouch and not ev.pressed)
	if up:
		_click(c)


## One game: icon + (title / slogan) + Install. `small` = a dual tile (Install as a small chip).
func _build_tile(c: Dictionary, m: Dictionary, small: bool) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.name = "row"
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_theme_constant_override("separation", int(m.gap * (0.8 if small else 1.0)))
	var icon := TextureRect.new()
	icon.name = "icon"
	icon.custom_minimum_size = Vector2(m.icon, m.icon)
	icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	icon.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	icon.texture = _icon_cache[str(c.get("iconUrl", ""))]  # ready_sequence() guarantees this creative's own icon
	icon.set_meta("icon_url", str(c.get("iconUrl", "")))
	row.add_child(icon)
	var col := VBoxContainer.new()
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	col.alignment = BoxContainer.ALIGNMENT_CENTER
	col.add_theme_constant_override("separation", 0)
	col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(col)
	var sub := slogan_for(c, _lang())
	var title := str(ads._pick_i18n(c.get("titleI18n", {})))
	# No tofu: the games bundle SUBSET fonts (assets/fonts, built from the game's own text). Remote text the bundle
	# cannot draw → the title falls back to English, the slogan is dropped (data/house_ad_text.json widens the subset).
	var f := _text_font()
	if not can_draw(f, title):
		title = str(c.get("titleI18n", {}).get("en", title))
	if not can_draw(f, sub) or not bool(m.show_sub):
		sub = ""
	if small:   # a dual tile is half the width: start from the short name when there is one
		var st := short_title(c, title, _lang())
		if st != "":
			title = st
	for part in [["title", title, int(m.title_px)], ["subtitle", sub, int(m.sub_px)]]:
		if str(part[1]).is_empty():
			continue
		var l := Label.new()
		l.name = part[0]
		l.text = part[1]
		l.add_theme_font_size_override("font_size", part[2])
		if part[0] == "title":
			l.set_meta("short_title", short_title(c, str(part[1]), _lang()))
		# never "…": the title shrinks (>= TITLE_FLOOR_PX), then switches to the short name; the slogan is hidden if it does not fit
		l.text_overrun_behavior = TextServer.OVERRUN_NO_TRIMMING
		l.autowrap_mode = TextServer.AUTOWRAP_OFF
		l.clip_text = true
		l.custom_minimum_size.x = 1
		l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		l.mouse_filter = Control.MOUSE_FILTER_IGNORE
		l.modulate = Color(1, 1, 1, 1.0 if part[0] == "title" else 0.75)
		var floor_px: int = int(m.title_min_px) if part[0] == "title" else int(m.sub_min_px)
		l.resized.connect(func() -> void: fit_one_line(l, part[2], floor_px))
		col.add_child(l)
	var bm := m
	if small:
		bm = m.duplicate()
		bm.btn_h = minf(roundf(CHIP_DP * float(m.u)), float(m.btn_h))
		bm.btn_px = maxf(roundf(float(m.btn_px) * 0.9), roundf(12.0 * float(m.u)))
		bm.btn_pad = roundf(10.0 * float(m.u))
	var btn := _install_button(c, bm, small)
	btn.name = "install"
	btn.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(btn)
	return row


## One click = one tracked click. CoddyAds.open_store(c, format, level) tracks it itself; an older ad layer (bridged
## games, only _open_store) gets the click tracked here.
func _click(c: Dictionary) -> void:
	if _now() - _visible_since < tap_grace():
		return   # the bar just appeared / moved: this tap was meant for what used to be here
	clicks[str(c.get("promotedAppId", ""))] = int(clicks.get(str(c.get("promotedAppId", "")), 0)) + 1
	if ads.has_method("open_store"):
		ads.open_store(c, "banner", 3)
	else:
		ads._track("click", {"promotedAppId": c.get("promotedAppId"), "format": "banner", "level": 3})
		ads._open_store(c)


var clicks: Dictionary = {}   ## promotedAppId -> clicks this session (tests)


func _text_font() -> Font:
	var kt := _kit_theme()
	if kt != null and kt.default_font != null:
		return kt.default_font
	var pt := ThemeDB.get_project_theme()
	if pt != null and pt.default_font != null:
		return pt.default_font
	return ThemeDB.fallback_font


## True when every character of `text` has a glyph in `f` (its fallbacks included).
static func can_draw(f: Font, text: String) -> bool:
	if f == null:
		return true
	for i in text.length():
		var ch := text.unicode_at(i)
		if ch > 32 and not f.has_char(ch):
			return false
	return true


func _lang() -> String:
	if ads != null and ads.has_method("ui_lang"):
		return ads.ui_lang()
	return TranslationServer.get_locale().substr(0, 2).to_lower()


## The banner subtitle: the creative's sloganI18n[lang] when it is a valid slogan, else "" (title only). No fallback to
## another language or to subtitleI18n.
static func slogan_for(c: Dictionary, lang: String) -> String:
	var m = c.get("sloganI18n", {})
	if not (m is Dictionary) or not m.has(lang):
		return ""
	var t := str(m[lang]).strip_edges()
	var limit := SLOGAN_MAX_CJK if CJK.has(lang) else SLOGAN_MAX
	if t.is_empty() or t.length() > limit or t.contains("…") or t.contains("...") or t.contains("\n"):
		return ""
	return safe_subtitle(t)


## Install label in `lang`: the game's own "Install" translation when it has one, else the INSTALL table (en fallback).
static func install_text(lang: String) -> String:
	if TranslationServer.get_locale().substr(0, 2).to_lower() == lang:
		var own := str(TranslationServer.translate("Install"))
		if own != "" and own != "Install":
			return own
	return str(INSTALL.get(lang, INSTALL.en))


## Subtitle safety: "" when it promises free / trial / a store (the banner then shows the title only).
static func safe_subtitle(text: String) -> String:
	var t := text.strip_edges()
	var low := t.to_lower()
	for w in SUB_BANNED:
		if low.contains(w):
			return ""
	return t


## One line: steps the font down from `start` to `floor_px` until the text fits the label width. A slogan that still
## does not fit is hidden (never "…"); the title keeps its ellipsis as a last resort.
const TITLE_FLOOR_PX := 12   ## on-screen px: the title never shrinks below this
const SHORT_SEPARATORS := [":", " - ", " – ", " — "]


## The game's short name: creative `shortNameI18n[lang]` / `shortName` when configured, else the part before the first
## ':' / ' - ' / ' – ' of the full title. "" when there is nothing shorter.
static func short_title(c: Dictionary, full: String, lang: String) -> String:
	var m = c.get("shortNameI18n", null)
	var cfg := ""
	if m is Dictionary:
		cfg = str(m.get(lang, m.get("en", "")))
	if cfg == "":
		cfg = str(c.get("shortName", ""))
	if cfg != "" and cfg != full:
		return cfg
	var cut := full.length()
	for sep in SHORT_SEPARATORS:
		var i := full.find(sep)
		if i > 0 and i < cut:
			cut = i
	var t := full.substr(0, cut).strip_edges()
	return t if t != "" and t != full else ""


static func fit_one_line(l: Label, start: int, floor_px: int = SUB_MIN_PX) -> int:
	if not is_instance_valid(l) or l.size.x <= 1.0:
		return start
	var f: Font = l.get_theme_font("font")
	var s := start
	if l.name == "title":
		floor_px = mini(floor_px, TITLE_FLOOR_PX)
		if f.get_string_size(l.text, HORIZONTAL_ALIGNMENT_LEFT, -1, floor_px).x > l.size.x \
				and str(l.get_meta("short_title", "")) != "":
			l.text = str(l.get_meta("short_title"))   # full name does not fit even at the floor → the short name
	while s > floor_px and f.get_string_size(l.text, HORIZONTAL_ALIGNMENT_LEFT, -1, s).x > l.size.x:
		s -= 1
	if l.get_theme_font_size("font_size") != s:
		l.add_theme_font_size_override("font_size", s)
	if l.name == "subtitle":
		var fits := f.get_string_size(l.text, HORIZONTAL_ALIGNMENT_LEFT, -1, s).x <= l.size.x + 0.5
		if l.visible != fits:
			l.visible = fits
	return s


func _kit() -> Script:
	return load(KIT_PATH) if ResourceLoader.exists(KIT_PATH) else null


func _kit_theme() -> Theme:
	var k := _kit()
	return k.theme() if k != null else null


## Install: the kit's primary button (palette accent = the game's colour) when the game ships coddy_ui, else a plain one.
func _install_button(c: Dictionary, m: Dictionary = {}, chip := false) -> Button:
	if m.is_empty():
		m = metrics()
	var k := _kit()
	var btn: Button
	if chip:   # dual tile: the button is decoration, the tile is the tap target (one click per tap)
		btn = Button.new()
		btn.text = install_text(_lang())
		btn.mouse_filter = Control.MOUSE_FILTER_IGNORE
		btn.focus_mode = Control.FOCUS_NONE
		if k != null:
			btn.theme_type_variation = "PrimaryButton"
	elif k != null:
		btn = k.button(install_text(_lang()), "primary", "", func() -> void: _click(c), 0)
		btn.set_meta("coddy_no_fit", true)   # the bar sets the size; the kit's word fit must not grow it back
	else:
		btn = Button.new()
		btn.text = install_text(_lang())
		btn.pressed.connect(func() -> void: _click(c))
	var t := _kit_theme()
	# The kit styles are sized for 84 px buttons: scale their paddings to the bar so the button keeps its height.
	for st in ["normal", "hover", "pressed", "disabled", "focus"]:
		var sb: StyleBox = null
		if t != null and t.has_stylebox(st, "PrimaryButton"):
			sb = t.get_stylebox(st, "PrimaryButton")
		elif t != null and t.has_stylebox(st, "Button"):
			sb = t.get_stylebox(st, "Button")
		if sb == null:
			continue
		sb = sb.duplicate()
		sb.content_margin_left = m.btn_pad
		sb.content_margin_right = m.btn_pad
		sb.content_margin_top = 0
		sb.content_margin_bottom = 0
		if sb is StyleBoxFlat:
			var r := mini(int(m.btn_h / 2.0), (sb as StyleBoxFlat).corner_radius_top_left)
			(sb as StyleBoxFlat).set_corner_radius_all(r)
			(sb as StyleBoxFlat).expand_margin_bottom = 0
			(sb as StyleBoxFlat).shadow_size = mini((sb as StyleBoxFlat).shadow_size, 2)
		btn.add_theme_stylebox_override(st, sb)
	btn.add_theme_font_size_override("font_size", int(m.btn_px))
	btn.clip_text = false
	btn.custom_minimum_size = Vector2(install_width(btn.text, t, int(m.btn_px), float(m.btn_pad), float(m.btn_h)), m.btn_h)
	return btn


## Install width: ≥ 2 × the button height, wider when the label (e.g. ja インストール) needs it — never squeezed or cut.
static func install_width(text: String, t: Theme, px: int = 26, pad: float = 24.0, btn_h: float = 58.0) -> float:
	var f: Font = null
	if t != null:
		f = t.get_font("font", "Button") if t.has_font("font", "Button") else t.default_font
	if f == null:
		f = ThemeDB.fallback_font
	return maxf(ceilf(f.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, px).x + 2.0 * pad + 4.0), ceilf(2.0 * btn_h))


## Downloads (or reads from disk) the icon of every house creative not yet cached; failures are remembered and skipped.
func _prefetch_icons() -> void:
	if not prefetch_enabled or ads == null or not (ads._mobile or force_desktop):
		return
	for c in _house_sequence():
		var url := str(c.get("iconUrl", ""))
		if _icon_cache.has(url) or _icon_failed.has(url) or _icon_loading.has(url):
			continue
		if url.is_empty() or not url.begins_with("http"):
			_icon_failed[url] = true
			continue
		var path := _icon_path(url)
		if FileAccess.file_exists(path):
			var tex = ads._texture_from(FileAccess.get_file_as_bytes(path))
			if tex != null:
				_icon_cache[url] = tex
				continue
		if not is_inside_tree():
			return
		var req := HTTPRequest.new()
		add_child(req)
		req.timeout = 15.0
		_icon_loading[url] = true
		req.request_completed.connect(func(_r: int, code: int, _h: PackedStringArray, body: PackedByteArray) -> void:
			req.queue_free()
			on_icon_loaded(url, code, body))
		if req.request(url) != OK:
			req.queue_free()
			on_icon_loaded(url, 0, PackedByteArray())


func _icon_path(url: String) -> String:
	return ICON_DIR + "/icon_" + url.md5_text() + ".png"


## HTTP result of one icon: only a 200 + decodable image is cached (downscaled, also to disk); anything else skips it.
func on_icon_loaded(url: String, code: int, body: PackedByteArray) -> void:
	_icon_loading.erase(url)
	var tex: Texture2D = ads._texture_from(body) if code == 200 else null
	if tex == null:
		_icon_failed[url] = true
		return
	var img := tex.get_image()
	if img != null and maxi(img.get_width(), img.get_height()) > ICON_PX:
		img.resize(ICON_PX, ICON_PX, Image.INTERPOLATE_LANCZOS)
		tex = ImageTexture.create_from_image(img)
	_icon_cache[url] = tex
	if img != null:
		DirAccess.make_dir_recursive_absolute(ICON_DIR)
		img.save_png(_icon_path(url))


## Kept for older callers (bridges / games that still call it): the AdMob banner is gone (house only, Emir #125).
func start_admob() -> void:
	pass


# ---------------------------------------------------------------- height → the game's layout

func _current_height() -> float:
	if _house != null:
		return _house_h
	return 0.0


## Canvas px per dp on this screen (dpi / 160 physical px per dp × canvas px per physical px).
func canvas_px_per_dp() -> float:
	var on_device_: bool = ads != null and ads._mobile
	if not on_device_ and dpi_override <= 0.0 and canvas_per_px_override <= 0.0:
		var cs := _canvas_size()   # off-device: the window is a DESKTOP_SHORT_DP-wide phone (deterministic)
		return maxf(minf(cs.x, cs.y), 1.0) / DESKTOP_SHORT_DP
	var dpi := dpi_override
	if dpi <= 0.0:
		var on_device: bool = ads != null and ads._mobile
		dpi = float(DisplayServer.screen_get_dpi()) if on_device else DESKTOP_DPI
		if dpi <= 0.0:
			dpi = 160.0 * maxf(DisplayServer.screen_get_scale(), 1.0)
	var cpp := canvas_per_px_override
	if cpp <= 0.0:
		cpp = _px_to_canvas(1.0)
	return dpi / 160.0 * cpp


func _canvas_size() -> Vector2:
	if canvas_size_override != Vector2.ZERO:
		return canvas_size_override
	var vp := get_viewport()
	return vp.get_visible_rect().size if vp != null else Vector2(720, 1280)


## Canvas px the bar keeps free under itself: the bottom safe area incl. the Android navigation bar (0 off-device).
func bottom_gap() -> float:
	if bottom_gap_override >= 0.0:
		return bottom_gap_override
	if ads == null or not (ads._mobile or _simulating()):
		return 0.0
	var cs := _canvas_size()
	if ResourceLoader.exists(SAFE_PATH):
		return roundf(float(load(SAFE_PATH).bottom(cs)))
	var safe := DisplayServer.get_display_safe_area()
	var scr := DisplayServer.screen_get_size()
	if safe.size.y <= 0 or scr.y <= 0:
		return 0.0
	return roundf(maxf(0.0, float(scr.y - safe.end.y) * cs.y / float(scr.y)))


## Desktop frames: coddy_ui UiSafeArea.simulate() / CODDY_SAFE_SIM — the bar keeps off the drawn system bars like on that phone.
func _simulating() -> bool:
	return ResourceLoader.exists(SAFE_PATH) and bool(load(SAFE_PATH).simulating())


## Canvas px the bar's CONTENT keeps free on the left / right (#165): the side safe area incl. the 3-button navigation bar,
## which sits at the side in landscape on phones (Android sideHint RIGHT / LEFT); the background still runs to the edge.
## Zero off-device. UiSafeArea caches its JNI reads (REFRESH_MS), so the 0.5 s check stays cheap.
func side_gaps() -> Vector2:
	if side_gap_override.x >= 0.0:
		return side_gap_override
	if ads == null or not (ads._mobile or _simulating()) or not ResourceLoader.exists(SAFE_PATH):
		return Vector2.ZERO
	var e: Vector4 = load(SAFE_PATH).edges(_canvas_size())
	return Vector2(roundf(maxf(0.0, e.x)), roundf(maxf(0.0, e.z)))


## The house bar height (canvas px) on this screen.
func house_height() -> float:
	return float(metrics().height)


func metrics() -> Dictionary:
	return house_metrics(_canvas_size(), canvas_px_per_dp())


## Pure sizing of the house bar for a canvas of `canvas` px where one dp is `u` canvas px. All values in canvas px.
static func house_metrics(canvas: Vector2, u: float) -> Dictionary:
	u = maxf(u, 0.05)
	var cap := floorf(MAX_SHARE * canvas.y)
	var h := minf(roundf(HOUSE_DP * u), cap)
	var k := h / (HOUSE_DP * u)                  # 1 = full size; < 1 = capped by MAX_SHARE
	var btn_h := minf(roundf(TOUCH_DP * u), h)
	var vpad := floorf((h - btn_h) / 2.0)
	var title_px := maxf(roundf(TITLE_DP * u * k), roundf(TITLE_MIN_DP * u))
	var sub_px := maxf(roundf(SUB_DP * u * k), roundf(SUB_MIN_DP * u))
	var line := 1.3                              # line height ≈ 1.3 × font size (kit / fallback fonts)
	var inner := h - 2.0 * maxf(vpad, roundf(2.0 * u))
	var show_sub := (title_px + sub_px) * line <= inner
	if title_px * line > h:                      # extreme cap: title must still fit inside the bar
		title_px = floorf(h / line)
	return {"height": h, "dp": h / u, "u": u, "share": h / maxf(canvas.y, 1.0), "btn_h": btn_h, "btn_dp": btn_h / u,
		"vpad": vpad, "hpad": roundf(10.0 * u), "gap": roundf(10.0 * u), "icon": btn_h,
		"title_px": title_px, "title_min_px": minf(title_px, roundf(TITLE_MIN_DP * u)),
		"sub_px": sub_px, "sub_min_px": roundf(SUB_MIN_DP * u), "show_sub": show_sub,
		"btn_px": maxf(roundf(15.0 * u * k), roundf(13.0 * u)), "btn_pad": roundf(12.0 * u * k)}


func _px_to_canvas(px: float) -> float:
	var vp := get_viewport()
	if vp == null:
		return px
	var win := float(DisplayServer.window_get_size().y)
	return px * vp.get_visible_rect().size.y / win if win > 0.0 else px


func _set_height(h: float) -> void:
	if absf(h - height) < 0.5:
		return
	if h > 0.0 and height > 0.0:
		_visible_since = _now()   # the layout under a showing bar moved: a fresh grace period
	height = h
	height_changed.emit(h)
