extends Node
## W66 art regression: real exports, Low budget, tier round-trip and unlock swap.
var checks := 0
var failures := 0
func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		push_error("DESERT_REDO " + label)
func tris(mesh: Mesh) -> int:
	var n := 0
	for s in mesh.get_surface_count():
		var a := mesh.surface_get_arrays(s)
		n += (a[Mesh.ARRAY_INDEX].size() if a[Mesh.ARRAY_INDEX] != null else a[Mesh.ARRAY_VERTEX].size()) / 3
	return n
func _ready() -> void:
	await get_tree().process_frame
	var old_count := tris(Models.mesh("house_adobe"))
	for k in 6:
		var id := "adobe_%d" % k
		var low := Models.mesh(id + "_low")
		var high := Models.mesh(id)
		check(not low is PrimitiveMesh and not high is PrimitiveMesh, id + " loads real Blender geometry")
		check(low.get_surface_count() == 1 and high.get_surface_count() == 1, id + " uses one palette surface")
		check(tris(low) <= old_count, id + " Low <= old house")
		check(tris(high) > tris(low), id + " heavy detail is tier-gated")
	var rv := RegionView.new()
	add_child(rv)
	rv.setup(0, UIKit.bold_font())
	rv.set_locked(false, false)
	rv.set_low(true)
	var found := {}
	for mmi in rv.multimeshes:
		if mmi.has_meta("adobe_model"):
			var id := str(mmi.get_meta("adobe_model"))
			found[id] = true
			check(mmi.multimesh.mesh == Models.mesh(id + "_low"), "Low selects cheap mesh")
	check(found.size() == 6, "all six house variants appear in Dust Valley")
	check(rv.is_low_merged() and rv._merged_pal.mesh.get_surface_count() == 1, "all six variants cost one merged draw on Low")
	rv.set_low(false)
	for mmi in rv.multimeshes:
		if mmi.has_meta("adobe_model"):
			check(mmi.multimesh.mesh == Models.mesh(str(mmi.get_meta("adobe_model"))), "Medium restores authored detail")
	rv.set_low(true)
	check(rv.is_low_merged(), "tier round trip re-bakes successfully")
	rv.free()
	var locked_rv := RegionView.new()
	add_child(locked_rv)
	locked_rv.setup(1, UIKit.bold_font())
	locked_rv.set_locked(true, false)
	check(locked_rv.tile_mi.mesh == locked_rv._locked_tile, "locked preview mesh selected")
	check(locked_rv._locked_tile.get_surface_count() == 1, "boundary is same draw as tile")
	check(tris(locked_rv._locked_tile) - tris(locked_rv._open_tile) <= 160, "boundary geometry is bounded")
	var plants := 0
	for mmi in locked_rv.multimeshes:
		for k in 3:
			if mmi.multimesh.mesh == Models.mesh("desert_plant_%d" % k):
				plants += 1
	check(plants == 3, "first locked region carries three plant silhouettes")
	var original_colors: PackedColorArray = locked_rv._open_tile.surface_get_arrays(0)[Mesh.ARRAY_COLOR]
	var preview_colors: PackedColorArray = locked_rv._locked_tile.surface_get_arrays(0)[Mesh.ARRAY_COLOR]
	check(original_colors[0] != preview_colors[0], "locked sand leaves original biome intact")
	locked_rv.set_low(true)
	locked_rv.set_locked(false, false)
	check(locked_rv.tile_mi.mesh == locked_rv._open_tile and locked_rv.is_low_merged(), "unlock restores biome and removes boundary before Low bake")
	locked_rv.set_locked(true, false)
	check(locked_rv.tile_mi.mesh == locked_rv._locked_tile and not locked_rv.is_low_merged(), "relock restores preview")
	locked_rv.free()
	print("DESERT_REDO checks=%d failures=%d" % [checks, failures])
	get_tree().quit(1 if failures else 0)
