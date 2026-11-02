extends RefCounted
## Dynamic downgrade (games/CLAUDE.md §10, W60-B1 shared module): 10 s in a row under 25 FPS moves the tier one step
## down; it never raises it by itself (only the player does, in Settings). The first seconds after start-up or after a
## tier change are ignored (loading hitches are not a slow device). The game shows its one-time notice ("Graphics
## lowered for smoother play — you can change it in Settings") and saves the new tier.
## A CAPPED frame rate is never a slow device (owner 2026-10-23: a Galaxy S25 Ultra came up on Low — 20 FPS under a
## window / idle is the game's own cap, not the phone): sample() reads the live cap (Engine.max_fps, or `cap`) and holds
## the clock while the cap is ≤ CAPPED_FPS or the game says the scene is covered (`covered`: a modal / pager / report
## open), then ignores SETTLE_S more after the cap is lifted (get_frames_per_second() is a 1 s average, it still shows
## the capped rate) and after any frame longer than HITCH_S (resume from an ad / the background, a shader stall).
## Also: the debug-build PERF line the emulator low-end report reads (games/tools/perf/qa_lowend_report.sh).
## Source of truth: games/tools/perf (copied into each game as addons/coddy_perf by sync.sh).
##
##   var guard := PerfGuard.new()
##   func _process(delta):
##       var to := guard.sample(Engine.get_frames_per_second(), delta, quality)
##       if to >= 0: set_quality(to); notice_once()
##       var line := guard.perf_line(delta, quality)
##       if line != "": print(line)

const SLOW_FPS := 25.0
const SLOW_FOR_S := 10.0
const GRACE_S := 5.0
const LOG_EVERY_S := 10.0
const CAPPED_FPS := 30 ## a live cap at or below this (window / idle pacing, the Low tier) is never read as slowness
const SETTLE_S := 2.5 ## ignored after a cap is lifted / a window closes / a long frame (the FPS figure is a 1 s average)
const HITCH_S := 0.5 ## a single frame this long is a resume or a stall, not a steady slow device

var slow_t := 0.0
var grace_t := GRACE_S
var downgrades := 0
var _log_t := 0.0
var _last_cap := -1


## One frame. Returns the tier to switch to (tier − 1) when the device has been slow long enough, else −1.
## A tier of 0 (Low) or a paused / headless frame (fps ≤ 0) never triggers anything. `cap`: the frame cap in force
## (−1 = read Engine.max_fps; 0 = uncapped). `covered`: a window / pager / report is over the scene (the game may pace
## it down at any moment) — such frames never count, nor do the SETTLE_S after it closes.
func sample(fps: float, delta: float, tier: int, cap: int = -1, covered: bool = false) -> int:
	var live := Engine.max_fps if cap < 0 else cap
	if live != _last_cap:
		_last_cap = live
		grace_t = maxf(grace_t, SETTLE_S)
	if covered or (live > 0 and live <= CAPPED_FPS) or delta > HITCH_S:
		slow_t = 0.0
		grace_t = maxf(grace_t, SETTLE_S)
		return -1
	if grace_t > 0.0:
		grace_t -= delta
		slow_t = 0.0
		return -1
	if tier <= 0 or fps <= 0.0:
		slow_t = 0.0
		return -1
	slow_t = slow_t + delta if fps < SLOW_FPS else 0.0
	if slow_t < SLOW_FOR_S:
		return -1
	slow_t = 0.0
	grace_t = GRACE_S
	downgrades += 1
	return tier - 1


## Call after any tier change (the player's own too): the next frames are a warm-up again.
func reset() -> void:
	slow_t = 0.0
	grace_t = GRACE_S


## Debug builds on a phone: "PERF fps=<n> quality=<tier> static_mb=<n>" every 10 s (else ""). `force` = log anywhere
## (tests).
func perf_line(delta: float, tier: int, force: bool = false) -> String:
	if not force and (not OS.is_debug_build() or not OS.has_feature("mobile")):
		return ""
	_log_t += delta
	if _log_t < LOG_EVERY_S:
		return ""
	_log_t = 0.0
	return "PERF fps=%d quality=%d static_mb=%d" % [Engine.get_frames_per_second(), tier, OS.get_static_memory_usage() / 1048576]
