extends Sheet
const UiRow = preload("res://addons/coddy_ui/ui_row.gd")
## Vehicles, vehicle upgrades, the showroom and drivers.

const PAINTS := ["E05A47", "3D8BD9", "5DBB63", "FFD23F", "8E5CD9", "F4F1EA", "2B2F36", "F28C28", "2BB3A8", "F06EAA"]
## Paid paint sets (coddy_iap features): [feature, title, colours]. Looks only; a locked swatch opens the shop on its product.
const PAINT_SETS := [
	["paint_street", "Street paints", ["FF3D81", "00E5FF", "B8FF3C", "FF6B1A", "7A5CFF", "FFE600"]],
	["paint_military", "Military paints", ["556B2F", "6B705C", "8B7D5B", "3E4A3D", "4B5320", "5A6670"]],
	["paint_luxury", "Luxury paints", ["F2EAD3", "C9A66B", "7B1E2B", "0F5B4D", "0B1F4D", "E8B4B8"]],
	["paint_gold", "Kingpin gold", ["D4AF37", "F0C040", "8C6A1F"]],
]

var tab := "Vehicles"
var detail_id := ""
var _picking_driver := false


func _init() -> void:
	super._init()
	title = "Fleet"
	icon_name = "truck"
	height_frac = 0.66


func on_opened() -> void:
	if typeof(arg) == TYPE_DICTIONARY:
		detail_id = str(arg.get("vehicle", ""))
		tab = str(arg.get("tab", tab))
	elif TutorialManager.current().get("id", "") == "upgrade" and VehicleManager.vehicles.size() > 0:
		detail_id = VehicleManager.vehicles[0].id
	VehicleManager.vehicle_changed.connect(_changed)
	VehicleManager.vehicle_added.connect(_changed)
	VehicleManager.drivers_changed.connect(refresh)
	VehicleManager.market_changed.connect(refresh)
	Monetization.changed.connect(refresh)
	EconomyManager.cash_changed.connect(_cash_changed)


func _changed(_v) -> void:
	refresh()


func _cash_changed(_c, _d) -> void:
	refresh()


func build(b: VBoxContainer) -> void:
	if detail_id != "" and not VehicleManager.get_vehicle(detail_id).is_empty():
		tabs_row.visible = false
		_build_detail(b, VehicleManager.get_vehicle(detail_id))
		return
	detail_id = ""
	set_title("Fleet")
	set_tabs(["Vehicles", "Showroom", "Drivers"], tab, func(t):
		tab = t
		refresh())
	match tab:
		"Vehicles":
			_build_vehicles(b)
		"Showroom":
			_build_showroom(b)
		"Drivers":
			_build_drivers(b)


# --- vehicles list -------------------------------------------------------------------
func _build_vehicles(b: VBoxContainer) -> void:
	set_sub([tr("Garage spaces %d/%d") % [VehicleManager.vehicles.size(), ProgressionManager.vehicle_slots()] + "  ·  " \
			+ tr("Payroll %d/%d") % [VehicleManager.drivers.size(), ProgressionManager.driver_slots()],
			tr("Garage spaces %d/%d") % [VehicleManager.vehicles.size(), ProgressionManager.vehicle_slots()]])
	for v in VehicleManager.vehicles:
		b.add_child(_vehicle_card(v))
	if VehicleManager.vehicles.size() < ProgressionManager.vehicle_slots():
		var add := Kit.button(tr("Buy a new vehicle"), "tonal", "truck", func():
			tab = "Showroom"
			refresh())
		add.name = "buy_vehicle"
		b.add_child(add)
	else:
		b.add_child(UIKit.wlabel(tr("Garage full - upgrade your HQ or Fleet Capacity for more space."), "CaptionMuted"))


## A list row whose tile is the vehicle's own 3D thumbnail (Low too: it was there before).
func _thumb_row(def_id: String, paint: String, title_text: String, sub, opts: Dictionary = {}) -> PanelContainer:
	var card := row("", title_text, sub, opts)
	var head := card.get_node("Col/Head")
	var old := head.get_node_or_null("Tile")
	if old != null:
		old.free()
	var th := ThumbnailRenderer.rect(def_id, paint, Vector2(112, 80))
	th.name = "Tile"
	th.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	if bool(opts.get("locked", false)):
		th.modulate = Color(1, 1, 1, 0.45)
	head.add_child(th)
	head.move_child(th, 0)
	return card


