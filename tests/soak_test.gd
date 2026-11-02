extends Node
## Robustness soak (api/soak 2026-10-22). The REAL Main scene (HUD + World) runs for SOAK_MINUTES of game time (default
## 3 = the gate; 30 for a full soak) driven by the reference bot, with a Logger catching script errors / freed-instance
## messages. Every game minute: orphan / node / object counts + static memory (series printed, growth gated), the
## autosave round trip (snapshot -> JSON -> Validator + a real save_game()/load_data()), and softlock checks (money
## finite, no vehicle busy without a job, deliveries keep coming while a vehicle is idle and offers exist). Then UI
## stress: every panel opened / closed SOAK_UI times, every language in turn (scene reload), pause / resume SOAK_UI times.
## Run: godot --headless --path . res://tests/soak_test.tscn     (env SOAK_MINUTES, SOAK_UI)
## Saves go to user://qa/tests/ (the scene path contains "tests/").

class Catch extends Logger:
	var seen: Array = []

	func _log_error(function: String, file: String, line: int, code: String, rationale: String, _editor_notify: bool,
			error_type: int, script_backtraces: Array[ScriptBacktrace]) -> void:
		if error_type != ERROR_TYPE_SCRIPT and not file.ends_with(".gd") and not (code + rationale).contains("freed") \
				and not (code + rationale).contains("must be normalized"):
			return
		var bt := ""
		for b in script_backtraces:
			bt += b.format(2, 2)
		seen.append("%s:%d %s - %s\n%s" % [file, line, function, rationale, bt])

	func _log_message(_message: String, _error: bool) -> void:
		pass


var checks := 0
var failures := 0
var main: Node
var series: Array = []


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		print("SOAK FAIL ", label)
	else:
		print("SOAK ok ", label)


func _mon(m: int) -> float:
	return Performance.get_monitor(m)


func _frames(n := 3) -> void:
	for i in n:
		await get_tree().process_frame


func _start_main() -> void:
	main = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
	get_tree().root.add_child(main)
	get_tree().current_scene = main


func _follow_scene() -> void:
	var cur := get_tree().current_scene
	if cur != null and is_instance_valid(cur) and cur.is_inside_tree():
		main = cur


## The reference bot of tests/test_runner.gd (take the best jobs, resolve events, spend sensibly).
func _bot_step() -> void:
	for j in EventManager.pending_jobs():
		EventManager.resolve(j, EventManager.auto_choice(j), true)
	var offers := ContractManager.offers.duplicate()
	offers.append_array(ContractManager.queue)
	offers.sort_custom(func(a, b): return float(a.reward) > float(b.reward))
	for c in offers:
		var v := ContractManager.best_vehicle(c)
		if v.is_empty():
			continue
		var route := "long" if HeatManager.get_heat(RegionManager.region_of_node(c.dest)) > 60 else "normal"
		ContractManager.dispatch(c, v, route)
	if ProgressionManager.garage_check().ok:
		ProgressionManager.upgrade_garage()
	var nxt := RegionManager.next_locked()
	if nxt >= 0 and RegionManager.unlock_check(nxt).ok:
		RegionManager.unlock(nxt)
	if VehicleManager.vehicles.size() < ProgressionManager.vehicle_slots():
		var best := ""
		for d in Data.vehicles:
			if VehicleManager.buy_check(d.id).ok and EconomyManager.cash > float(d.price) * 1.3:
				best = d.id
		if best != "":
			VehicleManager.buy(best)
	for b in ["automated_dispatch", "fleet_capacity", "contract_office", "warehouse", "dispatch_center",
			"operations_room", "regional_network", "maintenance_bay", "driver_lounge", "advanced_logistics"]:
		if ProgressionManager.business_check(b).ok and EconomyManager.cash > ProgressionManager.business_cost(b) * 2.0:
			ProgressionManager.buy_business(b)
	for cn in Data.connections:
		if ProgressionManager.connection_check(cn.id).ok:
			ProgressionManager.buy_connection(cn.id)
	for i in RegionManager.unlocked_count:
		if RegionManager.depot_check(i).ok and EconomyManager.cash > RegionManager.depot_cost(i) * 2.5:
			RegionManager.build_depot(i)
	for v in VehicleManager.vehicles:
		for u in ["engine", "capacity", "reliability", "navigation", "handling"]:
			if VehicleManager.upgrade_check(v, u).ok and EconomyManager.cash > VehicleManager.upgrade_cost(v, u) * 3.0:
				VehicleManager.upgrade(v, u)


