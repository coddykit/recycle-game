extends Node
## Sound hooks. Every gameplay moment calls play("<key>"); missing files are
## silently ignored, so final audio can be dropped into res://assets/audio/.

const SFX := ["click", "tap_soft", "cash", "coin", "accept", "dispatch", "complete", "upgrade",
		"warning", "event", "error", "unlock", "whoosh", "pop", "fail"]
const POOL_SIZE := 8

var _streams := {}
var suspended := false
var _pool: Array[AudioStreamPlayer] = []
var _music: AudioStreamPlayer
var _engine: AudioStreamPlayer
var _cooldown := {}
var sound_on := true
var music_on := true
var voice_on := true
var _voice: AudioStreamPlayer
var _voices := {}
var _voice_cd := {}
var _voice_global_cd := 0
var _tracks := {}
var _track := "music_main"


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	for key in SFX:
		# AI-generated .mp3 (tools/audio/elevenlabs_voice.py --sfx) wins over the synth .wav
		var s := _load("res://assets/audio/%s.mp3" % key)
		if s == null:
			s = _load("res://assets/audio/%s.wav" % key)
		if s:
			_streams[key] = s
	_voice = AudioStreamPlayer.new()
	_voice.volume_db = -2.0
	add_child(_voice)
	for i in POOL_SIZE:
		var p := AudioStreamPlayer.new()
		add_child(p)
		_pool.append(p)
	_music = AudioStreamPlayer.new()
	_music.volume_db = -14.0
	add_child(_music)
	for key in ["music_main", "music_empire"]:
		var ms := _load("res://assets/audio/%s.ogg" % key)
		if ms:
			_make_loop(ms)
			_tracks[key] = ms
	if _tracks.is_empty():
		var m := _load("res://assets/audio/music_loop.wav")
		if m:
			_make_loop(m)
			_tracks["music_main"] = m
	_music.stream = _tracks.get("music_main")
	_engine = AudioStreamPlayer.new()
	_engine.volume_db = -40.0
	add_child(_engine)
	var e := _load("res://assets/audio/engine_loop.wav")
	if e:
		_make_loop(e)
		_engine.stream = e


func _load(path: String) -> AudioStream:
	if not ResourceLoader.exists(path):
		return null
	return load(path) as AudioStream


func _make_loop(s: AudioStream) -> void:
	if s is AudioStreamOggVorbis:
		(s as AudioStreamOggVorbis).loop = true
	elif s is AudioStreamMP3:
		(s as AudioStreamMP3).loop = true
	if s is AudioStreamWAV:
		var w := s as AudioStreamWAV
		w.loop_mode = AudioStreamWAV.LOOP_FORWARD
		w.loop_begin = 0
		w.loop_end = int(w.get_length() * w.mix_rate)


func play(key: String, pitch_var := 0.06, volume_db := 0.0) -> void:
	if suspended or not sound_on or not _streams.has(key) or headless():
		return
	var now := Time.get_ticks_msec()
	if now - int(_cooldown.get(key, 0)) < 40:
		return
	_cooldown[key] = now
	for p in _pool:
		if not p.playing:
			p.stream = _streams[key]
			p.pitch_scale = 1.0 + randf_range(-pitch_var, pitch_var)
			p.volume_db = volume_db
			p.play()
			return


func set_sound(on: bool) -> void:
	sound_on = on
	if not on:
		_engine.stop()


func set_music(on: bool) -> void:
	music_on = on
	# headless runs (tests, check.sh, bots) are silent (games/CLAUDE.md: no sound in headless tests): the Dummy driver
	# never mixes, so a started playback outlived its player at quit — "2 resources still in use at exit"
	# (music_main.ogg + its packet sequence) in every suite, cash.mp3 & co. after a test played an effect
	if on and _music.stream and not _music.playing and not headless():
		_music.play()
	elif not on:
		_music.stop()


static func headless() -> bool:
	return DisplayServer.get_name() == "headless"


func start_music() -> void:
	set_music(music_on)


## Engine hum scales gently with how many vehicles are moving on screen.
func set_engine_activity(moving: int) -> void:
	if suspended or not sound_on or _engine.stream == null or headless():
		return
	var target := -40.0 if moving <= 0 else lerpf(-26.0, -18.0, clampf(moving / 6.0, 0.0, 1.0))
	_engine.volume_db = lerpf(_engine.volume_db, target, 0.05)
	if target > -39.0 and not _engine.playing:
		_engine.play()
	elif moving <= 0 and _engine.volume_db < -38.0 and _engine.playing:
		_engine.stop()


## Announcer voice line from res://assets/audio/voice/<key>.mp3 (optional asset).
## Lines never overlap and each has its own cooldown so they stay charming.
func voice(key: String, cooldown := 20.0) -> void:
	if suspended or key == "" or not voice_on or not sound_on or headless():
		return
	var now := Time.get_ticks_msec()
	if now < _voice_global_cd or now < int(_voice_cd.get(key, 0)):
		return
	var lang := TranslationServer.get_locale().get_slice("_", 0)
	# Only play recorded narration in its actual language. Subtitles cover all locales.
	if lang not in ["en", "tr"]:
		return
	var vkey := lang + "/" + key
	if not _voices.has(vkey):
		var st0 := _load("res://assets/audio/voice/%s/%s.mp3" % [lang, key])
		if st0 == null and lang != "en":
			st0 = _load("res://assets/audio/voice/en/%s.mp3" % key)
		_voices[vkey] = st0
	var st: AudioStream = _voices[vkey]
	if st == null:
		return
	_voice_cd[key] = now + int(cooldown * 1000.0)
	_voice_global_cd = now + 2500
	_voice.stream = st
	_voice.play()
	# duck the music a little while someone talks
	if _music.playing:
		var t := create_tween()
		t.tween_property(_music, "volume_db", -22.0, 0.15)
		t.tween_interval(maxf(0.5, st.get_length()))
		t.tween_property(_music, "volume_db", -14.0, 0.4)


func set_voice(on: bool) -> void:
	voice_on = on
	if not on and _voice:
		_voice.stop()


## Late-game theme kicks in as the empire grows (crossfade between tracks).
func set_music_track(key: String) -> void:
	if key == _track or not _tracks.has(key):
		return
	_track = key
	if not music_on or not _music.playing:
		_music.stream = _tracks[key]
		return
	var t := create_tween()
	t.tween_property(_music, "volume_db", -40.0, 1.5)
	t.tween_callback(func():
		_music.stream = _tracks[key]
		_music.play())
	t.tween_property(_music, "volume_db", -14.0, 2.0)



func _notification(what: int) -> void:
	if what in [NOTIFICATION_APPLICATION_PAUSED, NOTIFICATION_APPLICATION_FOCUS_OUT]:
		suspend_audio(true)
	elif what in [NOTIFICATION_APPLICATION_RESUMED, NOTIFICATION_APPLICATION_FOCUS_IN]:
		suspend_audio(false)

func suspend_audio(value: bool) -> void:
	suspended = value
	for child in get_children():
		if child is AudioStreamPlayer:
			if child == _music: child.stream_paused = value
			elif value: child.stop()


## Lets go of every stream on the way out (the AudioServer playback outlives the player by a mix turn otherwise and the
## Ogg / MP3 resources are reported "still in use at exit"). Also called by tests/audio_exit_test.gd.
func release() -> void:
	for p in [_music, _engine, _voice] + _pool:
		if p != null and is_instance_valid(p):
			p.stop()
			p.stream = null
	_tracks.clear()
	_streams.clear()
	_voices.clear()


func _exit_tree() -> void:
	release()
