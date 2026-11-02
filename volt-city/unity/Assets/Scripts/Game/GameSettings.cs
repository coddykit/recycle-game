// Volt City — player settings (engine-free). A kit JsonStateFile (atomic tmp -> main, .bak, .unreadable copies) next to
// the career save. Onboarding state lives HERE, never in "a save exists" (LL Driftwood P7): intro_done (pager seen),
// coach_done (guide finished or skipped), coach_step (resume point; a relaunch mid-guide re-derives the step, never locks).
// The graphics tier is the kit's (QualityTiers keeps graphics.json); this file only remembers that the first-start
// popup was answered.
using System.Collections.Generic;
using System.IO;
using CoddyKit;

namespace VoltCity.Game
{
    public sealed class GameSettings
    {
        public const string FileName = "settings.json";
        readonly JsonStateFile file;
        Dictionary<string, object> D => file.Data;

        GameSettings(string path) { file = new JsonStateFile(path).Load(); }

        public static GameSettings Load(string dir) => new GameSettings(Path.Combine(dir, FileName));

        public string Save() => file.Save();

        string S(string k, string def) => D.TryGetValue(k, out var v) && v is string s ? s : def;
        bool B(string k, bool def) => D.TryGetValue(k, out var v) && v is bool b ? b : def;
        int I(string k, int def) => D.ContainsKey(k) ? MiniJson.Int(D, k, def) : def;

        /// <summary>"" = follow the device language (first start); else one of Loc.Languages.</summary>
        public string Lang { get => S("lang", ""); set => D["lang"] = value ?? ""; }
        /// <summary>Music is OFF until the player turns it on (P4 brief: music-off-by-default; effects stay on).</summary>
        public bool Music { get => B("music", false); set => D["music"] = value; }
        public bool Effects { get => B("sfx", true); set => D["sfx"] = value; }
        /// <summary>Speed a new shift starts at (1/2/3; 4 only while the speed perk is owned — the sim re-checks).</summary>
        public int SpeedDefault { get => I("speed", 1); set => D["speed"] = (long)value; }
        public bool IntroDone { get => B("intro_done", false); set => D["intro_done"] = value; }
        public bool CoachDone { get => B("coach_done", false); set => D["coach_done"] = value; }
        public int CoachStep { get => I("coach_step", 0); set => D["coach_step"] = (long)value; }
        public int WrongTaps { get => I("wrong_taps", 0); set => D["wrong_taps"] = (long)value; }
        public bool GraphicsAsked { get => B("graphics_asked", false); set => D["graphics_asked"] = value; }
        public int Sessions { get => I("sessions", 0); set => D["sessions"] = (long)value; }
        /// <summary>A first-session review moment that happened inside the guide (ReviewHook.Deferred).</summary>
        public string ReviewDeferred { get => S("review_deferred", ""); set => D["review_deferred"] = value ?? ""; }

        // ---- P4: money + services
        /// <summary>Unix time of the first launch (offer day windows: starter day 3-7, bundle from day 20).</summary>
        public long FirstSeen { get => D.ContainsKey("first_seen") ? MiniJson.Long(D, "first_seen") : 0; set => D["first_seen"] = value; }
        /// <summary>Active city slot 1..4 (slot 1 = career.json; Extra City Slots unlock 2-4).</summary>
        public int Slot { get => System.Math.Max(1, I("slot", 1)); set => D["slot"] = (long)value; }
        /// <summary>Rush Hour Speed owners: collect the shift report by itself.</summary>
        public bool AutoCollect { get => B("auto_collect", false); set => D["auto_collect"] = value; }

        /// <summary>Equipped looks: slot (truck / room / pylons / blades / skyline) -> look entitlement or "".</summary>
        public Dictionary<string, string> Looks
        {
            get
            {
                var m = new Dictionary<string, string>();
                if (MiniJson.Obj(D, "looks") is Dictionary<string, object> o)
                    foreach (var kv in o) if (kv.Value is string v) m[kv.Key] = v;
                return m;
            }
            set
            {
                var o = new Dictionary<string, object>();
                if (value != null) foreach (var kv in value) o[kv.Key] = kv.Value ?? "";
                D["looks"] = o;
            }
        }

        public OfferRecord OfferRecord
        {
            get => OfferRecord.FromJson(MiniJson.Obj(D, "offers"));
            set => D["offers"] = value?.ToJson() ?? new Dictionary<string, object>();
        }

        /// <summary>Context hints already shown (each once).</summary>
        public bool HintSeen(string id) => MiniJson.Arr(D, "hints") is List<object> l && l.Contains(id);

        public void MarkHint(string id)
        {
            if (!(MiniJson.Arr(D, "hints") is List<object> l)) { l = new List<object>(); D["hints"] = l; }
            if (!l.Contains(id)) l.Add(id);
        }

        /// <summary>Settings → "How to play / Replay guide": pager + coach again (the career is not touched).</summary>
        public void ReplayGuide()
        {
            IntroDone = false;
            CoachDone = false;
            CoachStep = 0;
        }
    }
}
