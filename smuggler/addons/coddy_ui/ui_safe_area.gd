extends RefCounted
## Device safe area that also keeps the UI off the Android navigation bar (owner 2026-10-26, Döner Night on a 3-button
## Android phone: the bottom tab bar sat UNDER the system |||  ○  < buttons — "in ALL games UI sits under the bottom
## safe area").
##
## Why the engine value is not enough: every game exports with `screen/immersive_mode=true`. Godot 4.7.2 then hides the
## system bars and DisplayServer.get_display_safe_area() reports ONLY the display cutout (GodotIO.getDisplaySafeArea →
## WindowInsets.Type.displayCutout() in immersive mode). But the navigation bar still comes back over the game: a swipe
## from the edge shows it as a transient overlay (the insets do not change, so nothing moves), and OEM layers (Samsung
## Game Booster's nav bar with its game / touch-lock buttons) keep it up. The UI must therefore keep out of the bar's
## area whether the bar is visible right now or not: we reserve the navigation-bar insets IGNORING visibility
## (WindowInsets.getInsetsIgnoringVisibility(navigationBars), API 30+; stable insets before) — left / right / bottom
## (the 3-button bar sits at the side in landscape on phones). Gesture navigation reserves its small handle strip the
## same way (Android: keep touch targets out of the gesture area).
## The status bar too (Patron 2026-10-28, S1 banner audit: the top HUD of Borderline / Snow Empire / Parts Rush / Sky
## Tower / Disaster Command and, in the fleet scan, of most games sat under it): immersive mode hides it, an edge swipe
## brings it back OVER the game, and rect() used to hold only the cutout at the top — none on a phone without one, none
## at the top in landscape (the cutout is at the side). rect() / edges() now reserve its height at the top, visible or
## not (getInsetsIgnoringVisibility(statusBars)); overlay_edges() is the same rect from now on (kept for its callers).
## Backgrounds may still run to the screen edge; text and buttons stay inside.
##
## Use it as a drop-in for the engine call (same coordinate space: physical screen px, Rect2i):
##     const UiSafeArea := preload("res://addons/coddy_ui/ui_safe_area.gd")
##     var safe := UiSafeArea.rect()                 # was DisplayServer.get_display_safe_area()
##     var e := UiSafeArea.edges(get_viewport_rect().size)   # canvas px gaps: left, top, right, bottom
## Off Android (desktop, iOS, tests) rect() IS DisplayServer.get_display_safe_area() — iOS already includes the home
## indicator. Cost: a few JNI getters at most once per REFRESH_MS (cached), nothing per frame.
##
## Overlays (owner 2026-10-27, Borderline intro on a 3-button phone: the mentor panel's Continue sat on the nav bar and
## "Skip intro" under the status bar): intro / onboarding / coach / toast / Skip / notice layers that are anchored to a
## screen edge use overlay_edges(canvas) — edges() plus, at the top, the status bar's height even while immersive mode
## hides it (an edge swipe brings it back OVER the game; a button under it cannot be hit). Backgrounds may run on to the
## screen edge; buttons and text stay inside these gaps. Zero off a phone unless a simulation is set (desktop frames):
##     var e := UiSafeArea.overlay_edges(get_viewport_rect().size)   # skip.offset_top = e.y + 16; panel.offset_bottom = -(e.w + 24)
## Desktop frames / tests: env CODDY_SAFE_SIM="1080x2400:0,110,0,126" (phone screen px : left,top,right,bottom insets)
## or UiSafeArea.simulate(Vector2i(1080, 2400), Vector4(0, 110, 0, 126)) makes rect() / edges() / overlay_edges() act
## like that phone (games/tools/ui/tools/capture_safe_overlays.gd draws the bars on the frames). The top inset is then
## the status bar AND the cutout. A third value — "1080x1920:0,0,0,144:72" / simulate(screen, insets, 72) — is the
## status bar alone: the top inset is then only the cutout, like rect() on a real phone in immersive mode (Patron 10-28:
## a HUD that read rect() looked right on the 2-value simulation and still sat under the status bar on a phone without
## a cutout, and in landscape where the cutout is at the side) — overlay_edges() still reserves the status bar.
##
## HIDDEN navigation bar (owner 2026-10-30, Chaiwala: "safeareaviewin bottomından bahsediyorum, orasını artık
## kullanabiliriz, oyunda kocaman boşluk kalıyor"): on a 3-button phone in immersive mode the bar is NOT on screen, yet
## the house banner and every HUD stood on its full 48 dp (getInsetsIgnoringVisibility), leaving a ~56 dp dark strip
## under the banner. Now the navigation bar's full height is reserved only while the bar is really SHOWN
## (getInsets(navigationBars) > 0: not immersive, or an OEM layer keeping it up). Hidden: the gesture handle's strip
## (getInsets(mandatorySystemGestures): ~16 dp with gesture navigation, 0 with 3 buttons), at least MIN_EDGE_DP — never
## more than the bar itself. Backgrounds still run to the screen edge; only touchables keep that margin.
## Simulation: simulate(screen, insets, status, nav_mode) / CODDY_SAFE_SIM "WxH:l,t,r,b:status:mode" — mode "gesture" or
## "buttons" = the bar HIDDEN (immersive) and its insets are the bar's (bottom in portrait, the side in landscape); ""
## (default) = the bar shown, as before.

