// Charge Nurse — bottom sheets (CoddyKit BottomSheet: content-sized, finger scroll with inertia, no visible scrollbar,
// one action pill per card, fleet §16 / §17). Every sheet is rebuilt from the sim when something changed, never under a
// finger. Guide targets inside a sheet are named "Target_<target>" so the spotlight can find them.
//   patient   the card: symptoms, the colour buttons (shape + colour), send to a free bay, discharge
//   bed       a bay / ward bed: who is in it, discharge, clean
//   board     the capacity board (plan §4): the bottleneck sentence, beds by ward, boarders, next discharges, queue rule,
//             diversion rule, surge team, float staff / neighbour hospital (rewarded), daily bonus
//   staff     six teams: size, how busy, fatigue, hire; technicians per room; burnout
//   build     wings, rooms (X-ray / lab / CT / OR), bays, ward beds
//   research  upgrades on shift time (care · flow · comfort · resilience), one at a time
//   shop      offer, ad-free & packs, upgrades, looks, cash (store prices only), Restore
//   settings  language, music / effects, graphics tier, hospital theme, replay the guide, privacy options (UMP) + policy
//   report    the shift report: stars, numbers, reputation, Next day   ·   offline  the welcome-back card (×2 rewarded)
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using ChargeNurse.Game;
using ChargeNurse.Sim;

namespace ChargeNurse.UI
{
    public sealed partial class GameScreen
    {
        public string SheetKind => sheet != null && sheet.IsOpen ? sheetKind : "";

        public void OpenSheet(string kind, int arg = -1)
        {
            if (sheet != null && sheet.IsOpen && sheetKind == kind && sheetArg == arg) { FillSheet(); return; }
            CloseSheet();
            sheetKind = kind;
            sheetArg = arg;
            openTab = kind;
            sheet = UiKit.BottomSheet("", "", null, 0.78f, L.T("sheet.close"));
            sheet.Closed += () => { openTab = ""; main.SheetClosed(sheetKind); };
            FillSheet();
            sheet.Open(root);
            main.SheetOpened(kind);
        }

        public void CloseSheet()
        {
            if (sheet != null && sheet.IsOpen) sheet.Close();
        }

        void FillSheet()
        {
            if (sheet == null) return;
            sheet.Body.Clear();
            sheet.Actions.Clear();
            switch (sheetKind)
            {
                case "patient": FillPatient(sheetArg); break;
                case "bed": FillBed(sheetArg); break;
                case "board": FillBoard(); break;
                case "staff": FillStaff(); break;
                case "build": FillBuild(); break;
                case "research": FillResearch(); break;
                case "shop": FillShop(); break;
                case "settings": FillSettings(); break;
                case "report": FillReport(Sim.S.History.Count > 0 ? Sim.S.History[Sim.S.History.Count - 1] : null, true); break;
                case "report_last": FillReport(Sim.S.History.Count > 0 ? Sim.S.History[Sim.S.History.Count - 1] : null, false); break;
                case "offline": FillOffline(); break;
            }
        }

        void Title(string title, string sub = "")
        {
            UiKit.SetText(sheet.Header.Title, title);
            sheet.Header.SetSub(sub);
        }

        void Add(VisualElement e) => sheet.Body.Add(e);
        void Section(string text, string trailing = "") => Add(UiKit.SectionHeader(text, trailing));

        UiKit.ListCard Card(string title, string sub, string actionText, Action onAction, bool enabled = true, string kind = "primary", string target = null, string badge = "")
        {
            var c = UiKit.Card(title, sub, null, !enabled && actionText == null, false, badge);
            if (actionText != null)
            {
                var pill = UiKit.ActionPill(actionText, onAction, enabled, kind);
                if (target != null) pill.name = "Target_" + target.Replace(':', '_');
                c.SetSide(pill);
            }
            Add(c.Root);
            return c;
        }

