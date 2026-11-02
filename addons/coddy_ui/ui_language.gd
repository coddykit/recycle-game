extends RefCounted
## coddy_ui language picker (owner bug 2026-10-24, Patron #108): Settings never uses an OptionButton or a cycle button
## for the language. On a phone an OptionButton's popup opens under the finger and the finger's release selects the row
## below — players saw "tapping the language jumps to the next one". The rule: one Settings row shows the current
## language in its own name; tapping it opens a LIST (kit modal): "System language" first, then every language in its own
## name (English, Türkçe, Deutsch, …), the current one marked; a tap applies it and closes the list.
##   var row := UiLanguage.row(tr("Language"), UiLanguage.label(current, NATIVE, tr("System language")), func(): _pick())
##   UiLanguage.open(host, {"title": tr("Language"), "system": tr("System language"), "codes": LANGS, "names": NATIVE,
##       "current": current_code_or_empty, "on_pick": func(code: String): apply(code)})
## opts: codes (Array of language codes, game order), names ({code: native name}), current ("" = system / device),
## system (label of the first row; omit or "" to leave the system row out), title, pictures ({code: Texture2D} — CJK
## names as pictures for games that keep CJK line-break data unloaded, E49), on_pick (Callable(code: String), "" =
## system), close_label. Returns the UiModal (already added to `host`). Row node names: Lang_<code> / Lang_system.

const UiModal = preload("ui_modal.gd")
const UiRow = preload("ui_row.gd")
const UiKit = preload("ui_kit.gd")


## The Settings row: globe icon, title, the current language as its value, a chevron.
static func row(title: String, value: String, on_press: Callable) -> Button:
	var r := UiRow.nav("globe", title, value, on_press)
	r.name = "LanguageRow"
	return r


## The value text of the Settings row: the current language in its own name, or the system label.
static func label(current: String, names: Dictionary, system_label: String) -> String:
	return str(names.get(current, system_label)) if current != "" else system_label


static func open(host: Node, opts: Dictionary) -> Control:
	var m := UiModal.new()
	m.name = "LanguagePicker"
	host.add_child(m)
	m.setup(str(opts.get("title", "Language")), {"icon": "globe", "width": 760, "close": true, "sheet": false,
		"close_label": str(opts.get("close_label", ""))})
	m.close_requested.connect(func() -> void: m.queue_free())
	var current := str(opts.get("current", ""))
	var names: Dictionary = opts.get("names", {})
	var pictures: Dictionary = opts.get("pictures", {})
	var on_pick: Callable = opts.get("on_pick", Callable())
	var codes: Array = []
	if str(opts.get("system", "")) != "":
		codes.append("")
	codes.append_array(opts.get("codes", []))
	var g := UiKit.group()
	var with_pictures: Array = []
	for c in codes:
		var code := str(c)
		var text := str(opts.get("system", "")) if code == "" else str(names.get(code, code))
		var picture: Variant = pictures.get(code, null) if code != "" else null
		var r := UiRow.item("globe" if code == "" else "", "accent", "" if picture is Texture2D else text, "", Callable(), "none")
		r.name = "Lang_" + (code if code != "" else "system")
		r.selected = code == current
		if picture is Texture2D:
			with_pictures.append([r, picture])
		r.pressed.connect(func() -> void:
			if on_pick.is_valid():
				on_pick.call(code)
			if is_instance_valid(m):
				m.queue_free())
		UiKit.group_add(g, r)
	m.body.add_child(g)
	# CJK names as pictures (rows are built once in the tree): the picture replaces the title text in the row
	for pair in with_pictures:
		var texts: Node = (pair[0] as Node).find_child("Texts", true, false)
		if texts == null:
			continue
		var pic := TextureRect.new()
		pic.name = "NamePicture"
		pic.texture = pair[1]
		pic.expand_mode = TextureRect.EXPAND_FIT_WIDTH_PROPORTIONAL
		pic.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT
		pic.custom_minimum_size = Vector2(0, 40)
		pic.mouse_filter = Control.MOUSE_FILTER_IGNORE
		texts.add_child(pic)
	return m
