extends RefCounted
## coddy_ui shop look for the shared coddy_iap shop (games/tools/shop: IapScreen.build). The shop stays the shared,
## tested catalog code; this only restyles what it built — run it after every build and after its own rebuilds
## (Iap.prices_changed / entitlements_changed → call again, deferred). Idempotent.
##   IapScreen.build(list); UiShop.style(list.get_node("iap_shop"), {"best": tr("Best value")})
## Result (default, COMPACT — Patron 10-22: at least 3 products on one screen, landscape and portrait): intro line → a
## one-line info strip (tap to expand / collapse); group heads → overlines; each product → a compact row 92 px tall:
## icon tile 48 + title (1 line) + short description (1 line, ellipsis) + price / buy button on the right (brand fill for
## editions / bundles). `compact: false` gives the large cards (title + wrapped description, "Best value" badge).
## Restore → ghost button with an icon. Text never smaller than the 12 sp caption, buttons never below 84 px.
## opts: best (translated "Best value" badge text for bundles; "" = no badge), icon_for (Callable(slug) -> Lucide name),
##       featured (Array of slugs drawn with the brand price button; default: bundle / premium / edition slugs),
##       price_width (px, default 104 compact — widens to fit the price — / 150 large), compact (default true),
##       cards (#113 r2 list-card look, see _card), sub_for (Callable(slug) -> a short one-line text, cards only),
##       tag_for (Callable(slug) -> the short policy tag that ends the line: "Consumable · Ads stay", cards only).

const Tokens = preload("ui_tokens.gd")
const Icons = preload("ui_icons.gd")
const META := "coddy_ui_styled"


static func icon_for_slug(slug: String) -> String:
	var s := slug.to_lower()
	for pair in [["noads", "ban"], ["remove_ads", "ban"], ["bundle", "crown"], ["complete", "crown"], ["premium", "badge-check"],
			["edition", "badge-check"], ["pass", "star"], ["vip", "star"], ["starter", "gift"], ["fund", "coins"], ["cash", "coins"],
			["coin", "coins"], ["money", "coins"], ["gold_", "sparkles"], ["fleet_", "sparkles"], ["skin", "sparkles"], ["theme", "sparkles"],
			["speed", "zap"], ["rhythm", "zap"], ["boost", "zap"], ["fast", "zap"], ["assistant", "users"], ["helper", "users"],
			["staff", "users"], ["crew", "users"], ["kitchen", "utensils"], ["pack", "package"]]:
		if s.contains(pair[0]):
			return pair[1]
	return "package"


static func _featured(slug: String, opts: Dictionary) -> bool:
	if opts.has("featured"):
		return slug in (opts.featured as Array)
	var s := slug.to_lower()
	return s.contains("bundle") or s.contains("premium") or s.contains("edition") or s.contains("complete")


static func style(holder: Node, opts: Dictionary = {}) -> void:
	if holder == null:
		return
	var kit = load("res://addons/coddy_ui/ui_kit.gd")
	var cards := bool(opts.get("cards", false))
	var compact := bool(opts.get("compact", true)) and not cards
	# "tight" (#132, with "cards"): a short landscape panel — padding-free cards one 48 dp touch rect tall, no tile,
	# 6 px gap: >= 3 products stay on a 720 px high screen. Fewer canvas items than "cards" (no tile).
	var tight := cards and bool(opts.get("tight", false))
	if cards and holder is BoxContainer:
		(holder as BoxContainer).add_theme_constant_override("separation", 6 if tight else kit.CARD_GAP)
	elif compact and holder is BoxContainer:
		(holder as BoxContainer).add_theme_constant_override("separation", 6)
	var first_label := true
	for child in holder.get_children():
		if child.has_meta(META):
			first_label = false
			continue
		if child is Label:
			var l := child as Label
			l.modulate = Color.WHITE
			if String(l.name).begins_with("group_"):
				l.theme_type_variation = "Overline"
				# IapScreen hands an upper-cased, already translated group title: never looked up again as a CSV key
				# (the UiKit.upper_text rule, 10-27 "RACE" → "Race")
				l.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
				l.remove_theme_font_size_override("font_size")
				l.set_meta(META, true)
			elif first_label:
				_info_card(holder, l, kit, compact or cards, cards, bool(opts.get("low", false)))
			first_label = false
		elif child is PanelContainer and String(child.name).begins_with("prod_"):
			if cards:
				_card(child as PanelContainer, String(child.name).trim_prefix("prod_"), opts, kit)
			elif compact:
				_compact(child as PanelContainer, String(child.name).trim_prefix("prod_"), opts, kit)
			else:
				_product(child as PanelContainer, String(child.name).trim_prefix("prod_"), opts, kit)
			first_label = false
		elif child is Button and String(child.name) == "iap_restore":
			var b := child as Button
			b.theme_type_variation = "GhostButton"
			b.icon = Icons.texture("rotate-ccw", 28, Color.WHITE)
			b.set_meta(META, true)
		elif child is VBoxContainer and String(child.name).begins_with("iap_offer_"):
			style(child, opts)
	if compact and holder is Control and not holder.has_meta("ui_shop_fit"):
		# ≥ 3 products on one screen beat two-line descriptions: when three rows with 2-line descriptions do not fit the
		# list's height, every description drops to one line (full text stays in the tooltip); re-checked on resize
		holder.set_meta("ui_shop_fit", true)
		(holder as Control).resized.connect(func() -> void: _fit_rows.call_deferred(holder))
	if compact and holder is Control:
		_fit_rows.call_deferred(holder)


