extends Node
## Firebase Analytics facade (godot-x/firebase, Core + Analytics). Template: games/tools/firebase/Analytics.gd —
## adapted with Smuggler QA isolation and register as the `Analytics` autoload.
##   Analytics.event(name, params) · Analytics.screen(name)
## Collection is opt-in (Settings → Privacy), including for players upgrading from an older release.
## Export privacy-safe defaults hold the SDK off before initialization. All Firebase ad signals stay denied,
## independently of this switch and of UMP: enabling analytics never grants advertising consent.
## The preference is separate from the game save; QA never reads the player's choice or starts the native SDK.

const MAX_QUEUE := 50

var _core: Object
var _fa: Object
var _ready_ok := false
var _queue: Array = []
var consent := false


func _ready() -> void:
	if SaveManager.qa_mode:
		return
	_load_consent()
	if not Engine.has_singleton("GodotxFirebaseCore") or not Engine.has_singleton("GodotxFirebaseAnalytics"):
		return
	_core = Engine.get_singleton("GodotxFirebaseCore")
	_fa = Engine.get_singleton("GodotxFirebaseAnalytics")
	_core.core_initialized.connect(_on_core_initialized)
	_fa.analytics_initialized.connect(_on_analytics_initialized)
	_core.initialize()


func _on_core_initialized(success: bool) -> void:
	if success:
		_fa.initialize()
	else:
		push_warning("[Analytics] Firebase Core init failed")


func _on_analytics_initialized(success: bool) -> void:
	if not success:
		push_warning("[Analytics] Firebase Analytics init failed")
		return
	_ready_ok = true
	_apply_consent()
	event("game_ready", {"game_version": str(ProjectSettings.get_setting("application/config/version", ""))})
	UIManager.panel_opened.connect(screen)
	ContractManager.job_delivered.connect(func(_job, _result): event("delivery_completed"))
	TutorialManager.step_changed.connect(func():
		if TutorialManager.finished: event("tutorial_complete"))
	for e in _queue:
		if consent:
			_fa.log_event(e[0], e[1])
	_queue.clear()


## Separate preference file: no save schema migration and no legacy implicit opt-in.
func _consent_path() -> String:
	return SaveManager.root_path + "analytics_consent.cfg"


func _load_consent() -> void:
	var cfg := ConfigFile.new()
	consent = false
	if cfg.load(_consent_path()) == OK:
		var value: Variant = cfg.get_value("privacy", "analytics", false)
		consent = value is bool and value == true


func set_consent(allow: bool) -> void:
	consent = allow
	if not allow:
		_queue.clear()
	if _ready_ok:
		_apply_consent()
	var cfg := ConfigFile.new()
	cfg.set_value("privacy", "analytics", allow)
	if cfg.save(_consent_path()) != OK:
		push_warning("[Analytics] Could not save privacy preference")


func _apply_consent() -> void:
	_fa.set_consent({
		"analytics_storage": "granted" if consent else "denied",
		"ad_storage": "denied",
		"ad_user_data": "denied",
		"ad_personalization": "denied",
	})
	_fa.set_analytics_collection_enabled(consent)


## GA4 rules: name ≤ 40 chars [a-z0-9_], ≤ 25 params, no reserved prefixes (firebase_, google_, ga_).
func event(name: String, params: Dictionary = {}) -> void:
	if _fa == null or not consent:
		return
	if _ready_ok:
		_fa.log_event(name, params)
	elif _queue.size() < MAX_QUEUE:
		_queue.append([name, params])


func screen(screen_name: String) -> void:
	event("screen_view", {"screen_name": screen_name, "screen_class": screen_name})
