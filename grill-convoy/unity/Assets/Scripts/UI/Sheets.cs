// Grill Convoy — bottom sheets (CoddyKit BottomSheet: content-sized, finger scroll with inertia, no visible scrollbar,
// one action pill per card, fleet §16 / §17). Every sheet is rebuilt from the sim when something changed, never under a
// finger. Guide targets inside a sheet are named "Target_<target>" so the spotlight can find them.
//   map       the calendar (weather today / tomorrow, match days), the pitches (forecast, medal, permit), unlock the next
//             pitch, buy truck #2
//   pitch     one pitch: forecast, crowd, reputation, rival, which truck goes there
//   trucks    the fleet list (status, pitch, line, stock) -> truck
//   truck     status + live verbs (All hands, Extinguish, Clean, Propane, Restock, Recall, counter-offer, rewarded helpers),
//             today's pitch, the pack list, crew, menu board + fixings tier, the LAYOUT EDITOR (tap a station, tap a tile;
//             auto-arrange, undo, presets, sell, buy stations, body extension), Dispatcher rules
//   kitchen   the commissary: stock, prep batches (queue + timer), Quick prep (rewarded), par levels
//   research  three branches, one project at a time, ≤ 20 min
//   shop      offers, ad-free & packs, upgrades, looks, cash (store prices only), Restore
//   settings  language, music / effects, graphics tier, career slots, replay the guide, privacy options (UMP) + policy link
//   report    the day report: stars, numbers, Fame, Next day   ·   offline  the welcome-back card (×2 rewarded)
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using GrillConvoy.Game;
using GrillConvoy.Sim;

namespace GrillConvoy.UI
{
    public sealed partial class GameScreen
    {
        public string SheetKind => sheet != null && sheet.IsOpen ? sheetKind : "";
        /// <summary>Which truck-sheet section comes first ("" | crew | layout | pack) — the guide puts its target on top.</summary>
        public string TruckFocus = "";
        int layoutSel = -1;   // selected tile in the layout editor (-1 = none)

        public void OpenSheet(string kind, int arg = -1)
        {
            if (sheet != null && sheet.IsOpen && sheetKind == kind && sheetArg == arg) { FillSheet(); return; }
            CloseSheet();
            sheetKind = kind;
            sheetArg = arg;
            openTab = kind;
            layoutSel = -1;
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
                case "map": FillMap(); break;
                case "pitch": FillPitch(sheetArg); break;
                case "trucks": FillTrucks(); break;
                case "truck": FillTruck(sheetArg); break;
                case "kitchen": FillKitchen(); break;
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
        void Note(string text, string cls = "coddy-caption-muted") => Add(UiKit.Text(text, cls));

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
            r.AddToClassList("gc-row");
            r.Add(UiKit.Text(k, "gc-row__k"));
            var vl = UiKit.Text(v, "gc-row__v");
            if (cls != "") vl.AddToClassList(cls);
            r.Add(vl);
            Add(r);
            return r;
        }

        VisualElement Chips()
        {
            var c = new VisualElement();
            c.AddToClassList("gc-chips");
            Add(c);
            return c;
        }

        Button Chip(VisualElement row, string text, Action a, bool on, bool enabled = true, string target = null)
        {
            var b = UiKit.ActionPill(text, a, enabled, on ? "primary" : "tonal");
            if (target != null) b.name = "Target_" + target;
            row.Add(b);
            return b;
        }

        string BuyLabel(BuyInfo b) => b.Ok || b.Reason == "cash" ? Money(b.Cost) : L.T("err." + b.Reason);
        void Run(string a) => main.Run(a);

        void RewardCard(string placement, string key, int target = -1)
        {
            var o = main.Rewards?.Get(placement, target);
            if (o == null || !o.Show) return;
            string label = Simulation.RewardIsCash(placement)
                ? L.F(o.Video ? "reward.video_cash" : "reward.free_cash", "cash", Money(o.Amount))
                : L.T(o.Video ? "reward.video" : "reward.claim");
            Card(L.T("reward." + key + ".t"), o.Video ? L.T("reward." + key + ".b") : L.T("reward.free.b"), label, () => main.ClaimReward(placement, target), true, "tonal");
        }

        string PitchName(int node) => node >= 0 ? L.T("place." + Sim.Gd.Nodes[node].Id) : L.T("truck.no_pitch");

