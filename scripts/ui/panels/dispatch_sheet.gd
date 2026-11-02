extends Sheet
## Pick a vehicle and a route, see the economics, send it.

var contract: Dictionary
var vehicle_id := ""
var route_id := "normal"
var _framed := false


func _init() -> void:
	super._init()
	title = "Dispatch"
	icon_name = "route"
	height_frac = 0.6


func on_opened() -> void:
	VehicleManager.vehicle_changed.connect(func(_v): refresh())


func on_closed() -> void:
	if UIManager.world:
		UIManager.world.end_preview()


func _gone() -> void:
	var h: Node = UIManager.hud
	if h == null or h.current_sheet != self:
		return
	UIManager.toast("This contract is no longer available.", UIKit.DIM, "clock")
	h.open_panel("contracts")


func build(b: VBoxContainer) -> void:
	contract = ContractManager.get_offer(str(arg))
	if contract.is_empty():
		# polish 10-27 (W66): a whole empty sheet with one line read as broken; back to the list with a toast instead
		b.add_child(Kit.empty_card("clock", tr("This contract is no longer available."), "", "", Callable(), low()))
		_gone.call_deferred()
		return
	var cat := Data.category(contract.category)
	if vehicle_id == "" or VehicleManager.get_vehicle(vehicle_id).is_empty():
		var best := ContractManager.best_vehicle(contract)
		vehicle_id = best.get("id", "")
		if not best.is_empty() and ProgressionManager.auto_enabled("regional_manager"):
			route_id = ContractManager.smart_route(contract, best)
	set_title(str(cat.name))
	var route := "%s  ›  %s" % [RegionManager.node_name(contract.origin), RegionManager.node_name(contract.dest)]
	var unit_word := "pax" if cat.get("kind", "cargo") == "passenger" else ("unit" if int(contract.units) == 1 else "units")
	set_sub(["%s  ·  %d %s" % [route, int(contract.units), tr(unit_word)], route])
	# vehicles
	b.add_child(section("Choose vehicle"))
	var vs: ScrollContainer = UiScroll.make(false, true)
	vs.custom_minimum_size = Vector2(0, 214)
	b.add_child(vs)
	var vrow := UIKit.hbox(12)
	vs.add_child(vrow)
	var sorted := VehicleManager.vehicles.duplicate()
	sorted.sort_custom(func(x, y):
		var ax := VehicleManager.available_reason(x) == "" and VehicleManager.fit_reason(x, contract) == ""
		var ay := VehicleManager.available_reason(y) == "" and VehicleManager.fit_reason(y, contract) == ""
		return ax and not ay)
	for v in sorted:
		vrow.add_child(_vehicle_button(v))
	var v := VehicleManager.get_vehicle(vehicle_id)
	# routes
	b.add_child(section("Choose route"))
	var rrow := UIKit.hbox(10)
	b.add_child(rrow)
	for r in Data.routes:
		rrow.add_child(_route_button(r, v))
	# economics
	var e := ContractManager.estimate(contract, v, route_id)
	if UIManager.world:
		UIManager.world.preview_route(e.path, route_id, not _framed)
		_framed = true
	var sum := Kit.card("Card")
	b.add_child(sum)
	var sv := UIKit.vbox(8)
	sum.add_child(sv)
	_row(sv, "Contract pay", Fmt.money(float(e.reward)), UIKit.TEXT)
	_row(sv, "Route + running costs", "-" + Fmt.money(float(e.cost)), UIKit.RED)
	if e.late:
		_row(sv, tr("Late delivery (%s limit)") % Fmt.time(float(contract.deadline)), "-30%", UIKit.ORANGE)
	var line := ColorRect.new()
	line.color = Color(1, 1, 1, 0.1)
	line.custom_minimum_size = Vector2(0, 2)
	sv.add_child(line)
	var profit := float(e.profit)
	_row(sv, "Estimated profit", ("" if profit >= 0 else "-") + Fmt.money(absf(profit)), UIKit.GREEN if profit >= 0 else UIKit.RED, 34)
	var risk := float(e.risk)
	var rr := UIKit.hbox(10)
	sv.add_child(rr)
	rr.add_child(UIKit.stat_chip("risk", tr("Risk %d%% (%s)") % [int(risk), Fmt.risk_label(risk)], Fmt.risk_color(risk), 22))
	var fail := ContractManager.base_fail_chance(risk)
	if fail > 0:
		rr.add_child(UIKit.label(tr("fail %d%%") % int(fail * 100), 20, UIKit.RED, true))
	rr.add_child(UIKit.spacer())
	rr.add_child(UIKit.stat_chip("heat", tr("Heat %d") % int(HeatManager.get_heat(RegionManager.region_of_node(contract.dest))),
			Fmt.heat_color(HeatManager.get_heat(RegionManager.region_of_node(contract.dest))), 22))
	# go button
	var reason := ContractManager.dispatch_check(contract, v)
	if reason == "":
		var go := UIKit.button(tr("DISPATCH  %s") % Fmt.time(float(e.time)), "BtnPrimary", "arrow_right", Vector2(0, 104), 34)
		go.pressed.connect(_dispatch)
		footer.add_child(go)
		UIManager.register_target("dispatch_go", go)
	else:
		var why := UIKit.label(reason if vehicle_id != "" else "No free vehicle can take this job", 24, UIKit.ORANGE, true, HORIZONTAL_ALIGNMENT_CENTER)
		footer.add_child(why)
		var q := UIKit.button("QUEUE FOR LATER", "BtnBlue", "contract", Vector2(0, 92), 28)
		q.disabled = ContractManager.queue.has(contract)
		q.pressed.connect(func():
			if ContractManager.accept(contract):
				UIManager.toast("Queued - it'll wait for a free vehicle", UIKit.BLUE, "contract")
				close())
		footer.add_child(q)


