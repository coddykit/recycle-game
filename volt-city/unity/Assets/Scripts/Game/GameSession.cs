// Volt City — one running career: data + Simulation + the kit save repository + autosave (P3; SIM-API section 6).
// Engine-free so the relaunch / save-safety tests run under plain .NET today and in the Unity EditMode runner later.
//   * file: <persistentDataPath>/voltcity/career.json through CoddyKit.SaveRepository (tmp -> main, last GOOD save in .bak,
//     unreadable copies kept as .unreadable(.1-.7), never deleted) with SaveCodec.Decode as the validator;
//   * saves: every shift end, after Collect / Buy / Hookup / rewards, every balance.time.autosave_s (30 s) of live play,
//     on pause / quit (the Unity layer calls Pause());
//   * `save_unreadable` analytics event per rejected candidate (code + recovered 0/1, no PII) — games/CLAUDE.md section 14.
using System;
using System.Collections.Generic;
using System.IO;
using CoddyKit;
using VoltCity.Sim;

namespace VoltCity.Game
{
    public sealed class GameSession
    {
        public const string SaveFile = "career.json";

        public readonly GameData Gd;
        public readonly string Dir;
        public readonly GameSettings Settings;
        public Simulation Sim { get; private set; }
        public Func<long> Now = UnixNow;
        public Action<string, Dictionary<string, object>> Analytics = (n, p) => { };
        public LoadResult LastLoad { get; private set; }
        public string LastSaveError { get; private set; } = "";
        public int SaveCount { get; private set; }
        double autosaveAcc;

        /// <summary>The active city slot's career file (P4 Extra City Slots: slot 1 = career.json, 2-4 = career_N.json).</summary>
        public string SavePath => Path.Combine(Dir, SlotFile(Settings.Slot));

        public static string SlotFile(int slot) => slot <= 1 ? SaveFile : "career_" + slot + ".json";

        /// <summary>(exists, shift number, cash) of a slot's last good save — the Cities sheet (same SaveRepository recovery rules as a load).</summary>
        public (bool ok, int shift, long cash) SlotInfo(int slot)
        {
            if (slot == Settings.Slot && Sim != null) return (true, Sim.S.ShiftNo, Sim.S.Cash);
            var r = SaveRepository.Load(Path.Combine(Dir, SlotFile(slot)), Validate);
            if (!r.Ok) return (false, 0, 0);
            var d = SaveCodec.Decode(r.Text, Gd);
            return d.Ok ? (true, d.Sim.S.ShiftNo, d.Sim.S.Cash) : (false, 0, 0);
        }

        public static long UnixNow() => (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        public GameSession(GameData gd, string dir)
        {
            Gd = gd ?? throw new ArgumentNullException(nameof(gd));
            Dir = dir;
            Directory.CreateDirectory(dir);
            Settings = GameSettings.Load(dir);
        }

        /// <summary>SaveRepository validator: null = a save the sim accepts, else the decoder's error code.</summary>
        public string Validate(string text)
        {
            var r = SaveCodec.Decode(text, Gd);
            return r.Ok ? null : (string.IsNullOrEmpty(r.Error) ? "decode_failed" : r.Error);
        }

        /// <summary>Loads the career (main -> tmp -> bak) or starts a new one with this seed. True when a save was loaded.</summary>
        public bool LoadOrNew(long seedIfNew)
        {
            if (Settings.FirstSeen <= 0) { Settings.FirstSeen = Now(); Settings.Save(); }
            LastLoad = SaveRepository.Load(SavePath, Validate);
            foreach (var code in LastLoad.Unreadable)
                Analytics("save_unreadable", new Dictionary<string, object> { { "code", code }, { "recovered", LastLoad.Ok ? 1L : 0L } });
            if (LastLoad.Ok)
            {
                var r = SaveCodec.Decode(LastLoad.Text, Gd);
                Sim = r.Sim;
                if (LastLoad.Recovered) Save(); // the recovered copy becomes the main file again
                return true;
            }
            Sim = new Simulation(Gd, seedIfNew);
            return false;
        }

        /// <summary>Atomic save of the whole career (live shifts included: plan + grid snapshot + command log). "" = ok.</summary>
        public string Save()
        {
            if (Sim == null) return "no_career";
            Sim.Touch(Now());
            LastSaveError = SaveRepository.Write(SavePath, SaveCodec.Encode(Sim), Validate);
            SaveCount++;
            autosaveAcc = 0;
            if (LastSaveError != "") Analytics("save_failed", new Dictionary<string, object> { { "code", LastSaveError.Split(':')[0] } });
            return LastSaveError;
        }

        /// <summary>Every frame: real seconds. Returns what happened (drained sim events). Saves at shift end and every
        /// autosave_s of live, unpaused play.</summary>
        public List<SimEvent> Tick(double realDt)
        {
            Sim.Advance(realDt);
            var ev = Sim.DrainEvents();
            bool shiftDone = false;
            foreach (var e in ev) if (e.Type == "shift_done") shiftDone = true;
            if (shiftDone) Save();
            else if (Sim.Phase == "live" && !Sim.S.Paused)
            {
                autosaveAcc += realDt;
                if (autosaveAcc >= Math.Max(5, Gd.AutosaveS)) Save();
            }
            return ev;
        }

        // ------------------------------------------------------------------------------------------- saved actions

        public bool StartShift()
        {
            if (!Sim.StartShift()) return false;
            Sim.SetSpeed(Math.Max(1, Settings.SpeedDefault));
            Save();
            return true;
        }

        public long Collect()
        {
            long amount = Sim.Collect();
            if (Sim.Phase == "idle") Save();
            return amount;
        }

        public bool Buy(string id)
        {
            if (!Sim.Buy(id)) return false;
            Save();
            return true;
        }

        public bool Hookup(string id)
        {
            if (!Sim.Hookup(id)) return false;
            Save();
            return true;
        }

        // ------------------------------------------------------------------------------------------- lifecycle

        /// <summary>App going to the background / quitting: remember when, save.</summary>
        public void Pause() => Save();

        /// <summary>App back (or first frame after load): night-crew pay for the time away. Null when nothing was earned.</summary>
        public OfflineSummary Resume()
        {
            var o = Sim.Offline(Now());
            if (o != null) Save();
            return o;
        }

        /// <summary>Store rights -> sim perks (never saved; after every load and every purchase change). P4 maps the RC
        /// entitlement ids; P3 passes whatever the kit cache says.</summary>
        public void ApplyPerks(Func<string, bool> has)
        {
            var p = new Dictionary<string, bool>();
            foreach (var id in new[] { "premium", "offline_plus", "speed4", "dispatcher", "forecast_plus" }) p[id] = has != null && has(id);
            Sim.SetCommercePerks(p);
        }
    }
}
