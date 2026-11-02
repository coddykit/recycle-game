class_name DeviceTier
extends RefCounted
## Graphics-quality device classifier (owner rule 2026-10-14).
##
## Phones and tablets NEVER default to High: a strong device gets Medium, a weak or
## unknown device gets Low. Desktop / Steam keeps High. `classify()` is a pure function
## of a plain Dictionary so it can be unit-tested with fake inputs; `detect()` fills that
## Dictionary from the engine.

const LOW := "low"
const MEDIUM := "medium"
const HIGH := "high"
const TIERS := [LOW, MEDIUM, HIGH]

const GIB := 1073741824
const PerfDevice := preload("res://addons/coddy_perf/perf_device.gd")

## Android "strong" needs >= 6 GB RAM. Phones sold as 6 GB report only ~5.5-5.8 GiB of
## MemTotal (kernel/firmware carve-outs), so the check uses 5.2 GiB to mean "marketed 6 GB".
const ANDROID_MIN_RAM_BYTES := 5325 * 1048576 # 5.2 GiB fleet-wide (2026-10-14)
## Android "strong" needs >= 8 CPU cores (flagship / upper-mid SoCs).
const ANDROID_MIN_CORES := 8
## iOS "strong" = >= 4 GB RAM. iPhones with 4 GB report ~3.6-3.9 GiB, so 3.4 GiB.
const IOS_MIN_RAM_BYTES := 3482 * 1048576 # 3.4 GiB fleet-wide
## GPU families that stay Low even on a big-RAM Android phone (matched on lower-cased
## "<adapter> <vendor>"): Mali-G5x / G6x (two digits, so Mali-G610/G710 are NOT matched),
## every Mali-T (Midgard) part, any PowerVR, Adreno 5xx and 6[0-1]x.
const ANDROID_WEAK_GPU_PATTERNS := [
	"mali-?g[56]\\d\\b",
	"mali-?t\\d{3}",
	"powervr",
	"imagination",
	"adreno\\D{0,8}(5\\d\\d|6[01]\\d)\\b",
]


## inputs: platform ("android"|"ios"|"desktop"|"web"|...), ram_bytes, cores, adapter, vendor.
## Android is classified on RAM, cores and GPU name only: the fleet exports Android with gl_compatibility
## (Vulkan crashes at launch on many phones), so the renderer in use says nothing about the device.
static func classify(i: Dictionary) -> String:
	if _flagship(i):
		return HIGH   # owner 10-23: a flagship phone / tablet starts on High
	match str(i.get("platform", "")):
		"desktop":
			return HIGH
		"ios":
			return MEDIUM if int(i.get("ram_bytes", 0)) >= IOS_MIN_RAM_BYTES else LOW
		"android":
			return _android(i)
		_:
			return LOW   # web or anything unrecognised: unknown -> Low


static func _android(i: Dictionary) -> String:
	if int(i.get("ram_bytes", 0)) < ANDROID_MIN_RAM_BYTES:
		return LOW
	if int(i.get("cores", 0)) < ANDROID_MIN_CORES:
		return LOW
	var gpu := (str(i.get("adapter", "")) + " " + str(i.get("vendor", ""))).strip_edges().to_lower()
	if gpu == "":
		return LOW
	for pattern in ANDROID_WEAK_GPU_PATTERNS:
		var re := RegEx.new()
		re.compile(pattern)
		if re.search(gpu) != null:
			return LOW
	return MEDIUM


static func platform_name() -> String:
	match OS.get_name():
		"Android":
			return "android"
		"iOS":
			return "ios"
		"Windows", "macOS", "Linux", "FreeBSD", "NetBSD", "OpenBSD", "BSD":
			return "desktop"
	return OS.get_name().to_lower()


static func detect() -> Dictionary:
	return {
		"platform": platform_name(),
		"ram_bytes": ram_bytes(int(OS.get_memory_info().get("physical", 0))),
		"cores": OS.get_processor_count(),
		"adapter": RenderingServer.get_video_adapter_name(),
		"vendor": RenderingServer.get_video_adapter_vendor(),
	}


## Godot's physical RAM figure, or MemTotal from /proc/meminfo (coddy_perf PerfDevice.meminfo_mb) when Godot says
## ≤ 0 — Godot 4.7.2 returns −1 on Android, which made EVERY Android phone "unknown" → Low (S25 Ultra, 2026-10-23).
static func ram_bytes(physical: int, meminfo_path := "/proc/meminfo") -> int:
	if physical > 0:
		return physical
	var mb := PerfDevice.meminfo_mb(meminfo_path)
	return mb * 1048576 if mb > 0 else 0


## Default graphics tier for this device (a `--gfx-tier=low|medium|high` user arg overrides it
## for QA and screenshots).
static func default_quality() -> String:
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--gfx-tier="):
			var t := a.get_slice("=", 1)
			if t in TIERS:
				return t
	return classify(detect())


## Owner 10-23 (coddy_perf PerfDevice.classify, 17d6060b6d): a flagship handheld starts on High — Android RAM ≥ 7000 MB,
## ≥ 8 cores and a flagship GPU (Adreno 8xx / 730+, Immortalis, Mali-G720 / G725 / G9xx, Xclipse 9xx); iOS RAM ≥ 7000 MB.
static func _flagship(d: Dictionary) -> bool:
	var plat := str(d.get("platform", "")).to_lower()
	if plat != "android" and plat != "ios":
		return false
	var ram := int(d.get("ram_bytes", -1))
	return PerfDevice.classify({"handheld": true, "os": "Android" if plat == "android" else "iOS",
		"ram_mb": roundi(ram / 1048576.0) if ram > 0 else -1, "cores": int(d.get("cores", 0)),
		"gpu": str(d.get("adapter", ""))}) == "flagship"
