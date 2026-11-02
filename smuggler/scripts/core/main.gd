extends Node
## Entry point: boots the game state, then builds the 3D world and the HUD.

const HUD_SCENE := preload("res://scenes/ui/hud.tscn")

var world: World
var hud: Control


func _ready() -> void:
	UIManager.current_panel = ""
	GameManager.start()
	world = World.new()
	world.name = "World"
	add_child(world)
	var layer := CanvasLayer.new()
	layer.name = "UI"
	add_child(layer)
	hud = HUD_SCENE.instantiate()
	layer.add_child(hud)
	hud.setup(world)
	AudioManager.start_music()
	if OS.has_feature("debug") and GameManager.debug_args.has("qa"):
		var dbg: Node = load("res://scripts/core/debug_tools.gd").new()
		dbg.main = self
		add_child(dbg)
