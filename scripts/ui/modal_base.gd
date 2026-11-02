class_name ModalBase
extends Control
## Base of the game's pop-ups. Every pop-up is a coddy_ui UiModal (a bottom sheet on a phone: content-sized, icon + title,
## body scrolls inside, actions below). Subclasses call _kit_frame(), add their content to `content` and buttons to `footer`
## (or use kit_modal.add_action()).

const UiModal = preload("res://addons/coddy_ui/ui_modal.gd")

signal closed

var card: PanelContainer
var content: VBoxContainer
var footer: BoxContainer
var scroller: ScrollContainer
var kit_modal: Control
var _closing := false


func _init() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP


## opts: icon (lucide), icon_kind (primary / danger / success / warning / info / accent), subtitle, close (X + scrim tap).
func _kit_frame(title: String, opts: Dictionary = {}) -> void:
	kit_modal = UiModal.new()
	add_child(kit_modal)
	# 2026-10-26 redesign: a bottom sheet upright ("auto"), Low = flat close glyph
	var o := {"sheet": "auto", "low": GameManager.quality() == "low", "close_label": tr("Close")}
	o.merge(opts, true)
	kit_modal.setup(title, o)
	card = kit_modal.card
	content = kit_modal.body
	footer = kit_modal.actions
	scroller = kit_modal.scroll
	if bool(opts.get("close", false)):
		kit_modal.close_requested.connect(close)
	modulate.a = 0.0
	create_tween().tween_property(self, "modulate:a", 1.0, 0.18)


func close() -> void:
	if _closing:
		return
	_closing = true
	var t := create_tween()
	t.tween_property(self, "modulate:a", 0.0, 0.18)
	t.tween_callback(func():
		closed.emit()
		queue_free())


func dismiss() -> void:
	close()
