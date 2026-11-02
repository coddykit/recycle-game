extends RefCounted
## Safe-area LAYOUT probe for headless tests (Patron 2026-10-28, S1 banner-rollout audit: on a phone with the status bar,
## a display cutout and the 3-button navigation bar drawn on, the top HUD of Smuggler / Snow Empire / Parts Rush / Sky
## Tower sat under the status bar, Disaster Command's right sheet under the side navigation bar and Train Baron's
## "Tutorial skipped" toast on its tab labels).
##
## UiSafeArea.simulate() makes every UiSafeArea call act like a phone; set_phone() also sizes the root window so the
## canvas has that phone's aspect (headless works). scan() then lists every visible text / button whose on-screen rect
## crosses a system-bar band — UiSafeArea.overlay_edges(): the status bar (visible or not: immersive mode brings it back
## over the game on an edge swipe), the cutout and the navigation bar. Backgrounds (panels, scrims, the 3D view) may
## run to the screen edge; only Labels with text, RichTextLabels, LineEdits and buttons are checked.
##     const SafeLayout := preload("res://addons/coddy_ui/tests/safe_layout.gd")
##     for p in SafeLayout.phones(false): SafeLayout.set_phone(root, p); …build the screen…; SafeLayout.scan(root, {"skip": [...]})
## Pure test code: nothing here runs in a game.

const UiSafeArea := preload("res://addons/coddy_ui/ui_safe_area.gd")
const TOL := 2.0


## Six phones per orientation (10-30: + gesture nav and 3-button nav with the bar HIDDEN by immersive mode, "nav" key —
## the hidden bar's band shrinks to the gesture strip / 8 dp, and the owner's "huge gap" check runs on them).
## Four aspects per orientation (physical px; insets = left, top, right, bottom; status = the status bar's height). The
## top inset is the CUTOUT only — what the engine reports on a phone in immersive mode — and the status bar is separate
## (UiSafeArea.simulate's 3rd value; UiSafeArea.rect() reserves both since 10-28): a HUD that reads the engine value, or
## reads the safe area on a real phone only, fails here. Portrait: a punch-hole phone (cutout = status bar), a 16:9
## phone without a cutout, a tall phone with a shallow notch, a tablet; the 3-button bar at the bottom. Landscape (sensor
## landscape turns both ways): the cutout on one side and the 3-button bar on the other — right and left —, a tablet.
static func phones(landscape: bool) -> Array:
	if not landscape:
		return [
			{"name": "20:9 phone", "screen": Vector2i(1080, 2400), "insets": Vector4(0, 110, 0, 126), "status": 110.0},
			{"name": "16:9 phone, no cutout", "screen": Vector2i(1080, 1920), "insets": Vector4(0, 0, 0, 144), "status": 72.0},
			{"name": "22:9 tall phone", "screen": Vector2i(1080, 2640), "insets": Vector4(0, 80, 0, 132), "status": 132.0},
			{"name": "16:10 tablet", "screen": Vector2i(1600, 2560), "insets": Vector4(0, 0, 0, 96), "status": 50.0},
			{"name": "20:9 gesture nav, immersive", "screen": Vector2i(1080, 2400), "insets": Vector4(0, 110, 0, 48), "status": 110.0, "nav": "gesture"},
			{"name": "16:9 3-button, immersive", "screen": Vector2i(1080, 1920), "insets": Vector4(0, 0, 0, 144), "status": 72.0, "nav": "buttons"},
		]
	return [
		{"name": "20:9 phone, bar right", "screen": Vector2i(2400, 1080), "insets": Vector4(110, 0, 126, 0), "status": 63.0},
		{"name": "16:9 phone, bar left", "screen": Vector2i(1920, 1080), "insets": Vector4(144, 0, 0, 0), "status": 63.0},
		{"name": "22:9 tall phone, bar right", "screen": Vector2i(2640, 1080), "insets": Vector4(132, 0, 132, 0), "status": 63.0},
		{"name": "16:10 tablet", "screen": Vector2i(2560, 1600), "insets": Vector4(0, 0, 0, 96), "status": 50.0},
		{"name": "20:9 gesture nav, immersive", "screen": Vector2i(2400, 1080), "insets": Vector4(110, 0, 0, 48), "status": 63.0, "nav": "gesture"},
		{"name": "20:9 3-button right, immersive", "screen": Vector2i(2400, 1080), "insets": Vector4(110, 0, 144, 0), "status": 63.0, "nav": "buttons"},
	]