## low (#132, the game's Low tier): the strip has no info icon (one draw less; the chevron still says "tap me")
static func _info_card(holder: Node, l: Label, kit, compact: bool = false, note: bool = false, low: bool = false) -> void:
	var at := l.get_index()
	var pc: PanelContainer = kit.card("InfoCard")
	pc.name = "shop_intro"
	pc.set_meta(META, true)
	pc.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var h := HBoxContainer.new()
	h.add_theme_constant_override("separation", 14)
	if not low:
		h.add_child(kit.icon("info", 28 if compact else 30, "accent"))
	holder.remove_child(l)
	l.theme_type_variation = "Caption"
	l.remove_theme_font_size_override("font_size")
	l.add_theme_font_size_override("font_size", 22)
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	h.add_child(l)
	pc.add_child(h)
	holder.add_child(pc)
	holder.move_child(pc, at)
	if not compact:
		return
	if note:   # #113 r2: the compact explainer box of a list — ONE whole line (the first sentence when the text is longer,
		# never '…'); a tap shows the whole text
		pc.theme_type_variation = "NoteCard"
		l.set_meta("ui_shop_note", true)
		l.set_meta("coddy_no_fit", true)
		l.set_meta("coddy_fit_alts", sub_alts(l.text, true))
		l.set_meta("ui_shop_full", l.text)
		l.autowrap_mode = TextServer.AUTOWRAP_OFF
		l.clip_text = true
		l.text_overrun_behavior = TextServer.OVERRUN_NO_TRIMMING
		l.resized.connect(func() -> void:
			if l.autowrap_mode == TextServer.AUTOWRAP_OFF:
				kit.fit_line_now(l))
		var chev_n: TextureRect = kit.icon("chevron-down", 26, "on_surface_muted")
		chev_n.name = "expand"
		h.add_child(chev_n)
		pc.mouse_filter = Control.MOUSE_FILTER_STOP
		pc.gui_input.connect(func(ev: InputEvent) -> void:
			var up: bool = kit.is_tap(ev, true)   # once per finger (a phone also sends an emulated click)
			if up:
				toggle_intro(pc))
		return
	# One line that opens on a tap (and closes again): the rule matters, but it must not push the products off screen.
	var sb: StyleBoxFlat = (pc.get_theme_stylebox("panel") as StyleBoxFlat)
	if sb != null and not note:
		sb = sb.duplicate()
		sb.content_margin_top = 4
		sb.content_margin_bottom = 4
		pc.add_theme_stylebox_override("panel", sb)
	l.max_lines_visible = 1
	l.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	var chev: TextureRect = kit.icon("chevron-down", 26, "on_surface_muted")
	chev.name = "expand"
	h.add_child(chev)
	pc.mouse_filter = Control.MOUSE_FILTER_STOP
	pc.gui_input.connect(func(ev: InputEvent) -> void:
		var up: bool = kit.is_tap(ev, true)   # once per finger (a phone also sends an emulated click)
		if up:
			toggle_intro(pc))


