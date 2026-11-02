// Volt City — what the player is told when the sim says something happened (SimEvent -> toast / card / hint / review),
// engine-free. Toasts: <= 2 lines, queue compresses (kit ToastQueue). Event cards only for the events with a counterplay
// worth reading (Cards.EventGetsCard); never while the guide runs (TutorialSafe shifts are calm anyway).
using System;
using System.Collections.Generic;
using VoltCity.Sim;

namespace VoltCity.Game
{
    public sealed class FeedItem
    {
        public string Toast = "";
        public string Kind = "info";      // info | success | warning | danger
        /// <summary>Event id whose card should open ("" = none).</summary>
        public string Card = "";
        /// <summary>Storm lightning flash in the diorama.</summary>
        public bool Lightning;
        /// <summary>A review moment from the sim (reason) and whether the sim called it ready (play time + not the guide).</summary>
        public string ReviewMoment = "";
        public bool ReviewReady;
        /// <summary>A success the player made happen (lights back on, good shift, hookup): a deferred review moment may use it.</summary>
        public bool Success;
        /// <summary>Sound cue id (P4 audio manifest).</summary>
        public string Sfx = "";
    }

    public static class Feed
    {
        public static FeedItem From(SimEvent e, Simulation sim, Loc loc, bool guide)
        {
            var it = new FeedItem();
            var gd = sim.Gd;
            switch (e.Type)
            {
                case "fault":
                {
                    string d = e.Str("district");
                    if (d == "") d = Names.CorridorDistrict(gd, e.Str("corridor"));
                    it.Toast = loc.F(e.Str("kind") == "line" ? "toast.fault_line" : "toast.fault_tr", "district", Names.District(loc, d));
                    it.Kind = "danger";
                    it.Sfx = "spark";
                    it.Lightning = e.Str("cause") == "thunderstorm" || e.Str("cause") == "tutorial";
                    break;
                }
                case "repaired":
                    it.Toast = loc.F("toast.back_on", "district", Names.District(loc, e.Str("district")));
                    it.Kind = "success";
                    it.Sfx = "lights_on";
                    it.Success = true;
                    break;
                case "shed":
                    it.Toast = loc.F(J.B(e.Data, "auto") ? "toast.auto_shed" : "toast.hand_shed", "district", Names.District(loc, e.Str("district")));
                    it.Kind = J.B(e.Data, "auto") ? "danger" : "warning";
                    it.Sfx = "lights_off";
                    break;
                case "restore":
                    it.Toast = loc.F("toast.back_on", "district", Names.District(loc, e.Str("district")));
                    it.Kind = "success";
                    it.Sfx = "lights_on";
                    it.Success = !J.B(e.Data, "auto");
                    break;
                case "backup_on":
                    it.Toast = loc.T("toast.backup_on");
                    it.Kind = "warning";
                    break;
                case "plant_trip":
                    it.Toast = loc.F("toast.trip", "plant", Names.Plant(loc, e.Str("plant")));
                    it.Kind = "danger";
                    it.Sfx = "trip";
                    break;
                case "plant_runback":
                    it.Toast = loc.F("toast.runback", "plant", Names.Plant(loc, e.Str("plant")));
                    it.Kind = "warning";
                    break;
                case "line_trip":
                    it.Toast = loc.F("toast.line_trip", "district", Names.District(loc, Names.CorridorDistrict(gd, e.Str("corridor"))));
                    it.Kind = "danger";
                    break;
                case "crew_arrived":
                    it.Sfx = "truck";
                    break;
                case "dr":
                    it.Toast = loc.T("toast.dr");
                    it.Kind = "success";
                    break;
                case "event_start":
                {
                    string id = e.Str("id");
                    if (!guide && Cards.EventGetsCard(id)) it.Card = id;
                    else it.Toast = Names.EventTitle(loc, id);
                    it.Lightning = id == "thunderstorm";
                    it.Kind = "warning";
                    break;
                }
                case "hooked_up":
                    it.Toast = loc.F("toast.hooked", "district", Names.District(loc, e.Str("district")));
                    it.Kind = "success";
                    it.Sfx = "lights_on";
                    it.Success = true;
                    break;
                case "chapter":
                    it.Toast = loc.F("toast.chapter", "n", e.Num("chapter"), "name", loc.T("chapter." + e.Num("chapter") + ".t"));
                    it.Kind = "success";
                    break;
                case "chapter_done":
                    it.Toast = loc.T("toast.city_done");
                    it.Kind = "success";
                    break;
                case "review_moment":
                    it.ReviewMoment = e.Str("reason");
                    it.ReviewReady = J.B(e.Data, "ready") && !guide;
                    break;
                case "shift_done":
                    it.Sfx = "shift_end";
                    it.Success = e.Num("stars") >= 2;
                    break;
            }
            return it;
        }
    }

