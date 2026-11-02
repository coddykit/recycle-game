extends Node
## Garage level, business upgrades, automation, empire stages and goals.

signal garage_upgraded(level: int)
signal business_upgraded(id: String, level: int)
signal automation_changed
signal stage_changed(stage: int)
signal goal_completed(text: String, reward: float)
signal goal_changed
signal connection_bought(id: String, level: int)

const AUTO_KEYS := ["auto_accept", "auto_assign", "auto_dispatch", "regional_manager"]

var garage_level := 1
var business := {}
var auto_toggles := {}
var stage := 0
var goal_index := 0
var flags := {}
var connections := {}


func reset() -> void:
	garage_level = 1
	business = {}
	auto_toggles = {}
	for k in AUTO_KEYS:
		auto_toggles[k] = true
	stage = 0
	goal_index = 0
	flags = {}
	connections = {}


func _ready() -> void:
	reset()
	call_deferred("_connect_progress_signals")


func _connect_progress_signals() -> void:
	VehicleManager.vehicle_added.connect(func(_v): check_progress())
	VehicleManager.drivers_changed.connect(check_progress)
	RegionManager.region_unlocked.connect(func(_i): check_progress())
	RegionManager.depot_built.connect(func(_i): check_progress())


# --- capacities -------------------------------------------------------------
func garage_def(level := -1) -> Dictionary:
	return Data.garage_level(garage_level if level < 0 else level)


func vehicle_slots() -> int:
	return int(garage_def().vehicles) + business_level("fleet_capacity")


func driver_slots() -> int:
	return vehicle_slots() + 1 + business_level("driver_lounge")


func offer_slots() -> int:
	return int(garage_def().offers) + business_level("contract_office") + conn("fixer")


func offer_interval() -> float:
	return float(Data.config.get("offer_interval", 7.0)) * pow(0.85, business_level("contract_office"))


func reward_mult() -> float:
	return (1.0 + 0.06 * business_level("warehouse")) * (1.0 + 0.08 * business_level("advanced_logistics")) \
			* (1.0 + 0.05 * conn("big_clients")) * legacy_mult()


## Permanent income bonus from prestige (Legacy stars). See PrestigeManager.
func legacy_mult() -> float:
	return 1.0 + 0.1 * int(flags.get("legacy", 0))


func travel_mult() -> float:
	return (1.0 - 0.05 * business_level("dispatch_center")) / (1.0 + 0.04 * business_level("advanced_logistics"))


func risk_reduction() -> float:
	return 3.0 * business_level("operations_room")


func op_cost_mult() -> float:
	return 1.0 - 0.05 * business_level("maintenance_bay")


func repair_mult() -> float:
	return pow(0.85, business_level("maintenance_bay")) * (1.0 - 0.25 * conn("mechanic"))


# --- connections (bought with Influence) -------------------------------------------
func conn(id: String) -> int:
	return int(connections.get(id, 0))


func connection_cost(id: String) -> int:
	var d := Data.connection(id)
	var costs: Array = d.get("costs", [99])
	return int(costs[mini(conn(id), costs.size() - 1)])


func connection_check(id: String) -> Dictionary:
	var d := Data.connection(id)
	if d.is_empty():
		return {"ok": false, "reason": "?"}
	if conn(id) >= int(d.max):
		return {"ok": false, "reason": "Maxed", "maxed": true}
	if EconomyManager.influence < connection_cost(id):
		return {"ok": false, "reason": tr("Need %d influence") % connection_cost(id)}
	return {"ok": true, "reason": ""}


func buy_connection(id: String) -> bool:
	if not connection_check(id).ok:
		return false
	EconomyManager.add_influence(-connection_cost(id))
	connections[id] = conn(id) + 1
	connection_bought.emit(id, connections[id])
	return true


func max_garage() -> int:
	return Data.garage_levels.size()


# --- garage -----------------------------------------------------------------
func garage_check() -> Dictionary:
	if garage_level >= max_garage():
		return {"ok": false, "reason": "Max level"}
	var nxt := garage_def(garage_level + 1)
	if EconomyManager.reputation < int(nxt.rep):
		return {"ok": false, "reason": tr("Need %d reputation") % int(nxt.rep)}
	if not EconomyManager.can_afford(float(nxt.cost)):
		return {"ok": false, "reason": tr("Need %s") % Fmt.money(float(nxt.cost))}
	return {"ok": true, "reason": ""}


func upgrade_garage() -> bool:
	if not garage_check().ok:
		return false
	EconomyManager.spend(float(garage_def(garage_level + 1).cost), "garage")
	garage_level += 1
	garage_upgraded.emit(garage_level)
	check_progress()
	return true


# --- business upgrades --------------------------------------------------------
func business_level(id: String) -> int:
	return int(business.get(id, 0))


