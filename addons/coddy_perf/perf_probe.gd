extends RefCounted
## Perf probe (W63 measurement, shared module): a frame-time histogram (p50 / p95 / p99, frames ≥ 100 ms = visible
## hitches) plus the engine monitors (script process time, physics time, draw calls, primitives, nodes, static memory),
## summarised once per window as one "PERFX" line — logcat on a debug / QA phone build, stdout in a desktop bot run.
## The cheap per-frame part is one float into a preallocated ring (no allocation per frame).
## Source of truth: games/tools/perf (copied into each game as addons/coddy_perf by sync.sh).
##
##   var probe := PerfProbe.new()
##   func _process(delta):
##       var line := probe.frame(delta)   # "" until a window is full
##       if line != "" and PerfProbe.enabled(): print(line)

const WINDOW_S := 10.0
const HITCH_MS := 100.0
const RING := 1200 # 10 s at 120 FPS

var _ms := PackedFloat32Array()
var _n := 0
var _t := 0.0
var last := {}


func _init() -> void:
	_ms.resize(RING)


## On a phone only in debug / QA builds; anywhere with `--perfprobe` on the command line (desktop bot runs).
static func enabled() -> bool:
	if "--perfprobe" in OS.get_cmdline_user_args():
		return true
	return OS.is_debug_build() and OS.has_feature("mobile")


## One frame. Returns the summary line when a window is complete (else "").
func frame(delta: float) -> String:
	if _n < RING:
		_ms[_n] = delta * 1000.0
		_n += 1
	_t += delta
	if _t < WINDOW_S:
		return ""
	var line := summary()
	_n = 0
	_t = 0.0
	return line


## The current window as a dictionary (also kept in `last`) and its PERFX line.
func summary() -> String:
	var a := _ms.slice(0, _n)
	a.sort()
	var hitches := 0
	for v in a:
		if v >= HITCH_MS:
			hitches += 1
	last = {
		"frames": _n,
		"fps": snappedf(float(_n) / maxf(_t, 0.001), 0.1),
		"p50": _pct(a, 0.50), "p95": _pct(a, 0.95), "p99": _pct(a, 0.99),
		"hitches": hitches,
		"process_ms": snappedf(Performance.get_monitor(Performance.TIME_PROCESS) * 1000.0, 0.01),
		"physics_ms": snappedf(Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS) * 1000.0, 0.01),
		"draw_calls": int(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)),
		"primitives": int(Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME)),
		"nodes": int(Performance.get_monitor(Performance.OBJECT_NODE_COUNT)),
		"static_mb": int(Performance.get_monitor(Performance.MEMORY_STATIC) / 1048576.0),
	}
	return "PERFX " + JSON.stringify(last)


static func _pct(sorted: PackedFloat32Array, q: float) -> float:
	if sorted.is_empty():
		return 0.0
	return snappedf(sorted[clampi(int(ceil(q * sorted.size())) - 1, 0, sorted.size() - 1)], 0.1)