func _vehicle_card(v: Dictionary) -> Control:
	var d := Data.vehicle_def(v.def)
	var dr := VehicleManager.get_driver(v.driver)
	var status := _status_text(v)
	var st := tr(str(status[0]))
	var who := str(dr.name) if not dr.is_empty() else tr("No driver")
	var stats := tr("Cap %d  ·  x%.1f") % [VehicleManager.capacity(v), VehicleManager.speed_mult(v)]
	var lvl := VehicleManager.total_upgrades(v)
	var name := tr(str(d.name)) + ("  +%d" % lvl if lvl > 0 else "")
	var card := _thumb_row(str(v.def), str(v.paint), name, ["%s  ·  %s  ·  %s" % [st, who, stats], "%s  ·  %s" % [st, who], st])
	card.name = "vehicle_" + str(v.id)
	var line: Label = card.get_node_or_null("Col/Head/Texts/Sub")
	if line != null and (dr.is_empty() or v.state == "repair"):
		line.add_theme_color_override("font_color", status[1])
	var open_detail := func():
		detail_id = v.id
		_picking_driver = dr.is_empty()
		refresh()
	if dr.is_empty() and v.state != "busy":
		pill(card, "Assign a driver", open_detail)
	else:
		pill(card, "Upgrades", open_detail, true, "tonal")
	tap_row(card, open_detail)
	return card


func _status_text(v: Dictionary) -> Array:
	match v.state:
		"busy":
			var j := ContractManager.get_job_for_vehicle(v.id)
			if not j.is_empty() and j.phase == "back":
				return ["RETURNING", UIKit.BLUE]
			if not j.is_empty() and j.phase == "unload":
				return ["UNLOADING", UIKit.GOLD]
			return ["ON A JOB", UIKit.GREEN]
		"repair":
			return [tr("REPAIRING %s") % Fmt.time(float(v.repair_left)), UIKit.ORANGE]
	if v.driver == "":
		return ["NEEDS DRIVER", UIKit.RED]
	return ["READY", UIKit.CYAN]


