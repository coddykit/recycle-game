extends Sheet
## Region overview: heat, payouts, unlocks and regional depots.

var _prev_distance := 0.0
var _prev_target := Vector3.ZERO
var _unlocked := -1   ## a region opened while the map was up: the camera goes there on close


func _init() -> void:
	super._init()
	title = "World Map"
	icon_name = "map"
	height_frac = 0.5


func on_opened() -> void:
	RegionManager.region_unlocked.connect(_on_change)
	RegionManager.region_unlocked.connect(func(i): _unlocked = int(i))
	RegionManager.depot_built.connect(_on_change)
	EconomyManager.cash_changed.connect(func(_a, _b): refresh())
	ProgressionManager.business_upgraded.connect(func(_a, _b): refresh())
	var w: World = UIManager.world
	if w:
		_prev_distance = w.rig.distance
		_prev_target = w.rig.target
		var focus := RegionManager.unlocked_count
		if typeof(arg) == TYPE_INT:
			focus = int(arg)
			w.rig.focus_on(RegionManager.center(focus) + Vector3(0, 0, 14), 85.0, 0.9)
		else:
			_frame_overview(w, mini(focus, RegionManager.count() - 1))


## W66 round 2 (10-27): the overview (home region + the next one) was a fixed offset and zoom, so the next region's
## name and its price sign ($2,000 ★5) landed under the HUD at the top of the strip above the sheet. The camera now
## fits both regions' ground and their labels into that strip: below the HUD, above this sheet (CameraRig.fit_into,
## the same search as the home frame). One search on open; nothing per frame.
func _frame_overview(w: World, last: int) -> void:
	var vp := get_viewport_rect().size
	var top: float = hud.top_band_bottom() + 12.0
	var bottom: float = vp.y - hud.nav_height() - _cap_h - 12.0
	if bottom - top < 120.0:
		top = vp.y * 0.12
		bottom = maxf(top + 120.0, bottom)
	var pts := PackedVector3Array()
	var half := RegionManager.tile_size() * 0.18   # the towns, not the whole tile: labels stay large enough to read
	for i in [0, last]:
		var c := RegionManager.center(i)
		for off in [Vector3(-half, 0, -half), Vector3(half, 0, -half), Vector3(-half, 0, half), Vector3(half, 0, half)]:
			pts.append(c + off)
		pts.append(c + Vector3(0, 11.5, 0))   # top of the region name label (8.6 m + its line)
	w.rig.reset_rotation()
	var fit: Array = w.rig.fit_into(pts, Rect2(vp.x * 0.05, top, vp.x * 0.90, bottom - top))
	_widened = true
	w.rig.max_distance = maxf(w.rig.max_distance, float(fit[1]))
	w.rig.focus_on(fit[0], float(fit[1]), 0.9)


var _widened := false


func on_closed() -> void:
	var w: World = UIManager.world
	if w and _widened:
		w.rig.max_distance = w.rig.home_distance   # fit_active's own limit (the complete active frame)
	if w and _prev_distance > 0:
		# polish 10-27 (W66): only the zoom came back, the camera stayed on the overview's midpoint (open sea between
		# the home valley and the next region) — the end of the tutorial looked at water. Back to where the player was.
		var to := RegionManager.center(_unlocked) + Vector3(0, 0, 14) if _unlocked >= 0 else _prev_target
		w.rig.focus_on(to, minf(_prev_distance, 60.0), 0.6)


func _on_change(_i) -> void:
	refresh()


func build(b: VBoxContainer) -> void:
	set_sub([tr("Regions %d/%d  ·  Depot permits %d/%d") % [RegionManager.unlocked_count, RegionManager.count(),
			RegionManager.depots.size(), RegionManager.depot_permits()]])
	for i in RegionManager.count():
		b.add_child(_region_card(i))


## One region: [map icon] name + tagline · pay · risk [one pill: unlock / build depot]; a second line with its heat or
## its state; Medium / High: the heat as the card's bar. A tap on the row flies the camera there.
func _region_card(i: int) -> Control:
	var r := Data.region_at(i)
	var unlocked := RegionManager.is_unlocked(i)
	var is_next := i == RegionManager.unlocked_count
	var econ := "%s  ·  %s" % [tr("Pays x%.2f") % float(r.econ), tr("Risk +%d") % int(r.risk)]
	var sub: Array = ["%s  ·  %s" % [tr(str(r.tagline)), econ], econ]
	var locked := not unlocked and not is_next
	if locked:
		sub = [tr("Unlock %s first") % RegionManager.region_name(i - 1)]
	var card := row("map", str(r.name), sub, {"locked": locked, "selected": is_next})
	card.name = "region_%d" % i
	tap_row(card, func():
		if UIManager.world:
			UIManager.world.rig.focus_on(RegionManager.center(i) + Vector3(0, 0, 8), 55.0))
	var texts := card.get_node("Col/Head/Texts") as VBoxContainer
	var note := ""
	var note_color := UIKit.DIM
	var heat_bar := -1.0
	var blocked := ""
	if unlocked:
		var heat := HeatManager.get_heat(i)
		var heat_txt := tr("Heat %d · %s") % [int(heat), Fmt.heat_label(heat)]
		if i == 0:
			note = "%s  ·  %s" % [heat_txt, tr("Headquarters region")]
		elif RegionManager.depots.has(i):
			note = "%s  ·  %s" % [heat_txt, tr("Regional depot active - vehicles base here")]
		else:
			note = heat_txt
			var dc := RegionManager.depot_check(i)
			# the price alone on the pill (with the depot glyph on Medium / High); the reason it is blocked is its own line
			var db := Kit.action_pill(Fmt.money(RegionManager.depot_cost(i)), func(): RegionManager.build_depot(i), bool(dc.ok), "tonal", "warehouse", low())
			db.custom_minimum_size.x = 84
			db.name = "depot"
			db.tooltip_text = tr("Build depot %s") % Fmt.money(RegionManager.depot_cost(i))
			Kit.list_card_side(card, db)
			if not dc.ok and str(dc.reason).begins_with("Needs"):
				blocked = tr(str(dc.reason))
		note_color = Fmt.heat_color(heat)
		heat_bar = heat / 100.0
	elif is_next:
		var rep_ok := EconomyManager.reputation >= int(r.unlock_rep)
		note = tr("Needs %d reputation") % int(r.unlock_rep)
		note_color = UIKit.GREEN if rep_ok else UIKit.ORANGE
		var ub := pill(card, Fmt.money(float(r.unlock_cost)), func():
			if RegionManager.unlock(i):
				close(), bool(RegionManager.unlock_check(i).ok))
		ub.name = "unlock"
		UIManager.register_target("region_card_%d" % i, ub)
	if note != "":
		var nl := Kit.fit_line([note])
		nl.name = "Note"
		nl.add_theme_color_override("font_color", note_color)
		texts.add_child(nl)
	if blocked != "":
		var bl := Kit.fit_line([blocked])
		bl.name = "Blocked"
		bl.add_theme_color_override("font_color", UIKit.ORANGE)
		texts.add_child(bl)
	if heat_bar >= 0.0:
		progress(card, heat_bar)
	return card
