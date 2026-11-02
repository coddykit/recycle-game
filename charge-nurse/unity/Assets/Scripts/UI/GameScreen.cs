// Charge Nurse — the hospital screen (UI Toolkit, CoddyKit theme). Layout (portrait):
//   top    HUD: day + phase, clock, cash, reputation; the lens pill (the bottleneck in a few words — tap = the red line +
//          one sentence), crisis chips, speed buttons; the main action (Start shift on the morning board, Hand over in the
//          handover window, Report); the ambulance call card (Accept / Divert, countdown)
//   middle the cut-away campus + markers: a status badge over every bay / ward bed (tag colour of the patient, ✓ ready to
//          go, ⇢ waiting for a ward bed, broom = dirty), a chip over every waiting patient (tag colour + shape, "?" untriaged)
//   bottom the tab bar: Board · Staff · Build · Upgrades · Shop · ⚙ — each opens a bottom sheet (Sheets.cs)
// Text is set only when it changed (UiKit.SetText, LESSONS A10); lists are rebuilt on events, never under a finger
// (UiKit.Held, fleet §16); markers move at 30 Hz (Low 10 Hz) with style.translate (no layout pass).
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using ChargeNurse.Game;
using ChargeNurse.Sim;
using ChargeNurse.World;

namespace ChargeNurse.UI
{
    public sealed partial class GameScreen
    {
        /// <summary>Colour-blind safe severity shapes (plan §8): ● green, ▲ yellow, ◆ orange, ■ red.</summary>
        public static readonly string[] TagShape = { "●", "▲", "◆", "■" };
        static readonly Color[] TagColor = { new Color(0.30f, 0.72f, 0.38f), new Color(0.95f, 0.78f, 0.27f), new Color(0.94f, 0.54f, 0.24f), new Color(0.88f, 0.33f, 0.25f) };

        readonly VisualElement root;
        readonly NurseMain main;
        Loc L => main.Loc;
        Simulation Sim => main.Session.Sim;
        GameActions Act => main.Actions;
        HospitalWorld World => main.world;

        // HUD
        Label dayLabel, clockLabel, cashLabel, repLabel, lensLabel, ambText;
        VisualElement crisesRow, speedRow, hud, ambCard;
        Button mainAction, lensBtn, ambAccept, ambDivert;
        readonly List<Button> speedButtons = new List<Button>();
        // bottom
        VisualElement bottom, tabsRow;
        readonly Dictionary<string, Button> tabs = new Dictionary<string, Button>();
        // markers
        VisualElement markerLayer;
        readonly List<Marker> bedMarkers = new List<Marker>();
        readonly List<Marker> waitMarkers = new List<Marker>();
        float markerAcc;
        // state
        string openTab = "";
        UiKit.Sheet sheet;
        string sheetKind = "";
        int sheetArg = -1;
        bool sheetDirty;
        public bool WindowOpen => (sheet != null && sheet.IsOpen) || PagerOpen;

        sealed class Marker
        {
            public Button Root;
            public Label Badge;
            public string LastText = "";
            public string LastClass = "";
            public int Id = -1;
        }

        public GameScreen(VisualElement root, NurseMain main)
        {
            this.root = root;
            this.main = main;
            var uss = Resources.Load<StyleSheet>("ui/ChargeNurse");
            if (uss != null) root.styleSheets.Add(uss);
            root.AddToClassList("cn-root");
            BuildMarkers();
            BuildHud();
            BuildBottom();
            BuildCoach();
            L.Changed += Relocalise;
            Refresh(true);
        }

        // ------------------------------------------------------------------ build

