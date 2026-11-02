// Grill Convoy — the city screen (UI Toolkit, CoddyKit theme). Layout (portrait):
//   top    HUD: day + phase, clock, weather, cash, Fame; speed buttons; today's crisis chips; the main action (Open the
//          window / Start the day in the morning plan, Day report after 20:00); the guide's hand-cook button
//   middle the diorama + markers (a badge over every truck — line length + patty bar, ≥ 48 dp tap target; a name + medal
//          over every pitch; the commissary)
//   bottom the truck bar (the focused open truck: line, stock, All hands / Extinguish / Propane / Restock) and the tab bar:
//          Map · Trucks · Kitchen · Research · Shop · ⚙ — each opens a bottom sheet (Sheets.cs)
// Text is set only when it changed (UiKit.SetText, LESSONS A10); lists are rebuilt on events, never under a finger
// (UiKit.Held, fleet §16); markers move at 30 Hz (Low 10 Hz) with style.translate (no layout pass).
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using GrillConvoy.Game;
using GrillConvoy.Sim;
using GrillConvoy.World;

namespace GrillConvoy.UI
{
    public sealed partial class GameScreen
    {
        readonly VisualElement root;
        readonly GrillMain main;
        Loc L => main.Loc;
        Simulation Sim => main.Session.Sim;
        GameActions Act => main.Actions;
        RegionWorld World => main.world;

        // HUD
        Label dayLabel, clockLabel, weatherLabel, cashLabel, fameLabel;
        VisualElement crisesRow, speedRow, hud;
        Button mainAction, cookBtn;
        readonly List<Button> speedButtons = new List<Button>();
        // bottom
        VisualElement bottom, truckBar, tabsRow;
        Label barTitle, barInfo;
        Button allHandsBtn, extinguishBtn, propaneBtn, restockBtn, openTruckBtn;
        readonly Dictionary<string, Button> tabs = new Dictionary<string, Button>();
        // markers
        VisualElement markerLayer;
        Button commissaryBtn;
        readonly List<Marker> truckMarkers = new List<Marker>();
        readonly Dictionary<int, Button> pitchMarkers = new Dictionary<int, Button>();
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
        }

        public GameScreen(VisualElement root, GrillMain main)
        {
            this.root = root;
            this.main = main;
            var uss = Resources.Load<StyleSheet>("ui/GrillConvoy");
            if (uss != null) root.styleSheets.Add(uss);
            root.AddToClassList("gc-root");
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
            hud.AddToClassList("gc-hud");
            hud.pickingMode = PickingMode.Ignore;
            var row = Row("gc-hud__row");
            var left = Row("gc-hud__row");
            dayLabel = ChipLabel(left, out _);
            clockLabel = ChipLabel(left, out _);
            row.Add(left);
            var right = Row("gc-hud__row");
            cashLabel = ChipLabel(right, out var cashChip);
            cashChip.AddToClassList("gc-chip--money");
            fameLabel = ChipLabel(right, out var fameChip);
            fameChip.RegisterCallback<ClickEvent>(_ => OpenSheet("report_last"));
            row.Add(right);
            hud.Add(row);

            var row2 = Row("gc-hud__row");
            var leftRow = Row("gc-crises");
            weatherLabel = ChipLabel(leftRow, out var weatherChip);
            weatherChip.RegisterCallback<ClickEvent>(_ => OpenSheet("map"));
            crisesRow = Row("gc-crises");
            leftRow.Add(crisesRow);
            row2.Add(leftRow);
            speedRow = Row("gc-speed");
            for (int s = 1; s <= 4; s++)
            {
                int sp = s;
                var b = new Button(() => OnSpeed(sp)) { text = L.F("hud.speed", "n", sp) };
                b.AddToClassList("gc-speed__btn");
                b.focusable = false;
                speedButtons.Add(b);
                speedRow.Add(b);
            }
            row2.Add(speedRow);
            hud.Add(row2);

            mainAction = UiKit.PrimaryButton("", OnMainAction);
            mainAction.AddToClassList("gc-main-action");
            mainAction.name = "MainAction";
            hud.Add(mainAction);
            cookBtn = UiKit.PrimaryButton("", OnCook);
            cookBtn.AddToClassList("gc-cook");
            cookBtn.name = "Cook";
            hud.Add(cookBtn);
            root.Add(hud);
        }

