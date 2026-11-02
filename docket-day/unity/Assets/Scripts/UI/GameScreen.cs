// Docket Day — the firm screen (UI Toolkit, CoddyKit theme). Layout (portrait):
//   top    HUD: day · weekday · phase, clock, operating cash, trust (client money, never spendable), Standing; speed + the
//          "skip ahead" clock; crisis chips; the main action (Start the day / Billing run / Next day)
//   middle the diorama + markers (reception with the waiting clients, the courthouse with today's hearings, a build badge)
//   bottom the DOCKET RAIL (the next 10 days — 14 with the Partner's Pass — as columns of task chips, colour per matter:
//          amber = due soon, red = late or a collision, a gavel = a court date), the people row (hours left, tired, sick)
//          and the tab bar: Matters · Team · Office · Shop · Settings — each opens a bottom sheet (Sheets.cs).
//   Assigning: drag a person card UP onto a chip, or tap the person then tap the chip (≥ 48 dp targets, accessibility).
// Text is set only when it changed (UiKit.SetText, LESSONS A10); the rail / people rows are rebuilt only when their
// content key changes and never under a finger (UiKit.Held, fleet §16); markers move at 30 Hz (Low 10 Hz).
using System;
using System.Collections.Generic;
using System.Text;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using DocketDay.Game;
using DocketDay.Sim;
using DocketDay.World;

namespace DocketDay.UI
{
    public sealed partial class GameScreen
    {
        readonly VisualElement root;
        readonly DocketMain main;
        Loc L => main.Loc;
        Simulation Sim => main.Session.Sim;
        GameActions Act => main.Actions;
        RegionWorld World => main.world;

        // HUD
        Label dayLabel, clockLabel, cashLabel, trustLabel, standingLabel, buildLabel;
        VisualElement crisesRow, speedRow, hud, buildChip;
        Button mainAction, ffBtn;
        readonly List<Button> speedButtons = new List<Button>();
        // markers
        VisualElement markerLayer;
        Button receptionBtn, courtBtn;
        // bottom
        VisualElement bottom, railHost, tabsRow;
        ScrollView railScroll, staffScroll;
        Label railHint;
        string railKey = "", staffKey = "";
        readonly Dictionary<string, Button> tabs = new Dictionary<string, Button>();
        readonly Dictionary<string, VisualElement> chips = new Dictionary<string, VisualElement>();
        readonly Dictionary<int, VisualElement> staffCards = new Dictionary<int, VisualElement>();
        // drag
        VisualElement ghost;
        int dragStaff = -1, dragPointer = -1;
        bool dragging;
        Vector2 dragStart;
        VisualElement dragSource;
        float markerAcc, hudAcc;
        // state
        string openTab = "";
        UiKit.Sheet sheet;
        string sheetKind = "";
        int sheetArg = -1;
        bool sheetDirty;
        public bool WindowOpen => (sheet != null && sheet.IsOpen) || PagerOpen || HearingOpen;

        static readonly int MatterColours = 8;

        public GameScreen(VisualElement root, DocketMain main)
        {
            this.root = root;
            this.main = main;
            var uss = Resources.Load<StyleSheet>("ui/DocketDay");
            if (uss != null) root.styleSheets.Add(uss);
            root.AddToClassList("dd-root");
            BuildMarkers();
            BuildHud();
            BuildBottom();
            BuildCoach();
            BuildHearing();
            ghost = new VisualElement { name = "DragGhost", pickingMode = PickingMode.Ignore };
            ghost.AddToClassList("dd-ghost");
            ghost.style.display = DisplayStyle.None;
            ghost.Add(UiKit.Text("", "dd-ghost__label"));
            root.Add(ghost);
            L.Changed += Relocalise;
            Refresh(true);
        }

        // ------------------------------------------------------------------ build

