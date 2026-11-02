extends Node
## Bounded onboarding assistance must survive saves and leave established games alone.
var failures := 0
var checks := 0

func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		print("EARLY_PACING FAIL: ", label)

func _ready() -> void:
	await get_tree().process_frame
	SaveManager.delete_save()
	GameManager.new_game()
	var c: Dictionary = ContractManager.offers[0]
	var v: Dictionary = VehicleManager.vehicles[0]
	ContractManager.offline = true
	var normal := ContractManager.estimate(c, v, "normal")
	ContractManager.offline = false
	var early := ContractManager.estimate(c, v, "normal")
	check(early.time <= 12 and early.time < normal.time, "first delivery is at most 12 seconds")
	for key in ["reward", "cost", "risk", "payout", "profit", "path"]:
		check(early[key] == normal[key], "first-delivery " + key + " unchanged")
	var job := ContractManager.dispatch(c, v, "normal")
	check(job.get("intro_trip", false), "dispatch snapshots early-return eligibility")
	var saved := ContractManager.to_dict().duplicate(true)
	ContractManager.from_dict(saved)
	job = ContractManager.jobs[0]
	check(job.get("intro_trip", false), "in-flight early assistance survives save/load")
	EconomyManager.stats["deliveries"] = 5
	job.elapsed = job.total
	ContractManager._start_return(job)
	check(job.ret_total <= 6, "an already dispatched early trip keeps its short return")
	# Each boundary independently excludes normal trips from assistance.
	var ordinary := c.duplicate(true)
	ordinary.tutorial = false
	for reason in ["deliveries", "play_time", "prestige", "offline", "region"]:
		EconomyManager.stats["deliveries"] = 0
		EconomyManager.stats["play_time"] = 0
		ProgressionManager.flags["prestiges"] = 0
		ContractManager.offline = false
		ordinary.dest = c.dest
		match reason:
			"deliveries": EconomyManager.stats["deliveries"] = 5
			"play_time": EconomyManager.stats["play_time"] = 900
			"prestige": ProgressionManager.flags["prestiges"] = 1
			"offline": ContractManager.offline = true
			"region": ordinary.dest = "r1_hub"
		var actual := ContractManager.estimate(ordinary, v, "normal")
		ContractManager.offline = true
		var expected := ContractManager.estimate(ordinary, v, "normal")
		check(actual == expected, reason + " boundary preserves the unassisted estimate")
		ContractManager.offline = false
	# Old jobs have no marker and retain their original return timing.
	job.erase("intro_trip")
	job.phase = "unload"
	ContractManager._start_return(job)
	check(job.ret_total > 6, "legacy/unassisted jobs retain the original return")
	ContractManager.offline = false
	SaveManager.delete_save()
	print("EARLY_PACING checks=", checks, " failures=", failures)
	get_tree().quit(1 if failures else 0)
