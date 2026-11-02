class_name EventModal
extends ModalBase
## Random journey event (kit UiModal): headline + situation, the three abstract choices as rows with their trade-offs.
const Kit = preload("res://addons/coddy_ui/ui_kit.gd")
const UiRow = preload("res://addons/coddy_ui/ui_row.gd")

var job: Dictionary
var _timer_label: Label


func setup(j: Dictionary) -> void:
	job = j
	EventManager.event_resolved.connect(_on_resolved)
	var e := Data.event_def(job.event_id)
	_kit_frame(tr(str(e.get("title", "Event"))), {"icon": UIKit.lucide(str(e.get("icon", "risk")), "triangle-alert"), "icon_kind": "warning", "subtitle": tr("EVENT")})
	content.add_child(UIKit.wlabel(tr(str(e.get("text", ""))), "Body"))
	var v := VehicleManager.get_vehicle(job.vehicle_id)
	var ctx := HFlowContainer.new()
	ctx.add_theme_constant_override("h_separation", 10)
	ctx.add_theme_constant_override("v_separation", 8)
	content.add_child(ctx)
	ctx.add_child(Kit.chip("truck", str(Data.vehicle_def(v.get("def", "")).get("name", "?"))))
	ctx.add_child(Kit.chip("map", RegionManager.node_name(job.contract.dest)))
	ctx.add_child(Kit.chip("coins", Fmt.money(float(job.reward)), "primary"))
	var choices: Array = job.get("choices", [])
	for i in choices.size():
		content.add_child(_choice_row(i, choices[i]))
	_timer_label = UIKit.wlabel("", "CaptionMuted")
	_timer_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	content.add_child(_timer_label)


## One choice: its label with the trade-offs as the second line.
func _choice_row(i: int, c: Dictionary) -> Control:
	var fx: Array = []
	var cost := float(c.get("cost", 0))
	if cost > 0:
		fx.append("-" + Fmt.money(cost))
	var delay := float(c.get("delay", 0))
	if delay != 0:
		fx.append(("+%ds" if delay > 0 else "%ds") % int(delay))
	if c.has("risk"):
		fx.append(tr("Risk +%d%%") % int(c.risk))
	if c.has("chance"):
		fx.append(tr("%d%% success") % int(float(c.chance) * 100))
		fx.append(tr("fail: %s") % _fail_text(c.get("fail", {})))
	if c.has("damage"):
		fx.append(tr("Damage"))
	if c.has("heat"):
		var h := float(c.heat)
		fx.append(tr("Heat +%d" if h > 0 else "Heat %d") % int(h))
	if c.has("reward_mult"):
		fx.append(tr("Pay %d%%") % int(float(c.reward_mult) * 100))
	if fx.is_empty():
		fx.append(tr("No downside"))
	var row := UiRow.item("hand", "primary" if cost == 0 else "warning", tr(str(c.label)), " · ".join(fx), func():
		AudioManager.play("accept")
		EventManager.resolve(job, i)
		close())
	row.name = "choice_%d" % i
	row.custom_minimum_size.y = 104
	if cost > 0 and not EconomyManager.can_afford(cost):
		row.set_disabled_state(true)
	return row


func _on_resolved(j: Dictionary, _summary: String, _good: bool) -> void:
	if j.id == job.id:
		close()


func _process(_delta: float) -> void:
	# countdown hint: the driver decides on their own after a while
	if _timer_label:
		var left := ContractManager.EVENT_TIMEOUT - float(job.get("pending_time", 0.0))
		_timer_label.text = tr("Driver decides in %ds") % maxi(0, int(ceil(left)))


func _fail_text(f: Dictionary) -> String:
	if f.get("fail_contract", false):
		return tr("contract lost")
	var parts := []
	if f.has("damage"):
		parts.append(tr("damage"))
	if f.has("delay"):
		parts.append("+%ds" % int(f.delay))
	if f.has("risk"):
		parts.append(tr("risk +%d%%") % int(f.risk))
	if f.has("rep"):
		parts.append(tr("%d rep") % int(f.rep))
	if f.has("reward_mult"):
		parts.append(tr("pay %d%%") % int(float(f.reward_mult) * 100))
	return ", ".join(parts) if parts.size() > 0 else tr("nothing")
