extends Node
## Sponsor bonus + the other opt-in rewarded placements, delivery of bought packs, the shop's offer schedule and the house-first
## interstitial breaks. Core play stays free. Purchases themselves live in the `Iap` autoload (addons/coddy_iap):
## `ad_free` (Remove Ads, Kingpin Edition, Ultimate Bundle, Empire Pass - or a buyer from before this rule, migrated once) =
## no interstitials (Ads.ad_free_check) and the rewarded bonuses without the video. Cash packs and looks change neither.
signal changed
signal message(text: String)
var config: Dictionary = {}
var busy := false
var last_reward := 0.0
## Shop offers (IapOffers record), how many calendar days / sessions the player has opened the game (offer rules).
var offers: Dictionary = IapOffers.new_record()
var play_days := 0
var sessions := 0
var _day_key := ""
var _pending_reward := ""
var _pending_placement := "" ## "" = the sponsor bonus (older callers set only _pending_reward)
var _pending_ctx: Dictionary = {}
var _reward_amount := 0.0
## Rewarded placements: placement -> {"day": local date, "count": uses that day, "last": unix time}. Persisted next to
## last_reward (the sponsor's own stamp) in monetization.json, so a restart or a progress reset cannot refill a cap.
var ad_log: Dictionary = {}
var _offer_logged := {}
var _timeout := 0.0
var _ads: Node
var _path := ""
var _offer_shown := false
var _offer_timer := 0.0
var _returned_since_break := 0
var _admob_started := false
var review: Node

## Cash packs are measured in minutes of the player's reference income (EconomyManager.ref_income): ~1 / 2.5 / 5 play-days
## of a 30-minute-a-day player, never a fixed sum that ends the game.
const PACK_MINUTES := {"cash_s": 30.0, "cash_m": 75.0, "cash_l": 150.0, "starter": 18.0}
const LEGACY_MINUTES := 20.0
## Opt-in rewarded placements (the button is always the player's choice; all share the one rewarded ad unit).
const PLACEMENTS := ["sponsor", "offline_double", "repair_finish", "daily_double"]
const DEFAULT_LIMITS := {
	"sponsor": {"daily_cap": 5, "cooldown_seconds": 600},
	"offline_double": {"daily_cap": 3, "cooldown_seconds": 300},
	"repair_finish": {"daily_cap": 4, "cooldown_seconds": 180},
	"daily_double": {"daily_cap": 3, "cooldown_seconds": 120},
}
const OFFLINE_DOUBLE_MAX_SECONDS := 7200.0 ## the bonus never repays more than two hours of away income (Longer Offline stays worth buying)
const BREAK_EVERY_RETURNS := 5 ## interstitial only at a calm moment after this many finished trips (CoddyAds adds its own pacing)


func _ready() -> void:
	config = JSON.parse_string(FileAccess.get_file_as_string("res://data/monetization.json"))
	_path = SaveManager.root_path + "monetization.json"
	var legacy_pass := false
	var migrated := false
	if FileAccess.file_exists(_path):
		var saved: Variant = JSON.parse_string(FileAccess.get_file_as_string(_path))
		if saved is Dictionary:
			legacy_pass = bool(saved.get("owned", false))
			migrated = bool(saved.get("migrated", false))
			last_reward = float(saved.get("last_reward", 0.0))
			ad_log = _parse_ad_log(saved.get("ad_log", {}))
			play_days = int(saved.get("play_days", 0))
			sessions = int(saved.get("sessions", 0))
			_day_key = str(saved.get("day_key", ""))
			var rec: Variant = saved.get("offers", {})
			if rec is Dictionary and not rec.is_empty():
				offers = {"last_day": int(rec.get("last_day", -999)), "count": rec.get("count", {}) if rec.get("count", {}) is Dictionary else {}}
	# 1.1.0 stored the Empire Pass in monetization.json: it is a product of the new catalog now
	if legacy_pass and not migrated:
		Iap.grant_legacy("empire_pass")
	var today := Time.get_date_string_from_system()
	if today != _day_key:
		_day_key = today
		play_days += 1
	sessions += 1
	_persist()
	Iap.visible_rule = _product_visible
	Ads.ad_free_check = func() -> bool: return ad_free()
	_ads = preload("res://scripts/monetization/rewarded_ads.gd").new()
	add_child(_ads)
	_ads.earned.connect(_on_reward_earned)
	_ads.finished.connect(func():
		busy = false
		_pending_reward = ""
		_pending_placement = ""
		_pending_ctx = {}
		changed.emit())
	_ads.failed.connect(func(): _status("No ad is available right now. Try again later."))
	Iap.entitlements_changed.connect(func(): changed.emit())
	Iap.prices_changed.connect(func(): changed.emit())
	Iap.notice.connect(func(text: String, tone: String):
		UIManager.toast(text, UIKit.GREEN if tone == "good" else (UIKit.ORANGE if tone == "warn" else UIKit.RED), "star"))
	ContractManager.job_returned.connect(func(_j): _returned_since_break += 1)
	UIManager.panel_closed.connect(_on_panel_closed)
	review = preload("res://scripts/monetization/review_ask.gd").new() # store rating at a calm moment (standard 7)
	review.name = "review_ask"
	add_child(review)