        void BuildHud()
        {
            hud = new VisualElement { name = "Hud" };
            hud.AddToClassList("dd-hud");
            hud.pickingMode = PickingMode.Ignore;
            var row = Row("dd-hud__row");
            var left = Row("dd-hud__row");
            dayLabel = ChipLabel(left, "", out _);
            clockLabel = ChipLabel(left, "", out _);
            row.Add(left);
            var right = Row("dd-hud__row");
            cashLabel = ChipLabel(right, "", out var cashChip);
            cashChip.AddToClassList("dd-chip--money");
            trustLabel = ChipLabel(right, "", out var trustChip);
            trustChip.AddToClassList("dd-chip--trust");
            trustChip.RegisterCallback<ClickEvent>(_ => Toast(L.T("hud.trust_info"), ToastKind.Info));
            standingLabel = ChipLabel(right, "", out var stChip);
            stChip.RegisterCallback<ClickEvent>(_ => { if (Sim.S.History.Count > 0) OpenSheet("report_last"); else Toast(L.T("hud.standing_info"), ToastKind.Info); });
            row.Add(right);
            hud.Add(row);

            var row2 = Row("dd-hud__row");
            crisesRow = Row("dd-crises");
            row2.Add(crisesRow);
            speedRow = Row("dd-speed");
            for (int s = 1; s <= 4; s++)
            {
                int sp = s;
                var b = new Button(() => OnSpeed(sp)) { text = L.F("hud.speed", "n", sp) };
                b.AddToClassList("dd-speed__btn");
                b.focusable = false;
                speedButtons.Add(b);
                speedRow.Add(b);
            }
            ffBtn = new Button(() => main.Run("ff")) { name = "Target_ff" };
            ffBtn.AddToClassList("dd-ff");
            ffBtn.focusable = false;
            speedRow.Add(ffBtn);
            row2.Add(speedRow);
            hud.Add(row2);

            buildChip = new VisualElement();
            buildChip.AddToClassList("dd-chip");
            buildLabel = UiKit.Text("", "dd-chip__label");
            buildChip.Add(buildLabel);
            buildChip.style.alignSelf = Align.Center;
            hud.Add(buildChip);

            mainAction = UiKit.PrimaryButton("", OnMainAction);
            mainAction.AddToClassList("dd-main-action");
            mainAction.name = "MainAction";
            hud.Add(mainAction);
            root.Add(hud);
        }

        void BuildMarkers()
        {
            markerLayer = new VisualElement { name = "Markers" };
            markerLayer.AddToClassList("dd-markers");
            markerLayer.pickingMode = PickingMode.Ignore;
            receptionBtn = new Button(() => main.Run("reception")) { name = "Target_reception" };
            receptionBtn.AddToClassList("dd-marker");
            receptionBtn.focusable = false;
            courtBtn = new Button(() => main.Run("courthouse")) { name = "Courthouse" };
            courtBtn.AddToClassList("dd-marker");
            courtBtn.AddToClassList("dd-marker--court");
            courtBtn.focusable = false;
            markerLayer.Add(receptionBtn);
            markerLayer.Add(courtBtn);
            root.Add(markerLayer);
        }

        void BuildBottom()
        {
            bottom = new VisualElement { name = "Bottom" };
            bottom.AddToClassList("dd-bottom");
            bottom.pickingMode = PickingMode.Ignore;

            railHost = new VisualElement { name = "Rail" };
            railHost.AddToClassList("dd-rail");
            var head = Row("dd-rail__head");
            head.Add(UiKit.Text(L.T("rail.title"), "dd-rail__title"));
            railHint = UiKit.Text("", "dd-rail__hint");
            head.Add(railHint);
            railHost.Add(head);
            railScroll = UiKit.List(ScrollViewMode.Horizontal);
            railScroll.AddToClassList("dd-rail__scroll");
            railHost.Add(railScroll);
            staffScroll = UiKit.List(ScrollViewMode.Horizontal);
            staffScroll.AddToClassList("dd-staff__scroll");
            railHost.Add(staffScroll);
            bottom.Add(railHost);

            tabsRow = Row("dd-tabs");
            foreach (var id in new[] { "matters", "team", "office", "shop", "settings" })
            {
                string tid = id;
                var b = new Button(() => OnTab(tid)) { name = "Tab_" + id };
                b.AddToClassList("dd-tab");
                b.focusable = false;
                tabs[id] = b;
                tabsRow.Add(b);
            }
            bottom.Add(tabsRow);
            root.Add(bottom);
        }

