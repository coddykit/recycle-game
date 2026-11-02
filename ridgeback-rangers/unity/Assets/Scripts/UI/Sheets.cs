// Ridgeback Rangers — bottom sheets (CoddyKit BottomSheet: content-sized, finger scroll with inertia, no visible scrollbar,
// one action pill per card, fleet §16 / §17). Every sheet is rebuilt from the sim when something changed, never under a
// finger. Guide targets inside a sheet are named "Target_<target>" so the spotlight can find them.
//   pin       the problem, time left, the rangers who can take it (status + ETA, Send), Borrow a ranger / Storm Shelter (rewarded)
//   paddock   animals (mood, hunger, health, Compare, to the nursery), harmony, fence tier, water / shade / expand, the nursery
//   compare   the Compatibility card: two animals, the harmony bar, Diet / Biome / Temperament
//   build     an empty paddock slot   ·   park  zones, paddocks, park upgrades (tram, road lane, vet beds, café, gift shop)
//   rangers   the four roles: teams, status, hire; the Chief Ranger automation
//   hatchery  incubators (temperature slider), dig site chart (deterministic), Warm / Scout's tip (rewarded), the species log
//   plan      the feed board (morning crates per station), the tram timetable, today's announced events
//   shop · settings · report · report_last · offline (the Dawn Report ×2 rewarded)
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using RidgebackRangers.Game;
using RidgebackRangers.Sim;