        VisualElement KV(string k, string v, string cls = "")
        {
            var r = new VisualElement();
            r.AddToClassList("cn-row");
            r.Add(UiKit.Text(k, "cn-row__k"));
            var vl = UiKit.Text(v, "cn-row__v");
            if (cls != "") vl.AddToClassList(cls);
            r.Add(vl);
            Add(r);
            return r;
        }

        VisualElement ChipRow() => new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };

        string BuyLabel(BuyInfo b) => b.Ok || b.Reason == "cash" ? Money(b.Cost) : L.T("err." + b.Reason);

        void Buy(string item) => main.Run("buy:" + item);

        void RewardCard(string placement)
        {
            var o = main.Rewards?.Get(placement);
            if (o == null || !o.Show) return;
            string label = Simulation.RewardIsCash(placement)
                ? L.F(o.Video ? "reward.video_cash" : "reward.free_cash", "cash", Money(o.Amount))
                : L.T(o.Video ? "reward.video" : "reward.claim");
            string body = placement == "call_neighbor" ? L.F("reward.call_neighbor.b", "n", (long)o.Amount)
                : placement == "float_nurse" ? L.F("reward.float_nurse.b", "team", L.T("team." + Sim.FloatTarget()))
                : L.T("reward." + placement + ".b");
            Card(L.T("reward." + placement + ".t"), o.Video ? body : body + " " + L.T("reward.free.b"), label, () => main.ClaimReward(placement), true, "tonal");
        }

        string WardName(Bed b) => b.Bay ? L.T("zone.bays") : L.T("ward." + Sim.WardOf(b));

        int BayNumber(int bed)
        {
            int n = 0;
            for (int i = 0; i <= bed && i < Sim.S.Beds.Count; i++) if (Sim.S.Beds[i].Bay == Sim.S.Beds[bed].Bay && (Sim.S.Beds[bed].Bay || Sim.WardOf(Sim.S.Beds[i]) == Sim.WardOf(Sim.S.Beds[bed]))) n++;
            return n;
        }

        // ------------------------------------------------------------------ patient

        void FillPatient(int id)
        {
            var p = Sim.P(id);
            if (p == null) { CloseSheet(); return; }
            Title(L.F("patient.title", "n", p.Id), L.T("stage." + p.Stage) + (p.Amb ? " · " + L.T("patient.ambulance") : ""));
            Add(UiKit.Text(L.T("clue." + p.Sev + "." + (p.Id % 3)), "coddy-body"));
            if (!p.Seen) Add(UiKit.Text(L.F("patient.waited", "min", (long)Math.Round(p.Wait)), "coddy-caption-muted"));
            if (p.Test != "") Add(UiKit.Text(L.F("patient.test", "room", L.T("room." + p.Test)), "coddy-caption-muted"));
            if (p.Stage == Stage.Board) Add(UiKit.Text(L.F("patient.board", "ward", L.T("ward." + p.Ward)), "coddy-caption-muted"));
            if (p.NoteLost) Add(UiKit.Text(L.T("patient.note_lost"), "coddy-caption-muted"));

            if (!p.Seen && Stage.PreDoctor(p.Stage))
            {
                Section(L.T("patient.tag"), p.Tag >= 0 ? TagShape[p.Tag] + " " + L.T("tag." + Sev.Ids[p.Tag]) : "");
                Add(UiKit.Text(L.T("patient.tag_help"), "coddy-caption-muted"));
                var row = ChipRow();
                for (int t = 0; t < Sev.N; t++)
                {
                    int tt = t;
                    var b = UiKit.ActionPill(TagShape[t] + " " + L.T("tag." + Sev.Ids[t]), () => main.Run("tag:" + p.Id + ":" + tt), true, p.Tag == t ? "primary" : "tonal");
                    b.name = "Target_tag_" + t;
                    b.style.borderLeftColor = ColorOfTag(t);
                    b.style.borderLeftWidth = 6;
                    row.Add(b);
                }
                Add(row);
            }
            if (p.Stage == Stage.Bay)
            {
                int bay = -1;
                for (int i = 0; i < Sim.S.Beds.Count; i++) if (Sim.S.Beds[i].Bay && Sim.S.Beds[i].State == BedState.Clean && Sim.S.Beds[i].Patient < 0) { bay = i; break; }
                Card(L.T("patient.send"), bay >= 0 ? L.T("patient.send.d") : L.T("err.bay_busy"), bay >= 0 ? L.F("patient.send_to", "n", BayNumber(bay)) : L.T("err.bay_busy"),
                    () => main.Run("assign:" + bay), bay >= 0, "primary", "send");
            }
            if (p.Stage == Stage.Ready || p.Stage == Stage.WardReady || p.Stage == Stage.Lounge)
                Card(L.T("patient.discharge"), L.T("patient.discharge.d"), L.T("patient.discharge"), () => main.Run("discharge:" + p.Id));
        }