        // ------------------------------------------------------------------ map / pitches

        void FillMap()
        {
            var s = Sim.S;
            Title(L.T("tab.map"), L.F("map.sub", "fame", Fmt.Rating(s.Fame)));
            Section(L.T("map.calendar"));
            KV(L.T("map.today"), L.T("weather." + s.Weather) + (Sim.MatchDay(s.Day) ? " · " + L.T("map.match") : ""));
            KV(L.T("map.tomorrow"), L.T("weather." + s.Forecast) + (Sim.MatchDay(s.Day + 1) ? " · " + L.T("map.match") : ""));
            if (Sim.Weekend) Note(L.T("map.weekend"));
            RewardCard("daily_bonus", "daily");
            Section(L.T("map.pitches"));
            foreach (int p in Sim.Gd.Pitches)
            {
                int node = p;
                var n = Sim.Gd.Nodes[p];
                if (Sim.PitchOpen(p))
                    Card(L.T("place." + n.Id) + " · " + L.T("medal." + Sim.Medal(p)),
                        L.F("pitch.forecast", "n", (long)Math.Round(Sim.ForecastCustomers(p))) + " · " + L.F("pitch.permit", "cost", Money(n.Permit)),
                        L.T("common.open"), () => Run("select_pitch:" + n.Id), true, "tonal");
                else
                {
                    var dd = Sim.Gd.District(n.District);
                    var b = Sim.CanBuy("pitch:" + dd.Id);
                    if (b.Reason == "locked") { Card(L.T("place." + n.Id), L.T("pitch.later"), null, null, false); continue; }
                    Card(L.T("place." + n.Id), L.F("pitch.needs", "r", (long)dd.Fame) + " · " + L.T("pitch." + n.Id + ".d"), b.Reason == "fame" ? L.T("err.fame") : L.F("pitch.unlock", "cost", Money(dd.Cost)),
                        () => Run("buy:pitch:" + dd.Id), b.Ok);
                }
            }
            TruckShop();
        }

        void TruckShop()
        {
            foreach (var type in Sim.Gd.VehicleOrder)
            {
                var b = Sim.CanBuy("truck:" + type);
                if (b.Reason == "invalid" || b.Reason == "max") continue;
                string tt = type;
                Card(L.T("truck." + type), L.T("truck." + type + ".d"), b.Reason == "chapter" ? L.T("err.chapter") : BuyLabel(b), () => Run("buy:truck:" + tt), b.Ok);
            }
        }

        void FillPitch(int node)
        {
            if (node < 0 || node >= Sim.Gd.Nodes.Count || !Sim.Gd.Nodes[node].IsPitch) { CloseSheet(); return; }
            var n = Sim.Gd.Nodes[node];
            bool open = Sim.PitchOpen(node);
            Title(L.T("place." + n.Id), open ? L.T("medal." + Sim.Medal(node)) + " · " + L.F("pitch.rep", "r", (long)Math.Round(Sim.PitchState(node).Rep)) : L.T("common.locked"));
            Note(L.T("pitch." + n.Id + ".d"), "coddy-body");
            KV(L.T("pitch.today"), L.F("pitch.forecast", "n", (long)Math.Round(Sim.ForecastCustomers(node))) + " · " + L.T("weather." + Sim.S.Weather));
            KV(L.T("pitch.tomorrow"), L.F("pitch.forecast", "n", (long)Math.Round(Sim.ForecastCustomers(node, true))) + " · " + L.T("weather." + Sim.S.Forecast));
            KV(L.T("pitch.permit_k"), Money(n.Permit));
            KV(L.T("pitch.crowd"), L.T("curve." + n.Curve));
            if (n.Match) Note(L.T(Sim.MatchDay(Sim.S.Day) ? "pitch.match_today" : Sim.MatchDay(Sim.S.Day + 1) ? "pitch.match_tomorrow" : "pitch.match_every"));
            if (Sim.RivalMult(node, null) < 1.0) Note(L.T("pitch.rival"), "coddy-body");
            if (!open)
            {
                var dd = Sim.Gd.District(n.District);
                var b = Sim.CanBuy("pitch:" + dd.Id);
                Card(L.F("pitch.unlock", "cost", Money(dd.Cost)), L.F("pitch.needs", "r", (long)dd.Fame), b.Reason == "fame" ? L.T("err.fame") : b.Reason == "locked" ? L.T("err.locked") : Money(dd.Cost),
                    () => Run("buy:pitch:" + dd.Id), b.Ok);
                return;
            }
            Section(L.T("pitch.trucks"));
            for (int i = 0; i < Sim.S.Trucks.Count; i++)
            {
                int ti = i;
                var t = Sim.S.Trucks[i];
                bool here = t.Pitch == node;
                Card(L.F("truck.name", "n", i + 1), here ? L.T("pitch.assigned") : L.F("pitch.now_at", "name", PitchName(t.Pitch)),
                    here ? L.T("common.ok") : L.T("pitch.send_here"), () => Run("pitch:" + ti + ":" + n.Id), !here, here ? "tonal" : "primary");
            }
        }

