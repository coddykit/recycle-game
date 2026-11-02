extends Node
## Static game data loaded from res://data/*.json.
## Designers can tweak balance in those files without touching code.

var config: Dictionary = {}
var regions: Array = []
var vehicles: Array = []
var categories: Array = []
var modifiers: Array = []
var routes: Array = []
var events: Array = []
var vehicle_upgrades: Array = []
var garage_levels: Array = []
var business_upgrades: Array = []
var driver_data: Dictionary = {}
var stages: Array = []
var goals: Array = []
var model_meta: Dictionary = {}
var connections: Array = []
var onboarding: Dictionary = {}

var _region_index := {}
var _vehicle_index := {}
var _category_index := {}
var _modifier_index := {}
var _route_index := {}
var _event_index := {}
var _business_index := {}
var _vupgrade_index := {}


func _init() -> void:
	config = _load("config.json", {})
	regions = _load("regions.json", [])
	vehicles = _load("vehicles.json", [])
	var c: Dictionary = _load("contracts.json", {})
	categories = c.get("categories", [])
	modifiers = c.get("modifiers", [])
	routes = c.get("routes", [])
	events = _load("events.json", [])
	var u: Dictionary = _load("upgrades.json", {})
	vehicle_upgrades = u.get("vehicle", [])
	garage_levels = u.get("garage", [])
	business_upgrades = u.get("business", [])
	driver_data = _load("drivers.json", {})
	var p: Dictionary = _load("progression.json", {})
	stages = p.get("stages", [])
	goals = p.get("goals", [])
	model_meta = _load("model_meta.json", {})
	connections = _load("connections.json", [])
	onboarding = _load("onboarding.json", {})
	for i in regions.size():
		regions[i]["index"] = i
		_region_index[regions[i].id] = regions[i]
	for v in vehicles:
		_vehicle_index[v.id] = v
	for x in categories:
		_category_index[x.id] = x
	for x in modifiers:
		_modifier_index[x.id] = x
	for x in routes:
		_route_index[x.id] = x
	for x in events:
		_event_index[x.id] = x
	for x in business_upgrades:
		_business_index[x.id] = x
	for x in vehicle_upgrades:
		_vupgrade_index[x.id] = x


func _load(file: String, fallback: Variant) -> Variant:
	var path := "res://data/" + file
	if not FileAccess.file_exists(path):
		push_error("Missing data file: " + path)
		return fallback
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	if parsed == null:
		push_error("Invalid JSON in " + path)
		return fallback
	return parsed


func title() -> String:
	return str(config.get("title", "Borderline Empire"))


func region(id: String) -> Dictionary:
	return _region_index.get(id, {})


func region_at(index: int) -> Dictionary:
	if index < 0 or index >= regions.size():
		return {}
	return regions[index]


func vehicle_def(id: String) -> Dictionary:
	return _vehicle_index.get(id, {})


func category(id: String) -> Dictionary:
	return _category_index.get(id, {})


func modifier(id: String) -> Dictionary:
	return _modifier_index.get(id, {})


func route(id: String) -> Dictionary:
	return _route_index.get(id, {})


func event_def(id: String) -> Dictionary:
	return _event_index.get(id, {})


func business(id: String) -> Dictionary:
	return _business_index.get(id, {})


func connection(id: String) -> Dictionary:
	for c in connections:
		if c.id == id:
			return c
	return {}


func vehicle_upgrade(id: String) -> Dictionary:
	return _vupgrade_index.get(id, {})


func garage_level(level: int) -> Dictionary:
	level = clampi(level, 1, garage_levels.size())
	return garage_levels[level - 1]


func rarity(id: String) -> Dictionary:
	for r in driver_data.get("rarities", []):
		if r.id == id:
			return r
	return driver_data.get("rarities", [{}])[0]


func meta(model: String) -> Dictionary:
	return model_meta.get(model, {"min": [-0.5, 0, -0.5], "max": [0.5, 1, 0.5], "size": [1, 1, 1]})


static func color(hex: String) -> Color:
	return Color.html("#" + hex) if not hex.begins_with("#") else Color.html(hex)
