extends Control
const UiSafeArea := preload("res://addons/coddy_ui/ui_safe_area.gd") ## safe area incl. the Android navigation bar (owner 10-26)
const Kit = preload("res://addons/coddy_ui/ui_kit.gd")
const UiScroll = preload("res://addons/coddy_ui/ui_scroll.gd")
const UiToast = preload("res://addons/coddy_ui/ui_toast.gd")
## Main mobile HUD: top resource bar, goal tracker, active-job strip,
## bottom navigation, sheets, modals, toasts, floating feedback, tutorial.

const NAV := [
	["contracts", "tab.contracts", "clipboard-list"],
	["fleet", "tab.fleet", "truck"],
	["map", "tab.map", "map"],
	["upgrades", "tab.upgrades", "circle-arrow-up"],
	["business", "tab.business", "briefcase"],
]
const NAV_H := 124.0
## The tab bar's real height above the bottom safe area: at least NAV_H, more when its kit tabs + padding are taller
## (measured in _build_nav, before anything is laid out above it) — the tabs never hang into the navigation bar.
var _nav_h := NAV_H

var world: World
var current_sheet: Sheet = null
var top_inset := 0.0
var bottom_inset := 0.0

var cash_label: Label
var cash_chip: Control
var rep_label: Label
var heat_label: Label
var heat_chip: Control
var goal_chip: PanelContainer
var goal_label: Label
var goal_bar: ProgressBar
var goal_reward: Label
var stage_label: Label
var influence_chip: PanelContainer
var speed_btn: Button   ## ×1 / ×2 game speed (review 10-30 D3); hidden until the tutorial is over
var influence_label: Label
var jobs_box: HBoxContainer
var jobs_scroll: ScrollContainer
var _jobs_finger := {}
var dispatch_all_btn: Button
var rotate_box: VBoxContainer
var nav_buttons := {}
var badges := {}
var sheet_host: Control
var toast_box: Control
var ui_toast: Control
var _toast_top := 252.0      ## the default toast row (below the goal chip)
var float_layer: Control
var modal_layer: Control
var tutorial: Control
var shop_director: IapDirector
var title_card: Control
var _shown_cash := 0.0
var _float_pool: Array[Label] = []
var _coin_pool: Array[TextureRect] = []
var _modal_queue: Array = []
var _modal_open := false
var _job_cards := {}
var _badge_timer := 0.0


func _ready() -> void:
	UIKit.configure(str(GameManager.settings.get("theme", "system")))
	theme = UIKit.theme()
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	UIManager.hud = self
	add_child(ThumbnailRenderer.new())
	_compute_insets()
	_build_top()
	_build_nav()   # first: it measures the tab bar the strips above it are placed against
	_build_jobs_strip()
	sheet_host = Control.new()
	sheet_host.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	sheet_host.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(sheet_host)
	move_child(sheet_host, 0)
	float_layer = Control.new()
	float_layer.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	float_layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(float_layer)
	move_child(float_layer, 0)
	toast_box = Control.new()   # (debug QA drivers clear its children; the toasts themselves are the kit's UiToast below)
	toast_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(toast_box)
	ui_toast = UiToast.new()
	_toast_top = top_inset + 252
	ui_toast.setup({"top": _toast_top, "max_width": 640})
	add_child(ui_toast)
	modal_layer = Control.new()
	modal_layer.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	modal_layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(modal_layer)
	tutorial = TutorialOverlay.new()
	add_child(tutorial)
	# shop glue (coddy_iap): a locked feature opens the shop on its product, bought packs land in the game, one-off offers
	shop_director = IapDirector.new()
	shop_director.setup({"open_shop": func(focus): open_panel("shop", {"focus": focus}), "deliver": Monetization.deliver,
			"show_offer": _show_offer})
	add_child(shop_director)
	_shown_cash = EconomyManager.cash
	_connect_signals()
	_update_resources(true)
	_update_goal()
	_rebuild_jobs()
	_update_tabs(true)
	_attach_banner()


func setup(w: World) -> void:
	world = w
	world.vehicle_tapped.connect(_on_vehicle_tapped)
	world.hq_tapped.connect(func(): open_panel("upgrades"))
	world.region_tapped.connect(func(i): open_panel("map", i))
	_show_title()
	for j in EventManager.pending_jobs():
		_queue_event(j)
	if GameManager.has_meta("prestige_stars"):
		var st := int(GameManager.get_meta("prestige_stars"))
		GameManager.remove_meta("prestige_stars")
		AudioManager.play("unlock")
		show_modal("A New Empire Begins", tr("You sold your network for %d Legacy star%s.\nIncome is now +%d%% forever.") % [
				st, UIKit.pl(st), ProgressionManager.legacy() * 10], "star", [["Let's go!", "BtnGold", Callable()]])
		Monetization.check_offer.call_deferred("prestige")
	if not GameManager.pending_return().is_empty():
		_on_offline(GameManager.pending_return())
	_queue_quality_notice()
	if SaveManager.write_protected:
		show_modal("Save unavailable", "This save was created by a newer version. Update the game to continue. Your files have been preserved.")


## Safe-area gaps (canvas px) the HUD keeps its text and buttons inside: UiSafeArea.overlay_edges = the cutout, the
## navigation bar and, at the top, the status bar's height even while immersive mode hides it (an edge swipe brings it
## back over the game — S1 audit 10-28: the stat bar and Settings sat under it). Zero on a desktop window; a simulated
## phone (tests, frames) acts like the phone. Once, at build (portrait only).
func _compute_insets() -> void:
	var e := UiSafeArea.overlay_edges(get_viewport_rect().size)
	top_inset = e.y
	bottom_inset = e.w


## Screen y below the top bar (stat bar + goal chip): where the 3D view starts to be visible (W66 round 2).
func top_band_bottom() -> float:
	var y := top_inset
	for c in [find_child("StatBar", true, false), goal_chip]:
		if c is Control and (c as Control).is_visible_in_tree():
			y = maxf(y, (c as Control).get_global_rect().end.y)
	return y


func nav_height() -> float:
	return _nav_h + bottom_inset


