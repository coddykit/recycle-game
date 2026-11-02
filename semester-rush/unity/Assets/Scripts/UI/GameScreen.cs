// Semester Rush — the Campus screen (UI Toolkit, CoddyKit theme). Layout (portrait):
//   top    HUD: week · day, clock, cash, prestige; the late counter of the last bell change, hungry students, today's
//          crisis chips; speed buttons; the main action (Start the week on Sunday, Open the grades on Friday evening)
//   middle the diorama + markers (≥ 48 dp tap targets): every room shows the class in it now, plots show "+ build",
//          closed gates "open · $", the admin building the bursar's coins
//   bottom the tab bar: Timetable · Campus · Staff · Upgrades · Shop · ⚙ — each opens a bottom sheet (Sheets.cs)
// Text is set only when it changed (UiKit.SetText), lists are rebuilt on events, never under a finger (UiKit.Held,
// fleet §16); markers move at 30 Hz (Low 10 Hz) with style.translate (no layout pass).
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using SemesterRush.Game;
using SemesterRush.Sim;
using SemesterRush.World;

namespace SemesterRush.UI
{
    public sealed partial class GameScreen
    {
        readonly VisualElement root;
        readonly SemesterMain main;
        Loc L => main.Loc;
        Simulation Sim => main.Session.Sim;
        GameActions Act => main.Actions;
        CampusWorld World => main.world;

        // HUD
        Label dayLabel, clockLabel, cashLabel, prestigeLabel, lateLabel, hungryLabel;
        VisualElement crisesRow, speedRow, hud, lateChip, hungryChip;
        Button mainAction;
        readonly List<Button> speedButtons = new List<Button>();
        float lateFlash;
        // bottom
        VisualElement bottom, tabsRow;
        readonly Dictionary<string, Button> tabs = new Dictionary<string, Button>();
        // markers
        VisualElement markerLayer;
        readonly List<Marker> markers = new List<Marker>();
        string markerKey = "";
        float markerAcc;
        // state
        string openTab = "";
        UiKit.Sheet sheet;
        string sheetKind = "";
        int sheetArg = -1;
        bool sheetDirty;
        public bool WindowOpen => (sheet != null && sheet.IsOpen) || PagerOpen;
        public bool DecisionOpen => sheet != null && sheet.IsOpen && sheetKind == "decision";

        sealed class Marker
        {
            public Button Root;
            public Label Label;
            public string Kind;     // hall | building | site | gate | coins
            public int Index;
            public string Last = "";
            public Vector3 World;
        }

        public GameScreen(VisualElement root, SemesterMain main)
        {
            this.root = root;
            this.main = main;
            var uss = Resources.Load<StyleSheet>("ui/SemesterRush");
            if (uss != null) root.styleSheets.Add(uss);
            root.AddToClassList("sr-root");
            markerLayer = new VisualElement { name = "Markers", pickingMode = PickingMode.Ignore };
            markerLayer.AddToClassList("sr-markers");
            root.Add(markerLayer);
            BuildHud();
            BuildBottom();
            BuildCoach();
            L.Changed += Relocalise;
            Refresh(true);
        }

        // ------------------------------------------------------------------ build

        void BuildHud()
        {
            hud = new VisualElement { name = "Hud", pickingMode = PickingMode.Ignore };
            hud.AddToClassList("sr-hud");
            var row = Row("sr-hud__row");
            var left = Row("sr-hud__row");
            dayLabel = ChipLabel(left, out _);
            clockLabel = ChipLabel(left, out _);
            row.Add(left);
            var right = Row("sr-hud__row");
            cashLabel = ChipLabel(right, out var cashChip);
            cashChip.AddToClassList("sr-chip--money");
            prestigeLabel = ChipLabel(right, out var prestigeChip);
            prestigeChip.AddToClassList("sr-chip--prestige");
            prestigeChip.RegisterCallback<ClickEvent>(_ => OpenSheet("report_last"));
            row.Add(right);
            hud.Add(row);

            var row2 = Row("sr-hud__row");
            var chips = Row("sr-crises");
            lateLabel = ChipLabel(chips, out lateChip);
            lateChip.name = "LateChip";
            lateChip.AddToClassList("sr-chip--late");
            lateChip.RegisterCallback<ClickEvent>(_ => OpenSheet("late"));
            hungryLabel = ChipLabel(chips, out hungryChip);
            hungryChip.AddToClassList("sr-chip--warn");
            hungryChip.RegisterCallback<ClickEvent>(_ => OpenSheet("late"));
            crisesRow = Row("sr-crises");
            chips.Add(crisesRow);
            row2.Add(chips);
            speedRow = Row("sr-speed");
            for (int s = 1; s <= 4; s++)
            {
                int sp = s;
                var b = new Button(() => OnSpeed(sp)) { text = L.F("hud.speed", "n", sp) };
                b.AddToClassList("sr-speed__btn");
                b.focusable = false;
                speedButtons.Add(b);
                speedRow.Add(b);
            }
            row2.Add(speedRow);
            hud.Add(row2);

            mainAction = UiKit.PrimaryButton("", OnMainAction);
            mainAction.AddToClassList("sr-main-action");
            mainAction.name = "MainAction";
            hud.Add(mainAction);
            root.Add(hud);
        }