func _sample(minute: int) -> void:
	var s := {"min": minute, "orphans": int(_mon(Performance.OBJECT_ORPHAN_NODE_COUNT)),
		"nodes": int(_mon(Performance.OBJECT_NODE_COUNT)), "objects": int(_mon(Performance.OBJECT_COUNT)),
		"static_mb": _mon(Performance.MEMORY_STATIC) / 1048576.0}
	series.append(s)
	print("SOAK min %3d orphans %d nodes %d objects %d static %.1f MB cash %s deliveries %d vehicles %d" % [minute,
		s.orphans, s.nodes, s.objects, s.static_mb, Fmt.money(EconomyManager.cash), int(EconomyManager.stat("deliveries")),
		VehicleManager.vehicles.size()])


## SOAK_DUMP=1: node histogram (class under parent name) so a node count that climbs can be attributed.
func _hist(tag: String) -> void:
	if OS.get_environment("SOAK_DUMP") == "":
		return
	var h := {}
	var stack: Array = [get_tree().root]
	while not stack.is_empty():
		var n: Node = stack.pop_back()
		var k := "%s<%s" % [n.get_class(), n.get_parent().name if n.get_parent() else "-"]
		h[k] = int(h.get(k, 0)) + 1
		stack.append_array(n.get_children())
	var keys := h.keys()
	keys.sort_custom(func(a, b): return h[a] > h[b])
	var out := []
	for k in keys.slice(0, 22):
		out.append("%s=%d" % [k, h[k]])
	print("SOAK HIST ", tag, " ", ", ".join(out))


## Waits out the effects' real-time lifetime (walkers 5.5 s + fade) and returns the node count.
func _settled_nodes() -> int:
	await get_tree().create_timer(7.0, true, false, true).timeout
	await _frames(3)
	return int(_mon(Performance.OBJECT_NODE_COUNT))


func _state_ok() -> String:
	if is_nan(EconomyManager.cash) or is_inf(EconomyManager.cash) or EconomyManager.cash < 0.0:
		return "cash invalid %s" % str(EconomyManager.cash)
	for v in VehicleManager.vehicles:
		if v.state == "busy" and ContractManager.get_job_for_vehicle(v.id).is_empty():
			return "vehicle %s busy without a job" % str(v.id)
	for i in RegionManager.unlocked_count:
		var h := HeatManager.get_heat(i)
		if is_nan(h) or h < 0.0 or h > 100.0:
			return "heat out of range %s" % str(h)
	return ""


func _save_roundtrip() -> String:
	var snap := SaveManager.snapshot()
	var back: Variant = JSON.parse_string(JSON.stringify(snap))
	if typeof(back) != TYPE_DICTIONARY:
		return "snapshot did not survive JSON"
	if not SaveManager.Validator.valid(back):
		return "snapshot rejected by validator: " + SaveManager.Validator.last_reason
	if not SaveManager.save_game():
		return "save_game failed: " + SaveManager.last_error
	if SaveManager.load_data().is_empty():
		return "saved file did not load"
	return ""