func _process(delta: float) -> void:
	if busy:
		_timeout -= delta
		if _timeout <= 0:
			busy = false
			_status("No ad is available right now. Try again later.")
			changed.emit()
	if not GameManager.running or GameManager._suspended:
		return
	_offer_timer += delta
	if _offer_timer >= 20.0:
		_offer_timer = 0.0
		# the consent form / AdMob start only once the game is really running, and never for an ad-free player
		if not _admob_started and calm_game() and not ad_free() and OS.has_feature("mobile"):
			_admob_started = true
			Ads.start_admob()
		var h: Node = UIManager.hud
		if h != null and is_instance_valid(h) and h.get("shop_director") != null:
			h.shop_director.try_deliver() # a pack bought while the game could not take it (intro, no game running)
		check_offer("")


## True while the game runs in front of the player: intro and tutorial over, nothing open on top.
func calm_game() -> bool:
	var h: Node = UIManager.hud
	if h == null or not is_instance_valid(h) or not h.is_inside_tree():
		return false
	return ProgressionManager.intro_done() and TutorialManager.finished and h.can_present() and not h._modal_open \
			and h.current_sheet == null and h._modal_queue.is_empty() and not Ads.is_showing()


# ------------------------------------------------------------------ store links

## Settings -> "Rate Borderline Empire": a plain store link (Android only; the OS rating sheet is asked separately, see review_ask.gd).
func rate_link_available() -> bool:
	return OS.get_name() == "Android"


func open_rate_page() -> void:
	OS.shell_open("market://details?id=com.coddykit.borderlineempire")


# ------------------------------------------------------------------ sponsor bonus

func delivery_multiplier() -> float:
	return float(config.get("pass_delivery_multiplier", 1.25)) if Iap.has("empire_pass") else 1.0


func reward_value() -> float:
	return minf(150.0 * ProgressionManager.garage_level, reward_cap())


## W32 fleet rule: one cash reward <= 1/5 of the cheapest cash pack (cash_s = 30 min of ref income), all cash rewards of a
## day <= 1/2 of it (sponsor + offline double + daily double share the budget). Ad-free players get the same amounts without a video.
const CASH_PLACEMENTS := ["sponsor", "offline_double", "daily_double"]
var rewarded_remote := true ## W32 remote kill switch for rewarded VIDEOS (ads-config rewardedEnabled; absent = on)


## The rewarded caps count in UTC days (fleet rule 4.3): a timezone change or a clock trip west does not refill them.
func _utc_day() -> String:
	return Time.get_date_string_from_system(true)


func cash_today() -> float:
	var e: Dictionary = ad_log.get("_cash", {})
	return float(e.get("count", 0.0)) if str(e.get("day", "")) == _utc_day() else 0.0


func reward_cap() -> float:
	var pack := pack_cash("cash_s")
	return maxf(0.0, minf(pack * 0.2, pack * 0.5 - cash_today()))


func _spend_cash(amount: float) -> void:
	ad_log["_cash"] = {"day": _utc_day(), "count": cash_today() + maxf(0.0, amount), "last": 0.0}


func cooldown_left() -> float:
	return placement_cooldown_left("sponsor")


## The one ad entitlement (fleet rule 3): Remove Ads / Kingpin Edition / Ultimate Bundle / Empire Pass, or a migrated old buyer.
## Having bought something (a cash pack, a paint set) is NOT enough - the purchase history is not an entitlement.
func ad_free() -> bool:
	return Iap.ad_free()


