extends SceneTree
## Onboarding v2 evidence driver (run through tools/onboarding/capture_heroes.py, never by hand on a player's HOME):
##   -s res://tools/onboarding/onboarding.gd -- --mode=prepare|hero|cards|memory [--index=N]
## Thin wrapper: the game's autoloads exist only after this script compiled, so the work lives in onboarding_run.gd.

var _runner


func _initialize() -> void:
	var args := {}
	for a in OS.get_cmdline_user_args():
		var kv := a.trim_prefix("--").split("=", true, 1)
		args[kv[0]] = kv[1] if kv.size() > 1 else "1"
	_go.call_deferred(args)


func _go(args: Dictionary) -> void:
	_runner = load("res://tools/onboarding/onboarding_run.gd").new()
	var ok: bool = await _runner.run(self, args)
	_runner = null
	for i in 4:
		await process_frame
	quit(0 if ok else 1)