func _row(parent: Control, a: String, b: String, color: Color, size := 26) -> void:
	var h := UIKit.hbox(8)
	parent.add_child(h)
	h.add_child(UIKit.label(a, 24, UIKit.DIM))
	h.add_child(UIKit.spacer())
	h.add_child(UIKit.label(b, size, color, true))


func _vehicle_button(v: Dictionary) -> Button:
	var d := Data.vehicle_def(v.def)
	var reason := VehicleManager.available_reason(v)
	if reason == "":
		reason = VehicleManager.fit_reason(v, contract)
	var sel: bool = v.id == vehicle_id
	var btn := UIKit.button("", "", "", Vector2(190, 214))
	choice_style(btn, sel)
	var col := UIKit.vbox(2)
	col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	col.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	col.offset_top = 6
	col.offset_bottom = -8
	btn.add_child(col)
	var th := ThumbnailRenderer.rect(v.def, v.paint, Vector2(170, 96))
	th.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	col.add_child(th)
	var tc := UIKit.TEXT
	var nl := UIKit.label(str(d.name), 22, tc, true, HORIZONTAL_ALIGNMENT_CENTER)
	nl.clip_text = true
	nl.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	col.add_child(nl)
	var sub := tr("Cap %d  ·  x%.1f") % [VehicleManager.capacity(v), VehicleManager.speed_mult(v)] if reason == "" else reason
	var subl := UIKit.label(sub, 20, UIKit.DIM if reason == "" else UIKit.ORANGE, false, HORIZONTAL_ALIGNMENT_CENTER)
	subl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART   # long languages (ru, de) wrap instead of being cut off
	col.add_child(subl)
	btn.disabled = reason != "" and not sel
	if reason != "":
		btn.modulate = Color(1, 1, 1, 0.55)
	btn.pressed.connect(func():
		vehicle_id = v.id
		if ProgressionManager.auto_enabled("regional_manager"):
			route_id = ContractManager.smart_route(contract, v)
		refresh())
	return btn


func _route_button(r: Dictionary, v: Dictionary) -> Button:
	var sel: bool = r.id == route_id
	var e := ContractManager.estimate(contract, v, r.id)
	var btn := UIKit.button("", "", "", Vector2(0, 228))
	choice_style(btn, sel)
	btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var col := UIKit.vbox(4)
	col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	col.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	col.offset_left = 10
	col.offset_right = -10
	col.offset_top = 10
	btn.add_child(col)
	var tc := UIKit.GOLD if sel else UIKit.TEXT
	var head := UIKit.hbox(6)
	head.mouse_filter = Control.MOUSE_FILTER_IGNORE
	head.alignment = BoxContainer.ALIGNMENT_CENTER
	col.add_child(head)
	head.add_child(UIKit.icon(str(r.icon), 30))
	head.add_child(UIKit.label(str(r.name), 26, tc, true))
	var rlabel := UIKit.label(str(r.label), 20, UIKit.DIM, false, HORIZONTAL_ALIGNMENT_CENTER)
	rlabel.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	col.add_child(rlabel)
	col.add_child(UIKit.label(Fmt.time(float(e.time)), 26, tc, true, HORIZONTAL_ALIGNMENT_CENTER))
	var cost_l := UIKit.label(tr("Cost %s") % Fmt.money(float(e.cost)), 22, UIKit.GOLD, true, HORIZONTAL_ALIGNMENT_CENTER)
	col.add_child(cost_l)
	var risk := float(e.risk)
	var rl := UIKit.label(tr("Risk %d%%") % int(risk), 22, Fmt.risk_color(risk), true, HORIZONTAL_ALIGNMENT_CENTER)
	col.add_child(rl)
	btn.pressed.connect(func():
		route_id = r.id
		refresh())
	return btn


func _dispatch() -> void:
	var v := VehicleManager.get_vehicle(vehicle_id)
	var job := ContractManager.dispatch(contract, v, route_id)
	if job.is_empty():
		AudioManager.play("error")
		return
	UIManager.haptic(25)
	close()