## Act like `phone` (an entry of phones()): simulated insets + the root window at the phone's pixel size.
static func set_phone(root: Window, phone: Dictionary) -> void:
	UiSafeArea.simulate(phone.screen, phone.insets, float(phone.get("status", -1.0)), str(phone.get("nav", "")))
	UiSafeArea.refresh()
	root.size = phone.screen


static func clear(root: Window, size := Vector2i(1280, 720)) -> void:
	UiSafeArea.simulate(Vector2i.ZERO, Vector4.ZERO)
	root.size = size


## The canvas rect UI text and buttons must stay inside (canvas px).
static func safe_rect(root: Window) -> Rect2:
	var canvas := root.get_visible_rect().size
	var e := UiSafeArea.overlay_edges(canvas)
	return Rect2(e.x, e.y, canvas.x - e.x - e.z, canvas.y - e.y - e.w)


## On-screen rect of a control (canvas layers, transforms and clipping ancestors applied); empty = not on screen.
static func screen_rect(c: Control) -> Rect2:
	var r := c.get_global_transform_with_canvas() * Rect2(Vector2.ZERO, c.size)
	var p := c.get_parent()
	while p != null:
		if p is Control and ((p as Control).clip_contents or p is ScrollContainer):
			var pc := p as Control
			r = r.intersection(pc.get_global_transform_with_canvas() * Rect2(Vector2.ZERO, pc.size))
		elif p is SubViewport:
			return Rect2()
		p = p.get_parent()
	var canvas := c.get_viewport().get_visible_rect()
	return r.intersection(canvas)


## Visible to the player: visible in the tree, every CanvasLayer above it shown, not faded out.
static func shown(c: CanvasItem) -> bool:
	if not c.is_visible_in_tree():
		return false
	var a := 1.0
	var n: Node = c
	while n != null:
		if n is CanvasLayer and not (n as CanvasLayer).visible:
			return false
		if n is CanvasItem:
			a *= (n as CanvasItem).modulate.a * ((n as CanvasItem).self_modulate.a if n == c else 1.0)
		n = n.get_parent()
	return a > 0.05


static func _checked(c: Control) -> bool:
	if c is BaseButton or c is LineEdit:
		return true
	if c is Label:
		return (c as Label).text.strip_edges() != ""
	if c is RichTextLabel:
		return (c as RichTextLabel).get_parsed_text().strip_edges() != ""
	return false


static func _skipped(n: Node, skip: Array) -> bool:
	var p := n
	while p != null:
		if skip.has(p) or str(p.name) == "HouseBanner" or p.has_meta("safe_layout_skip"):
			return true   # the house banner keeps itself off the bars (kit CoddyBanner tests)
		p = p.get_parent()
	return false


## Every shown text / button under `from` (default: the whole tree) that crosses a system-bar band by more than TOL px.
## opts: "skip": [Node] subtrees not checked (labels that follow the 3D map, …), "edges": "ltrb" subset to check.
## Returns ["<edge> <path> '<text>' rect … safe …", …].
static func scan(root: Window, opts := {}) -> Array:
	var safe := safe_rect(root)
	var skip: Array = opts.get("skip", [])
	var edges: String = opts.get("edges", "ltrb")
	var out: Array = []
	_walk(opts.get("from", root), safe, skip, edges, out)
	return out