# --- vehicle detail ----------------------------------------------------------------------
func _build_detail(b: VBoxContainer, v: Dictionary) -> void:
	var d := Data.vehicle_def(v.def)
	set_title(str(d.name))
	var status := _status_text(v)
	var drv := VehicleManager.get_driver(v.driver)
	set_sub(["%s  ·  %s  ·  %s" % [tr(str(status[0])), str(drv.name) if not drv.is_empty() else tr("No driver"), tr("Trips completed: %d") % int(v.trips)],
			"%s  ·  %s" % [tr(str(status[0])), str(drv.name) if not drv.is_empty() else tr("No driver")], tr(str(status[0]))])
	var back := Kit.icon_button("chevron-left", func():
		detail_id = ""
		refresh(), tr("Fleet"), true)
	back.name = "back"
	back.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	header_extra.add_child(back)
	if UIManager.world:
		UIManager.world.select_vehicle(v.id, true)
	# hero
	var hero := Kit.card("Card")
	b.add_child(hero)
	var hr := UIKit.hbox(14)
	hero.add_child(hr)
	hr.add_child(ThumbnailRenderer.rect(v.def, v.paint, Vector2(230, 160)))
	var hc := UIKit.vbox(8)
	hc.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	hc.alignment = BoxContainer.ALIGNMENT_CENTER
	hr.add_child(hc)
	hc.add_child(UIKit.wlabel(tr(str(d.blurb)), "CaptionMuted"))
	var tags := UIKit.flow(6, 6)   # wraps: long Russian / German tag names never widen the card
	hc.add_child(tags)
	for t in d.tags:
		tags.add_child(Kit.card_badge(UIKit.upper(tr(str(t))), "level"))
	if v.state == "repair":
		var cost := VehicleManager.instant_repair_cost(v)
		var rb := UIKit.button(tr("Repair now %s") % Fmt.money(cost), "BtnGold", "wrench", Vector2(0, 84), 22)
		rb.disabled = not EconomyManager.can_afford(cost)
		rb.pressed.connect(func(): VehicleManager.instant_repair(v))
		hc.add_child(rb)
		if Monetization.placement_ready("repair_finish"): # opt-in rewarded ad (ad-free: no video)
			Monetization.note_offer("repair_finish")
			var ab := UIKit.button(tr("Finish repair") if Monetization.video_free() else tr("Watch ad · finish repair"), "", "wrench", Vector2(0, 84), 22)
			ab.theme_type_variation = "OutlineButton"
			ab.name = "repair_ad"
			ab.pressed.connect(func(): Monetization.claim_placement("repair_finish", {"vehicle": str(v.id)}))
			hc.add_child(ab)
	# stats
	var stats := Kit.card("Card")
	b.add_child(stats)
	var sv := UIKit.vbox(8)
	stats.add_child(sv)
	_stat_row(sv, "box", "Capacity", str(VehicleManager.capacity(v)), VehicleManager.capacity(v) / 30.0, UIKit.GOLD)
	_stat_row(sv, "bolt", "Speed", "x%.2f" % VehicleManager.speed_mult(v), VehicleManager.speed_mult(v) / 2.5, UIKit.CYAN)
	_stat_row(sv, "shield", "Reliability", "%d" % int(VehicleManager.reliability(v)), VehicleManager.reliability(v) / 100.0, UIKit.GREEN)
	var rm := VehicleManager.risk_mod(v)
	_stat_row(sv, "risk", "Risk modifier", ("%+d%%" % int(rm)), clampf(0.5 + rm / 40.0, 0.0, 1.0), UIKit.ORANGE)
	_stat_row(sv, "cash", "Running cost / trip", Fmt.money(VehicleManager.op_cost(v)), clampf(VehicleManager.op_cost(v) / 600.0, 0.02, 1.0), UIKit.RED)
	# driver
	b.add_child(section("Driver"))
	var dr := VehicleManager.get_driver(v.driver)
	var toggle_pick := func():
		_picking_driver = not _picking_driver
		refresh()
	if not dr.is_empty():
		var dc := _driver_card(dr, false)
		b.add_child(dc)
		var ch := pill(dc, "Change driver", toggle_pick, v.state != "busy", "tonal")
		ch.name = "change_driver"
	else:
		var e := Kit.empty_card("users", tr("No driver"), "", tr("Assign a driver"), toggle_pick, low())
		e.name = "no_driver"
		b.add_child(e)
	if _picking_driver:
		var others := VehicleManager.drivers.filter(func(x): return x.id != v.driver)
		if others.is_empty():
			b.add_child(Kit.empty_card("users", tr("No other drivers. Hire more in the Drivers tab."), "", tr("Drivers"), func():
				detail_id = ""
				tab = "Drivers"
				refresh(), low()))
		for od in others:
			var assigned := VehicleManager.driver_vehicle(od.id)
			var where := tr("in %s") % tr(str(Data.vehicle_def(assigned.def).name)) if not assigned.is_empty() else tr("available")
			var pc := row("driver", str(od.name), ["%s  ·  %s" % [tr("Lv %d") % int(od.level), where], where])
			pc.name = "pick_" + str(od.id)
			var pick := pill(pc, "Assign", func():
				VehicleManager.assign_driver(v, od.id)
				_picking_driver = false
				AudioManager.play("accept")
				refresh(), assigned.is_empty() or assigned.state != "busy")
			pick.name = "assign"
			b.add_child(pc)
	# upgrades
	b.add_child(section("Upgrades"))
	for u in Data.vehicle_upgrades:
		b.add_child(_upgrade_row(v, u))
	# paint
	b.add_child(section("Paint job"))
	var pr2 := GridContainer.new()
	pr2.columns = 5
	pr2.add_theme_constant_override("h_separation", 12)
	pr2.add_theme_constant_override("v_separation", 12)
	b.add_child(pr2)
	for hex in PAINTS:
		var sw := Button.new()
		sw.custom_minimum_size = Vector2(88, 88)
		sw.focus_mode = Control.FOCUS_NONE
		sw.mouse_filter = Control.MOUSE_FILTER_PASS
		var st := StyleBoxFlat.new()
		st.bg_color = Data.color(hex)
		st.set_corner_radius_all(28)
		if hex == str(v.paint):
			st.set_border_width_all(5)
			st.border_color = Color.WHITE
		sw.add_theme_stylebox_override("normal", st)
		sw.add_theme_stylebox_override("hover", st)
		sw.add_theme_stylebox_override("pressed", st)
		sw.pressed.connect(func():
			AudioManager.play("pop")
			VehicleManager.set_paint(v, hex))
		pr2.add_child(sw)
	for ps in PAINT_SETS:
		_paint_set(b, v, str(ps[0]), str(ps[1]), ps[2])


