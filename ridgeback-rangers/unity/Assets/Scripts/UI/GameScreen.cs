// Ridgeback Rangers — the Region screen (UI Toolkit, CoddyKit theme). Layout (portrait):
//   top    HUD: day + phase, clock, cash, Green Rating; speed buttons; today's crisis chips; the main action (Start the day
//          in the morning plan, Next day on the report)
//   middle the diorama + markers (a fill gauge over every return point, ≥ 48 dp tap target; the depot's coins / baler buttons)
//   bottom the route bar (while a truck is selected: stops, predicted km · fuel · minutes · load, Suggest / Clear / Go) and
//          the tab bar: Network · Fleet · Depot · Research · Shop · ⚙ — each opens a bottom sheet (Sheets.cs)
// Text is set only when it changed (UiKit.SetText, LESSONS A10); lists are rebuilt on events, never under a finger
// (UiKit.Held, fleet §16); markers move at 30 Hz (Low 10 Hz) with style.translate (no layout pass).
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using RidgebackRangers.Game;
using RidgebackRangers.Sim;
using RidgebackRangers.World;

namespace RidgebackRangers.UI
{
    public sealed partial class GameScreen
    {
        readonly VisualElement root;
        readonly RidgebackMain main;
        Loc L => main.Loc;
        Simulation Sim => main.Session.Sim;
        GameActions Act => main.Actions;
        RegionWorld World => main.world;

        // HUD
        Label dayLabel, clockLabel, cashLabel, ratingLabel;
        VisualElement crisesRow, speedRow, hud;
        Button mainAction;
        readonly List<Button> speedButtons = new List<Button>();
        // bottom
        VisualElement bottom, routeBar, tabsRow;
        Label routeTitle, routeEst, fuelLabel;
        Button goBtn, suggestBtn, clearBtn, fuelBadge;
        readonly Dictionary<string, Button> tabs = new Dictionary<string, Button>();
        // markers
        VisualElement markerLayer, depotMarker;
        Button coinsBtn, balerBtn, depotBtn;
        readonly List<Marker> markers = new List<Marker>();
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
            public VisualElement Fill;
            public Label Badge;
            public string LastBadge = "";
            public float LastPct = -1;
            public string LastClass = "";
        }

        public GameScreen(VisualElement root, RidgebackMain main)
        {
            this.root = root;
            this.main = main;
            var uss = Resources.Load<StyleSheet>("ui/RidgebackRangers");
            if (uss != null) root.styleSheets.Add(uss);
            root.AddToClassList("br-root");
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
            hud.AddToClassList("br-hud");
            hud.pickingMode = PickingMode.Ignore;
            var row = Row("br-hud__row");
            var left = Row("br-hud__row");
            dayLabel = ChipLabel(left, "", out _);
            clockLabel = ChipLabel(left, "", out _);
            row.Add(left);
            var right = Row("br-hud__row");
            cashLabel = ChipLabel(right, "", out var cashChip);
            cashChip.AddToClassList("br-chip--money");
            ratingLabel = ChipLabel(right, "", out var ratingChip);
            ratingChip.RegisterCallback<ClickEvent>(_ => OpenSheet("report_last"));
            row.Add(right);
            hud.Add(row);

            var row2 = Row("br-hud__row");
            crisesRow = Row("br-crises");
            fuelBadge = new Button(() => main.Run("fuel_info")) { name = "FuelBadge" };
            fuelBadge.AddToClassList("br-fuel");
            fuelBadge.focusable = false;
            fuelLabel = UiKit.Text("", "coddy-caption-muted");
            fuelBadge.Add(fuelLabel);
            row2.Add(fuelBadge);
            row2.Add(crisesRow);
            speedRow = Row("br-speed");
            for (int s = 1; s <= 4; s++)
            {
                int sp = s;
                var b = new Button(() => OnSpeed(sp)) { text = L.F("hud.speed", "n", sp) };
                b.AddToClassList("br-speed__btn");
                b.focusable = false;
                speedButtons.Add(b);
                speedRow.Add(b);
            }
            row2.Add(speedRow);
            hud.Add(row2);

            mainAction = UiKit.PrimaryButton("", OnMainAction);
            mainAction.AddToClassList("br-main-action");
            mainAction.name = "MainAction";
            hud.Add(mainAction);
            root.Add(hud);
        }

