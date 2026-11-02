## (No class_name: preloaded.) Posts a player's note to the fleet's feedback line (coddy_api `POST /api/feedbacks`, the same endpoint the RN apps and
## Corrupt State use; AppPanel reads and answers every ticket there). `send` returns true on a 2xx. The device key (random
## 64 hex, kept in user://) identifies the phone only to the API; nothing else about the player is sent.
## No user-facing text lives here (scripts/ui/feedback_modal.gd owns it); there is none to translate.
extends RefCounted

const API := "https://api.coddykit.com/api/feedbacks"
const KEY_FILE := "user://feedback_key"
const APP_SLUG := "borderline-empire"
const APP_NAME := "Borderline Empire"
const CATEGORIES := ["QUESTION", "BUG", "FEEDBACK", "FEATURE"]
const MIN_MESSAGE := 10
const EMAIL_RX := "^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$"


static func valid_email(e: String) -> bool:
	return RegEx.create_from_string(EMAIL_RX).search(e.strip_edges()) != null


## "" when the form can be sent, "email" or "message" for the field that is wrong.
static func problem(email: String, message: String) -> String:
	if not valid_email(email):
		return "email"
	if message.strip_edges().length() < MIN_MESSAGE:
		return "message"
	return ""


static func device_key() -> String:
	if FileAccess.file_exists(KEY_FILE):
		var k := FileAccess.get_file_as_string(KEY_FILE).strip_edges()
		if k.length() == 64:
			return k
	var key := Crypto.new().generate_random_bytes(32).hex_encode()
	var f := FileAccess.open(KEY_FILE, FileAccess.WRITE)
	if f != null:
		f.store_string(key)
		f.close()
	return key


static func platform() -> String:
	return "ios" if OS.get_name() == "iOS" else ("android" if OS.get_name() == "Android" else OS.get_name().to_lower())


static func query(category: String, lang: String) -> String:
	var ctx := {
		"category": category, "lang": lang, "appName": APP_NAME, "appSlug": APP_SLUG,
		"appVersion": str(ProjectSettings.get_setting("application/config/version", "")),
		"platform": platform(), "osVersion": OS.get_version(), "deviceModel": OS.get_model_name(),
	}
	var parts: Array = []
	for k in ctx:
		if String(ctx[k]) != "" and String(ctx[k]) != "Unknown":
			parts.append("%s=%s" % [k, String(ctx[k]).uri_encode()])
	return "?" + "&".join(parts)


static func body(name: String, email: String, category: String, message: String) -> Dictionary:
	return {"name": name.strip_edges(), "subject": "%s · %s" % [APP_NAME, category.capitalize()], "email": email.strip_edges().to_lower(),
		"message": message.strip_edges(), "featureRequest": category == "FEATURE"}


## Send the note. `host`: a Node in the tree to run the request on. Returns true on a 2xx.
static func send(host: Node, name: String, email: String, category: String, message: String, lang: String) -> bool:
	var req := HTTPRequest.new()
	req.timeout = 20.0
	host.add_child(req)
	var headers := PackedStringArray(["Content-Type: application/json", "Accept: application/json", "X-Feedback-Key: " + device_key()])
	var err := req.request(API + query(category, lang), headers, HTTPClient.METHOD_POST, JSON.stringify(body(name, email, category, message)))
	if err != OK:
		req.queue_free()
		return false
	var res: Array = await req.request_completed
	req.queue_free()
	return int(res[1]) >= 200 and int(res[1]) < 300