## The compact info strip: one line ↔ the whole text (tests / a tap).
static func toggle_intro(pc: Control) -> void:
	var l: Label = null
	for ch in pc.find_children("*", "Label", true, false):
		l = ch
	if l == null:
		return
	if l.has_meta("ui_shop_note"):
		var opening := l.autowrap_mode == TextServer.AUTOWRAP_OFF
		if opening:
			l.text = str(l.get_meta("ui_shop_full"))
			l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			l.clip_text = false
		else:
			l.remove_meta("_line_key")
			l.autowrap_mode = TextServer.AUTOWRAP_OFF
			l.clip_text = true
			load("res://addons/coddy_ui/ui_kit.gd").fit_line_now(l)
		var chev_n := pc.find_child("expand", true, false) as TextureRect
		if chev_n != null:
			chev_n.flip_v = opening
		return
	var open := l.max_lines_visible == 1
	l.max_lines_visible = -1 if open else 1
	# (with trimming on, an autowrap label's minimum is one line: trimming only while closed)
	l.text_overrun_behavior = TextServer.OVERRUN_NO_TRIMMING if open else TextServer.OVERRUN_TRIM_ELLIPSIS
	var chev := pc.find_child("expand", true, false) as TextureRect
	if chev != null:
		chev.flip_v = open


## Compact product row: tile 48 | title (≤ 2 lines) + description (≤ 2 lines, then …) | price button (84 px tall).
## Patron 10-22 (chaiwala shots): nothing a player needs to read is cut to one line — titles / descriptions wrap to two
## lines, the row grows with them; a price that has not loaded yet shows a fixed "…" (never "Loadin…"); a loaded price
## fits (font steps 25 → 21 px, then the button widens) and is never trimmed.
static func _compact(card: PanelContainer, slug: String, opts: Dictionary, kit) -> void:
	card.set_meta(META, true)
	card.theme_type_variation = "CardRaised"
	var sb := (card.get_theme_stylebox("panel") as StyleBoxFlat)
	if sb != null:
		sb = sb.duplicate()
		sb.content_margin_left = 14
		sb.content_margin_right = 4
		sb.content_margin_top = 1   # two-line descriptions: the row's own padding stays minimal (≥ 3 rows a screen)
		sb.content_margin_bottom = 1
		card.add_theme_stylebox_override("panel", sb)
	var h := card.get_child(0) as HBoxContainer
	if h == null:
		return
	h.add_theme_constant_override("separation", 14)
	var featured := _featured(slug, opts)
	var has_icon := false
	for ch in h.get_children():
		if ch is TextureRect:
			has_icon = true
			(ch as TextureRect).custom_minimum_size = Vector2(48, 48)
			(ch as TextureRect).size_flags_vertical = Control.SIZE_SHRINK_CENTER
	if not has_icon:
		var icb: Callable = opts.get("icon_for", Callable())
		var icon_name: String = str(icb.call(slug)) if icb.is_valid() else icon_for_slug(slug)
		var t: TextureRect = kit.tile(icon_name, "primary" if featured else "accent", 48)
		t.name = "tile"
		h.add_child(t)
		h.move_child(t, 0)
	var buy: Button = null
	var i := 0
	for ch in h.get_children():
		if ch is VBoxContainer:
			var v := ch as VBoxContainer
			v.add_theme_constant_override("separation", 0)
			v.alignment = BoxContainer.ALIGNMENT_CENTER
			v.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			for l in v.get_children():
				if not l is Label:
					continue
				var lab := l as Label
				lab.modulate = Color.WHITE
				lab.remove_theme_font_size_override("font_size")
				# up to two lines, then … (title and description); the row grows with them
				lab.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
				lab.clip_text = false
				lab.max_lines_visible = 2
				lab.add_theme_constant_override("line_spacing", 0) # four lines keep the compact row ≤140 px with Noto too
				lab.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
				lab.size_flags_horizontal = Control.SIZE_EXPAND_FILL
				lab.custom_minimum_size.x = 1   # the row gives the width; a long word never widens the row
				lab.tooltip_text = lab.text
				if i == 0:
					lab.theme_type_variation = "LabelStrong"
					lab.add_theme_font_size_override("font_size", 25)
					# one line when the name fits (25 → 21 px); only a name that is too long even at 21 px takes a 2nd line
					lab.resized.connect(func() -> void:
						if lab.size.x > 1.0:
							_fit_title(lab, lab.size.x))
				elif i == 1:
					lab.theme_type_variation = "CaptionMuted"
				else:
					lab.theme_type_variation = "Caption"
					lab.add_theme_color_override("font_color", kit.c("primary"))
				i += 1
		elif ch is Button:
			buy = ch
	if buy != null:
		buy.theme_type_variation = "PrimaryButton" if featured else "SecondaryButton"
		buy.custom_minimum_size = Vector2(float(opts.get("price_width", 104)), Tokens.TOUCH.min)
		buy.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		buy.autowrap_mode = TextServer.AUTOWRAP_OFF
		buy.clip_text = false
		buy.text_overrun_behavior = TextServer.OVERRUN_NO_TRIMMING
		buy.remove_theme_font_size_override("font_size")
		var bsb := buy.get_theme_stylebox("normal") as StyleBoxFlat
		if bsb != null:   # narrower side padding: prices are short
			for st in ["normal", "hover", "pressed", "hover_pressed", "disabled"]:
				var b2 := buy.get_theme_stylebox(st)
				if b2 != null:
					b2 = b2.duplicate()
					b2.content_margin_left = 16
					b2.content_margin_right = 16
					buy.add_theme_stylebox_override(st, b2)
		fit_price(buy, float(opts.get("price_width", 104)))   # 104 px: a price is short; the title gets the room


