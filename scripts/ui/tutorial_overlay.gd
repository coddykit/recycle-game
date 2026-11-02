class_name TutorialOverlay
extends Control
## The contextual tutorial in the kit's coach mark (UiCoach): the screen dims except a rounded spotlight around the one
## control to touch, a bubble with a pointer sits beside it (step counter, the line, Skip). Touches always pass through;
## TutorialManager decides which step is current and when it is done.

const UiCoach = preload("res://addons/coddy_ui/ui_coach.gd")

var coach: Control
var bubble: PanelContainer
var skip_btn: Button
var _last_text := ""
var _last_rect := Rect2()
var _last_step := -1


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	coach = UiCoach.new()
	add_child(coach)
	coach.setup({"skip": tr("Skip tutorial"), "hint": tr("Tap the highlighted spot")})
	bubble = coach.bubble
	skip_btn = coach.skip_button
	coach.skip_pressed.connect(_skip)
	UIManager.register_target("tutorial_skip", skip_btn)
	TutorialManager.step_changed.connect(func(): _last_text = "")


func _skip() -> void:
	TutorialManager.skip()
	if UIManager.hud != null and UIManager.hud.has_method("_update_tabs"):
		UIManager.hud._update_tabs()


func _process(_delta: float) -> void:
	var s := TutorialManager.current()
	var active := not s.is_empty() and TutorialManager.is_active()
	var modal_open: bool = UIManager.hud != null and (UIManager.hud._modal_open or is_instance_valid(UIManager.hud.title_card) \
			or is_instance_valid(UIManager.hud.onboarding))
	visible = active and not modal_open
	if not visible:
		return
	var target: Control = UIManager.get_target(str(s.get("target", "")))
	if UIManager.current_panel == "dispatch" and s.has("dispatch_target"):   # polish 10-27: the job is picked, now Dispatch
		var go: Control = UIManager.get_target(str(s.dispatch_target))
		if go != null:
			target = go
	if target == null and s.has("fallback"):
		target = UIManager.get_target(str(s.fallback))
	if target:
		_ensure_visible(target)
	var rect := target.get_global_rect() if target else Rect2()
	var text := str(s.text)
	if s.has("alt_text") and RegionManager.next_locked() >= 0 and not RegionManager.unlock_check(RegionManager.next_locked()).ok:
		text = str(s.alt_text)   # polish 10-27: the unlock line pointed at a $2,000 / 5-reputation button the player cannot use yet
	if text == _last_text and rect == _last_rect and TutorialManager.step == _last_step:
		return
	if text != _last_text and text == str(s.text):
		AudioManager.voice("tut_" + str(s.get("id", "")), 0.0)
	_last_text = text
	_last_rect = rect
	_last_step = TutorialManager.step
	coach.show_step(tr(text), tr("LEARN BY PLAYING · %d / %d") % [TutorialManager.step + 1, TutorialManager.steps.size()], rect, target != null)


var _scrolled_for: Control = null


## Scroll the target's list so the spotlight never points at something hidden.
func _ensure_visible(target: Control) -> void:
	if _scrolled_for == target:
		return
	var p := target.get_parent()
	while p and not (p is ScrollContainer):
		p = p.get_parent()
	if p == null:
		_scrolled_for = target
		return
	var sc := p as ScrollContainer
	var vis := sc.get_global_rect()
	var r := target.get_global_rect()
	if r.position.y < vis.position.y or r.end.y > vis.end.y:
		sc.ensure_control_visible(target)
		var extra := r.size.y
		sc.scroll_vertical += int(extra)
	_scrolled_for = target
