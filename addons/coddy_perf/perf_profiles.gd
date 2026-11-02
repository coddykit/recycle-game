extends RefCounted
## Graphics profiles (games/CLAUDE.md §10, W60-B1 shared module). One reference table plus a validator every game's
## own table must pass, and the engine-level part of applying a tier (frame cap, render scale, MSAA, mesh LOD). The
## game-specific part (crowd, particles, weather, draw distance) stays in the game, scaled by `crowd` / `particles`.
##   Low    = no shadows, render scale 0.80–0.85 (reference 0.85 without MSAA), MSAA ≤ 2x, 30 FPS cap, SSAO / glow
##            off, crowd and particles ≤ 50 % of the full count (High), coarser LOD.
##   Medium = native 3D resolution (scale > Low, the reference 1.0), MSAA 2x, 60 FPS cap, simple shadows allowed.
##   High   = a flagship's default, else the player's choice: MSAA 4x, the full crowd.
## On Android / Web GLES3 apply_core() keeps MSAA only where Godot 4.7.2 does not leak it (guard_msaa below).
## W67 (owner 2026-10-23: "even very good phones get a low resolution, the games look mediocre"): Medium had no
## anti-aliasing at all and Low drew 3D at 0.70 — jagged, blurry edges on every phone. Measured on kaiten-sushi,
## sky-tower-ops, chaiwala-rush and smuggler (game-builds/w67/profiles/): the Compatibility renderer (Android) has MSAA
## but NO screen-space AA — Viewport.screen_space_aa = FXAA draws the same pixels — so MSAA is the only AA. Low was
## first 0.80 + MSAA 2x (emulator p95 +6–10 %), then 0.85 without MSAA (p95 +0 %, visibly sharper than 0.70): MSAA at a
## render scale ≠ 1 is exactly the 4.7.2 Android framebuffer leak. MSAA only touches 3D (msaa_3d); the 2D UI is drawn by canvas_items stretch at the screen's resolution
## and scaling_3d_scale never scales it.
## Source of truth: games/tools/perf (copied into each game as addons/coddy_perf by sync.sh).

const LOW := 0
const MEDIUM := 1
const HIGH := 2

## scale: 3D render scale · fps: Engine.max_fps on a handheld · msaa: 0 off / 2 / 4 · shadows · crowd / particles:
## multipliers on the game's own full counts · lod: Viewport.mesh_lod_threshold (bigger = coarser).
const REFERENCE := [
	{"scale": 0.85, "fps": 30, "msaa": 0, "shadows": false, "crowd": 0.4, "particles": 0.4, "lod": 4.0},
	{"scale": 1.0, "fps": 60, "msaa": 2, "shadows": true, "crowd": 0.8, "particles": 0.8, "lod": 2.0},
	{"scale": 1.0, "fps": 60, "msaa": 4, "shadows": true, "crowd": 1.0, "particles": 1.0, "lod": 1.0},
]
const LOW_MAX_SCALE := 0.85
const LOW_MIN_SCALE := 0.8 ## below this the 3D looks blurry on a phone (W67: 0.70 was the old Low)
const LOW_MAX_MSAA := 2
const MEDIUM_MIN_MSAA := 2
const LOW_FPS := 30
const MEDIUM_FPS := 60


static func reference_profile(tier: int) -> Dictionary:
	return REFERENCE[clampi(tier, LOW, HIGH)].duplicate()


