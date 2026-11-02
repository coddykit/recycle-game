extends ScrollContainer
## A ScrollContainer that wires itself to the fleet TouchScroll + fading thumb (UiScroll.adopt). Use it as the script of
## every ScrollContainer placed in a .tscn (check_touch_scroll.py accepts scene ScrollContainers only with this script).

func _init() -> void:
	load("res://addons/coddy_ui/ui_scroll.gd").adopt(self)
