extends RefCounted
## Device class → starting graphics tier (games/CLAUDE.md §10, W60-B1 shared module). Owner decision 2026-10-23 (replaces
## the 2026-10-14 "a phone never starts on High"): a FLAGSHIP phone starts on High, a strong one on Medium, a weak or
## unknown one on Low; desktops keep High. PerfGuard stays the safety net: a phone that is really slow (10 s under
## 25 FPS, uncapped) is moved one step down.
##   Android flagship = RAM ≥ 7000 MB (an "8 GB" phone reports ~7.2–7.6 GB) AND ≥ 8 CPU cores AND a GPU on the flagship
##                      list: Adreno 8xx and Adreno 730+ (730 / 732 / 735 / 740 / 750 …), any Mali Immortalis
##                      (G715 / G720 / G925 …), Mali-G720 / G725 / G9xx, Samsung Xclipse 9xx. Adreno 725 / 710,
##                      Mali-G715 / G710 / G610 (without Immortalis) stay strong.
##   Android strong   = RAM ≥ 5.2 GiB AND ≥ 8 CPU cores AND a GPU outside the known-weak families (Mali-G5x / G6x /
##                      T8xx, PowerVR, Adreno 5xx / 600–619). The renderer is NOT a criterion: the fleet exports Android
##                      with gl_compatibility (Vulkan crash rule), so a "Vulkan running?" test made EVERY phone Low (the
##                      W60 audit's trap in corrupt / trading-sim / chaiwala / disaster).
##   iOS flagship     = RAM ≥ 7000 MB (8 GB: iPhone 15 Pro / 16 / 16 Pro / 17 class, M-series iPads). Conservative on
##                      purpose: RAM is the only figure iOS gives reliably (the GPU name says "Apple A17 Pro GPU" etc.),
##                      and every 8 GB iPhone has a current-generation GPU.
##   iOS strong       = RAM ≥ 3.4 GiB.
##   anything else on a handheld (no RAM figure, no GPU name, another OS) = unknown → Low.
## RAM is the OS figure, below the marketing size (a "6 GB" phone reports 5.4–5.8 GB, a "4 GB" iPhone ~3.7 GB, an
## "8 GB" Android 7.2–7.6 GB). Pure maths in classify(); probe() reads the running device. Source of truth:
## games/tools/perf (copied into each game as addons/coddy_perf — never edit a game's copy, run games/tools/perf/sync.sh).

const ANDROID_MIN_RAM_MB := 5325
const ANDROID_MIN_CORES := 8
const IOS_MIN_RAM_MB := 3482
const FLAGSHIP_MIN_RAM_MB := 7000
## Weak GPU families (lower-case adapter name): Mali-G5x / G6x (two digits, so Mali-G610 / G710 are not caught),
## Mali-T8xx, any PowerVR, Adreno 5xx and Adreno 600–619.
const WEAK_GPU := "mali-?g[56][0-9](?![0-9])|mali-?t8[0-9][0-9]|powervr|adreno[^0-9]*(5[0-9][0-9]|6[01][0-9])(?![0-9])"
## Flagship GPU families (lower-case adapter name): Adreno 8xx and 730–799, any Mali Immortalis, Mali-G720 / G725 / G9xx
## (G715 only as Immortalis), Samsung Xclipse 9xx (its GL driver reports "ANGLE (Samsung Xclipse 940) on Vulkan …").
const FLAGSHIP_GPU := "adreno[^0-9]*(8[0-9][0-9]|7[3-9][0-9])(?![0-9])|immortalis|mali-?g(72[05]|9[0-9][0-9])(?![0-9])|xclipse[^0-9]*9[0-9][0-9](?![0-9])"
const LOW := 0
const MEDIUM := 1
const HIGH := 2
const NAMES := ["low", "medium", "high"]

static var _weak_re: RegEx
static var _flagship_re: RegEx


## A phone or tablet (the devices the flagship / strong / weak classes apply to).
static func handheld() -> bool:
	return OS.get_name() in ["Android", "iOS"] or OS.has_feature("mobile")


## The running device: {handheld, os, ram_mb, cores, driver, method, gpu}. `is_handheld` lets a game pass its own test
## (e.g. a faked phone window). Debug builds can fake a device: `--device os=Android,ram=7600,cores=8,gpu=Adreno 730`.
static func probe(is_handheld: Variant = null) -> Dictionary:
	var mem := OS.get_memory_info()
	var phys := int(mem.get("physical", -1))
	var info := {
		"handheld": handheld() if is_handheld == null else bool(is_handheld),
		"os": OS.get_name(),
		"ram_mb": int(phys / 1048576) if phys > 0 else meminfo_mb(),
		"cores": OS.get_processor_count(),
		"driver": RenderingServer.get_current_rendering_driver_name(),
		"method": RenderingServer.get_current_rendering_method(),
		"gpu": RenderingServer.get_video_adapter_name(),
	}
	var ua := OS.get_cmdline_user_args()
	var i := ua.find("--device")
	if OS.is_debug_build() and i >= 0 and i + 1 < ua.size():
		for kv in ua[i + 1].split(","):
			var p := kv.split("=", true, 1)
			if p.size() == 2:
				var k: String = {"ram": "ram_mb"}.get(p[0], p[0])
				info[k] = int(p[1]) if k in ["ram_mb", "cores"] else p[1]
		info["handheld"] = true
	return info