## The §10 rules a game's [low, medium, high] profile table must keep (keys as in REFERENCE; a game may leave out what
## it does not have — e.g. no shadows at all — and missing keys are not checked, except that a 3D table (one with
## "scale") must set "msaa" on Medium and High). Returns the broken rules ([] = ok).
static func validate(table: Array) -> Array[String]:
	var bad: Array[String] = []
	if table.size() != 3:
		bad.append("need exactly three tiers (low, medium, high)")
		return bad
	var lo: Dictionary = table[LOW]
	var md: Dictionary = table[MEDIUM]
	var hi: Dictionary = table[HIGH]
	if int(lo.get("fps", 0)) <= 0 or int(lo.get("fps", 0)) > LOW_FPS:
		bad.append("Low must cap the frame rate at %d FPS (got %s)" % [LOW_FPS, str(lo.get("fps"))])
	if int(md.get("fps", 0)) != MEDIUM_FPS or int(hi.get("fps", 0)) != MEDIUM_FPS:
		bad.append("Medium and High cap at %d FPS" % MEDIUM_FPS)
	if lo.has("scale") and (float(lo.scale) > LOW_MAX_SCALE + 0.0001 or float(lo.scale) < LOW_MIN_SCALE - 0.0001):
		bad.append("Low render scale must be %.2f–%.2f (got %.2f)" % [LOW_MIN_SCALE, LOW_MAX_SCALE, float(lo.scale)])
	if lo.has("scale") and md.has("scale") and float(lo.scale) >= float(md.scale):
		bad.append("Low render scale must be below Medium")
	if bool(lo.get("shadows", false)):
		bad.append("Low has no shadows")
	if int(lo.get("msaa", 0)) > LOW_MAX_MSAA:
		bad.append("Low MSAA must be ≤ %dx (got %s)" % [LOW_MAX_MSAA, str(lo.get("msaa"))])
	if lo.has("scale") and not (md.has("msaa") and hi.has("msaa")):
		bad.append("Medium and High must set msaa (W67: no tier without anti-aliasing)")
	if md.has("msaa") and int(md.msaa) < MEDIUM_MIN_MSAA:
		bad.append("Medium needs MSAA ≥ %dx (got %s)" % [MEDIUM_MIN_MSAA, str(md.msaa)])
	if md.has("msaa") and hi.has("msaa") and int(hi.msaa) < int(md.msaa):
		bad.append("High MSAA must be ≥ Medium")
	for k in ["ssao", "glow"]:
		if bool(lo.get(k, false)):
			bad.append("Low has %s off" % k)
	for k in ["crowd", "particles"]:
		if lo.has(k) and hi.has(k) and float(lo[k]) > 0.5 * float(hi[k]) + 0.0001:
			bad.append("Low %s must be ≤ 50%% of the full count (%.2f vs High %.2f)" % [k, float(lo[k]), float(hi[k])])
		if lo.has(k) and md.has(k) and float(lo[k]) > float(md[k]):
			bad.append("Low %s must be ≤ Medium" % k)
		if md.has(k) and hi.has(k) and float(md[k]) > float(hi[k]):
			bad.append("Medium %s must be ≤ High" % k)
	if lo.has("lod") and md.has("lod") and float(lo.lod) < float(md.lod):
		bad.append("Low LOD must be at least as coarse as Medium")
	return bad


## The frame cap for a tier: 30 on Low, 60 above (handhelds; desktops keep the display rate unless `cap_desktop`).
static func fps_cap(table: Array, tier: int, is_handheld: bool, cap_desktop: bool = false) -> int:
	if not is_handheld and not cap_desktop:
		return 0
	return int((table[clampi(tier, LOW, HIGH)] as Dictionary).get("fps", MEDIUM_FPS))


## The engine-level part of a tier: frame cap, render scale (handhelds only unless `scale_desktop`), MSAA, mesh LOD.
## Safe with a null viewport (tests, boot). Returns the profile used.
static func apply_core(table: Array, tier: int, viewport: Viewport, is_handheld: bool, opts: Dictionary = {}) -> Dictionary:
	var p: Dictionary = table[clampi(tier, LOW, HIGH)]
	if not bool(opts.get("keep_fps", false)):
		Engine.max_fps = fps_cap(table, tier, is_handheld, bool(opts.get("cap_desktop", false)))
	if viewport != null:
		if p.has("scale") and (is_handheld or bool(opts.get("scale_desktop", false))):
			viewport.scaling_3d_scale = float(p.scale)
		if p.has("msaa"):
			viewport.msaa_3d = {0: Viewport.MSAA_DISABLED, 2: Viewport.MSAA_2X, 4: Viewport.MSAA_4X}.get(int(p.msaa), Viewport.MSAA_DISABLED)
		if p.has("lod"):
			viewport.mesh_lod_threshold = float(p.lod)
		# Godot 4.7.2 Android GLES3: MSAA + an internal 3D buffer leaks a framebuffer per frame (guard_msaa below)
		guard_msaa_soon(viewport, bool(opts.get("post_fx", false)) or bool(p.get("glow", false)) or bool(p.get("ssao", false)))
	return p


# ---------------------------------------------------------------- MSAA leak guard (Godot 4.7.2, Android / Web GLES3)

## Godot 4.7.2 Compatibility renderer on Android / Web (GLES3 with EXT_multisampled_render_to_texture — every Adreno /
## Mali / PowerVR phone): with 3D MSAA on AND an internal 3D buffer — render scale ≠ 1, or glow / adjustments / SSAO in
## the Environment — RenderSceneBuffersGLES3::_check_render_buffers() never sees its MSAA set-up as done (it tests
## msaa3d.color, which this path never fills) and creates a new MSAA framebuffer EVERY frame without deleting the old
## one, until a native allocation fails (Airport High on a Galaxy Tab S8: scudo OOM, 69d4de8021; the Corrupt cluster,
## f9bf16ac6e). Fixed upstream in 81a533bb9e (2026-10-19), not in 4.7.2. Until the export template carries that fix,
## MSAA stays off on Android / Web GLES3 whenever an internal buffer is in use (desktop and iOS Metal unchanged).
## apply_core() calls guard_msaa(); a game that turns glow / adjustments / SSAO on later (day cycle, look-dev) or
## changes the render scale itself calls PerfProfiles.guard_msaa(viewport) again right after.
static var android_gl_override := -1 ## -1 = this build; 0 = no; 1 = behave like an Android / Web GLES3 build (tests)


## The engine path with the per-frame MSAA framebuffer leak (Android / Web on the Compatibility renderer).
static func android_gl() -> bool:
	if android_gl_override >= 0:
		return android_gl_override == 1
	return (OS.has_feature("android") or OS.has_feature("web")) \
		and RenderingServer.get_current_rendering_method() == "gl_compatibility"