        // ------------------------------------------------------------------ the rail

        string ShortName(MatterState m) => L.T("area." + m.Area) + " #" + m.Id;
        string StaffName(int id)
        {
            if (id == Attend.Visiting) return L.T("lever.visiting");
            var s = Sim.StaffById(id);
            if (s == null) return "?";
            return Sim.IsFounder(s) ? L.T("staff.name.0") : L.T("staff.name." + s.Name);
        }

        string DayLabel(int day) => L.T("hud.weekday." + Sim.WeekdayOf(day)) + " " + day;

        string RailContentKey()
        {
            var sb = new StringBuilder();
            sb.Append(Sim.S.Day).Append('|').Append(Sim.RailDays).Append('|').Append(Act.SelectedStaff).Append('|').Append(L.Lang).Append('|');
            foreach (var c in Sim.Collisions()) sb.Append(c.Matter).Append('c');
            foreach (var m in Sim.S.Matters)
            {
                if (m.Status != MatterStatus.Open && m.Status != MatterStatus.Hearing) continue;
                sb.Append(m.Id).Append(m.Status[0]);
                foreach (var t in m.Tasks) sb.Append(t.Kind[0]).Append(t.Due).Append(t.Staff).Append(t.Finished ? 'f' : 'o').Append(t.Late ? 'l' : '-').Append((int)(t.Frac * 4));
            }
            return sb.ToString();
        }

        void SyncRail()
        {
            string key = RailContentKey();
            if (key == railKey || UiKit.Held(railScroll) || dragging) return;
            railKey = key;
            float scroll = railScroll.scrollOffset.x;
            railScroll.Clear();
            chips.Clear();
            var coll = Sim.Collisions();
            int days = Sim.RailDays;
            bool any = false;
            for (int d = 0; d < days; d++)
            {
                int day = Sim.S.Day + d;
                var col = new VisualElement();
                col.AddToClassList("dd-col");
                col.EnableInClassList("dd-col--today", d == 0);
                col.Add(UiKit.Text(d == 0 ? L.T("rail.today") : DayLabel(day), "dd-col__head"));
                foreach (var m in Sim.S.Matters)
                {
                    if (!m.Active) continue;
                    for (int i = 0; i < m.Tasks.Count; i++)
                    {
                        var t = m.Tasks[i];
                        if (t.Finished) continue;
                        int showDay = Math.Max(t.Due, Sim.S.Day);
                        if (showDay != day) continue;
                        col.Add(Chip(m, i, t, coll.Exists(c => c.Matter == m.Id) && t.Kind == Kinds.Hearing));
                        any = true;
                    }
                }
                railScroll.Add(col);
            }
            railScroll.scrollOffset = new Vector2(scroll, 0);
            UiKit.SetText(railHint, Act.SelectedStaff >= 0 ? L.F("rail.pick", "name", StaffName(Act.SelectedStaff)) : any ? L.T("rail.drag_hint") : L.T("rail.empty"));
        }

