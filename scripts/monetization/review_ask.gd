extends Node
## Asks the OS for a store rating at a good moment (review_gate.gd rules: the first session is OK after ~4 min of play,
## owner 2026-10-27; the native sheet is review_native.gd). A child of
## the Monetization autoload. Keeps its counters next to the save (review.json: sessions, play time, goals, stage, attempts)
## and does nothing on desktop, in tests, or when the platform has no native review call. There is no pre-question of its
## own and no reward for rating (store policy): it only hands the OS the request, at a calm moment after a promotion / a
## few finished goals. (No class_name: preloaded.)

const Gate := preload("res://scripts/monetization/review_gate.gd")
const Native := preload("res://scripts/monetization/review_native.gd")

var st := Gate.empty_state()
var path := ""
## Test seams: replace the platform probe, the native call and the "is the game calm" probe.
var available_hook := Callable()
var request_hook := Callable()
var calm_hook := Callable()
var _pending := ""
var _arg := 0
var _pending_t := 0.0
var _wait_t := 0.0
var _save_t := 0.0
var _busy := false


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	# next to the save: QA runs (their own folder) never touch the player's counters
	path = SaveManager.root_path + "review.json"
	if FileAccess.file_exists(path):
		st = Gate.decode(FileAccess.get_file_as_string(path))
	st.sessions = int(st.sessions) + 1
	_save()
	ProgressionManager.stage_changed.connect(_on_stage)
	ProgressionManager.goal_completed.connect(_on_goal)


func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_PAUSED or what == NOTIFICATION_WM_CLOSE_REQUEST:
		_save()


func _available() -> bool:
	return bool(available_hook.call()) if available_hook.is_valid() else Native.available()


func _calm() -> bool:
	return bool(calm_hook.call()) if calm_hook.is_valid() else Monetization.calm_game()


func _tutorial() -> bool:
	return not (ProgressionManager.intro_done() and TutorialManager.finished)


func _process(delta: float) -> void:
	if GameManager.running and not GameManager._suspended and not get_tree().paused:
		st.play_s = float(st.play_s) + delta
	_save_t += delta
	if _save_t > 30.0:
		_save_t = 0.0
		_save()
	if _pending == "" or _busy:
		return
	_pending_t += delta
	if _pending_t > 180.0: # the good moment has passed: not later, in the middle of something else
		_pending = ""
		return
	_wait_t += delta
	if _wait_t < 1.0:
		return
	_wait_t = 0.0
	_try()


func _on_stage(stage: int) -> void:
	if not Gate.promoted(st, stage):
		return
	_save()
	_want("stage", stage)


func _on_goal(_text: String, _reward: float) -> void:
	Gate.goal_done(st)
	_save()
	if _pending == "":
		_want("goal", 0)


func _want(moment: String, arg: int) -> void:
	if not _available():
		return
	if Gate.may_ask(st, moment, arg, _tutorial(), Time.get_unix_time_from_system()):
		_pending = moment
		_arg = arg
		_pending_t = 0.0


## At a calm moment (game running, no window, no ad, the tutorial off) hand the OS the request.
func _try() -> void:
	if not _calm():
		return
	var now := Time.get_unix_time_from_system()
	if not Gate.may_ask(st, _pending, _arg, false, now):
		_pending = ""
		return
	_busy = true
	var sent: bool = await request_hook.call() if request_hook.is_valid() else await Native.request(get_tree())
	_busy = false
	if sent:
		Gate.asked(st, now)
		Analytics.event("review_requested", {"moment": _pending, "n": int(st.asks)})
	_pending = ""
	_save()


func _save() -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file != null:
		file.store_string(Gate.encode(st))
		file.close()
