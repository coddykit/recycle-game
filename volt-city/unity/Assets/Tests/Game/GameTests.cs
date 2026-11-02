// Volt City P3 — EditMode tests of the engine-free game layer (VoltCity.Game): strings, formatting, HUD / tab / card view
// models, the action router + crew dispatch flow, the 9-step guide (right action advances, wrong does not, one wrong
// tap = one count, skip, relaunch mid-guide, newbie audit), the save repository wiring (relaunch, .bak recovery,
// save_unreadable), the review hook, and the diorama logic (layout, sim -> art diff, road graph, trucks, picking).
// Runs in the Unity Test Runner and today under .NET 8: sh games/volt-city/unity/Tools/game_dotnet_check.sh
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using VoltCity.Game;
using VoltCity.Sim;

namespace VoltCity.Game.Tests
{
    public class GameTests
    {
        static GameData gd;
        static Loc loc;
        static string unity;

        static string UnityDir()
        {
            string env = Environment.GetEnvironmentVariable("VOLTCITY_UNITY");
            return !string.IsNullOrEmpty(env) ? env : Directory.GetCurrentDirectory();
        }

        [OneTimeSetUp]
        public void Load()
        {
            unity = UnityDir();
            gd = GameData.LoadDirectory(Path.Combine(unity, "Assets", "Resources", "data"));
            Assert.IsTrue(gd.Ok, string.Join("; ", gd.Errors));
            loc = Loc.FromCsv(File.ReadAllText(Path.Combine(unity, "Assets", "Resources", "i18n", "strings.csv")), "en");
            Assert.IsEmpty(loc.Errors, string.Join("; ", loc.Errors));
        }