        VisualElement Chip(MatterState m, int index, TaskState t, bool collision)
        {
            int mid = m.Id, ti = index;
            var b = new Button(() => OnChip(mid, ti)) { name = "Target_task_" + m.Id + "_" + index };
            b.focusable = false;
            b.AddToClassList("dd-task");
            b.AddToClassList("dd-mc-" + (m.Id % MatterColours));
            bool hearing = t.Kind == Kinds.Hearing;
            b.EnableInClassList("dd-task--hearing", hearing);
            b.EnableInClassList("dd-task--late", t.Late || (!hearing && t.Due < Sim.S.Day));
            b.EnableInClassList("dd-task--soon", !hearing && !t.Late && t.Due <= Sim.S.Day + 1);
            b.EnableInClassList("dd-task--collision", collision);
            b.EnableInClassList("dd-task--session", m.Status == MatterStatus.Hearing);
            b.userData = new int[] { m.Id, index };
            b.Add(UiKit.Text(ShortName(m), "dd-task__title"));
            string who = t.Staff == Attend.None ? L.T("matter.nobody") : StaffName(t.Staff);
            string line = hearing ? "⚖ " + L.F("rail.court", "time", Fmt.ClockMin(Sim.Gd.HearingSlots[Math.Min(t.Slot, Sim.Gd.HearingSlots.Length - 1)])) : L.T("kind." + t.Kind) + " " + Fmt.Pct(t.Frac, L.Lang);
            b.Add(UiKit.Text(line, "dd-task__line"));
            b.Add(UiKit.Text(who, "dd-task__who"));
            chips[m.Id + ":" + index] = b;
            return b;
        }

        void OnChip(int matter, int task)
        {
            if (Act.SelectedStaff >= 0) main.Run("assign:" + Act.SelectedStaff + ":" + matter + ":" + task);
            else main.Run("matter:" + matter);
        }

        // ------------------------------------------------------------------ the people row (tap = select, drag up = assign)

        string StaffContentKey()
        {
            var sb = new StringBuilder();
            sb.Append(Act.SelectedStaff).Append(L.Lang).Append(main.Coach.Active ? main.Coach.SuggestedStaff(Sim) : -9);
            foreach (var s in Sim.S.Staff) sb.Append(s.Id).Append(s.Role[0]).Append((int)(s.HoursLeft * 2)).Append(s.Sick ? 's' : '-').Append(Sim.Tired(s) ? 't' : '-').Append(Sim.InSession(s.Id) ? 'c' : '-');
            return sb.ToString();
        }

        void SyncStaff()
        {
            string key = StaffContentKey();
            if (key == staffKey || UiKit.Held(staffScroll) || dragging) return;
            staffKey = key;
            staffScroll.Clear();
            staffCards.Clear();
            int suggest = main.Coach.Active ? main.Coach.SuggestedStaff(Sim) : -1;
            foreach (var s in Sim.S.Staff)
            {
                var r = Sim.RoleOf(s);
                if (r.Rate <= 0 && !r.Lawyer) continue;   // the receptionist works the desk, not the rail
                int id = s.Id;
                var card = new VisualElement { name = "Staff_" + id };
                card.AddToClassList("dd-person");
                card.EnableInClassList("dd-person--selected", Act.SelectedStaff == id);
                card.EnableInClassList("dd-person--suggest", suggest == id);
                card.EnableInClassList("dd-person--off", s.Sick);
                card.Add(UiKit.Text(StaffName(id), "dd-person__name"));
                string state = s.Sick ? L.T("staff.sick") : Sim.InSession(id) ? L.T("staff.in_court") : Sim.Tired(s) ? L.T("staff.tired") : L.T("role." + s.Role);
                card.Add(UiKit.Text(state, "dd-person__role"));
                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.AddToClassList("dd-person__bar");
                var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("dd-person__fill");
                fill.style.width = Length.Percent((float)(100.0 * s.HoursLeft / Math.Max(1, r.Hours)));
                bar.Add(fill);
                card.Add(bar);
                card.RegisterCallback<PointerDownEvent>(e => OnPersonDown(e, id, card));
                card.RegisterCallback<PointerMoveEvent>(OnPersonMove);
                card.RegisterCallback<PointerUpEvent>(e => OnPersonUp(e, id));
                card.RegisterCallback<PointerCancelEvent>(_ => EndDrag());
                staffScroll.Add(card);
                staffCards[id] = card;
            }
        }