static func _walk(n: Node, safe: Rect2, skip: Array, edges: String, out: Array) -> void:
	if skip.has(n) or str(n.name) == "HouseBanner" or n.has_meta("safe_layout_skip") or n is SubViewport:
		return
	if n is CanvasItem and not (n as CanvasItem).visible:
		return
	if n is CanvasLayer and not (n as CanvasLayer).visible:
		return
	if n is Control and _checked(n) and shown(n):
		var r := screen_rect(n)
		if r.has_area():
			var bad := ""
			if "t" in edges and r.position.y < safe.position.y - TOL:
				bad += "top %.0f px " % (safe.position.y - r.position.y)
			if "b" in edges and r.end.y > safe.end.y + TOL:
				bad += "bottom %.0f px " % (r.end.y - safe.end.y)
			if "l" in edges and r.position.x < safe.position.x - TOL:
				bad += "left %.0f px " % (safe.position.x - r.position.x)
			if "r" in edges and r.end.x > safe.end.x + TOL:
				bad += "right %.0f px " % (r.end.x - safe.end.x)
			if bad != "":
				out.append("%s%s '%s' rect %s safe %s" % [bad, _short_path(n), _text(n), r, safe])
	for c in n.get_children():
		_walk(c, safe, skip, edges, out)


static func _text(c: Control) -> String:
	var t := ""
	if "text" in c:
		t = str(c.get("text"))
	elif c is RichTextLabel:
		t = (c as RichTextLabel).get_parsed_text()
	t = t.replace("\n", " ").strip_edges()
	return t.substr(0, 32)


static func _short_path(n: Node) -> String:
	var parts: PackedStringArray = []
	var p := n
	while p != null and parts.size() < 4 and not (p is Window):
		parts.insert(0, str(p.name))
		p = p.get_parent()
	return "/".join(parts)


## Two shown controls overlap on screen by more than TOL px each way (a toast on a tab bar).
static func overlaps(a: Control, b: Control) -> bool:
	if a == null or b == null or not shown(a) or not shown(b):
		return false
	var i := screen_rect(a).intersection(screen_rect(b))
	return i.size.x > TOL and i.size.y > TOL


## Owner 10-30: with the navigation bar HIDDEN (phone "nav" set) the bottom / side band is the gesture strip or 8 dp, not
## the bar — "" when right, else what is wrong. Canvas px.
static func hidden_band_error(root: Window, phone: Dictionary) -> String:
	if str(phone.get("nav", "")) == "":
		return ""
	var canvas := root.get_visible_rect().size
	var e := UiSafeArea.edges(canvas)
	var k := canvas.y / float(phone.screen.y)
	var ins: Vector4 = phone.insets
	var keep := UiSafeArea.MIN_EDGE_DP * minf(phone.screen.x, phone.screen.y) / 360.0
	if str(phone.nav) == "gesture":
		keep = maxf(keep, ins.w)
	var limit := (keep + 1.0) * k
	if ins.w > 0.0 and e.w > limit:
		return "bottom band %.0f > %.0f" % [e.w, limit]
	if ins.w <= 0.0 and ins.z > 0.0 and e.z > limit:   # the 3-button bar on the right in landscape
		return "side band %.0f > %.0f" % [e.z, limit]
	return ""


## Owner 10-30: a background (panel / banner / scrim) that sits at a screen edge must REACH it — no empty strip between
## it and the edge. `c`'s on-screen rect, its panel style's expand margins counted. "" when right.
static func reaches_bottom(root: Window, c: Control) -> String:
	if c == null or not is_instance_valid(c) or not shown(c):
		return ""
	var r := screen_rect(c)
	var extra := 0.0
	var sb := c.get_theme_stylebox("panel") if c is PanelContainer or c is Panel else null
	if sb is StyleBoxFlat:
		extra = (sb as StyleBoxFlat).expand_margin_bottom
	var canvas := root.get_visible_rect().size
	if r.end.y + extra < canvas.y - TOL:
		return "%s stops %.0f px above the screen bottom" % [_short_path(c), canvas.y - r.end.y - extra]
	return ""