        static string TempDir()
        {
            string d = Path.Combine(Path.GetTempPath(), "voltcity-game-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }

        static readonly Regex RawKey = new Regex(@"^[a-z][a-z0-9_]*(\.[a-z0-9_]+)+$");

        /// <summary>No visible key, no unfilled placeholder.</summary>
        static void Clean(string s, string where)
        {
            if (string.IsNullOrEmpty(s)) return;
            Assert.IsFalse(RawKey.IsMatch(s), $"raw key visible in {where}: {s}");
            Assert.IsFalse(s.Contains("{") || s.Contains("}"), $"unfilled placeholder in {where}: {s}");
        }

        static void CleanView(TabView tv)
        {
            Clean(tv.Title, tv.Id + ".title");
            Clean(tv.Sub, tv.Id + ".sub");
            if (tv.Primary != null) Clean(tv.Primary.Label, tv.Id + ".primary");
            var ids = new HashSet<string>();
            foreach (var r in tv.Rows)
            {
                Assert.IsTrue(ids.Add(r.Id), "duplicate row id " + r.Id + " in " + tv.Id);
                Clean(r.Title, tv.Id + "/" + r.Id + ".title");
                Clean(r.Sub, tv.Id + "/" + r.Id + ".sub");
                Clean(r.Badge, tv.Id + "/" + r.Id + ".badge");
                Clean(r.Section, tv.Id + "/" + r.Id + ".section");
                foreach (var a in r.Actions) Clean(a.Label, tv.Id + "/" + r.Id + "/" + a.Id);
            }
        }

        static void CheckAllViews(Simulation sim, Loc l, DispatchFlow dispatch = null)
        {
            foreach (var t in Tabs.Ids) CleanView(Tabs.Build(t, sim, l, dispatch));
            var h = Hud.Build(sim, l);
            foreach (var s in new[] { h.Cash, h.Rating, h.Clock, h.Phase, h.Goal, h.NeedleLabel }) Clean(s, "hud");
            Clean(Hud.Explainer(sim, l), "explainer");
            CleanView(Goals.Sheet(sim, l));
            CleanView(Cards.Forecast(sim, l));
            foreach (var d in gd.Districts) CleanView(Cards.District(d.Id, sim, l));
            foreach (var p in GameData.Plants) if (sim.S.Level(p) > 0) CleanView(Cards.Plant(p, sim, l));
            if (sim.Phase == "report") CleanView(Cards.Report(sim, l));
            if (sim.Grid != null) foreach (var f in sim.Grid.Faults) CleanView(Cards.FaultCard(f.Id, sim, l, dispatch));
        }

        // =========================================================================================== strings

        [Test]
        public void Strings_CsvParser()
        {
            var rows = Loc.ParseCsv("﻿key,en\r\na,\"x, \"\"y\"\"\nz\"\nb,plain\n");
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("x, \"y\"\nz", rows[1][1]);
            var l = Loc.FromCsv("key,en,de\nhud.short,Short {mw},Fehlt {mw}\nonly.en,Only,\n", "de");
            Assert.AreEqual("Fehlt 4 MW", l.F("hud.short", "mw", "4 MW"));
            Assert.AreEqual("Only", l.T("only.en"));           // empty cell -> English
            Assert.AreEqual("no.such.key", l.T("no.such.key")); // missing -> key (a test failure elsewhere, never a crash)
            Assert.AreEqual(5, Loc.IndexOf("pt-BR"));
            Assert.AreEqual(9, Loc.IndexOf("hi_IN"));
            Assert.AreEqual(-1, Loc.IndexOf("id"));
        }

        [Test]
        public void Strings_TableComplete_TenLanguages_PlaceholderParity()
        {
            Assert.GreaterOrEqual(loc.Keys.Count, 350);
            var missing = new List<string>();
            var parity = new List<string>();
            foreach (var k in loc.Keys)
            {
                string en = loc.Cell(k, "en");
                Assert.IsNotEmpty(en, "English missing: " + k);
                var ph = string.Join(",", Loc.Placeholders(en));
                foreach (var lang in Loc.Languages)
                {
                    string v = loc.Cell(k, lang);
                    if (string.IsNullOrEmpty(v)) { missing.Add(lang + ":" + k); continue; }
                    if (string.Join(",", Loc.Placeholders(v)) != ph) parity.Add(lang + ":" + k);
                    Assert.IsFalse(Regex.IsMatch(v, @"free\s+trial|try\s+(it\s+)?free", RegexOptions.IgnoreCase), "trial wording " + lang + ":" + k);
                }
            }
            Assert.IsEmpty(parity, "placeholder mismatch: " + string.Join(" ", parity.Take(20)));
            Assert.IsEmpty(missing, $"{missing.Count} empty cells, e.g. " + string.Join(" ", missing.Take(20)));
        }

        [Test]
        public void Strings_EveryKeyTheCodeUses_Exists()
        {
            var prefixes = new[] { "grid.", "crews.", "city.", "build.", "hud.", "phase.", "goal.", "goals.", "fault.", "crew.", "report.", "offline.",
                "forecast.", "toast.", "settings.", "graphics.", "shop.", "coach.", "pager.", "hint.", "why.", "inv.", "invd.", "needle.", "event.",
                "hospital.", "sheet.", "unit.", "tab.", "bat.", "cause.", "chapter.", "wx.", "ev.", "fail.", "plant.", "district." };
            // literals that are view ids / coach targets, not texts
            var notText = new HashSet<string> { "build.gas_1.buy", "build.line_riverside_1.buy", "city.hilltop", "city.hookup.pill", "crews.crew", "crews.crew.send",
                "crews.fault", "crews.fault.send", "ev.tip", "event.go", "goal.buy", "goal.chapter", "goal.stars", "goal.hookup", "goals.hookup.pill", "grid.dr.call",
                "grid.gas.start", "grid.report", "grid.steam.plus", "hud.cash", "hud.clock", "hud.goal", "hud.needle", "hud.rating", "hud.settings", "hud.speed",
                "settings.json", "sheet.handle", "shop.soon", "shop.restore", "pager.skip", "pager.next", "coach.skip", "graphics.ok", "card.fault.send",
                "report.collect", "offline.ok", "event.go", "grid.start", "grid.battery", "card.plant.stop", "card.fault.crew", "card.fault.crew.send",
                "settings.music", "settings.effects", "settings.graphics", "settings.speed", "grid.gas.plus", "grid.gas.minus", "grid.steam.minus" };
            var missing = new List<string>();
            foreach (var dir in new[] { "Game", "UI" })
                foreach (var f in Directory.GetFiles(Path.Combine(unity, "Assets", "Scripts", dir), "*.cs"))
                    foreach (Match m in Regex.Matches(File.ReadAllText(f), "\"([a-z][a-z0-9_]*(?:\\.[a-z0-9_]+)+)\""))
                    {
                        string k = m.Groups[1].Value;
                        if (k.EndsWith("_") || !prefixes.Any(p => k.StartsWith(p)) || notText.Contains(k) || loc.Has(k) || loc.Has(k + ".other")) continue;   // .other: a Loc.N plural base key
                        missing.Add(Path.GetFileName(f) + ":" + k);
                    }
            // keys built from data ids
            foreach (var e in gd.Events) foreach (var s in new[] { ".t", ".b", ".tip" }) if (!loc.Has("ev." + e.Id + s)) missing.Add("ev." + e.Id + s);
            foreach (var d in gd.Districts) foreach (var k in new[] { "district." + d.Id, "district." + d.Id + ".flavour" }) if (!loc.Has(k)) missing.Add(k);
            foreach (var p in GameData.Plants) foreach (var k in new[] { "plant." + p, "plant." + p + ".about", "invd.plant." + p }) if (!loc.Has(k)) missing.Add(k);
            foreach (var w in gd.Weather) if (!loc.Has("wx." + w.Id)) missing.Add("wx." + w.Id);
            foreach (var x in gd.Investments)
            {
                if (x.Kind == "dispatcher") { if (!loc.Has("inv.auto." + x.Target) || !loc.Has("invd.auto." + x.Target)) missing.Add("auto." + x.Target); }
                else if (!new[] { "plant", "line", "dr", "crew_speed" }.Contains(x.Kind) && !loc.Has("invd." + x.Kind)) missing.Add("invd." + x.Kind);
            }
            foreach (var c in gd.Chapters) foreach (var s in new[] { ".t", ".b" }) if (!loc.Has("chapter." + c.Id + s)) missing.Add("chapter." + c.Id + s);
            foreach (var m in Grid.Modes) if (!loc.Has("bat." + m)) missing.Add("bat." + m);
            for (int i = 1; i <= Coach.Steps; i++) if (!loc.Has("coach." + i)) missing.Add("coach." + i);
            foreach (var h in Hints.Ids) if (!loc.Has("hint." + h)) missing.Add("hint." + h);
            foreach (var c in new[] { "thunderstorm", "squirrel", "balloons", "tutorial", "unknown" }) if (!loc.Has("cause." + c)) missing.Add("cause." + c);
            foreach (var t in Tabs.Ids) if (!loc.Has("tab." + t)) missing.Add("tab." + t);
            Assert.IsEmpty(missing, string.Join(" ", missing));
        }

        [Test]
        public void Strings_GuideSaysRiversideLineCostsWhatTheDataSays()
        {
            // DESIGN section 9 step 6 said $400; the data (and the guide) say $420 (P1 open item 4)
            Assert.AreEqual(420, gd.Investment["line_riverside_1"].Cost);
            StringAssert.Contains("$420", loc.T("coach.6"));
        }

        // =========================================================================================== formatting

        [Test]
        public void Fmt_NumbersMoneyPowerTime()
        {
            Assert.AreEqual("3,400", Fmt.Int(3400, "en"));
            Assert.AreEqual("3.400", Fmt.Int(3400, "de"));
            Assert.AreEqual("1,234,567", Fmt.Int(1234567, "en"));
            Assert.AreEqual("$1,950", Fmt.Money(1950));
            Assert.AreEqual("−$30", Fmt.Money(-30));
            Assert.AreEqual("$930", Fmt.MoneyShort(930));
            Assert.AreEqual("$12.4k", Fmt.MoneyShort(12480));
            Assert.AreEqual("$12,4k", Fmt.MoneyShort(12480, "de"));
            Assert.AreEqual("$1.2M", Fmt.MoneyShort(1_234_567));
            Assert.AreEqual("46 MW", Fmt.Mw(46_200));
            Assert.AreEqual("4.5 MW", Fmt.Mw(4_500));
            Assert.AreEqual("17:30", Fmt.Clock(17.5));
            Assert.AreEqual("00:00", Fmt.Clock(24.0));
            Assert.AreEqual("3.2★", Fmt.Rating(320));
            Assert.AreEqual("6 s", Fmt.Dur(5.2));
            Assert.AreEqual("1 min 20 s", Fmt.Dur(80));
            Assert.AreEqual(3700, Fmt.Homes(22_000));
        }

        // =========================================================================================== HUD + tabs + cards

        [Test]
        public void Hud_NeedleBandsAndAngles()
        {
            Assert.AreEqual(NeedleBand.Green, Hud.Band(30, gd));
            Assert.AreEqual(NeedleBand.Amber, Hud.Band(-31, gd));
            Assert.AreEqual(NeedleBand.Amber, Hud.Band(50, gd));
            Assert.AreEqual(NeedleBand.Red, Hud.Band(-51, gd));
            Assert.AreEqual(0f, Hud.Deg(0));
            Assert.AreEqual(-60f, Hud.Deg(-400));
            Assert.AreEqual(18f, Hud.Deg(30));
            Assert.AreEqual("phase.evening", Hud.PhaseKey(18.0));
            Assert.AreEqual("phase.morning", Hud.PhaseKey(7.5));
        }

        [Test]
        public void Views_ScriptedCareer_AllTabsAndCards_NoRawKeys_InEveryLanguage()
        {
            var sim = new Simulation(gd, 11);
            var langs = new List<Loc>();
            foreach (var l in Loc.Languages) { var x = Loc.FromCsv(File.ReadAllText(Path.Combine(unity, "Assets", "Resources", "i18n", "strings.csv")), l); langs.Add(x); }
            CheckAllViews(sim, loc);
            for (int shift = 0; shift < 14; shift++)
            {
                Assert.IsTrue(sim.StartShift(shift == 3 ? "rainy" : null, shift == 3 ? new List<string> { "thunderstorm", "squirrel" } : null));
                for (int k = 0; k < 8; k++)
                {
                    sim.RunSteps(sim.Gd.Steps / 8);
                    if (sim.Phase != "live") break;
                    foreach (var f in sim.Grid.OpenFaults()) if (f.Crew < 0) sim.Queue(Cmd.SendCrew(f.Id));
                    if (k == 2 || k == 6) foreach (var l in langs) CheckAllViews(sim, l, new DispatchFlow());
                }
                sim.RunSteps(sim.Gd.Steps);
                Assert.AreEqual("report", sim.Phase);
                foreach (var l in langs) CleanView(Cards.Report(sim, l));
                sim.Collect();
                foreach (var b in Economy.BuyableNow(sim))
                {
                    if (b.Kind == "investment") sim.Buy(b.Id); else sim.Hookup(b.Id);
                }
                CheckAllViews(sim, loc);
            }
            Assert.GreaterOrEqual(sim.S.Owned.Count, 5, "owned " + sim.S.Owned.Count + " cash " + sim.S.Cash + " shift " + sim.S.ShiftNo);
            CleanView(Cards.Offline(new OfflineSummary { AwayS = 9 * 3600, CountedS = 8 * 3600, Cash = 700, Capped = true }, loc));
            foreach (var e in gd.Events) CleanView(Cards.Event(e.Id, sim, loc));
        }

        [Test]
        public void Tabs_ContentRules()
        {
            var sim = new Simulation(gd, 3);
            var build = Tabs.BuildTab(sim, loc);
            var line = build.Rows.Find(r => r.Id == "inv:line_riverside_1");
            Assert.IsNotNull(line, "first investment listed");
            Assert.AreEqual("$420", line.Actions[0].Label);
            Assert.AreEqual("build.line_riverside_1.buy", line.Actions[0].Target);
            Assert.IsTrue(build.Rows.Exists(r => r.Locked && r.Id.StartsWith("inv:wind")), "wind needs Hilltop: locked row with the reason, never hidden");
            Assert.AreEqual("grid.start", Tabs.Grid(sim, loc).Primary.Target);
            sim.StartShift();
            var grid = Tabs.Grid(sim, loc);
            var steam = grid.Rows.Find(r => r.Id == "plant:steam");
            Assert.AreEqual(2, steam.Actions.Count, "−/+ stepper");
            Assert.AreEqual("grid.steam.plus", steam.Actions[1].Target);
            Assert.GreaterOrEqual(steam.Progress, 0f);
            // shed order: Hospital Hill always last, never a Shed pill on it
            foreach (var d in gd.Districts) sim.S.Hooked[d.Id] = 0;
            Assert.AreEqual("hospital_hill", Tabs.ShedOrder(sim).Last());
            Assert.AreEqual("stadium_row", Tabs.ShedOrder(sim).First());
            var s2 = new Simulation(gd, 4);
            foreach (var d in gd.Districts) s2.S.Hooked[d.Id] = 0;
            s2.StartShift();
            var city = Tabs.City(s2, loc);
            var hos = city.Rows.Find(r => r.Id == "district:hospital_hill");
            Assert.IsFalse(hos.Actions.Exists(a => a.Id.StartsWith("shed:")), "no shed on the hospital");
            Assert.IsTrue(city.Rows.Find(r => r.Id == "district:riverside").Actions.Exists(a => a.Id == "shed:riverside" && a.Kind == "danger"));
        }

        [Test]
        public void Crews_SortedByHomesDark_AndDispatchFlow()
        {
            var session = new GameSession(gd, TempDir());
            session.LoadOrNew(9);
            var sim = session.Sim;
            sim.S.Crews = 2;
            sim.TutorialSafe = true;
            sim.StartShift();
            sim.RunSteps(8);
            var actions = new GameActions(session, loc);
            sim.Queue(Cmd.ScriptFault("c_harbor"));
            sim.Queue(Cmd.ScriptFault("c_riverside"));
            sim.RunSteps(2);
            var faults = sim.Grid.OpenFaults();
            Assert.AreEqual(2, faults.Count);
            var tab = Tabs.Crews(sim, loc, actions.Dispatch);
            var rows = tab.Rows.Where(r => r.Id.StartsWith("fault:")).ToList();
            long h0 = Tabs.HomesDark(sim.Grid, sim.Grid.FindFault(int.Parse(rows[0].Id.Substring(6))));
            long h1 = Tabs.HomesDark(sim.Grid, sim.Grid.FindFault(int.Parse(rows[1].Id.Substring(6))));
            Assert.GreaterOrEqual(h0, h1, "most homes dark first");
            // tap fault -> selected; tap truck -> queued with that truck
            int fid = faults[0].Id;
            var r1 = actions.Run("select:" + fid);
            Assert.IsTrue(r1.Ok);
            Assert.AreEqual("card:fault:" + fid, r1.Open);
            Assert.AreEqual(fid, actions.Dispatch.Fault);
            var picks = Tabs.Crews(sim, loc, actions.Dispatch).Rows.Where(r => r.Actions.Exists(a => a.Id.StartsWith("send:" + fid + ":"))).ToList();
            Assert.AreEqual(2, picks.Count, "every free truck offers itself");
            var r2 = actions.Run("send:" + fid + ":1");
            Assert.IsTrue(r2.Ok);
            Assert.IsFalse(actions.Dispatch.Active);
            sim.RunSteps(1);
            Assert.AreEqual(1, sim.Grid.FindFault(fid).Crew, "the tapped truck went");
            Assert.IsFalse(actions.Run("send:" + fid).Ok, "already assigned");
        }

        [Test]
        public void Actions_RampShedBuySpeedCollect()
        {
            var s = new GameSession(gd, TempDir());
            s.LoadOrNew(21);
            var a = new GameActions(s, loc);
            Assert.IsFalse(a.Run("ramp:steam:+1").Ok, "not live");
            Assert.IsTrue(a.Run("shift:start").Ok);
            var r = a.Run("ramp:steam:+1");
            Assert.IsTrue(r.Ok);
            StringAssert.Contains("6 s", r.Toast, "steam takes effect after its 6 s delay");
            Assert.AreEqual("hospital", a.Run("shed:hospital_hill").Reason);
            Assert.AreEqual("live", a.Run("buy:line_riverside_1").Reason, "building waits for the shift end");
            Assert.IsTrue(a.Run("speed_next").Ok);
            Assert.AreEqual(2, s.Sim.S.Speed);
            a.Run("speed_next"); a.Run("speed_next");
            Assert.AreEqual(1, s.Sim.S.Speed, "1 -> 2 -> 3 -> 1 (4 needs the perk)");
            Assert.AreEqual("speed_locked", a.Run("speed:4").Reason);
            s.Sim.RunSteps(gd.Steps);
            long before = s.Sim.S.Cash;
            var c = a.Run("collect");
            Assert.IsTrue(c.Ok);
            Assert.AreEqual(before + c.Cash, s.Sim.S.Cash);
            Assert.IsTrue(a.Run("buy:line_riverside_1").Ok);
            Assert.AreEqual("owned", a.Run("buy:line_riverside_1").Reason);
        }

        // =========================================================================================== guide

        sealed class Harness
        {
            public GameSession Session;
            public GameActions Actions;
            public Coach Coach;
            public int Persisted = -1;
            public bool Done;
            public readonly List<string> Analytics = new List<string>();
            public double RealS;
            public readonly List<SimEvent> Events = new List<SimEvent>();

            public Harness(string dir, long seed, int resumeStep = 0, bool begin = true)
            {
                Session = new GameSession(gd, dir);
                Session.LoadOrNew(seed);
                Actions = new GameActions(Session, loc);
                Coach = new Coach { Persist = (s, d) => { Persisted = s; Done = d; }, Analytics = (n, p) => Analytics.Add(n) };
                Actions.Ran += (id, r) => Coach.OnAction(Session.Sim, id, r);
                if (begin) Coach.Begin(Session.Sim, resumeStep);
            }

            /// <summary>One UI frame: the coach looks first (as VoltCityMain does), the sim runs, then the coach sees the
            /// result before the player can react (a tap always comes at least one frame later).</summary>
            public void Frame(double dt = 0.25)
            {
                Coach.Update(Session.Sim);
                Session.Sim.SetPaused(Coach.WantsPause);
                Events.AddRange(Session.Tick(dt));
                RealS += dt;
                Coach.Update(Session.Sim);
                Session.Sim.SetPaused(Coach.WantsPause);
            }

            /// <summary>A player outside the guide: big primary buttons + sends free trucks to sparks.</summary>
            public void PlayBasic()
            {
                var sim = Session.Sim;
                if (sim.Phase == "idle") Actions.Run("shift:start");
                else if (sim.Phase == "report") Actions.Run("collect");
                else if (!Coach.Active && sim.Grid != null)
                    foreach (var f in sim.Grid.OpenFaults()) if (f.Crew < 0 && sim.Grid.IdleCrews() > 0) Actions.Run("send:" + f.Id);
            }
        }

        /// <summary>The control the spotlight points at really exists in the view model the UI renders.</summary>
        static void AssertTargetRendered(Harness h)
        {
            var c = h.Coach;
            var sim = h.Session.Sim;
            string t = c.Target;
            switch (t)
            {
                case "hud.needle": return;   // a fixed HUD element
                case "world.fault": Assert.IsNotNull(sim.Grid.FindFault(c.FaultId)); return;
                case "card.fault.send": Assert.AreEqual(t, Cards.FaultCard(c.FaultId, sim, loc, h.Actions.Dispatch).Primary.Target); return;
                case "report.collect": Assert.AreEqual(t, Cards.Report(sim, loc).Primary.Target); return;
            }
            var tv = Tabs.Build(c.Tab, sim, loc, h.Actions.Dispatch);
            bool found = tv.Rows.Exists(r => r.Target == t || r.Actions.Exists(a => a.Target == t && a.Enabled));
            Assert.IsTrue(found, $"coach target {t} (step {c.Step}) not rendered/enabled on tab {c.Tab}");
        }

        [TestCase(2026)]
        [TestCase(1)]
        [TestCase(77)]
        [TestCase(31337)]
        [TestCase(500)]
        public void Guide_NewbieTapsOnlyTheHighlight_FinishesUnder10Min_TwoStarsShift1(long seed)
        {
            var h = new Harness(TempDir(), seed);
            Assert.IsTrue(h.Actions.Run("shift:start").Ok);
            Assert.AreEqual(1050, h.Session.Sim.S.Plan.StartMin, "the guided first shift starts at 17:30");
            var seen = new List<int>();
            int shift1Stars = -1, assists = 0;
            while (h.Coach.Active && h.RealS < 1200)
            {
                h.Frame();
                foreach (var e in h.Events) if (e.Type == "shift_done" && e.Num("shift") == 0) shift1Stars = (int)e.Num("stars");
                h.Events.Clear();
                if (h.Coach.Spot)
                {
                    if (h.Coach.Showing && !seen.Contains(h.Coach.Step)) { seen.Add(h.Coach.Step); AssertTargetRendered(h); }
                    if (!h.Coach.Showing) { assists++; AssertTargetRendered(h); }
                    if (h.Session.Sim.Phase == "live") Assert.IsTrue(h.Session.Sim.S.Paused, "time stops while a step waits (step " + h.Coach.Step + ")");
                    string act = GuideBot.ActionFor(h.Coach);
                    Assert.IsNotEmpty(act, "bot knows step " + h.Coach.Step);
                    var r = h.Actions.Run(act);
                    Assert.IsTrue(r.Ok, $"step {h.Coach.Step} action {act}: {r.Reason}");
                }
                else if (h.Session.Sim.Phase == "idle") h.Actions.Run("shift:start");     // the big primary button
                else if (h.Session.Sim.Phase == "report") h.Actions.Run("collect");
            }
            Assert.IsFalse(h.Coach.Active, "guide finished");
            Assert.IsTrue(h.Done);
            CollectionAssert.AreEqual(Enumerable.Range(1, 9).ToList(), seen, "all 9 steps shown in order: " + string.Join(",", seen) + " active=" + h.Coach.Active + " step=" + h.Coach.Step + " phase=" + h.Session.Sim.Phase + " t=" + h.RealS);
            Assert.Less(h.RealS, 600, "newbie audit < 10 min");
            var r0 = h.Session.Sim.S.Reports.Count > 0 ? h.Session.Sim.S.Reports[0] : null;
            Assert.GreaterOrEqual(shift1Stars, 2, "shift 1 >= 2 stars: uptime " + r0?.UptimePm + " profit " + r0?.Profit + " sheds " + r0?.Shed + " outage " + r0?.OutageMin + " assists " + assists);
            Assert.IsFalse(h.Session.Sim.TutorialSafe, "guide over: normal shifts");
            Assert.IsTrue(h.Session.Sim.S.Owned.ContainsKey("line_riverside_1") && h.Session.Sim.S.Owned.ContainsKey("gas_1"));
            Assert.AreEqual(9, h.Analytics.Count(n => n == "onboarding_step"));
            TestContext.Progress.WriteLine($"GUIDE_AUDIT seed {seed}: done at {h.RealS:0} s real play, shift 1 = {shift1Stars} stars (uptime {r0?.UptimePm} pm, profit ${r0?.Profit}), {assists} assist taps, {h.Coach.WrongTaps} wrong taps");
            Assert.AreEqual(1, h.Analytics.Count(n => n == "onboarding_complete"));
        }

        [Test]
        public void Guide_WrongTapDoesNotAdvance_CountsOnce_RightTapAdvances()
        {
            var h = new Harness(TempDir(), 7);
            h.Actions.Run("shift:start");
            for (int i = 0; i < 200 && !h.Coach.Showing; i++) h.Frame();
            Assert.AreEqual(1, h.Coach.Step);
            Assert.IsTrue(h.Coach.Showing);
            Assert.IsFalse(h.Coach.Tap("tab.build"), "outside the spotlight = wrong");
            Assert.AreEqual(1, h.Coach.WrongTaps, "one physical tap = one wrong tap");
            Assert.AreEqual(1, h.Coach.Flash);
            h.Frame();
            Assert.AreEqual(1, h.Coach.Step, "no advance");
            Assert.IsTrue(h.Coach.Tap("grid.steam.plus"), "the spotlight target acts");
            Assert.IsTrue(h.Actions.Run("ramp:steam:-1").Ok);
            Assert.AreEqual(1, h.Coach.Step, "the wrong action (−) does not advance");
            h.Actions.Run("ramp:steam:+1");
            Assert.AreEqual(2, h.Coach.Step);
            Assert.AreEqual("hud.needle", h.Coach.Target);
            Assert.IsTrue(h.Coach.Tap("hud.needle"));
            Assert.IsFalse(h.Coach.Tap("hud.settings"));
            Assert.AreEqual(2, h.Coach.WrongTaps);
        }

        [Test]
        public void Guide_SkipThenReplay_NeverLocks()
        {
            var h = new Harness(TempDir(), 8);
            h.Actions.Run("shift:start");
            for (int i = 0; i < 100; i++) h.Frame();
            h.Coach.Skip(h.Session.Sim);
            h.Frame();
            Assert.IsFalse(h.Coach.Active);
            Assert.IsTrue(h.Done);
            Assert.IsFalse(h.Session.Sim.TutorialSafe);
            Assert.IsFalse(h.Session.Sim.S.Paused || h.Coach.WantsPause);
            Assert.Contains("onboarding_skip", h.Analytics);
            Assert.IsTrue(h.Coach.Tap("anything"), "no gate after skip");
            // replay later in the career (Settings -> Replay guide): steps whose moment passed / already owned are skipped
            h.Session.Sim.RunSteps(gd.Steps);
            h.Actions.Run("collect");
            h.Session.Sim.S.Cash = 5000;
            h.Actions.Run("buy:line_riverside_1");
            var st = h.Session.Settings;
            st.ReplayGuide();
            Assert.IsFalse(st.IntroDone || st.CoachDone);
            h.Coach.Begin(h.Session.Sim, 0);
            int guard = 0;
            while (h.Coach.Active && guard++ < 6000)
            {
                h.Frame();
                string act = GuideBot.ActionFor(h.Coach);
                if (act != "") Assert.IsTrue(h.Actions.Run(act).Ok, "replay step " + h.Coach.Step + " " + act);
                else if (h.Session.Sim.Phase == "idle") h.Actions.Run("shift:start");
                else if (h.Session.Sim.Phase == "report") h.Actions.Run("collect");
            }
            Assert.IsFalse(h.Coach.Active, "replay finishes in an advanced career (stuck at step " + h.Coach.Step + ")");
        }

        [Test]
        public void Guide_RelaunchMidGuide_ResumesWithoutLock()
        {
            string dir = TempDir();
            var h = new Harness(dir, 31);
            h.Actions.Run("shift:start");
            int guard = 0;
            while (!(h.Coach.Step == 4 && h.Coach.Showing) && guard++ < 2000)
            {
                h.Frame();
                string act = GuideBot.ActionFor(h.Coach);
                if (act != "" && h.Coach.Step < 4) h.Actions.Run(act);
            }
            Assert.AreEqual(4, h.Coach.Step);
            h.Session.Save();                                   // app killed here (step 4: the fault card was open)
            var h2 = new Harness(dir, 999, h.Persisted);        // relaunch with the persisted step
            Assert.AreEqual("live", h2.Session.Sim.Phase, "the live shift continues");
            Assert.IsTrue(h2.Session.Sim.TutorialSafe);
            for (int i = 0; i < 400 && !h2.Coach.Showing; i++) h2.Frame();
            Assert.IsTrue(h2.Coach.Showing, "the guide shows a step again");
            Assert.IsTrue(h2.Coach.Step == 4 || h2.Coach.Step == 3);
            Assert.IsNotNull(h2.Session.Sim.Grid.FindFault(h2.Coach.FaultId));
            guard = 0;
            while (h2.Coach.Active && guard++ < 8000)
            {
                h2.Frame();
                string act = GuideBot.ActionFor(h2.Coach);
                if (act != "") Assert.IsTrue(h2.Actions.Run(act).Ok);
                else if (h2.Session.Sim.Phase == "idle") h2.Actions.Run("shift:start");
                else if (h2.Session.Sim.Phase == "report") h2.Actions.Run("collect");
            }
            Assert.IsFalse(h2.Coach.Active);
        }

        // =========================================================================================== review hook

        [Test]
        public void Review_FirstSessionMoments_DeferredOutOfTheGuide_AskedAtNextSuccess()
        {
            var h = new Harness(TempDir(), 2026);
            var hook = new ReviewHook();
            h.Actions.Run("shift:start");
            int guard = 0;
            var moments = new List<string>();
            while (guard++ < 20000)
            {
                h.Frame();
                foreach (var e in h.Events)
                {
                    var it = Feed.From(e, h.Session.Sim, loc, h.Coach.Active);
                    if (it.ReviewMoment != "") hook.Offer(it.ReviewMoment, it.ReviewReady);
                    if (it.Success) hook.Success(h.Coach.Active);
                }
                h.Events.Clear();
                string m = hook.Tick(0.25, h.Coach.Active, false);
                if (m != "") { moments.Add(m); break; }
                string act = GuideBot.ActionFor(h.Coach);
                if (act != "") h.Actions.Run(act);
                else h.PlayBasic();
            }
            Assert.AreEqual(1, moments.Count, "the first session asks once; deferred=" + hook.Deferred + " pending=" + hook.Pending + " coach=" + h.Coach.Active + "/" + h.Coach.Step + " t=" + h.RealS);
            Assert.IsFalse(h.Coach.Active, "never during the guide");
            StringAssert.StartsWith("first_", moments[0]);
            Assert.Less(h.RealS, 3600, "still the first session");
            // a window open holds the ask, closing releases it
            var hk = new ReviewHook();
            hk.Offer("x", true);
            Assert.AreEqual("", hk.Tick(1, false, true));
            Assert.AreEqual("x", hk.Tick(1, false, false));
        }

        // =========================================================================================== saves

        [Test]
        public void Save_RelaunchMidShift_SameHashAndCash_ThenIdenticalFuture()
        {
            string dir = TempDir();
            var a = new GameSession(gd, dir) { Now = () => 1_800_000_000 };
            Assert.IsFalse(a.LoadOrNew(77), "new career");
            a.StartShift();
            a.Sim.Queue(Cmd.Ramp("steam", 1));
            for (int i = 0; i < 37; i++) a.Tick(0.27);           // odd frame times: a pending remainder exists
            Assert.AreEqual("", a.Save());
            var b = new GameSession(gd, dir) { Now = () => 1_800_000_010 };
            Assert.IsTrue(b.LoadOrNew(1));
            Assert.AreEqual(a.Sim.ShiftHash(), b.Sim.ShiftHash());
            Assert.AreEqual(a.Sim.S.Cash, b.Sim.S.Cash);
            for (int i = 0; i < 200; i++) { a.Tick(0.25); b.Tick(0.25); }
            Assert.AreEqual(a.Sim.ShiftHash(), b.Sim.ShiftHash(), "continues bit for bit after the relaunch");
        }

        /// <summary>Star targets (Patron #174b, game owner: 2 stars >= 88 % lights on; 3 stars asked >= 96 %, measured here and
        /// set to >= 98.5 % because at 96 % a typical new player got 3 stars in 17 of 18 shifts; shed 18 s / trip 35 s):
        /// a careful player gets 3 stars, a typical new player 2, 1 star only after a real mistake. Headless players over
        /// shifts 1-6 of a fresh career, 3 seeds; the guided first shift (TutorialSafe) is skipped.</summary>
        static readonly List<string> Uptimes = new List<string>();

        static int PlayShiftAs(Simulation sim, string who, int seed)
        {
            if (!sim.StartShift()) return -1;
            sim.TutorialSafe = false;
            var rng = new Random(seed);
            int lookEvery = who == "careful" ? 4 : 16;          // 1 s vs 4 s between looks at the needle
            int guard = 0, peakIgnore = -1;
            while (sim.Phase == "live" && guard++ < 5000)
            {
                var g = sim.Grid;
                // mistake: looks away for 60 s when each peak starts (morning ~07:30, evening ~18:30)
                double h = g.HourOfDay;
                if (who == "mistake" && peakIgnore < g.Step && ((h >= 7.5 && h < 7.6) || (h >= 18.5 && h < 18.6))) peakIgnore = g.Step + 60 * 4;
                bool away = who == "mistake" && g.Step < peakIgnore;
                if (!away && g.Step % lookEvery == 0)
                {
                    if (who == "careful")
                        foreach (var c in Dispatcher.Operate(sim, Policies.OperatorFor("greedy"))) sim.Queue(c);
                    else if (rng.NextDouble() < 0.65)   // a new player glances at the needle about two times in three
                    {
                        bool steamUp = g.PLevel[Grid.PSteam] > 0 && g.SetpointAfterPending(Grid.PSteam) < 10;
                        bool gasHave = g.PLevel[Grid.PGas] > 0;
                        if (g.NeedlePm < -50)          // visibly red: one tap more
                        {
                            if (steamUp) sim.Queue(Cmd.Ramp("steam", +1));
                            else if (gasHave && g.POn[Grid.PGas] == 0) sim.Queue(Cmd.Power(true));
                            else if (gasHave) sim.Queue(Cmd.Ramp("gas", +1));
                        }
                        else if (g.NeedlePm > 60)      // visibly red the other way: one tap less
                        {
                            if (gasHave && g.POn[Grid.PGas] == 1 && g.SetpointAfterPending(Grid.PGas) > 0) sim.Queue(Cmd.Ramp("gas", -1));
                            else if (g.SetpointAfterPending(Grid.PSteam) > 0) sim.Queue(Cmd.Ramp("steam", -1));
                        }
                        else
                        {
                            var a = Coach.AssistFor(sim);   // the game's own tip ("X is dark. Tap + ...")
                            if (a != null && a.Value.key == "coach.assist_restore" && steamUp) sim.Queue(Cmd.Ramp("steam", +1));
                        }
                        // a fault on the map: send a crew (the guide's step 3-4); a new player does it about half the time
                        var open = g.OpenFaults();
                        if (open.Count > 0 && open[0].Crew < 0 && rng.NextDouble() < 0.5) sim.Queue(Cmd.SendCrew(open[0].Id));
                    }
                }
                sim.RunSteps(1);
            }
            sim.DrainEvents();
            int stars = sim.S.Report != null ? sim.S.Report.Stars : 0;
            if (sim.S.Report != null) Uptimes.Add(who + ":" + sim.S.Report.UptimePm);
            sim.Collect();
            Policies.BuyPhase(sim, "greedy");
            return stars;
        }

        [Test]
        public void Stars_CarefulThree_TypicalTwo_MistakeOne()
        {
            var lines = new List<string>();
            var med = new Dictionary<string, double>();
            foreach (var who in new[] { "careful", "typical", "mistake" })
            {
                var all = new List<int>();
                foreach (long seed in new long[] { 3, 7, 11 })
                {
                    var sim = SimProbe.CareerAfterDays(gd, seed, 0);
                    // the guided first shift (TutorialSafe, no sheds / trips) as the game runs it
                    Policies.PlayShift(sim, "greedy"); sim.Collect(); Policies.BuyPhase(sim, "greedy");
                    var st = new List<int>();
                    for (int k = 0; k < 6; k++) { int x = PlayShiftAs(sim, who, (int)seed * 10 + k); if (x >= 0) st.Add(x); }
                    all.AddRange(st);
                    lines.Add($"{who} seed {seed}: {string.Join(" ", st)}");
                }
                all.Sort();
                med[who] = all.Count > 0 ? all[all.Count / 2] : 0;
                lines.Add($"{who}: mean {all.Average():F2}, median {med[who]}, 1-star {all.Count(x => x == 1)} / {all.Count}");
            }
            TestContext.WriteLine("STARS " + string.Join(" | ", lines));
            TestContext.WriteLine("UPTIME " + string.Join(" ", Uptimes));
            Assert.AreEqual(3, med["careful"], string.Join("\n", lines));
            // fair third star (owner #183-2b): a fault's dark time counts only until a crew is dispatched -> the careful
            // player keeps 3 stars in every shift, faults included
            Assert.IsTrue(lines.Where(l => l.StartsWith("careful seed")).All(l => l.Split(':')[1].Trim().Split(' ').All(x => x == "3")), "careful: 3 stars in 18 / 18\n" + string.Join("\n", lines));
            Assert.AreEqual(2, med["typical"], string.Join("\n", lines));
            Assert.IsFalse(lines.Any(l => l.StartsWith("typical seed") && l.Split(':')[1].Split(' ').Contains("1")), "a typical player never gets 1 star: " + string.Join("\n", lines));
            Assert.LessOrEqual(med["mistake"], 2, string.Join("\n", lines));
        }

        /// <summary>Volt soak (Patron #173, runs in check.sh): 30 game minutes of a career at 3x with a seeded random fuzz of
        /// every player action + every tab / card render, a save -> fresh-session reload every 3 game minutes (state hash +
        /// cash must survive), 0 exceptions.</summary>
        [Test]
        public void Soak_ThirtyGameMinutes_RandomFuzz_SaveLoadCycles()
        {
            string dir = TempDir();
            var rng = new Random(173);
            long now = 1_800_000_000;
            var s = new GameSession(gd, dir) { Now = () => now };
            s.LoadOrNew(173);
            var act = new GameActions(s, loc);
            var districts = gd.Districts.Select(d => d.Id).ToArray();
            var invs = gd.Investments.Select(x => x.Id).ToArray();
            string[] fixedIds = { "ramp:steam:+1", "ramp:steam:-1", "ramp:gas:+1", "ramp:gas:-1", "power:gas:on", "power:gas:off",
                "speed:1", "speed:2", "speed:3", "speed_next", "collect", "shift:start", "bat:auto", "bat:charge", "bat:hold", "dr", "noop",
                "open:needle", "open:report", "policy", "shop", "restore", "cancel:dispatch" };
            double gameS = 0, nextSave = 180;
            int actions = 0, saves = 0, renders = 0;
            var errors = new List<string>();
            while (gameS < 30 * 60)
            {
                try
                {
                    // fuzz: 0-3 random taps per frame, real action ids, random targets
                    int taps = rng.Next(4);
                    for (int i = 0; i < taps; i++)
                    {
                        string id;
                        switch (rng.Next(6))
                        {
                            case 0: id = fixedIds[rng.Next(fixedIds.Length)]; break;
                            case 1: id = (rng.Next(2) == 0 ? "shed:" : "restore:") + districts[rng.Next(districts.Length)]; break;
                            case 2: id = (rng.Next(2) == 0 ? "buy:" : "hookup:") + (rng.Next(2) == 0 ? invs[rng.Next(invs.Length)] : districts[rng.Next(districts.Length)]); break;
                            case 3: { var open = s.Sim.Grid?.OpenFaults(); id = open != null && open.Count > 0 ? (rng.Next(2) == 0 ? "select:" : "send:") + open[rng.Next(open.Count)].Id : "noop"; break; }
                            case 4: id = "card:district:" + districts[rng.Next(districts.Length)]; break;
                            default: id = new[] { "", "x", "ramp:", "send:abc", "buy:nope", "speed:99", "card:" }[rng.Next(7)]; break;   // junk must be refused, never throw
                        }
                        act.Run(id);
                        actions++;
                    }
                    // keep the career moving like a player would (a shift ends, the report is collected, a new one starts)
                    if (s.Sim.Phase == "report") act.Run("collect");
                    if (s.Sim.Phase == "idle" && rng.Next(10) == 0) act.Run("shift:start");
                    double dt = 0.016 + rng.NextDouble() * 0.05;
                    s.Tick(dt);
                    if (s.Sim.Grid != null) Assert.LessOrEqual(Math.Abs(s.Sim.Grid.NeedlePm), 1000, "needle within +-100 % (demand 0 exploded it, play 10-29)");
                    gameS += dt * Math.Max(1, s.Sim.S.Speed);
                    now += (long)Math.Ceiling(dt);
                    if (rng.Next(20) == 0)
                    {
                        foreach (var tab in Tabs.Ids) { Tabs.Build(tab, s.Sim, loc, act.Dispatch, null); renders++; }
                        Hud.Build(s.Sim, loc);
                    }
                    if (gameS >= nextSave)
                    {
                        nextSave += 180;
                        Assert.AreEqual("", s.Save());
                        var b = new GameSession(gd, dir) { Now = () => now };
                        Assert.IsTrue(b.LoadOrNew(1), "reload finds the save");
                        Assert.AreEqual(s.Sim.ShiftHash(), b.Sim.ShiftHash(), "save -> load keeps the state at " + (int)gameS + " s");
                        Assert.AreEqual(s.Sim.S.Cash, b.Sim.S.Cash);
                        s = b;                                   // continue in the reloaded session (a real relaunch)
                        act = new GameActions(s, loc);
                        saves++;
                    }
                }
                catch (AssertionException) { throw; }
                catch (Exception e) { errors.Add((int)gameS + " s: " + e.GetType().Name + " " + e.Message); if (errors.Count > 20) break; }
            }
            TestContext.WriteLine($"SOAK: {gameS / 60:F0} game min, {actions} fuzz actions, {renders} tab renders, {saves} save/load cycles, shift {s.Sim.S.ShiftNo}, {errors.Count} exceptions");
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.GreaterOrEqual(saves, 9);
        }

        [Test]
        public void Save_BrokenMain_LoadsBak_KeepsUnreadable_SendsEvent()
        {
            string dir = TempDir();
            var a = new GameSession(gd, dir);
            a.LoadOrNew(5);
            a.Save();
            a.StartShift();                                       // second save: the first one becomes .bak
            long cash = a.Sim.S.Cash;
            File.WriteAllText(a.SavePath, "{\"v\":1,\"broken");     // the main file is damaged
            var events = new List<string>();
            var b = new GameSession(gd, dir) { Analytics = (n, p) => events.Add(n + ":" + p["code"] + ":" + p["recovered"]) };
            Assert.IsTrue(b.LoadOrNew(9));
            Assert.AreEqual("bak", b.LastLoad.Source);
            Assert.AreEqual(cash, b.Sim.S.Cash);
            Assert.IsTrue(File.Exists(a.SavePath + ".unreadable"), "the broken file is kept, never deleted");
            Assert.AreEqual(1, events.Count);
            StringAssert.StartsWith("save_unreadable:", events[0]);
            StringAssert.EndsWith(":1", events[0]);
        }

        [Test]
        public void Save_Autosave30sLive_AndOfflineOnResume()
        {
            long now = 1_900_000_000;
            var s = new GameSession(gd, TempDir()) { Now = () => now };
            s.LoadOrNew(3);
            s.StartShift();
            int saves = s.SaveCount;
            for (int i = 0; i < 31 * 4; i++) s.Tick(0.25);
            Assert.AreEqual(saves + 1, s.SaveCount, "one autosave per 30 s of live play");
            s.Sim.RunSteps(gd.Steps);
            s.Tick(0.01);
            s.Collect();
            s.Pause();
            now += 3 * 3600;
            var off = s.Resume();
            Assert.IsNotNull(off);
            Assert.Greater(off.Cash, 0);
            CleanView(Cards.Offline(off, loc));
        }

        // =========================================================================================== diorama logic

        static CityLayout Layout() => CityLayout.Parse(File.ReadAllText(Path.Combine(unity, "Assets", "Art", "Models", "layout.json")));

        [Test]
        public void World_Layout_ArtMap_CoversSimCorridors()
        {
            var L = Layout();
            Assert.AreEqual(8, L.Districts.Count);
            Assert.AreEqual(16, L.Corridors.Count);
            foreach (var c in gd.Corridors)
            {
                Assert.IsTrue(ArtMap.Corridor.ContainsKey(c.Id), c.Id);
                var art = L.Corridors[ArtMap.Corridor[c.Id]];
                Assert.AreEqual(c.District, art.B, $"{c.Id} -> {art.Id} feeds {art.B}");
            }
            foreach (var t in gd.Ties) Assert.IsTrue(ArtMap.TieDistricts.ContainsKey(t.Id));
            foreach (var p in CityLayout.ArtPlants) Assert.IsTrue(L.PlantSite.ContainsKey(p), p);
        }

        [Test]
        public void World_Diff_ChangesOnly_FaultDarkensAndRepairLights()
        {
            var L = Layout();
            var wm = new WorldModel(L);
            var sim = new Simulation(gd, 12) { TutorialSafe = true };
            var d0 = wm.Sync(sim, false);
            Assert.AreEqual(8, d0.Districts.Count, "first sync = full state");
            Assert.IsTrue(d0.Districts.All(x => !x.animate));
            Assert.AreEqual(16, d0.Corridors.Count);
            Assert.IsNotNull(d0.PlantLevels);
            Assert.IsFalse(wm.Sync(sim, false).Any, "nothing changed -> nothing written");
            sim.StartShift();
            sim.RunSteps(4);
            wm.Sync(sim, false);
            sim.Queue(Cmd.ScriptFault("c_riverside"));
            sim.RunSteps(2);
            var d1 = wm.Sync(sim, false);
            Assert.IsTrue(d1.Corridors.Exists(c => c.index == 1 && c.state == ArtMap.Down), "river_line down");
            Assert.IsTrue(d1.Districts.Exists(x => x.id == "riverside" && !x.lit && x.animate), "Riverside goes dark in a ripple");
            Assert.AreEqual(1, d1.Faults.Count);
            int fid = sim.Grid.OpenFaults()[0].Id;
            sim.Queue(Cmd.SendCrew(fid));
            for (int i = 0; i < 400 && sim.Grid.FindFault(fid).Open; i++) sim.RunSteps(1);
            sim.RunSteps(1);   // routes are re-derived at the start of the next step
            var d2 = wm.Sync(sim, false);
            Assert.IsTrue(d2.Districts.Exists(x => x.id == "riverside" && x.lit), "lights back on");
            Assert.AreEqual(0, d2.Faults.Count);
        }

        [Test]
        public void World_RoadGraph_TrucksFollowTheSimClock_Monotone()
        {
            var roads = RoadGraph.From(gd.Raw["roads"]);
            foreach (var d in gd.Districts)
            {
                var p = roads.Path(gd.Depot, d.Site);
                Assert.Greater(p.Count, 1, "path to " + d.Site);
                Assert.AreEqual(gd.RoadDistance(gd.Depot, d.Site), TruckTrack.Length(p), 2.0 + p.Count, "visual path = the sim's road distance");
            }
            var sim = new Simulation(gd, 4) { TutorialSafe = true };
            sim.StartShift();
            sim.Queue(Cmd.ScriptFault("c_harbor"));
            sim.RunSteps(1);
            var trucks = new Trucks(roads);
            trucks.Update(sim);
            var start = trucks.Tracks[0].Pos;
            int fid = sim.Grid.OpenFaults()[0].Id;
            sim.Queue(Cmd.SendCrew(fid));
            sim.RunSteps(1);
            double last = -1;
            var site = roads.Nodes[sim.Grid.FindFault(fid).Site];
            while (sim.Grid.Crews[0].State == CrewTruck.Travel)
            {
                trucks.Update(sim);
                double dist = (trucks.Tracks[0].Pos - site).LenXZ;
                if (last >= 0) Assert.LessOrEqual(dist, last + 1e-6, "never drives backwards");
                last = dist;
                sim.RunSteps(1);
            }
            trucks.Update(sim);
            Assert.Less((trucks.Tracks[0].Pos - site).LenXZ, 0.5, "parked at the fault when the sim says it arrived");
            Assert.Greater((start - site).LenXZ, 10);
        }

        [Test]
        public void World_Picking_FaultBeatsDistrict_DistrictFromAbove()
        {
            var L = Layout();
            var d = L.DistrictById["riverside"];
            var center = (d.Min + d.Max) * 0.5;
            var hit = Picker.Pick(L, new V3(center.X, 500, center.Z), new V3(0, -1, 0), null, p => true);
            Assert.AreEqual("district", hit.Kind);
            Assert.AreEqual("riverside", hit.Id);
            var spark = new V3(center.X, center.Y + 30, center.Z);
            var hit2 = Picker.Pick(L, new V3(center.X, 500, center.Z), new V3(0, -1, 0), new List<(int, V3)> { (7, spark) }, p => true);
            Assert.AreEqual("fault", hit2.Kind);
            Assert.AreEqual("7", hit2.Id);
            var site = L.PlantSite["steam"];
            var hit3 = Picker.Pick(L, new V3(site.X, 400, site.Z), new V3(0, -1, 0), null, p => p == 0);
            Assert.AreEqual("plant", hit3.Kind);
            Assert.AreEqual("steam", hit3.Id);
        }

        [Test]
        public void World_Traffic_MovesOnRoads_Low20Medium40()
        {
            var L = Layout();
            var low = new Traffic(L, 20, 5);
            var med = new Traffic(L, 40, 5);
            Assert.AreEqual(20, low.Cars.Count);
            Assert.AreEqual(40, med.Cars.Count);
            var before = low.Cars.Select(c => c.Pos).ToList();
            for (int i = 0; i < 15; i++) low.Step(1.0 / 15);
            int moved = low.Cars.Where((c, i) => (c.Pos - before[i]).LenXZ > 1).Count();
            Assert.Greater(moved, 15);
            foreach (var c in low.Cars)
            {
                Assert.IsTrue(c.Pos.X > L.BoundsMin.X - 100 && c.Pos.X < L.BoundsMax.X + 100);
                Assert.IsTrue(c.Pos.Z > L.BoundsMin.Z - 100 && c.Pos.Z < L.BoundsMax.Z + 100);
            }
        }

        [Test]
        public void Hints_NeverInGuide_ForecastAfterSecondShift()
        {
            var s = new GameSession(gd, TempDir());
            s.LoadOrNew(2);
            Assert.AreEqual("", Hints.Next(s.Sim, s.Settings, new List<SimEvent>(), "grid", 1), "guide not done");
            s.Settings.CoachDone = true;
            s.Sim.S.ShiftNo = 2;
            Assert.AreEqual("forecast", Hints.Next(s.Sim, s.Settings, new List<SimEvent>(), "grid", 1));
            s.Settings.MarkHint("forecast");
            Assert.AreEqual("", Hints.Next(s.Sim, s.Settings, new List<SimEvent>(), "grid", 1));
            Assert.AreEqual("shop", Hints.Next(s.Sim, s.Settings, new List<SimEvent>(), "grid", 2));
        }
    }
}