        void BuildHud()
        {
            hud = new VisualElement { name = "Hud" };
            hud.AddToClassList("cn-hud");
            hud.pickingMode = PickingMode.Ignore;
            var row = Row("cn-hud__row");
            var left = Row("cn-hud__row");
            dayLabel = ChipLabel(left, "", out _);
            clockLabel = ChipLabel(left, "", out _);
            row.Add(left);
            var right = Row("cn-hud__row");
            cashLabel = ChipLabel(right, "", out var cashChip);
            cashChip.AddToClassList("cn-chip--money");
            repLabel = ChipLabel(right, "", out var repChip);
            repChip.RegisterCallback<ClickEvent>(_ => OpenSheet("report_last"));
            row.Add(right);
            hud.Add(row);

            var row2 = Row("cn-hud__row");
            lensBtn = new Button(() => main.Run("lens")) { name = "Lens" };
            lensBtn.AddToClassList("cn-lens");
            lensBtn.focusable = false;
            lensLabel = UiKit.Text("", "cn-lens__label");
            lensBtn.Add(lensLabel);
            row2.Add(lensBtn);
            crisesRow = Row("cn-crises");
            row2.Add(crisesRow);
            speedRow = Row("cn-speed");
            for (int s = 1; s <= 4; s++)
            {
                int sp = s;
                var b = new Button(() => OnSpeed(sp)) { text = L.F("hud.speed", "n", sp) };
                b.AddToClassList("cn-speed__btn");
                b.focusable = false;
                speedButtons.Add(b);
                speedRow.Add(b);
            }
            row2.Add(speedRow);
            hud.Add(row2);

            mainAction = UiKit.PrimaryButton("", OnMainAction);
            mainAction.AddToClassList("cn-main-action");
            mainAction.name = "MainAction";
            hud.Add(mainAction);

            ambCard = new VisualElement { name = "Ambulance" };
            ambCard.AddToClassList("cn-amb");
            ambText = UiKit.Text("", "cn-amb__text");
            ambCard.Add(ambText);
            var ambRow = Row("cn-hud__row");
            ambRow.pickingMode = PickingMode.Position;
            ambDivert = UiKit.ActionPill("", () => main.Run("ambulance:divert"), true, "tonal");
            ambAccept = UiKit.ActionPill("", () => main.Run("ambulance:accept"));
            ambAccept.name = "AmbAccept";
            ambRow.Add(ambDivert);
            ambRow.Add(ambAccept);
            ambCard.Add(ambRow);
            hud.Add(ambCard);
            root.Add(hud);
        }

        void BuildBottom()
        {
            bottom = new VisualElement { name = "Bottom" };
            bottom.AddToClassList("cn-bottom");
            bottom.pickingMode = PickingMode.Ignore;
            tabsRow = Row("cn-tabs");
            foreach (var id in new[] { "board", "staff", "build", "research", "shop", "settings" })
            {
                string tid = id;
                var b = new Button(() => OnTab(tid)) { name = "Tab_" + id };
                b.AddToClassList("cn-tab");
                b.focusable = false;
                tabs[id] = b;
                tabsRow.Add(b);
            }
            bottom.Add(tabsRow);
            root.Add(bottom);
        }

        void BuildMarkers()
        {
            markerLayer = new VisualElement { name = "Markers" };
            markerLayer.AddToClassList("cn-markers");
            markerLayer.pickingMode = PickingMode.Ignore;
            root.Add(markerLayer);
        }

        Marker NewMarker(string cls, Action click, string name)
        {
            var m = new Marker { Root = new Button(click) { name = name } };
            m.Root.AddToClassList(cls);
            m.Root.focusable = false;
            m.Badge = UiKit.Text("", cls + "__badge");
            m.Badge.pickingMode = PickingMode.Ignore;
            m.Root.Add(m.Badge);
            markerLayer.Add(m.Root);
            return m;
        }

        void SyncMarkers()
        {
            while (bedMarkers.Count < Sim.S.Beds.Count)
            {
                int i = bedMarkers.Count;
                bedMarkers.Add(NewMarker("cn-bed", () => BedTap(i), "Bed_" + i));
            }
            while (bedMarkers.Count > Sim.S.Beds.Count) { bedMarkers[bedMarkers.Count - 1].Root.RemoveFromHierarchy(); bedMarkers.RemoveAt(bedMarkers.Count - 1); }
            while (waitMarkers.Count < 10)
            {
                var m = NewMarker("cn-wait", null, "Wait_" + waitMarkers.Count);
                var mm = m;
                m.Root.clicked += () => { if (mm.Id >= 0) main.Run("select_patient:" + mm.Id); };
                waitMarkers.Add(m);
            }
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
            chip.AddToClassList("cn-chip");
            var l = UiKit.Text(text, "cn-chip__label");
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
        public string TagText(int tag) => tag < 0 ? "?" : TagShape[tag];

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
            Refresh(false);
        }

        void OnMainAction()
        {
            if (Sim.Phase == Phases.Plan) main.Run("start_day");
            else if (Sim.HandoverWindow) main.Run("handover");
            else if (Sim.Phase == Phases.Report) OpenSheet("report");
        }