        void BuildBottom()
        {
            bottom = new VisualElement { name = "Bottom", pickingMode = PickingMode.Ignore };
            bottom.AddToClassList("sr-bottom");
            tabsRow = Row("sr-tabs");
            foreach (var id in new[] { "board", "campus", "staff", "research", "shop", "settings" })
            {
                string tid = id;
                var b = new Button(() => OnTab(tid)) { name = "Tab_" + id };
                b.AddToClassList("sr-tab");
                b.focusable = false;
                tabs[id] = b;
                tabsRow.Add(b);
            }
            bottom.Add(tabsRow);
            root.Add(bottom);
        }

        /// <summary>Markers for every unlocked room / plot / closed gate + the bursar's coins (rebuilt when the campus changes).</summary>
        void SyncMarkers()
        {
            var gd = Sim.Gd;
            var sb = new System.Text.StringBuilder();
            foreach (var n in gd.Nodes)
            {
                if (!Sim.Unlocked(n.Zone) || (n.Kind != "building" && n.Kind != "site")) continue;
                var b = Sim.BuildingAt(n.Index);
                sb.Append(n.Index).Append(b == null ? 's' : b.Built ? 'b' : 'c').Append(',');
            }
            foreach (var e in gd.Edges) if (e.Type == "gate" && Sim.CanBuy("gate:" + e.Index).Reason != "locked" && !Sim.S.OpenGates.Contains(e.Index)) sb.Append('g').Append(e.Index);
            string key = sb.ToString();
            if (key == markerKey) return;
            markerKey = key;
            foreach (var m in markers) m.Root.RemoveFromHierarchy();
            markers.Clear();
            foreach (var n in gd.Nodes)
            {
                if (!Sim.Unlocked(n.Zone) || (n.Kind != "building" && n.Kind != "site")) continue;
                var b = Sim.BuildingAt(n.Index);
                string kind = b == null ? "site" : b.Built && Sim.DefOf(b).Teaching ? "hall" : "building";
                AddMarker(kind, n.Index, World != null ? World.NodePos(n.Index) + new Vector3(0, 22f, 9f) : Vector3.zero);
                if (b != null && b.Def == "admin") AddMarker("coins", n.Index, World != null ? World.NodePos(n.Index) + new Vector3(0, 34f, 9f) : Vector3.zero);
            }
            foreach (var e in gd.Edges)
                if (e.Type == "gate" && Sim.CanBuy("gate:" + e.Index).Reason != "locked" && !Sim.S.OpenGates.Contains(e.Index))
                    AddMarker("gate", e.Index, World != null ? World.EdgeMid(e.Index) + Vector3.up * 8f : Vector3.zero);
        }

        void AddMarker(string kind, int index, Vector3 world)
        {
            string action = kind == "coins" ? "till" : kind + ":" + index;
            var m = new Marker { Kind = kind, Index = index, World = world, Root = new Button(() => main.Run(action)) { name = MarkerName(kind, index) } };
            m.Root.AddToClassList("sr-marker");
            m.Root.AddToClassList("sr-marker--" + kind);
            m.Root.focusable = false;
            m.Label = UiKit.Text("", "sr-marker__label");
            m.Root.Add(m.Label);
            markerLayer.Add(m.Root);
            markers.Add(m);
        }

