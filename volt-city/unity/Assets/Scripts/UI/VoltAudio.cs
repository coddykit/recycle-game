// Volt City P4 — sound playback (SoundCues decides WHAT, this plays it). No AudioMixer asset (it cannot be authored without
// the editor): three code buses with their own gain — sfx (Settings "Effects", default on), music + ambience (Settings
// "Music", default OFF) — and the tier limits from SoundCues.Limits: Low = 3 SFX voices, no ambience bed; Medium 6; High 8.
// One-shots share a small AudioSource pool (oldest / lowest-priority voice is stolen; a cue never repeats inside its
// MinGapS); music + ambience are two looping sources with a 1 s cross-fade between calm and rush. Clips load lazily from
// Resources/audio/<id> (import settings: Editor/VoltCity/VoltAudioImport.cs — music streamed, SFX decompressed mono).
// Paused with the app (AudioListener.pause on focus loss) and silent in batch mode / tests. UNCOMPILED until the editor.
using System.Collections.Generic;
using UnityEngine;
using VoltCity.Game;

namespace VoltCity.UI
{
    public sealed class VoltAudio : MonoBehaviour
    {
        public bool Effects = true;
        public bool Music;
        MixerLimits limits = SoundCues.Limits(1);
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly List<int> poolPrio = new List<int>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        AudioSource musicA, musicB, amb;
        string musicId = "";
        float fade = 1f;
        bool silent;

        public static VoltAudio Create(Transform parent, int tier, bool effects, bool music)
        {
            var go = new GameObject("VoltAudio");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<VoltAudio>();
            a.silent = Application.isBatchMode;
            a.SetTier(tier);
            a.Effects = effects;
            a.SetMusic(music);
            return a;
        }

        public void SetTier(int tier)
        {
            limits = SoundCues.Limits(tier);
            while (pool.Count > limits.Voices) { Destroy(pool[pool.Count - 1]); pool.RemoveAt(pool.Count - 1); poolPrio.RemoveAt(poolPrio.Count - 1); }
            if (!limits.Ambience && amb != null) amb.Stop();
        }

        AudioClip Clip(string id)
        {
            if (clips.TryGetValue(id, out var c)) return c;
            c = Resources.Load<AudioClip>("audio/" + id);
            clips[id] = c;
            if (c == null) Debug.LogWarning("[VoltAudio] missing clip " + id);
            return c;
        }

        static float Gain(float db) => Mathf.Pow(10f, db / 20f);

        /// <summary>One-shot SFX by cue id ("" = nothing).</summary>
        public void Play(string id)
        {
            if (silent || !Effects || string.IsNullOrEmpty(id)) return;
            var cue = SoundCues.Get(id);
            if (cue == null || cue.Bus != "sfx") return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out float last) && now - last < cue.MinGapS) return;
            var clip = Clip(id);
            if (clip == null) return;
            int slot = -1;
            for (int i = 0; i < pool.Count; i++) if (!pool[i].isPlaying) { slot = i; break; }
            if (slot < 0 && pool.Count < limits.Voices)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                pool.Add(src);
                poolPrio.Add(0);
                slot = pool.Count - 1;
            }
            if (slot < 0)
            {
                // steal the lowest-priority voice, never one above this cue
                int low = 0;
                for (int i = 1; i < pool.Count; i++) if (poolPrio[i] < poolPrio[low]) low = i;
                if (poolPrio[low] > cue.Priority) return;
                slot = low;
            }
            lastPlayed[id] = now;
            var s = pool[slot];
            s.Stop();
            s.clip = clip;
            s.volume = Gain(cue.VolumeDb + limits.SfxDb);
            poolPrio[slot] = cue.Priority;
            s.Play();
        }

        public void SetMusic(bool on)
        {
            Music = on;
            if (!on)
            {
                musicA?.Stop();
                musicB?.Stop();
                amb?.Stop();
                musicId = "";
            }
        }

        AudioSource Loop(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        /// <summary>Every frame from VoltCityMain: which loop fits now (SoundCues.MusicFor). Cross-fades in 1 s.</summary>
        public void Tick(string wanted, float dt)
        {
            if (silent || !Music) return;
            if (musicA == null) { musicA = Loop("MusicA"); musicB = Loop("MusicB"); amb = Loop("Ambience"); }
            if (limits.Ambience && !amb.isPlaying)
            {
                amb.clip = Clip("amb_command_room");
                if (amb.clip != null) { amb.volume = Gain(SoundCues.Get("amb_command_room").VolumeDb + limits.AmbDb); amb.Play(); }
            }
            if (wanted != musicId)
            {
                musicId = wanted;
                (musicA, musicB) = (musicB, musicA);   // A = the new loop, B fades out
                musicA.clip = Clip(wanted);
                if (musicA.clip != null) { musicA.volume = 0f; musicA.Play(); }
                fade = 0f;
            }
            if (fade < 1f)
            {
                fade = Mathf.Min(1f, fade + dt);
                float g = Gain((SoundCues.Get(musicId)?.VolumeDb ?? -10f) + limits.MusicDb);
                musicA.volume = g * fade;
                musicB.volume = g * (1f - fade);
                if (fade >= 1f) musicB.Stop();
            }
        }

        void OnApplicationFocus(bool focus) { AudioListener.pause = !focus && !silent; }
    }
}
