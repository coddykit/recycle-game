extends RefCounted
## coddy_ui fonts: the game's OWN bundled Noto files (games/CLAUDE.md §9: Latin/Cyrillic, CJK, Hangul, Devanagari ship
## with the game, the OS font is never used). The current language's script is the primary font, every other bundled
## script is a fallback leaf (the language picker shows all of them), allow_system_fallback is off everywhere.
## Bold = the same files through FontVariation.variation_embolden (no extra font file, no shader); every leaf of the
## fallback chain is emboldened so a Japanese title is as heavy as a Latin one.
##
## A game whose fonts live elsewhere passes its own map: UiFonts.configure({"latin": "res://fonts/A.ttf", "ja": ...}).
## Keys: latin (required), ja, ko, hi, zh (zh_CN / zh_TW / zh), th. Missing files are skipped (warned once).

const DEFAULT_PATHS := {
	"latin": "res://assets/fonts/NotoSans.ttf",
	"ja": "res://assets/fonts/NotoSansJP.ttf",
	"ko": "res://assets/fonts/NotoSansKR.ttf",
	"hi": "res://assets/fonts/NotoSansDevanagari.ttf",
}
const BOLD_EMBOLDEN := 0.62   ## FontVariation.variation_embolden for "bold" text (synthetic semi-bold, all scripts)

static var _paths := DEFAULT_PATHS.duplicate()
static var _files := {}
static var _stacks := {}


static func clear_cache() -> void:
	_files.clear()
	_stacks.clear()


static func configure(paths: Dictionary) -> void:
	_paths = paths.duplicate()
	_files.clear()
	_stacks.clear()


static func paths() -> Dictionary:
	return _paths


static func _file(key: String) -> FontFile:
	if _files.has(key):
		return _files[key]
	var path: String = _paths.get(key, "")
	var f: FontFile = null
	if path != "" and ResourceLoader.exists(path):
		f = load(path) as FontFile
	elif path != "":
		push_warning("coddy_ui: font missing " + path)
	_files[key] = f
	return f


## Which bundled script leads for a locale ("ja_JP" → ja, "zh_TW" → zh, everything else → latin).
static func script_for(locale: String) -> String:
	var lang := locale.get_slice("_", 0)
	if lang in ["ja", "ko", "hi", "zh", "th"] and _paths.has(lang):
		return lang
	return "latin"


static func _leaf(source: FontFile, bold: bool) -> Font:
	var leaf: FontFile = source.duplicate()
	leaf.allow_system_fallback = false
	leaf.fallbacks = []
	if not bold:
		return leaf
	var v := FontVariation.new()
	v.base_font = leaf
	v.variation_embolden = BOLD_EMBOLDEN
	return v


## The font stack for `locale` (default: the current TranslationServer locale). Cached per locale + weight.
static func stack(bold: bool = false, locale: String = "") -> Font:
	if locale == "":
		locale = TranslationServer.get_locale()
	var lead := script_for(locale)
	var key := "%s|%s" % [lead, bold]
	if _stacks.has(key):
		return _stacks[key]
	var primary_src := _file(lead)
	if primary_src == null:
		primary_src = _file("latin")
	if primary_src == null:
		return ThemeDB.fallback_font
	var primary: FontFile = primary_src.duplicate()
	primary.allow_system_fallback = false
	var fallbacks: Array[Font] = []
	for k in ["latin", "ja", "ko", "zh", "hi", "th"]:
		var src := _file(k) if _paths.has(k) else null
		if src == null or src == primary_src:
			continue
		fallbacks.append(_leaf(src, bold))
	primary.fallbacks = fallbacks
	var out: Font = primary
	if bold:
		var v := FontVariation.new()
		v.base_font = primary
		v.variation_embolden = BOLD_EMBOLDEN
		out = v
	_stacks[key] = out
	return out


## Every leaf of a stack (tests: no system fallback anywhere, glyph coverage).
static func leaves(f: Font) -> Array:
	var out: Array = []
	var base: Font = f
	if f is FontVariation:
		base = (f as FontVariation).base_font
	out.append(base)
	for fb in base.fallbacks:
		out.append((fb as FontVariation).base_font if fb is FontVariation else fb)
	return out


static func has_char(f: Font, cp: int) -> bool:
	for leaf in leaves(f):
		if leaf.has_char(cp):
			return true
	return false