## #113 r2 list card (opts.cards): the product row in the kit's list-card look — big icon tile 64 + title on the left,
## ONE grey line under the title (fit_line: steps 23 → 21 px, then the first sentence; never '…', never a split word),
## and the price pill as the card's single badge on the right (owned → the green "open" pill). Featured products get the
## accent edge. Only the look changes: the same buy Button, the same pressed handler, the same disabled states.
## Weak devices: no extra node except the tile when the row has none; every stylebox is the shared theme one.
static func _card(card: PanelContainer, slug: String, opts: Dictionary, kit) -> void:
	card.set_meta(META, true)
	var featured := _featured(slug, opts)
	var tight := bool(opts.get("tight", false))
	if tight:
		card.theme_type_variation = "ListCardTightSelected" if featured else "ListCardTight"
	else:
		card.theme_type_variation = "ListCardDenseSelected" if featured else "ListCardDense"
	card.remove_theme_stylebox_override("panel")
	var h := card.get_child(0) as HBoxContainer
	if h == null:
		return
	h.add_theme_constant_override("separation", 16)
	var has_icon := false
	for ch in h.get_children():
		if ch is TextureRect:
			has_icon = true
			(ch as TextureRect).custom_minimum_size = Vector2(kit.CARD_TILE, kit.CARD_TILE)
			(ch as TextureRect).size_flags_vertical = Control.SIZE_SHRINK_CENTER
	if tight or bool(opts.get("low", false)):
		# #132 tight row / #136 Low tier: no icon tile (as on Low) — the width goes to the one grey line + tag (one draw less per card)
		for ch in h.get_children():
			if ch is TextureRect:
				h.remove_child(ch)
				ch.free()
		h.add_theme_constant_override("separation", 12)
	elif not has_icon:
		var icb: Callable = opts.get("icon_for", Callable())
		var icon_name: String = str(icb.call(slug)) if icb.is_valid() else icon_for_slug(slug)
		var t: TextureRect = kit.tile(icon_name, "primary" if featured else "accent", kit.CARD_TILE, false)
		t.name = "tile"
		t.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		h.add_child(t)
		h.move_child(t, 0)
	var buy: Button = null
	for ch in h.get_children():
		if ch is VBoxContainer:
			var v := ch as VBoxContainer
			v.add_theme_constant_override("separation", 2)
			v.alignment = BoxContainer.ALIGNMENT_CENTER
			v.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			var i := 0
			for l in v.get_children():
				if not l is Label:
					continue
				var lab := l as Label
				lab.modulate = Color.WHITE
				lab.remove_theme_font_size_override("font_size")
				lab.size_flags_horizontal = Control.SIZE_EXPAND_FILL
				lab.custom_minimum_size.x = 1
				lab.tooltip_text = lab.text
				if i == 0:
					lab.theme_type_variation = "LabelStrong"
					lab.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
					lab.clip_text = false
					lab.max_lines_visible = -1
					lab.text_overrun_behavior = TextServer.OVERRUN_NO_TRIMMING
					lab.add_theme_font_size_override("font_size", 25)
					lab.resized.connect(func() -> void:
						if lab.size.x > 1.0:
							_fit_title_words(lab, lab.size.x))
				elif i == 1:
					lab.name = "Sub"
					lab.theme_type_variation = "CaptionMuted"
					lab.max_lines_visible = -1
					lab.text_overrun_behavior = TextServer.OVERRUN_NO_TRIMMING
					lab.autowrap_mode = TextServer.AUTOWRAP_OFF
					lab.clip_text = true
					lab.add_theme_font_size_override("font_size", kit.CARD_SUB_PX)
					lab.set_meta("coddy_no_fit", true)
					var scb: Callable = opts.get("sub_for", Callable())
					var short: String = str(scb.call(slug)) if scb.is_valid() else ""
					# with the game's own short line, only whole sentences come before it (a clause can mislead)
					var alts := sub_alts(lab.text, short != "")
					if short != "" and not alts.has(short):
						alts.append(short)   # the game's short line: the last resort that always fits
					# store policy (Patron 10-25): what changes the buying decision ("Consumable", "Ads stay", "Not a
					# subscription") is never only in a tooltip — it ends EVERY alternative of the same line (0 extra draws)
					var tcb: Callable = opts.get("tag_for", Callable())
					var tag: String = str(tcb.call(slug)) if tcb.is_valid() else ""
					if tag != "":
						# #136: never a second line, never a cut — the shortest grey line + tag, then the tag alone, then
						# the game's short tag ("1× · Ads stay", UiShop.short_tag()) with the short line, then alone
						var last := str(alts[alts.size() - 1])
						alts = alts.map(func(a): return "%s  ·  %s" % [str(a), tag])
						alts.append(tag)                         # narrow card: the tag alone (≥ 3 products stay on screen)
						var shcb: Callable = opts.get("tag_short_for", Callable())
						var stag: String = str(shcb.call(slug)) if shcb.is_valid() else ""
						if stag != "" and stag != tag:
							alts.append("%s  ·  %s" % [last, stag])
							alts.append(stag)
					lab.set_meta("coddy_shop_tag", tag)
					lab.set_meta("coddy_fit_alts", alts)
					lab.remove_meta("_line_key")
					if not lab.has_meta("ui_shop_line"):
						lab.set_meta("ui_shop_line", true)
						lab.resized.connect(func() -> void: kit.fit_line_later(lab))
					kit.fit_line_later.call_deferred(lab)
				else:
					lab.visible = false   # "separately $x": the card keeps ONE secondary line (the price pill says the rest)
				i += 1
		elif ch is Button:
			buy = ch
	if buy != null:
		var owned := buy.disabled and not _pending(buy)
		buy.theme_type_variation = "OwnedButton" if owned else "PriceButton"
		buy.custom_minimum_size = Vector2(float(opts.get("price_width", 104)), Tokens.TOUCH.min)
		buy.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		buy.autowrap_mode = TextServer.AUTOWRAP_OFF
		buy.clip_text = false
		buy.text_overrun_behavior = TextServer.OVERRUN_NO_TRIMMING
		for st in ["normal", "hover", "pressed", "hover_pressed", "disabled"]:
			buy.remove_theme_stylebox_override(st)
		buy.remove_theme_font_size_override("font_size")
		fit_price(buy, float(opts.get("price_width", 104)))