        void OnTab(string id)
        {
            if (main.Coach.Active) { main.Coach.WrongTap(); return; }
            if (openTab == id && sheet != null && sheet.IsOpen) { CloseSheet(); return; }
            OpenSheet(id);
        }

        /// <summary>A bay / bed badge: send the selected patient there, clean it, sign the discharge, or open its card.</summary>
        void BedTap(int b)
        {
            if (b < 0 || b >= Sim.S.Beds.Count) return;
            var bed = Sim.S.Beds[b];
            var sel = Act.SelectedPatient >= 0 ? Sim.P(Act.SelectedPatient) : null;
            if (bed.Bay && sel != null && sel.Stage == Stage.Bay && bed.State == BedState.Clean && bed.Patient < 0) { main.Run("assign:" + b); return; }
            if (bed.State == BedState.Dirty || bed.State == BedState.Cleaning) { main.Run("clean:" + b); return; }
            var p = bed.Patient >= 0 ? Sim.P(bed.Patient) : null;
            if (p != null && (p.Stage == Stage.Ready || p.Stage == Stage.WardReady)) { main.Run("discharge:" + p.Id); return; }
            if (main.Coach.Active) { main.Coach.WrongTap(); return; }
            main.Run("select_bed:" + b);
        }

        /// <summary>A world tap (HospitalWorld.Picked).</summary>
        public void WorldPick(WorldHit hit)
        {
            switch (hit.Kind)
            {
                case "patient": main.Run("select_patient:" + hit.Index); break;
                case "bed": BedTap(hit.Index); break;
                default:
                    if (main.Coach.Active) main.Coach.WrongTap();
                    else main.Run("clear");
                    break;
            }
        }

        /// <summary>After an action ran (NurseMain): open the right sheet, toast, refresh.</summary>
        public void AfterAction(string id, ActionResult r)
        {
            var a = id.Split(':');
            if (r.Ok)
            {
                switch (a[0])
                {
                    case "select_patient": OpenSheet("patient", int.Parse(a[1])); break;
                    case "select_bed": OpenSheet("bed", int.Parse(a[1])); break;
                    case "tag":
                        if (main.Coach.Active || sheetKind != "patient") break;
                        CloseSheet();
                        break;
                    case "assign": case "clear": case "handover": case "start_day": case "next_day":
                        if (sheet != null && sheet.IsOpen && sheetKind != "report") CloseSheet();
                        break;
                    case "discharge": Toast(L.T("toast.discharged"), ToastKind.Success); break;
                    case "lens":
                        Toast(L.T("lens." + r.Text), r.Text == "smooth" ? ToastKind.Success : ToastKind.Warning);
                        if (World != null && Act.LastLens != null) World.ShowLens(Act.LastLens.Zone);
                        break;
                    case "ambulance": Toast(L.T(r.Value > 0 ? "toast.amb_accept" : "toast.amb_divert"), ToastKind.Info); break;
                }
                sheetDirty = true;
            }
            else if (r.Error != "blocked") Toast(L.T("err." + r.Error), ToastKind.Warning);
            Refresh(true);
        }

        public void Toast(string text, ToastKind kind)
        {
            if (!string.IsNullOrEmpty(text) && !text.StartsWith("err.") && !text.StartsWith("lens.")) UiToastHost.Show(text, kind);
        }

        // ------------------------------------------------------------------ frame

        public void Tick(float dt, bool low)
        {
            markerAcc += dt;
            if (markerAcc >= (low ? 0.1f : 1f / 30f)) { markerAcc = 0; PlaceMarkers(); }
            Refresh(false);
            CoachTick(dt);
            if (sheetDirty && sheet != null && sheet.IsOpen && !UiKit.Held(sheet.Body)) { sheetDirty = false; FillSheet(); }
        }

        float hudAcc, lensAcc;
        string lensKey = "";

