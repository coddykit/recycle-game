// Pour & Roast — player settings (engine-free). A kit JsonStateFile (atomic tmp -> main, .bak, .unreadable copies) next to
// the career save. Onboarding state lives HERE, never in "a save exists" (fleet lesson, Driftwood P7): intro_done (pager
// seen), coach_done (guide finished or skipped), coach_step (resume point; a relaunch mid-guide re-derives the step).
using System.Collections.Generic;
using System.IO;
using CoddyKit;

namespace PourAndRoast.Game
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
        long L(string k) => D.ContainsKey(k) ? MiniJson.Long(D, k) : 0;

        /// <summary>"" = follow the device language; else one of Loc.Languages.</summary>
        public string Lang { get => S("lang", ""); set => D["lang"] = value ?? ""; }
        /// <summary>Fleet §13: Music / Effects toggles, both ON by default, persisted.</summary>
        public bool Music { get => B("music", true); set => D["music"] = value; }
        public bool Effects { get => B("sfx", true); set => D["sfx"] = value; }
        public int SpeedDefault { get => I("speed", 1); set => D["speed"] = (long)value; }
        public bool IntroDone { get => B("intro_done", false); set => D["intro_done"] = value; }
        public bool CoachDone { get => B("coach_done", false); set => D["coach_done"] = value; }
        public int CoachStep { get => I("coach_step", 0); set => D["coach_step"] = (long)value; }
        public int Sessions { get => I("sessions", 0); set => D["sessions"] = (long)value; }
        public long FirstSeen { get => L("first_seen"); set => D["first_seen"] = value; }
        public long LastOfferAt { get => L("last_offer_at"); set => D["last_offer_at"] = value; }
        public string ReviewDeferred { get => S("review_deferred", ""); set => D["review_deferred"] = value ?? ""; }
        /// <summary>Speed Pack owners: skip the day report by itself.</summary>
        public bool AutoSkipReport { get => B("auto_report", false); set => D["auto_report"] = value; }
        /// <summary>Equipped truck livery ("" = default; premium gold / cosmetic liveries).</summary>
        public string Livery { get => S("livery", ""); set => D["livery"] = value ?? ""; }

        public bool Flag(string id) => MiniJson.Arr(D, "flags") is List<object> l && l.Contains(id);

        public void SetFlag(string id)
        {
            if (!(MiniJson.Arr(D, "flags") is List<object> l)) { l = new List<object>(); D["flags"] = l; }
            if (!l.Contains(id)) l.Add(id);
        }

        /// <summary>Context hints already shown (each once).</summary>
        public bool HintSeen(string id) => Flag("hint:" + id);
        public void MarkHint(string id) => SetFlag("hint:" + id);

        /// <summary>Consumable transactions already delivered (idempotent delivery; also kept by the kit ledger).</summary>
        public bool Delivered(string txn) => Flag("txn:" + txn);
        public void MarkDelivered(string txn) => SetFlag("txn:" + txn);

        /// <summary>Settings -> "How to play / Replay the guide": pager + coach again (the career is not touched).</summary>
        public void ReplayGuide()
        {
            IntroDone = false;
            CoachDone = false;
            CoachStep = 0;
        }
    }
}