func _paint_set(b: VBoxContainer, v: Dictionary, feature: String, set_title: String, colors: Array) -> void:
	var unlocked := Iap.has(feature)
	var head := UIKit.hbox(8)
	b.add_child(head)
	var t := section(set_title)
	t.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	head.add_child(t)
	if not unlocked:
		var lk := Kit.icon("lock", 28, "on_surface_muted")
		lk.size_flags_vertical = Control.SIZE_SHRINK_END
		head.add_child(lk)
	var grid := GridContainer.new()
	grid.columns = 6
	grid.add_theme_constant_override("h_separation", 10)
	grid.add_theme_constant_override("v_separation", 10)
	b.add_child(grid)
	for hex in colors:
		var sw := Button.new()
		sw.name = "paint_" + str(hex)
		sw.custom_minimum_size = Vector2(88, 88)
		sw.focus_mode = Control.FOCUS_NONE
		sw.mouse_filter = Control.MOUSE_FILTER_PASS
		var st := StyleBoxFlat.new()
		st.bg_color = Data.color(str(hex)) if unlocked else Data.color(str(hex)).darkened(0.45)
		st.set_corner_radius_all(26)
		if str(hex) == str(v.paint):
			st.set_border_width_all(5)
			st.border_color = Color.WHITE
		for state in ["normal", "hover", "pressed"]:
			sw.add_theme_stylebox_override(state, st)
		sw.pressed.connect(func():
			if not unlocked:
				AudioManager.play("error", 0.0, -6.0)
				Iap.locked(feature)
				return
			AudioManager.play("pop")
			VehicleManager.set_paint(v, str(hex)))
		grid.add_child(sw)


func _stat_row(parent: Control, icon_name: String, name: String, value: String, frac: float, color: Color) -> void:
	var h := UIKit.hbox(10)
	parent.add_child(h)
	h.add_child(UIKit.icon(icon_name, 30))
	var l := UIKit.label(name, 22, UIKit.DIM)
	l.custom_minimum_size.x = 230
	h.add_child(l)
	var bar := UIKit.bar(clampf(frac, 0.02, 1.0), color, 12)
	bar.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	h.add_child(bar)
	var vl := UIKit.label(value, 22, UIKit.TEXT, true, HORIZONTAL_ALIGNMENT_RIGHT)
	vl.custom_minimum_size.x = 100
	h.add_child(vl)


## One vehicle upgrade: [icon] name + what it does [price pill]; Medium / High: its level as the card's bar.
func _upgrade_row(v: Dictionary, u: Dictionary) -> Control:
	var lvl := VehicleManager.upgrade_level(v, u.id)
	var card := row(str(u.icon), str(u.name), [tr("Lv %d") % lvl + " / %d  ·  " % int(u.max) + tr(str(u.desc)), tr(str(u.desc))])
	card.name = "upgrade_" + str(u.id)
	var check := VehicleManager.upgrade_check(v, u.id)
	var btn: Button
	if check.get("maxed", false):
		btn = pill(card, "MAX", Callable(), false, "tonal")
	else:
		btn = pill(card, Fmt.money(VehicleManager.upgrade_cost(v, u.id)), func():
			if VehicleManager.upgrade(v, u.id):
				refresh(), bool(check.ok))
	btn.name = "buy"
	progress(card, float(lvl), float(u.max))
	if u.id == "engine":
		UIManager.register_target("upgrade_engine", btn)
	return card


# --- showroom ------------------------------------------------------------------------------
func _build_showroom(b: VBoxContainer) -> void:
	set_sub([tr("Spaces %d/%d") % [VehicleManager.vehicles.size(), ProgressionManager.vehicle_slots()]])
	for d in Data.vehicles:
		var check := VehicleManager.buy_check(d.id)
		var locked: bool = check.get("locked", false)
		var tags := ", ".join(d.tags.map(func(t): return tr(str(t))))
		var facts := "%s  ·  %s" % [tr("TIER %d") % int(d.tier), tr("Cap %d  ·  x%.1f") % [int(d.capacity), float(d.speed)]]
		var sub: Array = [str(check.reason)] if locked else ["%s  ·  %s" % [facts, tags], facts]
		var card := _thumb_row(str(d.id), str(d.paint), str(d.name), sub, {"locked": locked})
		card.name = "show_" + str(d.id)
		b.add_child(card)
		if locked:
			var line: Label = card.get_node_or_null("Col/Head/Texts/Sub")
			if line != null:
				line.add_theme_color_override("font_color", UIKit.ORANGE)
			continue
		var btn: Button
		if check.get("full", false):
			btn = pill(card, "Garage full", Callable(), false, "tonal")
		else:
			btn = pill(card, Fmt.money(float(d.price)), func():
				var nv := VehicleManager.buy(d.id)
				if not nv.is_empty():
					AudioManager.play("cash")
					detail_id = ""
					tab = "Vehicles"
					refresh(), bool(check.ok))
		btn.name = "buy"


