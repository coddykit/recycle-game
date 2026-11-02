class_name HQSite
extends Node3D
## The player's home base: garage model per level, parking lot, busy workers.

var level := 0
var garage: Node3D
var pad: MeshInstance3D
var workers: Array[Node3D] = []
var _time := 0.0
var world: Node


func setup(w: Node) -> void:
	world = w
	name = "HQ"
	pad = MeshInstance3D.new()
	pad.material_override = Models.terrain_material
	pad.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(pad)
	set_level(ProgressionManager.garage_level, false)
	Iap.entitlements_changed.connect(refresh_sign) # HQ Signature bought / restored: the gold sign shows at once


func set_level(new_level: int, animate: bool) -> void:
	if new_level == level:
		return
	level = new_level
	var old := garage
	garage = Models.instance("garage_l%d" % clampi(level, 1, 5), Models.paint_material(ProgressionManager.company_color()))
	add_child(garage)
	garage.global_transform = Sites.garage_transform(level)
	_place_sign()
	_build_pad()
	_build_workers()
	if animate:
		var final_scale := Vector3.ONE
		garage.scale = Vector3(0.01, 0.01, 0.01)
		var t := create_tween()
		t.tween_interval(0.35)
		t.tween_property(garage, "scale", final_scale, 0.7).set_trans(Tween.TRANS_ELASTIC).set_ease(Tween.EASE_OUT)
		if old:
			var t2 := old.create_tween()
			t2.tween_property(old, "scale", Vector3(1.1, 0.01, 1.1), 0.35).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_IN)
			t2.tween_callback(old.queue_free)
		world.fx_dust(garage.global_position)
		FX.dust(world, garage.global_position + Vector3(2, 0, 1))
		FX.dust(world, garage.global_position + Vector3(-2, 0, -1))
		get_tree().create_timer(0.6).timeout.connect(func(): FX.confetti(world, garage.global_position + Vector3.UP * 2))
	elif old:
		old.queue_free()


## W66: Low <-> Medium / High switched the model files: the garage takes the tier's meshes in place.
func swap_models() -> void:
	if garage:
		Models.swap_meshes(garage, "garage_l%d" % clampi(level, 1, 5))


var sign_label: Label3D


func set_company(company: String, c: Color) -> void:
	if garage:
		Models.apply_material(garage, Models.paint_material(c))
	_place_sign()
	if sign_label:
		sign_label.text = _sign_text(company)
		_style_sign()


## Floating company name above the HQ.
func _place_sign() -> void:
	if sign_label == null:
		sign_label = Label3D.new()
		sign_label.font = UIKit.bold_font()
		sign_label.font_size = 48
		sign_label.outline_size = 18
		sign_label.pixel_size = 0.011
		sign_label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		sign_label.no_depth_test = true
		sign_label.modulate = Color("ffd23f")
		sign_label.outline_modulate = Color(0.08, 0.09, 0.15, 0.9)
		add_child(sign_label)
	sign_label.text = _sign_text(ProgressionManager.company_name()) if ProgressionManager.flags.has("company") else ""
	_style_sign()
	var m := Data.meta("garage_l%d" % clampi(level, 1, 5))
	sign_label.global_position = Sites.garage_transform(level).origin + Vector3(0, float(m.size[1]) + 1.2, 0)


## The paid "HQ Signature" look: a bigger, brighter gold sign with stars; the player can switch it off in Settings.
func signature_on() -> bool:
	return Iap.has("hq_theme") and bool(GameManager.settings.get("hq_signature", true))


func _sign_text(company: String) -> String:
	var t := UIKit.upper(company)
	return "★ %s ★" % t if signature_on() else t


func _style_sign() -> void:
	if sign_label == null:
		return
	var sig := signature_on()
	sign_label.font_size = 62 if sig else 48
	sign_label.outline_size = 24 if sig else 18
	sign_label.modulate = Color("ffe28a") if sig else Color("ffd23f")
	sign_label.outline_modulate = ProgressionManager.company_color().darkened(0.6) if sig else Color(0.08, 0.09, 0.15, 0.9)


## Settings changed the signature switch or the company colour: restyle.
func refresh_sign() -> void:
	_place_sign()


func _build_pad() -> void:
	var f := Sites.hq_frame()
	var slots := ProgressionManager.vehicle_slots()
	var half_count := int(ceil(slots / 2.0))
	var width := 2.0 * (0.6 + (half_count + 0.5) * Sites.SPOT_SPACING)
	var mesh := TerrainBuilder.pad_mesh(Vector2(width, 4.4), Color("55585f"), half_count * 2, Color("e9e4d8"))
	pad.mesh = mesh
	pad.global_transform = Transform3D(Basis(f.side, Vector3.UP, -f.away), f.pos + f.away * 1.4)


func _build_workers() -> void:
	for w in workers:
		w.queue_free()
	workers.clear()
	var n := 1 + level
	for i in n:
		var c := CharacterActor.new()
		add_child(c)
		c.setup("worker" if i % 2 == 0 else "driver", 1.35)
		c.global_position = _work_point(i)
		c.set_meta("idx", i)
		c.set_meta("wait", randf_range(0.5, 3.0))
		workers.append(c)


## Points around the garage door and parking lot where the crew hangs out.
func _work_point(i: int) -> Vector3:
	var gt := Sites.garage_transform(level)
	var f := Sites.hq_frame()
	var front: Vector3 = gt.origin - f.away * (float(Data.meta("garage_l%d" % level).size[2]) * 0.5 + 0.7)
	var spread := 1.2 + level * 0.6
	return front + f.side * (randf_range(-spread, spread)) + f.away * randf_range(-0.3, 0.3) + Vector3(0, 0, 0) * i


func _process(delta: float) -> void:
	_time += delta
	for w in workers:
		var c := w as CharacterActor
		if c.state == "walk":
			continue
		var wait: float = float(c.get_meta("wait")) - delta
		c.set_meta("wait", wait)
		if wait > 0:
			continue
		c.set_meta("wait", randf_range(1.5, 4.5))
		var roll := randf()
		if roll < 0.55:
			# shuttle a crate between the garage and the lot
			if c.state != "carry" and randf() < 0.6:
				c.carry(Models.instance("crate"))
				c.walk_to(_work_point(int(c.get_meta("idx"))), "carry", 1.0)
			else:
				c.drop_carry()
				c.walk_to(_work_point(int(c.get_meta("idx"))), "idle", 1.3)
		elif roll < 0.75:
			c.drop_carry()
			c.set_state("wave")
		else:
			c.drop_carry()
			c.set_state("idle")
