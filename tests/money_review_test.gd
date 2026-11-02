extends Node

func _ready() -> void:
	var failed := 0
	EconomyManager.cash = 10.0
	if EconomyManager.can_afford(10.0005):
		failed += 1
	if EconomyManager.spend(10.0005, "review regression"):
		failed += 1
	if EconomyManager.cash != 10.0:
		failed += 1
	EconomyManager.cash = 10.0
	if not EconomyManager.spend(10.0, "review regression") or EconomyManager.cash != 0.0:
		failed += 1
	if EconomyManager.spend(0.0005, "review regression") or EconomyManager.cash != 0.0:
		failed += 1
	print("MONEY_REVIEW: 5 checks, %d failures" % failed)
	get_tree().quit(1 if failed else 0)
