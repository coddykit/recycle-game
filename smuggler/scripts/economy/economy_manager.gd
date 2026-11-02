extends Node
## Cash, reputation and lifetime business statistics.

signal cash_changed(cash: float, delta: float)
signal reputation_changed(rep: int, delta: int)
signal stats_changed
signal influence_changed(value: int, delta: int)

var cash: float = 0.0
var reputation: int = 0
var influence: int = 0
var stats := {}
## rolling income samples: [time, amount]
var _income_log: Array = []
var _clock := 0.0
var _ref_t := 0.0
## Reference income for paid cash packs: the best recent income per minute, fading with a ~2 h half-life. Reset by prestige.
const REF_INTERVAL := 5.0
const REF_DECAY := 0.9995
const REF_FLOOR := 120.0 ## per minute: a brand-new empire still gets a usable pack


func reset() -> void:
	cash = float(Data.config.get("start_cash", 250))
	reputation = 0
	influence = 0
	stats = {
		"earned": 0.0, "spent": 0.0, "deliveries": 0, "failed": 0, "cross_border": 0,
		"vehicle_upgrades": 0, "business_upgrades": 0, "events": 0, "best_payout": 0.0,
		"play_time": 0.0,
	}
	_income_log.clear()
	cash_changed.emit(cash, 0)
	reputation_changed.emit(reputation, 0)


func tick(delta: float) -> void:
	_clock += delta
	stats["play_time"] = float(stats.get("play_time", 0.0)) + delta
	while _income_log.size() > 0 and _clock - float(_income_log[0][0]) > 180.0:
		_income_log.pop_front()
	_ref_t += delta
	if _ref_t >= REF_INTERVAL:
		_ref_t = 0.0
		var steady := not _income_log.is_empty() and _clock - float(_income_log[0][0]) >= 90.0 # a short burst is not an income
		stats["ipm_ref"] = maxf(float(stats.get("ipm_ref", 0.0)) * REF_DECAY, income_per_minute() if steady else 0.0)


func can_afford(amount: float) -> bool:
	return cash >= amount


func spend(amount: float, _reason := "") -> bool:
	if amount <= 0:
		return true
	if not can_afford(amount):
		return false
	cash -= amount
	stats["spent"] = float(stats.get("spent", 0.0)) + amount
	cash_changed.emit(cash, -amount)
	return true


## Charges a cost that may exceed the balance (e.g. mandatory fees);
## cash never drops below zero, the business just eats the difference.
func charge(amount: float, reason := "") -> float:
	var paid: float = minf(amount, cash)
	if paid > 0:
		spend(paid, reason)
	return paid


func earn(amount: float, _reason := "", count_income := true) -> void:
	if amount <= 0:
		return
	cash += amount
	if count_income:
		stats["earned"] = float(stats.get("earned", 0.0)) + amount
		_income_log.append([_clock, amount])
	cash_changed.emit(cash, amount)
	stats_changed.emit()


func add_reputation(amount: int) -> void:
	if amount == 0:
		return
	var old := reputation
	reputation = maxi(0, reputation + amount)
	reputation_changed.emit(reputation, reputation - old)


func add_influence(amount: int) -> void:
	if amount == 0:
		return
	influence = maxi(0, influence + amount)
	if amount > 0:
		stats["influence_earned"] = float(stats.get("influence_earned", 0.0)) + amount
	influence_changed.emit(influence, amount)


func add_stat(key: String, amount: float = 1.0) -> void:
	stats[key] = float(stats.get(key, 0.0)) + amount
	stats_changed.emit()


func stat(key: String) -> float:
	return float(stats.get(key, 0.0))


## Estimated income per minute from the last few minutes of play.
func income_per_minute() -> float:
	if _income_log.is_empty():
		return 0.0
	var total := 0.0
	for e in _income_log:
		total += float(e[1])
	var span: float = clampf(_clock - float(_income_log[0][0]), 30.0, 180.0)
	return total / span * 60.0


## What a paid cash pack is measured against (money per minute): never below REF_FLOOR, never lower than what the
## player earns right now, and not dropping to zero when they just sat idle for three minutes.
func ref_income() -> float:
	return maxf(REF_FLOOR, maxf(float(stats.get("ipm_ref", 0.0)), 0.0))


func to_dict() -> Dictionary:
	return {"cash": cash, "reputation": reputation, "influence": influence, "stats": stats, "ipm": income_per_minute()}


func from_dict(d: Dictionary) -> void:
	reset()
	cash = maxf(0.0, float(d.get("cash", cash)))
	reputation = int(d.get("reputation", 0))
	influence = int(d.get("influence", 0))
	var s: Dictionary = d.get("stats", {})
	for k in s:
		stats[k] = s[k]
	var ipm := float(d.get("ipm", 0.0))
	if ipm > 0:
		_income_log.append([_clock - 60.0, ipm])
	cash_changed.emit(cash, 0)
	reputation_changed.emit(reputation, 0)