        void BuildBottom()
        {
            bottom = new VisualElement { name = "Bottom" };
            bottom.AddToClassList("gc-bottom");
            bottom.pickingMode = PickingMode.Ignore;

            truckBar = new VisualElement { name = "TruckBar" };
            truckBar.AddToClassList("gc-truckbar");
            var top = Row("gc-truckbar__row");
            barTitle = UiKit.Text("", "coddy-label-strong");
            top.Add(barTitle);
            openTruckBtn = UiKit.ActionPill("", () => { int t = FocusTruck(); if (t >= 0) main.Run("select_truck:" + t); }, true, "tonal");
            top.Add(openTruckBtn);
            truckBar.Add(top);
            barInfo = UiKit.Text("", "gc-truckbar__info");
            truckBar.Add(barInfo);
            var actions = Row("gc-truckbar__row");
            allHandsBtn = UiKit.ActionPill("", () => main.Run("allhands:" + FocusTruck()));
            allHandsBtn.name = "AllHands";
            extinguishBtn = UiKit.ActionPill("", () => main.Run("extinguish:" + FocusTruck()), true, "danger");
            extinguishBtn.name = "Extinguish";
            propaneBtn = UiKit.ActionPill("", () => main.Run("propane:" + FocusTruck()), true, "tonal");
            restockBtn = UiKit.ActionPill("", () => main.Run("restock:" + FocusTruck()), true, "tonal");
            actions.Add(allHandsBtn);
            actions.Add(extinguishBtn);
            actions.Add(propaneBtn);
            actions.Add(restockBtn);
            truckBar.Add(actions);
            bottom.Add(truckBar);

            tabsRow = Row("gc-tabs");
            foreach (var id in new[] { "map", "trucks", "kitchen", "research", "shop", "settings" })
            {
                string tid = id;
                var b = new Button(() => OnTab(tid)) { name = "Tab_" + id };
                b.AddToClassList("gc-tab");
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
            markerLayer.AddToClassList("gc-markers");
            markerLayer.pickingMode = PickingMode.Ignore;
            commissaryBtn = new Button(() => OpenSheet("kitchen")) { name = "CommissaryMarker" };
            commissaryBtn.AddToClassList("gc-place");
            commissaryBtn.focusable = false;
            markerLayer.Add(commissaryBtn);
            root.Add(markerLayer);
        }

        void SyncMarkers()
        {
            while (truckMarkers.Count < Sim.S.Trucks.Count)
            {
                int i = truckMarkers.Count;
                var m = new Marker { Root = new Button(() => main.Run("select_truck:" + i)) { name = "Truck_" + i } };
                m.Root.AddToClassList("gc-marker");
                m.Root.focusable = false;
                var gauge = new VisualElement { pickingMode = PickingMode.Ignore };
                gauge.AddToClassList("gc-marker__gauge");
                m.Fill = new VisualElement { pickingMode = PickingMode.Ignore };
                m.Fill.AddToClassList("gc-marker__fill");
                gauge.Add(m.Fill);
                m.Badge = UiKit.Text("", "gc-marker__badge");
                m.Root.Add(m.Badge);
                m.Root.Add(gauge);
                markerLayer.Add(m.Root);
                truckMarkers.Add(m);
            }
            while (truckMarkers.Count > Sim.S.Trucks.Count) { truckMarkers[truckMarkers.Count - 1].Root.RemoveFromHierarchy(); truckMarkers.RemoveAt(truckMarkers.Count - 1); }
            foreach (int p in Sim.Gd.Pitches)
            {
                if (pitchMarkers.ContainsKey(p)) continue;
                string id = Sim.Gd.Nodes[p].Id;
                var b = new Button(() => main.Run("select_pitch:" + id)) { name = "Pitch_" + id };
                b.AddToClassList("gc-place");
                b.focusable = false;
                markerLayer.Insert(0, b);
                pitchMarkers[p] = b;
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

        static Label ChipLabel(VisualElement parent, out VisualElement chip)
        {
            chip = new VisualElement();
            chip.AddToClassList("gc-chip");
            var l = UiKit.Text("", "gc-chip__label");
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

        /// <summary>The truck the bar shows: the selected one if it is out, else the first open / driving truck, else -1.</summary>
        int FocusTruck()
        {
            var tr = Sim.S.Trucks;
            if (Act.SelectedTruck >= 0 && Act.SelectedTruck < tr.Count && tr[Act.SelectedTruck].Busy) return Act.SelectedTruck;
            for (int i = 0; i < tr.Count; i++) if (tr[i].Open) return i;
            for (int i = 0; i < tr.Count; i++) if (tr[i].Busy) return i;
            return -1;
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

        static readonly string[] CookActions = { "", "ticket", "cook:grill", "", "cook:flip", "cook:assemble", "cook:serve" };

        void OnCook()
        {
            int st = Sim.S.HandStage;
            if (st > 0 && st < CookActions.Length && CookActions[st] != "") main.Run(CookActions[st]);
        }

        void OnTab(string id)
        {
            if (openTab == id && sheet != null && sheet.IsOpen) { CloseSheet(); return; }
            OpenSheet(id);
        }

        /// <summary>A world tap (RegionWorld.Picked).</summary>
        public void WorldPick(WorldHit hit)
        {
            switch (hit.Kind)
            {
                case "truck": main.Run("select_truck:" + hit.Index); break;
                case "pitch": main.Run("select_pitch:" + Sim.Gd.Nodes[hit.Index].Id); break;
                case "commissary": OpenSheet("kitchen"); break;
                default: if (main.Coach.Active) main.Coach.WrongTap(); break;
            }
        }

        /// <summary>After an action ran (GrillMain): open the right sheet, refresh.</summary>
        public void AfterAction(string id, ActionResult r)
        {
            var a = id.Split(':');
            if (r.Ok)
            {
                switch (a[0])
                {
                    case "select_truck": OpenSheet("truck", int.Parse(a[1])); break;
                    case "select_pitch": OpenSheet("pitch", Act.SelectedPitch); break;
                    case "start_day":
                    case "next_day":
                        CloseSheet();
                        break;
                    case "restock": Toast(L.T("toast.restock"), ToastKind.Info); break;
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

        // ------------------------------------------------------------------ frame

        public void Tick(float dt, bool low)
        {
            markerAcc += dt;
            if (markerAcc >= (low ? 0.1f : 1f / 30f)) { markerAcc = 0; PlaceMarkers(); }
            Refresh(false);
            CoachTick(dt);
            if (sheetDirty && sheet != null && sheet.IsOpen && !UiKit.Held(sheet.Body)) { sheetDirty = false; FillSheet(); }
        }

        /// <summary>Sheets refresh on the next calm frame (sim events that change what an open sheet shows).</summary>
        public void MarkSheetDirty() => sheetDirty = true;

        float hudAcc;

        public void Refresh(bool force)
        {
            hudAcc += Time.unscaledDeltaTime;
            if (!force && hudAcc < 0.2f) return;   // 5 Hz HUD text
            hudAcc = 0;
            var s = Sim.S;
            UiKit.SetText(dayLabel, L.F("hud.day", "n", s.Day) + " · " + L.T("phase." + s.Phase));
            UiKit.SetText(clockLabel, Fmt.ClockMin(s.Minute));
            UiKit.SetText(weatherLabel, L.T("weather." + s.Weather));
            UiKit.SetText(cashLabel, Money(s.Cash));
            UiKit.SetText(fameLabel, L.F("hud.fame", "r", Fmt.Rating(s.Fame)));
            for (int i = 0; i < speedButtons.Count; i++)
            {
                speedButtons[i].EnableInClassList("gc-speed__btn--on", s.Speed == i + 1);
                speedButtons[i].style.display = Sim.Phase == Phases.Live && !main.Coach.Active ? DisplayStyle.Flex : DisplayStyle.None;
            }
            var c = main.Coach;
            bool guideHidesMain = c.Active && c.Step != 1 && c.Step != 12;
            bool showMain = Sim.Phase != Phases.Live && !guideHidesMain;
            mainAction.style.display = showMain ? DisplayStyle.Flex : DisplayStyle.None;
            bool firstOpen = s.Day == 1 && s.Trucks.Count > 0 && s.Trucks[0].Open;
            UiKit.SetText(mainAction, Sim.Phase == Phases.Plan ? L.T(firstOpen ? "hud.open_window" : "hud.start_day") : L.T("phase.report"));
            // the guide's hand-cooked ticket
            int hs = s.HandStage;
            cookBtn.style.display = hs > 0 && Sim.Phase == Phases.Live ? DisplayStyle.Flex : DisplayStyle.None;
            if (hs > 0)
            {
                UiKit.SetText(cookBtn, L.T("cook." + hs));
                cookBtn.SetEnabled(hs != 3);
            }
            // crisis chips
            var crises = Sim.ActiveCrises();
            crises.Remove("heat");
            crises.Remove("rain");
            string key = string.Join(",", crises);
            if ((string)crisesRow.userData != key)
            {
                crisesRow.userData = key;
                crisesRow.Clear();
                foreach (var cr in crises)
                {
                    var chip = new VisualElement();
                    chip.AddToClassList("gc-chip");
                    chip.AddToClassList("gc-chip--warn");
                    chip.Add(UiKit.Text(L.T("crisis." + cr + ".t"), "gc-chip__label"));
                    crisesRow.Add(chip);
                }
            }
            RefreshTruckBar();
            foreach (var kv in tabs)
            {
                UiKit.SetText(kv.Value, kv.Key == "settings" ? "⚙" : L.T("tab." + kv.Key));
                kv.Value.EnableInClassList("gc-tab--on", openTab == kv.Key && sheet != null && sheet.IsOpen);
            }
            UiKit.SetText(commissaryBtn, L.T("kitchen.title"));
            SyncMarkers();
            foreach (var kv in pitchMarkers)
            {
                bool open = Sim.PitchOpen(kv.Key);
                string medal = open ? " " + L.T("medal." + Sim.Medal(kv.Key)) : " 🔒";
                UiKit.SetText(kv.Value, L.T("place." + Sim.Gd.Nodes[kv.Key].Id) + medal);
                kv.Value.EnableInClassList("gc-place--locked", !open);
            }
        }

        void RefreshTruckBar()
        {
            int ti = FocusTruck();
            bool show = ti >= 0 && Sim.Phase == Phases.Live && Sim.S.HandStage == 0;
            truckBar.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            var t = Sim.S.Trucks[ti];
            string where = t.Open ? L.T("place." + Sim.Gd.Nodes[t.AtNode].Id) : L.T("truck.status." + t.Status);
            UiKit.SetText(barTitle, L.F("truck.name", "n", ti + 1) + " · " + where);
            UiKit.SetText(barInfo, t.Open
                ? L.F("bar.info", "q", t.Queue.Count, "p", t.Load[Stock.Patty], "b", t.Load[Stock.Bun], "rate", (long)Math.Round(Sim.LiveThroughput(t) * 60))
                : L.F("bar.driving", "p", t.Load[Stock.Patty]));
            UiKit.SetText(openTruckBtn, L.T("bar.open"));
            bool ready = t.Open && t.AllHandsReady <= Sim.S.Minute;
            UiKit.SetText(allHandsBtn, t.AllHandsUntil > Sim.S.Minute ? L.T("bar.allhands_on") : ready ? L.T("bar.allhands") : L.F("bar.allhands_wait", "min", (long)Math.Ceiling(t.AllHandsReady - Sim.S.Minute)));
            allHandsBtn.SetEnabled(ready);
            allHandsBtn.style.display = t.Open ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(extinguishBtn, L.T("bar.extinguish"));
            extinguishBtn.style.display = t.Flaring ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(propaneBtn, Sim.S.PropaneKits > 0 ? L.T("bar.propane_kit") : L.F("bar.propane", "cost", Money(Sim.Gd.Svc.PropaneSwap)));
            propaneBtn.style.display = t.Open && t.Propane <= Sim.Gd.Svc.PropaneLow ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(restockBtn, L.T("bar.restock"));
            restockBtn.style.display = t.Open && !main.Coach.Active ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void PlaceMarkers()
        {
            if (World == null) return;
            for (int i = 0; i < truckMarkers.Count && i < Sim.S.Trucks.Count; i++)
            {
                var m = truckMarkers[i];
                var t = Sim.S.Trucks[i];
                var sp = World.ScreenOf(World.TruckWorld(i) + Vector3.up * 9f);
                if (sp == null) { m.Root.style.display = DisplayStyle.None; continue; }
                m.Root.style.display = DisplayStyle.Flex;
                var pp = ToPanel(sp.Value);
                m.Root.style.translate = new Translate(pp.x, pp.y);
                float pct = Mathf.Clamp01(t.Load[Stock.Patty] / (float)Mathf.Max(1, Sim.TruckCap(t, Stock.Patty)));
                if (Mathf.Abs(pct - m.LastPct) > 0.01f) { m.LastPct = pct; m.Fill.style.width = Length.Percent(pct * 100f); }
                m.Fill.EnableInClassList("gc-marker__fill--low", pct < 0.15f);
                string badge = t.Flaring ? "🔥" : t.Open ? (i + 1) + " · " + t.Queue.Count : (i + 1).ToString();
                if (badge != m.LastBadge) { m.LastBadge = badge; UiKit.SetText(m.Badge, badge); }
                m.Root.EnableInClassList("gc-marker--busy", t.Open && t.Queue.Count >= Sim.QueueCap(t) - 2);
                m.Root.EnableInClassList("gc-marker--selected", Act.SelectedTruck == i);
            }
            foreach (var kv in pitchMarkers)
            {
                var sp = World.ScreenOf(World.PitchPos(kv.Key) + new Vector3(0, 2f, -26f));
                if (sp == null) { kv.Value.style.display = DisplayStyle.None; continue; }
                kv.Value.style.display = DisplayStyle.Flex;
                var pp = ToPanel(sp.Value);
                kv.Value.style.translate = new Translate(pp.x, pp.y);
            }
            var cp = World.ScreenOf(World.CommissaryPos + new Vector3(0, 14f, 18f));
            if (cp != null) { var pp = ToPanel(cp.Value); commissaryBtn.style.translate = new Translate(pp.x, pp.y); }
        }

        void Relocalise()
        {
            foreach (var m in truckMarkers) m.LastBadge = "";
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
                "main" => mainAction,
                "cook" => cookBtn,
                "allhands" => allHandsBtn,
                "extinguish" => extinguishBtn,
                _ => null,
            };
            if (e == null && sheet != null && sheet.IsOpen) e = sheet.Panel.Q<VisualElement>("Target_" + target.Replace(':', '_'));
            if (e == null || e.panel == null || e.resolvedStyle.display == DisplayStyle.None) return null;
            return e.worldBound;
        }
    }
}
