extends Node
## Patron 10-29: every headless run printed "2 resources still in use at exit" (+ 4 leaked ObjectDB instances):
## main.gd / the title card start the music, and the Dummy driver's music_main.ogg playback (OggPacketSequence +
## AudioStreamPlaybackOggVorbis) outlived its player at quit (an effect's cash.mp3 the same way). Now headless runs
## are silent (no music, effects, engine or voice) and AudioManager lets go of every stream when it leaves the tree. The real main scene runs here; check.sh also fails
## this scene's run on any "resources still in use" line, so the exit itself is the last check.
## Run: godot --headless --audio-driver Dummy --path . res://tests/audio_exit_test.tscn
var failures := 0
var checks := 0


func check(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		print("AUDIOEXIT FAIL ", label)
	else:
		print("AUDIOEXIT ok ", label)


func _frames(n := 6) -> void:
	for i in n:
		await get_tree().process_frame


func _ready() -> void:
	get_tree().create_timer(120.0).timeout.connect(func(): get_tree().quit(2))
	await get_tree().process_frame
	SaveManager.delete_save()
	GameManager.settings["language"] = "en"
	GameManager.settings["quality_notice"] = ""
	GameManager.settings["music"] = true
	AudioManager.music_on = true
	var main_path := str(ProjectSettings.get_setting("application/run/main_scene", ""))
	if main_path.begins_with("uid://"):
		main_path = ResourceUID.get_id_path(ResourceUID.text_to_id(main_path))
	var main: Node = (load(main_path) as PackedScene).instantiate()
	add_child(main)
	await _frames(20)
	check(AudioManager.headless(), "this run is headless")
	check(AudioManager._music.stream != null, "the music track is loaded")
	check(not AudioManager._music.playing, "the main scene did not start the music loop headless")
	AudioManager.start_music()
	AudioManager.set_music(true)
	await _frames(2)
	check(AudioManager.music_on, "the music setting stays on")
	check(not AudioManager._music.playing, "start_music / set_music(true) never play headless")
	check(AudioManager._streams.has("cash"), "the effects are loaded")
	AudioManager.play("cash")
	AudioManager.voice("welcome", 0.0)
	AudioManager.set_engine_activity(6)
	var busy := 0
	for p in [AudioManager._engine, AudioManager._voice] + AudioManager._pool:
		busy += 1 if p.playing else 0
	check(busy == 0, "effects, voice and engine hum stay silent headless (%d busy)" % busy)
	main.queue_free()
	await _frames(2)
	AudioManager.release()
	var held := 0
	for p in [AudioManager._music, AudioManager._engine, AudioManager._voice] + AudioManager._pool:
		held += 1 if (p.playing or p.stream != null) else 0
	check(held == 0, "release() stops every player and drops its stream (%d held)" % held)
	check(AudioManager._tracks.is_empty() and AudioManager._streams.is_empty(), "release() drops the cached streams")
	await _frames(3)
	print("AUDIOEXIT checks=%d failures=%d" % [checks, failures])
	get_tree().quit(1 if failures > 0 else 0)
