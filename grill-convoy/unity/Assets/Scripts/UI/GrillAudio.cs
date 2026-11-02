// Grill Convoy — sound (fleet §13): SFX + two music loops (calm plan / busy day), Settings → Music / Effects (both ON by
// default, persisted), silent in the background and in batch mode (headless tests / CI: no audio). Clips come from
// Resources/audio/<id> (ElevenLabs batch through games/tools/audio — only after the owner approves the credits; until
// then the game runs silently and every Play() is a cheap no-op).
//   sfx ids: ui_tap, truck, open, sizzle, bell, hiss, place, coins, success, warning   music: music_calm, music_busy
using System.Collections.Generic;
using UnityEngine;

namespace GrillConvoy.UI
{
    public sealed class GrillAudio : MonoBehaviour
    {
        public static readonly string[] Sfx = { "ui_tap", "truck", "open", "sizzle", "bell", "hiss", "place", "coins", "success", "warning" };
        public static readonly string[] Music = { "music_calm", "music_busy" };

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource music;
        bool musicOn, sfxOn, muted;
        string track = "";
        int next;

        public static GrillAudio Create(Transform parent, bool musicOn, bool sfxOn)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<GrillAudio>();
            a.muted = Application.isBatchMode;
            if (!a.muted)
            {
                foreach (var id in Sfx) { var c = Resources.Load<AudioClip>("audio/" + id); if (c != null) a.clips[id] = c; }
                foreach (var id in Music) { var c = Resources.Load<AudioClip>("audio/" + id); if (c != null) a.clips[id] = c; }
                for (int i = 0; i < 4; i++) { var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; a.pool.Add(s); }
                a.music = go.AddComponent<AudioSource>();
                a.music.loop = true;
                a.music.playOnAwake = false;
                a.music.volume = 0.45f;
            }
            a.Set(musicOn, sfxOn);
            return a;
        }

        public void Set(bool musicOn, bool sfxOn)
        {
            this.musicOn = musicOn;
            this.sfxOn = sfxOn;
            if (music == null) return;
            if (!musicOn) music.Stop();
            else PlayMusic(track == "" ? "music_calm" : track);
        }

        public void Play(string id)
        {
            if (muted || !sfxOn || string.IsNullOrEmpty(id) || pool.Count == 0 || !clips.TryGetValue(id, out var c)) return;
            var s = pool[next++ % pool.Count];
            s.PlayOneShot(c, id == "ui_tap" ? 0.5f : 0.9f);
        }

        public void PlayMusic(string id)
        {
            track = id;
            if (muted || music == null || !musicOn || !clips.TryGetValue(id, out var c)) return;
            if (music.clip == c && music.isPlaying) return;
            music.clip = c;
            music.Play();
        }

        void OnApplicationPause(bool paused)
        {
            if (music == null) return;
            if (paused) music.Pause(); else if (musicOn) music.UnPause();
        }
    }
}