        // ------------------------------------------------------------------ trucks

        void FillTrucks()
        {
            Title(L.T("tab.trucks"), L.F("trucks.sub", "n", Sim.S.Trucks.Count));
            for (int i = 0; i < Sim.S.Trucks.Count; i++)
            {
                int ti = i;
                var t = Sim.S.Trucks[i];
                string sub = L.T("truck.status." + t.Status) + " · " + PitchName(t.Pitch) + " · " + L.F("truck.stock", "p", t.Load[Stock.Patty], "b", t.Load[Stock.Bun]);
                Card(L.F("truck.name", "n", i + 1) + " · " + L.T("truck." + t.Type), sub, L.T("common.open"), () => Run("select_truck:" + ti), true, "tonal");
            }
            TruckShop();
        }

        void FillTruck(int ti)
        {
            if (ti < 0 || ti >= Sim.S.Trucks.Count) { CloseSheet(); return; }
            var t = Sim.S.Trucks[ti];
            var line = Sim.Line(t);
            Title(L.F("truck.name", "n", ti + 1) + " · " + L.T("truck." + t.Type), L.T("truck.status." + t.Status) + " · " + PitchName(t.Open ? t.AtNode : t.Pitch));
            var order = new List<string> { "status", "pack", "crew", "menu", "layout" };
            if (TruckFocus != "" && order.Remove(TruckFocus)) order.Insert(0, TruckFocus);
            foreach (var part in order)
                switch (part)
                {
                    case "status": TruckOverview(ti, t, line); break;
                    case "pack": TruckPack(ti, t); break;
                    case "crew": TruckCrew(ti, t); break;
                    case "menu": TruckMenu(ti, t); break;
                    case "layout": TruckLayout(ti, t, line); break;
                }
        }

        void TruckOverview(int ti, TruckState t, LineStats line)
        {
            Section(L.T("truck.line"), L.F("truck.per_hour", "n", (long)Math.Round(line.Throughput * 60)));
            Note(line.Complete ? L.F("truck.bottleneck", "stage", L.T("station." + line.Bottleneck)) + " · " + L.F("truck.walk", "n", line.WalkTiles) : L.T("truck.incomplete"), "coddy-body");
            if (t.Open)
            {
                KV(L.T("truck.queue"), t.Queue.Count + " / " + Sim.QueueCap(t));
                KV(L.T("truck.today"), L.F("truck.served", "n", t.Served, "w", t.Walked + t.SoldOut));
                if (t.Flaring) Card(L.T("crisis.flare.t"), L.T("crisis.flare.b"), L.T("bar.extinguish"), () => Run("extinguish:" + ti), true, "primary");
                if (t.Propane <= Sim.Gd.Svc.PropaneLow) Card(L.T("crisis.propane.t"), L.T("crisis.propane.b"), Sim.S.PropaneKits > 0 ? L.T("bar.propane_kit") : L.F("bar.propane", "cost", Money(Sim.Gd.Svc.PropaneSwap)), () => Run("propane:" + ti), true, "primary");
                if (Sim.RivalMult(t.AtNode, null) < 1.0 && !t.Promo) Card(L.T("crisis.rival.t"), L.T("truck.promo.d"), L.T("truck.promo"), () => Run("promo:" + ti), true, "tonal");
                RewardCard("crisis_rescue", "rescue", ti);
                RewardCard("extra_runner", "runner", ti);
                var acts = Chips();
                Chip(acts, t.AllHandsReady <= Sim.S.Minute ? L.T("bar.allhands") : L.F("bar.allhands_wait", "min", (long)Math.Ceiling(t.AllHandsReady - Sim.S.Minute)), () => Run("allhands:" + ti), true, t.AllHandsReady <= Sim.S.Minute);
                Chip(acts, L.F("truck.clean", "pct", Fmt.Pct(Math.Min(1, t.Grease / Math.Max(1, Sim.Gd.Svc.GreaseSafe)), L.Lang)), () => Run("clean:" + ti), false, t.Grease >= 1);
                Chip(acts, L.T("bar.restock"), () => Run("restock:" + ti), false);
                Chip(acts, L.T("truck.recall"), () => Run("recall:" + ti), false);
            }
            else if (t.Status == TruckStatus.Idle && Sim.Phase == Phases.Live && t.Pitch >= 0)
                Card(L.F("truck.send", "name", PitchName(t.Pitch)), L.F("truck.drive", "min", (long)Math.Round(Sim.DriveMinutes(t, Sim.Gd.DepotNode, t.Pitch))), L.T("truck.go"), () => Run("send:" + ti));
            Section(L.T("truck.pitch"));
            var pr = Chips();
            foreach (int p in Sim.Gd.Pitches)
            {
                if (!Sim.PitchOpen(p)) continue;
                string id = Sim.Gd.Nodes[p].Id;
                Chip(pr, L.T("place." + id), () => Run("pitch:" + ti + ":" + id), t.Pitch == p);
            }
            if (Sim.Has("dispatcher"))
                Card(L.T("truck.auto"), L.F("truck.auto.d", "n", Sim.AutoCount, "m", Sim.RuleSlots), L.T(t.Auto ? "settings.on" : "settings.off"), () => Run("auto:" + ti + ":" + (t.Auto ? 0 : 1)), true, t.Auto ? "primary" : "tonal");
        }