## True when this Environment makes the GLES3 renderer use an internal 3D buffer (an effect applied in post).
static func env_post_fx(env: Environment) -> bool:
	if env == null:
		return false
	return env.glow_enabled or env.adjustment_enabled or env.ssao_enabled \
		or (env.background_mode == Environment.BG_CANVAS and env.tonemap_mode != Environment.TONE_MAPPER_LINEAR)


## The Environment a viewport renders with: its current camera's own, else its World3D's (a WorldEnvironment sets it).
static func view_env(vp: Viewport) -> Environment:
	if vp == null:
		return null
	var cam := vp.get_camera_3d()
	if cam != null and cam.environment != null:
		return cam.environment
	var w := vp.find_world_3d()
	return w.environment if w != null else null


## Would MSAA leak a framebuffer per frame with these settings?
static func msaa_leaks(msaa: int, scale: float, post_fx: bool, on_android_gl: bool) -> bool:
	return on_android_gl and msaa != Viewport.MSAA_DISABLED and (not is_equal_approx(scale, 1.0) or post_fx)


## Switches the viewport's 3D MSAA off where it would leak (Android / Web GLES3 and render scale ≠ 1 or a post effect in
## its Environment; `post_fx` = an effect the game turns on later on this tier). Returns true when it switched it off.
static func guard_msaa(vp: Viewport, post_fx: bool = false) -> bool:
	if vp == null:
		return false
	if msaa_leaks(vp.msaa_3d, vp.scaling_3d_scale, post_fx or env_post_fx(view_env(vp)), android_gl()):
		vp.msaa_3d = Viewport.MSAA_DISABLED
		return true
	return false


## guard_msaa now and once more on the next frame (the render scale or a WorldEnvironment set later in the same frame).
## Call it right after a game's own `msaa_3d =` (thumbnails, previews, its own tier code).
static func guard_msaa_soon(vp: Viewport, post_fx: bool = false) -> bool:
	var off := guard_msaa(vp, post_fx)
	if vp != null and vp.is_inside_tree():
		var id := vp.get_instance_id()   # not the viewport itself: a lambda holding a freed object errors when called
		var recheck := func() -> void:
			var v := instance_from_id(id) as Viewport
			if v != null:
				guard_msaa(v, post_fx)
		vp.get_tree().process_frame.connect(recheck, CONNECT_ONE_SHOT)
	return off


## W63 frame pacing on a handheld: the tier's cap while the player is touching the game; 20 FPS under a window that
## covers the scene (reports, shops, settings) once it has been left alone COVERED_IDLE_S; after IDLE_AFTER_S without
## input 30 at most (20 on a 30-FPS tier). The game resets its idle clock on any touch / key / drag (_input) AND while
## ui_active() (_process: a list's finger scroll and its fling never reach _input — TouchScroll uses them up first).
## PerfGuard must skip throttled frames (< 30 FPS cap).
## Play review 2026-10-23 (Corrupt State "the scrolling isn't smooth", fix 3ce4774aa4): the window cap was 20 FPS
## whatever the finger did, and the lists live in windows — every list scrolled at 20 FPS. The 20 now waits until the
## window is only being read.
const IDLE_AFTER_S := 5.0
const COVERED_IDLE_S := 1.5
const STATIC_FPS := 20
const HOLD_SAFETY_MS := 3000 ## a "finger down" with no scroll for this long no longer counts (a lost release)


static func live_cap(tier_fps: int, covered: bool, idle_s: float) -> int:
	if covered:
		return STATIC_FPS if idle_s >= COVERED_IDLE_S else tier_fps
	if idle_s >= IDLE_AFTER_S:
		return 30 if tier_fps > 30 else STATIC_FPS
	return tier_fps


## True while the player is using the UI in a way the game's _input may never see: the left button / a finger is down,
## or the viewport's coddy_ui TouchScroll (meta "coddy_touch_scroll") has a finger down or a list dragging / gliding.
## Duck-typed: games without the UI kit just get the button test. Poll it in _process.
static func ui_active(vp: Viewport) -> bool:
	if Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		return true
	if vp == null or not vp.has_meta("coddy_touch_scroll"):
		return false
	var ts = vp.get_meta("coddy_touch_scroll")
	if ts == null or not is_instance_valid(ts):
		return false
	if ts.has_method("is_scrolling") and bool(ts.is_scrolling()):
		return true
	if bool(ts.get("finger_down")):
		var down = ts.get("_down_ms")
		return down == null or Time.get_ticks_msec() - int(down) < HOLD_SAFETY_MS
	return false


## The idle clock after one frame: back to 0 while ui_active(vp), else + delta. `idle_s = PerfProfiles.idle_step(idle_s,
## delta, get_viewport())` in _process, next to the game's own _input reset.
static func idle_step(idle_s: float, delta: float, vp: Viewport) -> float:
	return 0.0 if ui_active(vp) else idle_s + delta
