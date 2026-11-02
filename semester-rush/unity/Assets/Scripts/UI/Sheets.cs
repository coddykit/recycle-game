// Semester Rush — bottom sheets (CoddyKit BottomSheet: content-sized, finger scroll with inertia, no visible scrollbar,
// one action pill per card, fleet §16 / §17). Every sheet is rebuilt from the sim when something changed, never under a
// finger. Guide targets inside a sheet are named "Target_<target>" so the spotlight can find them.
//   board     the timetable of one room: room pills, a thumb-sized 5 × 5 grid (Mon–Fri × slots), the course picker for a
//             chosen cell, the picked class (professor, warning, remove), the courses list (professors), Auto-fill,
//             the lunch-crunch fix card
//   campus    zones (unlock), plots (build), gates, wider paths, the buildings
//   staff     professors per faculty, the five departments (hire / let go), staggered bells
//   research  four branches, one project at a time, ≤ 20 min
//   late      the last bell change: late students, the busiest paths, extra marshals (rewarded), hungry students
//   crisis / decision   today's crises + "Call the city for help" (rewarded) / a choice with its cost
//   shop · settings · report (week grades, semester close + intake) · offline (welcome back ×2) · building · site · gate
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using SemesterRush.Game;
using SemesterRush.Sim;

namespace SemesterRush.UI
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
            sheet = UiKit.BottomSheet("", "", null, kind == "board" ? 0.84f : 0.78f, L.T("sheet.close"));
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
            var last = Sim.S.History.Count > 0 ? Sim.S.History[Sim.S.History.Count - 1] : null;
            switch (sheetKind)
            {
                case "board": FillBoard(sheetArg >= 0 ? sheetArg : Act.BoardHall); break;
                case "campus": FillCampus(); break;
                case "staff": FillStaff(); break;
                case "research": FillResearch(); break;
                case "late": FillLate(); break;
                case "crisis": FillCrisis(); break;
                case "decision": FillDecision(); break;
                case "shop": FillShop(); break;
                case "settings": FillSettings(); break;
                case "report": FillReport(last, true); break;
                case "report_last": FillReport(last, false); break;
                case "offline": FillOffline(); break;
                case "building": FillBuilding(sheetArg); break;
                case "site": FillSite(sheetArg); break;
                case "gate": FillGate(sheetArg); break;
            }
        }

        void Title(string title, string sub = "")
        {
            UiKit.SetText(sheet.Header.Title, title);
            sheet.Header.SetSub(sub);
        }

        void Add(VisualElement e) => sheet.Body.Add(e);
        void Section(string text, string trailing = "") => Add(UiKit.SectionHeader(text, trailing));
        void Body(string text, string cls = "coddy-body") => Add(UiKit.Text(text, cls));

        UiKit.ListCard Card(string title, string sub, string actionText, Action onAction, bool enabled = true, string kind = "primary", string target = null, string badge = "")
        {
            var c = UiKit.Card(title, sub, null, !enabled && actionText == null, false, badge);
            if (actionText != null)
            {
                var pill = UiKit.ActionPill(actionText, onAction, enabled, kind);
                if (target != null) pill.name = "Target_" + Safe(target);
                c.SetSide(pill);
            }
            Add(c.Root);
            return c;
        }

        VisualElement KV(string k, string v, string cls = "")
        {
            var r = new VisualElement();
            r.AddToClassList("sr-row");
            r.Add(UiKit.Text(k, "sr-row__k"));
            var vl = UiKit.Text(v, "sr-row__v");
            if (cls != "") vl.AddToClassList(cls);
            r.Add(vl);
            Add(r);
            return r;
        }

        VisualElement Pills()
        {
            var p = new VisualElement();
            p.AddToClassList("sr-pills");
            Add(p);
            return p;
        }

        string BuyLabel(BuyInfo b) => b.Ok || b.Reason == "cash" ? Money(b.Cost) : L.T("err." + b.Reason);
        void Buy(string item) => main.Run("buy:" + item);

        void RewardCard(string placement)
        {
            var o = main.Rewards?.Get(placement);
            if (o == null || !o.Show) return;
            string label = Simulation.RewardIsCash(placement)
                ? L.F(o.Video ? "reward.video_cash" : "reward.free_cash", "cash", Money(o.Amount))
                : L.T(o.Video ? "reward.video" : "reward.claim");
            Card(L.T("reward." + placement + ".t"), o.Video ? L.T("reward." + placement + ".b") : L.T("reward.free.b"), label, () => main.ClaimReward(placement), true, "tonal");
        }

        // ------------------------------------------------------------------ board (the timetable)

        void FillBoard(int hall)
        {
            if (Act.BoardHall >= 0) hall = Act.BoardHall;
            if (hall < 0 || Sim.BuildingAt(hall) == null) hall = Sim.MainHall();
            if (hall < 0) { Title(L.T("tab.board")); return; }
            var def = Sim.DefOf(Sim.BuildingAt(hall));
            Title(Place(hall), L.T("building." + def.Id) + " · " + L.F("board.seats", "n", def.Seats) + (Sim.ExamWeek ? " · " + L.T("board.exams") : ""));
            // room pills
            var rooms = Pills();
            foreach (var b in Sim.S.Buildings)
            {
                if (!b.Built || !Sim.DefOf(b).Teaching) continue;
                int n = b.Node;
                rooms.Add(UiKit.ActionPill(Place(n), () => main.Run("hall:" + n), true, n == hall ? "primary" : "tonal"));
            }
            // the grid: header row + one row per slot
            var grid = new VisualElement();
            grid.AddToClassList("sr-board");
            var head = new VisualElement();
            head.AddToClassList("sr-board__row");
            head.Add(UiKit.Text("", "sr-board__time"));
            for (int d = 0; d < Sim.Gd.DaysPerWeek; d++) head.Add(UiKit.Text(L.T("day.short." + d), "sr-board__day"));
            grid.Add(head);
            for (int slot = 0; slot < Sim.Gd.SlotCount; slot++)
            {
                var row = new VisualElement();
                row.AddToClassList("sr-board__row");
                var time = UiKit.Text(Fmt.ClockMin(Sim.Gd.Slots[slot]), "sr-board__time");
                if (Sim.Gd.IsLunchSlot(slot)) time.AddToClassList("sr-board__time--lunch");
                row.Add(time);
                for (int d = 0; d < Sim.Gd.DaysPerWeek; d++)
                {
                    int dd = d, ss = slot;
                    int i = Sim.At(d, slot, hall);
                    var cell = new Button(() => main.Run("cell:" + dd + ":" + ss)) { name = "Target_cell_" + d + "_" + slot };
                    cell.AddToClassList("sr-cell");
                    cell.focusable = false;
                    if (i >= 0)
                    {
                        var p = Sim.ActivePlan[i];
                        cell.text = CourseCode(p.Course);
                        cell.AddToClassList("sr-cell--fac" + Sim.FacultyIndexOfCourse(p.Course));
                        if (Sim.Warning(p) != "") cell.AddToClassList("sr-cell--warn");
                        if (Act.SelectedPlacement == i) cell.AddToClassList("sr-cell--selected");
                    }
                    else if (Act.SelectedDay == d && Act.SelectedSlot == slot) cell.AddToClassList("sr-cell--selected");
                    if (Sim.Past(d, slot)) cell.AddToClassList("sr-cell--past");
                    if (Sim.Phase == Phases.Live && d == Sim.S.Day && (Sim.ClassSlotAt(Sim.S.Minute) == slot || Sim.BellSlotAt(Sim.S.Minute) == slot)) cell.AddToClassList("sr-cell--now");
                    row.Add(cell);
                }
                grid.Add(row);
            }
            Add(grid);
            Body(Sim.Missing > 0 ? L.F("board.missing", "n", Sim.Missing) : L.T("board.complete"), Sim.Missing > 0 ? "sr-warn-text" : "coddy-caption-muted");

            // the guide's / a helpful lunch fix
            var fix = Sim.FindLunchFix();
            if (fix != null && (!main.Coach.Active || main.Coach.Step == 10))
            {
                var fp = Sim.ActivePlan[fix.Placement];
                Card(L.F("board.lunch_t", "day", L.T("day." + fix.Day)), L.F("board.lunch_b", "course", CourseName(fp.Course), "time", Fmt.ClockMin(Sim.Gd.Slots[fix.Slot])),
                    L.F("board.move_to", "time", Fmt.ClockMin(Sim.Gd.Slots[fix.Slot])), () => main.Run("move:" + fix.Placement + ":" + fix.Day + ":" + fix.Slot + ":" + fix.Node),
                    true, "primary", "move:" + fix.Placement);
            }

            if (Act.SelectedPlacement >= 0 && Act.SelectedPlacement < Sim.ActivePlan.Count) SelectedClass(Act.SelectedPlacement);
            else if (Act.CellChosen) CoursePicker(hall);

            if (!Sim.ExamWeek)
            {
                Section(L.T("board.courses"));
                for (int c = 0; c < Sim.Gd.Courses.Count; c++)
                {
                    if (!Sim.CourseOn(c)) continue;
                    CourseCard(c);
                }
            }
            sheet.Actions.Add(UiKit.ActionPill(L.T(Sim.SmartFill ? "board.auto_fill_smart" : "board.auto_fill"), () => main.Run("auto_fill"), Sim.Missing > 0, "tonal"));
        }

        void CoursePicker(int hall)
        {
            Section(L.F("board.pick", "day", L.T("day." + Act.SelectedDay), "time", Fmt.ClockMin(Sim.Gd.Slots[Act.SelectedSlot])));
            int shown = 0;
            for (int c = 0; c < Sim.Gd.Courses.Count; c++)
            {
                if (!Sim.CourseOn(c) || Sim.Count(c) >= Sim.Needed(c)) continue;
                int cc = c;
                string why = Sim.CanPlace(c, Act.SelectedDay, Act.SelectedSlot, hall);
                var cohort = Sim.S.Cohorts[Sim.Gd.Courses[c].CohortIndex];
                Card(CourseName(c), L.T("cohort." + Sim.Gd.Cohorts[Sim.Gd.Courses[c].CohortIndex].Id) + " · " + L.F("board.students", "n", cohort.Size) + " · " + L.F("board.count", "n", Sim.Count(c), "m", Sim.Needed(c)),
                    why == "" ? L.T("board.place") : L.T("err." + why), () => main.Run("course:" + cc), why == "", "primary", "course:" + c);
                shown++;
            }
            if (shown == 0) Body(L.T("board.nothing_to_place"), "coddy-caption-muted");
        }

        void SelectedClass(int i)
        {
            var p = Sim.ActivePlan[i];
            string warn = Sim.Warning(p);
            Section(L.T("board.selected"));
            var cohort = Sim.S.Cohorts[Sim.Gd.Courses[p.Course].CohortIndex];
            Card(CourseName(p.Course), L.T("day." + p.Day) + " " + Fmt.ClockMin(Sim.Gd.Slots[p.Slot]) + " · " + L.F("board.students", "n", cohort.Size)
                + (Sim.ExamWeek ? "" : " · " + ProfName(Sim.S.CourseProf[p.Course])) + (warn != "" ? " · " + L.T("warn." + warn) : ""),
                L.T("board.remove"), () => main.Run("remove:" + i), !Sim.Past(p.Day, p.Slot), "tonal");
            Body(L.T("board.move_hint"), "coddy-caption-muted");
        }

        void CourseCard(int c)
        {
            int prof = Sim.S.CourseProf[c];
            int fac = Sim.FacultyIndexOfCourse(c);
            var card = Card(CourseName(c), L.T("faculty." + Sim.Gd.Faculties[fac].Id) + " · " + L.F("board.count", "n", Sim.Count(c), "m", Sim.Needed(c)) + " · " + ProfName(prof), null, null);
            var pills = new VisualElement();
            pills.AddToClassList("sr-pills");
            int pi = 0;
            for (int p = 0; p < Sim.S.Profs.Count; p++)
            {
                if (Sim.S.Profs[p].Faculty != fac) continue;
                int pp = p;
                var b = UiKit.ActionPill(ProfName(p), () => main.Run("prof:" + c + ":" + pp), true, prof == p ? "primary" : "tonal");
                b.name = "Target_prof_" + c + "_" + pi;
                pills.Add(b);
                pi++;
            }
            if (pi == 0) pills.Add(UiKit.ActionPill(L.T("staff.hire_prof"), () => OpenSheet("staff"), true, "tonal"));
            card.Root.Add(pills);
        }

        // ------------------------------------------------------------------ campus

        void FillCampus()
        {
            Title(L.T("tab.campus"), L.F("hud.prestige", "p", (long)Math.Round(Sim.S.Prestige)) + " · " + L.F("campus.students", "n", Sim.TotalStudents));
            foreach (var z in Sim.Gd.Zones)
            {
                if (Sim.Unlocked(z.Id)) continue;
                var b = Sim.CanBuy("zone:" + z.Id);
                if (b.Reason == "locked") continue;
                Card(L.T("zone." + z.Id), L.F("zone.needs", "p", (long)z.Prestige) + " · " + L.T("zone." + z.Id + ".d"), L.F("zone.unlock", "cost", Money(z.Cost)), () => Buy("zone:" + z.Id), b.Ok);
                break;
            }
            Section(L.T("campus.build"));
            foreach (var n in Sim.Gd.Nodes)
            {
                if (n.Kind != "site" || !Sim.Unlocked(n.Zone)) continue;
                var bs = Sim.BuildingAt(n.Index);
                if (bs != null && bs.Built) continue;
                if (bs != null) { Card(Place(n.Index), L.T("building." + n.Building) + " · " + L.F("marker.building", "pct", Fmt.Pct(1 - bs.BuildLeft / Math.Max(1, Sim.DefOf(bs).BuildMin), L.Lang)), null, null); continue; }
                var b = Sim.CanBuy("build:" + n.Id);
                string id = n.Id;
                Card(Place(n.Index), L.T("building." + n.Building) + " — " + L.T("building." + n.Building + ".d"), BuyLabel(b), () => Buy("build:" + id), b.Ok);
            }
            Section(L.T("campus.paths"));
            foreach (var e in Sim.Gd.Edges)
            {
                if (e.Type != "gate") continue;
                var b = Sim.CanBuy("gate:" + e.Index);
                if (b.Reason == "locked" || b.Reason == "owned") continue;
                int ei = e.Index;
                Card(L.F("campus.gate", "a", NodeLabel(e.A), "b", NodeLabel(e.B)), L.T("campus.gate.d"), BuyLabel(b), () => main.Run("gate:" + ei), b.Ok);
            }
            if (Sim.Has("wide_paths"))
            {
                int shown = 0;
                foreach (var e in Sim.Gd.Edges)
                {
                    var b = Sim.CanBuy("widen:" + e.Index);
                    if (!b.Ok && b.Reason != "cash") continue;
                    int ei = e.Index;
                    double heat = ei < Sim.PeakHeat.Length ? Sim.PeakHeat[ei] : 0;
                    if (heat < 0.7) continue;   // only the paths that got busy this week
                    Card(L.F("campus.widen", "a", NodeLabel(e.A), "b", NodeLabel(e.B)), L.F("campus.widen.busy", "pct", Fmt.Pct(heat, L.Lang)), BuyLabel(b), () => Buy("widen:" + ei), b.Ok);
                    shown++;
                }
                if (shown == 0) Body(L.T("campus.widen.none"), "coddy-caption-muted");
            }
            Section(L.T("campus.buildings"));
            foreach (var b in Sim.S.Buildings)
            {
                if (!b.Built) continue;
                int n = b.Node;
                Card(Place(n), L.T("building." + b.Def), L.T("common.open"), () => main.Run(Sim.DefOf(b).Teaching ? "hall:" + n : "building:" + n), true, "tonal");
            }
        }

        string NodeLabel(int node) => Sim.Gd.Nodes[node].Kind == "junction" ? L.T("place.junction") : Place(node);

        void FillBuilding(int node)
        {
            var b = Sim.BuildingAt(node);
            if (b == null) { CloseSheet(); return; }
            var def = Sim.DefOf(b);
            Title(Place(node), L.T("building." + def.Id));
            Body(L.T("building." + def.Id + ".d"));
            if (!b.Built) { KV(L.T("building.progress"), Fmt.Pct(1 - b.BuildLeft / Math.Max(1, def.BuildMin), L.Lang)); return; }
            switch (def.Kind)
            {
                case "canteen":
                    KV(L.T("building.meals"), Fmt.Int((long)Math.Round(Sim.Meals), L.Lang));
                    for (int d = 0; d < Sim.Gd.DaysPerWeek; d++)
                        KV(L.T("day." + d) + " " + Fmt.ClockMin(Sim.Gd.Slots[Sim.Gd.LunchSlots[0]]), Fmt.Int((long)Sim.LunchDemand(d, Sim.Gd.LunchSlots[0]), L.Lang), Sim.LunchDemand(d, Sim.Gd.LunchSlots[0]) > Sim.Meals ? "sr-row__v--bad" : "");
                    break;
                case "dorm":
                    KV(L.T("building.beds"), Fmt.Int(Sim.Beds, L.Lang));
                    KV(L.T("building.residents"), Fmt.Pct(Sim.ResidentShare, L.Lang));
                    break;
                case "library":
                    Body(L.T("building.library.effect"), "coddy-caption-muted");
                    break;
                case "admin":
                    KV(L.T("building.till"), Money(Math.Floor(Sim.S.Till)));
                    Card(L.T("building.collect"), "", L.T("common.ok"), () => main.Run("till"), Sim.S.Till >= 1);
                    break;
            }
            KV(L.T("building.upkeep"), "−" + Money(def.Upkeep));
        }

        void FillSite(int node)
        {
            var n = Sim.Gd.Nodes[node];
            var def = Sim.Gd.Building(n.Building);
            Title(Place(node), L.T("building." + def.Id));
            Body(L.T("building." + def.Id + ".d"));
            if (def.Seats > 0) KV(L.T("building.seats"), Fmt.Int(def.Seats, L.Lang));
            if (def.Beds > 0) KV(L.T("building.beds"), Fmt.Int(def.Beds, L.Lang));
            if (def.Meals > 0) KV(L.T("building.meals"), Fmt.Int(def.Meals, L.Lang));
            KV(L.T("building.time"), L.F("building.days", "n", Math.Round(def.BuildMin / (Sim.Gd.DayEnd - Sim.Gd.DayStart), 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            KV(L.T("building.upkeep"), "−" + Money(def.Upkeep));
            Body(L.T("building.crews"), "coddy-caption-muted");
            var b = Sim.CanBuy("build:" + n.Id);
            if (b.Reason == "owned") return;
            var pill = UiKit.PrimaryButton(L.F("building.build", "cost", Money(b.Cost)), () => Buy("build:" + n.Id));
            pill.name = "Target_build_" + n.Id;
            pill.SetEnabled(b.Ok);
            sheet.Actions.Add(pill);
        }

        void FillGate(int edge)
        {
            if (edge < 0 || edge >= Sim.Gd.Edges.Count) { CloseSheet(); return; }
            var e = Sim.Gd.Edges[edge];
            Title(L.F("campus.gate", "a", NodeLabel(e.A), "b", NodeLabel(e.B)));
            Body(L.T("campus.gate.d"));
            var b = Sim.CanBuy("gate:" + edge);
            if (b.Reason == "owned") { Body(L.T("campus.gate_open"), "coddy-caption-muted"); return; }
            var pill = UiKit.PrimaryButton(L.F("marker.gate", "cost", Money(b.Cost)), () => main.Run("gate:" + edge));
            pill.SetEnabled(b.Ok);
            sheet.Actions.Add(pill);
        }

        // ------------------------------------------------------------------ staff

        void FillStaff()
        {
            Title(L.T("tab.staff"), L.F("staff.payroll", "cash", Money(Payroll())));
            Section(L.T("staff.professors"));
            foreach (var f in Sim.Gd.Faculties)
            {
                if (!Sim.Unlocked(f.Zone)) continue;
                var names = new List<string>();
                foreach (var p in Sim.S.Profs) if (p.Faculty == f.Index) names.Add(L.T("prof." + p.Name) + (p.Leaving ? " (" + L.T("staff.leaving") + ")" : p.OffDay == Sim.AbsDay ? " (" + L.T("staff.sick") + ")" : ""));
                var b = Sim.CanBuy("professor:" + f.Id);
                string fid = f.Id;
                Card(L.T("faculty." + f.Id), names.Count > 0 ? string.Join(", ", names) : L.T("staff.no_prof"), b.Reason == "max" ? L.T("err.max") : L.F("staff.hire", "cost", Money(b.Cost)),
                    () => Buy("professor:" + fid), b.Ok, names.Count == 0 ? "primary" : "tonal");
            }
            Body(L.F("staff.prof_rule", "n", Sim.Gd.ProfMaxPerDay, "wage", Money(Sim.Gd.ProfWage)), "coddy-caption-muted");
            Section(L.T("staff.departments"));
            foreach (var d in Depts.All)
            {
                var b = Sim.CanBuy("staff:" + d);
                string dd = d;
                var card = Card(L.T("staff." + d) + " · " + Sim.Staff(d), L.T("staff." + d + ".d"), b.Reason == "max" ? L.T("err.max") : L.F("staff.hire", "cost", Money(b.Cost)), () => Buy("staff:" + dd), b.Ok, "tonal");
                if (Sim.Staff(d) > 0)
                {
                    var row = new VisualElement();
                    row.AddToClassList("sr-pills");
                    row.Add(UiKit.ActionPill(L.T("staff.dismiss"), () => main.Run("dismiss:" + dd), true, "tonal"));
                    card.Root.Add(row);
                }
            }
            if (Sim.Has("staggered_bells"))
            {
                Section(L.T("staff.policies"));
                Card(L.T("research.staggered_bells.t"), L.F("staff.staggered.d", "cash", Money(Sim.Gd.StaggeredCostDay)), L.T(Sim.S.Staggered ? "settings.on" : "settings.off"),
                    () => main.Run("staggered:" + (Sim.S.Staggered ? 0 : 1)), true, Sim.S.Staggered ? "primary" : "tonal");
            }
        }

        double Payroll()
        {
            double w = Sim.S.Profs.Count * Sim.Gd.ProfWage;
            foreach (var d in Depts.All) w += Sim.Staff(d) * Sim.Gd.Staff[d].Wage;
            return w;
        }

        // ------------------------------------------------------------------ research

        void FillResearch()
        {
            string running = Sim.S.Researching;
            Title(L.T("tab.research"), running != "" ? L.T("research." + running + ".t") + " · " + L.F("research.left", "time", Fmt.Dur(Sim.S.ResearchLeftS, L)) : "");
            foreach (var branch in new[] { "learning", "life", "mobility", "admin" })
            {
                Section(L.T("research.branch." + branch));
                foreach (var r in Sim.Gd.Research)
                {
                    if (r.Branch != branch) continue;
                    string why = Sim.CanResearch(r.Id);
                    string id = r.Id;
                    string action = why == "owned" ? L.T("research.done") : why == "running" ? L.F("research.left", "time", Fmt.Dur(Sim.S.ResearchLeftS, L))
                        : why == "requires" ? L.F("research.requires", "name", L.T("research." + r.Requires.Find(q => !Sim.Has(q)) + ".t"))
                        : why == "locked" ? L.F("research.zone", "name", L.T("zone." + r.Zone)) : L.F("research.start", "cost", Money(r.Cost));
                    Card(L.T("research." + id + ".t"), L.T("research." + id + ".d") + " · " + Fmt.Dur(r.Minutes * 60, L), action, () => main.Run("research:" + id), why == "", "primary");
                }
            }
        }

        // ------------------------------------------------------------------ bell change / crises

        void FillLate()
        {
            Title(L.T("late.title"), L.F("late.today", "n", Sim.S.LateToday, "m", Sim.S.TripsToday));
            var b = Sim.Bell;
            if (b != null && b.Students > 0)
            {
                KV(L.T("late.walking"), Fmt.Int(b.Students, L.Lang));
                KV(L.T("late.late"), Fmt.Int(b.Late, L.Lang), b.Late > 0 ? "sr-row__v--bad" : "sr-row__v--good");
                Section(L.T("late.busiest"));
                var busy = new List<int>();
                for (int e = 0; e < b.Heat.Length; e++) if (b.Heat[e] > 0.7) busy.Add(e);
                busy.Sort((x, y) => b.Heat[y].CompareTo(b.Heat[x]));
                for (int k = 0; k < busy.Count && k < 3; k++)
                {
                    var e = Sim.Gd.Edges[busy[k]];
                    KV(NodeLabel(e.A) + " – " + NodeLabel(e.B), Fmt.Pct(b.Heat[busy[k]], L.Lang), b.Heat[busy[k]] > 1 ? "sr-row__v--bad" : "");
                }
                if (busy.Count == 0) Body(L.T("late.flowing"), "coddy-caption-muted");
            }
            else Body(L.T("late.none_now"), "coddy-caption-muted");
            RewardCard("extra_marshals");
            Section(L.T("late.how"));
            Body(L.T("late.tips"), "coddy-caption-muted");
            int hungry = 0;
            foreach (var c in Sim.S.Cohorts) if (c.Enrolled && c.Lunch >= 0) hungry += (int)Math.Round(c.Size * c.Unfed);
            if (hungry > 0) { Section(L.T("late.lunch")); Body(L.F("late.hungry", "n", hungry, "m", (long)Math.Round(Sim.Meals))); }
        }

        void FillCrisis()
        {
            Title(L.T("crisis.title"));
            var list = Sim.ActiveCrises();
            if (list.Count == 0) Body(L.T("crisis.none"), "coddy-caption-muted");
            foreach (var c in list) Card(L.T("crisis." + c + ".t"), L.T("crisis." + c + ".b"), null, null);
            RewardCard("city_help");
        }

        void FillDecision()
        {
            var d = Sim.S.Pending;
            if (d == null) { CloseSheet(); return; }
            string who = d.Target >= 0 && d.Target < Sim.S.Profs.Count ? ProfName(d.Target) : "";
            Title(L.T("crisis." + d.Id + ".t"));
            Body(L.F("decision." + d.Id + ".b", "name", who));
            foreach (var o in Simulation.Options(d.Id))
            {
                double cost = Sim.OptionCost(o);
                string opt = o;
                Card(L.T("decision." + o), L.T("decision." + o + ".d"), cost > 0 ? Money(cost) : L.T("common.ok"), () => main.Run("decide:" + opt), true, "primary");
            }
            Body(L.T("decision.paused"), "coddy-caption-muted");
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
            sheet.Actions.Add(UiKit.ActionPill(L.T("shop.restore"), () => shop.Restore(), true, "tonal"));
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
                langs.Add(UiKit.ActionPill(Loc.NativeNames[i], () => main.SetLanguage(code), true, L.Lang == code ? "primary" : "tonal"));
            }
            Section(L.T("settings.sound"));
            Card(L.T("settings.music"), "", L.T(st.Music ? "settings.on" : "settings.off"), () => main.SetAudio(!st.Music, st.Effects), true, st.Music ? "primary" : "tonal");
            Card(L.T("settings.effects"), "", L.T(st.Effects ? "settings.on" : "settings.off"), () => main.SetAudio(st.Music, !st.Effects), true, st.Effects ? "primary" : "tonal");
            Section(L.T("settings.graphics"));
            var tiers = Pills();
            foreach (var t in new[] { Tier.Low, Tier.Medium, Tier.High })
            {
                var tt = t;
                tiers.Add(UiKit.ActionPill(L.T("graphics." + t.ToString().ToLowerInvariant()), () => main.SetTier(tt), true, main.CurrentTier == t ? "primary" : "tonal"));
            }
            var shop = main.Shop;
            if (shop != null)
            {
                var owned = new List<string>();
                foreach (var t in new[] { "gold_trim", "theme_ivy", "theme_modern", "theme_desert" }) if (shop.Has(t)) owned.Add(t);
                if (owned.Count > 0)
                {
                    Section(L.T("settings.theme"));
                    var themes = Pills();
                    themes.Add(UiKit.ActionPill(L.T("theme.default"), () => { main.SetTheme(""); FillSheet(); }, true, st.Theme == "" ? "primary" : "tonal"));
                    foreach (var t in owned) { string tt = t; themes.Add(UiKit.ActionPill(L.T("theme." + t), () => { main.SetTheme(tt); FillSheet(); }, true, st.Theme == t ? "primary" : "tonal")); }
                }
                if (shop.Has("automation"))
                    Card(L.T("settings.auto_repair"), L.T("settings.auto_repair.d"), L.T(st.AutoRepair ? "settings.on" : "settings.off"), () => { main.SetAutoRepair(!st.AutoRepair); FillSheet(); }, true, st.AutoRepair ? "primary" : "tonal");
                if (shop.Has("speed"))
                    Card(L.T("settings.auto_skip"), L.T("settings.auto_skip.d"), L.T(st.AutoSkipReport ? "settings.on" : "settings.off"), () => { main.SetAutoSkip(!st.AutoSkipReport); FillSheet(); }, true, st.AutoSkipReport ? "primary" : "tonal");
            }
            Section(L.T("settings.help"));
            Card(L.T("settings.guide"), "", L.T("intro.start"), main.ReplayGuide, true, "tonal");
            Section(L.T("settings.privacy"));
            Card(L.T("settings.privacy_options"), "", L.T("common.open"), main.ShowPrivacyOptions, true, "tonal");
            Card(L.T("settings.policy"), "", L.T("common.open"), () => Application.OpenURL(SemesterRush.App.SemesterGameConfig.PrivacyUrl), true, "tonal");
            Card(L.T("settings.restore"), "", L.T("common.ok"), () => main.Shop?.Restore(), true, "tonal");
        }

        // ------------------------------------------------------------------ report / offline

        void FillReport(WeekStats w, bool withNext)
        {
            if (w == null) { Title(L.T("phase.report")); Body(L.T("report.none"), "coddy-caption-muted"); return; }
            Title(w.Exam ? L.F("report.exam_title", "n", (w.Week - 1) / Sim.Gd.WeeksPerSemester + 1) : L.F("report.title", "n", (w.Week - 1) % Sim.Gd.WeeksPerSemester + 1),
                L.F("report.prestige", "p", (long)Math.Round(Sim.S.Prestige), "d", (w.PrestigeDelta >= 0 ? "+" : "−") + Math.Abs(Math.Round(w.PrestigeDelta))));
            var big = new VisualElement();
            big.AddToClassList("sr-report__big");
            big.Add(UiKit.Text(Fmt.Pct(w.Grade, L.Lang), "sr-report__grade"));
            big.Add(UiKit.Text(L.T(w.Grade >= 0.8 ? "report.grade_great" : w.Grade >= 0.65 ? "report.grade_good" : "report.grade_low"), "coddy-caption-muted"));
            Add(big);
            Section(L.T("report.cohorts"));
            for (int c = 0; c < w.Cohort.Length && c < Sim.Gd.Cohorts.Count; c++)
                if (w.Cohort[c] >= 0) KV(L.T("cohort." + Sim.Gd.Cohorts[c].Id), Fmt.Pct(w.Cohort[c], L.Lang), w.Cohort[c] < 0.6 ? "sr-row__v--bad" : "");
            Section(L.T("report.week"));
            KV(L.T("report.held"), w.Held + " / " + w.Required, w.Held < w.Required ? "sr-row__v--bad" : "");
            KV(L.T("report.late"), Fmt.Int(w.Late, L.Lang) + " (" + Fmt.Pct(w.LateRate, L.Lang) + ")", w.LateRate > 0.05 ? "sr-row__v--bad" : "sr-row__v--good");
            if (w.Unfed > 0) KV(L.T("report.hungry"), Fmt.Int(w.Unfed, L.Lang), "sr-row__v--bad");
            Section(L.T("report.money"));
            KV(L.T("report.tuition"), Money(w.Tuition));
            if (w.Rent > 0) KV(L.T("report.rent"), Money(w.Rent));
            if (w.ExamFees > 0) KV(L.T("report.exam_fees"), Money(w.ExamFees));
            if (w.Grants > 0) KV(L.T("report.grants"), Money(w.Grants));
            if (w.EventIncome > 0) KV(L.T("report.events"), Money(w.EventIncome));
            KV(L.T("report.wages"), "−" + Money(w.Wages));
            KV(L.T("report.upkeep"), "−" + Money(w.Upkeep));
            if (w.Overtime > 0) KV(L.T("report.overtime"), "−" + Money(w.Overtime));
            if (w.Concessions > 0) KV(L.T("report.concessions"), "−" + Money(w.Concessions));
            KV(L.T("report.net"), Money(w.Net), w.Net >= 0 ? "sr-row__v--good" : "sr-row__v--bad");
            if (w.Exam)
            {
                Section(L.T("report.semester"));
                KV(L.T("report.semester_grade"), Fmt.Pct(w.SemesterGrade, L.Lang));
                Section(L.T("intake.title"));
                Body(L.T("intake.help"), "coddy-caption-muted");
                foreach (var i in new[] { Intakes.Keep, Intakes.Grow, Intakes.Select })
                {
                    string ii = i;
                    Card(L.T("intake." + i), L.T("intake." + i + ".d"), Sim.S.Intake == i ? L.T("intake.chosen") : L.T("intake.choose"), () => main.Run("intake:" + ii), Sim.Phase == Phases.Report, Sim.S.Intake == i ? "primary" : "tonal");
                }
            }
            if (withNext && Sim.Phase == Phases.Report && !main.Coach.Active)
            {
                if (!w.Exam) RewardCard("daily_bonus");
                var next = UiKit.PrimaryButton(L.T("hud.next_week"), () => main.Run("next_week"));
                next.name = "Target_next_week";
                sheet.Actions.Add(next);
            }
        }

        void FillOffline()
        {
            var o = main.LastOffline;
            Title(L.T("offline.title"));
            if (o == null) return;
            Body(L.F("offline.body", "cash", Money(o.Amount), "time", Fmt.Dur(o.Seconds, L)));
            if (o.Capped) Body(L.F("offline.capped", "h", (long)Math.Round(o.Hours)), "coddy-caption-muted");
            RewardCard("offline_x2");
            sheet.Actions.Add(UiKit.PrimaryButton(L.T("common.ok"), CloseSheet));
        }
    }
}