        void OnPersonDown(PointerDownEvent e, int id, VisualElement card)
        {
            dragStaff = id;
            dragPointer = e.pointerId;
            dragStart = e.position;
            dragSource = card;
            dragging = false;
        }

        void OnPersonMove(PointerMoveEvent e)
        {
            if (dragStaff < 0 || e.pointerId != dragPointer) return;
            Vector2 d = (Vector2)e.position - dragStart;
            if (!dragging)
            {
                // an upward drag picks the person up; a sideways drag scrolls the row (the ScrollView handles it)
                if (d.y < -14f && Mathf.Abs(d.y) > Mathf.Abs(d.x))
                {
                    dragging = true;
                    dragSource.CapturePointer(dragPointer);
                    ghost.style.display = DisplayStyle.Flex;
                    UiKit.SetText(ghost.Q<Label>(), StaffName(dragStaff));
                }
                else return;
            }
            ghost.style.translate = new Translate(e.position.x, e.position.y);
            HighlightDrop(e.position);
        }

        void OnPersonUp(PointerUpEvent e, int id)
        {
            if (dragStaff != id || e.pointerId != dragPointer) { EndDrag(); return; }
            if (dragging)
            {
                var target = ChipAt(e.position);
                EndDrag();
                if (target != null && target.userData is int[] ref2) main.Run("assign:" + id + ":" + ref2[0] + ":" + ref2[1]);
                else if (main.Coach.Active) main.Coach.WrongTap();
                return;
            }
            EndDrag();
            if (((Vector2)e.position - dragStart).sqrMagnitude < 20f * 20f) main.Run("select_staff:" + id);
        }

        VisualElement hoverChip;

        void HighlightDrop(Vector2 pos)
        {
            var c = ChipAt(pos);
            if (c == hoverChip) return;
            hoverChip?.RemoveFromClassList("dd-task--drop");
            hoverChip = c;
            hoverChip?.AddToClassList("dd-task--drop");
        }

        VisualElement ChipAt(Vector2 panelPos)
        {
            if (root.panel == null) return null;
            ghost.style.display = DisplayStyle.None;
            var e = root.panel.Pick(panelPos);
            if (dragging) ghost.style.display = DisplayStyle.Flex;
            while (e != null && !(e.userData is int[])) e = e.parent;
            return e;
        }

        void EndDrag()
        {
            if (dragSource != null && dragPointer >= 0 && dragSource.HasPointerCapture(dragPointer)) dragSource.ReleasePointer(dragPointer);
            dragging = false;
            dragStaff = -1;
            dragPointer = -1;
            dragSource = null;
            ghost.style.display = DisplayStyle.None;
            hoverChip?.RemoveFromClassList("dd-task--drop");
            hoverChip = null;
        }

        // ------------------------------------------------------------------ helpers

        static VisualElement Row(string cls)
        {
            var r = new VisualElement();
            r.AddToClassList(cls);
            r.pickingMode = PickingMode.Ignore;
            return r;
        }

        static Label ChipLabel(VisualElement parent, string text, out VisualElement chip)
        {
            chip = new VisualElement();
            chip.AddToClassList("dd-chip");
            var l = UiKit.Text(text, "dd-chip__label");
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
            if (Sim.Phase == Phases.Plan) main.Run("start_day");
            else if (Sim.Phase == Phases.Report)
            {
                if (!Sim.S.Billed) main.Run("bill");
                else main.Run("next_day");
            }
        }

        void OnTab(string id)
        {
            if (main.Coach.Active) { main.Coach.WrongTap(); return; }
            if (openTab == id && sheet != null && sheet.IsOpen) { CloseSheet(); return; }
            OpenSheet(id);
        }

        /// <summary>A world tap (RegionWorld.Picked).</summary>
        public void WorldPick(WorldHit hit)
        {
            switch (hit.Kind)
            {
                case "reception": main.Run("reception"); break;
                case "courthouse": main.Run("courthouse"); break;
                case "office": if (main.Coach.Active) main.Coach.WrongTap(); else OpenSheet("office"); break;
                case "cat": main.Audio?.Play("meow"); break;
                default:
                    if (main.Coach.Active) main.Coach.WrongTap();
                    else if (Act.SelectedStaff >= 0) main.Run("select_staff:" + Act.SelectedStaff);
                    break;
            }
        }

