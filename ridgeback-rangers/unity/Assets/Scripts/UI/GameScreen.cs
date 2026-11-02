// Ridgeback Rangers — the Island screen (UI Toolkit, CoddyKit theme). Layout (portrait):
//   top    HUD: the Chief's badge (day + phase), clock, coins, Conservation Points, Sanctuary Rating; speed buttons; today's
//          event chips; the main action (Start the day in the morning plan, Day report at the end)
//   middle the diorama + markers: a PIN over every problem (icon + time left, ≥ 48 dp tap target), the gate (while closed),
//          the tram (queue count, tap = send it out), a "+" over every empty paddock slot, the "Fed!" button in the guide
//   bottom the tab bar: Park · Rangers · Hatchery · Plan · Shop · ⚙ — each opens a bottom sheet (Sheets.cs)
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
        IslandWorld World => main.world;

        // HUD
        Label clockLabel, cashLabel, cpLabel, ratingLabel;
        Button badge;
        VisualElement crisesRow, speedRow, hud;
        Button mainAction;
        readonly List<Button> speedButtons = new List<Button>();
        // bottom
        VisualElement bottom, tabsRow;
        readonly Dictionary<string, Button> tabs = new Dictionary<string, Button>();
        // markers
        VisualElement markerLayer;
        Button gateBtn, tramBtn, fedBtn;
        readonly Dictionary<int, Button> pinMarkers = new Dictionary<int, Button>();
        readonly Dictionary<int, Button> slotMarkers = new Dictionary<int, Button>();
        float markerAcc;
        // state
        string openTab = "";
        UiKit.Sheet sheet;
        string sheetKind = "";
        int sheetArg = -1;
        bool sheetDirty;
        public bool WindowOpen => (sheet != null && sheet.IsOpen) || PagerOpen;

        public static readonly Dictionary<string, string> PinIcon = new Dictionary<string, string>
        {
            { PinKinds.Hungry, "🥬" }, { PinKinds.Squabble, "💢" }, { PinKinds.Sick, "✚" }, { PinKinds.Fence, "🔧" }, { PinKinds.Escape, "⚠" }, { PinKinds.Flood, "💧" },
        };

        public GameScreen(VisualElement root, RidgebackMain main)
        {
            this.root = root;
            this.main = main;
            var uss = Resources.Load<StyleSheet>("ui/RidgebackRangers");
            if (uss != null) root.styleSheets.Add(uss);
            root.AddToClassList("rr-root");
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
            hud.AddToClassList("rr-hud");
            hud.pickingMode = PickingMode.Ignore;
            var row = Row("rr-hud__row");
            var left = Row("rr-hud__row");
            badge = new Button(() => main.Run("badge")) { name = "Badge" };
            badge.AddToClassList("rr-badge");
            badge.focusable = false;
            left.Add(badge);
            clockLabel = ChipLabel(left, "", out _);
            row.Add(left);
            var right = Row("rr-hud__row");
            cashLabel = ChipLabel(right, "", out var cashChip);
            cashChip.AddToClassList("rr-chip--money");
            cpLabel = ChipLabel(right, "", out var cpChip);
            cpChip.AddToClassList("rr-chip--cp");
            ratingLabel = ChipLabel(right, "", out var ratingChip);
            ratingChip.RegisterCallback<ClickEvent>(_ => OpenSheet("report_last"));
            row.Add(right);
            hud.Add(row);

            var row2 = Row("rr-hud__row");
            crisesRow = Row("rr-crises");
            row2.Add(crisesRow);
            speedRow = Row("rr-speed");
            for (int s = 1; s <= 3; s++)
            {
                int sp = s;
                var b = new Button(() => OnSpeed(sp)) { text = L.F("hud.speed", "n", sp) };
                b.AddToClassList("rr-speed__btn");
                b.focusable = false;
                speedButtons.Add(b);
                speedRow.Add(b);
            }
            row2.Add(speedRow);
            hud.Add(row2);

            mainAction = UiKit.PrimaryButton("", OnMainAction);
            mainAction.AddToClassList("rr-main-action");
            mainAction.name = "MainAction";
            hud.Add(mainAction);
            root.Add(hud);
        }

        void BuildBottom()
        {
            bottom = new VisualElement { name = "Bottom" };
            bottom.AddToClassList("rr-bottom");
            bottom.pickingMode = PickingMode.Ignore;
            tabsRow = Row("rr-tabs");
            foreach (var id in new[] { "park", "rangers", "hatchery", "plan", "shop", "settings" })
            {
                string tid = id;
                var b = new Button(() => OnTab(tid)) { name = "Tab_" + id };
                b.AddToClassList("rr-tab");
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
            markerLayer.AddToClassList("rr-markers");
            markerLayer.pickingMode = PickingMode.Ignore;
            gateBtn = MarkerButton("", () => main.Run("gate"), "Gate", "rr-marker--gate");
            tramBtn = MarkerButton("", OnTramTap, "Tram", "rr-marker--tram");
            fedBtn = MarkerButton("", () => main.Run("fed_ok"), "Fed", "rr-marker--fed");
            root.Add(markerLayer);
        }

        Button MarkerButton(string text, Action a, string name, string cls)
        {
            var b = new Button(a) { text = text, name = name };
            b.AddToClassList("rr-marker");
            b.AddToClassList(cls);
            b.focusable = false;
            markerLayer.Add(b);
            return b;
        }

        void OnTramTap()
        {
            if (Sim.Phase == Phases.Live && !Sim.S.Tram.Running && (Sim.Queued > 0 || Sim.S.Tram.Riders > 0)) main.Run("tram_go");
            else OpenSheet("plan");
        }

        void SyncMarkers()
        {
            // pins: one marker per open pin (keyed by pin id)
            var alive = new HashSet<int>();
            foreach (var p in Sim.S.Pins)
            {
                alive.Add(p.Id);
                if (pinMarkers.ContainsKey(p.Id)) continue;
                int id = p.Id;
                var b = MarkerButton(PinIcon.TryGetValue(p.Kind, out var ic) ? ic : "!", () => OnPinTap(id), "Pin_" + p.Id, "rr-marker--pin");
                b.AddToClassList("rr-marker--pin-" + p.Kind);
                b.userData = p.Kind;
                pinMarkers[p.Id] = b;
            }
            foreach (var id in new List<int>(pinMarkers.Keys))
                if (!alive.Contains(id)) { pinMarkers[id].RemoveFromHierarchy(); pinMarkers.Remove(id); }
            // empty paddock slots in open zones
            var slots = new HashSet<int>();
            foreach (var n in Sim.Gd.Nodes)
            {
                if (n.Kind != "paddock" || !Sim.Unlocked(n.Zone) || Sim.PaddockAt(n.Index) >= 0) continue;
                slots.Add(n.Index);
                if (slotMarkers.ContainsKey(n.Index)) continue;
                int node = n.Index;
                slotMarkers[node] = MarkerButton("+", () => OnSlotTap(node), "Slot_" + n.Id, "rr-marker--slot");
            }
            foreach (var id in new List<int>(slotMarkers.Keys))
                if (!slots.Contains(id)) { slotMarkers[id].RemoveFromHierarchy(); slotMarkers.Remove(id); }
        }

        void OnPinTap(int id)
        {
            var r = main.Run("pin:" + id);
            if (r.Ok) OpenSheet("pin", id);
        }

        void OnSlotTap(int node)
        {
            if (main.Coach.Active) { main.Coach.WrongTap(); return; }
            OpenSheet("build", node);
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
            chip.AddToClassList("rr-chip");
            var l = UiKit.Text(text, "rr-chip__label");
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
        string Cp(double v) => L.F("hud.cp", "n", Fmt.Int((long)Math.Floor(v), L.Lang));

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
            if (main.Coach.Active) { main.Coach.WrongTap(); return; }
            if (openTab == id && sheet != null && sheet.IsOpen) { CloseSheet(); return; }
            OpenSheet(id);
        }

        /// <summary>A world tap (IslandWorld.Picked).</summary>
        public void WorldPick(WorldHit hit)
        {
            switch (hit.Kind)
            {
                case "animal": OnAnimalTap(hit.Index); break;
                case "tram": if (main.Coach.Active && main.Coach.Step != 7) main.Coach.WrongTap(); else OnTramTap(); break;
                case "gate": if (!Sim.S.GateOpen) main.Run("gate"); else if (main.Coach.Active) main.Coach.WrongTap(); break;
                case "paddock": if (main.Coach.Active) main.Coach.WrongTap(); else OpenSheet("paddock", hit.Index); break;
                case "slot": OnSlotTap(hit.Index); break;
                default:
                    if (main.Coach.Active) main.Coach.WrongTap();
                    break;
            }
        }

        void OnAnimalTap(int i)
        {
            var r = main.Run("animal:" + i);
            if (r.Ok && Act.CompareSecond >= 0) OpenSheet("compare");
            else if (r.Ok) Toast(L.F("compare.picked", "name", AnimalName(i)), ToastKind.Info);
        }

        string AnimalName(int i) => i >= 0 && i < Sim.S.Animals.Count ? L.T("sp." + Sim.S.Animals[i].Species) + (Sim.S.Animals[i].Young ? " · " + L.T("animal.young") : "") : "";

        /// <summary>After an action ran (RidgebackMain): open the right sheet, refresh.</summary>
        public void AfterAction(string id, ActionResult r)
        {
            var a = id.Split(':');
            if (r.Ok)
            {
                switch (a[0])
                {
                    case "send":
                        Toast(L.T("toast.sent"), ToastKind.Info);
                        if (sheet != null && sheet.IsOpen && sheetKind == "pin") CloseSheet();
                        break;
                    case "gate": Toast(L.T("toast.gate"), ToastKind.Success); break;
                    case "tram_go": Toast(L.F("toast.tram", "n", Sim.S.Tram.Riders), ToastKind.Success); break;
                    case "badge": Toast(L.F("hud.badge_info", "r", Fmt.Rating(Sim.S.Rating)), ToastKind.Info); break;
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

        /// <summary>Something in the sim changed that an open sheet shows (pins, eggs, a hatchling…).</summary>
        public void MarkSheetDirty() => sheetDirty = true;

        float hudAcc;

        public void Refresh(bool force)
        {
            hudAcc += Time.unscaledDeltaTime;
            if (!force && hudAcc < 0.2f) return;   // 5 Hz HUD text
            hudAcc = 0;
            var s = Sim.S;
            UiKit.SetText(badge, "★ " + L.F("hud.day", "n", s.Day) + " · " + L.T("phase." + s.Phase));
            UiKit.SetText(clockLabel, Fmt.ClockMin(s.Minute));
            UiKit.SetText(cashLabel, Money(s.Cash));
            UiKit.SetText(cpLabel, Cp(s.Cp));
            UiKit.SetText(ratingLabel, L.F("hud.rating", "r", Fmt.Rating(s.Rating)));
            for (int i = 0; i < speedButtons.Count; i++)
            {
                speedButtons[i].EnableInClassList("rr-speed__btn--on", s.Speed == i + 1);
                speedButtons[i].style.display = Sim.Phase == Phases.Live && !main.Coach.Active ? DisplayStyle.Flex : DisplayStyle.None;
            }
            bool showMain = Sim.Phase != Phases.Live && !main.Coach.Active;
            mainAction.style.display = showMain ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(mainAction, Sim.Phase == Phases.Plan ? L.T("hud.start_day") : L.T("phase.report"));
            // event chips
            var crises = Sim.ActiveCrises();
            string key = string.Join(",", crises);
            if ((string)crisesRow.userData != key)
            {
                crisesRow.userData = key;
                crisesRow.Clear();
                foreach (var c in crises)
                {
                    var chip = new VisualElement();
                    chip.AddToClassList("rr-chip");
                    chip.AddToClassList("rr-chip--warn");
                    chip.Add(UiKit.Text(L.T("crisis." + c + ".t"), "rr-chip__label"));
                    crisesRow.Add(chip);
                }
            }
            foreach (var kv in tabs)
            {
                UiKit.SetText(kv.Value, kv.Key == "settings" ? "⚙" : L.T("tab." + kv.Key));
                kv.Value.EnableInClassList("rr-tab--on", openTab == kv.Key && sheet != null && sheet.IsOpen);
            }
            // world buttons
            gateBtn.style.display = Sim.Phase == Phases.Live && !s.GateOpen ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(gateBtn, L.T("marker.gate"));
            int waiting = Sim.Queued + (s.Tram.Running ? 0 : s.Tram.Riders);
            UiKit.SetText(tramBtn, s.Tram.Running ? L.T("marker.tram_out") : L.F("marker.tram", "n", waiting));
            tramBtn.EnableInClassList("rr-marker--ready", !s.Tram.Running && waiting > 0 && Sim.Phase == Phases.Live);
            bool fed = main.Coach.Active && main.Coach.Target == "fed";
            fedBtn.style.display = fed ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(fedBtn, L.T("marker.fed"));
            SyncMarkers();
        }

        void Place(VisualElement e, Vector3 world)
        {
            var sp = World.ScreenOf(world);
            if (sp == null) { e.style.visibility = Visibility.Hidden; return; }
            e.style.visibility = Visibility.Visible;
            var pp = ToPanel(sp.Value);
            e.style.translate = new Translate(pp.x, pp.y);
        }

        Vector3 PinWorld(PinState p)
        {
            var basePos = p.Paddock >= 0 && p.Kind != PinKinds.Flood && p.Kind != PinKinds.Escape ? World.PaddockCenter(p.Paddock) : World.NodePos(p.Node);
            int k = Array.IndexOf(PinKinds.All, p.Kind);
            return basePos + new Vector3(-9f + (k % 3) * 9f, 14f, (k / 3) * 6f);
        }

        void PlaceMarkers()
        {
            if (World == null) return;
            foreach (var p in Sim.S.Pins)
            {
                if (!pinMarkers.TryGetValue(p.Id, out var b)) continue;
                Place(b, PinWorld(p));
                double left = p.Deadline - Sim.S.Minute;
                string text = (PinIcon.TryGetValue(p.Kind, out var ic) ? ic : "!") + (p.Ranger >= 0 ? " ⏵" : Sim.S.Tutorial ? "" : " " + (long)Math.Max(0, Math.Ceiling(left)) + "′");
                UiKit.SetText(b, text);
                b.EnableInClassList("rr-marker--urgent", p.Ranger < 0 && left < 20 && !Sim.S.Tutorial);
                b.EnableInClassList("rr-marker--taken", p.Ranger >= 0);
                b.EnableInClassList("rr-marker--selected", Act.SelectedPin == p.Id);
            }
            foreach (var kv in slotMarkers) Place(kv.Value, World.NodePos(kv.Key) + Vector3.up * 6f);
            Place(gateBtn, World.GatePos + Vector3.up * 16f);
            Place(tramBtn, World.StationPos + new Vector3(0, 14f, 0));
            Place(fedBtn, World.PaddockCenter(0) + Vector3.up * 24f);
        }

        void Relocalise()
        {
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
                "badge" => badge,
                "gate" => gateBtn,
                "tram" => tramBtn,
                "fed" => fedBtn,
                _ => null,
            };
            if (e == null && target.StartsWith("pin:"))
            {
                string kind = target.Substring(4);
                foreach (var p in Sim.S.Pins) if (p.Kind == kind && pinMarkers.TryGetValue(p.Id, out var b)) { e = b; break; }
            }
            if (e == null && sheet != null && sheet.IsOpen) e = sheet.Panel.Q<VisualElement>("Target_" + target.Replace(':', '_'));
            if (e == null || e.panel == null || e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden) return null;
            return e.worldBound;
        }
    }
}
