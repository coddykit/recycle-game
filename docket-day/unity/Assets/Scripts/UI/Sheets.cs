// Docket Day — bottom sheets (CoddyKit BottomSheet: content-sized, finger scroll with inertia, no visible scrollbar,
// one action pill per card, fleet §16 / §17). Every sheet is rebuilt from the sim when something changed, never under a
// finger. Guide targets inside a sheet are named "Target_<target>" so the spotlight can find them.
//   intake    reception: waiting clients (area, fee model, judge, court window, conflict badge) -> Accept / Decline /
//             Refer / Ethical wall; who is coming next (case preview, rewarded); standing instructions (auto-intake)
//   matter    the matter card: parties, judge habit, odds (± until the twist is known), trust held, tasks (who, progress,
//             due), the prep table, levers (settle, visiting counsel, continuance), collision warning
//   prep      the prep table: match each claim with its exhibit (hints on in the guide)
//   matters   every open matter by court date · team  people, hours, hire / let go · office  chapter, rooms, looks
//   court     today's hearings · shop · settings · report (stars, numbers, billing boost, Billing run, Next day) · offline
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using DocketDay.Game;
using DocketDay.Sim;

namespace DocketDay.UI
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
            sheet = UiKit.BottomSheet("", "", null, kind == "intake" || kind == "matter" ? 0.7f : 0.78f, L.T("sheet.close"));
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
                case "intake": FillIntake(); break;
                case "matter": FillMatter(sheetArg); break;
                case "prep": FillPrep(sheetArg); break;
                case "matters": FillMatters(); break;
                case "team": FillTeam(); break;
                case "office": FillOffice(); break;
                case "court": FillCourt(); break;
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
        void Line(string text, string cls = "coddy-body") => Add(UiKit.Text(text, cls));

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

        VisualElement Pills()
        {
            var r = new VisualElement();
            r.AddToClassList("dd-pills");
            Add(r);
            return r;
        }

        Button Pill(VisualElement row, string text, Action a, bool enabled = true, string kind = "tonal", string target = null)
        {
            var b = UiKit.ActionPill(text, a, enabled, kind);
            if (target != null) b.name = "Target_" + target.Replace(':', '_');
            row.Add(b);
            return b;
        }

        VisualElement KV(string k, string v, string cls = "")
        {
            var r = new VisualElement();
            r.AddToClassList("dd-row");
            r.Add(UiKit.Text(k, "dd-row__k"));
            var vl = UiKit.Text(v, "dd-row__v");
            if (cls != "") vl.AddToClassList(cls);
            r.Add(vl);
            Add(r);
            return r;
        }

        string BuyLabel(BuyInfo b, string key = "office.build") => b.Ok || b.Reason == "cash" ? L.F(key, "cost", Money(b.Cost)) : L.T("err." + b.Reason);
        string Pct(double p) => Fmt.Pct(p, L.Lang);
        string MatterTitle(MatterState m) => L.T("matter." + m.Area + "." + m.Title);
        string Party(int i) => i >= 0 && i < Sim.Gd.Parties.Count ? L.T("party." + Sim.Gd.Parties[i]) : "?";

        void RewardCard(string placement)
        {
            var o = main.Rewards?.Get(placement);
            if (o == null || !o.Show) return;
            string label = Simulation.RewardIsCash(placement)
                ? L.F(o.Video ? "reward.video_cash" : "reward.free_cash", "cash", Money(o.Amount))
                : L.T(o.Video ? "reward.video" : "reward.claim");
            Card(L.T("reward." + placement + ".t"), o.Video ? L.T("reward." + placement + ".b") : L.T("reward.free.b"), label, () => main.ClaimReward(placement), true, "tonal");
        }

        string FeeLine(MatterState m)
        {
            var a = Sim.AreaOf(m);
            string money = m.Fee == Fees.Contingency ? L.F("intake.value", "cash", Money(a.Value * Sim.TierOf(m).Fee))
                : m.Fee == Fees.Retainer ? L.F("intake.retainer", "cash", Money(a.Retainer * Sim.TierOf(m).Fee)) : L.F("intake.win_fee", "cash", Money(Sim.WinFee(m)));
            return L.T("area." + m.Area) + " · " + L.T("tier." + m.Tier) + " · " + L.T("fee." + m.Fee) + " · " + money;
        }

        string TwistLine(MatterState m) => !m.Revealed ? L.T("intake.twist_hidden") : m.Twist > 0.001 ? L.T("intake.twist_hard") : m.Twist < -0.001 ? L.T("intake.twist_easy") : L.T("intake.twist_none");

        // ------------------------------------------------------------------ intake (reception)

        void FillIntake()
        {
            Title(L.T("intake.title"), L.F("intake.waiting", "n", Sim.IntakeCount));
            bool any = false;
            foreach (var m in new List<MatterState>(Sim.Intakes))
            {
                any = true;
                int id = m.Id;
                var a = Sim.AreaOf(m);
                bool conflict = Sim.Conflict(m);
                var c = UiKit.Card(MatterTitle(m), L.F("matter.parties", "client", Party(m.Client), "opponent", Party(m.Opponent)), null, false, false, conflict ? L.T("rail.collision") : L.T("tier." + m.Tier));
                if (conflict) c.Root.AddToClassList("dd-card--conflict");
                Add(c.Root);
                Line(FeeLine(m), "coddy-caption-muted");
                Line(L.T("judge." + Sim.Gd.Judges[m.Judge].Id) + " — " + L.T("judge." + Sim.Gd.Judges[m.Judge].Id + ".d") + " · " + L.F("intake.hearing_in", "a", a.AfterMin, "b", a.AfterMax), "coddy-caption-muted");
                Line(TwistLine(m) + (m.Tutorial ? "" : " · " + L.F("intake.patience", "time", Fmt.ClockMin(m.Expires))), "coddy-caption-muted");
                if (conflict) Line(L.T("intake.conflict"), "dd-warn");
                var row = Pills();
                Pill(row, L.T("intake.accept"), () => main.Run("accept:" + id), !conflict, "primary", "accept:" + id);
                if (conflict) Pill(row, L.F("intake.wall", "cost", Money(Sim.Gd.WallCost)), () => main.Run("wall:" + id), Sim.CanAccept(id, true) == "");
                if (!m.Tutorial)
                {
                    Pill(row, L.T("intake.refer"), () => main.Run("refer:" + id));
                    Pill(row, L.T("intake.decline"), () => main.Run("decline:" + id));
                }
            }
            if (!any) Line(L.T("intake.empty"), "coddy-caption-muted");
            if (main.Coach.Active) return;
            Section(L.T("intake.upcoming"));
            foreach (var m in Sim.Upcoming) Line(MatterTitle(m) + " · " + L.T("area." + m.Area) + " · " + TwistLine(m), "coddy-caption-muted");
            RewardCard("case_preview");
            Section(L.T("intake.rules"));
            Line(L.F("intake.rules_help", "n", Sim.S.Rules.Count, "m", Sim.MaxRules), "coddy-caption-muted");
            var rules = Pills();
            foreach (var a in Sim.Gd.Areas)
            {
                if (!Sim.AreaUnlocked(a.Id)) continue;
                bool on = Sim.S.Rules.Contains(a.Id);
                string aid = a.Id;
                Pill(rules, L.T("area." + a.Id) + " · " + L.T(on ? "intake.rule_on" : "intake.rule_off"), () => main.Run("rule:" + aid + ":" + (on ? 0 : 1)), true, on ? "primary" : "tonal");
            }
        }

        // ------------------------------------------------------------------ matter card

        void FillMatter(int id)
        {
            var m = Sim.MatterById(id);
            if (m == null) { CloseSheet(); return; }
            if (m.Status == MatterStatus.Intake) { sheetKind = "intake"; FillIntake(); return; }
            var j = Sim.Gd.Judges[m.Judge];
            Title(MatterTitle(m), L.F("matter.parties", "client", Party(m.Client), "opponent", Party(m.Opponent)));
            Line(FeeLine(m), "coddy-caption-muted");
            if (m.Wall) Line(L.T("matter.wall"), "coddy-caption-muted");
            KV(L.T("judge." + j.Id), L.T("judge." + j.Id + ".d"));
            KV(L.T("matter.twist_label"), TwistLine(m));
            KV(L.T("matter.odds_label"), m.Revealed ? Pct(Sim.ShownOdds(m)) : Pct(Sim.ShownOdds(m)) + " ±?", Sim.ShownOdds(m) >= 0.7 ? "dd-row__v--good" : Sim.ShownOdds(m) < 0.5 ? "dd-row__v--bad" : "");
            if (m.Trust > 0) KV(L.T("matter.trust_label"), Money(m.Trust));
            if (m.Status == MatterStatus.Hearing) { Line(L.T("matter.in_session"), "dd-warn"); return; }
            var col = Sim.Collisions().Find(c => c.Matter == m.Id);
            if (col != null) Line(L.T(col.Kind == "double" ? "matter.collision" : "matter.uncovered"), "dd-warn");

            Section(L.T("matter.tasks"));
            for (int i = 0; i < m.Tasks.Count; i++)
            {
                var t = m.Tasks[i];
                int ti = i;
                string who = t.Staff == Attend.None ? L.T("matter.nobody") : StaffName(t.Staff);
                string sub;
                if (t.Kind == Kinds.Hearing)
                    sub = L.F("matter.hearing_line", "day", DayLabel(t.Due), "time", Fmt.ClockMin(Sim.Gd.HearingSlots[Math.Min(t.Slot, Sim.Gd.HearingSlots.Length - 1)])) + " · "
                        + (t.Staff == Attend.Visiting ? L.T("matter.visiting") : L.F("matter.attends", "name", who));
                else
                    sub = (t.Finished ? L.T("matter.done") : t.Late ? L.T("matter.late") : t.Due <= Sim.S.Day ? L.T("matter.due_today") : L.F("matter.due", "day", DayLabel(t.Due)))
                        + " · " + L.F("matter.progress", "done", t.Done.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), "need", t.Need.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)) + " · " + who;
                bool canRemove = !t.Finished && t.Staff >= 0;
                Card(L.T("kind." + t.Kind), sub, canRemove ? L.T("matter.unassign") : null, () => main.Run("unassign:" + m.Id + ":" + ti), canRemove, "tonal");
            }
            Line(L.T("rail.drag_hint"), "coddy-caption-muted");

            Section(L.T("matter.prep_open"));
            if (m.PuzzleSolved) Line(L.T("matter.prep_done"), "coddy-caption-muted");
            else if (!Sim.PrepOpen(m)) Line(L.T("matter.prep_locked"), "coddy-caption-muted");
            else Card(L.T("prep.title"), L.T("prep.help"), L.T("settings.open"), () => OpenSheet("prep", m.Id), true, "primary", "prep:" + m.Id);

            Section(L.T("matter.levers"));
            double settle = Sim.SettleFee(m);
            Card(L.F("matter.settle", "cash", Money(settle)), L.T("matter.settle_d") + " " + L.F("matter.litigate_d", "win", Money(Sim.WinFee(m)), "loss", Money(Sim.LossFee(m))),
                L.T("lever.settle"), () => main.Run("settle:" + m.Id), !main.Coach.Active, "primary");
            var h = m.HearingTask;
            if (h != null && h.Staff != Attend.Visiting)
                Card(L.F("matter.visiting_btn", "cash", Money(Sim.VisitingCost(m.Id))), L.T("matter.visiting_d"), L.T("lever.visiting"), () => main.Run("visiting:" + m.Id), Sim.S.Cash >= Sim.VisitingCost(m.Id) && !main.Coach.Active, "tonal");
            if (!m.Continued) Card(L.T("matter.continuance"), L.T("matter.continuance_d"), L.T("lever.continue"), () => main.Run("continuance:" + m.Id), !main.Coach.Active && !m.Tutorial, "tonal");
        }

        // ------------------------------------------------------------------ prep table

        void FillPrep(int id)
        {
            var m = Sim.MatterById(id);
            if (m == null || m.Status != MatterStatus.Open) { CloseSheet(); return; }
            Title(L.T("prep.title"), MatterTitle(m));
            Line(L.T("prep.help"), "coddy-caption-muted");
            if (m.PuzzleSolved) { Line(L.T("prep.done"), "dd-good"); sheet.Actions.Add(UiKit.PrimaryButton(L.T("common.ok"), () => OpenSheet("matter", id))); return; }
            int claim = 0;
            while (m.Matched.Contains(claim)) claim++;
            bool hints = main.Coach.Active;
            for (int c = 0; c < m.Claims; c++)
            {
                bool done = m.Matched.Contains(c);
                var card = UiKit.Card(L.F("prep.claim", "n", c + 1, "text", L.T("claim." + c)), done ? "✓ " + L.T("exhibit." + c) : "", null, false, c == claim, "");
                Add(card.Root);
            }
            Section(L.T("prep.title"));
            var row = Pills();
            int cl = claim;
            foreach (int e in m.Exhibits)
            {
                if (e < m.Claims && m.Matched.Contains(e)) continue;
                int ex = e;
                var b = Pill(row, e >= m.Claims ? L.T("exhibit.decoy") : L.T("exhibit." + e), () => main.Run("prep_match:" + id + ":" + cl + ":" + ex), true, hints && ex == cl ? "primary" : "tonal");
                if (ex == cl) b.name = "Target_prep_" + id;
            }
            if (hints) Line(L.T("prep.hint"), "coddy-caption-muted");
        }

        // ------------------------------------------------------------------ matters / court

        void FillMatters()
        {
            var list = new List<MatterState>();
            foreach (var m in Sim.S.Matters) if (m.Active) list.Add(m);
            list.Sort((a, b) => a.HearingTask.Due.CompareTo(b.HearingTask.Due));
            int coll = Sim.Collisions().Count;
            Title(L.T("matters.title"), coll > 0 ? L.F("matters.collisions", "n", coll) : "");
            if (list.Count == 0) Line(L.T("matters.empty"), "coddy-caption-muted");
            foreach (var m in list)
            {
                int id = m.Id;
                string sub = FeeLine(m) + " · " + L.F("matter.hearing_line", "day", DayLabel(m.HearingTask.Due), "time", Fmt.ClockMin(Sim.Gd.HearingSlots[Math.Min(m.HearingTask.Slot, Sim.Gd.HearingSlots.Length - 1)]));
                Card(MatterTitle(m), sub, L.F(m.Revealed ? "matter.odds" : "matter.odds_hidden", "pct", Pct(Sim.ShownOdds(m))), () => OpenSheet("matter", id), true, "tonal", null, Sim.InCollision(id) ? L.T("rail.collision") : "");
            }
        }

        void FillCourt()
        {
            Title(L.T("court.title"), L.T("court.today"));
            bool any = false;
            foreach (var m in Sim.S.Matters)
            {
                if (!m.Active || m.HearingTask.Due != Sim.S.Day) continue;
                any = true;
                int id = m.Id;
                Card(MatterTitle(m), L.T("judge." + Sim.Gd.Judges[m.Judge].Id) + " · " + Fmt.ClockMin(Sim.Gd.HearingSlots[Math.Min(m.HearingTask.Slot, Sim.Gd.HearingSlots.Length - 1)]),
                    L.F("matter.odds", "pct", Pct(Sim.ShownOdds(m))), () => OpenSheet("matter", id), true, "tonal");
            }
            if (!any) Line(L.T("court.none"), "coddy-caption-muted");
        }

        // ------------------------------------------------------------------ team

        void FillTeam()
        {
            Title(L.T("team.title"), L.F("team.sub", "n", Sim.S.Staff.Count, "d", Sim.S.Staff.FindAll(x => x.TempUntil <= 0).Count, "m", Sim.DeskCap));
            Section(L.T("team.roster"));
            foreach (var s in Sim.S.Staff)
            {
                var r = Sim.RoleOf(s);
                int id = s.Id;
                string state = s.Sick ? L.T("staff.sick") : Sim.InSession(id) ? L.T("staff.in_court") : Sim.Tired(s) ? L.T("staff.tired") : "";
                string sub = L.T("role." + s.Role) + " · " + L.F("staff.hours", "left", s.HoursLeft.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), "total", r.Hours)
                    + " · " + L.F("staff.queue", "h", Sim.QueuedHours(s).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)) + (state != "" ? " · " + state : "") + (s.TempUntil > 0 ? " · " + L.T("staff.day_pass") : "");
                string name = Sim.IsFounder(s) ? L.T("staff.founder") : L.T("staff.name." + s.Name);
                if (Sim.IsFounder(s) || s.TempUntil > 0) Card(name, sub, null, null);
                else Card(name, sub, L.T("team.fire"), () => main.Run("fire:" + id), !main.Coach.Active, "tonal");
            }
            Section(L.T("team.hire"));
            foreach (var role in Sim.Gd.RoleOrder)
            {
                var b = Sim.CanHire(role);
                if (b.Reason == "locked" || b.Reason == "max") continue;
                var d = Sim.Gd.Roles[role];
                string rr = role;
                Card(L.T("role." + role), L.T("role." + role + ".d") + " · " + L.F("team.rate", "rate", Money(d.Rate), "salary", Money(d.Salary)), BuyLabel(b, "team.hire_cost"), () => main.Run("hire:" + rr), b.Ok);
            }
        }

        // ------------------------------------------------------------------ office

        void FillOffice()
        {
            var ch = Sim.Gd.ChapterOf(Sim.Chapter);
            Title(L.T("office.title"), L.F("office.chapter", "n", Sim.Chapter, "name", L.T("chapter." + ch.Id)));
            if (Sim.S.Building != "") Line(L.F("office.building", "name", L.T("room." + Sim.S.Building), "time", Fmt.Dur(Sim.BuildLeftS, L)), "dd-good");
            var next = Sim.NextChapter;
            if (next != null)
            {
                string why = Sim.CanMoveUp();
                Card(L.F("office.move_up", "name", L.T("chapter." + next.Id)),
                    L.F("office.needs", "s", next.Standing.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), "n", next.Closed,
                        "have", Sim.FirmStanding.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), "closed", Sim.S.Closed),
                    why == "" || why == "cash" ? L.F("office.move_cost", "cost", Money(next.Cost)) : L.T("err." + why), () => main.Run("move_up"), why == "" && !main.Coach.Active);
            }
            else Line(L.T("office.max"), "coddy-caption-muted");
            Section(L.T("office.rooms"), L.F("office.desks", "n", Sim.S.Staff.FindAll(x => x.TempUntil <= 0).Count, "m", Sim.DeskCap));
            foreach (var r in Sim.Gd.Rooms)
            {
                var b = Sim.CanBuild(r.Id);
                if (b.Reason == "locked") continue;
                string id = r.Id;
                string label = b.Reason == "owned" ? L.T("office.built") : b.Reason == "building" ? L.T("err.building") : BuyLabel(b);
                bool allowed = b.Ok && (!main.Coach.Active || id == "library");
                Card(L.T("room." + r.Id), L.T("room." + r.Id + ".d") + (r.Max > 1 ? " · " + Sim.RoomCount(r.Id) + "/" + r.Max : "") + " · " + Fmt.Dur(r.BuildS, L), label, () => main.Run("build:" + id), allowed, "primary", "build:" + id);
            }
            if (main.Coach.Active) return;
            // looks (owned cosmetics; never sold by ads)
            var shop = main.Shop;
            if (shop == null) return;
            var themes = new List<string>();
            foreach (var t in new[] { "gold_nameplate", "theme_classic", "theme_loft", "theme_deco" }) if (shop.Has(t)) themes.Add(t);
            if (themes.Count == 0 && !shop.Has("court_themes") && !shop.Has("outfits")) return;
            Section(L.T("office.looks"));
            var st = main.Session.Settings;
            var row = Pills();
            Pill(row, L.T("looks.default"), () => main.SetLooks("", st.CourtTheme, st.Outfits), true, st.OfficeTheme == "" ? "primary" : "tonal");
            foreach (var t in themes)
            {
                string tt = t;
                Pill(row, t == "gold_nameplate" ? L.T("iap.premium.t") : L.T("iap." + t + ".t"), () => main.SetLooks(tt, st.CourtTheme, st.Outfits), true, st.OfficeTheme == t ? "primary" : "tonal");
            }
            if (shop.Has("court_themes")) Card(L.T("iap.court_themes.t"), "", L.T(st.CourtTheme ? "settings.on" : "settings.off"), () => main.SetLooks(st.OfficeTheme, !st.CourtTheme, st.Outfits), true, st.CourtTheme ? "primary" : "tonal");
            if (shop.Has("outfits")) Card(L.T("iap.outfits.t"), "", L.T(st.Outfits ? "settings.on" : "settings.off"), () => main.SetLooks(st.OfficeTheme, st.CourtTheme, !st.Outfits), true, st.Outfits ? "primary" : "tonal");
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
            var langs = Pills();
            for (int i = 0; i < Loc.Languages.Length; i++)
            {
                string code = Loc.Languages[i];
                Pill(langs, Loc.NativeNames[i], () => main.SetLanguage(code), true, L.Lang == code ? "primary" : "tonal");
            }
            Section(L.T("settings.sound"));
            Card(L.T("settings.music"), "", L.T(st.Music ? "settings.on" : "settings.off"), () => main.SetAudio(!st.Music, st.Effects), true, st.Music ? "primary" : "tonal");
            Card(L.T("settings.effects"), "", L.T(st.Effects ? "settings.on" : "settings.off"), () => main.SetAudio(st.Music, !st.Effects), true, st.Effects ? "primary" : "tonal");
            Card(L.T("settings.objection"), "", L.T(st.ObjectionTap ? "settings.on" : "settings.off"), () => main.SetObjection(!st.ObjectionTap), true, st.ObjectionTap ? "primary" : "tonal");
            Section(L.T("settings.graphics"));
            var tiers = Pills();
            foreach (var t in new[] { Tier.Low, Tier.Medium, Tier.High })
            {
                var tt = t;
                Pill(tiers, L.T("graphics." + t.ToString().ToLowerInvariant()), () => main.SetTier(tt), true, main.CurrentTier == t ? "primary" : "tonal");
            }
            Section(L.T("settings.help"));
            Card(L.T("settings.guide"), "", L.T("intro.start"), main.ReplayGuide, true, "tonal");
            Section(L.T("settings.privacy"));
            Card(L.T("settings.privacy"), "", L.T("settings.open"), main.ShowPrivacyOptions, true, "tonal");
            Card(L.T("settings.policy"), "", L.T("settings.open"), () => Application.OpenURL(DocketDay.App.DocketGameConfig.PrivacyUrl), true, "tonal");
            Card(L.T("settings.restore"), "", L.T("shop.restore"), () => main.Shop?.Restore(), true, "tonal");
            Section(L.T("settings.about"));
            Line(L.T("settings.about_text"), "coddy-caption-muted");
        }

        // ------------------------------------------------------------------ report / offline

        void FillReport(DayStats d, bool withActions)
        {
            if (d == null) { Title(L.T("phase.report")); return; }
            Title(L.F("report.title", "n", d.Day), L.F("report.standing", "s", Sim.FirmStanding.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
            var stars = new VisualElement();
            stars.AddToClassList("dd-report__stars");
            void Star(string key, int n)
            {
                var c = new VisualElement();
                c.AddToClassList("dd-report__star");
                c.Add(UiKit.Text(new string('★', n) + new string('☆', 3 - n), "dd-report__star-value"));
                c.Add(UiKit.Text(L.T(key), "coddy-caption-muted"));
                stars.Add(c);
            }
            Star("report.deadlines", d.StarDeadlines);
            Star("report.clients", d.StarClients);
            Star("report.billing", d.StarBilling);
            Add(stars);
            KV(L.T("report.worked"), d.HoursWorked.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " h · " + Pct(d.Utilization));
            KV(L.T("report.wins") + " / " + L.T("report.losses") + " / " + L.T("report.settles"), d.Wins + " / " + d.Losses + " / " + d.Settles);
            if (d.WalkedOut > 0) KV(L.T("report.walked_out"), Fmt.Int(d.WalkedOut, L.Lang), "dd-row__v--bad");
            if (d.Late > 0) KV(L.T("report.late"), Fmt.Int(d.Late, L.Lang), "dd-row__v--bad");
            KV(L.T("report.billed"), Money(d.Billed));
            KV(L.T("report.fees"), Money(d.Fees));
            if (d.Boost > 0) KV(L.T("report.boost"), Money(d.Boost), "dd-row__v--good");
            KV(L.T("report.salaries"), "−" + Money(d.Salaries));
            KV(L.T("report.rent"), "−" + Money(d.Rent));
            if (d.Visiting > 0) KV(L.T("report.visiting"), "−" + Money(d.Visiting));
            if (d.Walls > 0) KV(L.T("report.walls"), "−" + Money(d.Walls));
            if (d.Sanctions > 0) KV(L.T("report.sanctions"), "−" + Money(d.Sanctions), "dd-row__v--bad");
            KV(L.T("report.net"), Money(d.Net), d.Net >= 0 ? "dd-row__v--good" : "dd-row__v--bad");
            if (!withActions || Sim.Phase != Phases.Report) return;
            if (!Sim.S.Billed)
            {
                Line(L.F("report.invoices", "cash", Money(Sim.S.Wip)), "dd-good");
                RewardCard("billing_boost");
                var bill = UiKit.PrimaryButton(L.T("hud.bill"), () => main.Run("bill"));
                bill.name = "Target_bill";
                sheet.Actions.Add(bill);
            }
            else
            {
                Line(L.F("report.billed_done", "cash", Money(d.Invoiced)), "dd-good");
                if (!main.Coach.Active)
                {
                    var next = UiKit.PrimaryButton(L.T("hud.next_day"), () => main.Run("next_day"));
                    next.name = "Target_next_day";
                    sheet.Actions.Add(next);
                }
            }
        }

        void FillOffline()
        {
            var o = main.LastOffline;
            Title(L.T("offline.title"));
            if (o == null) return;
            Line(L.F("offline.body", "cash", Money(o.Amount), "time", Fmt.Dur(o.Seconds, L)));
            if (o.Capped) Line(L.F("offline.capped", "h", (long)Math.Round(o.Hours)), "coddy-caption-muted");
            RewardCard("offline_x2");
            sheet.Actions.Add(UiKit.PrimaryButton(L.T("common.ok"), CloseSheet));
        }
    }
}