## Only an ad-free player takes the rewarded bonuses without the video (same caps); everyone else watches the ad.
func video_free() -> bool:
	return ad_free()


func ads_available() -> bool:
	return rewarded_remote and _ads.available(str(config.get("rewarded_android" if OS.get_name() == "Android" else "rewarded_ios", "")))


# --- per-placement daily cap + cooldown (persisted) ---

func _limit(p: String, key: String) -> int:
	var cfg: Variant = config.get("placements", {})
	if cfg is Dictionary and cfg.get(p) is Dictionary and (cfg[p] as Dictionary).has(key):
		return int(cfg[p][key])
	if p == "sponsor" and key == "cooldown_seconds":
		return int(config.get("reward_cooldown_seconds", 600))
	return int(DEFAULT_LIMITS.get(p, {}).get(key, 0))


func daily_cap(p: String) -> int:
	return _limit(p, "daily_cap")


func uses_today(p: String) -> int:
	var e: Dictionary = ad_log.get(p, {})
	return int(e.get("count", 0)) if str(e.get("day", "")) == _utc_day() else 0


func cap_left(p: String) -> int:
	return maxi(0, daily_cap(p) - uses_today(p))


func _last_used(p: String) -> float:
	return last_reward if p == "sponsor" else float(ad_log.get(p, {}).get("last", 0.0))


func placement_cooldown_left(p: String) -> float:
	return maxf(0.0, _last_used(p) + float(_limit(p, "cooldown_seconds")) - Time.get_unix_time_from_system())


## Cap and cooldown allow another use (says nothing about the ad itself).
func placement_open(p: String) -> bool:
	return cap_left(p) > 0 and placement_cooldown_left(p) <= 0.0 and (p not in CASH_PLACEMENTS or reward_cap() >= 1.0)


## The button may be shown/enabled: cap + cooldown allow it AND (an ad-free player's no-video claim, or an ad is ready).
func placement_ready(p: String) -> bool:
	return not busy and placement_open(p) and (video_free() or ads_available())


## Firebase payload of the rewarded events (fleet rule 4.9): reward_amount = cash of the reward (0 = a non-cash reward),
## daily_index = the number of this use among the placement's uses of the UTC day.
func event_params(p: String, amount: float, index: int = -1) -> Dictionary:
	return {"placement": p, "reward_amount": int(amount), "daily_index": index if index > 0 else uses_today(p) + 1}


## `rewarded_offer_shown`: the offer button was shown (at most once per placement per session).
func note_offer(p: String, amount: float = 0.0) -> void:
	if _offer_logged.has(p):
		return
	_offer_logged[p] = true
	Analytics.event("rewarded_offer_shown", event_params(p, amount))


func _parse_ad_log(saved_log: Variant) -> Dictionary:
	var out := {}
	if saved_log is Dictionary:
		for p in saved_log:
			if (p in PLACEMENTS or p == "_cash") and saved_log[p] is Dictionary:
				var e: Dictionary = saved_log[p]
				var c: Variant = maxf(0.0, float(e.get("count", 0))) if p == "_cash" else maxi(0, int(e.get("count", 0)))
				out[p] = {"day": str(e.get("day", "")), "count": c, "last": float(e.get("last", 0.0))}
	return out


func _record_use(p: String) -> void:
	var now := Time.get_unix_time_from_system()
	if p == "sponsor":
		last_reward = now
	var e: Dictionary = ad_log.get(p, {})
	var today := _utc_day()
	var count := int(e.get("count", 0)) if str(e.get("day", "")) == today else 0
	ad_log[p] = {"day": today, "count": count + 1, "last": now}


func claim_sponsor() -> void:
	claim_placement("sponsor")


## Starts a rewarded placement (an ad-free player takes it at once, everyone else watches the video first). The reward is granted
## only from the SDK's earned callback (_on_reward_earned) and only if the grant's own checks still hold then.
func claim_placement(p: String, ctx: Dictionary = {}) -> void:
	if busy or p not in PLACEMENTS or not placement_open(p): return
	_reward_amount = reward_value() if p == "sponsor" else 0.0
	_pending_placement = p
	_pending_ctx = ctx
	_pending_reward = Crypto.new().generate_random_bytes(16).hex_encode()
	var offered := _reward_amount if p == "sponsor" else _offer_amount(p, ctx)
	Analytics.event("rewarded_offer_accepted", event_params(p, offered))
	if video_free():
		Analytics.event("rewarded_skipped_paid_user", event_params(p, offered))
		_on_reward_earned(_pending_reward, false)
		return
	if not ads_available():
		_pending_reward = ""
		_pending_placement = ""
		_pending_ctx = {}
		_status("No ad is available right now. Try again later.")
		return
	busy = true
	_timeout = 120.0
	changed.emit()
	_ads.show_reward(str(config.get("rewarded_android" if OS.get_name() == "Android" else "rewarded_ios", "")), _pending_reward, _pending_placement)


