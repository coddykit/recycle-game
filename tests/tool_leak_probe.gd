extends SceneTree
## Scene-reload leak probe (not a gate test): boots Main N times (cold start, as after Continue / language switch) and
## prints static memory, object / node / resource counts so a per-reload leak shows as a straight line.
##   godot --headless --path . -s tests/tool_leak_probe.gd        (env MK_REPS, default 40)

func _initialize() -> void:
	_run()


func _run() -> void:
	var reps := int(OS.get_environment("MK_REPS")) if OS.get_environment("MK_REPS") != "" else 40
	await process_frame
	var gm := root.get_node("GameManager")
	var sm := root.get_node("SaveManager")
	sm.delete_save()
	for i in reps + 1:
		var main: Node = (load("res://scenes/main/main.tscn") as PackedScene).instantiate()
		root.add_child(main)
		current_scene = main
		for _f in 6:
			await process_frame
		if i % 5 == 0:
			print("[leak] rep %2d static %.1f MB objects %d nodes %d orphans %d resources %d video %.1f MB" % [i,
				Performance.get_monitor(Performance.MEMORY_STATIC) / 1048576.0, int(Performance.get_monitor(Performance.OBJECT_COUNT)),
				int(Performance.get_monitor(Performance.OBJECT_NODE_COUNT)), int(Performance.get_monitor(Performance.OBJECT_ORPHAN_NODE_COUNT)),
				int(Performance.get_monitor(Performance.OBJECT_RESOURCE_COUNT)), Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED) / 1048576.0])
		gm.save_now()
		main.queue_free()
		for _f in 4:
			await process_frame
	sm.delete_save()
	quit(0)
