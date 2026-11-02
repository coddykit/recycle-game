# games/tools/audio — fleet SFX + music (ElevenLabs)

Every game ships SFX + music generated with this tool (games/CLAUDE.md §13). Manifest-driven, sequential, skip-if-exists.

```sh
python3 -m venv ~/.venvs/coddy-audio && ~/.venvs/coddy-audio/bin/pip install numpy soundfile   # once (ffmpeg needed too)
PY=~/.venvs/coddy-audio/bin/python
$PY games/tools/audio/gen.py games/<game> --dry-run     # missing assets + credit estimate
$PY games/tools/audio/gen.py games/<game>               # generate what is missing (one request at a time)
$PY games/tools/audio/gen.py games/<game> --only id1,id2 --force   # regenerate (new API call)
$PY games/tools/audio/gen.py games/<game> --reprocess   # post-process again from the raw cache, no API
$PY games/tools/audio/gen.py games/<game> --status      # QA table → audio/qa.json
```

- Key: `~/.config/elevenlabs/api_key` (Mac Mini, chmod 600) or `$ELEVENLABS_API_KEY`. Never printed/logged/committed.
- Before a batch the subscription is read; it stops when remaining credits would drop below `--floor` (15 %).
  Real cost per call = response header `character-cost`, appended to `audio/usage_log.jsonl`.
  Measured 2026-10-21 (Starter): sound generation 10 credits/s, music 750 credits per minute (no cost header: logged
  at that rate). One game (~30 SFX + 2×60 s music + 22 s ambience) ≈ 2,100–2,350 credits, ~2 MB of Ogg.
- 429 → wait 60 s and retry; 5xx/network → backoff. `--gap` seconds between calls (default 1.5).
- Raw MP3s: `$CODDY_AUDIO_RAW/<game>/<id>.<spec-hash>.mp3` (default `/Volumes/BuildSSD/game-deps/audio-raw`), outside git.
- Ogg Vorbis is written with libsndfile (`soundfile`), because Homebrew ffmpeg has no libvorbis.
- Paid generation: ask the owner before large batches (general rule); this pass was approved 2026-10-21.

## Output (Godot `res://audio/…`)
`games/<game>/audio/{sfx,music,ambience}/<id>.ogg`, `audio/manifest.json`, `audio/qa.json`, `audio/usage_log.jsonl`,
`audio/INTEGRATION.md` (event → id map).

| kind | channels | loudness | Vorbis q | processing |
|---|---|---|---|---|
| sfx | mono | −14 LUFS, true peak ≤ −1.5 dBTP (≤ 4 dB limiting; very short clicks end up quieter) | 4 | trim silence, 3 ms / ≤60 ms fades |
| music | stereo | −18 LUFS, TP ≤ −1.5 | 3 | loop: drop fade-in/out, 3 s equal-power seam crossfade |
| ambience | stereo | −24 LUFS, TP ≤ −3 | 2 | ElevenLabs `loop=true` + 1.5 s seam crossfade |

## manifest.json
```json
{
  "game": "chaiwala-rush",
  "style": {"sfx": "appended to every sfx prompt", "music": "appended to every music prompt", "ambience": "…"},
  "assets": [
    {"id": "ui_tap", "kind": "sfx", "duration": 0.5, "prompt": "…", "influence": 0.6, "event": "any button press"},
    {"id": "music_calm", "kind": "music", "duration": 60, "loop": true, "prompt": "…", "event": "build/menu"},
    {"id": "amb_street", "kind": "ambience", "duration": 22, "loop": true, "prompt": "…"}
  ]
}
```
Fields: `id` lower_snake (file name), `kind`, `duration` s (sfx/ambience 0.5–30, music 10–300), `prompt`,
optional `loop`, `influence` (sfx prompt influence 0–1, default 0.5), `lufs` / `tp` / `q` overrides, `max_len` (cap after
trim), `event` (free text: where the game plays it), `no_style` (do not append the game style).
Write prompts for a healthy level: "very soft / faint / quiet" makes ElevenLabs render near-silent takes (raw peak −45 dBFS)
that need +40 dB of gain; say how gentle the sound is, not how loud — the game sets the volume. Changing prompt/duration/loop/influence changes the raw hash.

## Integration rules (Godot)
Autoload `Audio` with `Music` and `SFX` buses (default_bus_layout.tres), small player pool, per-id min gap, music
streamed (`AudioStreamOggVorbis`, `loop = true` at runtime), Settings toggles Music / Sound (default on, persisted),
pause/duck on `NOTIFICATION_APPLICATION_PAUSED` / focus out, and **no playback when `DisplayServer.get_name() == "headless"`**
(tests stay silent; the Mac Mini is always muted). No per-frame allocations.

The ten factory games share `godot/Audio.gd` byte for byte. Run
`python3 games/tools/audio/check_copies.py` from a checkout containing those games
after syncing the template. Their `tests/audio.gd` suites also exercise focus loss
and resume before `_ready()` has created the players; keep the game's constants
when copying shared test cases. Airport and Corrupt use independent audio systems.
The service hooks (`_on_reward_granted`, `_on_store_notice`) take untyped, optional arguments (10-27): a game's
`reward_granted` / `notice` signal may carry other types or another count (Driftwood: a gift Dictionary). Check with
`godot --headless --audio-driver Dummy --path games/<game> -s <repo>/games/tools/audio/godot/test_reward_args.gd`.