namespace RidgebackRangers.UI
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
                case "pin": FillPin(sheetArg); break;
                case "paddock": FillPaddock(sheetArg); break;
                case "compare": FillCompare(); break;
                case "build": FillBuild(sheetArg); break;
                case "park": FillPark(); break;
                case "rangers": FillRangers(); break;
                case "hatchery": FillHatchery(); break;
                case "plan": FillPlan(); break;
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
        void Body(string text, string cls = "coddy-body") => Add(UiKit.Text(text, cls));

        UiKit.ListCard Card(string title, string sub, string actionText, Action onAction, bool enabled = true, string kind = "primary", string target = null, string badgeText = "")
        {
            var c = UiKit.Card(title, sub, null, !enabled && actionText == null, false, badgeText);
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
            r.AddToClassList("rr-row");
            r.Add(UiKit.Text(k, "rr-row__k"));
            var vl = UiKit.Text(v, "rr-row__v");
            if (cls != "") vl.AddToClassList(cls);
            r.Add(vl);
            Add(r);
            return r;
        }

        VisualElement Pills()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            Add(row);
            return row;
        }

        /// <summary>A bar 0..1 (harmony, mood, progress): a track with a fill, coloured good / warn / bad.</summary>
        void Bar(string label, double value, string target = null)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("rr-bar");
            if (target != null) wrap.name = "Target_" + target;
            wrap.Add(UiKit.Text(label, "coddy-caption-muted"));
            var track = new VisualElement();
            track.AddToClassList("rr-bar__track");
            var fill = new VisualElement();
            fill.AddToClassList("rr-bar__fill");
            fill.AddToClassList(value >= 0.7 ? "rr-bar__fill--good" : value >= 0.45 ? "rr-bar__fill--warn" : "rr-bar__fill--bad");
            fill.style.width = Length.Percent((float)Math.Max(0.03, Math.Min(1, value)) * 100f);
            track.Add(fill);
            wrap.Add(track);
            Add(wrap);
        }

        string BuyLabel(BuyInfo b) => b.Ok || b.Reason == "cash" ? Money(b.Cost) : L.T("err." + b.Reason);
        void Buy(string item) => main.Run("buy:" + item);
        void BuyCard(string item, string key, string target = null)
        {
            var b = Sim.CanBuy(item);
            if (b.Reason == "owned" || b.Reason == "max" || b.Reason == "invalid") return;
            Card(L.T("buy." + key), L.T("buy." + key + ".d"), BuyLabel(b), () => Buy(item), b.Ok, "primary", target);
        }

        void RewardCard(string placement, int target = -1)
        {
            var o = main.Rewards?.Get(placement, target);
            if (o == null || !o.Show) return;
            string label = Simulation.RewardIsCash(placement)
                ? L.F(o.Video ? "reward.video_cash" : "reward.free_cash", "cash", Money(o.Amount))
                : L.T(o.Video ? "reward.video" : "reward.claim");
            Card(L.T("reward." + placement + ".t"), o.Video ? L.T("reward." + placement + ".b") : L.T("reward.free.b"), label, () => main.ClaimReward(placement, target), true, "tonal");
        }

        string Mins(double m) => L.F("unit.minutes", "n", (long)Math.Max(0, Math.Ceiling(m)));
        string Pct(double f) => Fmt.Pct(f, L.Lang);

        // ------------------------------------------------------------------ pin

        void FillPin(int id)
        {
            var p = Sim.Pin(id);
            if (p == null) { CloseSheet(); return; }
            string where = p.Paddock >= 0 && p.Paddock < Sim.S.Paddocks.Count ? L.T("place." + Sim.Gd.Nodes[Sim.S.Paddocks[p.Paddock].Node].Id) : L.T("zone." + p.Zone);
            string sev = p.Kind == PinKinds.Sick ? " · " + L.T("health." + p.Severity) : "";
            Title((PinIcon.TryGetValue(p.Kind, out var ic) ? ic + " " : "") + L.T("pin." + p.Kind), where + sev);
            Body(L.T("pin." + p.Kind + ".d"));
            if (p.Animal >= 0 && p.Animal < Sim.S.Animals.Count) Body(AnimalName(p.Animal), "coddy-caption-muted");
            if (!Sim.S.Tutorial) Body(p.Ranger >= 0 ? L.T("pin.on_way") : L.F("pin.left", "time", Mins(p.Deadline - Sim.S.Minute)), "coddy-caption-muted");
            string role = Sim.RoleFor(p.Kind);
            Section(L.F("pin.who", "role", L.T("role." + role)));
            bool first = true;
            for (int i = 0; i < Sim.S.Rangers.Count; i++)
            {
                var r = Sim.S.Rangers[i];
                if (r.Role != role || r.Temp) continue;
                int ri = i;
                bool free = !r.Busy;
                string sub = L.T("ranger.status." + r.Status) + (free ? " · " + L.F("pin.eta", "time", Mins(Sim.EtaMin(i, id))) : "");
                Card(L.F("ranger.name", "role", L.T("role." + role), "n", RoleNumber(i)), sub, p.Ranger >= 0 ? null : L.T("pin.send"),
                    () => main.Run("send:" + id + ":" + ri), free && p.Ranger < 0, "primary", free && first && p.Ranger < 0 ? "ranger:" + role : null);
                if (free) first = false;
            }
            if (p.Ranger < 0) RewardCard("borrow_ranger", id);
            if (p.Kind == PinKinds.Fence || p.Kind == PinKinds.Escape) RewardCard("storm_shelter");
        }

        int RoleNumber(int index)
        {
            int n = 0;
            for (int i = 0; i <= index && i < Sim.S.Rangers.Count; i++) if (Sim.S.Rangers[i].Role == Sim.S.Rangers[index].Role && !Sim.S.Rangers[i].Temp) n++;
            return n;
        }

        // ------------------------------------------------------------------ paddock / compare / build

        void FillPaddock(int pi)
        {
            if (pi < 0 || pi >= Sim.S.Paddocks.Count) { CloseSheet(); return; }
            var pd = Sim.S.Paddocks[pi];
            var node = Sim.Gd.Nodes[pd.Node];
            Title(L.T("place." + node.Id), L.T("biome." + Sim.BiomeOf(pd)) + " · " + L.F("paddock.slots", "n", Sim.SlotsUsed(pi), "m", Sim.PaddockSlots(pi)) + " · " + L.T(pd.Fence == 1 ? "fence.tier1" : pd.Fence == 2 ? "fence.tier2" : "fence.tier3"));
            Bar(L.F("paddock.harmony", "pct", Pct(Sim.PaddockHarmony(pi))), Sim.PaddockHarmony(pi));
            Bar(L.F("paddock.fence", "pct", Pct(pd.Integrity)), pd.Integrity);
            if (Sim.FenceTooWeak(pi)) Body(L.T("paddock.weak"), "coddy-caption-muted");
            if (Sim.BiomeOf(pd) == "dune" && !pd.Water) Body(L.T("paddock.needs_water"), "coddy-caption-muted");
            Section(L.T("paddock.animals"));
            var ids = Sim.AnimalsIn(pi);
            if (ids.Count == 0) Body(L.T("paddock.empty"), "coddy-caption-muted");
            foreach (int i in ids)
            {
                var a = Sim.S.Animals[i];
                int ai = i;
                string sub = L.F("animal.mood", "pct", Pct(a.Mood)) + " · " + L.F("animal.fed", "pct", Pct(1 - a.Hunger)) + " · " + L.T("health." + a.Health);
                Card(AnimalName(i), sub, L.T("compare.pick"), () => { var r = main.Run("animal:" + ai); if (r.Ok && Act.CompareSecond >= 0) OpenSheet("compare"); else FillSheet(); },
                    true, Act.CompareFirst == i ? "primary" : "tonal", null, Act.CompareFirst == i ? L.T("compare.first") : "");
            }
            var nursery = new List<int>();
            for (int i = 0; i < Sim.S.Animals.Count; i++) if (Sim.S.Animals[i].Paddock < 0) nursery.Add(i);
            if (nursery.Count > 0)
            {
                Section(L.T("paddock.nursery"));
                foreach (int i in nursery)
                {
                    int ai = i;
                    Card(AnimalName(i), L.T("biome." + Sim.SpeciesOf(Sim.S.Animals[i]).Biome), L.T("paddock.move_here"), () => main.Run("move:" + ai + ":" + pi), true, "tonal");
                }
            }
            if (ids.Count > 0)
            {
                Section(L.T("paddock.rehome"));
                var row = Pills();
                foreach (int i in ids)
                {
                    int ai = i;
                    row.Add(UiKit.ActionPill(L.F("paddock.to_nursery", "name", L.T("sp." + Sim.S.Animals[i].Species)), () => main.Run("move:" + ai + ":-1"), true, "tonal"));
                }
            }
            Section(L.T("upgrade.title"));
            BuyCard("fence:" + pi, pd.Fence == 1 ? "fence2" : "fence3", "fence:" + pi);
            BuyCard("water:" + pi, "water");
            BuyCard("shade:" + pi, "shade");
            BuyCard("expand:" + pi, "expand");
        }

        void FillCompare()
        {
            Title(L.T("compare.title"), L.T("compare.sub"));
            int a = Act.CompareFirst, b = Act.CompareSecond;
            if (a >= 0 && b >= 0 && a < Sim.S.Animals.Count && b < Sim.S.Animals.Count)
            {
                var x = Sim.S.Animals[a];
                var y = Sim.S.Animals[b];
                string biome = x.Paddock >= 0 ? Sim.BiomeOf(Sim.S.Paddocks[x.Paddock]) : null;
                double h = Sim.Harmony(x, y, biome);
                Body(AnimalName(a) + "  +  " + AnimalName(b), "coddy-label-strong");
                Bar(L.F("compare.harmony", "pct", Pct(h)), h, "harmony");
                Body(L.T(h >= 0.7 ? "compare.good" : h >= 0.45 ? "compare.ok" : "compare.bad"));
                foreach (var an in new[] { x, y })
                {
                    var sp = Sim.SpeciesOf(an);
                    KV(L.T("sp." + sp.Id), L.T("diet." + sp.Diet) + " · " + L.T("biome." + sp.Biome) + " · " + L.T("temper." + sp.Temperament));
                }
                sheet.Actions.Add(UiKit.ActionPill(L.T("compare.again"), () => { Act.ClearCompare(); FillSheet(); }, true, "tonal"));
                return;
            }
            Body(a >= 0 ? L.F("compare.second", "name", AnimalName(a)) : L.T("compare.help"));
            for (int i = 0; i < Sim.S.Animals.Count; i++)
            {
                if (Sim.S.Animals[i].Paddock < 0) continue;
                int ai = i;
                var sp = Sim.SpeciesOf(Sim.S.Animals[i]);
                Card(AnimalName(i), L.T("temper." + sp.Temperament) + " · " + L.T("diet." + sp.Diet), L.T("compare.pick"),
                    () => main.Run("animal:" + ai), i != a, a == i ? "primary" : "tonal", "animal:" + i);
            }
        }

        void FillBuild(int node)
        {
            if (node < 0 || node >= Sim.Gd.Nodes.Count) { CloseSheet(); return; }
            var n = Sim.Gd.Nodes[node];
            Title(L.T("place." + n.Id), L.T("zone." + n.Zone));
            Body(L.T("build.help"));
            var b = Sim.CanBuy("paddock:" + n.Id);
            Card(L.T("buy.paddock"), L.T("buy.paddock.d"), BuyLabel(b), () => { Buy("paddock:" + n.Id); CloseSheet(); }, b.Ok);
        }

        // ------------------------------------------------------------------ park

        void FillPark()
        {
            Title(L.T("tab.park"), L.F("hud.rating", "r", Fmt.Rating(Sim.S.Rating)) + " · " + L.F("park.animals", "n", Sim.S.Animals.Count));
            foreach (var z in Sim.Gd.Zones)
            {
                if (Sim.Unlocked(z.Id)) continue;
                var b = Sim.CanBuy("zone:" + z.Id);
                if (b.Reason == "locked") continue;
                Card(L.T("zone." + z.Id), L.T("zone." + z.Id + ".d") + " · " + L.F("zone.needs", "r", (long)z.Rating), L.F("zone.unlock", "cost", Money(z.Cost)), () => Buy("zone:" + z.Id), b.Ok);
                break;
            }
            foreach (var z in Sim.Gd.Zones)
            {
                if (!Sim.Unlocked(z.Id)) continue;
                Section(L.T("zone." + z.Id), L.T("biome." + z.Biome));
                foreach (var n in Sim.Gd.Nodes)
                {
                    if (n.Zone != z.Id || n.Kind != "paddock") continue;
                    int pi = Sim.PaddockAt(n.Index);
                    if (pi >= 0)
                    {
                        Card(L.T("place." + n.Id), L.F("paddock.slots", "n", Sim.SlotsUsed(pi), "m", Sim.PaddockSlots(pi)) + " · " + L.F("paddock.harmony", "pct", Pct(Sim.PaddockHarmony(pi))),
                            L.T("common.open"), () => OpenSheet("paddock", pi), true, "tonal");
                        continue;
                    }
                    var b = Sim.CanBuy("paddock:" + n.Id);
                    Card(L.T("place." + n.Id), L.T("buy.paddock.d"), BuyLabel(b), () => Buy("paddock:" + n.Id), b.Ok);
                }
            }
            Section(L.T("upgrade.title"));
            BuyCard("tram_car", "tram_car");
            BuyCard("lane", "lane");
            BuyCard("vet_beds", "vet_beds");
            BuyCard("cafe", Sim.S.Cafe == 0 ? "cafe1" : "cafe2");
            BuyCard("gift", "gift");
        }

        // ------------------------------------------------------------------ rangers

        void FillRangers()
        {
            Title(L.T("tab.rangers"), L.F("rangers.count", "n", Sim.S.Rangers.Count));
            if (Sim.ChiefAvailable)
                Card(L.T("chief.title"), L.T("chief.d"), L.T(Sim.S.ChiefOn ? "settings.on" : "settings.off"), () => main.Run("chief:" + (Sim.S.ChiefOn ? 0 : 1)), true, Sim.S.ChiefOn ? "primary" : "tonal");
            else
            {
                var b = Sim.CanBuy("chief");
                Card(L.T("chief.title"), L.T("chief.d") + " · " + L.F("zone.needs", "r", (long)Sim.Gd.ChiefRating), BuyLabel(b), () => Buy("chief"), b.Ok);
            }
            foreach (var role in Roles.All)
            {
                Section(L.T("role." + role), L.F("rangers.role", "n", Sim.CountRole(role), "m", Sim.RangerCap));
                Body(L.T("role." + role + ".d"), "coddy-caption-muted");
                var row = Pills();
                for (int i = 0; i < Sim.S.Rangers.Count; i++)
                {
                    var r = Sim.S.Rangers[i];
                    if (r.Role != role) continue;
                    var chip = UiKit.Text((r.Temp ? "+ " : "") + RoleNumber(i) + " · " + L.T("ranger.status." + r.Status), "rr-pill-label");
                    row.Add(chip);
                }
                var hire = Sim.CanBuy("hire:" + role);
                if (hire.Reason != "max")
                    Card(L.T("buy.hire"), L.F("buy.hire.d", "wage", Money(Sim.Gd.RoleDefs[role].Wage)), BuyLabel(hire), () => Buy("hire:" + role), hire.Ok);
            }
        }

        // ------------------------------------------------------------------ hatchery

        void FillHatchery()
        {
            var s = Sim.S;
            Title(L.T("tab.hatchery"), L.F("hatch.log", "n", s.Found.Count, "m", Sim.Gd.Species.Count));
            Section(L.T("hatch.incubators"), s.Eggs.Count + "/" + s.Incubators);
            if (s.Eggs.Count == 0) Body(L.T("hatch.none"), "coddy-caption-muted");
            for (int i = 0; i < s.Eggs.Count; i++)
            {
                var e = s.Eggs[i];
                int slot = i;
                Card(L.F("hatch.egg", "name", L.T("sp." + e.Species)), L.F("hatch.left", "time", Fmt.Dur(e.LeftS / (e.Checked || Sim.Has("hatch_tech") ? 1 : 0.5), L))
                    + " · " + L.T(e.Checked ? "hatch.warm" : "hatch.cold"), null, null);
                if (!e.Checked)
                {
                    var slider = new Slider(L.T("hatch.temp"), 0, 100) { value = 25, name = "TempSlider_" + i };
                    slider.AddToClassList("rr-slider");
                    Add(slider);
                    Body(L.T("hatch.temp_help"), "coddy-caption-muted");
                    var check = UiKit.ActionPill(L.T("hatch.check"), () => main.Run("temp:" + slot + ":" + Mathf.RoundToInt(slider.value)), true, "primary");
                    check.name = "Target_temp_" + i;
                    Add(check);
                }
                RewardCard("warm_incubator", i);
            }
            foreach (var w in s.EggsWaiting) Body(L.F("hatch.waiting", "name", L.T("sp." + w)), "coddy-caption-muted");
            BuyCard("incubator", "incubator");
            BuyCard("hatch_tech", "hatch_tech");
            Section(L.T("hatch.dig"));
            Body(L.F("hatch.dig_help", "cp", (long)Sim.Gd.DigCp), "coddy-caption-muted");
            for (int d = 0; d < Sim.Gd.DigSites.Count; d++)
            {
                var site = Sim.Gd.DigSites[d];
                if (!Sim.Unlocked(site.Zone)) continue;
                int di = d;
                var names = new List<string>();
                for (int k = 0; k < site.Species.Count; k++) names.Add(Sim.DigRevealed(d, k) ? L.T("sp." + site.Species[k]) + (s.Found.Contains(site.Species[k]) ? " ✓" : "") : "???");
                string why = Sim.CanDig(d);
                string action = why == "" ? L.T("hatch.send") : why == "running" ? L.T("hatch.digging") : L.T("err." + why);
                Card(L.T("place." + site.Id), string.Join(" · ", names) + " · " + L.F("hatch.progress", "sp", L.T("sp." + Sim.DigTarget(d)), "pct", Pct(Sim.DigProgress(d) / 100.0)),
                    action, () => main.Run("dig:" + di), why == "");
            }
            RewardCard("scout_tip");
        }

        // ------------------------------------------------------------------ plan

        void FillPlan()
        {
            Title(L.T("tab.plan"), Sim.Phase == Phases.Plan ? L.T("plan.morning") : L.T("plan.live"));
            var ev = Sim.ActiveCrises();
            if (ev.Count > 0)
            {
                Section(L.T("plan.today"));
                foreach (var c in ev) Card(L.T("crisis." + c + ".t"), L.T("crisis." + c + ".b"), null, null);
            }
            Section(L.T("plan.feed"));
            Body(L.F("plan.feed_help", "cost", Money(Sim.Gd.CrateCost)), "coddy-caption-muted");
            foreach (var z in Sim.S.Zones)
            {
                if (!Sim.S.Stations.TryGetValue(z, out var st)) continue;
                string zone = z;
                int need = Sim.FeedNeed(z);
                Card(L.T("zone." + z), L.F("plan.station", "n", (long)Math.Floor(st.Crates), "cap", (long)Sim.StationCap(z)) + " · " + L.F("plan.need", "n", need),
                    L.F("plan.crates", "n", st.Plan), () => main.Run("feed:" + zone + ":" + Math.Min((int)Sim.StationCap(zone), need + 1)), true, "tonal");
                var row = Pills();
                row.Add(UiKit.ActionPill("−", () => main.Run("feed:" + zone + ":" + (st.Plan - 1)), st.Plan > 0, "tonal"));
                row.Add(UiKit.ActionPill("+", () => main.Run("feed:" + zone + ":" + (st.Plan + 1)), st.Plan < Sim.StationCap(zone), "tonal"));
                row.Add(UiKit.ActionPill(L.F("plan.suggest", "n", Math.Min((int)Sim.StationCap(zone), need + 1)), () => main.Run("feed:" + zone + ":" + Math.Min((int)Sim.StationCap(zone), need + 1)), true, "tonal"));
                if (!st.Cooler) BuyCard("cooler:" + z, "cooler");
            }
            BuyCard("feed_run2", "feed_run2");
            Section(L.T("plan.tram"), L.F("plan.cars", "n", Sim.S.Tram.Cars));
            Body(L.F("plan.tram_help", "min", (long)Math.Round(Sim.TramLoopMin), "cap", (long)Math.Round(Sim.TramCapacityPerH)), "coddy-caption-muted");
            var tr = Pills();
            var t = Sim.S.Tram;
            tr.Add(UiKit.ActionPill(L.T("plan.full"), () => main.Run("tram_policy:full:0"), true, t.Policy == TramPolicy.Full ? "primary" : "tonal"));
            foreach (int m in TramPolicy.Intervals)
                tr.Add(UiKit.ActionPill(L.F("plan.every", "n", m), () => main.Run("tram_policy:interval:" + m), true, t.Policy == TramPolicy.Interval && t.Interval == m ? "primary" : "tonal"));
            BuyCard("tram_car", "tram_car");
            if (Sim.Phase == Phases.Plan) sheet.Actions.Add(UiKit.PrimaryButton(L.T("hud.start_day"), () => main.Run("start_day")));
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
                    var c = Card(shop.Cat.Title(slug, L.Lang), shop.Cat.Desc(slug, L.Lang), label, () => shop.Buy(s), why == "", "primary", null, shop.ActiveOffer == slug ? L.T("shop.offer") : "");
                    if (why == "owned" && slug.StartsWith("skin_tram_"))
                        Add(UiKit.ActionPill(L.T(main.Session.Settings.TramSkin == slug ? "shop.equipped" : "shop.equip"), () => main.EquipSkin(main.Session.Settings.TramSkin == s ? "" : s), true, "tonal"));
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
            Section(L.T("settings.help"));
            Card(L.T("settings.guide"), "", L.T("intro.start"), main.ReplayGuide, true, "tonal");
            Section(L.T("settings.privacy"));
            Card(L.T("settings.privacy"), "", L.T("common.open"), main.ShowPrivacyOptions, true, "tonal");
            Card(L.T("settings.policy"), "", L.T("common.open"), () => Application.OpenURL(RidgebackRangers.App.RidgebackGameConfig.PrivacyUrl), true, "tonal");
            Card(L.T("settings.restore"), "", L.T("common.ok"), () => main.Shop?.Restore(), true, "tonal");
        }

        // ------------------------------------------------------------------ report / offline

        void FillReport(DayStats d, bool withNext)
        {
            if (d == null) { Title(L.T("phase.report")); return; }
            Title(L.F("report.title", "n", d.Day), L.F("report.rating", "r", Fmt.Rating(Sim.S.Rating)));
            var stars = new VisualElement();
            stars.AddToClassList("rr-report__stars");
            void Star(string key, int n)
            {
                var c = new VisualElement();
                c.AddToClassList("rr-report__star");
                c.Add(UiKit.Text(new string('★', n) + new string('☆', 3 - n), "rr-report__star-value"));
                c.Add(UiKit.Text(L.T(key), "coddy-caption-muted"));
                stars.Add(c);
            }
            Star("report.care", d.StarCare);
            Star("report.visitors", d.StarVisitors);
            Star("report.calm", d.StarCalm);
            Add(stars);
            KV(L.T("report.guests"), Fmt.Int(d.Visitors, L.Lang));
            KV(L.T("report.rides"), Fmt.Int(d.Rides, L.Lang));
            KV(L.T("report.walked"), Fmt.Int(d.Walked, L.Lang), d.Visitors > 0 && d.Walked > d.Visitors * 0.1 ? "rr-row__v--bad" : "");
            KV(L.T("report.satisfaction"), Pct(d.Satisfaction));
            KV(L.T("report.care_score"), Pct(d.Care));
            KV(L.T("report.pins"), L.F("report.pins_v", "ok", d.PinsResolved, "n", d.PinsResolved + d.PinsFailed));
            if (d.Failed.Count > 0)
            {
                var kinds = new List<string>();
                foreach (var k in d.Failed) { string t = L.T("pin." + k); if (!kinds.Contains(t)) kinds.Add(t); }
                Body(L.F("report.ignored", "list", string.Join(", ", kinds)), "coddy-caption-muted");
            }
            KV(L.T("report.tickets"), Money(d.Tickets));
            KV(L.T("report.snacks"), Money(d.Snacks + d.Gifts));
            KV(L.T("report.donations"), Money(d.Donations));
            KV(L.T("report.cp"), "+" + Cp(d.CpEarned));
            KV(L.T("report.wages"), "−" + Money(d.Wages));
            KV(L.T("report.feed"), "−" + Money(d.Feed));
            KV(L.T("report.upkeep"), "−" + Money(d.Upkeep));
            if (d.Fines > 0) KV(L.T("report.fines"), "−" + Money(d.Fines), "rr-row__v--bad");
            KV(L.T("report.net"), Money(d.Net), d.Net >= 0 ? "rr-row__v--good" : "rr-row__v--bad");
            if (withNext && Sim.Phase == Phases.Report)
            {
                var next = UiKit.PrimaryButton(L.T("hud.next_day"), () => main.Run("next_day"));
                next.name = "Target_next_day";
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
            RewardCard("dawn_double");
            sheet.Actions.Add(UiKit.PrimaryButton(L.T("common.ok"), CloseSheet));
        }
    }
}