# --- top bar --------------------------------------------------------------------------
func _build_top() -> void:
	var top := MarginContainer.new()
	top.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	top.add_theme_constant_override("margin_left", 16)
	top.add_theme_constant_override("margin_right", 16)
	top.add_theme_constant_override("margin_top", int(top_inset) + 14)
	top.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(top)
	var col := UIKit.vbox(10)
	col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	top.add_child(col)
	var row := UIKit.hbox(10)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	col.add_child(row)
	# the three figures: one glass card (kit stat = icon + value + caption)
	var bar := UIKit.card("Pill")
	bar.name = "StatBar"
	bar.mouse_filter = Control.MOUSE_FILTER_PASS
	bar.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(bar)
	var figures := UIKit.hbox(14)
	figures.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bar.add_child(figures)
	var cash := Kit.stat("coins", "$0", tr("Cash"), "primary")
	cash.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	cash.size_flags_stretch_ratio = 1.5
	figures.add_child(cash)
	cash_label = cash.find_child("Value", true, false)
	cash_chip = cash
	UIManager.register_target("hud_cash", cash)
	var rep := Kit.stat("star", "0", tr("Reputation"), "primary")
	rep.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	figures.add_child(rep)
	rep_label = rep.find_child("Value", true, false)
	var heat := Kit.stat("flame", "0", tr("Heat"), "danger")
	heat.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	heat.mouse_filter = Control.MOUSE_FILTER_STOP
	figures.add_child(heat)
	heat_label = heat.find_child("Value", true, false)
	heat_chip = heat
	heat.gui_input.connect(func(e):
		if _is_tap(e):
			open_panel("map"))
	var gear := Kit.icon_button("settings", func(): open_panel("settings"), tr("Settings"))
	gear.name = "SettingsButton"
	UIManager.register_target("hud_settings", gear)
	row.add_child(gear)
	# goal tracker: stage overline, goal text, progress, reward; influence chip beside it
	var goal_row := UIKit.hbox(10)
	goal_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	col.add_child(goal_row)
	goal_chip = UIKit.card("Pill")
	goal_chip.mouse_filter = Control.MOUSE_FILTER_STOP
	goal_chip.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	goal_chip.gui_input.connect(func(e):
		if _is_tap(e):
			TutorialManager.show_advice())
	goal_row.add_child(goal_chip)
	var g := UIKit.hbox(12)
	g.mouse_filter = Control.MOUSE_FILTER_IGNORE
	goal_chip.add_child(g)
	g.add_child(Kit.tile("target", "primary", 52, true))
	var gcol := UIKit.vbox(2)
	gcol.mouse_filter = Control.MOUSE_FILTER_IGNORE
	gcol.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	gcol.alignment = BoxContainer.ALIGNMENT_CENTER
	g.add_child(gcol)
	stage_label = Kit.label("", "Overline")
	stage_label.clip_text = true
	stage_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	stage_label.custom_minimum_size.x = 1
	gcol.add_child(stage_label)
	goal_label = UIKit.wlabel("", "LabelStrong")
	goal_label.max_lines_visible = 2
	goal_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	goal_label.custom_minimum_size.x = 1
	goal_label.add_theme_font_size_override("font_size", 23)
	gcol.add_child(goal_label)
	goal_bar = Kit.progress(0.0)
	gcol.add_child(goal_bar)
	goal_reward = Kit.label("", "Accent")
	goal_reward.add_theme_color_override("font_color", Kit.c("primary"))
	g.add_child(goal_reward)
	influence_chip = Kit.chip("gem", "0", "accent")
	influence_chip.mouse_filter = Control.MOUSE_FILTER_STOP
	influence_label = influence_chip.find_child("Text", true, false)
	influence_chip.gui_input.connect(func(e):
		if _is_tap(e):
			open_panel("business", {"section": "connections"}))
	goal_row.add_child(influence_chip)
	UIManager.register_target("hud_influence", influence_chip)
	# ×1 / ×2 beside the influence chip: the stat row has no width to spare on a 720 px phone with a big cash figure
	# (layout scan); the goal chip's text clips / wraps instead
	speed_btn = Kit.button("×1", "secondary", "fast-forward", _on_speed_pressed)
	speed_btn.name = "SpeedButton"
	speed_btn.tooltip_text = tr("Game speed")
	speed_btn.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	goal_row.add_child(speed_btn)
	_update_speed_button()


## ×1 ↔ ×2 (GameManager.toggle_game_speed persists it); the button exists only after the tutorial (speed_unlocked).
func _on_speed_pressed() -> void:
	var sp := GameManager.toggle_game_speed()
	_update_speed_button()
	UIManager.toast(tr("Game speed ×%d") % sp, UIKit.GOLD, "")


## Visible only once the tutorial is over; gold while ×2 runs. Cheap: called from the HUD's 0.5 s badge timer.
func _update_speed_button() -> void:
	if speed_btn == null:
		return
	var on := GameManager.speed_unlocked()
	if speed_btn.visible != on:
		speed_btn.visible = on
	var fast := GameManager.game_speed() >= 2.0
	var txt := "×2" if fast else "×1"
	if speed_btn.text != txt:
		speed_btn.text = txt
		speed_btn.theme_type_variation = "PrimaryButton" if fast else "SecondaryButton"


func _is_tap(e: InputEvent) -> bool:
	# ONE tap = one action (touch audit 2026-10-27): a phone sends the touch AND Godot's emulated click, and this project
	# also turns every desktop click into an emulated touch (emulate_touch_from_mouse) — both halves reached here, so
	# open_panel() ran twice and the Heat / goal chips and the new-contract card opened their sheet and shut it again.
	# UiKit.is_tap drops the emulated half.
	return Kit.is_tap(e)