        // ------------------------------------------------------------------ bed

        void FillBed(int b)
        {
            if (b < 0 || b >= Sim.S.Beds.Count) { CloseSheet(); return; }
            var bed = Sim.S.Beds[b];
            Title(L.F(bed.Bay ? "bed.bay" : "bed.ward", "n", BayNumber(b), "ward", WardName(bed)), L.T("bedstate." + bed.State));
            var p = bed.Patient >= 0 ? Sim.P(bed.Patient) : null;
            if (p != null)
            {
                Card(L.F("patient.title", "n", p.Id), (p.Tag >= 0 ? TagShape[p.Tag] + " " + L.T("tag." + Sev.Ids[p.Tag]) + " · " : "") + L.T("stage." + p.Stage), L.T("common.open"),
                    () => OpenSheet("patient", p.Id), true, "tonal");
                if (p.Stage == Stage.Stay) Add(UiKit.Text(L.F("bed.stay_left", "time", Fmt.ClockMin(Math.Min(Sim.Gd.DayEnd, Sim.S.Minute + p.Left))), "coddy-caption-muted"));
                if (p.Stage == Stage.Ready || p.Stage == Stage.WardReady) Card(L.T("patient.discharge"), L.T("patient.discharge.d"), L.T("patient.discharge"), () => main.Run("discharge:" + p.Id));
            }
            if (bed.State == BedState.Dirty || bed.State == BedState.Cleaning)
                Card(L.T("bed.clean"), L.T("bed.clean.d"), L.T("bed.clean"), () => main.Run("clean:" + b));
        }

        // ------------------------------------------------------------------ board