        void BuildBottom()
        {
            bottom = new VisualElement { name = "Bottom" };
            bottom.AddToClassList("br-bottom");
            bottom.pickingMode = PickingMode.Ignore;

            routeBar = new VisualElement { name = "RouteBar" };
            routeBar.AddToClassList("br-route");
            var top = Row("br-route__row");
            routeTitle = UiKit.Text("", "coddy-label-strong");
            top.Add(routeTitle);
            routeBar.Add(top);
            routeEst = UiKit.Text("", "br-route__est");
            routeBar.Add(routeEst);
            var actions = Row("br-route__row");
            suggestBtn = UiKit.ActionPill("", () => main.Run("suggest"), true, "tonal");
            clearBtn = UiKit.ActionPill("", () => main.Run("clear"), true, "tonal");
            goBtn = UiKit.ActionPill("", () => main.Run("go"));
            goBtn.name = "Go";
            actions.Add(suggestBtn);
            actions.Add(clearBtn);
            actions.Add(goBtn);
            routeBar.Add(actions);
            bottom.Add(routeBar);

            tabsRow = Row("br-tabs");
            foreach (var id in new[] { "network", "fleet", "depot", "research", "shop", "settings" })
            {
                string tid = id;
                var b = new Button(() => OnTab(tid)) { name = "Tab_" + id };
                b.AddToClassList("br-tab");
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
            markerLayer.AddToClassList("br-markers");
            markerLayer.pickingMode = PickingMode.Ignore;
            depotMarker = new VisualElement { name = "DepotMarker" };
            depotMarker.AddToClassList("br-depot-marker");
            depotBtn = DepotButton("", () => main.Run("depot"), "Depot");
            coinsBtn = DepotButton("", () => main.Run("till"), "Coins");
            coinsBtn.AddToClassList("br-depot-btn--coins");
            balerBtn = DepotButton("", () => main.Run("baler"), "Baler");
            depotMarker.Add(depotBtn);
            depotMarker.Add(coinsBtn);
            depotMarker.Add(balerBtn);
            markerLayer.Add(depotMarker);
            root.Add(markerLayer);
        }

        Button DepotButton(string text, Action a, string name)
        {
            var b = new Button(a) { text = text, name = name };
            b.AddToClassList("br-depot-btn");
            b.focusable = false;
            return b;
        }

        void SyncMarkers()
        {
            while (markers.Count < Sim.S.Points.Count)
            {
                int i = markers.Count;
                var m = new Marker { Root = new Button(() => main.Run("select_point:" + i)) { name = "Point_" + i } };
                m.Root.AddToClassList("br-marker");
                m.Root.focusable = false;
                m.Root.style.backgroundColor = new Color(0, 0, 0, 0);
                m.Root.style.borderTopWidth = m.Root.style.borderBottomWidth = m.Root.style.borderLeftWidth = m.Root.style.borderRightWidth = 0;
                var gauge = new VisualElement { pickingMode = PickingMode.Ignore };
                gauge.AddToClassList("br-marker__gauge");
                m.Fill = new VisualElement { pickingMode = PickingMode.Ignore };
                m.Fill.AddToClassList("br-marker__fill");
                gauge.Add(m.Fill);
                m.Root.Add(gauge);
                m.Badge = UiKit.Text("", "br-marker__badge");
                m.Root.Add(m.Badge);
                markerLayer.Insert(0, m.Root);
                markers.Add(m);
            }
            while (markers.Count > Sim.S.Points.Count) { markers[markers.Count - 1].Root.RemoveFromHierarchy(); markers.RemoveAt(markers.Count - 1); }
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
            chip.AddToClassList("br-chip");
            var l = UiKit.Text(text, "br-chip__label");
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
            Refresh(false);
        }

        void OnMainAction()
        {
            if (Sim.Phase == Phases.Plan) main.Run("start_day");
            else if (Sim.Phase == Phases.Report) OpenSheet("report");
        }

        void OnTab(string id)
        {
            if (main.Coach.Active && main.Coach.Target != "tab." + id) { main.Coach.WrongTap(); return; }
            if (openTab == id && sheet != null && sheet.IsOpen) { CloseSheet(); return; }
            OpenSheet(id);
        }

        /// <summary>A world tap (RegionWorld.Picked).</summary>
        public void WorldPick(WorldHit hit)
        {
            switch (hit.Kind)
            {
                case "point": main.Run("select_point:" + hit.Index); break;
                case "truck": main.Run("select_truck:" + hit.Index); break;
                case "depot": main.Run("depot"); break;
                default:
                    if (main.Coach.Active) main.Coach.WrongTap();
                    else if (Act.SelectedTruck < 0) main.Run("clear");
                    break;
            }
        }

        /// <summary>After an action ran (RidgebackMain): open the right sheet, refresh.</summary>
        public void AfterAction(string id, ActionResult r)
        {
            var a = id.Split(':');
            if (r.Ok)
            {
                switch (a[0])
                {
                    case "select_point":
                        if (Act.SelectedTruck < 0) OpenSheet("point", int.Parse(a[1]));
                        break;
                    case "select_truck":
                    case "go":
                    case "clear":
                        if (sheet != null && sheet.IsOpen && sheetKind != "depot") CloseSheet();
                        break;
                    case "depot":
                    case "baler":
                        OpenSheet("depot");
                        break;
                    case "till":
                        if (r.Value > 0) Toast(L.F("toast.till", "cash", Money(r.Value)), ToastKind.Success);
                        break;
                    case "fuel_info":
                        Toast(L.T("hud.fuel_info"), ToastKind.Info);
                        break;
                    case "start_day":
                    case "next_day":
                        CloseSheet();
                        break;
                }
                sheetDirty = true;
            }
            else if (r.Error != "blocked") Toast(L.T("err." + r.Error), ToastKind.Warning);
            Refresh(false);
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
            if (sheetDirty && sheet != null && sheet.IsOpen && !UiKit.Held(sheet.Body)) { sheetDirty = false; FillSheet(); }
        }

        float hudAcc;

        public void Refresh(bool force)
        {
            hudAcc += Time.unscaledDeltaTime;
            if (!force && hudAcc < 0.2f) return;   // 5 Hz HUD text
            hudAcc = 0;
            var s = Sim.S;
            UiKit.SetText(dayLabel, L.F("hud.day", "n", s.Day) + " · " + L.T("phase." + s.Phase));
            UiKit.SetText(clockLabel, Fmt.ClockMin(s.Minute));
            UiKit.SetText(cashLabel, Money(s.Cash));
            UiKit.SetText(ratingLabel, L.F("hud.rating", "r", Fmt.Rating(s.Rating)));
            for (int i = 0; i < speedButtons.Count; i++)
            {
                speedButtons[i].EnableInClassList("br-speed__btn--on", s.Speed == i + 1);
                speedButtons[i].style.display = Sim.Phase == Phases.Live ? DisplayStyle.Flex : DisplayStyle.None;
            }
            bool showMain = Sim.Phase != Phases.Live && !(main.Coach.Active && main.Coach.Step < 6);
            mainAction.style.display = showMain ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(mainAction, Sim.Phase == Phases.Plan ? L.T("hud.start_day") : L.T("phase.report"));
            UiKit.SetText(fuelLabel, L.F("hud.fuel", "cost", Money(s.Today.Fuel)));
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
                    chip.AddToClassList("br-chip");
                    chip.AddToClassList("br-chip--warn");
                    chip.Add(UiKit.Text(L.T("crisis." + c + ".t"), "br-chip__label"));
                    crisesRow.Add(chip);
                }
            }
            // route bar
            bool route = Act.SelectedTruck >= 0;
            routeBar.style.display = route ? DisplayStyle.Flex : DisplayStyle.None;
            if (route)
            {
                var t = Sim.S.Trucks[Act.SelectedTruck];
                UiKit.SetText(routeTitle, L.F("truck.name", "type", L.T("truck." + t.Type), "n", Act.SelectedTruck + 1) + " · " + L.F("route.draft", "n", Act.Draft.Count));
                var e = Act.DraftEstimate;
                UiKit.SetText(routeEst, e != null && e.Ok
                    ? L.F("route.est", "km", Fmt.Km(e.Km, L.Lang), "cost", Money(e.Cost), "min", (long)Math.Round(e.Minutes), "pct", Fmt.Pct(e.LoadPct, L.Lang))
                    : (Sim.FreeDrivers <= 0 && !t.Busy ? L.T("truck.no_driver") : L.T("route.hint")));
                goBtn.SetEnabled(Act.Draft.Count > 0);
            }
            UiKit.SetText(suggestBtn, L.T("truck.suggest"));
            UiKit.SetText(clearBtn, L.T("truck.clear"));
            UiKit.SetText(goBtn, L.T("truck.go"));
            foreach (var kv in tabs)
            {
                UiKit.SetText(kv.Value, kv.Key == "settings" ? "⚙" : L.T("tab." + kv.Key));
                kv.Value.EnableInClassList("br-tab--on", openTab == kv.Key && sheet != null && sheet.IsOpen);
            }
            // depot marker
            UiKit.SetText(depotBtn, L.T("depot.title"));
            double till = Math.Floor(s.Depot.Till);
            coinsBtn.style.display = till >= 1 || main.Coach.Target == "coins" ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(coinsBtn, "+" + Money(till));
            UiKit.SetText(balerBtn, s.Depot.BalerDown ? "⚠ " + L.T("depot.baler") : L.T("depot.baler"));
            SyncMarkers();
        }

        void PlaceMarkers()
        {
            if (World == null) return;
            for (int i = 0; i < markers.Count && i < Sim.S.Points.Count; i++)
            {
                var m = markers[i];
                var p = Sim.S.Points[i];
                var sp = World.ScreenOf(World.PointPos(i) + Vector3.up * 8f);
                if (sp == null) { m.Root.style.display = DisplayStyle.None; continue; }
                m.Root.style.display = DisplayStyle.Flex;
                var pp = ToPanel(sp.Value);
                m.Root.style.translate = new Translate(pp.x, pp.y);
                float pct = (float)Sim.DisplayPct(p);
                if (Mathf.Abs(pct - m.LastPct) > 0.01f) { m.LastPct = pct; m.Fill.style.height = Length.Percent(pct * 100f); }
                string cls = p.Jammed || Sim.IsFull(p) ? "br-marker__fill--full" : pct >= p.Threshold / 100f - 0.0001f ? "br-marker__fill--warn" : "";
                if (cls != m.LastClass)
                {
                    m.Fill.EnableInClassList("br-marker__fill--full", cls == "br-marker__fill--full");
                    m.Fill.EnableInClassList("br-marker__fill--warn", cls == "br-marker__fill--warn");
                    m.LastClass = cls;
                }
                m.Fill.EnableInClassList("br-marker__fill--estimate", !Sim.DisplayIsLive(p));
                int stop = Act.Draft.IndexOf(i);
                string badge = p.Jammed ? "!" : stop >= 0 ? (stop + 1).ToString() : Fmt.Pct(pct, L.Lang);
                if (badge != m.LastBadge) { m.LastBadge = badge; UiKit.SetText(m.Badge, badge); }
                m.Root.EnableInClassList("br-marker--route", stop >= 0);
                m.Root.EnableInClassList("br-marker--jam", p.Jammed);
                m.Root.EnableInClassList("br-marker--selected", Act.SelectedPoint == i);
            }
            var dp = World.ScreenOf(World.DepotPos + Vector3.up * 14f);
            if (dp != null) { var pp = ToPanel(dp.Value); depotMarker.style.translate = new Translate(pp.x, pp.y); }
        }

        void Relocalise()
        {
            foreach (var m in markers) { m.LastBadge = ""; }
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
                "go" => goBtn,
                "fuel" => fuelBadge,
                "depot" => depotBtn,
                "coins" => coinsBtn,
                "baler" => balerBtn,
                _ => null,
            };
            if (e == null && target.StartsWith("point:") && int.TryParse(target.Substring(6), out int pi) && pi < markers.Count) e = markers[pi].Root;
            if (e == null && sheet != null && sheet.IsOpen) e = sheet.Panel.Q<VisualElement>("Target_" + target.Replace(':', '_'));
            if (e == null || e.panel == null || e.resolvedStyle.display == DisplayStyle.None) return null;
            return e.worldBound;
        }
    }
}