        void TruckPack(int ti, TruckState t)
        {
            bool home = Sim.AtCommissary(t);
            Section(L.T("pack.title"), home ? "" : L.T("pack.away"));
            for (int i = 0; i < Stock.N; i++)
            {
                string item = Stock.Ids[i];
                var r = new VisualElement();
                r.AddToClassList("gc-pack");
                r.Add(UiKit.Text(L.F("pack.row", "name", L.T("stock." + item), "n", t.Load[i], "cap", Sim.TruckCap(t, i)) + (home ? " · " + L.F("pack.fridge", "n", Sim.S.Commissary.Stock[i]) : ""), "gc-pack__label"));
                var minus = UiKit.ActionPill("−10", () => Run("load:" + ti + ":" + item + ":-10"), home && t.Load[i] > 0, "tonal");
                var plus = UiKit.ActionPill("+20", () => Run("load:" + ti + ":" + item + ":20"), home && Sim.S.Commissary.Stock[i] > 0 && t.Load[i] < Sim.TruckCap(t, i));
                plus.name = "Target_load_" + item;
                r.Add(minus);
                r.Add(plus);
                Add(r);
            }
            if (home)
            {
                var sug = Sim.SuggestPack(ti);
                Note(L.F("pack.suggest", "p", sug[Stock.Patty], "b", sug[Stock.Bun], "s", sug[Stock.Side]));
                var row = Chips();
                Chip(row, L.T("pack.apply_suggest"), () => Run("suggest_pack:" + ti), false);
                Chip(row, L.T("pack.repack"), () => Run("repack:" + ti), false);
            }
            RewardCard("bonus_patties", "patties", ti);
        }

        void TruckCrew(int ti, TruckState t)
        {
            var v = Sim.VehicleOf(t);
            Section(L.T("crew.title"), L.F("crew.count", "n", t.Crew.Count, "m", v.Crew));
            Note(L.F("crew.wage", "cost", Money(Sim.Gd.Wage)));
            for (int i = 0; i < t.Crew.Count; i++)
            {
                int ci = i;
                string role = t.Crew[i];
                string next = Roles.All[(Array.IndexOf(Roles.All, role) + 1) % Roles.All.Length];
                Card(L.T("role." + role), L.T("role." + role + ".d"), L.F("crew.switch", "role", L.T("role." + next)), () => Run("role:" + ti + ":" + ci + ":" + next), true, "tonal");
            }
            if (t.Crew.Count < v.Crew)
            {
                var row = Chips();
                foreach (var role in Roles.All)
                {
                    var b = Sim.CanBuy("hire:" + ti + ":" + role);
                    string rr = role;
                    Chip(row, L.F("crew.hire", "role", L.T("role." + role), "cost", Money(b.Cost > 0 ? b.Cost : Sim.Gd.HirePrice)), () => Run("buy:hire:" + ti + ":" + rr), role == Roles.Runner, b.Ok, role == Roles.Runner ? "hire" : null);
                }
            }
            if (t.Crew.Count > 1) Card(L.T("crew.release"), L.T("crew.release.d"), L.T("crew.release_btn"), () => Run("release:" + ti + ":" + (t.Crew.Count - 1)), true, "tonal");
        }