## Total RAM from /proc/meminfo ("MemTotal: <kB>"), −1 if unreadable. Godot 4.7.2 on Android returns −1 for
## OS.get_memory_info().physical, and its FileAccess cannot open /proc/meminfo there (open error 1) — emulator proof
## 2026-10-23: physical −1, `cat /proc/meminfo` → MemTotal 6068684 kB. Without this every Android phone, a Galaxy S25
## Ultra included, was "unknown" → Low. Reads the file where FileAccess can, else one `cat` (once per launch).
static func meminfo_mb(path: String = "/proc/meminfo") -> int:
	var text := ""
	var f := FileAccess.open(path, FileAccess.READ)
	if f != null:
		for _i in 8: # proc files report length 0: line by line
			text += f.get_line() + "\n"
			if f.eof_reached():
				break
	if not text.contains("MemTotal:") and OS.get_name() in ["Android", "Linux"]:
		var out := []
		if OS.execute("cat", [path], out) == 0 and out.size() > 0:
			text = str(out[0])
	return parse_meminfo_mb(text)


## "MemTotal:  11534336 kB" → 11264 (MB), −1 without a usable MemTotal line.
static func parse_meminfo_mb(text: String) -> int:
	for line in text.split("\n"):
		if line.begins_with("MemTotal:"):
			var kb := line.trim_prefix("MemTotal:").strip_edges().split(" ", false)
			if kb.size() > 0 and kb[0].is_valid_int() and int(kb[0]) > 0:
				return int(kb[0]) / 1024
	return -1


## "desktop", "flagship", "strong" or "weak" ("weak" also covers unknown devices). The driver / renderer fields are
## ignored.
static func classify(info: Dictionary) -> String:
	if not bool(info.get("handheld", false)):
		return "desktop"
	var ram := int(info.get("ram_mb", -1))
	match String(info.get("os", "")):
		"iOS":
			if ram >= FLAGSHIP_MIN_RAM_MB:
				return "flagship"
			return "strong" if ram >= IOS_MIN_RAM_MB else "weak"
		"Android":
			var gpu := String(info.get("gpu", "")).strip_edges()
			if ram < ANDROID_MIN_RAM_MB or int(info.get("cores", 0)) < ANDROID_MIN_CORES:
				return "weak"
			if gpu == "" or weak_gpu(gpu):
				return "weak"
			if ram >= FLAGSHIP_MIN_RAM_MB and flagship_gpu(gpu):
				return "flagship"
			return "strong"
	return "weak"


static func weak_gpu(name: String) -> bool:
	if _weak_re == null:
		_weak_re = RegEx.create_from_string(WEAK_GPU)
	return _weak_re.search(name.to_lower()) != null


static func flagship_gpu(name: String) -> bool:
	if _flagship_re == null:
		_flagship_re = RegEx.create_from_string(FLAGSHIP_GPU)
	return _flagship_re.search(name.to_lower()) != null


## The tier a device starts on: HIGH (desktop, flagship handheld) · MEDIUM (strong handheld) · LOW (weak / unknown).
static func default_tier(info: Dictionary) -> int:
	return {"desktop": HIGH, "flagship": HIGH, "strong": MEDIUM}.get(classify(info), LOW)


static func tier_name(tier: int) -> String:
	return NAMES[clampi(tier, LOW, HIGH)]


static func tier_of(name: String) -> int:
	var i := NAMES.find(name)
	return i if i >= 0 else LOW


## The one-time correction for players the old renderer test put on Low (W60-B1): a handheld still BELOW its device
## default is raised to it (never lowered); any other setting stands. Above Medium only on a flagship (owner 10-23:
## the only handheld whose default is High). Returns the tier to use.
static func raise_to_default(current: int, device_default: int, is_handheld: bool) -> int:
	if not is_handheld:
		return current
	return maxi(current, device_default)


## The one-time correction of a WRONG Low (owner 2026-10-23: a Galaxy S25 Ultra came up on Low). Two fleet bugs put
## strong phones there: probe() read no RAM on Android (Godot −1 → "unknown" → Low default, every Android phone), and
## PerfGuard could read the game's own 20 / 30 FPS window and idle caps as a slow phone. A handheld the device class now
## calls strong (default Medium) is put back on Medium once — silently — unless the tier is the PLAYER's own choice
## (`players_choice`: the game recorded a pick in Settings). A weak device and any tier at or above Medium stand.
## A game that never recorded the player's pick passes false: before this fix a strong Android phone could only be on
## Low by the wrong default or an automatic downgrade (a player would have had to raise it and lower it again).
## Returns the tier to use.
static func fix_wrong_low(current: int, device_default: int, is_handheld: bool, players_choice: bool) -> int:
	if not is_handheld or players_choice or device_default < MEDIUM:
		return current
	return maxi(current, MEDIUM)


## The one-time flagship upgrade (owner 2026-10-23: flagship phones start on High). A player on a flagship (device default
## High) still on Medium — the OLD automatic default — goes to High once, quietly, unless the tier is the PLAYER's own
## choice (`players_choice`: the game recorded a pick in Settings). Never lowers; Low (a PerfGuard step down or the
## player's pick) and High stand; strong / weak devices and desktops are untouched. Returns the tier to use.
static func flagship_upgrade(current: int, device_default: int, is_handheld: bool, players_choice: bool) -> int:
	if not is_handheld or players_choice or device_default < HIGH or current != MEDIUM:
		return current
	return HIGH