# --- job strip -------------------------------------------------------------------------
func _build_jobs_strip() -> void:
	jobs_scroll = UiScroll.make(false, true)
	jobs_scroll.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	jobs_scroll.offset_top = -(nav_height() + 104)
	jobs_scroll.offset_bottom = -(nav_height() + 8)
	jobs_scroll.offset_left = 12
	jobs_scroll.offset_right = -12
	jobs_scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	jobs_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_SHOW_NEVER
	jobs_scroll.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(jobs_scroll)
	jobs_box = UIKit.hbox(10)
	jobs_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	jobs_scroll.add_child(jobs_box)
	dispatch_all_btn = UIKit.button("Dispatch All", "BtnPrimary", "arrow_right", Vector2(0, 84), 24)
	dispatch_all_btn.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_RIGHT)
	dispatch_all_btn.offset_left = -270
	dispatch_all_btn.offset_right = -16
	dispatch_all_btn.offset_top = -(nav_height() + 204)
	dispatch_all_btn.offset_bottom = -(nav_height() + 120)
	dispatch_all_btn.visible = false
	dispatch_all_btn.mouse_filter = Control.MOUSE_FILTER_STOP
	dispatch_all_btn.pressed.connect(func():
		var n := ContractManager.dispatch_all_ready()
		if n > 0:
			toast(tr("Dispatched %d vehicle%s") % [n, UIKit.pl(n)], UIKit.GREEN, "truck"))
	add_child(dispatch_all_btn)
	rotate_box = UIKit.vbox(8)
	rotate_box.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_RIGHT)
	rotate_box.offset_left = -120
	rotate_box.offset_right = -12
	rotate_box.offset_top = -(nav_height() + 344)
	rotate_box.offset_bottom = -(nav_height() + 120)
	rotate_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(rotate_box)
	for d in [[-1, "rotate_left"], [1, "rotate_right"]]:
		var dir: int = d[0]
		var rb := Kit.icon_button("rotate-ccw" if dir < 0 else "rotate-cw", func():
			if world:
				world.rig.rotate_step(dir), tr("Rotate left") if dir < 0 else tr("Rotate right"))
		rb.mouse_filter = Control.MOUSE_FILTER_STOP
		rotate_box.add_child(rb)
		UIManager.register_target(str(d[1]), rb)


func _rebuild_jobs() -> void:
	for c in jobs_box.get_children():
		c.queue_free()
	_job_cards.clear()
	for j in ContractManager.jobs:
		_add_job_card(j)


func _add_job_card(job: Dictionary) -> void:
	var v := VehicleManager.get_vehicle(job.vehicle_id)
	if v.is_empty():
		return
	var card := UIKit.card("Pill")
	card.mouse_filter = Control.MOUSE_FILTER_STOP
	card.custom_minimum_size = Vector2(230, 84)
	var h := UIKit.hbox(8)
	h.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(h)
	var ic := UIKit.icon(Data.category(job.contract.category).get("icon", "box"), 40)
	h.add_child(ic)
	var col := UIKit.vbox(4)
	col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	h.add_child(col)
	var name_l := UIKit.label(str(Data.vehicle_def(v.def).name), 18, UIKit.DIM, true)
	name_l.clip_text = true
	name_l.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS   # a long vehicle name ends in "...", never a hard cut
	name_l.custom_minimum_size.x = 150
	col.add_child(name_l)
	var eta := UIKit.label("", 22, UIKit.TEXT, true)
	col.add_child(eta)
	var bar := UIKit.bar(0, UIKit.GREEN, 10, 150)
	col.add_child(bar)
	card.gui_input.connect(func(e):
		_job_card_input(e, func():
			var jj := ContractManager.get_job(job.id)
			if not jj.is_empty() and jj.get("pending_event", false):
				_show_event_now(jj)
			elif world:
				world.focus_vehicle(job.vehicle_id)))
	jobs_box.add_child(card)
	_job_cards[job.id] = {"card": card, "eta": eta, "bar": bar, "icon": ic}
	UIKit.pop_in(card)
	if jobs_box.get_child_count() == 1:
		UIManager.register_target("job_first", card)


## A finger on a job card. The strip itself ignores touches (its empty parts belong to the 3D camera); the kit's TouchScroll
## scrolls it sideways when a drag starts on a card (more jobs than fit the width stay reachable, glides on), so the card only
## decides "tap": press and release without having moved opens the job.
func _job_card_input(e: InputEvent, on_tap: Callable) -> void:
	var st := _jobs_finger # one finger state for the whole strip
	if e is InputEventScreenTouch:
		if e.pressed:
			st["down"] = true
			st["moved"] = false
			st["sum"] = Vector2.ZERO
			st["index"] = e.index
		elif st.get("down", false) and e.index == st.get("index", -1):
			st["down"] = false
			if not st.moved:
				on_tap.call()
	elif e is InputEventScreenDrag and st.get("down", false) and e.index == st.get("index", -1):
		st["sum"] = (st.sum as Vector2) + e.relative
		if (st.sum as Vector2).length() > 12.0:
			st["moved"] = true


func _update_job_cards() -> void:
	for id in _job_cards.keys():
		var j := ContractManager.get_job(id)
		var c: Dictionary = _job_cards[id]
		if j.is_empty():
			(c.card as Control).queue_free()
			_job_cards.erase(id)
			continue
		var bar: ProgressBar = c.bar
		var eta: Label = c.eta
		if j.get("pending_event", false):
			eta.text = tr("EVENT!")
			eta.add_theme_color_override("font_color", UIKit.ORANGE)
			bar.modulate = UIKit.ORANGE
			var s := 1.0 if UIKit.reduced_motion() else 1.0 + sin(Time.get_ticks_msec() / 120.0) * 0.04
			(c.card as Control).scale = Vector2(s, s)
		elif j.phase == "unload":
			eta.text = "Unloading"
			eta.add_theme_color_override("font_color", UIKit.GOLD)
			bar.modulate = UIKit.GOLD
			(c.card as Control).scale = Vector2.ONE
		elif j.phase == "out":
			eta.text = Fmt.time(ContractManager.time_left(j))
			eta.add_theme_color_override("font_color", UIKit.TEXT)
			bar.modulate = Color.WHITE
			(c.card as Control).scale = Vector2.ONE
		else:
			eta.text = "Returning"
			eta.add_theme_color_override("font_color", UIKit.DIM)
			bar.modulate = UIKit.BLUE
			(c.card as Control).scale = Vector2.ONE
		bar.value = ContractManager.progress(j)


# --- navigation ------------------------------------------------------------------------
func _build_nav() -> void:
	var nav := UIKit.card("SheetBottom")
	nav.name = "NavBar"
	var sb := (UIKit.theme().get_stylebox("panel", "SheetBottom") as StyleBoxFlat).duplicate() as StyleBoxFlat
	sb.corner_radius_bottom_left = 0
	sb.corner_radius_bottom_right = 0
	sb.content_margin_left = 12
	sb.content_margin_right = 12
	sb.content_margin_top = 8
	sb.content_margin_bottom = 10 + bottom_inset
	nav.add_theme_stylebox_override("panel", sb)
	nav.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	nav.offset_top = -nav_height()
	nav.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(nav)
	var row := UIKit.hbox(4)
	nav.add_child(row)
	for n in NAV:
		var key: String = n[0]
		var b := Kit.tab_button(tr(n[1]), n[2], func(): open_panel(key))
		b.custom_minimum_size = Vector2(0, 96)
		b.add_theme_font_size_override("font_size", 22)
		b.button_down.connect(func(): UIManager.haptic(10))
		var badge := UIKit.pill("", UIKit.RED, Color.WHITE, 18)
		badge.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
		badge.offset_left = -50
		badge.offset_top = 2
		badge.offset_right = -14
		badge.visible = false
		b.add_child(badge)
		var lock := Kit.icon("lock", 28, "on_surface_muted")
		lock.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
		lock.position = Vector2(12, 6)
		lock.visible = false
		b.add_child(lock)
		row.add_child(b)
		nav_buttons[key] = {"button": b, "label": b, "icon": b, "lock": lock}
		badges[key] = badge
		UIManager.register_target("nav_" + key, b)
	_nav_h = maxf(NAV_H, nav.get_combined_minimum_size().y - bottom_inset)
	nav.offset_top = -nav_height()