func _on_reward_earned(token: String, from_video: bool = true) -> void:
	if token.is_empty() or token != _pending_reward: return
	if from_video: # a watched video: the next interstitial of this session waits one more interval
		Ads.postpone_after_rewarded()
	var p := _pending_placement if _pending_placement != "" else "sponsor"
	var ctx := _pending_ctx
	var granted := 0.0
	_pending_reward = ""
	_pending_placement = ""
	_pending_ctx = {}
	if p == "sponsor":
		_record_use(p)
		_spend_cash(_reward_amount)
		EconomyManager.earn(_reward_amount, "sponsor", false)
		GameManager.save_now()
		_persist()
		_status(tr("Sponsor reward: %s") % Fmt.money(_reward_amount))
		granted = _reward_amount
	else:
		var text := _grant(p, ctx)
		if text == "": # nothing to give any more (receipt/task/vehicle changed): no cap or cooldown is spent
			changed.emit()
			return
		_record_use(p)
		_spend_cash(_granted_cash)
		GameManager.save_now()
		_persist()
		granted = _granted_cash
		_status(text)
	Analytics.event("rewarded_completed", event_params(p, granted, uses_today(p)))
	changed.emit()


## Applies one non-sponsor reward; returns the toast text, "" = granted nothing.
var _granted_cash := 0.0


func _grant(p: String, ctx: Dictionary) -> String:
	_granted_cash = 0.0
	match p:
		"offline_double":
			var receipt: Dictionary = GameManager.session.get("receipt", {})
			if receipt.is_empty() or str(receipt.get("id", "")) != str(ctx.get("id", "")) or bool(receipt.get("doubled", false)):
				return ""
			var bonus := offline_bonus(receipt)
			if bonus <= 0.0:
				return ""
			receipt["doubled"] = true
			_granted_cash = bonus
			EconomyManager.earn(bonus, "offline_double", false)
			return tr("Earnings doubled: +%s") % Fmt.money(bonus)
		"repair_finish":
			if not VehicleManager.free_repair(VehicleManager.get_vehicle(str(ctx.get("vehicle", "")))):
				return ""
			return tr("Repair finished")
		"daily_double":
			var amount := DailyManager.double_claim(str(ctx.get("task", "")), int(ctx.get("day", -1)), floorf(reward_cap()))
			if amount <= 0.0:
				return ""
			_granted_cash = amount
			return tr("Daily reward doubled: +%s") % Fmt.money(amount)
	return ""


## What the rewarded double of a claimed daily task would add: its reward, at most what is left of the day's reward budget.
func daily_double_amount(task_id: String) -> float:
	return DailyManager.double_amount(task_id, floorf(reward_cap()))


## The cash a placement would pay right now (0 = not a cash placement / nothing to give), for the event payloads.
func _offer_amount(p: String, ctx: Dictionary) -> float:
	match p:
		"sponsor":
			return reward_value()
		"offline_double":
			return offline_bonus(GameManager.session.get("receipt", {}))
		"daily_double":
			return daily_double_amount(str(ctx.get("task", "")))
	return 0.0


## What "double my away earnings" adds: the receipt's earnings again, capped at two hours' worth of the time away.
func offline_bonus(report: Dictionary) -> float:
	var seconds := float(report.get("seconds", 0.0))
	var earned := float(report.get("earned", 0.0))
	if seconds <= 0.0 or earned <= 0.0:
		return 0.0
	return minf(float(Fmt.nice(earned * minf(1.0, OFFLINE_DOUBLE_MAX_SECONDS / seconds))), floorf(reward_cap()))


