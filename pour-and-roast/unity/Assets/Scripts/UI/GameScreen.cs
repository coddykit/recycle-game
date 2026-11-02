// Pour & Roast — the cafe screen (UI Toolkit, CoddyKit theme). Layout (portrait):
//   top    HUD: day + phase, clock, cash, reputation stars (-> Review Wall); the forecast chip (-> forecast sheet), crisis
//          chips, speed buttons; the TICKET RAIL (one card per open ticket in working order, ≥ 48 dp, a patience bar, a
//          pickup clock on pre-orders, a flag) + the Batch toggle; the main action (Open in the morning, the day report)
//   middle the diorama + station markers (espresso heads busy, the grinder's tune gauge — hold to dial in, the milk
//          fridge, the pickup shelf — tap to hand over, pastry case, ice, the roaster's progress — tap to drop)
//   bottom the ticket bar (while a ticket is selected: Brew next · Priority · Cancel) and the tab bar:
//          Bar · Menu · Roastery · Grow · Shop · ⚙ — each opens a bottom sheet (Sheets.cs)
// Text is set only when it changed (UiKit.SetText, LESSONS A10); lists are rebuilt on events, never under a finger
// (UiKit.Held, fleet §16); markers move at 30 Hz (Low 10 Hz) with style.translate (no layout pass). No scroll views on
// the HUD: the rail shows the first cards that fit and a "+n" chip (no horizontal scrollbar on a phone).
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using PourAndRoast.Game;
using PourAndRoast.Sim;
using PourAndRoast.World;

namespace PourAndRoast.UI
{
    public sealed partial class GameScreen
    {
        const int RailCards = 5;
        readonly VisualElement root;
        readonly RoastMain main;
        Loc L => main.Loc;
        Simulation Sim => main.Session.Sim;
        GameActions Act => main.Actions;
        CafeWorld World => main.world;

        // HUD
        Label dayLabel, clockLabel, cashLabel, ratingLabel, weatherLabel;
        VisualElement crisesRow, speedRow, hud, rail;
        Button mainAction, batchBtn, weatherChip;
        readonly List<Button> speedButtons = new List<Button>();
        readonly List<RailCard> railCards = new List<RailCard>();
        Label railMore;
        bool railDirty = true;
        // bottom
        VisualElement bottom, ticketBar, tabsRow;
        Label ticketTitle, ticketSub;
        Button brewBtn, flagBtn, cancelBtn, closeTicketBtn;
        readonly Dictionary<string, Button> tabs = new Dictionary<string, Button>();
        // markers
        VisualElement markerLayer;
        readonly Dictionary<string, Marker> markers = new Dictionary<string, Marker>();
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

        sealed class RailCard
        {
            public Button Root;
            public Label Items, Sub;
            public VisualElement Bar;
            public int Ticket = -1;
            public string LastItems = "", LastSub = "";
        }

        public GameScreen(VisualElement root, RoastMain main)
        {
            this.root = root;
            this.main = main;
            var uss = Resources.Load<StyleSheet>("ui/PourAndRoast");
            if (uss != null) root.styleSheets.Add(uss);
            root.AddToClassList("pr-root");
            BuildMarkers();
            BuildHud();
            BuildBottom();
            BuildCoach();
            L.Changed += Relocalise;
            Refresh(true);
        }

        public void MarkRailDirty() => railDirty = true;
        public void MarkSheetDirty() => sheetDirty = true;

        // ------------------------------------------------------------------ build

