class_name DriverPortrait
extends Control
## Procedural driver portrait (the paid Driver Portraits look): the same driver always gets the same face, drawn from
## their name — skin, hair, hat, glasses, shirt — on a disc in their rarity colour. No art files, looks only.

const SKIN := ["f6d3b3", "e9b98e", "d49a6a", "b9764a", "8d5524", "5e3a1e"]
const HAIR := ["20160f", "4a2c17", "7a4b21", "c9962b", "d8d2c4", "a3341f", "2a2f3a"]
const SHIRT := ["3d8bd9", "5dbb63", "e05a47", "8e5cd9", "2bb3a8", "f28c28", "f4f1ea", "2b2f36"]
const STYLES := 5 # short, long, bald, cap, bun
const ACCESSORIES := 4 # none, glasses, moustache, headband

var _rim := Color("a7aeb8")


static func make(driver: Dictionary, size := 64) -> DriverPortrait:
	var p := DriverPortrait.new()
	p.custom_minimum_size = Vector2(size, size)
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	p.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	p.setup(driver)
	return p


## The traits a name maps to (tests: every driver has a stable, valid look).
static func traits(driver_name: String) -> Dictionary:
	var h := absi(driver_name.hash())
	return {"skin": h % SKIN.size(), "hair": (h / 7) % HAIR.size(), "style": (h / 53) % STYLES, "acc": (h / 311) % ACCESSORIES,
			"shirt": (h / 1999) % SHIRT.size(), "cap": (h / 9973) % SHIRT.size()}


func setup(driver: Dictionary) -> void:
	_traits = traits(str(driver.get("name", "?")))
	_rim = UIKit.rarity_color(str(driver.get("rarity", "common")))
	queue_redraw()


var _traits := {}


func _draw() -> void:
	if _traits.is_empty():
		return
	var s := minf(size.x, size.y)
	if s <= 0.0:
		return
	var c := Vector2(size.x, size.y) * 0.5
	draw_circle(c, s * 0.5, _rim.darkened(0.55))
	draw_arc(c, s * 0.5 - s * 0.03, 0.0, TAU, 40, _rim, s * 0.06, true)
	var skin := Color(SKIN[_traits.skin])
	var hair := Color(HAIR[_traits.hair])
	var shirt := Color(SHIRT[_traits.shirt])
	# shoulders (clipped to the disc by staying inside it)
	var shoulders := PackedVector2Array()
	for i in 13:
		var a := PI + PI * float(i) / 12.0
		shoulders.append(c + Vector2(cos(a) * s * 0.40, 0.0) + Vector2(0, s * 0.40 + sin(a) * s * 0.24))
	draw_colored_polygon(shoulders, shirt)
	# neck + head
	draw_rect(Rect2(c + Vector2(-s * 0.07, s * 0.08), Vector2(s * 0.14, s * 0.14)), skin.darkened(0.18))
	var head := c + Vector2(0, -s * 0.07)
	var hr := s * 0.22
	match int(_traits.style):
		1: # long hair behind the head
			draw_rect(Rect2(head + Vector2(-hr * 1.12, -hr * 0.2), Vector2(hr * 2.24, hr * 1.7)), hair)
		4: # bun
			draw_circle(head + Vector2(0, -hr * 1.12), hr * 0.34, hair)
	draw_circle(head, hr, skin)
	match int(_traits.style):
		0, 1, 4: # hair on top
			draw_arc(head, hr * 0.92, PI * 1.02, PI * 1.98, 18, hair, hr * 0.5, true)
		3: # cap with a brim
			var cap := Color(SHIRT[_traits.cap]).darkened(0.1)
			draw_arc(head, hr * 0.92, PI * 1.0, PI * 2.0, 18, cap, hr * 0.62, true)
			draw_rect(Rect2(head + Vector2(-hr * 1.05, -hr * 0.42), Vector2(hr * 2.1, hr * 0.2)), cap.darkened(0.2))
	# face
	var eye_y := head.y - hr * 0.02
	draw_circle(Vector2(head.x - hr * 0.38, eye_y), maxf(1.5, s * 0.026), Color("1b1b24"))
	draw_circle(Vector2(head.x + hr * 0.38, eye_y), maxf(1.5, s * 0.026), Color("1b1b24"))
	draw_arc(Vector2(head.x, head.y + hr * 0.30), hr * 0.30, 0.15 * PI, 0.85 * PI, 10, Color("7a3b2e"), maxf(1.5, s * 0.022), true)
	match int(_traits.acc):
		1: # glasses
			var gr := hr * 0.30
			draw_arc(Vector2(head.x - hr * 0.38, eye_y), gr, 0.0, TAU, 14, Color("111118"), maxf(1.5, s * 0.028), true)
			draw_arc(Vector2(head.x + hr * 0.38, eye_y), gr, 0.0, TAU, 14, Color("111118"), maxf(1.5, s * 0.028), true)
			draw_line(Vector2(head.x - hr * 0.08, eye_y), Vector2(head.x + hr * 0.08, eye_y), Color("111118"), maxf(1.5, s * 0.028))
		2: # moustache
			draw_rect(Rect2(Vector2(head.x - hr * 0.42, head.y + hr * 0.16), Vector2(hr * 0.84, hr * 0.14)), hair.darkened(0.1))
		3: # headband
			draw_rect(Rect2(Vector2(head.x - hr * 0.98, head.y - hr * 0.55), Vector2(hr * 1.96, hr * 0.2)), Color(SHIRT[_traits.cap]))
