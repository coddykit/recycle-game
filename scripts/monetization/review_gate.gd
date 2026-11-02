extends RefCounted
## When Borderline Empire may ask for a store rating (owner standard 2026-10-18, PROGRAM.md item 7). Pure logic: no Node, no store.
## The ask itself is the OS's own native sheet (Google Play In-App Review / StoreKit requestReview - review_native.gd);
## the game never shows its own "do you like it / how many stars" card first (review gating is forbidden by Google and
## Apple), never offers a reward for rating, and the OS decides whether the sheet really appears.
## Rules (owner 2026-10-27): the first session is OK once the player has ~4 minutes of play; never during the intro or
## the tutorial (it runs until the garage upgrade + a look at the map, ~minute 3); only at a good moment - the empire's
## 1st promotion (stage 1 "Growing Fleet" = a 2nd vehicle, the same moment as goal 5 "Buy a second vehicle": ~minute 4-5
## with 20-40 s trips) or a goal finished once >= 3 goals are done, whichever comes first; at most 3 attempts, at least
## 30 days apart. The OS never says whether a rating was given, so every hand-over counts as an ask (a player who rated
## is asked at most every 30 days, 3 times in all).
## (No class_name: preloaded, so a fresh checkout works before the editor rebuilds the class cache.)

const MIN_SESSIONS := 1
const MIN_PLAY_S := 240.0
const MAX_ASKS := 3
const GAP_S := 30.0 * 86400.0
const PROMOTION_STAGE := 1   ## empire stage 1 = the 1st promotion (Growing Fleet)
const GOALS := 3


static func empty_state() -> Dictionary:
	return {"v": 1, "sessions": 0, "play_s": 0.0, "asks": 0, "last_ask": 0.0, "goals": 0, "stage": 0}


static func decode(text: String) -> Dictionary:
	var st := empty_state()
	var parser := JSON.new()
	var data = parser.data if text != "" and parser.parse(text) == OK else null # quiet: a broken file just starts fresh
	if typeof(data) == TYPE_DICTIONARY:
		st.sessions = maxi(0, int(data.get("sessions", 0)))
		st.play_s = maxf(0.0, float(data.get("play_s", 0.0)))
		st.asks = maxi(0, int(data.get("asks", 0)))
		st.last_ask = maxf(0.0, float(data.get("last_ask", 0.0)))
		st.goals = maxi(0, int(data.get("goals", 0)))
		st.stage = maxi(0, int(data.get("stage", 0)))
	return st


static func encode(st: Dictionary) -> String:
	return JSON.stringify(st)


## May we ask now? `moment`: "stage" (arg = the stage reached) | "goal" | "" (just check the player's history).
## `tutorial`: the tutorial or the intro is running. `now`: unix seconds.
static func may_ask(st: Dictionary, moment: String, arg: int, tutorial: bool, now: float) -> bool:
	if tutorial or int(st.sessions) < MIN_SESSIONS or float(st.play_s) < MIN_PLAY_S:
		return false
	if int(st.asks) >= MAX_ASKS:
		return false
	if int(st.asks) > 0 and now - float(st.last_ask) < GAP_S:
		return false
	match moment:
		"stage":
			return arg >= PROMOTION_STAGE
		"goal":
			return int(st.goals) >= GOALS
	return false


static func asked(st: Dictionary, now: float) -> void:
	st.asks = int(st.asks) + 1
	st.last_ask = now
	st.goals = 0 # the next ask waits for three fresh goals (or the next promotion) again


## A goal was completed.
static func goal_done(st: Dictionary) -> void:
	st.goals = int(st.goals) + 1


## The empire reached `stage`. Returns true when it is a promotion we have not seen yet (the highest stage only grows).
static func promoted(st: Dictionary, stage: int) -> bool:
	if stage > int(st.stage):
		st.stage = stage
		return true
	return false