func _ready() -> void:
	get_tree().create_timer(1500.0).timeout.connect(func(): get_tree().quit(2))
	var minutes := int(OS.get_environment("SOAK_MINUTES")) if OS.get_environment("SOAK_MINUTES") != "" else 3
	var ui_n := int(OS.get_environment("SOAK_UI")) if OS.get_environment("SOAK_UI") != "" else 20
	seed(20261022)
	await _frames(1)
	get_tree().root.size = Vector2i(540, 960)
	await _frames(1)
	var catch := Catch.new()
	OS.add_logger(catch)
	SaveManager.delete_save()
	GameManager.settings["language"] = "en"
	GameManager.settings["quality_notice"] = ""
	GameManager.apply_language()
	GameManager.time_scale = 0.0001 # game time is driven below, in fixed steps
	_start_main()
	await _frames(30)
	# ---- (1) regressions found by this soak
	# a scene reload frees the crate pool (static) with the world: the next delivery must skip the dead crates
	var dead := Node3D.new()
	UnloadSequence._crate_pool.append(dead)
	dead.free()
	var holder := Node3D.new()
	add_child(holder)
	var crate: Node3D = UnloadSequence._take_crate(holder)
	check(crate != null and crate.get_parent() == holder, "unload crate pool skips freed crates")
	# the unload fade scales walkers to zero while they still walk: no engine error from the turn-to-heading slerp
	var walker := CharacterActor.new()
	holder.add_child(walker)
	walker.setup("worker", 1.35)
	walker.walk_to(Vector3(20, 0, 20), "idle", 1.4)
	await _frames(3)
	walker.create_tween().tween_property(walker, "scale", Vector3.ONE * 0.01, 0.3)
	await get_tree().create_timer(0.5, true, false, true).timeout
	check(catch.seen.is_empty(), "a walker scaled down mid-walk raises no error (%s)" % str(catch.seen.slice(0, 1)))
	holder.queue_free()
	await _frames(2)
	# ---- (2) game-time soak
	var problems: Array = []
	var last_deliveries := -1.0
	var stalled_min := 0
	var node_mid := 0
	var fuzz_saves := 0
	var fuzz_bad := 0
	var vehicles_mid := 0
	for m in minutes:
		for i in 240: # 240 x 0.25 s = one game minute
			GameManager.tick(0.25)
			if i % 4 == 0:
				_bot_step()
				# save fuzz: the state right after every round of bot purchases / dispatches must encode, decode
				# through JSON and be accepted by the validator (LESSONS B1: the codec takes every state the game makes)
				var back: Variant = JSON.parse_string(JSON.stringify(SaveManager.snapshot()))
				fuzz_saves += 1
				if typeof(back) != TYPE_DICTIONARY or not SaveManager.Validator.valid(back):
					fuzz_bad += 1
					if fuzz_bad <= 3:
						problems.append("min %d: fuzz save rejected: %s" % [m, SaveManager.Validator.last_reason])
			if i % 8 == 7:
				await get_tree().process_frame
		await _frames(2)
		_sample(m)
		if m in [1, 8]:
			_hist("min %d" % m)
		var why := _state_ok()
		if why != "":
			problems.append("min %d: %s" % [m, why])
		var rt := _save_roundtrip()
		if rt != "":
			problems.append("min %d: %s" % [m, rt])
		# softlock: while an idle vehicle and a rep-ok offer exist the deliveries counter must move within 6 minutes
		var d := EconomyManager.stat("deliveries")
		if d > last_deliveries:
			stalled_min = 0
		else:
			stalled_min += 1
		last_deliveries = d
		if m >= 8 and stalled_min >= 6 and not VehicleManager.idle_vehicles().is_empty() and not ContractManager.offers.is_empty():
			problems.append("min %d: softlock, no delivery for %d min with an idle vehicle + offers" % [m, stalled_min])
		if minutes >= 6 and m == minutes * 2 / 3:
			node_mid = await _settled_nodes()
			_hist("settled 2/3")
			vehicles_mid = VehicleManager.vehicles.size()
		if m == minutes / 3 and m > 0 and GameManager.running: # a cold restart in the middle (Continue)
			GameManager.save_now()
			main.queue_free()
			await _frames(2)
			_start_main()
			await _frames(10)
	print("SOAK save fuzz: %d saves, %d unloadable" % [fuzz_saves, fuzz_bad])
	check(problems.is_empty(), "no state / save / softlock problem in %d game minutes %s" % [minutes, str(problems.slice(0, 6))])
	check(EconomyManager.stat("deliveries") > 1.0 * minutes, "the bot keeps delivering (%d)" % int(EconomyManager.stat("deliveries")))
	if series.size() >= 6:
		var a: Dictionary = series[series.size() / 3]
		var z: Dictionary = series[series.size() - 1]
		check(int(z.orphans) <= int(a.orphans) + 20, "orphan nodes do not grow (%d -> %d)" % [a.orphans, z.orphans])
		check(float(z.static_mb) <= float(a.static_mb) * 1.5 + 20.0, "static memory does not run away (%.1f -> %.1f MB)" % [a.static_mb, z.static_mb])
	# The soak runs game time far faster than real time and effects (particles, walkers, tweens) live in real time, so the
	# node count climbs during the run by design (pooled emitters stay once allocated). Compared at the same "settled"
	# state, halfway and at the end, a real leak keeps climbing.
	var peak := int(_mon(Performance.OBJECT_NODE_COUNT))
	var settled := await _settled_nodes()
	print("SOAK nodes peak %d settled-2/3 %d settled-end %d" % [peak, node_mid, settled])
	_hist("settled")
	if node_mid > 0:
		check(settled <= node_mid + node_mid / 4 + 150 + 60 * maxi(0, VehicleManager.vehicles.size() - vehicles_mid), # pools (coins, particles, crates) still fill with load
			"settled node count stops growing (%d at 2/3 -> %d at the end)" % [node_mid, settled])
	# ---- (3) UI stress
	_follow_scene()
	await _frames(5)
	var before := {"orphans": int(_mon(Performance.OBJECT_ORPHAN_NODE_COUNT)), "nodes": int(_mon(Performance.OBJECT_NODE_COUNT)),
		"static": _mon(Performance.MEMORY_STATIC) / 1048576.0}
	var panels: Array = ["contracts", "fleet", "map", "upgrades", "business", "settings", "shop", "daily"]
	for p in panels:
		for i in ui_n:
			UIManager.open_panel(p)
			await get_tree().process_frame
			if i % 3 == 0:
				UIManager.open_panel(p) # re-open the open panel
			UIManager.close_panel()
			await get_tree().process_frame
	if not ContractManager.offers.is_empty():
		for i in ui_n:
			UIManager.open_panel("dispatch", ContractManager.offers[0].id)
			await get_tree().process_frame
			UIManager.close_panel()
	if not VehicleManager.vehicles.is_empty():
		for i in ui_n:
			UIManager.open_panel("fleet", {"vehicle": VehicleManager.vehicles[0].id})
			await get_tree().process_frame
			UIManager.close_panel()
	await _frames(5)
	print("SOAK static after panels %.1f MB" % (_mon(Performance.MEMORY_STATIC) / 1048576.0))
	for lang in GameManager.LANGUAGES:
		GameManager.set_setting("language", lang)
		await _frames(8)
		_follow_scene()
		print("SOAK static after language %s %.1f MB" % [lang, _mon(Performance.MEMORY_STATIC) / 1048576.0])
		for p in panels:
			UIManager.open_panel(p)
			await _frames(2)
			UIManager.close_panel()
	# a second round over every language must add (almost) nothing: the first visit fills the glyph / font caches, a
	# per-switch leak would show here
	var mid := _mon(Performance.MEMORY_STATIC) / 1048576.0
	for lang in GameManager.LANGUAGES:
		GameManager.set_setting("language", lang)
		await _frames(8)
		_follow_scene()
		for p in panels:
			UIManager.open_panel(p)
			await _frames(2)
			UIManager.close_panel()
	var round2 := _mon(Performance.MEMORY_STATIC) / 1048576.0
	print("SOAK static language rounds: %.1f -> %.1f MB" % [mid, round2])
	check(round2 <= mid + 25.0, "a second language round does not grow static memory (%.1f -> %.1f MB)" % [mid, round2])
	GameManager.set_setting("language", "en")
	await _frames(8)
	_follow_scene()
	for i in ui_n * 5:
		get_tree().root.propagate_notification(Node.NOTIFICATION_APPLICATION_PAUSED)
		await get_tree().process_frame
		if i % 7 == 0:
			GameManager._paused_at -= 600.0
		get_tree().root.propagate_notification(Node.NOTIFICATION_APPLICATION_RESUMED)
		await get_tree().process_frame
	await _frames(10)
	var after := {"orphans": int(_mon(Performance.OBJECT_ORPHAN_NODE_COUNT)), "nodes": int(_mon(Performance.OBJECT_NODE_COUNT)),
		"static": _mon(Performance.MEMORY_STATIC) / 1048576.0}
	print("SOAK ui before ", before, " after ", after)
	check(int(after.orphans) <= int(before.orphans) + 40, "UI stress leaves no orphan nodes (%d -> %d)" % [before.orphans, after.orphans])
	check(int(after.nodes) <= int(before.nodes) + 300, "UI stress leaves no extra nodes (%d -> %d)" % [before.nodes, after.nodes])
	# CJK glyph caches are filled once per script (ja/ko/zh share one 16 MB font but ~90 MB of glyphs on the first visit)
	check(float(after.static) <= float(before.static) + 300.0, "UI stress keeps static memory bounded (%.1f -> %.1f MB)" % [before.static, after.static])
	GameManager.time_scale = 1.0
	TranslationServer.set_locale("en")
	OS.remove_logger(catch)
	for s in catch.seen.slice(0, 12):
		print("SOAK SCRIPT ERROR ", s)
	check(catch.seen.size() == 0, "no script error or freed-instance message (%d)" % catch.seen.size())
	var cur := get_tree().current_scene
	if cur != null and is_instance_valid(cur):
		cur.queue_free()
	await _frames(2)
	SaveManager.delete_save()
	print("SOAK checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)