        public void Refresh(bool force)
        {
            hudAcc += Time.unscaledDeltaTime;
            if (!force && hudAcc < 0.2f) return;   // 5 Hz HUD text
            hudAcc = 0;
            var s = Sim.S;
            UiKit.SetText(dayLabel, L.F("hud.day", "n", s.Day) + " · " + L.T(Sim.HandoverWindow ? "phase.handover" : "phase." + s.Phase));
            UiKit.SetText(clockLabel, Fmt.ClockMin(s.Minute));
            UiKit.SetText(cashLabel, Money(s.Cash));
            UiKit.SetText(repLabel, L.F("hud.reputation", "r", Fmt.Rating(s.Reputation)));
            for (int i = 0; i < speedButtons.Count; i++)
            {
                speedButtons[i].EnableInClassList("cn-speed__btn--on", s.Speed == i + 1);
                speedButtons[i].style.display = Sim.Phase == Phases.Live ? DisplayStyle.Flex : DisplayStyle.None;
            }
            // main action: Start shift / Hand over / Report (the guide shows Hand over only at its last step)
            bool guide = main.Coach.Active;
            bool showMain = (Sim.Phase == Phases.Plan && !guide) || Sim.HandoverWindow || Sim.Phase == Phases.Report;
            mainAction.style.display = showMain ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(mainAction, Sim.Phase == Phases.Plan ? L.T("hud.start_day") : Sim.HandoverWindow ? L.T("hud.handover") : L.T("phase.report"));
            mainAction.EnableInClassList("cn-main-action--handover", Sim.HandoverWindow);
            // the lens pill: the current bottleneck in a few words (1 Hz)
            lensAcc += 0.2f;
            if (force || lensAcc >= 1f)
            {
                lensAcc = 0;
                lensKey = Sim.Phase == Phases.Live ? Sim.FindBottleneck().Key : "smooth";
            }
            bool showLens = Sim.Phase == Phases.Live && (!guide || main.Coach.Step >= 9);
            lensBtn.style.display = showLens ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(lensLabel, L.T("lens.short." + lensKey));
            lensBtn.EnableInClassList("cn-lens--bad", lensKey != "smooth");
            // ambulance call
            var call = s.Ambulance;
            ambCard.style.display = call != null && Sim.Phase == Phases.Live ? DisplayStyle.Flex : DisplayStyle.None;
            if (call != null)
            {
                UiKit.SetText(ambText, L.F("amb.call", "tag", L.T("tag." + Sev.Ids[call.Sev]), "shape", TagShape[call.Sev]) + (call.Guided ? "" : " · " + L.F("amb.left", "min", (long)Math.Ceiling(call.Left))));
                UiKit.SetText(ambAccept, L.T("amb.accept"));
                UiKit.SetText(ambDivert, L.T("amb.divert"));
                ambDivert.style.display = guide ? DisplayStyle.None : DisplayStyle.Flex;
            }
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
                    chip.AddToClassList("cn-chip");
                    chip.AddToClassList("cn-chip--warn");
                    chip.Add(UiKit.Text(L.T("crisis." + c + ".t"), "cn-chip__label"));
                    chip.RegisterCallback<ClickEvent>(_ => OpenSheet("board"));
                    crisesRow.Add(chip);
                }
            }
            foreach (var kv in tabs)
            {
                UiKit.SetText(kv.Value, kv.Key == "settings" ? "⚙" : L.T("tab." + kv.Key));
                kv.Value.EnableInClassList("cn-tab--on", openTab == kv.Key && sheet != null && sheet.IsOpen);
            }
            SyncMarkers();
        }

        void Place(Marker m, Vector3 world, float lift)
        {
            var sp = World.ScreenOf(world + Vector3.up * lift);
            if (sp == null) { m.Root.style.display = DisplayStyle.None; return; }
            m.Root.style.display = DisplayStyle.Flex;
            var pp = ToPanel(sp.Value);
            m.Root.style.translate = new Translate(pp.x, pp.y);
        }

        static void SetClass(Marker m, string cls)
        {
            if (cls == m.LastClass) return;
            if (m.LastClass != "") m.Root.RemoveFromClassList(m.LastClass);
            if (cls != "") m.Root.AddToClassList(cls);
            m.LastClass = cls;
        }

        void PlaceMarkers()
        {
            if (World == null) return;
            for (int i = 0; i < bedMarkers.Count && i < Sim.S.Beds.Count; i++)
            {
                var m = bedMarkers[i];
                var b = Sim.S.Beds[i];
                Place(m, World.BedPos(i), 1.6f);
                var p = b.Patient >= 0 ? Sim.P(b.Patient) : null;
                string text, cls;
                switch (b.State)
                {
                    case BedState.Dirty: text = "!"; cls = "cn-bed--dirty"; break;
                    case BedState.Cleaning: text = "…"; cls = "cn-bed--cleaning"; break;
                    case BedState.Closed: text = "✕"; cls = "cn-bed--closed"; break;
                    default:
                        if (p == null) { text = b.Bay ? "+" : ""; cls = b.Bay ? "cn-bed--free" : "cn-bed--empty"; break; }
                        if (p.Stage == Stage.Ready || p.Stage == Stage.WardReady) { text = "✓"; cls = "cn-bed--ready"; break; }
                        if (p.Stage == Stage.Board) { text = "→"; cls = "cn-bed--board"; break; }
                        text = TagText(Math.Max(p.Tag, 0)); cls = "cn-bed--tag" + Math.Max(p.Tag, 0); break;
                }
                if (text != m.LastText) { m.LastText = text; UiKit.SetText(m.Badge, text); }
                SetClass(m, cls);
                bool spot = Act.SelectedPatient >= 0 && b.Bay && b.State == BedState.Clean && b.Patient < 0;
                m.Root.EnableInClassList("cn-bed--target", spot);
            }
            var waiting = Sim.Waiting();
            for (int k = 0; k < waitMarkers.Count; k++)
            {
                var m = waitMarkers[k];
                if (k >= waiting.Count || k >= 8) { m.Id = -1; m.Root.style.display = DisplayStyle.None; continue; }
                var p = waiting[k];
                m.Id = p.Id;
                Place(m, World.PatientWorld(p.Id, k), 2.9f);
                string text = TagText(p.Tag);
                if (text != m.LastText) { m.LastText = text; UiKit.SetText(m.Badge, text); }
                SetClass(m, p.Tag < 0 ? "cn-wait--new" : "cn-wait--tag" + p.Tag);
                m.Root.EnableInClassList("cn-wait--selected", Act.SelectedPatient == p.Id);
            }
            // overflow count on the 9th chip
            if (waiting.Count > 8 && waitMarkers.Count > 8)
            {
                var m = waitMarkers[8];
                m.Id = -1;
                Place(m, World.WaitSpot(8), 2.9f);
                string t = "+" + (waiting.Count - 8);
                if (t != m.LastText) { m.LastText = t; UiKit.SetText(m.Badge, t); }
                SetClass(m, "cn-wait--more");
            }
        }

        void Relocalise()
        {
            foreach (var m in bedMarkers) m.LastText = "";
            foreach (var m in waitMarkers) m.LastText = "";
            for (int s = 0; s < speedButtons.Count; s++) speedButtons[s].text = L.F("hud.speed", "n", s + 1);
            crisesRow.userData = null;
            Refresh(true);
            sheetDirty = true;
        }

        /// <summary>The top-left screen rect of a UI target for the guide spotlight (null = not a UI target / hidden).</summary>
        Rect? UiTarget(string target)
        {
            VisualElement e = target switch
            {
                "lens" => lensBtn,
                "handover" => mainAction,
                "ambulance" => ambAccept,
                _ => null,
            };
            if (e == null && target.StartsWith("patient:") && int.TryParse(target.Substring(8), out int pid))
                foreach (var m in waitMarkers) if (m.Id == pid) e = m.Root;
            if (e == null && target == "bay:free")
                for (int i = 0; i < Sim.S.Beds.Count && i < bedMarkers.Count; i++)
                    if (Sim.S.Beds[i].Bay && Sim.S.Beds[i].State == BedState.Clean && Sim.S.Beds[i].Patient < 0) { e = bedMarkers[i].Root; break; }
            if (e == null && target.StartsWith("discharge:") && int.TryParse(target.Substring(10), out int did))
            {
                var p = Sim.P(did);
                if (p != null && p.Bed >= 0 && p.Bed < bedMarkers.Count) e = bedMarkers[p.Bed].Root;
            }
            if (e == null && target == "clean")
                for (int i = 0; i < Sim.S.Beds.Count && i < bedMarkers.Count; i++)
                    if (Sim.S.Beds[i].State == BedState.Dirty) { e = bedMarkers[i].Root; break; }
            if (e == null && sheet != null && sheet.IsOpen) e = sheet.Panel.Q<VisualElement>("Target_" + target.Replace(':', '_'));
            if (e == null || e.panel == null || e.resolvedStyle.display == DisplayStyle.None) return null;
            return e.worldBound;
        }

        public static Color ColorOfTag(int tag) => tag >= 0 && tag < TagColor.Length ? TagColor[tag] : Color.white;
    }
}
