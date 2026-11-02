extends Node
## Native boundary double: no network, real facade + real Settings UiRow and isolated preference file.
const Facade = preload("res://scripts/autoload/Analytics.gd")
const Settings = preload("res://scripts/ui/panels/settings_sheet.gd")
var checks := 0
var failures := 0

class NativeAnalytics extends RefCounted:
	var grants: Dictionary = {}
	var collecting := false
	var events: Array = []
	func set_consent(value: Dictionary) -> void: grants = value.duplicate()
	func set_analytics_collection_enabled(value: bool) -> void: collecting = value
	func log_event(event_name: String, params: Dictionary) -> void:
		events.append([event_name, params.duplicate()])

func check(value: bool, label: String) -> void:
	checks += 1
	if not value:
		failures += 1
		push_error("ANALYTICS CONSENT: " + label)

func ads_denied(native: NativeAnalytics) -> bool:
	return native.grants.get("ad_storage") == "denied" and native.grants.get("ad_user_data") == "denied" and native.grants.get("ad_personalization") == "denied"

func _ready() -> void:
	check(SaveManager.qa_mode and Analytics._fa == null, "QA never starts native Firebase")
	var path: String = Analytics._consent_path()
	check(path.begins_with("user://qa/"), "preference is isolated from player files")
	DirAccess.remove_absolute(path)
	var native := NativeAnalytics.new()
	var a := Facade.new()
	a._load_consent()
	check(not a.consent, "absent preference: existing and new players start OFF")
	a._fa = native
	a.event("before_init")
	check(a._queue.is_empty(), "no pre-opt-in buffering")
	a._on_analytics_initialized(true)
	check(not native.collecting and native.grants.analytics_storage == "denied" and native.events.is_empty(), "startup denies consent and collection, including game_ready")
	check(ads_denied(native), "startup denies advertising signals")
	a.set_consent(true)
	a.event("allowed", {"count": 1})
	check(native.collecting and native.grants.analytics_storage == "granted" and native.events.size() == 1, "opt-in delivers event to native SDK")
	check(ads_denied(native), "analytics opt-in never grants ads, including required/unanswered UMP")
	var restarted := Facade.new()
	restarted._load_consent()
	check(restarted.consent, "explicit opt-in persists across launch")
	restarted.free()
	a.set_consent(false)
	a.screen("private_screen")
	check(not native.collecting and native.grants.analytics_storage == "denied" and native.events.size() == 1, "revocation immediately stops events and collection")
	check(ads_denied(native), "revocation keeps ads denied")
	restarted = Facade.new()
	restarted._load_consent()
	check(not restarted.consent, "revocation persists across launch")
	restarted.free()
	a.free()
	# Opt-in then revoke while initialization is pending: queued events must never be replayed later.
	a = Facade.new()
	native = NativeAnalytics.new()
	a._fa = native
	a.set_consent(true)
	a.event("pending")
	check(a._queue.size() == 1, "explicitly opted-in event may wait for initialization")
	a.set_consent(false)
	a._on_analytics_initialized(true)
	check(a._queue.is_empty() and native.events.is_empty() and not native.collecting, "revocation before init discards queue")
	a.free()
	# Wrong-typed legacy values cannot turn the new switch on.
	var cfg := ConfigFile.new()
	cfg.set_value("privacy", "analytics", "true")
	cfg.save(path)
	a = Facade.new()
	a._load_consent()
	check(not a.consent, "malformed preference fails closed")
	a.free()
	DirAccess.remove_absolute(path)
	Analytics._load_consent()
	native = NativeAnalytics.new()
	Analytics._fa = native
	Analytics._ready_ok = true
	Analytics._apply_consent()
	UIKit.theme()
	var sheet := Settings.new()
	var body := VBoxContainer.new()
	add_child(body)
	sheet.build(body)
	var row: Button = body.find_child("analytics_consent", true, false)
	check(row != null and not row.button_pressed and row.trailing == "switch", "real Privacy kit switch starts off")
	row.button_pressed = true
	Analytics.event("ui_enabled")
	check(Analytics.consent and native.collecting and native.events.size() == 1, "real switch grants consent and sends")
	row.button_pressed = false
	Analytics.event("ui_disabled")
	check(not Analytics.consent and not native.collecting and native.events.size() == 1, "real switch revokes and stops")
	for lang in GameManager.LANGUAGES:
		TranslationServer.set_locale(lang)
		check(not tr("Share anonymous analytics").is_empty() and (lang == "en" or tr("Share anonymous analytics") != "Share anonymous analytics"), "translated toggle: " + lang)
	TranslationServer.set_locale("en")
	sheet.free()
	body.free()
	Analytics._fa = null
	Analytics._ready_ok = false
	DirAccess.remove_absolute(path)
	print("ANALYTICS CONSENT checks=%d failures=%d" % [checks, failures])
	get_tree().quit(1 if failures else 0)