        /// <summary>After an action ran (DocketMain): open the right sheet, toast, refresh.</summary>
        public void AfterAction(string id, ActionResult r)
        {
            var a = id.Split(':');
            if (r.Ok)
            {
                switch (a[0])
                {
                    case "reception": OpenSheet("intake"); break;
                    case "courthouse": OpenSheet("court"); break;
                    case "matter": OpenSheet("matter", (int)r.Value); break;
                    case "accept": case "wall":
                        if (int.TryParse(a[1], out int am)) { var m = Sim.MatterById(am); if (m != null && m.Trust > 0) Toast(L.F("toast.trust_in", "cash", Money(m.Trust)), ToastKind.Info); }
                        if (main.Coach.Active) CloseSheet();
                        break;
                    case "assign":
                        if (int.TryParse(a[1], out int sid)) Toast(L.F("toast.assigned", "name", StaffName(sid)), ToastKind.Success);
                        break;
                    case "settle": Toast(L.F("toast.settled", "cash", Money(r.Value)), ToastKind.Success); CloseSheet(); break;
                    case "bill": if (!main.Coach.Active) OpenSheet("report"); break;
                    case "start_day": case "next_day": case "ff": CloseSheet(); break;
                    case "move_up": CloseSheet(); break;
                }
                sheetDirty = true;
                railKey = ""; staffKey = "";
            }
            else if (r.Error != "blocked") Toast(L.T("err." + r.Error), ToastKind.Warning);
            Refresh(true);
        }

        public void Toast(string text, ToastKind kind)
        {
            if (!string.IsNullOrEmpty(text) && !text.StartsWith("err.")) UiToastHost.Show(text, kind);
        }

        // ------------------------------------------------------------------ frame

        public void Tick(float dt, bool low)
        {
            markerAcc += dt;
            if (markerAcc >= (low ? 0.1f : 1f / 30f)) { markerAcc = 0; PlaceMarkers(); }
            Refresh(false);
            CoachTick(dt);
            HearingTick(dt);
            if (sheetDirty && sheet != null && sheet.IsOpen && !UiKit.Held(sheet.Body)) { sheetDirty = false; FillSheet(); }
        }

        public void MarkSheetDirty() => sheetDirty = true;