func _set_badge(key: String, text: String) -> void:
	var b: PanelContainer = badges[key]
	var was := b.visible
	b.visible = text != ""
	if text != "":
		(b.get_child(0) as Label).text = text
		if not was:
			UIKit.pop_in(b)


func _update_badges() -> void:
	_update_tabs()
	var fresh := 0
	for c in ContractManager.offers:
		if c.get("fresh", false):
			fresh += 1
	_set_badge("contracts", str(fresh) if fresh > 0 else ("" if ContractManager.queue.is_empty() else str(ContractManager.queue.size())))
	var up: bool = ProgressionManager.garage_check().ok
	if not up:
		for b in Data.business_upgrades:
			if ProgressionManager.business_check(b.id).ok:
				up = true
				break
	_set_badge("upgrades", "!" if up else "")
	var nxt := RegionManager.next_locked()
	_set_badge("map", "!" if nxt >= 0 and RegionManager.unlock_check(nxt).ok else "")
	var fleet := ""
	for v in VehicleManager.vehicles:
		if VehicleManager.available_reason(v) == "No driver":
			fleet = "!"
	if fleet == "" and VehicleManager.vehicles.size() < ProgressionManager.vehicle_slots():
		# a count, never a "+": a white plus on the red pill reads as the protected Red Cross emblem (S3 #168)
		var buyable := 0
		for d in Data.vehicles:
			if VehicleManager.buy_check(d.id).ok:
				buyable += 1
		if buyable > 0:
			fleet = str(buyable)
	_set_badge("fleet", fleet)
	for k in nav_buttons:
		if not ProgressionManager.tab_unlocked(k):
			_set_badge(k, "")
		elif _new_tabs.has(k):
			_set_badge(k, "NEW")
	for k in nav_buttons:
		var active: bool = UIManager.current_panel == k or (k == "contracts" and UIManager.current_panel == "dispatch")
		(nav_buttons[k].button as Button).set_pressed_no_signal(active)


# --- sheets ------------------------------------------------------------------------------
const SHEETS := {
	"daily": "res://scripts/ui/panels/daily_sheet.gd",
	"contracts": "res://scripts/ui/panels/contracts_sheet.gd",
	"dispatch": "res://scripts/ui/panels/dispatch_sheet.gd",
	"fleet": "res://scripts/ui/panels/fleet_sheet.gd",
	"map": "res://scripts/ui/panels/map_sheet.gd",
	"upgrades": "res://scripts/ui/panels/upgrades_sheet.gd",
	"business": "res://scripts/ui/panels/business_sheet.gd",
	"settings": "res://scripts/ui/panels/settings_sheet.gd",
	"shop": "res://scripts/ui/panels/shop_sheet.gd",
}


var _tabs_seen := {}
var _new_tabs := {}


## Progressive disclosure: tabs unlock as the player learns the game.
func _update_tabs(silent := false) -> void:
	if bool(ProgressionManager.flags.get("tabs_all", false)):
		silent = true
	for k in nav_buttons:
		var unlocked := ProgressionManager.tab_unlocked(k)
		var nb: Dictionary = nav_buttons[k]
		(nb.icon as Control).modulate = Color.WHITE if unlocked else Color(1, 1, 1, 0.28)
		(nb.lock as Control).visible = not unlocked
		if unlocked and not _tabs_seen.get(k, false):
			_tabs_seen[k] = true
			if not silent and k != "contracts" and ProgressionManager.intro_done():
				_reveal_tab(k)
		elif not unlocked:
			_tabs_seen[k] = false


func _reveal_tab(k: String) -> void:
	var nb: Dictionary = nav_buttons[k]
	UIKit.bounce(nb.icon, 1.6)
	AudioManager.play("unlock", 0.0, -6.0)
	AudioManager.voice("tab_" + k, 0.0)
	_new_tabs[k] = true
	_set_badge(k, "NEW")
	var msg := str(Data.onboarding.get("tab_unlocked", {}).get(k, ""))
	if msg != "":
		toast(tr(msg), Color("d9c2ff"), "mentor")


func open_panel(name: String, arg: Variant = null) -> void:
	if not is_inside_tree():   # a late call while the scene is being reloaded
		return
	if nav_buttons.has(name) and not ProgressionManager.tab_unlocked(name):
		toast(tr(ProgressionManager.tab_hint(name)), UIKit.DIM, "lock")
		AudioManager.play("error", 0.0, -6.0)
		return
	if current_sheet and is_instance_valid(current_sheet):
		var same := current_sheet.sheet_name == name and arg == null
		if same:
			current_sheet.close()
		else:
			current_sheet.close_instant()
		current_sheet = null
		if same:
			return
	if not SHEETS.has(name):
		return
	_new_tabs.erase(name)
	var script: GDScript = load(SHEETS[name])
	var s: Sheet = script.new()
	s.sheet_name = name
	sheet_host.add_child(s)
	current_sheet = s
	s.open(self, arg)
	# kit round 2 (10-27): the toast row (under the goal chip) fell on a full-height sheet's header — it keeps off the
	# header and the tab strip, moving under them while the sheet is open (a toast already up too), back when it closes
	ui_toast.keep_clear(s.head.box as Control)
	ui_toast.keep_clear(s.tabs_row)
	_update_badges()


func close_panel() -> void:
	if current_sheet and is_instance_valid(current_sheet):
		current_sheet.close()
	current_sheet = null