        void BuildHud()
        {
            hud = new VisualElement { name = "Hud" };
            hud.AddToClassList("pr-hud");
            hud.pickingMode = PickingMode.Ignore;
            var row = Row("pr-hud__row");
            var left = Row("pr-hud__row");
            dayLabel = ChipLabel(left, out _);
            clockLabel = ChipLabel(left, out _);
            row.Add(left);
            var right = Row("pr-hud__row");
            cashLabel = ChipLabel(right, out var cashChip);
            cashChip.AddToClassList("pr-chip--money");
            var ratingBtn = new Button(() => OpenSheet("reviews")) { name = "Rating" };
            ratingBtn.AddToClassList("pr-chip");
            ratingBtn.focusable = false;
            ratingLabel = UiKit.Text("", "pr-chip__label");
            ratingBtn.Add(ratingLabel);
            right.Add(ratingBtn);
            row.Add(right);
            hud.Add(row);

            var row2 = Row("pr-hud__row");
            var lr = Row("pr-hud__row");
            weatherChip = new Button(() => { if (main.Run("forecast").Ok) OpenSheet("forecast"); }) { name = "Forecast" };
            weatherChip.AddToClassList("pr-chip");
            weatherChip.focusable = false;
            weatherLabel = UiKit.Text("", "pr-chip__label");
            weatherChip.Add(weatherLabel);
            lr.Add(weatherChip);
            crisesRow = Row("pr-crises");
            lr.Add(crisesRow);
            row2.Add(lr);
            speedRow = Row("pr-speed");
            for (int s = 1; s <= 4; s++)
            {
                int sp = s;
                var b = new Button(() => OnSpeed(sp)) { text = L.F("hud.speed", "n", sp) };
                b.AddToClassList("pr-speed__btn");
                b.focusable = false;
                speedButtons.Add(b);
                speedRow.Add(b);
            }
            row2.Add(speedRow);
            hud.Add(row2);

            // the ticket rail
            var railRow = Row("pr-rail-row");
            rail = Row("pr-rail");
            rail.name = "Rail";
            for (int i = 0; i < RailCards; i++)
            {
                int idx = i;
                var c = new RailCard { Root = new Button(() => OnRailCard(idx)) { name = "Ticket_" + i } };
                c.Root.AddToClassList("pr-ticket");
                c.Root.focusable = false;
                c.Items = UiKit.Text("", "pr-ticket__items");
                c.Sub = UiKit.Text("", "pr-ticket__sub");
                var barBg = new VisualElement { pickingMode = PickingMode.Ignore };
                barBg.AddToClassList("pr-ticket__bar-bg");
                c.Bar = new VisualElement { pickingMode = PickingMode.Ignore };
                c.Bar.AddToClassList("pr-ticket__bar");
                barBg.Add(c.Bar);
                c.Root.Add(c.Items);
                c.Root.Add(c.Sub);
                c.Root.Add(barBg);
                rail.Add(c.Root);
                railCards.Add(c);
            }
            railMore = UiKit.Text("", "pr-rail__more");
            rail.Add(railMore);
            railRow.Add(rail);
            batchBtn = new Button(() => main.Run("batch:" + (Sim.S.Batch ? "off" : "on"))) { name = "Batch" };
            batchBtn.AddToClassList("pr-batch");
            batchBtn.focusable = false;
            railRow.Add(batchBtn);
            hud.Add(railRow);

            mainAction = UiKit.PrimaryButton("", OnMainAction);
            mainAction.AddToClassList("pr-main-action");
            mainAction.name = "MainAction";
            hud.Add(mainAction);
            root.Add(hud);
        }

        void BuildBottom()
        {
            bottom = new VisualElement { name = "Bottom" };
            bottom.AddToClassList("pr-bottom");
            bottom.pickingMode = PickingMode.Ignore;

            ticketBar = new VisualElement { name = "TicketBar" };
            ticketBar.AddToClassList("pr-ticketbar");
            var top = Row("pr-ticketbar__row");
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.style.flexShrink = 1;
            ticketTitle = UiKit.Text("", "coddy-label-strong");
            ticketSub = UiKit.Text("", "pr-ticketbar__sub");
            texts.Add(ticketTitle);
            texts.Add(ticketSub);
            top.Add(texts);
            closeTicketBtn = UiKit.ActionPill("✕", () => main.Run("deselect"), true, "tonal");
            top.Add(closeTicketBtn);
            ticketBar.Add(top);
            var actions = Row("pr-ticketbar__row");
            brewBtn = UiKit.ActionPill("", () => main.Run("brew"));
            brewBtn.name = "Brew";
            flagBtn = UiKit.ActionPill("", () => { if (Act.SelectedTicket >= 0) main.Run("flag:" + Act.SelectedTicket); }, true, "tonal");
            flagBtn.name = "Flag";
            cancelBtn = UiKit.ActionPill("", () => { if (Act.SelectedTicket >= 0) main.Run("cancel:" + Act.SelectedTicket); }, true, "tonal");
            actions.Add(brewBtn);
            actions.Add(flagBtn);
            actions.Add(cancelBtn);
            ticketBar.Add(actions);
            bottom.Add(ticketBar);

            tabsRow = Row("pr-tabs");
            foreach (var id in new[] { "bar", "menu", "roastery", "grow", "shop", "settings" })
            {
                string tid = id;
                var b = new Button(() => OnTab(tid)) { name = "Tab_" + id };
                b.AddToClassList("pr-tab");
                b.focusable = false;
                tabs[id] = b;
                tabsRow.Add(b);
            }
            bottom.Add(tabsRow);
            root.Add(bottom);
        }