# --- drivers --------------------------------------------------------------------------------
func _build_drivers(b: VBoxContainer) -> void:
	set_sub([tr("Payroll %d/%d") % [VehicleManager.drivers.size(), ProgressionManager.driver_slots()]])
	if not Iap.has("driver_portraits"):
		var faces := UiRow.nav("lock", tr("Driver portraits"), "", func(): Iap.locked("driver_portraits"), "neutral")
		faces.name = "locked_portraits"
		b.add_child(faces)
	for d in VehicleManager.drivers:
		var dc := _driver_card(d, true)
		b.add_child(dc)
		var veh := VehicleManager.driver_vehicle(d.id)
		if veh.is_empty() and VehicleManager.drivers.size() > 1:
			var dis := Kit.icon_button("x", func(): VehicleManager.dismiss_driver(d.id), tr("Close"), true)
			dis.name = "dismiss"
			Kit.list_card_side(dc, dis)
	var mh := UIKit.hbox(8)
	b.add_child(mh)
	var board := section(tr("Hiring board  ·  new faces in %s") % Fmt.time(VehicleManager.market_timer))
	board.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	mh.add_child(board)
	var rf := UIKit.button(Fmt.money(VehicleManager.market_refresh_cost()), "", "refresh", Vector2(0, 84), 22)
	rf.theme_type_variation = "OutlineButton"
	rf.name = "market_refresh"
	rf.autowrap_mode = TextServer.AUTOWRAP_OFF
	rf.add_theme_constant_override("icon_max_width", 30)
	rf.size_flags_vertical = Control.SIZE_SHRINK_END
	rf.disabled = not EconomyManager.can_afford(VehicleManager.market_refresh_cost())
	rf.pressed.connect(func(): VehicleManager.paid_market_refresh())
	mh.add_child(rf)
	for d in VehicleManager.market:
		var c := _driver_card(d, false)
		b.add_child(c)
		var check := VehicleManager.hire_check(d)
		var hb: Button
		if check.get("full", false):
			hb = pill(c, "Payroll full", Callable(), false, "tonal")
		else:
			hb = pill(c, Fmt.money(VehicleManager.hire_cost(d)), func():
				if VehicleManager.hire(d):
					AudioManager.play("cash")
					UIManager.toast(tr("%s hired!") % d.name, UIKit.GREEN, "driver"), bool(check.ok))
		hb.name = "hire"


## A driver row: [portrait / icon] name + rarity · level · wage (· vehicle) and, as a second grey line, the three skills.
func _driver_card(d: Dictionary, owned: bool) -> PanelContainer:
	var rarity := UIKit.upper(tr(str(Data.rarity(d.rarity).name)))
	var pay := tr("Lv %d  ·  %s / trip") % [int(d.level), Fmt.money(float(Data.rarity(d.rarity).salary))]
	var alts: Array = ["%s  ·  %s" % [rarity, pay], pay]
	if owned:
		var veh := VehicleManager.driver_vehicle(d.id)
		var where := tr(str(Data.vehicle_def(veh.def).name)) if not veh.is_empty() else tr("Benched")
		alts = ["%s  ·  %s  ·  %s" % [rarity, pay, where], "%s  ·  %s" % [pay, where], where]
	var card := row("driver", str(d.name), alts)
	card.name = "driver_" + str(d.id)
	var head := card.get_node("Col/Head")
	if Iap.has("driver_portraits") and not low():
		var t := head.get_node_or_null("Tile")
		if t != null:
			t.free()
		var face := DriverPortrait.make(d, Kit.CARD_TILE)
		face.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		head.add_child(face)
		head.move_child(face, 0)
	var skills: Array = []
	for pair in [["speed", "Speed"], ["reliability", "Reliab."], ["stress", "Nerves"]]:
		skills.append("%s %d" % [tr(pair[1]), int(VehicleManager.driver_stat(d, pair[0]))])
	var sk := Kit.fit_line(["  ·  ".join(skills)])
	sk.name = "Skills"
	(card.get_node("Col/Head/Texts") as Control).add_child(sk)
	return card