        void FillBoard()
        {
            var s = Sim.S;
            Title(L.T("tab.board"), L.F("board.sub", "r", Fmt.Rating(s.Reputation), "pct", Fmt.Pct(Math.Min(9.99, Sim.EdLoad()), L.Lang)));
            var bn = Sim.FindBottleneck();
            Card(L.T("lens.short." + bn.Key), L.T("lens." + bn.Key), L.T("board.show"), () => { CloseSheet(); main.Run("lens"); }, true, "tonal");
            if (s.Phase == Phases.Plan)
            {
                var crises = Sim.ActiveCrises();
                Add(UiKit.Text(crises.Count == 0 ? L.T("board.forecast_calm") : L.F("board.forecast", "list", string.Join(", ", crises.ConvertAll(c => L.T("crisis." + c + ".t")))), "coddy-body"));
                RewardCard("daily_bonus");
            }
            RewardCard("call_neighbor");
            RewardCard("float_nurse");
            var surge = Sim.CanBuy("surge");
            if (Sim.SurgeDay && surge.Reason != "context")
                Card(L.T("board.surge"), L.T("board.surge.d"), surge.Reason == "owned" ? L.T("board.surge_on") : BuyLabel(surge), () => Buy("surge"), surge.Ok, "primary");

            Section(L.T("board.capacity"));
            int bays = 0, baysUsed = 0, board = 0;
            foreach (var b in s.Beds) if (b.Bay) { bays++; if (b.State != BedState.Clean) baysUsed++; }
            foreach (var p in s.Patients) if (p.Stage == Stage.Board) board++;
            KV(L.T("zone.bays"), L.F("board.used", "n", baysUsed, "m", bays));
            KV(L.T("board.boarding"), Fmt.Int(board, L.Lang), board > 0 ? "cn-row__v--bad" : "");
            foreach (var w in new[] { "obs", "general", "surgical", "icu" })
            {
                int n = Sim.BedsIn(w);
                if (n == 0) continue;
                int busy = 0, dirty = 0, closed = 0, soon = 0;
                foreach (var b in s.Beds)
                {
                    if (b.Bay || Sim.WardOf(b) != w) continue;
                    if (b.State == BedState.Busy) busy++;
                    else if (b.State == BedState.Dirty || b.State == BedState.Cleaning) dirty++;
                    else if (b.State == BedState.Closed) closed++;
                    var p = b.Patient >= 0 ? Sim.P(b.Patient) : null;
                    if (p != null && (p.Stage == Stage.WardReady || (p.Stage == Stage.Stay && p.Left <= 60))) soon++;
                }
                KV(L.T("ward." + w), L.F("board.ward", "busy", busy, "n", n, "dirty", dirty, "soon", soon) + (closed > 0 ? " · " + L.F("board.closed", "n", closed) : ""), busy + closed >= n ? "cn-row__v--bad" : "");
            }

            Section(L.T("rule.queue"));
            Add(UiKit.Text(L.T("rule.queue.help"), "coddy-caption-muted"));
            var row = ChipRow();
            foreach (var q in QueueRules.All)
            {
                string qq = q;
                row.Add(UiKit.ActionPill(L.T("rule." + q), () => main.Run("queue:" + qq), true, s.Queue == q ? "primary" : "tonal"));
            }
            Add(row);
            Section(L.T("rule.divert"));
            if (!Sim.Has("diversion_rules")) Add(UiKit.Text(L.T("rule.divert.locked"), "coddy-caption-muted"));
            else
            {
                var dr = ChipRow();
                foreach (int pct in new[] { 0, 80, 90, 100 })
                {
                    int pp = pct;
                    dr.Add(UiKit.ActionPill(pct == 0 ? L.T("rule.divert.never") : L.F("rule.divert.at", "pct", Fmt.Pct(pct / 100.0, L.Lang)), () => main.Run("divert:" + pp), true, s.DivertAt == pct ? "primary" : "tonal"));
                }
                Add(dr);
            }
        }

        // ------------------------------------------------------------------ staff

        void FillStaff()
        {
            var s = Sim.S;
            Title(L.T("tab.staff"), L.F("staff.burnout", "pct", Fmt.Pct(s.Burnout / 100.0, L.Lang)));
            if (s.Burnout >= Sim.Gd.BurnoutSlowAt) Add(UiKit.Text(L.T("staff.burnout_high"), "coddy-body"));
            for (int t = 0; t < Teams.Pool.Length; t++)
            {
                string id = Teams.Pool[t];
                double fat = s.Fatigue.TryGetValue(id, out var f) ? f : 0;
                string sub = L.F("staff.line", "n", Sim.Team(id), "busy", Fmt.Pct(Sim.Utilisation(id), L.Lang), "tired", Fmt.Pct(fat / 100.0, L.Lang))
                    + (s.SickTeam == id ? " · " + L.T("staff.sick") : "") + (s.FloatTeam == id && s.FloatLeft > 0 ? " · " + L.T("staff.float") : "");
                var b = Sim.CanBuy("hire:" + id);
                Card(L.T("team." + id), sub + "\n" + L.T("team." + id + ".d"), b.Reason == "max" ? L.T("err.max") : L.F("staff.hire", "cost", Money(b.Cost)), () => Buy("hire:" + id), b.Ok, "primary", "hire:" + id);
            }
            Section(L.T("team.tech"));
            Add(UiKit.Text(L.T("team.tech.d"), "coddy-caption-muted"));
            foreach (var r in s.Rooms)
            {
                if (r.Kind == "or") continue;
                string k = r.Kind;
                var b = Sim.CanBuy("tech:" + k);
                Card(L.T("room." + k), L.F("staff.techs", "n", r.Techs) + (r.Down > 0 ? " · " + L.T("room.down") : ""), b.Reason == "max" ? L.T("err.max") : L.F("staff.hire", "cost", Money(b.Cost)), () => Buy("tech:" + k), b.Ok, "primary", "tech:" + k);
            }
        }