## The welcome-back card may offer the double: the receipt is the saved, un-doubled one and the placement is ready.
func offline_double_offer(report: Dictionary) -> bool:
	var receipt: Dictionary = GameManager.session.get("receipt", {})
	return not report.is_empty() and str(receipt.get("id", "")) == str(report.get("id", "-")) and not bool(receipt.get("doubled", false)) \
			and offline_bonus(receipt) > 0.0 and placement_ready("offline_double")


func privacy_options() -> void:
	if Ads.privacy_options_required():
		Ads.show_privacy_options()
	else:
		_ads.privacy_options()


func privacy_options_available() -> bool:
	return Ads.privacy_options_required() or ads_available()


# ------------------------------------------------------------------ paid packs

## Cash a pack delivers right now (shown in the shop, used by deliver()).
func pack_cash(fx: String) -> float:
	if fx == "legacy_pack":
		var last := float(ProgressionManager.flags.get("last_run_ref", 0.0))
		var floor_cash := 3.0 * ProgressionManager.legacy_start_cash(maxi(1, ProgressionManager.legacy()))
		return float(Fmt.nice(maxf(floor_cash, last * LEGACY_MINUTES)))
	return float(Fmt.nice(EconomyManager.ref_income() * float(PACK_MINUTES.get(fx, 0.0))))


## IapDirector's deliver hook: put a bought pack into the running game. False = not now (it waits, saved).
func deliver(_slug: String, fx: String) -> bool:
	if not GameManager.running or SaveManager.write_protected or not ProgressionManager.intro_done():
		return false
	match fx:
		"cash_s", "cash_m", "cash_l", "legacy_pack":
			var cash := pack_cash(fx)
			EconomyManager.earn(cash, "pack", false)
			_delivered(cash)
		"starter":
			var cash := pack_cash(fx)
			EconomyManager.earn(cash, "pack", false)
			VehicleManager.grant_driver("skilled")
			_delivered(cash)
		_:
			return true # an effect this version does not know: drop it rather than keep it forever
	return true


func _delivered(cash: float) -> void:
	GameManager.save_now()
	AudioManager.play("cash")
	UIManager.toast(tr("Pack delivered: +%s") % Fmt.money(cash), UIKit.GREEN, "cash")


## Is this product worth showing right now? (Legacy Head Start only means something once the empire has been sold or can be.)
func _product_visible(slug: String) -> bool:
	if slug == "starter":
		return not Iap.owns("bundle") # the bundle brings Quick Start along
	if slug == "legacy_pack":
		return ProgressionManager.legacy() >= 1 or ProgressionManager.prestige_available()
	return true


# ------------------------------------------------------------------ one-off offers (never in the tutorial / first session)

func offer_context(event: String) -> Dictionary:
	return {"day": play_days, "event": event, "tutorial": not (ProgressionManager.intro_done() and TutorialManager.finished),
			"first_session": sessions <= 1}


## A calm moment (or a named event such as "prestige"): show the offer that is due, at most one per session.
func check_offer(event: String) -> String:
	if _offer_shown:
		return ""
	var h: Node = UIManager.hud
	if h == null or not is_instance_valid(h) or h.get("shop_director") == null:
		return ""
	if event == "" and not calm_game():
		return ""
	var slug: String = h.shop_director.check_offer(offers, offer_context(event))
	if slug != "":
		_offer_shown = true
		_persist()
	return slug


# ------------------------------------------------------------------ interstitial breaks (house-first, CoddyAds)

func _on_panel_closed(_name: String) -> void:
	if _returned_since_break < BREAK_EVERY_RETURNS or ad_free():
		return
	if not (ProgressionManager.intro_done() and TutorialManager.finished):
		return
	_returned_since_break = 0
	Ads.interstitial("panel_closed")


## The welcome-back card is a natural break too.
func offline_break() -> void:
	if not ad_free() and ProgressionManager.intro_done() and TutorialManager.finished:
		Ads.interstitial("welcome_back")


# ------------------------------------------------------------------ persistence

func _persist() -> void:
	if _path.is_empty():
		return
	var file := FileAccess.open(_path + ".tmp", FileAccess.WRITE)
	if file == null: return
	file.store_string(JSON.stringify({"migrated": true, "last_reward": last_reward, "ad_log": ad_log, "offers": offers,
			"play_days": play_days, "sessions": sessions, "day_key": _day_key}))
	file.close()
	DirAccess.rename_absolute(_path + ".tmp", _path)


func _status(text: String) -> void:
	message.emit(text)
	UIManager.toast(text, UIKit.BLUE, "star")
