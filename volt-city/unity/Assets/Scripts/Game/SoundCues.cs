// Volt City P4 — what plays when (engine-free; VoltAudio plays it). NO paid generation: every file is an existing fleet
// asset (docs/AUDIO.md: Disaster Command's calm "command-room" set, made 2026-10-21 with the fleet's commercial ElevenLabs
// plan) under Assets/Resources/audio/<id>.ogg.
//   * buses: sfx (Effects toggle, default on) · music (Music toggle, default OFF) · amb (follows Music);
//   * mixer limits by graphics tier: Low = 3 SFX voices, no ambience bed, music streamed; Medium 6; High 8;
//   * every cue has a minimum gap (no machine-gun coins) and a priority (a busy pool drops the lowest);
//   * music: calm, or "rush" while a storm / heatwave runs or the needle is red in a live shift.
using System;
using System.Collections.Generic;
using VoltCity.Sim;

namespace VoltCity.Game
{
    public sealed class SoundCue
    {
        public string Id = "", Bus = "sfx";
        public float VolumeDb, MinGapS;
        public int Priority;
        public bool Loop;
    }

    public struct MixerLimits
    {
        public int Voices;
        public bool Ambience;
        public float MusicDb, SfxDb, AmbDb;
    }

    public static class SoundCues
    {
        static SoundCue C(string id, float db, float gap, int prio, string bus = "sfx", bool loop = false) =>
            new SoundCue { Id = id, VolumeDb = db, MinGapS = gap, Priority = prio, Bus = bus, Loop = loop };

        public static readonly SoundCue[] All =
        {
            C("ui_tap", -4, 0.08f, 0), C("ui_open", -2, 0.2f, 1), C("ui_close", -4, 0.2f, 1), C("ui_confirm", 0, 0.15f, 2),
            C("ui_error", -2, 0.4f, 2), C("purchase", 1, 1.0f, 5), C("coin", -6, 0.3f, 2), C("reward", 0, 1.0f, 4),
            C("upgrade", 0, 0.3f, 3), C("level_up", 0, 1.0f, 4), C("wow", 0, 3.0f, 5), C("alert", -3, 1.5f, 3),
            C("fail", -2, 1.5f, 3), C("day_end", 0, 1.5f, 3), C("unlock", -1, 2.0f, 3), C("op_start", 0, 1.0f, 3),
            C("dispatch_send", -3, 0.15f, 2), C("vehicle_delivered", -3, 0.5f, 2), C("rescue_done", -1, 0.5f, 3),
            C("chaos_blackout", -3, 1.0f, 3), C("all_clear", -2, 1.0f, 3), C("city_expand", 0, 1.0f, 4),
            C("music_calm", -10, 0, 9, "music", true), C("music_rush", -10, 0, 9, "music", true), C("amb_command_room", -18, 0, 9, "amb", true),
        };

        static Dictionary<string, SoundCue> byId;

        public static SoundCue Get(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, SoundCue>();
                foreach (var c in All) byId[c.Id] = c;
            }
            return id != null && byId.TryGetValue(id, out var x) ? x : null;
        }

        /// <summary>tier: 0 Low, 1 Medium, 2 High (kit QualityTiers order).</summary>
        public static MixerLimits Limits(int tier) => new MixerLimits
        {
            Voices = tier <= 0 ? 3 : tier == 1 ? 6 : 8,
            Ambience = tier > 0,
            MusicDb = 0, SfxDb = 0, AmbDb = tier > 0 ? 0 : -80,
        };

        /// <summary>The cue for a sim event ("" = silent).</summary>
        public static string ForSim(SimEvent e)
        {
            switch (e.Type)
            {
                case "shift_start": return "op_start";
                case "event_start": return HardEvent(e.Str("id")) ? "alert" : "";
                case "fault": return "chaos_blackout";
                case "crew_sent": return "dispatch_send";
                case "crew_arrived": return "vehicle_delivered";
                case "repaired": return "rescue_done";
                case "restore": return J.B(e.Data, "auto") ? "" : "all_clear";
                case "backup_on": case "plant_trip": case "line_trip": return "alert";
                case "import": return "unlock";
                case "shift_done":
                {
                    long stars = e.Num("stars");
                    return stars >= 3 ? "wow" : stars == 2 ? "level_up" : stars <= 0 ? "fail" : "day_end";
                }
                case "chapter": return "unlock";
                case "bought": return "upgrade";
                case "hooked_up": return "city_expand";
                case "offline": return "coin";
                case "reward": return "reward";
                case "purchase_funds": return "purchase";
            }
            return "";
        }

        /// <summary>The cue for a player action's result (the plain tap sound comes from the kit button hook).</summary>
        public static string ForAction(string id, bool ok, string reason)
        {
            if (!ok) return string.IsNullOrEmpty(reason) || reason == "unknown" ? "" : "ui_error";
            if (id == "collect") return "coin";
            if (id != null && id.StartsWith("shift:start")) return "ui_confirm";
            return "";
        }

        /// <summary>Which music loop fits the moment: rush while a hard event runs (storm / heatwave / cold snap, tracked
        /// from event_start / event_end) or the needle is in the red band of a live shift; calm otherwise.</summary>
        public static string MusicFor(Simulation sim, bool hardEventRunning)
        {
            if (sim == null || sim.Phase != "live" || sim.Grid == null) return "music_calm";
            if (hardEventRunning || Hud.Band(sim.Grid.NeedlePm, sim.Gd) == NeedleBand.Red) return "music_rush";
            return "music_calm";
        }

        public static bool HardEvent(string id) => id == "thunderstorm" || id == "heatwave" || id == "cold_snap";
    }
}