        // ------------------------------------------------------------------ build

        void FillBuild()
        {
            Title(L.T("tab.build"), L.F("hud.reputation", "r", Fmt.Rating(Sim.S.Reputation)));
            foreach (var w in Sim.Gd.Wings)
            {
                if (Sim.Unlocked(w.Id)) continue;
                var b = Sim.CanBuy("wing:" + w.Id);
                if (b.Reason == "locked") continue;
                Card(L.T("wing." + w.Id), L.T("wing." + w.Id + ".d") + " · " + L.F("wing.needs", "r", (long)w.Reputation),
                    b.Reason == "reputation" ? L.T("err.reputation") : L.F("wing.open", "cost", Money(w.Cost)), () => Buy("wing:" + w.Id), b.Ok, "primary", "wing:" + w.Id);
                break;
            }
            Section(L.T("build.rooms"));
            foreach (var kind in new[] { "xray", "lab", "ct", "or" })
            {
                var b = Sim.CanBuy("room:" + kind);
                if (b.Reason == "owned") { Card(L.T("room." + kind), L.T("room." + kind + ".d"), L.T("shop.owned"), null, false, "tonal"); continue; }
                string k = kind;
                Card(L.T("room." + kind), L.T("room." + kind + ".d"), b.Reason == "wing" ? L.T("err.wing") : BuyLabel(b), () => Buy("room:" + k), b.Ok, "primary", "room:" + kind);
            }
            Section(L.T("build.beds"));
            var bay = Sim.CanBuy("bay");
            if (bay.Reason != "max") Card(L.T("build.bay"), L.F("build.bay.d", "n", Sim.Bays), bay.Reason == "wing" ? L.T("err.wing") : BuyLabel(bay), () => Buy("bay"), bay.Ok);
            foreach (var w in new[] { "obs", "general", "surgical", "icu" })
            {
                var b = Sim.CanBuy("bed:" + w);
                if (b.Reason == "wing" || b.Reason == "invalid") continue;
                string ww = w;
                Card(L.F("build.bed", "ward", L.T("ward." + w)), L.F("build.bed.d", "n", Sim.BedsIn(w)), b.Reason == "max" ? L.T("err.max") : BuyLabel(b), () => Buy("bed:" + ww), b.Ok);
            }
        }

        // ------------------------------------------------------------------ research

        void FillResearch()
        {
            string running = Sim.S.Researching;
            Title(L.T("tab.research"), running != "" ? L.T("research." + running + ".t") + " · " + L.F("research.left", "min", (long)Math.Ceiling(Sim.S.ResearchLeft)) : L.T("research.help"));
            foreach (var branch in new[] { "care", "flow", "comfort", "resilience" })
            {
                Section(L.T("research.branch." + branch));
                foreach (var r in Sim.Gd.Research)
                {
                    if (r.Branch != branch) continue;
                    string why = Sim.CanResearch(r.Id);
                    string id = r.Id;
                    string action = why == "owned" ? L.T("research.done") : why == "running" ? L.F("research.left", "min", (long)Math.Ceiling(Sim.S.ResearchLeft))
                        : why == "requires" ? L.F("research.requires", "name", L.T("research." + r.Requires[0] + ".t"))
                        : why == "wing" ? L.F("research.wing", "name", L.T("wing." + r.Wing)) : L.F("research.start", "cost", Money(r.Cost));
                    Card(L.T("research." + id + ".t"), L.T("research." + id + ".d") + " · " + L.F("research.shift_min", "min", (long)r.Minutes), action, () => main.Run("research:" + id), why == "", "primary");
                }
            }
        }