        static string MarkerName(string kind, int index) => kind == "coins" ? "Coins" : kind == "site" ? "Site_" + index : kind == "gate" ? "Gate_" + index : "Node_" + index;

        // ------------------------------------------------------------------ helpers

        static VisualElement Row(string cls)
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.AddToClassList(cls);
            return r;
        }

        static Label ChipLabel(VisualElement parent, out VisualElement chip)
        {
            chip = new VisualElement();
            chip.AddToClassList("sr-chip");
            var l = UiKit.Text("", "sr-chip__label");
            chip.Add(l);
            parent.Add(chip);
            return l;
        }

        Vector2 ToPanel(Vector2 screenTopLeft)
        {
            var p = root.panel;
            return p != null ? RuntimePanelUtils.ScreenToPanel(p, screenTopLeft) : screenTopLeft;
        }

        string Money(double v) => Fmt.Money((long)Math.Round(v), L.Lang);
        string Place(int node) => L.T("place." + Sim.Gd.Nodes[node].Id);
        string CourseName(int c) => L.T("course." + Sim.Gd.Courses[c].Id);
        string CourseCode(int c) => L.T("course." + Sim.Gd.Courses[c].Id + ".code");
        string ProfName(int p) => p >= 0 && p < Sim.S.Profs.Count ? L.T("prof." + Sim.S.Profs[p].Name) : L.T("board.no_prof");

        /// <summary>A UI point (top-left screen px) lies on a pickable UI element (camera pan / world taps must not start there).</summary>
        public bool OverUi(Vector2 screenTopLeft)
        {
            var p = root.panel;
            if (p == null) return false;
            var picked = p.Pick(ToPanel(screenTopLeft));
            return picked != null && picked != root && picked.pickingMode == PickingMode.Position && !(picked == markerLayer || picked == hud || picked == bottom);
        }

        // ------------------------------------------------------------------ input

        void OnSpeed(int s)
        {
            if (main.Coach.Active) { Toast(L.T("hud.speed_guide"), ToastKind.Info); return; }
            if (s > Sim.MaxSpeed) { Toast(L.T("hud.speed_locked"), ToastKind.Info); OpenSheet("shop"); return; }
            main.Run("speed:" + s);
            Refresh(true);
        }

        void OnMainAction()
        {
            if (Sim.Phase == Phases.Plan) main.Run("start_week");
            else if (Sim.Phase == Phases.Report) { main.Run("report"); OpenSheet("report"); }
        }

        void OnTab(string id)
        {
            if (main.Coach.Active) { main.Coach.WrongTap(); return; }
            if (openTab == id && sheet != null && sheet.IsOpen) { CloseSheet(); return; }
            if (id == "board") { int h = Act.BoardHall >= 0 ? Act.BoardHall : Sim.MainHall(); main.Run("hall:" + h); return; }
            OpenSheet(id);
        }

        /// <summary>A world tap (CampusWorld.Picked).</summary>
        public void WorldPick(WorldHit hit)
        {
            switch (hit.Kind)
            {
                case "hall": main.Run("hall:" + hit.Index); break;
                case "building": main.Run("building:" + hit.Index); break;
                case "site": main.Run("site:" + hit.Index); break;
                case "gate": OpenSheet("gate", hit.Index); break;
                default: if (main.Coach.Active) main.Coach.WrongTap(); break;
            }
        }

        /// <summary>After an action ran (SemesterMain): open the right sheet, refresh.</summary>
        public void AfterAction(string id, ActionResult r)
        {
            var a = id.Split(':');
            if (r.Ok)
            {
                switch (a[0])
                {
                    case "hall": OpenSheet("board", int.Parse(a[1])); break;
                    case "building": OpenSheet("building", int.Parse(a[1])); break;
                    case "site": OpenSheet("site", int.Parse(a[1])); break;
                    case "till": if (r.Value > 0) Toast(L.F("toast.till", "cash", Money(r.Value)), ToastKind.Success); break;
                    case "gate": Toast(L.T("toast.gate"), ToastKind.Success); if (sheetKind == "gate") CloseSheet(); break;
                    case "buy":
                        if (a.Length > 1 && a[1] == "build") { Toast(L.T("toast.build_start"), ToastKind.Success); if (sheetKind == "site") CloseSheet(); }
                        break;
                    case "start_week":
                    case "next_week":
                        CloseSheet();
                        break;
                    case "decide":
                        CloseSheet();
                        break;
                }
                sheetDirty = true;
            }
            else if (r.Error != "blocked") Toast(L.T("err." + r.Error), ToastKind.Warning);
            Refresh(true);
        }

