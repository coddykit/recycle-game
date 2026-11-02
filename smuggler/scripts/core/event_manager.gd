extends Node
## Random journey events with abstract, game-like choices.

signal event_triggered(job: Dictionary)
signal event_resolved(job: Dictionary, summary: String, good: bool)

## Neutral "life happens" chance on top of contract risk.
const BASE_CHANCE := 0.05


## Decides at dispatch time whether (and when) an event will interrupt the job.
func roll_for_job(job: Dictionary, forced_id := "") -> void:
	job["event_at"] = -1.0
	job["event_id"] = ""
	if bool(job.contract.get("tutorial", false)):
		return
	var chance := (float(job.risk) / 100.0 + BASE_CHANCE) * (1.0 - 0.15 * ProgressionManager.conn("inside_contact"))
	if forced_id == "" and randf() > chance:
		return
	var ev_id := forced_id if forced_id != "" else pick_event(job)
	if ev_id == "":
		return
	job.event_id = ev_id
	job.event_at = float(job.total) * randf_range(0.3, 0.65)


func pick_event(job: Dictionary) -> String:
	var v := VehicleManager.get_vehicle(job.vehicle_id)
	var rel := VehicleManager.reliability(v) if not v.is_empty() else 60.0
	var kind: String = Data.category(job.contract.category).get("kind", "cargo")
	var heat := HeatManager.get_heat(RegionManager.region_of_node(job.contract.dest))
	var fragile: bool = job.contract.get("modifier", "") == "fragile"
	var total := 0.0
	var weights := []
	for e in Data.events:
		var w := float(e.weight)
		if e.kind != "any" and e.kind != kind:
			w = 0.0
		if e.get("reliability", false):
			w *= clampf(1.7 - rel / 100.0, 0.4, 1.7)
		if e.get("heat_scaled", false):
			w *= 1.0 + heat / 40.0
		if e.get("fragile", false) and fragile:
			w *= 2.5
		if e.id == "lost_cargo" and not v.is_empty():
			w *= pow(0.85, VehicleManager.upgrade_level(v, "suspension"))
		weights.append(w)
		total += w
	var roll := randf() * total
	for i in Data.events.size():
		roll -= float(weights[i])
		if roll <= 0 and float(weights[i]) > 0:
			return Data.events[i].id
	return ""


## Fills in concrete numbers for each choice of the job's event.
func build_choices(job: Dictionary) -> Array:
	var e := Data.event_def(job.event_id)
	var out := []
	var reward := float(job.reward)
	var v := VehicleManager.get_vehicle(job.vehicle_id)
	var dr := VehicleManager.get_driver(v.get("driver", "")) if not v.is_empty() else {}
	for c in e.get("choices", []):
		var ch: Dictionary = c.duplicate(true)
		ch["cost"] = 0.0
		if c.has("cost_frac"):
			ch.cost = float(Fmt.nice(maxf(float(c.get("cost_min", 0)), reward * float(c.cost_frac))))
		if c.has("chance"):
			var p := float(c.chance)
			if c.get("stress", false) and not dr.is_empty():
				p += VehicleManager.driver_stat(dr, "stress") * 0.003
			if c.get("speed", false) and not v.is_empty():
				p += (VehicleManager.speed_mult(v) - 1.0) * 0.4
			ch.chance = clampf(p, 0.05, 0.95)
		out.append(ch)
	return out


func trigger(job: Dictionary, auto := false) -> void:
	job["pending_event"] = true
	job["choices"] = build_choices(job)
	EconomyManager.add_stat("events")
	if auto or ProgressionManager.auto_enabled("regional_manager") or ProgressionManager.crisis_manager_on():
		resolve(job, auto_choice(job), true)
		return
	event_triggered.emit(job)


func auto_choice(job: Dictionary) -> int:
	var choices: Array = job.get("choices", [])
	var best := 0
	var best_score := -INF
	var reward := float(job.reward)
	for i in choices.size():
		var c: Dictionary = choices[i]
		var score := 0.0
		score -= float(c.cost) / maxf(1.0, reward) * 100.0
		if float(c.cost) > EconomyManager.cash:
			score -= 1000.0
		score -= float(c.get("delay", 0)) * 0.6
		score -= float(c.get("risk", 0)) * 1.2
		score -= float(c.get("damage", 0)) * 0.5
		score -= (1.0 - float(c.get("reward_mult", 1.0))) * 100.0
		if c.has("chance"):
			var f: Dictionary = c.get("fail", {})
			var penalty := 100.0 if f.get("fail_contract", false) else 15.0
			score -= (1.0 - float(c.chance)) * penalty
		if score > best_score:
			best_score = score
			best = i
	return best


func resolve(job: Dictionary, index: int, auto := false) -> void:
	if not job.get("pending_event", false):
		return
	var choices: Array = job.get("choices", [])
	if choices.is_empty():
		job.pending_event = false
		return
	index = clampi(index, 0, choices.size() - 1)
	var c: Dictionary = choices[index]
	if float(c.cost) > 0 and not EconomyManager.can_afford(float(c.cost)):
		if auto:
			index = 0
			for i in choices.size():
				if float(choices[i].cost) <= 0:
					index = i
					break
			c = choices[index]
		else:
			return
	var summary := ""
	var good := true
	if float(c.cost) > 0:
		EconomyManager.spend(float(c.cost), "event")
	var effects: Dictionary = c
	if c.has("chance"):
		if randf() <= float(c.chance):
			summary = tr("Success!")
			effects = {}
		else:
			effects = c.get("fail", {})
			summary = tr("That didn't go well...")
			good = false
	_apply(job, effects)
	if effects.get("fail_contract", false):
		summary = tr("Contract lost!")
		good = false
	elif summary == "":
		summary = _describe(effects)
	job.pending_event = false
	job.event_id = ""
	job.event_at = -1.0
	job.erase("choices")
	event_resolved.emit(job, summary, good)
	if effects.get("fail_contract", false):
		ContractManager.fail_job(job, "Stopped at inspection")


func _apply(job: Dictionary, fx: Dictionary) -> void:
	var delay := float(fx.get("delay", 0))
	if delay != 0:
		ContractManager.add_delay(job, delay)
	job.fail_chance = float(job.get("fail_chance", 0)) + float(fx.get("risk", 0)) / 100.0
	job.damage = float(job.get("damage", 0)) + float(fx.get("damage", 0))
	job.reward_mult = float(job.get("reward_mult", 1.0)) * float(fx.get("reward_mult", 1.0))
	if fx.has("heat"):
		HeatManager.add_heat(RegionManager.region_of_node(job.contract.dest), float(fx.heat))
	if fx.has("rep"):
		EconomyManager.add_reputation(int(fx.rep))


func _describe(fx: Dictionary) -> String:
	var parts := []
	if fx.has("delay") and float(fx.delay) > 0:
		parts.append("+%ds" % int(fx.delay))
	if fx.has("risk"):
		parts.append(tr("Risk +%d%%") % int(fx.risk))
	if fx.has("damage"):
		parts.append(tr("Vehicle damaged"))
	if fx.has("reward_mult"):
		parts.append(tr("Pay %d%%") % int(float(fx.reward_mult) * 100))
	if parts.is_empty():
		return tr("Back on the road!")
	return ", ".join(parts)


func pending_jobs() -> Array:
	return ContractManager.jobs.filter(func(j): return j.get("pending_event", false))