    /// <summary>Context hints, each once, never during the guide, one at a time (DESIGN section 9).</summary>
    public static class Hints
    {
        public static readonly string[] Ids = { "battery", "forecast", "shed", "dr", "heatwave", "overheat", "smart_sub", "dispatcher", "offline", "shop", "speed" };

        /// <summary>The first hint whose moment is now and which was not shown yet ("" = none).</summary>
        public static string Next(Simulation sim, GameSettings st, List<SimEvent> events, string openTab, int sessions)
        {
            if (!st.CoachDone) return "";
            var s = sim.S;
            var g = sim.Grid;
            bool Seen(string id) => st.HintSeen(id);
            if (!Seen("battery") && s.Level("battery") > 0 && openTab == "grid") return "battery";
            if (!Seen("forecast") && s.Phase == "idle" && s.ShiftNo >= 2) return "forecast";
            if (!Seen("shed") && g != null && g.NeedlePm <= -40) return "shed";
            if (!Seen("dr") && s.Dr > 0 && openTab == "grid") return "dr";
            foreach (var e in events)
            {
                if (e.Type == "event_start" && e.Str("id") == "heatwave" && !Seen("heatwave")) return "heatwave";
                if (e.Type == "bought" && e.Str("id").StartsWith("smart_sub") && !Seen("smart_sub")) return "smart_sub";
                if (e.Type == "bought" && e.Str("id").StartsWith("auto_") && !Seen("dispatcher")) return "dispatcher";
                if (e.Type == "offline" && !Seen("offline")) return "offline";
            }
            if (!Seen("overheat") && g != null)
                for (int c = 0; c < g.CHeat.Length; c++)
                    if (g.CHeat[c] >= g.Gd.LineWarnPct * 10000) return "overheat";
            if (!Seen("shop") && sessions >= 2 && s.Phase == "idle") return "shop";
            // the mid-day lull (#183 play diagnostics: 60-90 s with nothing to decide in most shifts): once, the speed pill
            if (!Seen("speed") && g != null && s.Phase == "live" && s.Speed == 1 && s.ShiftNo >= 2 && Math.Abs(g.NeedlePm) <= g.Gd.BandPct * 10
                && g.HourOfDay >= 10.0 && g.HourOfDay < 15.0 && g.OpenFaults().Count == 0) return "speed";
            return "";
        }
    }

    /// <summary>The sim's review moments -> the kit's ReviewGate (OS sheet only; games/CLAUDE.md section 12).
    /// The first-session moments (first lights back on ~15 s, first shift done ~4 min) happen INSIDE the guide, where
    /// asking is forbidden: such a moment is DEFERRED (kept in the settings) and used at the player's next own success
    /// after the guide (a repaired fault, a hand restore, a >= 2-star shift, a hookup). A ready moment waits while a
    /// window / sheet is open (asked when it closes, within 60 s). The kit gate still checks play time, asks, gap.</summary>
    public sealed class ReviewHook
    {
        public string Pending { get; private set; } = "";
        /// <summary>Moment blocked by the guide / play time, waiting for the next success (persist it: GameSettings).</summary>
        public string Deferred = "";
        double age;

        public void Offer(string moment, bool ready)
        {
            if (string.IsNullOrEmpty(moment)) return;
            if (ready) { Pending = moment; age = 0; }
            else if (Deferred == "") Deferred = moment;
        }

        /// <summary>The player just succeeded at something (outside the guide).</summary>
        public void Success(bool guide)
        {
            if (guide || Deferred == "" || Pending != "") return;
            Pending = Deferred;
            Deferred = "";
            age = 0;
        }

        /// <summary>Returns the moment to hand to the kit now ("" = not now).</summary>
        public string Tick(double dt, bool guide, bool windowOpen)
        {
            if (Pending == "") return "";
            age += dt;
            if (age > 60) { Pending = ""; return ""; }
            if (guide || windowOpen) return "";
            string m = Pending;
            Pending = "";
            return m;
        }
    }
}
