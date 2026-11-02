class_name FeedbackModal
extends ModalBase
const Kit = preload("res://addons/coddy_ui/ui_kit.gd")
const UiSegmented = preload("res://addons/coddy_ui/ui_segmented.gd")
## Settings -> "Send feedback": a short form (category, e-mail, message) that posts to the fleet's feedback line
## (scripts/monetization/feedback_client.gd). Always reachable, on every platform; nothing is promised or rewarded for
## sending. The fields keep clear of the phone's on-screen keyboard: the card shrinks to the room above it.

const Client := preload("res://scripts/monetization/feedback_client.gd")
const CATEGORIES := [["question", "Question"], ["bug", "Bug"], ["feedback", "Comment"], ["feature", "Idea"]]

signal sent(category: String)

var category := "feedback" ## lower case here; the API wants the upper-case code (Client.CATEGORIES)
var email: LineEdit
var message: TextEdit
var note: Label
var send_button: Button
var category_buttons: Dictionary = {}
var category_seg: Control
var busy := false
## Test seam: replaces the network call (returns true when "sent").
var send_hook := Callable()
var _kb_applied := 0.0


func setup() -> void:
	_kit_frame(tr("Send feedback"), {"icon": "message-square", "icon_kind": "accent", "close": true})
	content.add_child(UIKit.wlabel(tr("Something broken, or an idea? Write to us and we reply by email."), "BodyMuted"))
	var names: Array = []
	for entry in CATEGORIES:
		names.append(tr(entry[1]))
	category_seg = UiSegmented.new()
	category_seg.name = "feedback_category"
	category_seg.setup(names, maxi(0, CATEGORIES.map(func(c): return c[0]).find(category)), "cat_")
	category_seg.changed_to.connect(func(i: int): _pick(CATEGORIES[i][0]))
	content.add_child(category_seg)
	for i in CATEGORIES.size():
		category_buttons[CATEGORIES[i][0]] = category_seg.buttons[i]
	content.add_child(Kit.label(tr("Email"), "CaptionMuted"))
	email = LineEdit.new()
	email.name = "feedback_email"
	email.placeholder_text = "you@example.com"
	email.virtual_keyboard_type = LineEdit.KEYBOARD_TYPE_EMAIL_ADDRESS
	email.custom_minimum_size.y = 84
	content.add_child(email)
	content.add_child(Kit.label(tr("Your message"), "CaptionMuted"))
	message = TextEdit.new()
	message.name = "feedback_message"
	message.wrap_mode = TextEdit.LINE_WRAPPING_BOUNDARY
	message.custom_minimum_size = Vector2(0, 180)
	message.add_theme_font_size_override("font_size", 25)
	message.add_theme_stylebox_override("normal", Kit.theme().get_stylebox("normal", "LineEdit"))
	message.add_theme_stylebox_override("focus", Kit.theme().get_stylebox("focus", "LineEdit"))
	content.add_child(message)
	note = UIKit.wlabel("", "Caption")
	note.name = "feedback_note"
	content.add_child(note)
	send_button = kit_modal.add_action(tr("Send"), "primary", submit, "feedback_send")
	kit_modal.add_action(tr("Close"), "ghost", close)
	_pick(category)


func _pick(code: String) -> void:
	category = code
	for key in category_buttons:
		category_buttons[key].set_pressed_no_signal(key == code)


func submit() -> void:
	if busy:
		return
	match Client.problem(email.text, message.text):
		"email":
			_say("Enter a valid email so we can reply.", UIKit.RED)
			return
		"message":
			_say("Your message is a little short. Write a few sentences.", UIKit.RED)
			return
	busy = true
	send_button.disabled = true
	_say("Sending your message…", UIKit.DIM)
	var ok: bool
	if send_hook.is_valid():
		ok = await send_hook.call(category, email.text, message.text)
	else:
		ok = await Client.send(self, "", email.text, category.to_upper(), message.text, GameManager.language())
	busy = false
	if not is_instance_valid(send_button): # the player left while it was sending
		if ok:
			sent.emit(category)
		return
	send_button.disabled = false
	if ok:
		_say("Thank you! Your message reached us.", UIKit.GREEN)
		message.text = ""
		sent.emit(category)
	else:
		_say("Could not send. Check your connection and try again.", UIKit.RED)


func _say(text: String, color: Color) -> void:
	note.text = tr(text)
	note.add_theme_color_override("font_color", color)


## Phone keyboard: the card gets only the room above it (the modal layer is shortened by the keyboard's height).
func _process(_dt: float) -> void:
	if not DisplayServer.has_feature(DisplayServer.FEATURE_VIRTUAL_KEYBOARD) or kit_modal == null:
		return
	var kb := float(DisplayServer.virtual_keyboard_get_height())
	var window_h := float(DisplayServer.window_get_size().y)
	var canvas_h := get_viewport().get_visible_rect().size.y
	var want := kb * canvas_h / maxf(1.0, window_h)
	if absf(want - _kb_applied) > 1.0:
		_kb_applied = want
		var layer := get_parent() as Control
		if layer != null:
			layer.offset_bottom = -want


func _exit_tree() -> void:
	var layer := get_parent() as Control
	if layer != null and _kb_applied != 0.0:
		layer.offset_bottom = 0.0