# --- signals -----------------------------------------------------------------------------
func _connect_signals() -> void:
	EconomyManager.cash_changed.connect(func(_c, _d): _update_resources())
	EconomyManager.reputation_changed.connect(func(_r, d):
		_update_resources()
		if d > 0:
			UIKit.bounce(rep_label, 1.25))
	HeatManager.heat_changed.connect(_on_heat_changed)
	EconomyManager.influence_changed.connect(func(v, d):
		influence_label.text = str(v)
		if d > 0:
			UIKit.bounce(influence_chip, 1.2))
	ContractManager.job_started.connect(func(j):
		_add_job_card(j)
		_update_badges())
	ContractManager.offers_changed.connect(_on_offers_changed)
	ContractManager.queue_changed.connect(_update_badges)
	ContractManager.job_delivered.connect(func(_j, _r): _update_goal())
	ContractManager.job_failed.connect(func(_j, _r): _update_goal())
	EventManager.event_triggered.connect(_queue_event)
	EventManager.event_resolved.connect(_on_event_resolved)
	ProgressionManager.goal_completed.connect(_on_goal_completed)
	ProgressionManager.goal_changed.connect(_update_goal)
	ProgressionManager.stage_changed.connect(_on_stage_changed)
	ProgressionManager.garage_upgraded.connect(func(l):
		_update_goal()
		toast(tr("Garage upgraded: %s!") % tr(Data.garage_level(l).name), UIKit.GOLD, "garage"))
	ProgressionManager.business_upgraded.connect(func(id, l):
		_update_goal()
		var d := Data.business(id)
		var txt := tr("%s Lv %d") % [tr(d.name), l]
		if id == "automated_dispatch":
			txt = tr("Unlocked: %s") % tr(d.levels[l - 1])
			AudioManager.voice("automation", 5.0)
		toast(txt, UIKit.PURPLE, d.icon))
	RegionManager.region_unlocked.connect(_on_region_unlocked)
	RegionManager.depot_built.connect(func(i):
		_update_goal()
		toast(tr("Depot built in %s") % RegionManager.region_name(i), UIKit.GOLD, "depot"))
	VehicleManager.vehicle_added.connect(func(v):
		_update_goal()
		if not v.get("free", false):
			toast(tr("%s joined the fleet!") % tr(Data.vehicle_def(v.def).name), UIKit.GREEN, "truck"))
	VehicleManager.vehicle_upgraded.connect(func(_v, _s):
		AudioManager.play("upgrade")
		_update_goal())
	GameManager.offline_report.connect(_on_offline)


var _heat_state := {}


func _on_heat_changed(region: int, value: float) -> void:
	_update_resources()
	var label := Fmt.heat_label(value)
	var prev: String = _heat_state.get(region, "Cold")
	_heat_state[region] = label
	if label == prev:
		return
	var name := RegionManager.region_name(region)
	if label == "Critical":
		AudioManager.play("warning")
		AudioManager.voice("heat_critical", 60.0)
		toast(tr("%s heat CRITICAL! Costs +30%%, trips slower.") % name, UIKit.RED, "heat")
		UIKit.bounce(heat_chip, 1.2)
	elif label == "Hot" and prev != "Critical":
		AudioManager.voice("heat_hot", 90.0)
		toast(tr("%s is getting hot. Expect more trouble.") % name, UIKit.ORANGE, "heat")
		UIKit.bounce(heat_chip, 1.12)
	elif label == "Cold" and prev != "Cold":
		toast(tr("%s has cooled down.") % name, UIKit.CYAN, "heat")


var _known_offers := {}
var _offer_toast_cd := 0.0
var _contract_card: Control = null   ## the sliding new-contract card (freed when a window opens over it)


func _on_offers_changed() -> void:
	_update_badges()
	var now := Time.get_ticks_msec() / 1000.0
	var best: Dictionary = {}
	for c in ContractManager.offers:
		if not _known_offers.has(c.id):
			_known_offers[c.id] = true
			if c.get("fresh", false) and (best.is_empty() or float(c.reward) > float(best.reward)):
				best = c
	var tut_busy := not TutorialManager.finished and (TutorialManager.step <= 4 or TutorialManager.is_active())   # not over the coach card
	if best.is_empty() or current_sheet != null or _modal_open or now < _offer_toast_cd or tut_busy:
		return
	_offer_toast_cd = now + 9.0
	_contract_popup(best)
	UIKit.bounce(nav_buttons.contracts.button, 1.15)


## A mini contract card slides in from the right: contracts "arrive as cards".
func _contract_popup(c: Dictionary) -> void:
	var cat := Data.category(c.category)
	var card := PanelContainer.new()
	card.add_theme_stylebox_override("panel", UIKit.card_style(UIKit.CARD, Data.color(cat.get("color", "C4693D")), 10))
	card.mouse_filter = Control.MOUSE_FILTER_STOP
	card.custom_minimum_size = Vector2(430, 0)
	var row := UIKit.hbox(12)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(row)
	row.add_child(UIKit.icon(str(cat.get("icon", "box")), 52))
	var col := UIKit.vbox(0)
	col.mouse_filter = Control.MOUSE_FILTER_IGNORE
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(col)
	col.add_child(UIKit.label("NEW CONTRACT", 16, UIKit.CYAN, true))
	col.add_child(UIKit.label(str(cat.name), 24, UIKit.TEXT, true))
	col.add_child(UIKit.label("> " + RegionManager.node_name(c.dest), 18, UIKit.DIM))
	row.add_child(UIKit.label(Fmt.money(float(c.reward)), 30, UIKit.GOLD, true))
	add_child(card)
	if is_instance_valid(_contract_card):
		_contract_card.queue_free()
	_contract_card = card
	var vp := get_viewport_rect().size
	var y := vp.y - nav_height() - banner_inset - 230
	card.position = Vector2(vp.x + 20, y)
	card.gui_input.connect(func(e):
		if _is_tap(e) and is_instance_valid(card):
			card.queue_free()
			open_panel("contracts"))
	AudioManager.play("pop", 0.05, -4.0)
	var t := card.create_tween()
	t.tween_property(card, "position:x", vp.x - 446, 0.35).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	t.tween_interval(3.6)
	t.tween_property(card, "position:x", vp.x + 20, 0.3).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_IN)
	t.tween_callback(card.queue_free)


