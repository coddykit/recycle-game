extends Node
## Per-region heat (0-100). Risky work raises it, time cools it down.
## High heat = more risk & events, but better pay. Encourages expansion.

signal heat_changed(region: int, value: float)

## Heat cools proportionally (time constant ~90s) plus a small constant drift,
## so a single busy vehicle keeps a region "Warm" while concentrating the whole
## fleet in one region pushes it to "Hot" or "Critical".
const DECAY_RATE := 0.011
const DRIFT := 0.04

var heat: Array = []


func reset() -> void:
	heat = []
	for i in Data.regions.size():
		heat.append(0.0)


func _ready() -> void:
	reset()


func get_heat(region: int) -> float:
	if region < 0 or region >= heat.size():
		return 0.0
	return float(heat[region])


func add_heat(region: int, amount: float) -> void:
	if region < 0 or region >= heat.size() or amount == 0:
		return
	heat[region] = clampf(float(heat[region]) + amount, 0.0, 100.0)
	heat_changed.emit(region, heat[region])


func decay_rate() -> float:
	return DECAY_RATE * (1.0 + 0.2 * ProgressionManager.business_level("operations_room")) \
			* (1.0 + 0.15 * ProgressionManager.conn("night_owls"))


func tick(delta: float) -> void:
	var k := decay_rate()
	var keep := exp(-k * delta)
	for i in heat.size():
		var h: float = heat[i]
		if h > 0.0:
			var nh: float = maxf(0.0, h * keep - DRIFT * delta)
			heat[i] = nh
			if int(nh) != int(h):
				heat_changed.emit(i, nh)


func risk_bonus(region: int) -> float:
	return get_heat(region) * 0.35


func reward_bonus(region: int) -> float:
	return get_heat(region) * 0.004


func is_critical(region: int) -> bool:
	return get_heat(region) > 75.0


func max_heat() -> float:
	var m := 0.0
	for i in RegionManager.unlocked_count:
		m = maxf(m, get_heat(i))
	return m


func to_dict() -> Dictionary:
	return {"heat": heat}


func from_dict(d: Dictionary) -> void:
	reset()
	var h: Array = d.get("heat", [])
	for i in mini(h.size(), heat.size()):
		heat[i] = clampf(float(h[i]), 0.0, 100.0)