const REFRESH_MS := 1000
## WindowInsets.Type.navigationBars() (android.view.WindowInsets.Type, API 30+: 1 << 1).
const TYPE_NAVIGATION_BARS := 2
## WindowInsets.Type.statusBars() (API 30+: 1 << 0).
const TYPE_STATUS_BARS := 1
## WindowInsets.Type.mandatorySystemGestures() (API 30+: 1 << 5): the gesture-navigation handle area.
const TYPE_MANDATORY_GESTURES := 32
## A hidden bar still leaves touchables this far off the screen edge (dp).
const MIN_EDGE_DP := 8.0

## Tests: forced navigation-bar insets in physical px (left, top, right, bottom); x < 0 = read the device.
static var nav_override := Vector4(-1, -1, -1, -1)
static var _nav := Vector4.ZERO
static var _nav_ms := -1000000
static var _status := 0.0
static var _rx: RegEx = null
## Simulated phone (screen px) and its insets (left, top, right, bottom; top = status bar / cutout). x <= 0 = off.
static var sim_screen := Vector2i.ZERO
static var sim_insets := Vector4.ZERO
## Simulated status-bar height (px) apart from the top inset (the cutout); < 0 = the top inset is both.
static var sim_status := -1.0
## Simulated navigation: "" = the bar shown (its insets reserved in full), "gesture" / "buttons" = hidden (immersive).
static var sim_nav_mode := ""
static var _sim_env_read := false


## Desktop frames / tests: act like a `screen`-sized phone with these bar / cutout insets (physical px). ZERO = off.
## `status` >= 0: the status bar's height on its own (the top inset is then just the cutout, as rect() on a phone).
static func simulate(screen: Vector2i, insets: Vector4, status := -1.0, nav_mode := "") -> void:
	_sim_env_read = true
	sim_screen = screen
	sim_insets = insets
	sim_status = status
	sim_nav_mode = nav_mode


static func simulating() -> bool:
	if not _sim_env_read:
		_sim_env_read = true
		var env := OS.get_environment("CODDY_SAFE_SIM")   # "1080x2400:0,110,0,126" or "1080x1920:0,0,0,144:72"
		if env != "" and ":" in env and "x" in env:
			var wh := env.get_slice(":", 0).split("x")
			var ins := env.get_slice(":", 1).split(",")
			if wh.size() == 2 and ins.size() == 4:
				sim_screen = Vector2i(int(wh[0]), int(wh[1]))
				sim_insets = Vector4(float(ins[0]), float(ins[1]), float(ins[2]), float(ins[3]))
				if env.get_slice_count(":") > 2 and env.get_slice(":", 2).is_valid_float():
					sim_status = float(env.get_slice(":", 2))
				if env.get_slice_count(":") > 3:
					sim_nav_mode = env.get_slice(":", 3)
	return sim_screen.x > 0 and sim_screen.y > 0


## Drop-in for DisplayServer.get_display_safe_area(): the engine safe area shrunk so it also clears the navigation bar.
## Status bar included at the top (visible or not), like the navigation bar at its edge.
static func rect() -> Rect2i:
	if simulating():
		var i := sim_edges()
		var top := maxf(i.y, status_top())   # the cutout or the status bar, whichever reaches lower
		return Rect2i(int(i.x), int(top), sim_screen.x - int(i.x + i.z), sim_screen.y - int(top + i.w))
	var safe := DisplayServer.get_display_safe_area()
	var nav := nav_insets()
	return combine(safe, DisplayServer.screen_get_size(), Vector4(nav.x, status_top(), nav.z, nav.w))


