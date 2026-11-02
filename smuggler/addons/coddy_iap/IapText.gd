## coddy_iap — the shop's words in 10 languages (en tr de es fr ja ko pt ru zh), independent of each game's i18n system.
## Generic messages live in iap_text.json next to this file; product titles / descriptions / group titles come from the
## game's data/iap.json as {lang: text}. The language is the TranslationServer locale (the game's own language setting),
## or `IapText.lang` when a game sets it explicitly. Missing language → English → the key.
class_name IapText
extends RefCounted

const FILE := "res://addons/coddy_iap/iap_text.json"
const LANGS := ["en", "tr", "de", "es", "fr", "ja", "ko", "pt", "ru", "zh"]

static var lang := "" # "" = follow TranslationServer
static var _t: Dictionary = {}


static func current() -> String:
	var l := lang if lang != "" else TranslationServer.get_locale().substr(0, 2)
	return l if l in LANGS else "en"


## A generic message by key ("restored", "thanks"…), with {placeholders}.
static func t(key: String, vars: Dictionary = {}) -> String:
	if _t.is_empty() and FileAccess.file_exists(FILE):
		var d = JSON.parse_string(FileAccess.get_file_as_string(FILE))
		if typeof(d) == TYPE_DICTIONARY:
			_t = d
	return pick(_t.get(key, {}), key).format(vars)


## {lang: text} → the current language's text.
static func pick(m, fallback: String = "") -> String:
	if typeof(m) == TYPE_STRING:
		return String(m)
	if typeof(m) != TYPE_DICTIONARY:
		return fallback
	var l := current()
	if String(m.get(l, "")) != "":
		return String(m[l])
	if String(m.get("en", "")) != "":
		return String(m.en)
	return fallback


static func title(slug: String) -> String:
	return pick(IapCatalog.def(slug).get("title", {}), slug)


static func desc(slug: String) -> String:
	return pick(IapCatalog.def(slug).get("desc", {}), "")


static func group_title(id: String) -> String:
	for g in IapCatalog.groups():
		if String(g.get("id", "")) == id:
			return pick(g.get("title", {}), id)
	return id


## Every text a language must have (tests): the generic keys + each product's title/desc + each group title.
static func missing(l: String) -> Array:
	var keep := lang
	lang = l
	var out := []
	t("thanks")
	for k in _t:
		if String(_t[k].get(l, "")) == "":
			out.append("text:" + String(k))
	for s in IapCatalog.slugs():
		for f in ["title", "desc"]:
			if String(IapCatalog.def(s).get(f, {}).get(l, "")) == "":
				out.append("%s.%s" % [s, f])
	for g in IapCatalog.groups():
		if String(g.get("title", {}).get(l, "")) == "":
			out.append("group:" + String(g.get("id", "")))
	lang = keep
	return out