        // ------------------------------------------------------------------ shop

        void FillShop()
        {
            var shop = main.Shop;
            Title(L.T("shop.title"));
            if (shop == null) return;
            foreach (var group in shop.Cat.Groups)
            {
                var slugs = shop.Visible(group);
                if (slugs.Count == 0) continue;
                Section(shop.Cat.Group(group, L.Lang));
                foreach (var slug in slugs)
                {
                    string s = slug;
                    string why = shop.WhyNot(slug);
                    string price = shop.Price(slug);
                    string label = why == "owned" ? L.T("shop.owned") : price != "" ? price : Commerce.Loading;
                    Card(shop.Cat.Title(slug, L.Lang), shop.Cat.Desc(slug, L.Lang), label, () => shop.Buy(s), why == "", "primary", null, shop.ActiveOffer == slug ? L.T("shop.offer") : "");
                }
            }
            var restore = UiKit.ActionPill(L.T("shop.restore"), () => shop.Restore(), true, "tonal");
            sheet.Actions.Add(restore);
        }

        // ------------------------------------------------------------------ settings

        void FillSettings()
        {
            var st = main.Session.Settings;
            Title(L.T("settings.title"), L.F("settings.version", "v", Application.version));
            Section(L.T("settings.language"));
            var langs = ChipRow();
            for (int i = 0; i < Loc.Languages.Length; i++)
            {
                string code = Loc.Languages[i];
                langs.Add(UiKit.ActionPill(Loc.NativeNames[i], () => main.SetLanguage(code), true, L.Lang == code ? "primary" : "tonal"));
            }
            Add(langs);
            Section(L.T("settings.sound"));
            Card(L.T("settings.music"), "", L.T(st.Music ? "settings.on" : "settings.off"), () => main.SetAudio(!st.Music, st.Effects), true, st.Music ? "primary" : "tonal");
            Card(L.T("settings.effects"), "", L.T(st.Effects ? "settings.on" : "settings.off"), () => main.SetAudio(st.Music, !st.Effects), true, st.Effects ? "primary" : "tonal");
            Section(L.T("settings.graphics"));
            var tiers = ChipRow();
            foreach (var t in new[] { Tier.Low, Tier.Medium, Tier.High })
            {
                var tt = t;
                tiers.Add(UiKit.ActionPill(L.T("graphics." + t.ToString().ToLowerInvariant()), () => main.SetTier(tt), true, main.CurrentTier == t ? "primary" : "tonal"));
            }
            Add(tiers);
            Section(L.T("settings.theme"));
            var themes = ChipRow();
            foreach (var th in new[] { "", "gold_theme", "theme_modern", "theme_retro", "theme_pediatric" })
            {
                if (th != "" && (main.Shop == null || !main.Shop.Has(th))) continue;
                string t2 = th;
                themes.Add(UiKit.ActionPill(L.T(th == "" ? "theme.default" : "theme." + th), () => main.SetTheme(t2), true, st.Theme == th ? "primary" : "tonal"));
            }
            Add(themes);
            Section(L.T("settings.privacy"));
            Card(L.T("settings.guide"), "", L.T("intro.start"), main.ReplayGuide, true, "tonal");
            Card(L.T("settings.privacy_options"), "", L.T("common.open"), main.ShowPrivacyOptions, true, "tonal");
            Card(L.T("settings.policy"), "", L.T("common.open"), () => Application.OpenURL(ChargeNurse.App.NurseGameConfig.PrivacyUrl), true, "tonal");
            Card(L.T("settings.restore"), "", L.T("common.ok"), () => main.Shop?.Restore(), true, "tonal");
        }

