extends RefCounted
## Bundled glyph checks only: OS fallback must never hide a missing Android glyph on a Mac.
static func has_glyph(font: Font, cp: int, seen: Array = []) -> bool:
	if font == null or font in seen:
		return false
	seen.append(font)
	if font is FontVariation and has_glyph(font.base_font, cp, seen):
		return true
	if font is FontFile:
		var system: bool = font.allow_system_fallback
		font.allow_system_fallback = false
		var found := font.has_char(cp)
		font.allow_system_fallback = system
		if found:
			return true
	for fallback in font.fallbacks:
		if has_glyph(fallback, cp, seen):
			return true
	return false
static func missing(font: Font, text: String) -> Array:
	var out: Array = []
	for i in text.length():
		var cp := text.unicode_at(i)
		# Whitespace, joiners and variation selectors do not require standalone glyphs.
		if cp <= 32 or cp in [0x200b, 0x200c, 0x200d, 0xfe0e, 0xfe0f]:
			continue
		if not has_glyph(font, cp):
			var tag := "%s U+%04X" % [text[i], cp]
			if tag not in out: out.append(tag)
	return out
static func scan(node: Node, out: Array, visible_only := true) -> int:
	var count := 0
	if node is Control and visible_only and not node.is_visible_in_tree(): return count
	if node is Label or node is Button or node is RichTextLabel:
		var text: String = node.get_parsed_text() if node is RichTextLabel else node.text
		text = node.tr(text) if node.auto_translate_mode != Node.AUTO_TRANSLATE_MODE_DISABLED else text
		if not text.is_empty():
			count += 1
			var font: Font = node.get_theme_font("normal_font" if node is RichTextLabel else "font")
			var absent := missing(font, text)
			if not absent.is_empty():
				out.append({"node": str(node.get_path()), "text": text, "missing": absent})
	for child in node.get_children(): count += scan(child, out, visible_only)
	return count