static func _pending(buy: Button) -> bool:
	var t := String(buy.get_meta("price_text", buy.text))
	for ch in t:
		if ch >= "0" and ch <= "9":
			return false
	return t == "…" or t.ends_with("…") or t.ends_with("...")


## #136 short policy tags (10 languages) for a narrow card: UiShop.short_tag(consumable, ads_stay, lang) →
## "Cons. · Ads stay" / "1× · Ads stay" / "1×". Games pass it as opts.tag_short_for.
const SHORT_TAGS := {
	"consumable": {"en": "Cons.", "tr": "Tük.", "de": "Verbr.", "es": "Cons.", "fr": "Conso.", "pt": "Cons.", "ru": "Расх.", "ja": "消耗", "ko": "소모", "hi": "खपत"},
	"once": {"en": "1×", "tr": "1×", "de": "1×", "es": "1×", "fr": "1×", "pt": "1×", "ru": "1×", "ja": "1×", "ko": "1×", "hi": "1×"},
	"ads_stay": {"en": "Ads stay", "tr": "Reklam kalır", "de": "mit Werbung", "es": "Con anuncios", "fr": "Avec pubs", "pt": "Com anúncios", "ru": "С рекламой", "ja": "広告あり", "ko": "광고 있음", "hi": "विज्ञापन सहित"},
}