func business_cost(id: String) -> float:
	var d := Data.business(id)
	var lvl := business_level(id)
	if d.has("costs"):
		var costs: Array = d.costs
		return float(costs[mini(lvl, costs.size() - 1)])
	return Fmt.nice(float(d.base) * pow(float(d.growth), lvl))


func business_required_garage(id: String) -> int:
	var d := Data.business(id)
	if d.has("garages"):
		var g: Array = d.garages
		return int(g[mini(business_level(id), g.size() - 1)])
	return int(d.get("garage", 1))


func business_check(id: String) -> Dictionary:
	var d := Data.business(id)
	if d.is_empty():
		return {"ok": false, "reason": "?"}
	if business_level(id) >= int(d.max):
		return {"ok": false, "reason": "Maxed", "maxed": true}
	var g := business_required_garage(id)
	if garage_level < g:
		return {"ok": false, "reason": tr("Needs %s") % tr(str(garage_def(g).name)), "locked": true}
	if not EconomyManager.can_afford(business_cost(id)):
		return {"ok": false, "reason": tr("Need %s") % Fmt.money(business_cost(id))}
	return {"ok": true, "reason": ""}


func buy_business(id: String) -> bool:
	if not business_check(id).ok:
		return false
	EconomyManager.spend(business_cost(id), "business")
	business[id] = business_level(id) + 1
	EconomyManager.add_stat("business_upgrades")
	business_upgraded.emit(id, business[id])
	if id == "automated_dispatch":
		automation_changed.emit()
	check_progress()
	return true


# --- automation -------------------------------------------------------------
func automation_level() -> int:
	return business_level("automated_dispatch")


func auto_unlocked(key: String) -> bool:
	return automation_level() > AUTO_KEYS.find(key)


func auto_enabled(key: String) -> bool:
	return auto_unlocked(key) and bool(auto_toggles.get(key, true))


func set_auto(key: String, on: bool) -> void:
	auto_toggles[key] = on
	automation_changed.emit()


## The fifth automation line (paid, Crisis Manager): road events are handled and daily jobs claimed for the player.
## Not part of AUTO_KEYS (those unlock with the Automated Dispatch business upgrade); the player can switch it off.
func crisis_manager_owned() -> bool:
	return Iap.has("auto_plus")


func crisis_manager_on() -> bool:
	return crisis_manager_owned() and bool(auto_toggles.get("crisis", true))


# --- stages & goals -----------------------------------------------------------
func metric(type: String) -> float:
	match type:
		"deliveries":
			return EconomyManager.stat("deliveries")
		"vehicle_upgrades":
			return EconomyManager.stat("vehicle_upgrades")
		"business_upgrades":
			return EconomyManager.stat("business_upgrades")
		"cross_border":
			return EconomyManager.stat("cross_border")
		"earned":
			return EconomyManager.stat("earned")
		"garage":
			return garage_level
		"vehicles":
			return VehicleManager.vehicles.size()
		"regions":
			return RegionManager.unlocked_count
		"automation":
			return automation_level()
		"depots":
			return RegionManager.depots.size()
		"skilled_drivers":
			var n := 0
			for d in VehicleManager.drivers:
				if d.rarity != "common":
					n += 1
			return n
		"reputation":
			return EconomyManager.reputation
	return 0.0


func stage_progress(index: int) -> float:
	if index >= Data.stages.size():
		return 1.0
	var req: Array = Data.stages[index].req
	if req.is_empty():
		return 1.0
	var total := 0.0
	for r in req:
		total += clampf(metric(r[0]) / float(r[1]), 0.0, 1.0)
	return total / req.size()


func current_goal() -> Dictionary:
	if goal_index < Data.goals.size():
		return Data.goals[goal_index]
	# endless generated goals
	var n := goal_index - Data.goals.size()
	if n % 2 == 0:
		var target := 100 * int(pow(1.6, n / 2 + 1))
		return {"text": tr("Complete %s deliveries") % Fmt.commas(target), "type": "deliveries", "value": target,
				"reward": Fmt.nice(EconomyManager.income_per_minute() * 3.0 + 5000)}
	var earn: float = Fmt.nice(1_000_000.0 * pow(2.2, n / 2))
	return {"text": tr("Earn %s in total") % Fmt.money(earn), "type": "earned", "value": earn,
			"reward": Fmt.nice(earn * 0.03)}


func goal_progress() -> float:
	var g := current_goal()
	return clampf(metric(g.type) / maxf(1.0, float(g.value)), 0.0, 1.0)


func check_progress() -> void:
	# goals (one at a time so each gets its moment)
	var g := current_goal()
	if metric(g.type) >= float(g.value):
		goal_index += 1
		var reward := float(g.get("reward", 0))
		EconomyManager.earn(reward, "goal", false)
		EconomyManager.add_influence(2)
		goal_completed.emit(str(g.text), reward)
		goal_changed.emit()
	# stages
	var s := stage
	while s + 1 < Data.stages.size() and stage_progress(s + 1) >= 1.0:
		s += 1
	if s != stage:
		EconomyManager.add_influence(5 * (s - stage))
		stage = s
		stage_changed.emit(stage)