        void TruckMenu(int ti, TruckState t)
        {
            Section(L.T("menu.title"), L.F("menu.slots", "n", t.Menu.Count, "m", Sim.MenuSlots));
            Note(L.T("menu.help"));
            var row = Chips();
            foreach (var c in Sim.Gd.Cards)
            {
                string id = c.Id;
                bool on = t.Menu.Contains(id);
                bool avail = Sim.CardAvailable(c);
                Chip(row, L.T("card." + id) + " " + Money(c.Price), () => Run("card:" + ti + ":" + id), on, avail || on);
            }
            Section(L.T("tier.title"));
            var tiers = Chips();
            for (int k = 0; k < 3; k++)
            {
                int kk = k;
                Chip(tiers, L.T("tier." + k), () => Run("tier:" + ti + ":" + kk), t.Tier == k, k < 2 || Sim.Has("premium_tier"));
            }
            Note(L.T("tier." + t.Tier + ".d"));
        }

        /// <summary>The guide's step 9: the toaster tile, then (once picked) a free tile next to the bench.</summary>
        int GuideLayoutTile(TruckState t)
        {
            int w = Sim.GridW(t), h = Sim.GridH(t);
            int toaster = t.Layout.FindIndex(s => s.Type == Stations.Toaster);
            if (toaster < 0) return -1;
            int toasterTile = t.Layout[toaster].Y * w + t.Layout[toaster].X;
            if (layoutSel != toasterTile) return toasterTile;
            foreach (var s in t.Layout)
            {
                if (s.Type != Stations.Assembly) continue;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int x = s.X + dx, y = s.Y + dy;
                    if (x >= 0 && y >= 0 && x < w && y < h && Sim.TileIndexAt(t, x, y) < 0) return y * w + x;
                }
            }
            return -1;
        }

        void TruckLayout(int ti, TruckState t, LineStats line)
        {
            int w = Sim.GridW(t), h = Sim.GridH(t);
            Section(L.T("layout.title"), w + "×" + h);
            Note(L.T(layoutSel >= 0 ? "layout.help_place" : "layout.help_pick"));
            int guide = main.Coach.Active && main.Coach.Target == "layout" && ti == 0 ? GuideLayoutTile(t) : -1;
            var grid = new VisualElement();
            grid.AddToClassList("gc-grid");
            for (int y = 0; y < h; y++)
            {
                var row = new VisualElement();
                row.AddToClassList("gc-grid__row");
                for (int x = 0; x < w; x++)
                {
                    int tile = y * w + x;
                    int si = Sim.TileIndexAt(t, x, y);
                    string type = si >= 0 ? t.Layout[si].Type : "";
                    var b = new Button(() => OnTile(ti, tile)) { text = si >= 0 ? L.T("station." + type) : "" };
                    b.name = tile == guide ? "Target_layout" : "Tile_" + tile;
                    b.focusable = false;
                    b.AddToClassList("gc-tile");
                    if (si >= 0) b.AddToClassList("gc-tile--" + type);
                    if (tile == layoutSel) b.AddToClassList("gc-tile--selected");
                    if (y == 0) b.AddToClassList("gc-tile--front");
                    row.Add(b);
                }
                grid.Add(row);
            }
            Add(grid);
            Note(L.T("layout.front"));
            var acts = Chips();
            Chip(acts, L.T("layout.auto"), () => Run("auto_layout:" + ti), false);
            Chip(acts, L.T("layout.undo"), () => Run("undo:" + ti), false, t.Undo != null);
            if (layoutSel >= 0 && Sim.TileIndexAt(t, layoutSel % w, layoutSel / w) >= 0)
            {
                int sel = layoutSel;
                Chip(acts, L.T("layout.sell"), () => { layoutSel = -1; Run("remove:" + ti + ":" + sel); }, false);
            }
            for (int p = 0; p < Sim.PresetSlots; p++)
            {
                int slot = p;
                Chip(acts, L.F("layout.save", "n", p + 1), () => Run("preset_save:" + ti + ":" + slot), false);
                Chip(acts, L.F("layout.load", "n", p + 1), () => Run("preset_load:" + ti + ":" + slot), false, slot < t.Presets.Count && t.Presets[slot].Count > 0);
            }
            Section(L.T("layout.buy"));
            foreach (var st in Stations.All)
            {
                var b = Sim.CanBuy("station:" + ti + ":" + st);
                if (b.Reason == "max") continue;
                string s2 = st;
                Card(L.T("station." + st), L.T("station." + st + ".d"), b.Reason == "research" ? L.T("err.research") : BuyLabel(b), () => Run("buy:station:" + ti + ":" + s2), b.Ok);
            }
            var ext = Sim.CanBuy("extend:" + ti);
            if (ext.Reason != "owned") Card(L.T("layout.extend"), L.T("layout.extend.d"), ext.Reason == "research" ? L.T("err.research") : BuyLabel(ext), () => Run("buy:extend:" + ti), ext.Ok);
        }