static func short_tag(consumable: bool, ads_stay: bool, lang: String = "") -> String:
	var l := (lang if lang != "" else TranslationServer.get_locale()).split("_")[0]
	var parts := PackedStringArray()
	var k := "consumable" if consumable else "once"
	parts.append(str(SHORT_TAGS[k].get(l, SHORT_TAGS[k].en)))
	if ads_stay:
		parts.append(str(SHORT_TAGS.ads_stay.get(l, SHORT_TAGS.ads_stay.en)))
	return " · ".join(parts)


## Alternatives of a product description for the one-line fit: the whole text, then its first sentence / clause.
const SENTENCE_ENDS := [". ", "! ", "? ", "। ", "।", "。", "！", "？"]


static func sub_alts(text: String, sentences_only: bool = false) -> Array:
	var out := [text]
	# sentence ends: Latin ". ", Devanagari danda "।", CJK "。！？" (#132 / Patron: hi notes never fitted one line)
	for sep in (SENTENCE_ENDS if sentences_only else SENTENCE_ENDS + ["; ", ", ", " · "]):
		var at := text.find(sep)
		if at > 0:
			var first := text.substr(0, at).strip_edges()
			if first != "" and not out.has(first):
				out.append(first)
	out.sort_custom(func(a, b): return a.length() > b.length())
	return out


## Card title: 25 → 21 px on one line; a name too long even at 21 px wraps between words (never inside one).
static func _fit_title_words(l: Label, width: float) -> void:
	var f: Font = l.get_theme_font("font")
	var s := 25
	while s > 21 and f.get_string_size(l.text, HORIZONTAL_ALIGNMENT_LEFT, -1, s).x > width:
		s -= 1
	if f.get_string_size(l.text, HORIZONTAL_ALIGNMENT_LEFT, -1, s).x <= width:
		if l.get_theme_font_size("font_size") != s:
			l.add_theme_font_size_override("font_size", s)
		return
	_fit_longest_word(l, width, 25, 21)


static func _product(card: PanelContainer, slug: String, opts: Dictionary, kit) -> void:
	card.set_meta(META, true)
	card.theme_type_variation = "CardRaised"
	var h := card.get_child(0) as HBoxContainer
	if h == null:
		return
	h.add_theme_constant_override("separation", 18)
	var has_icon := false
	for ch in h.get_children():
		if ch is TextureRect:
			has_icon = true
			(ch as TextureRect).custom_minimum_size = Vector2(72, 72)
	var featured := _featured(slug, opts)
	if not has_icon:
		var icb: Callable = opts.get("icon_for", Callable())
		var icon_name: String = str(icb.call(slug)) if icb.is_valid() else icon_for_slug(slug)
		var t: TextureRect = kit.tile(icon_name, "primary" if featured else "accent", 72)
		t.name = "tile"
		h.add_child(t)
		h.move_child(t, 0)
	var labels: Array = []
	var buy: Button = null
	for ch in h.get_children():
		if ch is VBoxContainer:
			(ch as VBoxContainer).add_theme_constant_override("separation", 4)
			for l in ch.get_children():
				if l is Label:
					labels.append(l)
		elif ch is Button:
			buy = ch
	for i in labels.size():
		var l: Label = labels[i]
		l.modulate = Color.WHITE
		l.remove_theme_font_size_override("font_size")
		l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		if i == 0:
			l.theme_type_variation = "LabelStrong"
		elif i == 1:
			l.theme_type_variation = "CaptionMuted"
		else:
			l.theme_type_variation = "Caption"
			l.add_theme_color_override("font_color", kit.c("primary"))
	var best: String = str(opts.get("best", ""))
	if best != "" and slug.to_lower().contains("bundle") and not labels.is_empty():
		var v: Node = (labels[0] as Label).get_parent()
		var badge: Control = kit.badge(best)
		badge.name = "best"
		v.add_child(badge)
		v.move_child(badge, 0)
	if buy != null:
		buy.theme_type_variation = "PrimaryButton" if featured else "SecondaryButton"
		buy.custom_minimum_size = Vector2(float(opts.get("price_width", 150)), Tokens.TOUCH.min)
		buy.clip_text = false
		buy.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		buy.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		buy.remove_theme_font_size_override("font_size")
		if buy.disabled:
			buy.add_theme_font_size_override("font_size", Tokens.TYPE.caption.size)