func _update_resources(instant := false) -> void:
	if instant:
		_shown_cash = EconomyManager.cash
	rep_label.text = str(EconomyManager.reputation)
	influence_label.text = str(EconomyManager.influence)
	# heat shows the hottest unlocked region
	var hottest := 0
	for i in RegionManager.unlocked_count:
		if HeatManager.get_heat(i) > HeatManager.get_heat(hottest):
			hottest = i
	var h := HeatManager.get_heat(hottest)
	heat_label.text = "%d" % int(h)
	heat_label.add_theme_color_override("font_color", Fmt.heat_color(h))
	_update_badges()
	for child in _intro_hidden:
		if is_instance_valid(child): child.hide()


func _update_goal() -> void:
	var g := ProgressionManager.current_goal()
	goal_label.text = str(g.text)
	goal_bar.value = ProgressionManager.goal_progress()
	goal_reward.text = Fmt.money(float(g.get("reward", 0)))
	stage_label.text = tr("STAGE %d · Tap for advice") % (ProgressionManager.stage + 1)


func _process(delta: float) -> void:
	# rolling cash counter
	var target := EconomyManager.cash
	if absf(_shown_cash - target) > 0.5:
		_shown_cash = lerpf(_shown_cash, target, minf(1.0, delta * 6.0))
		if absf(_shown_cash - target) < 1.0:
			_shown_cash = target
	cash_label.text = Fmt.money(_shown_cash, _shown_cash >= 100000)
	_update_job_cards()
	jobs_scroll.visible = _intro_hidden.is_empty() and (current_sheet == null or not is_instance_valid(current_sheet))
	rotate_box.visible = jobs_scroll.visible and not dispatch_all_btn.visible and not _modal_open
	_badge_timer -= delta
	if _badge_timer <= 0:
		_badge_timer = 0.5
		goal_bar.value = ProgressionManager.goal_progress()
		dispatch_all_btn.visible = jobs_scroll.visible and ProgressionManager.auto_enabled("auto_assign") \
				and not ProgressionManager.auto_enabled("auto_dispatch") and ContractManager.ready_pairs().size() > 0
		_update_badges()
		_update_speed_button()
	stage_label.visible = not is_instance_valid(current_sheet)
	if not _modal_open and _modal_queue.size() > 0 and can_present():
		_show_next_modal()


func _input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		if _modal_open:
			_dismiss_top_modal()
			get_viewport().set_input_as_handled()
			return
		if current_sheet:
			close_panel()
			get_viewport().set_input_as_handled()


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_GO_BACK_REQUEST:
		if Ads.is_showing():
			return # a full-screen ad owns the Back button
		# 10-28 fleet Back check: the first-run story (name step included) is not the main screen — Back used to save and
		# quit in the middle of it; it stays on screen (its own buttons go on / skip)
		if is_instance_valid(onboarding):
			return
		# Android back: close the top-most thing, otherwise save & leave
		if _modal_open:
			_dismiss_top_modal()
			get_viewport().set_input_as_handled()
			return
		if current_sheet:
			close_panel()
		else:
			if GameManager.save_now(): get_tree().quit()


# --- feedback -----------------------------------------------------------------------------
## A kit toast card (icon tile + up to 2 lines) under the HUD; the colour picks the kind (green success, red danger,
## orange warning, otherwise info). The newest message replaces the current one.
func toast(text: String, color := Color.WHITE, icon_name := "", top_at := -1.0) -> void:
	var kind := UIKit.kind_of(color)
	# W66 fleet sweep 10-27 (pharmacy 3f1ed56ae0): while the guide's bubble is up it is the feedback — a toast landed on
	# the bubble ("cash" step) or the sheet row it spotlights; danger and the goal chip's own toast (top_at) still show
	if kind != "danger" and top_at < 0.0 and tutorial != null and tutorial.visible and tutorial.bubble != null and tutorial.bubble.visible:
		return
	# below the header even when the goal line wraps to two lines (a long language beside the ×1 / ×2 button)
	ui_toast.top = top_at if top_at >= 0.0 else maxf(_toast_top, _header_bottom() - ui_toast.global_position.y + 12.0)
	ui_toast.max_width = minf(640.0, goal_chip.size.x) if top_at >= 0.0 and goal_chip != null else 640.0
	# the goal chip's own toast is said during the guide on purpose: kit priority "guide" (clear of the bubble; kit 10-27)
	ui_toast.show_toast(tr(text), kind if kind in ["success", "danger", "warning"] else "info", UIKit.lucide(icon_name), "guide" if top_at >= 0.0 else "")


func float_text(world_pos: Vector3, text: String, color := Color.WHITE) -> void:
	var sp := UIManager.screen_pos(world_pos)
	if sp.x < -500:
		return
	var l: Label = _float_pool.pop_back() if _float_pool.size() > 0 else null
	if l == null:
		l = UIKit.label("", 40, color, true, HORIZONTAL_ALIGNMENT_CENTER)
		l.add_theme_constant_override("outline_size", 12)
		l.add_theme_color_override("font_outline_color", Color(0.05, 0.06, 0.12, 0.9))
		float_layer.add_child(l)
	l.text = text
	l.add_theme_color_override("font_color", color)
	l.modulate.a = 1.0
	l.visible = true
	l.size = Vector2(400, 60)
	l.position = sp - Vector2(200, 30)
	l.pivot_offset = l.size * 0.5
	l.scale = Vector2(0.4, 0.4)
	var t := l.create_tween()
	t.tween_property(l, "scale", Vector2.ONE, 0.3).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	t.parallel().tween_property(l, "position:y", l.position.y - 110, 1.4).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	t.tween_property(l, "modulate:a", 0.0, 0.3)
	t.tween_callback(func():
		l.visible = false
		_float_pool.append(l))


## Coins fly from the world into the cash counter.
func coin_burst(world_pos: Vector3, amount: float) -> void:
	coin_burst_screen(UIManager.screen_pos(world_pos), amount)


func coin_burst_screen(sp: Vector2, amount: float) -> void:
	if UIKit.reduced_motion(): return
	var vp := get_viewport_rect().size
	if sp.x < 0 or sp.y < 0 or sp.x > vp.x or sp.y > vp.y:
		sp = vp * Vector2(0.5, 0.55)
	var target := cash_chip.global_position + Vector2(40, cash_chip.size.y * 0.5)
	var n := clampi(int(log(maxf(amount, 10.0)) / log(10.0) * 3.0), 4, 12)
	for i in n:
		var c: TextureRect = _coin_pool.pop_back() if _coin_pool.size() > 0 else null
		if c == null:
			c = UIKit.icon("cash", 40)
			float_layer.add_child(c)
		c.visible = true
		c.position = sp - Vector2(20, 20)
		var burst := sp + Vector2(randf_range(-90, 90), randf_range(-100, 30))
		var t := c.create_tween()
		t.tween_property(c, "position", burst - Vector2(20, 20), 0.25).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
		t.tween_interval(0.05 * i)
		t.tween_property(c, "position", target - Vector2(20, 20), 0.45).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
		t.tween_callback(func():
			AudioManager.play("coin", 0.15, -4.0)
			UIKit.bounce(cash_chip, 1.08))
		t.tween_callback(func():
			c.visible = false
			_coin_pool.append(c))