# --- onboarding: company identity & progressive tabs ----------------------------------
func intro_done() -> bool:
	return bool(flags.get("intro_done", false))


func company_name() -> String:
	return str(flags.get("company", "Borderline Logistics"))


func company_color() -> Color:
	return Data.color(str(flags.get("company_color", "E05A47")))


func tab_unlocked(tab: String) -> bool:
	if bool(flags.get("tabs_all", false)):
		return true
	var t: Dictionary = Data.onboarding.get("tabs", {}).get(tab, {})
	if t.is_empty():
		return true
	if EconomyManager.stat("deliveries") >= float(t.get("deliveries", 0)):
		return true
	match tab:
		"upgrades":
			return garage_level >= 2
		"map":
			return garage_level >= 2 or RegionManager.unlocked_count > 1
		"business":
			return RegionManager.unlocked_count > 1 or automation_level() > 0
	return false


func tab_hint(tab: String) -> String:
	return str(Data.onboarding.get("tabs", {}).get(tab, {}).get("hint", ""))


# --- prestige: "Sell the Empire" ------------------------------------------------------
const PRESTIGE_STAGE := 8


func legacy() -> int:
	return int(flags.get("legacy", 0))


func prestige_available() -> bool:
	return stage >= PRESTIGE_STAGE


func run_earned() -> float:
	return EconomyManager.stat("earned") - float(flags.get("earned_at_prestige", 0.0))


## Legacy stars for selling now: grows with the square root of this run's income.
func prestige_stars() -> int:
	return maxi(1, int(floor(sqrt(maxf(0.0, run_earned()) / 1_500_000.0))))


func legacy_start_cash(stars: int) -> float:
	return 250.0 + 1500.0 * pow(float(stars), 1.25)


## Resets the run but keeps Legacy stars, Connections, Influence and lifetime stats.
func apply_prestige() -> int:
	var stars := prestige_stars()
	var keep_flags := flags.duplicate(true)
	keep_flags["legacy"] = legacy() + stars
	keep_flags["prestiges"] = int(flags.get("prestiges", 0)) + 1
	keep_flags["earned_at_prestige"] = EconomyManager.stat("earned")
	keep_flags["last_run_ref"] = EconomyManager.ref_income() # what the Legacy Head Start pack is measured against
	var keep_conn := connections.duplicate(true)
	var keep_goal := goal_index
	var keep_influence := EconomyManager.influence
	var keep_stats := EconomyManager.stats.duplicate(true)
	GameManager.new_game(true)
	flags = keep_flags
	connections = keep_conn
	goal_index = keep_goal
	EconomyManager.stats = keep_stats
	EconomyManager.stats["ipm_ref"] = 0.0 # the new run's packs are measured against the new run
	EconomyManager.influence = keep_influence + 5 * stars
	EconomyManager.cash = legacy_start_cash(legacy())
	EconomyManager.reputation = 6 * legacy()
	# veteran head start
	if legacy() >= 1:
		VehicleManager.add_vehicle("small_van", true)
		garage_level = 2
	if legacy() >= 6:
		garage_level = 3
		VehicleManager.add_vehicle("cargo_van", true)
	TutorialManager.finished = true
	EconomyManager.cash_changed.emit(EconomyManager.cash, 0)
	GameManager.save_now()
	return stars


## Re-derives the empire stage without fanfare (used after loading).
func recompute_stage() -> void:
	var s := stage
	while s + 1 < Data.stages.size() and stage_progress(s + 1) >= 1.0:
		s += 1
	stage = s


func to_dict() -> Dictionary:
	return {"garage": garage_level, "business": business, "auto": auto_toggles, "stage": stage,
			"goal": goal_index, "flags": flags, "connections": connections}


func from_dict(d: Dictionary) -> void:
	reset()
	garage_level = clampi(int(d.get("garage", 1)), 1, max_garage())
	var b: Dictionary = d.get("business", {})
	for k in b:
		var def := Data.business(k)
		if not def.is_empty():
			business[k] = clampi(int(b[k]), 0, int(def.max))
	var a: Dictionary = d.get("auto", {})
	for k in a:
		auto_toggles[k] = bool(a[k])
	stage = clampi(int(d.get("stage", 0)), 0, Data.stages.size() - 1)
	goal_index = maxi(0, int(d.get("goal", 0)))
	flags = d.get("flags", {})
	if not flags.has("intro_done") and float(d.get("goal", 0)) > 0:
		flags["intro_done"] = true
	var cn: Dictionary = d.get("connections", {})
	for k in cn:
		if not Data.connection(k).is_empty():
			connections[k] = clampi(int(cn[k]), 0, int(Data.connection(k).max))
