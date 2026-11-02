extends Node
## Contextual tutorial: a pointer + one-line hint, never a wall of text.
## Every step is condition based, so players who explore on their own simply
## skip ahead.

signal step_changed

var step := 0
var finished := false
var _timer := 0.0
var _map_seen := 0.0

var steps: Array = []


func _ready() -> void:
	steps = [
		{"id": "contracts", "target": "nav_contracts", "text": "A client is calling! Open Contracts."},
		{"id": "accept", "target": "offer_accept_first", "fallback": "nav_contracts", "text": "Easy job, zero risk. Accept it!"},
		{"id": "dispatch", "target": "dispatch_go", "fallback": "nav_contracts", "text": "Pick your car and a route. Normal is fine. Hit Dispatch!"},
		{"id": "watch", "target": "", "text": "There she goes! Watch the delivery."},
		{"id": "cash", "target": "hud_cash", "text": "Cha-ching! Your first profit lands here."},
		{"id": "upgrade", "target": "upgrade_engine", "fallback": "nav_fleet", "text": "Invest in your ride. A better engine means faster trips."},
		{"id": "again", "target": "offer_accept_first", "fallback": "nav_contracts", "dispatch_target": "dispatch_go", "text": "Clients keep calling. Take another job!"},
		{"id": "garage", "target": "garage_upgrade_btn", "fallback": "nav_upgrades", "text": "Time to expand! A bigger garage means more vehicles."},
		{"id": "region", "target": "region_card_1", "fallback": "nav_map", "text": "Green Republic is next door. Unlock it to grow your network!",
				"alt_text": "Green Republic is next door. Earn reputation and cash, then unlock it here!"},
		{"id": "done", "target": "", "text": "You're a natural, boss. I'll leave the empire to you!"},
	]


func reset() -> void:
	step = 0
	finished = false
	_timer = 0.0
	_map_seen = 0.0
	step_changed.emit()


func current() -> Dictionary:
	if finished or step >= steps.size():
		return {}
	return steps[step]


func mentor_name() -> String:
	return str(Data.onboarding.get("mentor", "Dot"))


func allow_offer_spawning() -> bool:
	# never let the tutorial starve the economy for long
	return finished or step >= 3 or EconomyManager.stat("play_time") > 90.0


## Whether the current step should be shown right now.
func is_active() -> bool:
	var s := current()
	if s.is_empty() or not ProgressionManager.intro_done():
		return false
	match s.id:
		"upgrade":
			var v: Dictionary = VehicleManager.vehicles[0] if VehicleManager.vehicles.size() > 0 else {}
			return not v.is_empty() and VehicleManager.upgrade_check(v, "engine").ok
		"again":
			return ContractManager.jobs.is_empty()
		"garage":
			return ProgressionManager.garage_check().ok
		"region":
			return RegionManager.unlocked_count == 1
	return true


func _done() -> bool:
	var s := current()
	var deliveries := EconomyManager.stat("deliveries")
	var panel := UIManager.current_panel
	match s.get("id", ""):
		"contracts":
			return panel == "contracts" or panel == "dispatch" or ContractManager.jobs.size() > 0 or deliveries > 0
		"accept":
			return panel == "dispatch" or ContractManager.jobs.size() > 0 or deliveries > 0
		"dispatch":
			return ContractManager.jobs.size() > 0 or deliveries > 0
		"watch":
			return deliveries > 0
		"cash":
			return _timer > 3.0
		"upgrade":
			return EconomyManager.stat("vehicle_upgrades") > 0
		"again":
			return deliveries >= 2 or (ContractManager.jobs.size() > 0 and EconomyManager.stat("vehicle_upgrades") > 0)
		"garage":
			return ProgressionManager.garage_level >= 2
		"region":
			return _map_seen > 3.5 or RegionManager.unlocked_count > 1
		"done":
			return _timer > 5.0
	return true


func tick(delta: float) -> void:
	if finished:
		_tick_advisor(delta)
		return
	_timer += delta
	if UIManager.current_panel == "map":
		_map_seen += delta
	var guard := 0
	while not finished and _done() and guard < 12:
		guard += 1
		step += 1
		_timer = 0.0
		if step >= steps.size():
			finished = true
		step_changed.emit()


func skip() -> void:
	finished = true
	ProgressionManager.flags["intro_done"] = true
	ProgressionManager.flags["tabs_all"] = true
	step_changed.emit()


