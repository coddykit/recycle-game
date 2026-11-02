class_name BasicModal
extends ModalBase
const Kit = preload("res://addons/coddy_ui/ui_kit.gd")
## Celebration / info / confirmation dialog (kit UiModal: icon tile + headline, body, actions).


## buttons: [[text, button variation, callable, "stack"?], ...]; accent picks the icon tile colour.
func setup(title: String, body: String, icon_name: String, buttons: Array, accent: Color) -> void:
	_kit_frame(tr(title), {"icon": UIKit.lucide(icon_name, "info"), "icon_kind": UIKit.kind_of(accent)})
	if body != "":
		var bl := UIKit.wlabel(tr(body), "Body")
		content.add_child(bl)
	if buttons.is_empty():
		buttons = [["OK", "BtnGold", Callable()]]
	for b in buttons:
		var cb: Callable = b[2]
		var btn: Button = kit_modal.add_action(str(b[0]), UIKit.action_kind(str(b[1])), func():
			if cb.is_valid():
				cb.call()
			close())
		btn.name = "action_" + str(b[0]).validate_node_name()
	AudioManager.play("pop")