## The simulated insets (physical px) after the navigation-bar rule: with a HIDDEN bar (sim_nav_mode "gesture" /
## "buttons") the bar's side — the bottom in portrait, the deeper side in landscape — keeps only what reserve() keeps.
static func sim_edges() -> Vector4:
	var i := sim_insets
	if sim_nav_mode == "":
		return i
	var min_px := MIN_EDGE_DP * minf(sim_screen.x, sim_screen.y) / 360.0   # a 360 dp-wide phone
	var g := 1.0 if sim_nav_mode == "gesture" else 0.0   # gesture navigation: the handle strip = the bar's own inset
	if i.w > 0.0 or sim_screen.y >= sim_screen.x:      # the bar at the bottom (portrait; gesture nav in landscape)
		i.w = reserve_side(i.w, 0.0, i.w * g, min_px)
	elif i.z >= i.x:                                   # 3-button bar at the side in landscape
		i.z = reserve_side(i.z, 0.0, i.z * g, min_px)
	else:
		i.x = reserve_side(i.x, 0.0, i.x * g, min_px)
	return i


## Pure: what one edge reserves for the navigation bar (physical px). `bar` = its height visible or not, `shown` = its
## height while really on screen (0 hidden), `gesture` = the gesture handle's strip. Shown → the bar. Hidden → the
## handle strip, at least `min_px`, never more than the bar. No bar → 0.
static func reserve_side(bar: float, shown: float, gesture: float, min_px: float) -> float:
	if bar <= 0.0:
		return 0.0
	if shown > 0.0:
		return maxf(shown, minf(gesture, bar))
	return minf(bar, maxf(gesture, min_px))


static func reserve(bar: Vector4, shown: Vector4, gesture: Vector4, min_px: float) -> Vector4:
	return Vector4(reserve_side(bar.x, shown.x, gesture.x, min_px), 0.0,
		reserve_side(bar.z, shown.z, gesture.z, min_px), reserve_side(bar.w, shown.w, gesture.w, min_px))


## Safe-area gaps in canvas px (left, top, right, bottom) for a canvas of `canvas` px covering the whole screen.
static func edges(canvas: Vector2) -> Vector4:
	return to_canvas(rect(), screen_size(), canvas)


## The physical screen size rect() is measured in (the simulated phone while simulating).
static func screen_size() -> Vector2i:
	return sim_screen if simulating() else DisplayServer.screen_get_size()


## Gaps (canvas px: left, top, right, bottom) an edge-anchored OVERLAY keeps its buttons / text inside: edges() with the
## status bar's height reserved at the top too (visible or not). Zero off a phone (desktop windows, tests) unless
## simulating — the desktop engine safe area is the monitor's, not the window's.
static func overlay_edges(canvas: Vector2) -> Vector4:
	if not simulating() and not OS.has_feature("mobile"):
		return Vector4.ZERO
	var screen := screen_size()
	var e := to_canvas(rect(), screen, canvas)
	if screen.y > 0:
		e.y = maxf(e.y, status_top() * canvas.y / float(screen.y))
	return e


## Pure: the gaps `e` (overlay_edges of a `canvas`) left inside a control whose global rect is `r` — what the control
## itself must still keep free (0 on a side the control already stops short of the unsafe band).
static func inner_gaps(e: Vector4, r: Rect2, canvas: Vector2) -> Vector4:
	return Vector4(maxf(0.0, e.x - r.position.x), maxf(0.0, e.y - r.position.y),
		maxf(0.0, e.z - (canvas.x - r.end.x)), maxf(0.0, e.w - (canvas.y - r.end.y)))


## Status-bar height in physical px, visible or not (Android; the simulated top inset while simulating; 0 elsewhere —
## iOS's engine safe area already holds it).
static func status_top() -> float:
	if simulating():
		return sim_status if sim_status >= 0.0 else sim_insets.y
	if not OS.has_feature("android"):
		return 0.0
	nav_insets()   # one cached read fills both
	return _status


## Bottom gap only (canvas px): what a bottom bar / the house banner must keep free under itself.
static func bottom(canvas: Vector2) -> float:
	return edges(canvas).w


## Pure: the safe rect `safe` (screen px) of a `screen`-sized display, pushed in so that it also clears `nav`
## (physical px from the left, top, right, bottom edges). A side the engine already insets further is kept as it is.
## No navigation bar → `safe` unchanged. An empty engine rect with a nav bar → the full screen minus the bar.
static func combine(safe: Rect2i, screen: Vector2i, nav: Vector4) -> Rect2i:
	if nav.x <= 0 and nav.y <= 0 and nav.z <= 0 and nav.w <= 0:
		return safe
	if screen.x <= 0 or screen.y <= 0:
		return safe
	if safe.size.x <= 0 or safe.size.y <= 0:
		safe = Rect2i(Vector2i.ZERO, screen)
	var l := maxi(safe.position.x, int(ceil(nav.x)))
	var t := maxi(safe.position.y, int(ceil(nav.y)))
	var r := mini(safe.end.x, screen.x - int(ceil(nav.z)))
	var b := mini(safe.end.y, screen.y - int(ceil(nav.w)))
	if r <= l or b <= t:
		return safe   # nonsense insets (bigger than the screen): keep the engine value
	return Rect2i(l, t, r - l, b - t)