func to_dict() -> Dictionary:
	return {"step": step, "finished": finished}


func from_dict(d: Dictionary) -> void:
	step = int(d.get("step", 0))
	finished = bool(d.get("finished", false))
	if d.is_empty() and EconomyManager.stat("deliveries") > 0:
		finished = true
	step_changed.emit()


var _advisor_idle := 0.0
var _advisor_signature := ""
var _advisor_reminded := false

func next_action() -> Dictionary:
	if not EventManager.pending_jobs().is_empty():
		return {"goal":"A driver needs your decision.", "reason":"Open the active delivery to review the choices.", "panel":"contracts", "target":"nav_contracts", "blocker":"event", "completion":"event_resolved"}
	for v in VehicleManager.vehicles:
		if v.state == "idle" and str(v.driver).is_empty():
			return {"goal":"Assign a driver to your vehicle.", "reason":"A vehicle needs a driver before it can depart.", "panel":"fleet", "target":"nav_fleet", "blocker":"driver", "completion":"driver_assigned"}
	if ProgressionManager.garage_check().ok:
		return {"goal":"Expand your garage.", "reason":"More space lets you grow your delivery fleet.", "panel":"upgrades", "target":"garage_upgrade_btn", "blocker":"", "completion":"garage_upgraded"}
	var region := RegionManager.next_locked()
	if region >= 0 and RegionManager.unlock_check(region).ok:
		return {"goal":"Open your next region.", "reason":"New routes bring more delivery opportunities.", "panel":"map", "target":"nav_map", "blocker":"", "completion":"region_unlocked"}
	# An idle vehicle can leave now even while another driver is travelling.
	# This runs on demand when advice opens, never in the per-frame HUD refresh.
	for c in ContractManager.offers + ContractManager.queue:
		var v := ContractManager.best_vehicle(c)
		if not v.is_empty() and ContractManager.dispatch_check(c, v) == "":
			return {"goal":"Take your next delivery.", "reason":"Accept a suitable contract and choose an available vehicle.", "panel":"contracts", "target":"nav_contracts", "blocker":"", "completion":"job_started"}
	for v in VehicleManager.vehicles:
		if VehicleManager.upgrade_check(v, "engine").ok:
			return {"goal":"Upgrade a vehicle.", "reason":"A faster engine reduces delivery time.", "panel":"fleet", "target":"nav_fleet", "blocker":"", "completion":"vehicle_upgraded"}
	if not ContractManager.jobs.is_empty():
		return {"goal":"Your drivers are on the road.", "reason":"Let this delivery finish, then invest the earnings.", "panel":"contracts", "target":"nav_contracts", "blocker":"wait", "completion":"job_returned"}
	return {"goal":"Take your next delivery.", "reason":"Accept a suitable contract and choose an available vehicle.", "panel":"contracts", "target":"nav_contracts", "blocker":"cash", "completion":"job_started"}

func show_advice() -> void:
	var action := next_action()
	_advisor_idle = 0
	_advisor_reminded = true
	UIManager.modal("Dot's advice", tr(str(action.goal)) + "\n\n" + tr(str(action.reason)), "mentor",
		[["Not now", "BtnDark", Callable()], ["Show me", "BtnGold", func():
			UIManager.open_panel(str(action.panel))
			if action.blocker == "event" and UIManager.hud:
				var jobs := EventManager.pending_jobs()
				if not jobs.is_empty(): UIManager.hud._queue_event(jobs[0])]])

func _tick_advisor(delta: float) -> void:
	var signature := "%s:%s:%s:%s:%s" % [EconomyManager.stat("deliveries"), EconomyManager.stat("vehicle_upgrades"), ProgressionManager.garage_level, RegionManager.unlocked_count, ContractManager.jobs.size()]
	if signature != _advisor_signature:
		_advisor_signature = signature
		_advisor_idle = 0
		_advisor_reminded = false
	if not GameManager.settings.get("advisor", true) or _advisor_reminded: return
	if not UIManager.current_panel.is_empty() or not ContractManager.jobs.is_empty(): return
	if UIManager.hud == null or not UIManager.hud.can_present() or UIManager.hud._modal_open: return
	_advisor_idle += delta
	if _advisor_idle >= 90:
		_advisor_reminded = true
		UIManager.toast("Need a hand? Open Dot's advice in Settings.", UIKit.BLUE, "mentor")
