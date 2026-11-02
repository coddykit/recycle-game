extends Node3D
## Renders the app icon diorama (transparent background) for store assets.
## xvfb-run godot --rendering-driver opengl3 --resolution 1024x1024 res://tools/store/icon_render.tscn


func _ready() -> void:
	get_viewport().transparent_bg = true
	var env := WorldEnvironment.new()
	var e := Environment.new()
	e.background_mode = Environment.BG_CLEAR_COLOR
	e.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	e.ambient_light_color = Color("d6e2f5")
	e.ambient_light_energy = 0.55
	env.environment = e
	add_child(env)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-50, 25, 0)
	sun.light_energy = 0.42
	sun.shadow_enabled = true
	add_child(sun)
	# island
	var st := SurfaceTool.new()
	var tile := TerrainBuilder.tile_mesh(7.0, Color("e2be78"), Color("d9ae66"), Color("a86f43"), 3, [true, true, true, true],
			Color("efd9a0"), false)
	var island := MeshInstance3D.new()
	island.mesh = tile
	island.material_override = Models.terrain_material
	island.scale = Vector3(1, 0.5, 1)
	add_child(island)
	var road := MeshInstance3D.new()
	road.mesh = TerrainBuilder.road_mesh([[Vector3(-3.5, 0, 1.2), Vector3(3.5, 0, -1.2)]], Vector3.ZERO, 2.0,
			Color("45474d"), Color("7a7c82"), Color("f4f1ea"), [])
	road.material_override = Models.terrain_material
	add_child(road)
	var car := Models.instance("hatchback", Models.paint_material(Color("e05a47")))
	add_child(car)
	car.position = Vector3(-0.2, 0.0, 0.1)
	car.rotation.y = deg_to_rad(-71)
	car.scale = Vector3.ONE * 1.35
	var rack := Models.instance("att_roofrack")
	car.add_child(rack)
	rack.position = Vector3(0, 0.88, 0.1)
	for p in [[Vector3(-0.6, 0, -2.6), "cactus", 1.2], [Vector3(-2.4, 0, -1.2), "cactus", 0.9],
			[Vector3(1.5, 0, -2.0), "crate", 1.6], [Vector3(1.95, 0, -1.6), "crate", 1.3], [Vector3(-1.6, 0, 1.9), "rock_small", 1.2]]:
		var m := Models.instance(p[1])
		add_child(m)
		m.position = p[0]
		m.scale = Vector3.ONE * float(p[2])
		m.rotation.y = randf() * TAU
	var cam := Camera3D.new()
	cam.fov = 30
	add_child(cam)
	cam.position = Vector3(10.6, 9.4, 10.6)
	cam.look_at(Vector3(0, -0.6, 0))
	await get_tree().create_timer(1.0).timeout
	get_viewport().get_texture().get_image().save_png("res://store/icons/icon_layer.png")
	get_tree().quit()