## Price button text: a price not loaded yet (no digit, ends with … — "Loading…", "Preis wird geladen…"; "Owned" keeps its word) shows a
## fixed "…" with the full text as tooltip; a loaded price keeps the 25 px label size, steps down to 21 px if it does
## not fit, and only then widens the button — a price is never trimmed. Idempotent (call again after prices change).
static func fit_price(buy: Button, min_w: float) -> void:
	var full := String(buy.get_meta("price_text", buy.text)) if buy.text == "…" else buy.text
	buy.set_meta("price_text", full)
	var has_digit := false
	for ch in full:
		if ch >= "0" and ch <= "9":
			has_digit = true
			break
	buy.custom_minimum_size.x = min_w
	# only a price still loading ("Loading price…", every language ends with …) collapses; "Owned" / "Unavailable" stay readable
	if not has_digit and (full.ends_with("…") or full.ends_with("...")):
		buy.tooltip_text = full
		buy.text = "…"
		buy.remove_theme_font_size_override("font_size")
		return
	buy.text = full
	buy.tooltip_text = ""
	var f: Font = buy.get_theme_font("font")
	var pad := 32.0
	var room := min_w - pad
	var size := 25
	while size > 21 and f.get_string_size(full, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x > room:
		size -= 1
	buy.add_theme_font_size_override("font_size", size)
	var need := f.get_string_size(full, HORIZONTAL_ALIGNMENT_LEFT, -1, size).x + pad
	if need > min_w:
		buy.custom_minimum_size.x = ceilf(need)


## Title: one line at 25 → 21 px when the whole name fits; otherwise two lines (longest word fitted, never broken).
static func _fit_title(l: Label, width: float) -> void:
	var f: Font = l.get_theme_font("font")
	# #112: measured with the GAME's font (Airport's Montserrat is wider than Noto). A name that does not fit on one line
	# even at 21 px gets the room of the row's icon tile (the tile is decoration, the name must be whole — de
	# 'Warteschlangenhilfe' in a 428 px side panel); the tile comes back when the name fits beside it again.
	var tile := _row_tile(l)
	if tile != null:
		var room: float = tile.size.x + float((tile.get_parent() as BoxContainer).get_theme_constant("separation"))
		var one_line_21: float = f.get_string_size(l.text, HORIZONTAL_ALIGNMENT_LEFT, -1, 21).x
		if tile.visible and one_line_21 > width and one_line_21 <= width + room:
			tile.visible = false
			return   # the row lays out again; resized → _fit_title with the wider column
		if not tile.visible and one_line_21 <= width - room:
			tile.visible = true
			return
	var s := 25
	while s > 21 and f.get_string_size(l.text, HORIZONTAL_ALIGNMENT_LEFT, -1, s).x > width:
		s -= 1
	var desc := _desc_of(l)
	if f.get_string_size(l.text, HORIZONTAL_ALIGNMENT_LEFT, -1, s).x <= width:
		l.add_theme_font_size_override("font_size", s)   # the whole name on one line
		if desc != null and desc.max_lines_visible != 2:
			_set_lines(desc, 2)
		return
	_fit_longest_word(l, width, 25, 21)   # two lines, broken between words
	# at most three text lines a row (≥ 3 products stay on screen): a two-line name leaves one line to the description
	if desc != null and desc.max_lines_visible != 1:
		_set_lines(desc, 1)


## #112: a Label does not re-measure its minimum height when max_lines_visible changes — a description cut to 1 line
## and given 2 again stayed 1 line tall (the second line was never drawn). Change the limit, then re-measure.
static func _set_lines(l: Label, n: int) -> void:
	l.max_lines_visible = n
	l.update_minimum_size()


## #112: how much taller a compact row gets when its description takes one more line, measured: the row is as tall as
## its tallest child (the 84 px buy button, usually), so a text column with room to spare grows the row by 0 — the old
## "+1 line" guess cut descriptions to one line in rows that had the space (Airport / Montserrat, 1280x720 side panel).
static func _growth(desc: Label, line_h: float) -> float:
	var col := desc.get_parent() as Control
	var row := col.get_parent() as Control if col != null else null
	if row == null:
		return line_h
	var other := 0.0
	for c in row.get_children():
		if c != col and c is Control and (c as Control).visible:
			other = maxf(other, (c as Control).get_combined_minimum_size().y)
	var before := maxf(other, col.get_combined_minimum_size().y)
	var after := maxf(other, col.get_combined_minimum_size().y + line_h)
	return after - before


## The icon tile at the front of a compact product row (the title's VBox sits in the row's HBox), or null.
static func _row_tile(title: Label) -> Control:
	var v := title.get_parent()
	var h := v.get_parent() if v != null else null
	if not h is BoxContainer:
		return null
	for c in h.get_children():
		if c is TextureRect:
			return c
	return null


static func _desc_of(title: Label) -> Label:
	var p := title.get_parent()
	if p == null:
		return null
	var i := title.get_index() + 1
	return p.get_child(i) as Label if i < p.get_child_count() else null


## Steps a wrapping title's font down (start → floor) until its longest word fits the width, so a long compound word
## wraps between words instead of breaking in the middle.
static func _fit_longest_word(l: Label, width: float, start: int, floor_px: int) -> void:
	var f: Font = l.get_theme_font("font")
	var longest := ""
	for w in l.text.split(" ", false):
		if w.length() > longest.length():
			longest = w
	var s := start
	while s > floor_px and f.get_string_size(longest, HORIZONTAL_ALIGNMENT_LEFT, -1, s).x > width:
		s -= 1
	l.add_theme_font_size_override("font_size", s)


## `holder_ref` is untyped on purpose: a deferred call whose shop was freed meanwhile cannot convert a freed Object to Node
## (engine ERROR "Cannot convert argument 1 from Object to Object" in headless runs); the validity check below handles it.
static func _fit_rows(holder_ref) -> void:
	if not is_instance_valid(holder_ref) or not holder_ref.is_inside_tree():
		return
	var holder: Node = holder_ref
	var sc: ScrollContainer = null
	var p := holder.get_parent()
	while p != null:
		if p is ScrollContainer:
			sc = p
			break
		p = p.get_parent()
	if sc == null or sc.size.y < 1.0:
		return
	var descs: Array[Label] = []
	var rows: Array[Control] = []
	for c in holder.get_children():
		if c is PanelContainer and String(c.name).begins_with("prod_"):
			rows.append(c)
			for l in c.find_children("*", "Label", true, false):
				if l.get_index() == 1:
					descs.append(l)
	if rows.size() < 3:
		return
	# height of everything down to the third product, as if every description had two lines
	var sep := float((holder as BoxContainer).get_theme_constant("separation")) if holder is BoxContainer else 0.0
	var line_h := 0.0
	if not descs.is_empty():
		var f: Font = descs[0].get_theme_font("font")
		line_h = f.get_height(descs[0].get_theme_font_size("font_size"))
	var h := 0.0
	var seen := 0
	for c in holder.get_children():
		if not (c is Control) or not (c as Control).visible:
			continue
		var ch := (c as Control).size.y
		if rows.has(c):
			var d: Label = null
			for l in c.find_children("*", "Label", true, false):
				if l.get_index() == 1:
					d = l
			if d != null and d.max_lines_visible == 1 and d.get_line_count() > 1:
				ch += _growth(d, line_h)   # what a second description line really adds to this row
			seen += 1
		h += ch + sep
		if seen == 3:
			break
	var two := h <= sc.size.y

	for d in descs:
		var want := 2 if two else 1
		if d.max_lines_visible != want:
			_set_lines(d, want)