        void OnTile(int ti, int tile)
        {
            var t = Sim.S.Trucks[ti];
            int w = Sim.GridW(t);
            bool occupied = Sim.TileIndexAt(t, tile % w, tile / w) >= 0;
            if (layoutSel < 0)
            {
                if (!occupied) return;
                if (main.Coach.Active && main.Coach.Target == "layout" && ti == 0 && tile != GuideLayoutTile(t)) { main.Coach.WrongTap(); return; }
                layoutSel = tile;
                FillSheet();
                return;
            }
            if (tile == layoutSel) { layoutSel = -1; FillSheet(); return; }
            int from = layoutSel;
            layoutSel = -1;
            double before = Sim.Line(t).Throughput;
            var r = main.Run("move:" + ti + ":" + from + ":" + tile);
            if (r.Ok)
            {
                double after = Sim.Line(t).Throughput;
                if (Math.Abs(after - before) > 1e-6) Toast(L.F(after > before ? "layout.faster" : "layout.slower", "pct", Fmt.Pct(Math.Abs(after / Math.Max(1e-6, before) - 1), L.Lang)), after > before ? ToastKind.Success : ToastKind.Info);
            }
            FillSheet();
        }

        // ------------------------------------------------------------------ kitchen

        void FillKitchen()
        {
            var c = Sim.S.Commissary;
            Title(L.T("kitchen.title"), L.F("kitchen.queue", "n", c.Queue.Count, "m", Simulation.MaxPrepQueue));
            Note(L.T("kitchen.help"));
            for (int i = 0; i < Stock.N; i++)
            {
                string item = Stock.Ids[i];
                string why = PrepWhy(i);
                Card(L.F("kitchen.stock", "name", L.T("stock." + item), "n", c.Stock[i], "cap", Sim.Gd.StockDefs[i].Cap),
                    L.F("kitchen.batch", "n", Sim.BatchSize(i), "min", (long)Math.Round(Sim.Gd.StockDefs[i].PrepMin / Sim.PrepSpeed)),
                    why == "" || why == "cash" ? L.F("kitchen.prep", "cost", Money(Sim.BatchCost(i))) : L.T("err." + why), () => Run("prep:" + item), why == "", "primary", "prep_" + item);
            }
            if (c.Queue.Count > 0)
            {
                Section(L.T("kitchen.in_progress"));
                for (int q = 0; q < c.Queue.Count; q++)
                    Note((q == 0 ? L.F("kitchen.now", "min", (long)Math.Ceiling(c.PrepLeft / Sim.PrepSpeed)) + " · " : "") + L.F("kitchen.batch_of", "n", Sim.BatchSize(Math.Max(0, Stock.Index(c.Queue[q]))), "name", L.T("stock." + c.Queue[q])), "coddy-body");
                RewardCard("quick_prep", "prep");
            }
            Note(L.T("kitchen.night"));
            if (Sim.Has("par_levels"))
            {
                Section(L.T("kitchen.par"));
                for (int i = 0; i < Stock.N; i++)
                {
                    string item = Stock.Ids[i];
                    int step = Sim.Gd.StockDefs[i].Batch;
                    var r = new VisualElement();
                    r.AddToClassList("gc-pack");
                    r.Add(UiKit.Text(L.F("kitchen.par_row", "name", L.T("stock." + item), "n", c.Par[i]), "gc-pack__label"));
                    r.Add(UiKit.ActionPill("−", () => Run("par:" + item + ":" + (c.Par[Stock.Index(item)] - step)), true, "tonal"));
                    r.Add(UiKit.ActionPill("+", () => Run("par:" + item + ":" + (c.Par[Stock.Index(item)] + step))));
                    Add(r);
                }
            }
        }