        public void Toast(string text, ToastKind kind)
        {
            if (!string.IsNullOrEmpty(text) && !text.StartsWith("err.")) UiToastHost.Show(text, kind);
        }

        /// <summary>A bell change made students late: the late chip pulses.</summary>
        public void LateFlash() => lateFlash = 1.2f;

        // ------------------------------------------------------------------ frame

        public void Tick(float dt, bool low)
        {
            markerAcc += dt;
            if (markerAcc >= (low ? 0.1f : 1f / 30f)) { markerAcc = 0; PlaceMarkers(); }
            hudAcc += dt;
            if (hudAcc >= 0.2f) { hudAcc = 0; Refresh(false); }   // 5 Hz HUD text
            lateFlash = Mathf.Max(0, lateFlash - dt);
            float s = 1f + lateFlash * 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 10f));
            lateChip.style.scale = new Scale(new Vector3(s, s, 1));
            CoachTick(dt);
            if (sheetDirty && sheet != null && sheet.IsOpen && !UiKit.Held(sheet.Body)) { sheetDirty = false; FillSheet(); }
        }

        float hudAcc;

        public void Refresh(bool force)
        {
            var s = Sim.S;
            string day = Sim.Phase == Phases.Plan ? L.T("day.sunday") : Sim.Phase == Phases.Report ? L.T("phase.report") : L.T("day." + s.Day);
            UiKit.SetText(dayLabel, (Sim.ExamWeek ? L.F("hud.exam_week", "n", Sim.WeekOfSemester) : L.F("hud.week", "n", Sim.WeekOfSemester, "s", Sim.Semester)) + " · " + day);
            UiKit.SetText(clockLabel, Sim.Phase == Phases.Live ? Fmt.ClockMin(s.Minute) : "—");
            UiKit.SetText(cashLabel, Money(s.Cash));
            UiKit.SetText(prestigeLabel, L.F("hud.prestige", "p", (long)Math.Round(s.Prestige)));
            int late = Sim.Bell != null && Sim.Phase == Phases.Live ? Sim.Bell.Late : 0;
            UiKit.SetText(lateLabel, L.F("hud.late", "n", late));
            lateChip.EnableInClassList("sr-chip--warn", late > 0);
            int hungry = 0;
            foreach (var c in s.Cohorts) if (c.Enrolled && c.Lunch >= 0) hungry += (int)Math.Round(c.Size * c.Unfed);
            hungryChip.style.display = hungry > 0 && Sim.Phase == Phases.Live ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(hungryLabel, L.F("hud.hungry", "n", hungry));
            for (int i = 0; i < speedButtons.Count; i++)
            {
                speedButtons[i].EnableInClassList("sr-speed__btn--on", s.Speed == i + 1);
                speedButtons[i].style.display = Sim.Phase == Phases.Live ? DisplayStyle.Flex : DisplayStyle.None;
            }
            bool showMain = Sim.Phase != Phases.Live && !(main.Coach.Active && main.Coach.Step < 5);
            mainAction.style.display = showMain ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(mainAction, Sim.Phase == Phases.Plan ? (Sim.ExamWeek ? L.T("hud.start_exams") : L.T("hud.start_week")) : L.T("hud.report"));
            mainAction.name = Sim.Phase == Phases.Plan ? "MainAction" : "ReportAction";
            var crises = Sim.ActiveCrises();
            string key = string.Join(",", crises);
            if ((string)crisesRow.userData != key)
            {
                crisesRow.userData = key;
                crisesRow.Clear();
                foreach (var c in crises)
                {
                    var chip = new VisualElement();
                    chip.AddToClassList("sr-chip");
                    chip.AddToClassList("sr-chip--warn");
                    chip.Add(UiKit.Text(L.T("crisis." + c + ".t"), "sr-chip__label"));
                    chip.RegisterCallback<ClickEvent>(_ => OpenSheet(Sim.S.Pending != null ? "decision" : "crisis"));
                    crisesRow.Add(chip);
                }
            }
            foreach (var kv in tabs)
            {
                UiKit.SetText(kv.Value, kv.Key == "settings" ? "⚙" : L.T("tab." + kv.Key) + (kv.Key == "board" && Sim.Missing > 0 ? " · " + Sim.Missing : ""));
                kv.Value.EnableInClassList("sr-tab--on", openTab == kv.Key && sheet != null && sheet.IsOpen);
            }
            SyncMarkers();
            if (force) PlaceMarkers();
        }

        string MarkerText(Marker m)
        {
            switch (m.Kind)
            {
                case "coins": { double t = Math.Floor(Sim.S.Till); return t >= 1 ? "+" + Money(t) : ""; }
                case "site": return "+ " + L.T("building." + Sim.Gd.Nodes[m.Index].Building);
                case "gate": return L.F("marker.gate", "cost", Money(Sim.CanBuy("gate:" + m.Index).Cost));
                case "building":
                {
                    var b = Sim.BuildingAt(m.Index);
                    if (b != null && !b.Built) return L.F("marker.building", "pct", Fmt.Pct(1 - b.BuildLeft / Math.Max(1, Sim.DefOf(b).BuildMin), L.Lang));
                    return b != null ? L.T("building." + b.Def) : "";
                }
                case "hall":
                {
                    var b = Sim.BuildingAt(m.Index);
                    if (b.ClosedUntil > Sim.S.Minute) return "⚠ " + L.T("marker.closed");
                    if (Sim.Phase == Phases.Live)
                    {
                        int slot = Sim.ClassSlotAt(Sim.S.Minute);
                        if (slot < 0) slot = Sim.BellSlotAt(Sim.S.Minute);
                        int i = slot >= 0 ? Sim.At(Sim.S.Day, slot, m.Index) : -1;
                        if (i >= 0) return CourseCode(Sim.ActivePlan[i].Course);
                    }
                    return Place(m.Index);
                }
            }
            return "";
        }

        void PlaceMarkers()
        {
            if (World == null) return;
            foreach (var m in markers)
            {
                string text = MarkerText(m);
                if (text != m.Last) { m.Last = text; UiKit.SetText(m.Label, text); }
                bool visible = text != "" && !(main.Coach.Active && m.Kind != "hall" && m.Kind != "coins" && m.Kind != "gate" && m.Kind != "site");
                var sp = visible ? World.ScreenOf(m.World) : null;
                if (sp == null) { m.Root.style.display = DisplayStyle.None; continue; }
                m.Root.style.display = DisplayStyle.Flex;
                var pp = ToPanel(sp.Value);
                m.Root.style.translate = new Translate(pp.x, pp.y);
                if (m.Kind == "hall") m.Root.EnableInClassList("sr-marker--selected", Act.BoardHall == m.Index && SheetKind == "board");
            }
        }

        void Relocalise()
        {
            foreach (var m in markers) m.Last = "";
            for (int s = 0; s < speedButtons.Count; s++) speedButtons[s].text = L.F("hud.speed", "n", s + 1);
            crisesRow.userData = null;
            Refresh(true);
            sheetDirty = true;
        }

        /// <summary>The top-left screen rect of a UI target for the guide spotlight (null = not a UI target / hidden).</summary>
        Rect? UiTarget(string target)
        {
            VisualElement e = null;
            if (sheet != null && sheet.IsOpen) e = sheet.Panel.Q<VisualElement>("Target_" + Safe(target));
            if (e == null)
            {
                string[] a = target.Split(':');
                switch (a[0])
                {
                    case "start_week": e = mainAction; break;
                    case "report": e = mainAction; break;
                    case "late": e = lateChip; break;
                    case "coins": e = markers.Find(m => m.Kind == "coins")?.Root; break;
                    case "hall": e = root.Q<VisualElement>("Node_" + a[1]); break;
                    case "gate": e = root.Q<VisualElement>("Gate_" + a[1]); break;
                    case "build": { int n = Sim.Gd.Node(a[1]); e = n >= 0 ? root.Q<VisualElement>("Site_" + n) : null; break; }
                }
            }
            if (e == null || e.panel == null || e.resolvedStyle.display == DisplayStyle.None) return null;
            return e.worldBound;
        }

        static string Safe(string target) => target.Replace(':', '_');
    }
}