# --- modals --------------------------------------------------------------------------------
func show_modal(title: String, body: String, icon_name := "", buttons: Array = [], accent := UIKit.GOLD) -> void:
	_modal_queue.append({"type": "basic", "title": title, "body": body, "icon": icon_name, "buttons": buttons, "accent": accent})


## One-time "graphics were set for this device" notice (owner rule 2026-10-14). Queued behind the
## title card / onboarding like every other modal; the flag is cleared only when it is really shown.
func _queue_quality_notice() -> void:
	var tier := QualityPolicy.pending_notice(GameManager.settings)
	if tier != DeviceTier.LOW and tier != DeviceTier.MEDIUM:
		return
	var body := tr("Graphics are set to Medium for smooth play on this device. You can raise them anytime in Settings → Graphics quality.") \
			if tier == DeviceTier.MEDIUM else tr("Graphics are set to Low for smooth play on this device. You can raise them anytime in Settings → Graphics quality.")
	_modal_queue.append({"type": "basic", "title": tr("Graphics quality"), "body": body, "icon": "gear", "accent": UIKit.GOLD,
			"buttons": [["OK", "BtnGold", Callable()], ["Settings", "BtnDark", func(): open_panel("settings")]],
			"on_show": func(): GameManager.consume_quality_notice()})


## Settings -> Send feedback.
func show_feedback() -> void:
	for m in _modal_queue:
		if m.type == "feedback":
			return
	_modal_queue.append({"type": "feedback"})


func _queue_event(job: Dictionary) -> void:
	for m in _modal_queue:
		if m.type == "event" and m.job.id == job.id:
			return
	_modal_queue.append({"type": "event", "job": job})


func _show_event_now(job: Dictionary) -> void:
	if _modal_open:
		return
	_modal_queue = _modal_queue.filter(func(m): return not (m.type == "event" and m.job.id == job.id))
	_modal_queue.push_front({"type": "event", "job": job})
	_show_next_modal()


func _show_next_modal() -> void:
	if not can_present() or _modal_queue.is_empty():
		return
	var m: Dictionary = _modal_queue.pop_front()
	if is_instance_valid(_contract_card):   # polish 10-27 (W66): the card slid in over the event's choice buttons
		_contract_card.queue_free()
		_contract_card = null
	if m.type == "event":
		var job: Dictionary = ContractManager.get_job(m.job.id)
		if job.is_empty() or not job.get("pending_event", false):
			return
		_modal_open = true
		var em := EventModal.new()
		modal_layer.add_child(em)
		em.closed.connect(func(): _modal_open = false)
		em.setup(job)
		# Event choices do not steal a camera the player is controlling.
	elif m.type == "feedback":
		_modal_open = true
		var fm := FeedbackModal.new()
		modal_layer.add_child(fm)
		fm.closed.connect(func(): _modal_open = false)
		fm.setup()
	else:
		_modal_open = true
		if m.has("on_show"):
			(m.on_show as Callable).call()
		var bm := BasicModal.new()
		modal_layer.add_child(bm)
		bm.closed.connect(func(): _modal_open = false)
		bm.setup(m.title, m.body, m.icon, m.buttons, m.accent)


func _on_event_resolved(job: Dictionary, summary: String, good: bool) -> void:
	if job.get("auto_handled", false):
		job.erase("auto_handled")
		summary = tr("Driver handled it: %s") % summary
	var v := VehicleManager.get_vehicle(job.vehicle_id)
	if world and world.actors.has(job.vehicle_id):
		var a: VehicleActor = world.actors[job.vehicle_id]
		float_text(a.global_position + Vector3.UP * 2.0, summary, UIKit.GREEN if good else UIKit.ORANGE)
	elif not v.is_empty():
		toast(summary, UIKit.GREEN if good else UIKit.ORANGE)


## Bottom of the stat row + goal row (screen y); 0 before the HUD is laid out.
func _header_bottom() -> float:
	if goal_chip == null or not goal_chip.is_inside_tree():
		return 0.0
	var row := goal_chip.get_parent() as Control
	return row.get_global_rect().end.y if row != null else goal_chip.get_global_rect().end.y


func _goal_toast_top() -> float:
	if goal_chip == null or not is_instance_valid(goal_chip) or not goal_chip.is_inside_tree():
		return -1.0
	return maxf(0.0, goal_chip.global_position.y - ui_toast.global_position.y - 4.0)


func _on_goal_completed(text: String, reward: float) -> void:
	AudioManager.play("cash")
	AudioManager.voice("goal", 30.0)
	# #99: the goal toast used to float over the map under the HUD. It now takes the goal chip's own row (the chip it
	# announces), so the play field stays clear; the next goal is back on the chip a few seconds later.
	toast(tr("Goal complete: %s  +%s") % [tr(text), Fmt.money(reward)], UIKit.GOLD, "trophy", _goal_toast_top())
	UIKit.bounce(goal_chip, 1.1)
	if reward > 0:
		coin_burst_screen(goal_chip.global_position + goal_chip.size * 0.5, reward)
	_update_goal()


func _on_stage_changed(stage: int) -> void:
	_update_goal()
	var st: Dictionary = Data.stages[stage]
	AudioManager.play("unlock")
	AudioManager.voice("stage", 5.0)
	show_modal(tr("Stage %d: %s") % [stage + 1, tr(st.name)], tr("Your operation is growing.\n%s") % _stage_hint(stage), "trophy",
			[["Keep Going", "BtnGold", Callable()]], UIKit.GOLD)


func _stage_hint(stage: int) -> String:
	if stage + 1 < Data.stages.size():
		return tr("Next: %s - %s") % [tr(Data.stages[stage + 1].name), tr(Data.stages[stage + 1].desc)]
	return "You run the biggest shadow logistics empire in the world!"


