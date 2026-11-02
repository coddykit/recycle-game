// Ridgeback Rangers — one running career: data + Simulation + the kit save repository + autosave. Engine-free, so the relaunch
// / save-safety tests run under plain .NET and in the Unity EditMode runner.
//   * file: <persistentDataPath>/ridgebackrangers/career.json through CoddyKit.SaveRepository (tmp -> main, last GOOD save in
//     .bak, unreadable copies kept as .unreadable(.1-.7), never deleted) with SaveCodec.Decode as the validator;
//   * saves: every day end, after purchases / sales / rewards, every balance autosave_s of live play, on pause / quit;
//   * `save_unreadable` analytics event per rejected candidate (code + recovered 0/1, no PII) — fleet §14.
using System;
using System.Collections.Generic;
using System.IO;
using CoddyKit;
using RidgebackRangers.Sim;

namespace RidgebackRangers.Game
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

        public string SavePath => Path.Combine(Dir, SaveFile);

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
                Sim = SaveCodec.Decode(LastLoad.Text, Gd).Sim;
                if (LastLoad.Recovered) Save();
                return true;
            }
            Sim = new Simulation(Gd, seedIfNew);
            if (Settings.CoachDone) Sim.EndTutorial();   // guide already done on this device: a fresh career starts without it
            return false;
        }

        /// <summary>Atomic save of the whole career. "" = ok.</summary>
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

        /// <summary>Every frame with real seconds. Returns the drained sim events. Saves at day end and every autosave_s
        /// of live, unpaused play.</summary>
        public List<SimEvent> Tick(double realDt)
        {
            Sim.Advance(realDt);
            var ev = Sim.DrainEvents();
            bool dayDone = false;
            foreach (var e in ev) if (e.Type == "day_done" || e.Type == "research_done") dayDone = true;
            if (dayDone) Save();
            else if (Sim.Phase == Phases.Live && !Sim.S.Paused)
            {
                autosaveAcc += realDt;
                if (autosaveAcc >= Math.Max(5, Gd.AutosaveS)) Save();
            }
            return ev;
        }

        /// <summary>App going to the background / quitting.</summary>
        public void Pause() => Save();

        /// <summary>App back (or the first frame after load): the network's pay for the time away. Null = nothing earned.</summary>
        public OfflineSummary Resume()
        {
            var o = Sim.Offline(Now());
            if (o != null) Save();
            return o;
        }

        /// <summary>Store rights -> sim perks (never saved; after every load and every purchase change).</summary>
        public void ApplyPerks(Func<string, bool> has)
        {
            var p = new Dictionary<string, bool>();
            foreach (var id in Commerce.PerkEntitlements) p[id] = has != null && has(id);
            Sim.SetPerks(p);
        }
    }
}