        public void Refresh(bool force)
        {
            hudAcc += Time.unscaledDeltaTime;
            if (!force && hudAcc < 0.2f) return;   // 5 Hz HUD text
            hudAcc = 0;
            var s = Sim.S;
            UiKit.SetText(dayLabel, L.F("hud.day", "n", s.Day) + " · " + L.T("hud.weekday." + Sim.Weekday) + " · " + L.T("phase." + s.Phase));
            UiKit.SetText(clockLabel, Fmt.ClockMin(s.Minute));
            UiKit.SetText(cashLabel, Money(s.Cash));
            UiKit.SetText(trustLabel, L.F("hud.trust", "cash", Money(Sim.TrustTotal)));
            UiKit.SetText(standingLabel, L.F("hud.standing", "s", Sim.FirmStanding.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
            bool live = Sim.Phase == Phases.Live;
            for (int i = 0; i < speedButtons.Count; i++)
            {
                speedButtons[i].EnableInClassList("dd-speed__btn--on", s.Speed == i + 1 && !Act.FastForwarding);
                speedButtons[i].style.display = live ? DisplayStyle.Flex : DisplayStyle.None;
            }
            ffBtn.style.display = Sim.Phase != Phases.Report ? DisplayStyle.Flex : DisplayStyle.None;
            ffBtn.EnableInClassList("dd-ff--on", Act.FastForwarding);
            UiKit.SetText(ffBtn, L.T("hud.ff"));
            bool coachHidesMain = main.Coach.Active && !(main.Coach.Step == 7 && Sim.Phase == Phases.Report);
            mainAction.style.display = !live && !coachHidesMain ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(mainAction, Sim.Phase == Phases.Plan ? L.T("hud.start_day") : !s.Billed ? L.T("hud.bill") : L.T("hud.next_day"));
            mainAction.name = Sim.Phase == Phases.Report && !s.Billed ? "Target_bill" : "MainAction";
            string building = s.Building;
            buildChip.style.display = building != "" ? DisplayStyle.Flex : DisplayStyle.None;
            if (building != "") UiKit.SetText(buildLabel, L.F("hud.building", "name", L.T("room." + building), "time", Fmt.Dur(Sim.BuildLeftS, L)));
            // crisis chips
            var crises = Sim.ActiveCrises();
            string key = string.Join(",", crises);
            if ((string)crisesRow.userData != key)
            {
                crisesRow.userData = key;
                crisesRow.Clear();
                foreach (var c in crises)
                {
                    var chip = new VisualElement();
                    chip.AddToClassList("dd-chip");
                    chip.AddToClassList("dd-chip--warn");
                    chip.Add(UiKit.Text(L.T("crisis." + c + ".t"), "dd-chip__label"));
                    crisesRow.Add(chip);
                }
            }
            int waiting = Sim.IntakeCount;
            UiKit.SetText(receptionBtn, L.T("intake.title") + (waiting > 0 ? " · " + waiting : ""));
            receptionBtn.EnableInClassList("dd-marker--hot", waiting > 0);
            int hearings = 0;
            foreach (var m in Sim.S.Matters) if (m.Active && m.HearingTask != null && m.HearingTask.Due == s.Day) hearings++;
            UiKit.SetText(courtBtn, L.T("court.title") + (hearings > 0 ? " · " + hearings : ""));
            foreach (var kv in tabs)
            {
                UiKit.SetText(kv.Value, L.T("tab." + kv.Key));
                kv.Value.EnableInClassList("dd-tab--on", openTab == kv.Key && sheet != null && sheet.IsOpen);
            }
            SyncRail();
            SyncStaff();
        }

        void PlaceMarkers()
        {
            if (World == null) return;
            var rp = World.ScreenOf(RegionWorld.ReceptionPos + Vector3.up * 5f);
            if (rp != null) { var pp = ToPanel(rp.Value); receptionBtn.style.translate = new Translate(pp.x, pp.y); }
            var cp = World.ScreenOf(RegionWorld.CourtDoor + Vector3.up * 17f);
            if (cp != null) { var pp = ToPanel(cp.Value); courtBtn.style.translate = new Translate(pp.x, pp.y); }
        }

        void Relocalise()
        {
            for (int s = 0; s < speedButtons.Count; s++) speedButtons[s].text = L.F("hud.speed", "n", s + 1);
            crisesRow.userData = null;
            railKey = ""; staffKey = "";
            Refresh(true);
            sheetDirty = true;
        }

        /// <summary>Scrolls the rail so a chip is visible (the guide's target).</summary>
        void ShowChip(string key)
        {
            if (chips.TryGetValue(key, out var c) && c.panel != null) railScroll.ScrollTo(c);
        }

        /// <summary>The top-left screen rect of a UI target for the guide spotlight (null = not a UI target / hidden).</summary>
        Rect? UiTarget(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;
            if (target.StartsWith("task:")) ShowChip(target.Substring(5));
            string name = "Target_" + target.Replace(':', '_');
            // a pill inside the open sheet wins (the HUD underneath is behind the sheet's scrim)
            VisualElement e = (sheet != null && sheet.IsOpen ? sheet.Panel.Q<VisualElement>(name) : null) ?? root.Q<VisualElement>(name);
            if (e == null || e.panel == null || e.resolvedStyle.display == DisplayStyle.None) return null;
            return e.worldBound;
        }
    }
}