func _on_region_unlocked(index: int) -> void:
	_update_goal()
	var r := Data.region_at(index)
	show_modal(tr("%s Unlocked!") % tr(r.name), tr("%s\nPays x%.1f  ·  Base risk +%d%%") % [tr(r.tagline), float(r.econ), int(r.risk)],
			"map", [["Explore", "BtnPrimary", Callable()]], UIKit.GREEN)


func _on_offline(report: Dictionary) -> void:
	var body := tr("You were away for %s.\n") % Fmt.time(float(report.seconds))
	if int(report.deliveries) > 0:
		body += tr("%d deliveries completed.\n") % int(report.deliveries)
	if float(report.manager) > 0:
		body += tr("Your Regional Managers earned %s.") % Fmt.money(float(report.manager))
	body += "\n" + tr("Already added to your balance: %s") % Fmt.money(float(report.earned))
	AudioManager.voice("welcome_back", 5.0)
	var buttons: Array = [["Continue", "BtnGold", func():
			GameManager.acknowledge_return(str(report.id))
			Monetization.offline_break()]]
	# opt-in rewarded "double it" (an ad-free player takes it without the video; no interstitial is stacked on top of it)
	if Monetization.offline_double_offer(report):
		var bonus := Monetization.offline_bonus(report)
		Monetization.note_offer("offline_double", bonus)
		var label := tr("Double earnings +%s") % Fmt.money(bonus) if Monetization.video_free() else tr("Watch ad · double +%s") % Fmt.money(bonus)
		var double_cb := func():
			GameManager.acknowledge_return(str(report.id))
			Monetization.claim_placement("offline_double", {"id": str(report.id)})
		buttons.push_front([label, "BtnPrimary", double_cb, "stack"])
	show_modal("Welcome Back, Boss!", body, "cash", buttons)


## A one-off shop offer (Quick Start, Legacy Head Start): a modal with the store's price and "Not now".
func _show_offer(slug: String) -> void:
	var body: String = IapText.desc(slug) + "\n\n" + load("res://scripts/ui/panels/shop_sheet.gd").tag_for(slug, true)   # store policy: the tags in the offer too
	var fx := IapCatalog.effect(slug)
	if IapCatalog.is_consumable(slug) or fx == "starter":
		body += "\n" + tr("Right now: about %s") % Fmt.money(Monetization.pack_cash(fx))
	var price := Iap.price_text(slug)
	show_modal(IapText.title(slug), body, str(IapCatalog.def(slug).get("icon", "star")),
			[[price if price != "" else IapText.t("price_pending"), "BtnGold", func(): Iap.purchase(slug)],
			[IapText.t("not_now"), "BtnDark", Callable()]], UIKit.GOLD)


func _on_vehicle_tapped(id: String) -> void:
	var j := ContractManager.get_job_for_vehicle(id)
	if not j.is_empty() and j.get("pending_event", false):
		_show_event_now(j)
		return
	if j.is_empty():
		open_panel("fleet", {"vehicle": id})


# --- title card ------------------------------------------------------------------------------
func _show_title() -> void:
	if GameManager.has_meta("reopen_settings"):
		GameManager.remove_meta("reopen_settings")
		call_deferred("open_panel", "settings")
		return
	if GameManager.debug_args.has("notitle"):
		_maybe_onboard()
		return
	_hide_game_chrome()
	title_card = TitleCard.new()
	add_child(title_card)
	title_card.dismissed.connect(_maybe_onboard)


var onboarding: Onboarding
var _intro_hidden: Array[CanvasItem] = []

func _hide_game_chrome() -> void:
	if not _intro_hidden.is_empty(): return
	for child in get_children():
		if child is CanvasItem and child.visible and child != title_card:
			_intro_hidden.append(child)
			child.hide()

func _restore_game_chrome() -> void:
	for child in _intro_hidden:
		if is_instance_valid(child): child.show()
	_intro_hidden.clear()


func _maybe_onboard() -> void:
	if ProgressionManager.intro_done() or world == null:
		_restore_game_chrome()
		return
	_hide_game_chrome()
	onboarding = Onboarding.new()
	add_child(onboarding)
	onboarding.start(world)
	onboarding.finished.connect(func():
		_restore_game_chrome()
		_update_tabs(true)
		_update_goal())


func can_present() -> bool:
	return not GameManager._suspended and not is_instance_valid(onboarding) and not is_instance_valid(title_card) and not Monetization.busy and not Iap.busy


func _dismiss_top_modal() -> void:
	for node in modal_layer.get_children():
		if node is ModalBase:
			node.dismiss()
			return


# --- bottom house banner (Emir #112/#125: house only, games/tools/ads/README.md, CoddyBannerHost section) -----------------
const BannerHost := preload("res://scripts/monetization/ads/CoddyBannerHost.gd")
var banner_host: Node = null   ## kit CoddyBanner bridge, fed by the Ads autoload (config / telemetry / store / ad_free)
var banner_inset := 0.0        ## canvas px the banner covers at the bottom; the whole HUD (nav bar, job strip) ends above it


func _attach_banner() -> void:
	if banner_host != null:
		return
	banner_host = BannerHost.new()
	banner_host.name = "coddy_banner_host"
	banner_host.source = get_node_or_null("/root/Ads")
	banner_host.gate = banner_ok
	banner_host.banner_height_changed.connect(set_banner_inset)
	add_child(banner_host)


## Only on the plain map: no sheet (shop / upgrades / every tab panel), no modal or queued modal, no title card,
## onboarding or guided tutorial, no purchase in flight.
func banner_ok() -> bool:
	if current_sheet != null and is_instance_valid(current_sheet):
		return false
	if _modal_open or not can_present() or not is_visible_in_tree():
		return false
	return not TutorialManager.is_active()


## The HUD's bottom edge moves up by the banner. The nav bar's content is taller than NAV_H (it hangs a few px below
## the screen edge without a banner, which only clips its bottom padding); over a banner that overhang would cover the
## banner's top, so it is added to the lift.
func set_banner_inset(px: float) -> void:
	var lift := maxf(0.0, px - bottom_inset)   # the banner height already includes the bottom safe area it sits above (kit 10-26): max, not sum (nav_height keeps bottom_inset)
	var nav := get_node_or_null("NavBar") as Control
	if px > 0.0 and nav != null:
		lift += maxf(0.0, nav.get_combined_minimum_size().y - nav_height())
	banner_inset = lift
	offset_bottom = -lift