        // ------------------------------------------------------------------ report / offline

        void FillReport(DayStats d, bool withNext)
        {
            if (d == null) { Title(L.T("phase.report")); return; }
            Title(L.F("report.title", "n", d.Day), L.F("report.reputation", "r", Fmt.Rating(Sim.S.Reputation)));
            var stars = new VisualElement();
            stars.AddToClassList("cn-report__stars");
            void Star(string key, int n)
            {
                var c = new VisualElement();
                c.AddToClassList("cn-report__star");
                c.Add(UiKit.Text(new string('★', n) + new string('☆', 3 - n), "cn-report__star-value"));
                c.Add(UiKit.Text(L.T(key), "coddy-caption-muted"));
                stars.Add(c);
            }
            Star("report.care", d.StarCare);
            Star("report.waits", d.StarWaits);
            Star("report.flow", d.StarFlow);
            Add(stars);
            Add(UiKit.Text(L.T(d.Rushed ? "report.rushed" : "report.calm"), "coddy-caption-muted"));
            KV(L.T("report.arrivals"), Fmt.Int(d.Arrivals, L.Lang));
            KV(L.T("report.treated"), Fmt.Int(d.Treated, L.Lang));
            KV(L.T("report.wait"), L.F("report.minutes", "n", (long)Math.Round(d.AvgWait)), d.AvgWait > Sim.Gd.StarWaits[1] ? "cn-row__v--bad" : "");
            KV(L.T("report.bad"), L.F("report.bad_v", "lwbs", d.Lwbs, "worse", d.Worsened, "transfer", d.Transferred), d.Bad > 0 ? "cn-row__v--bad" : "");
            KV(L.T("report.blocked"), Fmt.Pct(d.BlockedShare, L.Lang), d.BlockedShare > Sim.Gd.StarFlow[1] ? "cn-row__v--bad" : "");
            if (d.Diverted > 0) KV(L.T("report.diverted"), Fmt.Int(d.Diverted, L.Lang));
            KV(L.T("report.night"), L.F("report.night_v", "n", d.NightTreated, "cash", Money(d.NightFees)));
            KV(L.T("report.fees"), Money(d.Fees));
            if (d.Ward > 0) KV(L.T("report.ward"), Money(d.Ward));
            if (d.Bonus > 0) KV(L.T("report.bonus"), Money(d.Bonus));
            KV(L.T("report.wages"), "−" + Money(d.Wages));
            KV(L.T("report.supplies"), "−" + Money(d.Supplies));
            KV(L.T("report.upkeep"), "−" + Money(d.Upkeep));
            if (d.Fines > 0) KV(L.T("report.fines"), "−" + Money(d.Fines), "cn-row__v--bad");
            KV(L.T("report.net"), Money(d.Net), d.Net >= 0 ? "cn-row__v--good" : "cn-row__v--bad");
            if (withNext && Sim.Phase == Phases.Report)
            {
                var next = UiKit.PrimaryButton(L.T("hud.next_day"), () => main.Run("next_day"));
                next.name = "Target_next_day";
                sheet.Actions.Add(next);
            }
        }

        void FillOffline()
        {
            var o = main.LastOffline;
            Title(L.T("offline.title"));
            if (o == null) return;
            Add(UiKit.Text(L.F("offline.body", "cash", Money(o.Amount), "time", Fmt.Dur(o.Seconds, L)), "coddy-body"));
            if (o.Capped) Add(UiKit.Text(L.F("offline.capped", "h", (long)Math.Round(o.Hours)), "coddy-caption-muted"));
            RewardCard("offline_x2");
            sheet.Actions.Add(UiKit.PrimaryButton(L.T("common.ok"), CloseSheet));
        }
    }
}