        static readonly string[] MarkerIds = { "espresso", "grinder", "fridge", "shelf", "pastry", "ice", "roaster" };

        void BuildMarkers()
        {
            markerLayer = new VisualElement { name = "Markers" };
            markerLayer.AddToClassList("pr-markers");
            markerLayer.pickingMode = PickingMode.Ignore;
            foreach (var id in MarkerIds)
            {
                string sid = id;
                var m = new Marker { Root = new Button(() => OnStation(sid)) { name = "Station_" + id } };
                m.Root.AddToClassList("pr-marker");
                m.Root.AddToClassList("pr-marker--" + id);
                m.Root.focusable = false;
                var gauge = new VisualElement { pickingMode = PickingMode.Ignore };
                gauge.AddToClassList("pr-marker__gauge");
                m.Fill = new VisualElement { pickingMode = PickingMode.Ignore };
                m.Fill.AddToClassList("pr-marker__fill");
                gauge.Add(m.Fill);
                m.Badge = UiKit.Text("", "pr-marker__badge");
                m.Root.Add(m.Badge);
                m.Root.Add(gauge);
                if (id == "grinder") HoldToDialIn(m.Root);
                markerLayer.Add(m.Root);
                markers[id] = m;
            }
            root.Add(markerLayer);
        }

        // the grinder: tap-and-hold (≈ 1 s, forgiving) dials it in; a jam is fixed by a hold too
        float holdStart = -1f;
        bool holdFired;

        void HoldToDialIn(VisualElement e)
        {
            e.RegisterCallback<PointerDownEvent>(_ => { holdStart = Time.unscaledTime; holdFired = false; e.AddToClassList("pr-marker--holding"); }, TrickleDown.TrickleDown);
            e.RegisterCallback<PointerUpEvent>(_ => { holdStart = -1f; e.RemoveFromClassList("pr-marker--holding"); });
            e.RegisterCallback<PointerLeaveEvent>(_ => { holdStart = -1f; e.RemoveFromClassList("pr-marker--holding"); });
        }

