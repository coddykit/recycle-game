extends Node
## UTC plans, frozen quotes and wallet+claim committed together in the game save.
signal changed
var state: Dictionary = {}
var _timer := 0.0

func reset() -> void:
	state = {}

func utc_day(now := -1.0) -> int:
	return int(floor((Time.get_unix_time_from_system() if now < 0 else now) / 86400.0))

func ensure_day(day := -1) -> bool:
	if day < 0: day = utc_day()
	# A backward clock never reopens a previous day's rewards.
	if day <= int(state.get("day", -1)): return true
	var old := state.duplicate(true)
	var pool := [
		{"id":"deliver", "metric":"deliveries", "target":3.0, "reward":150.0, "text":"Complete %d deliveries"},
		{"id":"earn", "metric":"earned", "target":1200.0, "reward":200.0, "text":"Earn %s from deliveries"},
		{"id":"routes", "metric":"deliveries", "target":8.0, "reward":250.0, "text":"Complete %d deliveries"},
	]
	if not VehicleManager.vehicles.is_empty() and VehicleManager.upgrade_check(VehicleManager.vehicles[0], "engine").ok:
		pool[2] = {"id":"upgrade", "metric":"vehicle_upgrades", "target":1.0, "reward":250.0, "text":"Upgrade a vehicle %d time(s)"}
	var rng := RandomNumberGenerator.new()
	rng.seed = day
	var tasks: Array = []
	while not pool.is_empty():
		var task: Dictionary = pool.pop_at(rng.randi_range(0, pool.size() - 1))
		task["base"] = EconomyManager.stat(task.metric)
		task["claimed"] = false
		tasks.append(task)
	state = {"day": day, "tasks": tasks}
	if not GameManager.save_now():
		state = old
		return false
	changed.emit()
	return true

func progress(task: Dictionary) -> float:
	return clampf(EconomyManager.stat(task.metric) - float(task.base), 0, float(task.target))

func description(task: Dictionary) -> String:
	return tr(str(task.text)) % (Fmt.money(float(task.target)) if task.metric == "earned" else int(task.target))

func claim(id: String, day: int) -> bool:
	if day != utc_day() or day != int(state.get("day", -1)) or SaveManager.write_protected: return false
	for task in state.get("tasks", []):
		if task.id != id: continue
		if task.claimed or progress(task) < float(task.target): return false
		var before := SaveManager.snapshot()
		task.claimed = true
		EconomyManager.earn(float(task.reward), "daily", false)
		if not GameManager.save_now():
			SaveManager.apply(before)
			changed.emit()
			return false
		UIManager.feedback("claim")
		changed.emit()
		return true
	return false

## What the rewarded double of a claimed task adds: its reward once more, at most `limit` (the day's reward budget).
func double_amount(id: String, limit: float = INF) -> float:
	for task in state.get("tasks", []):
		if task.id == id:
			return minf(float(task.reward), limit) if task.claimed and not task.get("doubled", false) else 0.0
	return 0.0

## Rewarded "double it": a claimed task pays its reward once more (task.doubled), at most `limit`. Returns the extra cash, 0 = nothing.
func double_claim(id: String, day: int, limit: float = INF) -> float:
	if day != utc_day() or day != int(state.get("day", -1)) or SaveManager.write_protected: return 0.0
	for task in state.get("tasks", []):
		if task.id != id: continue
		if not task.claimed or task.get("doubled", false): return 0.0
		var before := SaveManager.snapshot()
		var amount := minf(float(task.reward), limit)
		if amount <= 0.0: return 0.0
		task["doubled"] = true
		EconomyManager.earn(amount, "daily_double", false)
		if not GameManager.save_now():
			SaveManager.apply(before)
			changed.emit()
			return 0.0
		changed.emit()
		return amount
	return 0.0

func _process(delta: float) -> void:
	if not GameManager.running or GameManager._suspended: return
	_timer += delta
	if _timer >= 30:
		_timer = 0
		if ProgressionManager.intro_done():
			ensure_day()
			if ProgressionManager.crisis_manager_on():
				_auto_claim()


## Crisis Manager (paid automation): finished daily jobs are claimed for the player.
func _auto_claim() -> void:
	var day := int(state.get("day", -1))
	for task in state.get("tasks", []):
		if not task.claimed and progress(task) >= float(task.target):
			claim(str(task.id), day)

func to_dict() -> Dictionary:
	return state.duplicate(true)

func from_dict(d: Dictionary) -> void:
	state = d.duplicate(true)
	if not state.is_empty(): state.day = int(state.day)

func reset_text() -> String:
	var left := (utc_day() + 1) * 86400 - int(Time.get_unix_time_from_system())
	return tr("Resets in %s · UTC") % Fmt.time(maxi(0, left))
