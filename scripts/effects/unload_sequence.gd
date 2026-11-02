class_name UnloadSequence
extends Node3D
## Delivery moment: rear hatch opens, cargo hops out onto a stack, a receiver
## walks over and carries a crate away (or passengers step out, wave and leave).

const LIFETIME := 5.5

## Untyped on purpose: a scene reload (language / prestige / reset) frees the pool keeper and every crate in it, and
## reading a freed instance into a typed Node3D variable is a script error ("previously freed instance").
static var _crate_pool: Array = []


static func _take_crate(parent: Node) -> Node3D:
	while _crate_pool.size() > 0:
		var item: Variant = _crate_pool.pop_back()
		if is_instance_valid(item):
			var c: Node3D = item
			if c.get_parent() != null:
				c.get_parent().remove_child(c)
			parent.add_child(c)
			c.visible = true
			return c
	var n := Models.instance("crate")
	parent.add_child(n)
	return n


## Crates go back to a hidden pool under the world instead of being freed.
static func _return_crate(c: Node3D, keeper: Node) -> void:
	if not is_instance_valid(c) or not is_instance_valid(keeper):
		return
	if c.get_parent() != null:
		c.get_parent().remove_child(c)
	keeper.add_child(c)
	c.visible = false
	c.scale = Vector3.ONE
	_crate_pool.append(c)

var _t := 0.0
var _door_pivot: Node3D
var _actor: Node3D
var _crates: Array[Node3D] = []
var _people: Array[CharacterActor] = []


func start(world: Node3D, actor: Node3D, dest: Vector3, category_id: String, units: int, paint: Color) -> void:
	world.add_child(self)
	_actor = actor
	var cat := Data.category(category_id)
	var passenger: bool = cat.get("kind", "cargo") == "passenger"
	var heading: Vector3 = -actor.global_basis.z
	heading.y = 0
	heading = heading.normalized() if heading.length() > 0.01 else Vector3.FORWARD
	var right := Vector3(-heading.z, 0, heading.x)
	var length := float(actor.get_meta("length", 1.6))
	var height := float(actor.get_meta("height", 0.9))
	var rear: Vector3 = actor.global_position - heading * (length * 0.5)
	var drop: Vector3 = actor.global_position + right * 2.1 - heading * 0.4
	drop.y = 0.0
	_open_door(actor, length, height, paint)
	if passenger:
		var model := "vip" if category_id == "vip_passenger" else "passenger"
		for i in clampi(units, 1, 3):
			var p := CharacterActor.new()
			add_child(p)
			p.setup(model, 1.35)
			p.global_position = actor.global_position + right * 0.7 - heading * (0.3 * i)
			p.scale = Vector3.ONE * 0.01
			var tw := p.create_tween()
			tw.tween_interval(0.35 + i * 0.35)
			tw.tween_property(p, "scale", Vector3.ONE, 0.2).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
			var away := drop + right * (1.5 + i * 0.6) + heading * randf_range(-1.0, 1.0)
			tw.tween_callback(p.walk_to.bind(away, "wave", 1.4))
			_people.append(p)
	else:
		var n := clampi(units, 1, 6)
		for i in n:
			var crate := _take_crate(self)
			crate.global_position = rear + Vector3(0, 0.45, 0)
			crate.scale = Vector3.ONE * 0.01
			var layer := i / 4
			var slot := i % 4
			var target := drop + right * (0.45 * float(slot % 2)) + heading * (0.45 * float(slot / 2)) + Vector3(0, 0.4 * layer, 0)
			var from := crate.global_position
			var arc := func(f: float) -> void:
				if is_instance_valid(crate):
					var p := from.lerp(target, f)
					p.y += sin(f * PI) * 0.9
					crate.global_position = p
			var tw := crate.create_tween()
			tw.tween_interval(0.45 + i * 0.22)
			tw.tween_property(crate, "scale", Vector3.ONE, 0.08)
			tw.tween_method(arc, 0.0, 1.0, 0.4).set_trans(Tween.TRANS_QUAD)
			tw.tween_callback(func(): AudioManager.play("pop", 0.2, -10.0))
			_crates.append(crate)
		# receiver comes to collect
		var r := CharacterActor.new()
		add_child(r)
		r.setup("worker", 1.35)
		var spawn := drop + right * 3.0 + heading * 0.8
		r.global_position = spawn
		r.face(drop)
		r.walk_to(drop + right * 0.9, "idle", 1.6)
		var tw2 := r.create_tween()
		tw2.tween_interval(0.6 + n * 0.22 + 0.4)
		tw2.tween_callback(func():
			if _crates.size() > 0 and is_instance_valid(_crates[0]):
				var top: Node3D = _crates.pop_back()
				_return_crate(top, get_parent())
				r.carry(Models.instance("crate"))
				r.walk_to(spawn + right * 1.5, "carry", 1.1))
		_people.append(r)
	var cleanup := create_tween()
	cleanup.tween_interval(LIFETIME - 0.6)
	cleanup.tween_callback(_fade_out)
	cleanup.tween_interval(0.6)
	cleanup.tween_callback(_recycle_and_free)


func _open_door(actor: Node3D, length: float, height: float, paint: Color) -> void:
	_door_pivot = Node3D.new()
	actor.add_child(_door_pivot)
	var top := height * 0.82
	_door_pivot.position = Vector3(0, top, length * 0.5 + 0.03)
	var door := MeshInstance3D.new()
	var bm := BoxMesh.new()
	var w := clampf(float(actor.get_meta("width", 0.9)) * 0.8, 0.5, 1.1)
	var h := height * 0.55
	bm.size = Vector3(w, h, 0.05)
	door.mesh = bm
	var mat := StandardMaterial3D.new()
	mat.albedo_color = paint.darkened(0.1)
	mat.roughness = 0.8
	door.material_override = mat
	door.position = Vector3(0, -h * 0.5, 0)
	_door_pivot.add_child(door)
	var tw := _door_pivot.create_tween()
	tw.tween_property(_door_pivot, "rotation:x", -1.45, 0.35).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	tw.tween_interval(maxf(0.5, ContractManager.UNLOAD_TIME - 0.75))
	tw.tween_property(_door_pivot, "rotation:x", 0.0, 0.25).set_trans(Tween.TRANS_QUAD)
	tw.tween_callback(_door_pivot.queue_free)


func _fade_out() -> void:
	for c in _crates:
		if is_instance_valid(c):
			c.create_tween().tween_property(c, "scale", Vector3.ZERO, 0.4)
	for p in _people:
		if is_instance_valid(p):
			p.create_tween().tween_property(p, "scale", Vector3.ZERO, 0.4)


func _recycle_and_free() -> void:
	for c in _crates:
		_return_crate(c, get_parent())
	_crates.clear()
	queue_free()