        void HoldTick()
        {
            if (holdStart < 0 || holdFired) return;
            if (Time.unscaledTime - holdStart < 1.0f) return;
            holdFired = true;
            main.Run(Sim.Jammed ? "repair" : "dial_in");
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
            chip.AddToClassList("pr-chip");
            var l = UiKit.Text("", "pr-chip__label");
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

        string ShortItems(TicketState t)
        {
            var parts = new List<string>();
            foreach (var it in t.Items)
            {
                var d = Sim.ItemDef(it);
                if (d == null) continue;
                string s = L.T("menu." + d.Id + ".short");
                if (it.Milk == 2) s += "·" + L.T("ticket.oat");
                if (it.Failed) s = "✕" + s;
                parts.Add(s);
            }
            return string.Join(" ", parts);
        }

        string LongItems(TicketState t)
        {
            var parts = new List<string>();
            foreach (var it in t.Items)
            {
                var d = Sim.ItemDef(it);
                if (d == null) continue;
                parts.Add(L.T("menu." + d.Id) + (it.Milk == 2 ? " (" + L.T("ticket.oat_long") + ")" : ""));
            }
            return string.Join(", ", parts);
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

        void OnRailCard(int i)
        {
            if (i >= railCards.Count || railCards[i].Ticket < 0) return;
            main.Run("select_ticket:" + railCards[i].Ticket);
        }

        void OnStation(string id)
        {
            switch (id)
            {
                case "espresso":
                    if (Act.SelectedTicket >= 0 || main.Coach.Active) main.Run("brew");
                    else OpenSheet("bar");
                    break;
                case "grinder":
                    if (!holdFired) { if (main.Coach.Active && main.Coach.Target != "grinder") main.Coach.WrongTap(); else if (!main.Coach.Active) OpenSheet("bar"); else Toast(L.T("hud.hold_grinder"), ToastKind.Info); }
                    holdFired = false;
                    break;
                case "shelf": main.Run("handover"); break;
                case "fridge": if (main.Run("fridge").Ok && !main.Coach.Active) OpenSheet("menu"); break;
                case "pastry": case "ice": if (main.Coach.Active) main.Coach.WrongTap(); else OpenSheet("menu"); break;
                case "roaster":
                    if (Sim.S.Roast != null && !main.Coach.Active) main.Run("drop");
                    else if (main.Coach.Active) main.Coach.WrongTap();
                    else OpenSheet("roastery");
                    break;
            }
        }

        /// <summary>A world tap (CafeWorld.Picked).</summary>
        public void WorldPick(WorldHit hit)
        {
            if (hit.Kind == "station") { OnStation(hit.Id); return; }
            if (main.Coach.Active) { main.Coach.WrongTap(); return; }
            if (hit.Kind == "staff") OpenSheet("bar");
            else if (Act.SelectedTicket >= 0) main.Run("deselect");
        }

        /// <summary>After an action ran (RoastMain): open the right sheet, refresh.</summary>
        public void AfterAction(string id, ActionResult r)
        {
            var a = id.Split(':');
            if (r.Ok)
            {
                switch (a[0])
                {
                    case "handover": Toast(L.F("toast.handover", "n", (long)r.Value), ToastKind.Success); break;
                    case "dial_in": Toast(L.T("toast.dialed"), ToastKind.Success); break;
                    case "repair": Toast(L.T("toast.repaired"), ToastKind.Success); break;
                    case "drop": Toast(L.T(r.Value > 0 ? "toast.drop_sweet" : "toast.drop"), r.Value > 0 ? ToastKind.Success : ToastKind.Info); break;
                    case "fridge":
                        Toast(L.F("toast.fridge", "whole", Fmt.Amount(Sim.S.Milk[0], "L", L.Lang), "oat", Fmt.Amount(Sim.S.Milk[1], "L", L.Lang)), ToastKind.Info);
                        break;
                    case "start_day": case "next_day": CloseSheet(); break;
                }
                railDirty = true;
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
            HoldTick();
            markerAcc += dt;
            if (markerAcc >= (low ? 0.1f : 1f / 30f)) { markerAcc = 0; PlaceMarkers(); }
            Refresh(false);
            CoachTick(dt);
            if (sheetDirty && sheet != null && sheet.IsOpen && !UiKit.Held(sheet.Body)) { sheetDirty = false; FillSheet(); }
        }

        float hudAcc, sheetAcc;

        public void Refresh(bool force)
        {
            hudAcc += Time.unscaledDeltaTime;
            sheetAcc += Time.unscaledDeltaTime;
            if (sheetAcc > 1f && (sheetKind == "roastery" || sheetKind == "bar") && sheet != null && sheet.IsOpen) { sheetAcc = 0; sheetDirty = true; }
            if (!force && hudAcc < 0.2f) return;   // 5 Hz HUD text
            hudAcc = 0;
            var s = Sim.S;
            UiKit.SetText(dayLabel, L.F("hud.day", "n", s.Day) + " · " + L.T("phase." + s.Phase));
            UiKit.SetText(clockLabel, Fmt.ClockMin(s.Minute));
            UiKit.SetText(cashLabel, Fmt.MoneyShort((long)Math.Round(s.Cash), L.Lang));
            UiKit.SetText(ratingLabel, "★ " + Fmt.Stars(s.Rating, L.Lang));
            string tomorrow = s.Forecast.Count > 1 ? s.Forecast[1] : Weathers.Sunny;
            UiKit.SetText(weatherLabel, L.T("weather." + Sim.Weather) + " → " + L.T("weather." + tomorrow));
            for (int i = 0; i < speedButtons.Count; i++)
            {
                speedButtons[i].EnableInClassList("pr-speed__btn--on", s.Speed == i + 1);
                speedButtons[i].style.display = Sim.Phase == Phases.Live && i < Math.Max(2, Sim.MaxSpeed) ? DisplayStyle.Flex : DisplayStyle.None;
            }
            bool showMain = Sim.Phase != Phases.Live && !(main.Coach.Active && main.Coach.Step >= 2);
            mainAction.style.display = showMain ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(mainAction, Sim.Phase == Phases.Plan ? L.T("hud.open") : L.T("phase.report"));
            UiKit.SetText(batchBtn, L.T(s.Batch ? "hud.batch_on" : "hud.batch_off"));
            batchBtn.EnableInClassList("pr-batch--on", s.Batch);
            // crisis chips
            var crises = Sim.ActiveCrises();
            string key = string.Join(",", crises);
            if ((string)crisesRow.userData != key)
            {
                crisesRow.userData = key;
                crisesRow.Clear();
                foreach (var c in crises)
                {
                    var chip = new Button(() => { if (!main.Coach.Active) OpenSheet("bar"); });
                    chip.focusable = false;
                    chip.AddToClassList("pr-chip");
                    chip.AddToClassList("pr-chip--warn");
                    chip.Add(UiKit.Text(L.T("crisis." + c + ".t"), "pr-chip__label"));
                    crisesRow.Add(chip);
                }
            }
            RefreshRail();
            RefreshTicketBar();
            foreach (var kv in tabs)
            {
                UiKit.SetText(kv.Value, kv.Key == "settings" ? "⚙" : L.T("tab." + kv.Key));
                kv.Value.EnableInClassList("pr-tab--on", openTab == kv.Key && sheet != null && sheet.IsOpen);
            }
        }

        void RefreshRail()
        {
            var order = Sim.WorkOrder();
            // ready tickets wait on the shelf; the rail shows what is still to make
            for (int i = 0; i < railCards.Count; i++)
            {
                var c = railCards[i];
                if (i >= order.Count) { c.Root.style.display = DisplayStyle.None; c.Ticket = -1; continue; }
                var t = order[i];
                c.Root.style.display = DisplayStyle.Flex;
                c.Ticket = t.Id;
                string items = ShortItems(t);
                if (items != c.LastItems || railDirty) { c.LastItems = items; UiKit.SetText(c.Items, items); }
                string sub = (t.Flag ? "⚑ " : "") + (t.Pre ? "⏰ " + Fmt.ClockMin(t.Due) : L.T("cust." + Sim.Gd.Customers[t.Cust].Id)) + (t.Dup ? " · " + L.T("ticket.dup") : "");
                if (sub != c.LastSub) { c.LastSub = sub; UiKit.SetText(c.Sub, sub); }
                double left = t.Pre ? 1 - Math.Max(0, Math.Min(1, (Sim.S.Minute - t.Arrived) / Math.Max(1, t.Due - t.Arrived)))
                    : 1 - Math.Max(0, Math.Min(1, (Sim.S.Minute - t.Arrived) / Math.Max(1, Sim.Gd.Customers[t.Cust].Patience)));
                c.Bar.style.width = Length.Percent((float)(left * 100));
                c.Bar.EnableInClassList("pr-ticket__bar--low", left < 0.3);
                c.Root.EnableInClassList("pr-ticket--selected", Act.SelectedTicket == t.Id);
                c.Root.EnableInClassList("pr-ticket--pre", t.Pre);
                bool working = false;
                foreach (var it in t.Items) if (it.Worker != -1) working = true;
                c.Root.EnableInClassList("pr-ticket--working", working);
            }
            int more = order.Count - railCards.Count;
            railMore.style.display = more > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (more > 0) UiKit.SetText(railMore, "+" + more);
            rail.EnableInClassList("pr-rail--empty", order.Count == 0);
            railDirty = false;
        }

        void RefreshTicketBar()
        {
            var t = Act.Selected;
            ticketBar.style.display = t != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (t == null) return;
            UiKit.SetText(ticketTitle, LongItems(t));
            string who = L.T("cust." + Sim.Gd.Customers[t.Cust].Id);
            UiKit.SetText(ticketSub, t.Pre ? L.F("ticket.pickup", "who", who, "time", Fmt.ClockMin(t.Due)) : L.F("ticket.waiting", "who", who, "min", (long)Math.Round(Sim.S.Minute - t.Arrived)));
            UiKit.SetText(brewBtn, L.T("ticket.brew"));
            UiKit.SetText(flagBtn, L.T(t.Flag ? "ticket.unflag" : "ticket.flag"));
            UiKit.SetText(cancelBtn, L.T("ticket.cancel"));
            bool started = false;
            foreach (var it in t.Items) if (it.Stage > 0 || it.Worker != -1) started = true;
            cancelBtn.style.display = !started && !main.Coach.Active ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void PlaceMarkers()
        {
            if (World == null || World.Sim == null) return;
            var s = Sim.S;
            var v = Sim.Venue;
            foreach (var kv in markers)
            {
                var m = kv.Value;
                string id = kv.Key;
                if (!World.HasStation(id)) { m.Root.style.display = DisplayStyle.None; continue; }
                var sp = World.ScreenOf(World.StationPos(id) + Vector3.up * (id == "roaster" ? 2.2f : 0.9f));
                if (sp == null) { m.Root.style.display = DisplayStyle.None; continue; }
                m.Root.style.display = DisplayStyle.Flex;
                var pp = ToPanel(sp.Value);
                m.Root.style.translate = new Translate(pp.x, pp.y);
                float pct = -1; string badge = "", cls = "";
                switch (id)
                {
                    case "espresso":
                        badge = L.F("marker.heads", "n", Sim.ShotsBusy, "m", v.Heads);
                        if (Act.SelectedTicket >= 0) { badge = L.T("marker.brew_here"); cls = "go"; }
                        break;
                    case "grinder":
                        pct = (float)(v.Tune / 100.0);
                        badge = Sim.Jammed ? L.T("marker.jam") : Sim.OffTune ? L.T("marker.bitter") : Fmt.Pct(v.Tune / 100.0, L.Lang);
                        cls = Sim.Jammed || Sim.OffTune ? "bad" : v.Tune < 75 ? "warn" : "";
                        break;
                    case "fridge":
                    {
                        double cap = Math.Max(1, s.MilkOrder[0] + s.MilkOrder[1]);
                        pct = (float)Math.Min(1, (s.Milk[0] + s.Milk[1]) / cap);
                        badge = Fmt.Amount(s.Milk[0] + s.Milk[1], "L", L.Lang);
                        cls = Sim.MilkLow ? "bad" : pct < 0.3f ? "warn" : "";
                        break;
                    }
                    case "shelf":
                    {
                        int n = Sim.ReadyCount;
                        badge = n > 0 ? L.F("marker.ready", "n", n) : L.T("marker.shelf");
                        cls = n > 0 ? "go" : "";
                        break;
                    }
                    case "pastry":
                    {
                        int n = 0; foreach (var x in s.Pastry) n += x;
                        badge = n.ToString();
                        cls = n == 0 ? "warn" : "";
                        break;
                    }
                    case "ice":
                        pct = (float)Math.Min(1, s.Ice / Math.Max(1, Sim.IceCap));
                        badge = Fmt.Amount(s.Ice, "kg", L.Lang);
                        cls = Sim.IceLow ? "bad" : "";
                        break;
                    case "roaster":
                        if (s.Roast != null) { pct = (float)s.Roast.Progress; badge = Sim.InSweetSpot ? L.T("marker.drop_now") : Fmt.Pct(s.Roast.Progress, L.Lang); cls = Sim.InSweetSpot ? "go" : ""; }
                        else badge = L.T("marker.roaster");
                        break;
                }
                if (badge != m.LastBadge) { m.LastBadge = badge; UiKit.SetText(m.Badge, badge); }
                m.Fill.parent.style.display = pct >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (pct >= 0 && Mathf.Abs(pct - m.LastPct) > 0.01f) { m.LastPct = pct; m.Fill.style.width = Length.Percent(pct * 100f); }
                m.Root.EnableInClassList("pr-marker--go", cls == "go");
                m.Root.EnableInClassList("pr-marker--warn", cls == "warn");
                m.Root.EnableInClassList("pr-marker--bad", cls == "bad");
            }
        }

        void Relocalise()
        {
            foreach (var m in markers.Values) m.LastBadge = "";
            foreach (var c in railCards) { c.LastItems = ""; c.LastSub = ""; }
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
                "open" => mainAction,
                "batch" => batchBtn,
                "flag" => flagBtn,
                "ticket:first" => railCards[0].Root,
                "station:espresso" => markers["espresso"].Root,
                "shelf" => markers["shelf"].Root,
                "fridge" => markers["fridge"].Root,
                "grinder" => markers["grinder"].Root,
                _ => null,
            };
            if (e == null && sheet != null && sheet.IsOpen) e = sheet.Panel.Q<VisualElement>("Target_" + target.Replace(':', '_'));
            if (e == null || e.panel == null || e.resolvedStyle.display == DisplayStyle.None) return null;
            return e.worldBound;
        }
    }
}
