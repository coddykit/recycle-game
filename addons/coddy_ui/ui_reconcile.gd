extends RefCounted
## coddy_ui reconcile (W62/W63, canal 6ad000c4f2 / sky-tower 1a563c5978 / airport e5eb3e96c3): lists refreshed every
## second must not free the button under a finger (a Button fires on release: a press whose node was freed is lost).
## Build the new rows into a DETACHED container, then:
##   UiReconcile.apply(list, fresh, scroll)   → same structure: texts / colours / enabled / callbacks copied in place;
##                                             changed structure: rebuilt, but never while TouchScroll.held(list);
##                                             returns "patched" | "rebuilt" | "deferred" (fresh is freed in every case).
## Rows that implement copy_from(other) (UiRow) copy themselves; Buttons, Labels and TextureRects are patched here.

const TouchScroll = preload("touch_scroll.gd")


## Structure key: class + name of every node, nested (Buttons and rows are leaves: their inner labels are theirs).
static func shape(node: Node) -> String:
	var parts := PackedStringArray()
	for child in node.get_children():
		var nm := String(child.name)
		var leaf := child is BaseButton or child.has_method("copy_from")
		parts.append("%s:%s%s" % [child.get_class(), "" if nm.begins_with("@") else nm, ("(" + shape(child) + ")") if child.get_child_count() > 0 and not leaf else ""])
	return "|".join(parts)


static func patch(old: Node, new: Node) -> void:
	for i in old.get_child_count():
		var a: Node = old.get_child(i)
		var b: Node = new.get_child(i)
		if a.has_method("copy_from"):
			a.copy_from(b)
			_callbacks(a as BaseButton, b as BaseButton)
		elif a is Button:
			var ab := a as Button
			var bb := b as Button
			if ab.text != bb.text:
				ab.text = bb.text
			if ab.disabled != bb.disabled:
				ab.disabled = bb.disabled
			if ab.icon != bb.icon:
				ab.icon = bb.icon
			if ab.theme_type_variation != bb.theme_type_variation:
				ab.theme_type_variation = bb.theme_type_variation
			_callbacks(ab, bb)
		elif a is Label:
			var al := a as Label
			var bl := b as Label
			if bl.has_meta("coddy_fit_alts") and al.get_meta("coddy_fit_alts", []) != bl.get_meta("coddy_fit_alts"):
				al.set_meta("coddy_fit_alts", bl.get_meta("coddy_fit_alts"))   # #113 r2 one-line fit: re-fit the new texts
				al.remove_meta("_line_key")
				al.text = bl.text
			elif bl.has_meta("coddy_fit_alts"):
				pass   # same alternatives: the live label keeps the alternative / size it fitted
			elif al.text != bl.text:
				al.text = bl.text
			if al.theme_type_variation != bl.theme_type_variation:
				al.theme_type_variation = bl.theme_type_variation
			for k in ["font_color"]:
				if bl.has_theme_color_override(k):
					al.add_theme_color_override(k, bl.get_theme_color(k))
				elif al.has_theme_color_override(k):
					al.remove_theme_color_override(k)
			if bl.has_theme_font_size_override("font_size") and not bl.has_meta("coddy_fit_alts"):
				al.add_theme_font_size_override("font_size", bl.get_theme_font_size("font_size"))
		elif a is TextureRect:
			if (a as TextureRect).texture != (b as TextureRect).texture:
				(a as TextureRect).texture = (b as TextureRect).texture
		else:
			if a is Control and (a as Control).theme_type_variation != (b as Control).theme_type_variation:
				(a as Control).theme_type_variation = (b as Control).theme_type_variation   # #113 r2 card state (locked / selected)
			patch(a, b)


static func _callbacks(a: BaseButton, b: BaseButton) -> void:
	if a == null or b == null:
		return
	for c in a.pressed.get_connections():
		a.pressed.disconnect(c.callable)
	for c in b.pressed.get_connections():
		a.pressed.connect(c.callable)


static func apply(list: Node, fresh: Node, scroll: ScrollContainer = null) -> String:
	var result := "patched"
	if shape(list) == shape(fresh):
		patch(list, fresh)
	elif list.get_child_count() > 0 and TouchScroll.held(list):
		# #113: an EMPTY list (a tab/drawer just opened) has no button under the finger to protect — deferring it left
		# the drawer blank until the next 1 Hz refresh (~1 s; up to HOLD_SAFETY_MS + 1 s when a release was lost).
		result = "deferred"
	else:
		var keep := scroll.scroll_vertical if scroll != null else 0
		for child in list.get_children():
			list.remove_child(child)
			child.queue_free()
		for child in fresh.get_children():
			fresh.remove_child(child)
			list.add_child(child)
		if scroll != null:
			scroll.set_deferred("scroll_vertical", keep)
		result = "rebuilt"
	fresh.free()
	return result
