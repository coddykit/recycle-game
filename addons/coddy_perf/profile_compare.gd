extends SceneTree
## W67 graphics-profile comparison (owner 2026-10-23: "even very good phones get a low resolution, the games look
## mediocre"). Builds one of the game's perf-gate scenes (tests/perf_gate_hook.gd, Low applied before the world as on a
## weak phone), then for each config sets 3D render scale / MSAA on every 3D viewport (the root and each SubViewport
## that draws 3D), times the frames with vsync off and no FPS cap (best of 3 runs of 240 frames — a relative cost on
## this GPU, not a phone's FPS) and saves a capture. WINDOWED, Compatibility renderer, phone-sized, under shot-lock:
##   shot-lock.sh Godot --path <game> --rendering-method gl_compatibility --audio-driver Dummy --resolution 1440x3120 \
##     -s res://addons/coddy_perf/profile_compare.gd -- --out=<dir> [--size=1440x3120] [--scene=start] [--configs=0.7:0,0.85:2,1.0:2,1.0:4]
## Prints "PROFILE_COMPARE <scene> scale=… msaa=… frame_ms=… draws=…" per config. Never touches a player save (the
## hook uses its own user:// files).
## Source of truth: games/tools/perf (copied into each game as addons/coddy_perf by sync.sh).

const PerfGate = preload("res://addons/coddy_perf/perf_gate.gd")

var _args := {}


func _initialize() -> void:
	for a in OS.get_cmdline_user_args():
		var kv := a.trim_prefix("--").split("=", true, 1)
		_args[kv[0]] = kv[1] if kv.size() > 1 else "1"
	_run.call_deferred()


func _run() -> void:
	if DisplayServer.get_name() == "headless":
		print("PROFILE_COMPARE SKIP: no renderer (run windowed)")
		quit(0)
		return
	var hook: GDScript = load(String(_args.get("hook", "res://tests/perf_gate_hook.gd")))
	var out := String(_args.get("out", "user://profile-compare"))
	DirAccess.make_dir_recursive_absolute(out)
	var scene := String(_args.get("scene", hook.scenes()[0]))
	var configs := String(_args.get("configs", "0.7:0,0.85:0,0.85:2,1.0:0,1.0:2,1.0:4")).split(",")
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	if _args.has("size"):   # a phone's physical size (the window may be bigger than this Mac's screen)
		var wh := String(_args.size).split("x")
		root.size = Vector2i(int(wh[0]), int(wh[1]))
	await hook.setup(self, scene)
	for i in 60:
		Engine.max_fps = 0
		await process_frame
	for c in configs:
		var sm := c.split(":")
		var scale := float(sm[0])
		var msaa := int(sm[1]) if sm.size() > 1 else 0
		var vps := _views()
		for vp in vps:
			vp.scaling_3d_scale = scale
			vp.msaa_3d = {0: Viewport.MSAA_DISABLED, 2: Viewport.MSAA_2X, 4: Viewport.MSAA_4X}.get(msaa, Viewport.MSAA_DISABLED)
		var best := 1e9
		for rep in 3:
			for i in 30:
				Engine.max_fps = 0
				await process_frame
			var t0 := Time.get_ticks_usec()
			for i in 240:
				Engine.max_fps = 0
				await process_frame
			best = minf(best, float(Time.get_ticks_usec() - t0) / 240000.0)
		var r := await PerfGate.measure(self, 30)
		await RenderingServer.frame_post_draw
		var name := "%s_s%03d_m%d" % [scene, roundi(scale * 100), msaa]
		root.get_texture().get_image().save_png(out.path_join(name + ".png"))
		print("PROFILE_COMPARE %s scale=%.2f msaa=%d frame_ms=%.3f draws=%d prims=%d views=%d 3d_px=%s" % [scene, scale, msaa,
			best, r.draws, r.prims, vps.size(), str(vps[vps.size() - 1].size if vps.size() > 1 else root.size)])
	if hook.has_method("teardown"):
		hook.teardown(self)
		await process_frame
	quit(0)


## The viewports that draw 3D: the root and every SubViewport without disable_3d.
func _views() -> Array:
	var out: Array = [root]
	for n in root.find_children("*", "SubViewport", true, false):
		if not (n as SubViewport).disable_3d:
			out.append(n)
	return out