## Pure: gaps (canvas px: left, top, right, bottom) of `safe` inside a `screen`-sized display drawn on a `canvas`-sized
## canvas (stretch mode canvas_items / expand: one scale per axis). Empty rect or screen → zero gaps.
static func to_canvas(safe: Rect2i, screen: Vector2i, canvas: Vector2) -> Vector4:
	if safe.size.x <= 0 or safe.size.y <= 0 or screen.x <= 0 or screen.y <= 0:
		return Vector4.ZERO
	var k := canvas / Vector2(screen)
	return Vector4(maxf(0.0, safe.position.x * k.x), maxf(0.0, safe.position.y * k.y),
		maxf(0.0, (screen.x - safe.end.x) * k.x), maxf(0.0, (screen.y - safe.end.y) * k.y))


## Navigation-bar insets in physical px (left, top = 0, right, bottom) the UI keeps off: the whole bar while it is shown,
## the gesture strip / MIN_EDGE_DP while immersive hides it (reserve()). Zero off Android.
static func nav_insets() -> Vector4:
	if nav_override.x >= 0.0:
		return nav_override
	if not OS.has_feature("android"):
		return Vector4.ZERO
	var now := Time.get_ticks_msec()
	if now - _nav_ms < REFRESH_MS:
		return _nav
	_nav_ms = now
	_nav = _read_android()
	return _nav


## Forget the cached insets (rotation, navigation-mode switch): the next call reads the device again.
static func refresh() -> void:
	_nav_ms = -1000000


static func _read_android() -> Vector4:
	if not Engine.has_singleton("AndroidRuntime"):
		return _nav
	var rt = Engine.get_singleton("AndroidRuntime")
	var act = rt.getActivity()
	if act == null:
		return _nav
	var win = act.getWindow()
	if win == null:
		return _nav
	var decor = win.getDecorView()
	if decor == null:
		return _nav
	var wi = decor.getRootWindowInsets()
	if wi == null:
		return _nav   # not attached yet: keep the last value, retry after REFRESH_MS
	if wi.has_java_method("getInsetsIgnoringVisibility"):
		var ins = wi.getInsetsIgnoringVisibility(TYPE_NAVIGATION_BARS)
		if ins == null:
			return _nav
		var st = wi.getInsetsIgnoringVisibility(TYPE_STATUS_BARS)
		if st != null:
			_status = parse_insets(str(st.toString())).y
		var v := parse_insets(str(ins.toString()))
		var shown := Vector4.ZERO
		var gesture := Vector4.ZERO
		if wi.has_java_method("getInsets"):
			var sh = wi.getInsets(TYPE_NAVIGATION_BARS)
			if sh != null:
				shown = parse_insets(str(sh.toString()))
			var gs = wi.getInsets(TYPE_MANDATORY_GESTURES)
			if gs != null:
				gesture = parse_insets(str(gs.toString()))
		return reserve(v, shown, gesture, _min_px())
	# API 24–29: stable insets = system bars as if shown (the top one is the status bar: kept apart in _status);
	# system-window insets = the bars really on screen now (0 while immersive hides them).
	_status = float(wi.getStableInsetTop())
	var bar := Vector4(float(wi.getStableInsetLeft()), 0.0, float(wi.getStableInsetRight()), float(wi.getStableInsetBottom()))
	var now := Vector4(float(wi.getSystemWindowInsetLeft()), 0.0, float(wi.getSystemWindowInsetRight()),
		float(wi.getSystemWindowInsetBottom()))
	return reserve(bar, now, Vector4.ZERO, _min_px())


## MIN_EDGE_DP in physical px on this device.
static func _min_px() -> float:
	var dpi := float(DisplayServer.screen_get_dpi())
	return MIN_EDGE_DP * (dpi / 160.0 if dpi > 0.0 else 2.75)


## Pure: "Insets{left=0, top=63, right=0, bottom=126}" (android.graphics.Insets.toString) → Vector4(l, t, r, b).
static func parse_insets(s: String) -> Vector4:
	if _rx == null:
		_rx = RegEx.create_from_string("(left|top|right|bottom)=(-?\\d+)")
	var v := Vector4.ZERO
	for m in _rx.search_all(s):
		var n := maxf(0.0, float(m.get_string(2)))
		match m.get_string(1):
			"left": v.x = n
			"top": v.y = n
			"right": v.z = n
			"bottom": v.w = n
	return v
