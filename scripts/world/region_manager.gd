extends Node
## Owns region unlock state, regional depots and the road network.

signal region_unlocked(index: int)
signal depot_built(index: int)

var graph := RoadGraph.new()
var unlocked_count := 1
var depots: Array = []  # region indices with a depot


func _ready() -> void:
	graph.build(Data.regions, spacing(), tile_size())


func reset() -> void:
	unlocked_count = 1
	depots = []


func spacing() -> float:
	return float(Data.config.get("tile_spacing", 44.0))


func tile_size() -> float:
	return float(Data.config.get("tile_size", 40.0))


func count() -> int:
	return Data.regions.size()


func max_region() -> int:
	return unlocked_count - 1


func is_unlocked(index: int) -> bool:
	return index < unlocked_count


func center(index: int) -> Vector3:
	return RoadGraph.region_center(Data.region_at(index), spacing())


func region_of_node(node_id: String) -> int:
	return int(graph.nodes.get(node_id, {}).get("region", 0))


func node_name(node_id: String) -> String:
	return str(graph.nodes.get(node_id, {}).get("name", node_id))


func region_name(index: int) -> String:
	return tr(str(Data.region_at(index).get("name", "?")))


## Contract pickup/drop locations among unlocked regions.
func locations() -> Array:
	var out := []
	for i in unlocked_count:
		for s in ["hub", "a", "b"]:
			out.append("r%d_%s" % [i, s])
	return out


func next_locked() -> int:
	return unlocked_count if unlocked_count < count() else -1


func unlock_check(index: int) -> Dictionary:
	var r := Data.region_at(index)
	if r.is_empty():
		return {"ok": false, "reason": "Unknown region"}
	if is_unlocked(index):
		return {"ok": false, "reason": "Unlocked"}
	if index != unlocked_count:
		return {"ok": false, "reason": tr("Unlock %s first") % region_name(index - 1)}
	if EconomyManager.reputation < int(r.unlock_rep):
		return {"ok": false, "reason": tr("Need %d reputation") % int(r.unlock_rep), "rep": true}
	if not EconomyManager.can_afford(float(r.unlock_cost)):
		return {"ok": false, "reason": tr("Need %s") % Fmt.money(float(r.unlock_cost)), "cash": true}
	return {"ok": true, "reason": ""}


func unlock(index: int) -> bool:
	if not unlock_check(index).ok:
		return false
	EconomyManager.spend(float(Data.region_at(index).unlock_cost), "region")
	unlocked_count = index + 1
	EconomyManager.add_influence(3)
	region_unlocked.emit(index)
	return true


func depot_cost(index: int) -> float:
	var r := Data.region_at(index)
	return maxf(2500.0, float(r.get("unlock_cost", 0)) * 0.45)


func depot_permits() -> int:
	return ProgressionManager.business_level("regional_network")


func depot_check(index: int) -> Dictionary:
	if index == 0:
		return {"ok": false, "reason": "HQ is here"}
	if not is_unlocked(index):
		return {"ok": false, "reason": "Region locked"}
	if depots.has(index):
		return {"ok": false, "reason": "Built"}
	if depots.size() >= depot_permits():
		return {"ok": false, "reason": "Needs Regional Network upgrade"}
	if not EconomyManager.can_afford(depot_cost(index)):
		return {"ok": false, "reason": tr("Need %s") % Fmt.money(depot_cost(index))}
	return {"ok": true, "reason": ""}


func build_depot(index: int) -> bool:
	if not depot_check(index).ok:
		return false
	EconomyManager.spend(depot_cost(index), "depot")
	depots.append(index)
	depot_built.emit(index)
	return true


## Node ids vehicles can be based at.
func home_nodes() -> Array:
	var out := ["hq"]
	for d in depots:
		out.append("r%d_hub" % int(d))
	return out


func nearest_home(node_id: String) -> String:
	var best := "hq"
	var best_len := INF
	for h in home_nodes():
		var p := graph.find_path(node_id, h, "normal", max_region())
		if p.is_empty():
			continue
		var l := graph.path_length(p)
		if l < best_len:
			best_len = l
			best = h
	return best


func to_dict() -> Dictionary:
	return {"unlocked": unlocked_count, "depots": depots}


func from_dict(d: Dictionary) -> void:
	reset()
	unlocked_count = clampi(int(d.get("unlocked", 1)), 1, count())
	for x in d.get("depots", []):
		var i := int(x)
		if i > 0 and i < unlocked_count and not depots.has(i):
			depots.append(i)