        string PrepWhy(int item)
        {
            var c = Sim.S.Commissary;
            if (Sim.Phase == Phases.Report) return "report";
            if (c.Queue.Count >= Simulation.MaxPrepQueue) return "queue_full";
            if (c.Stock[item] + Sim.Queued(item) + Sim.BatchSize(item) > Sim.Gd.StockDefs[item].Cap) return "fridge_full";
            if (Sim.S.Cash < Sim.BatchCost(item)) return "cash";
            return "";
        }

        // ------------------------------------------------------------------ research

        void FillResearch()
        {
            string running = Sim.S.Researching;
            Title(L.T("tab.research"), running != "" ? L.T("research." + running + ".t") + " · " + L.F("research.left", "time", Fmt.Dur(Sim.S.ResearchLeftS, L)) : "");
            foreach (var branch in new[] { "menu", "kitchen", "ops" })
            {
                Section(L.T("research.branch." + branch));
                foreach (var r in Sim.Gd.Research)
                {
                    if (r.Branch != branch) continue;
                    string why = Sim.CanResearch(r.Id);
                    string id = r.Id;
                    string action = why == "owned" ? L.T("research.done") : why == "running" ? L.F("research.left", "time", Fmt.Dur(Sim.S.ResearchLeftS, L))
                        : why == "requires" ? L.F("research.requires", "name", L.T("research." + r.Requires[0] + ".t"))
                        : why == "locked" ? L.T("err.locked") : L.F("research.start", "cost", Money(r.Cost));
                    Card(L.T("research." + id + ".t"), L.T("research." + id + ".d") + " · " + Fmt.Dur(r.Minutes * 60, L), action, () => Run("research:" + id), why == "", "primary");
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
            var langs = Chips();
            for (int i = 0; i < Loc.Languages.Length; i++)
            {
                string code = Loc.Languages[i];
                Chip(langs, Loc.NativeNames[i], () => main.SetLanguage(code), L.Lang == code);
            }
            Section(L.T("settings.sound"));
            Card(L.T("settings.music"), "", L.T(st.Music ? "settings.on" : "settings.off"), () => main.SetAudio(!st.Music, st.Effects), true, st.Music ? "primary" : "tonal");
            Card(L.T("settings.effects"), "", L.T(st.Effects ? "settings.on" : "settings.off"), () => main.SetAudio(st.Music, !st.Effects), true, st.Effects ? "primary" : "tonal");
            Section(L.T("settings.graphics"));
            var tiers = Chips();
            foreach (var t in new[] { Tier.Low, Tier.Medium, Tier.High })
            {
                var tt = t;
                Chip(tiers, L.T("graphics." + t.ToString().ToLowerInvariant()), () => main.SetTier(tt), main.CurrentTier == t);
            }
            Section(L.T("settings.looks"));
            var looks = Chips();
            Chip(looks, L.T("looks.default"), () => main.SetLivery(""), st.Livery == "");
            foreach (var lv in new[] { "gold_trim", "livery_retro", "livery_neon" })
            {
                string l2 = lv;
                if (main.Owns(lv)) Chip(looks, L.T("looks." + lv), () => main.SetLivery(l2), st.Livery == lv);
            }
            Note(L.T("looks.help"));
            int owned = main.SlotCount;
            Section(L.T("settings.slots"), L.F("settings.slots_n", "n", owned));
            var slots = Chips();
            for (int i = 0; i < Math.Max(owned, 1); i++)
            {
                int slot = i;
                Chip(slots, L.F(main.Session.SlotUsed(i) || i == st.Slot ? "settings.slot" : "settings.slot_new", "n", i + 1), () => main.SwitchSlot(slot), st.Slot == i);
            }
            if (owned <= 1) Note(L.T("settings.slots_more"));
            Section(L.T("settings.help"));
            Card(L.T("settings.guide"), "", L.T("settings.replay"), main.ReplayGuide, true, "tonal");
            Section(L.T("settings.privacy"));
            Card(L.T("settings.privacy_options"), "", L.T("common.open"), main.ShowPrivacyOptions, true, "tonal");
            Card(L.T("settings.policy"), "", L.T("common.open"), () => Application.OpenURL(GrillConvoy.App.GrillGameConfig.PrivacyUrl), true, "tonal");
            Card(L.T("settings.restore"), "", L.T("common.ok"), () => main.Shop?.Restore(), true, "tonal");
        }

        // ------------------------------------------------------------------ report / offline

        void FillReport(DayStats d, bool withNext)
        {
            if (d == null) { Title(L.T("phase.report")); return; }
            Title(L.F("report.title", "n", d.Day), L.F("report.fame", "r", Fmt.Rating(Sim.S.Fame)) + " · " + L.T("weather." + d.Weather));
            var stars = new VisualElement();
            stars.AddToClassList("gc-report__stars");
            void Star(string key, int n)
            {
                var c = new VisualElement();
                c.AddToClassList("gc-report__star");
                c.Add(UiKit.Text(new string('★', n) + new string('☆', 3 - n), "gc-report__star-value"));
                c.Add(UiKit.Text(L.T(key), "coddy-caption-muted"));
                stars.Add(c);
            }
            Star("report.service", d.StarService);
            Star("report.stock", d.StarStock);
            Star("report.speed", d.StarSpeed);
            Add(stars);
            KV(L.T("report.served"), Fmt.Int(d.Served, L.Lang) + " · " + L.F("report.burgers", "n", d.Items));
            KV(L.T("report.lost"), Fmt.Int(d.Walked + d.SoldOut, L.Lang), d.LostRate > 0.12 ? "gc-row__v--bad" : "");
            KV(L.T("report.wait"), L.F("report.minutes", "n", d.AvgWait.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
            KV(L.T("report.sellout"), L.F("report.minutes", "n", ((long)Math.Round(d.SelloutMin)).ToString()), d.SelloutMin > 30 ? "gc-row__v--bad" : "");
            KV(L.T("report.waste"), Fmt.Int(d.Waste, L.Lang) + " · " + Money(d.WasteValue), d.Waste > 40 ? "gc-row__v--bad" : "");
            KV(L.T("report.sales"), Money(d.Sales));
            if (d.Bonus > 0) KV(L.T("report.bonus"), Money(d.Bonus));
            KV(L.T("report.ingredients"), "−" + Money(d.Ingredients + d.Fixings));
            KV(L.T("report.wages"), "−" + Money(d.Wages));
            KV(L.T("report.permits"), "−" + Money(d.Permits));
            KV(L.T("report.fuel"), "−" + Money(d.Fuel + d.Propane));
            KV(L.T("report.net"), Money(d.Net), d.Net >= 0 ? "gc-row__v--good" : "gc-row__v--bad");
            Note(L.T(Hint(d)), "coddy-body");
            if (withNext && Sim.Phase == Phases.Report)
            {
                var next = UiKit.PrimaryButton(L.T("hud.next_day"), () => main.Run("next_day"));
                next.name = "Target_next_day";
                sheet.Actions.Add(next);
            }
        }

        /// <summary>Every metric points to a decision (plan §4 loop 3).</summary>
        static string Hint(DayStats d)
        {
            if (d.SelloutMin > 30) return "report.hint_sellout";
            if (d.Arrivals > 0 && (double)d.Walked / d.Arrivals > 0.12) return "report.hint_line";
            if (d.Waste > 40) return "report.hint_waste";
            if (d.AvgWait > 6) return "report.hint_wait";
            return "report.hint_good";
        }

        void FillOffline()
        {
            var o = main.LastOffline;
            Title(L.T("offline.title"));
            if (o == null) return;
            Note(L.F("offline.body", "cash", Money(o.Amount), "time", Fmt.Dur(o.Seconds, L)), "coddy-body");
            if (o.Capped) Note(L.F("offline.capped", "h", (long)Math.Round(o.Hours)));
            RewardCard("offline_x2", "offline");
            sheet.Actions.Add(UiKit.PrimaryButton(L.T("common.ok"), CloseSheet));
        }
    }
}
