// Ridgeback Rangers — bottom sheets (CoddyKit BottomSheet: content-sized, finger scroll with inertia, no visible scrollbar,
// one action pill per card, fleet §16 / §17). Every sheet is rebuilt from the sim when something changed, never under a
// finger. Guide targets inside a sheet are named "Target_<target>" so the spotlight can find them.
//   point     fill + estimate/live, today's numbers, service rule chips, upgrades, technician, emergency empty (rewarded)
//   depot     till, count lines, baler, yard / glass bay, sell buttons (today's price), depot upgrades, baler rescue (rewarded)
//   fleet     trucks (status, load, auto), drivers / technicians, buy trucks, fuel voucher (rewarded)
//   network   districts (unlock), free sites to build on (machines / shops / café contracts), dispatch office, counting centre
//   research  three branches, one project at a time, ≤ 20 min
//   shop      offer, ad-free & packs, upgrades, looks, cash (store prices only), Restore
//   settings  language, music / effects, graphics tier, replay the guide, privacy options (UMP) + policy link, restore
//   report    the day report: stars, numbers, Green Rating, Next day   ·   offline  the welcome-back card (×2 rewarded)
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
                case "point": FillPoint(sheetArg); break;
                case "depot": FillDepot(); break;
                case "fleet": FillFleet(); break;
                case "network": FillNetwork(); break;
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
            r.AddToClassList("br-row");
            r.Add(UiKit.Text(k, "br-row__k"));
            var vl = UiKit.Text(v, "br-row__v");
            if (cls != "") vl.AddToClassList(cls);
            r.Add(vl);
            Add(r);
            return r;
        }

        string BuyLabel(BuyInfo b) => b.Ok ? Money(b.Cost) : b.Reason == "cash" ? Money(b.Cost) : L.T("err." + b.Reason);

        void Buy(string item) => main.Run("buy:" + item);

        void RewardCard(string placement, string key, int target = -1)
        {
            var o = main.Rewards?.Get(placement, target);
            if (o == null || !o.Show) return;
            string label = Simulation.RewardIsCash(placement)
                ? L.F(o.Video ? "reward.video_cash" : "reward.free_cash", "cash", Money(o.Amount))
                : L.T(o.Video ? "reward.video" : "reward.claim");
            Card(L.T("reward." + key + ".t"), o.Video ? L.T("reward." + key + ".b") : L.T("reward.free.b"), label, () => main.ClaimReward(placement, target), true, "tonal");
        }

        // ------------------------------------------------------------------ point

        void FillPoint(int i)
        {
            if (i < 0 || i >= Sim.S.Points.Count) { CloseSheet(); return; }
            var p = Sim.S.Points[i];
            var node = Sim.NodeOf(p);
            Title(L.T("place." + node.Id), L.T("kind." + p.Kind) + " · " + L.T("district." + node.District));
            double pct = Sim.DisplayPct(p);
            KV(L.F("point.fill", "pct", Fmt.Pct(pct, L.Lang)), Sim.DisplayIsLive(p) ? L.T("point.live") : p.SensorOffline ? L.T("point.offline") : L.T("point.estimate"),
                Sim.IsFull(p) ? "br-row__v--bad" : "");
            if (p.Jammed) Add(UiKit.Text(L.T("point.jammed"), "coddy-body"));
            else if (p.BusyUntil > Sim.S.Minute) Add(UiKit.Text(L.T("point.busy"), "coddy-body"));
            else if (Sim.IsFull(p)) Add(UiKit.Text(L.T("point.full"), "coddy-body"));
            if (node.Alley) Add(UiKit.Text(L.T("point.alley"), "coddy-caption-muted"));
            Add(UiKit.Text(L.F("point.today", "dep", p.Deposited, "turned", p.Turned), "coddy-caption-muted"));

            if (p.Jammed || p.SensorOffline)
                Card(L.T("point.send_tech"), L.F("fleet.techs", "n", Sim.FreeTechs), p.TechCalled ? L.T("point.tech_on_way") : L.T("point.send_tech"),
                    () => main.Run("tech:point:" + i), !p.TechCalled && Sim.FreeTechs > 0);
            RewardCard("emergency_empty", "empty", i);

            Section(L.T("policy.title"));
            Add(UiKit.Text(L.T("policy.help"), "coddy-caption-muted"));
            var chips = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            void Chip(string text, string action, bool on, string target)
            {
                var b = UiKit.ActionPill(text, () => main.Run(action), true, on ? "primary" : "tonal");
                if (target != null) b.name = "Target_" + target;
                chips.Add(b);
            }
            foreach (int th in Policies.Thresholds)
                Chip(L.F("policy.threshold", "pct", Fmt.Pct(th / 100.0, L.Lang)), "policy:" + i + ":threshold:" + th, p.Policy == Policies.Threshold && p.Threshold == th,
                    th == 80 ? "policy_" + i : null);
            foreach (int d in Policies.FixedDays)
                Chip(d == 1 ? L.T("policy.fixed1") : L.F("policy.fixed", "n", d), "policy:" + i + ":fixed:" + d, p.Policy == Policies.Fixed && p.FixedEvery == d, null);
            Chip(L.T("policy.call"), "policy:" + i + ":call:0", p.Policy == Policies.Call, null);
            Add(chips);

            if (p.IsMachine)
            {
                Section(L.T("upgrade.title"));
                var bin = Sim.CanBuy("bin_m:" + i);
                if (bin.Reason != "owned") Card(L.T("upgrade.bin_m"), L.T("upgrade.bin_m.d"), BuyLabel(bin), () => Buy("bin_m:" + i), bin.Ok, "primary", "upgrade:" + i);
                var comp = Sim.CanBuy("compactor:" + i);
                if (comp.Reason != "owned") Card(L.T("upgrade.compactor"), L.T("upgrade.compactor.d"), BuyLabel(comp), () => Buy("compactor:" + i), comp.Ok);
                var sen = Sim.CanBuy("sensor:" + i);
                if (sen.Reason != "owned") Card(L.T("upgrade.sensor"), L.T("upgrade.sensor.d"), BuyLabel(sen), () => Buy("sensor:" + i), sen.Ok);
            }
        }

        // ------------------------------------------------------------------ depot

        void FillDepot()
        {
            var d = Sim.S.Depot;
            Title(L.T("depot.title"), L.F("depot.lines", "n", d.Lines, "rate", (long)Math.Round(Sim.CountPerMinute * 60)));
            double till = Math.Floor(d.Till);
            Card(till >= 1 ? L.F("depot.till", "cash", Money(till)) : L.T("depot.till_empty"), L.F("depot.intake", "n", (long)Math.Round(d.IntakeTotal)),
                L.T("common.ok"), () => main.Run("till"), till >= 1, "primary", "coins");
            int gate = 0;
            foreach (var t in Sim.S.Trucks) if (t.Status == TruckStatus.Wait) gate++;
            if (gate > 0) Add(UiKit.Text(L.F("depot.gate", "n", gate), "coddy-body"));

            Section(L.T("depot.baler"), d.BalerDown ? L.T("depot.baler_down") : "");
            int pressing = d.Press[Mats.Alu] > d.Press[Mats.Pet] ? Mats.Alu : Mats.Pet;
            double press = d.Press[pressing] / Sim.Gd.Materials[pressing].BaleKg;
            Add(UiKit.Text(d.BalerDown ? L.T("depot.baler_down") : press > 0 ? L.F("depot.baler_ok", "mat", L.T("mat." + Mats.Ids[pressing]), "pct", Fmt.Pct(press, L.Lang)) : L.T("depot.baler_idle"), "coddy-body"));
            if (d.BalerDown)
            {
                Card(L.T("point.send_tech"), L.F("fleet.techs", "n", Sim.FreeTechs), L.T("point.send_tech"), () => main.Run("tech:baler:0"), Sim.FreeTechs > 0);
                RewardCard("baler_rescue", "baler");
            }
            Add(UiKit.Text(Sim.YardFull ? L.T("depot.yard_full") : L.F("depot.yard", "n", d.Yard, "cap", d.YardCap), "coddy-caption-muted"));
            Add(UiKit.Text(L.F("depot.glass", "n", d.Bales[Mats.Glass], "cap", Sim.GlassBayCap), "coddy-caption-muted"));
            for (int m = 0; m < Mats.N; m++)
            {
                int mm = m;
                int n = d.Bales[m];
                double price = Sim.BalePrice(m);
                Card(L.T("mat." + Mats.Ids[m]), L.F("depot.price", "pct", Fmt.Pct(Sim.MarketMult(m), L.Lang)),
                    n > 0 ? L.F("depot.sell", "n", n, "cash", Money(Math.Round(n * price))) : L.T("depot.none"), () => main.Run("sell:" + mm), n > 0, "primary", "sell:" + m);
            }
            Section(L.T("upgrade.title"));
            foreach (var item in new[] { "line", "intake", "yard", "centre" })
            {
                var b = Sim.CanBuy(item);
                if (b.Reason == "owned" || b.Reason == "max") continue;
                string it = item;
                Card(L.T("buy." + item), L.T("buy." + item + ".d"), BuyLabel(b), () => Buy(it), b.Ok);
            }
        }

        // ------------------------------------------------------------------ fleet

        void FillFleet()
        {
            Title(L.T("tab.fleet"), L.F("fleet.drivers", "n", Sim.S.Drivers, "m", Sim.S.Trucks.Count) + " · " + L.F("fleet.techs", "n", Sim.S.Techs));
            for (int i = 0; i < Sim.S.Trucks.Count; i++)
            {
                int ti = i;
                var t = Sim.S.Trucks[i];
                string sub = L.T("truck.status." + t.Status) + " · " + L.F("truck.load", "pct", Fmt.Pct(Sim.LoadPct(t), L.Lang))
                    + (t.Stops.Count > 0 ? " · " + L.F("truck.stops", "n", t.Stops.Count) : "") + (Sim.LoadLimitedByWeight(t) && Sim.LoadPct(t) > 0.5 ? " · " + L.T("truck.weight") : "");
                if (t.Busy && (t.Status == TruckStatus.Drive || t.Status == TruckStatus.Service))
                    Card(L.F("truck.name", "type", L.T("truck." + t.Type), "n", i + 1), sub, L.T("truck.recall"), () => main.Run("recall:" + ti), true, "tonal");
                else if (t.Status == TruckStatus.Broken)
                    Card(L.F("truck.name", "type", L.T("truck." + t.Type), "n", i + 1), sub, L.T("point.send_tech"), () => main.Run("tech:truck:" + ti), Sim.FreeTechs > 0, "tonal");
                else
                    Card(L.F("truck.name", "type", L.T("truck." + t.Type), "n", i + 1), sub, L.T("truck.suggest"), () => { CloseSheet(); main.Run("select_truck:" + ti); main.Run("suggest"); }, !t.Busy, "tonal");
                if (Sim.S.Depot.Dispatcher)
                {
                    var tog = UiKit.ActionPill(L.T("truck.auto"), () => main.Run("auto:" + ti + ":" + (t.Auto ? 0 : 1)), true, t.Auto ? "primary" : "tonal");
                    Add(tog);
                }
            }
            RewardCard("fuel_voucher", "fuel");
            Section(L.T("upgrade.title"));
            var drv = Sim.CanBuy("driver");
            if (drv.Reason != "max") Card(L.T("buy.driver"), L.T("buy.driver.d"), BuyLabel(drv), () => Buy("driver"), drv.Ok, "primary", "hire");
            var tech = Sim.CanBuy("tech");
            if (tech.Reason != "max") Card(L.T("buy.tech"), L.T("buy.tech.d"), BuyLabel(tech), () => Buy("tech"), tech.Ok);
            foreach (var type in Sim.Gd.VehicleOrder)
            {
                var b = Sim.CanBuy("truck:" + type);
                if (b.Reason == "max") continue;
                string tt = type;
                Card(L.T("truck." + type), L.T("truck." + type + ".d"), b.Reason == "research" ? L.T("err.research") : BuyLabel(b), () => Buy("truck:" + tt), b.Ok);
            }
        }

        // ------------------------------------------------------------------ network

        void FillNetwork()
        {
            Title(L.T("tab.network"), L.F("hud.rating", "r", Fmt.Rating(Sim.S.Rating)));
            foreach (var dd in Sim.Gd.Districts)
            {
                if (Sim.Unlocked(dd.Id)) continue;
                var b = Sim.CanBuy("district:" + dd.Id);
                if (b.Reason == "locked") continue;
                Card(L.T("district." + dd.Id), L.F("district.needs", "r", (long)dd.Rating), L.F("district.unlock", "cost", Money(dd.Cost)), () => Buy("district:" + dd.Id), b.Ok);
                break;
            }
            foreach (var item in new[] { "dispatcher", "centre" })
            {
                var b = Sim.CanBuy(item);
                if (b.Reason == "owned") continue;
                string it = item;
                Card(L.T("buy." + item), L.T("buy." + item + ".d"), b.Reason == "research" ? L.T("err.research") : BuyLabel(b), () => Buy(it), b.Ok);
            }
            foreach (var dd in Sim.Gd.Districts)
            {
                if (!Sim.Unlocked(dd.Id)) continue;
                Section(L.T("district." + dd.Id));
                foreach (var n in Sim.Gd.Nodes)
                {
                    if (n.District != dd.Id || n.Kind != "site") continue;
                    int pi = Sim.PointAt(n.Index);
                    if (pi >= 0)
                    {
                        var p = Sim.S.Points[pi];
                        Card(L.T("place." + n.Id), L.T("kind." + p.Kind) + " · " + L.F("point.fill", "pct", Fmt.Pct(Sim.DisplayPct(p), L.Lang)), L.T("common.ok"), () => OpenSheet("point", pi), true, "tonal");
                        continue;
                    }
                    string id = (n.Site == "machine" ? "machine:" : n.Site + ":") + n.Id;
                    var b = Sim.CanBuy(id);
                    string key = n.Site == "machine" ? "machine" : n.Site;
                    Card(L.T("place." + n.Id), L.T("buy." + key) + " — " + L.T("buy." + key + ".d"), b.Reason == "research" ? L.T("err.research") : BuyLabel(b), () => Buy(id), b.Ok);
                }
            }
        }

        // ------------------------------------------------------------------ research

        void FillResearch()
        {
            string running = Sim.S.Researching;
            Title(L.T("tab.research"), running != "" ? L.T("research." + running + ".t") + " · " + L.F("research.left", "time", Fmt.Dur(Sim.S.ResearchLeftS, L)) : "");
            foreach (var branch in new[] { "network", "fleet", "depot" })
            {
                Section(L.T("research.branch." + branch));
                foreach (var r in Sim.Gd.Research)
                {
                    if (r.Branch != branch) continue;
                    string why = Sim.CanResearch(r.Id);
                    string id = r.Id;
                    string action = why == "owned" ? L.T("research.done") : why == "running" ? L.F("research.left", "time", Fmt.Dur(Sim.S.ResearchLeftS, L))
                        : why == "requires" ? L.F("research.requires", "name", L.T("research." + r.Requires[0] + ".t")) : L.F("research.start", "cost", Money(r.Cost));
                    Card(L.T("research." + id + ".t"), L.T("research." + id + ".d") + " · " + Fmt.Dur(r.Minutes * 60, L), action, () => main.Run("research:" + id), why == "", "primary");
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
            var langs = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            for (int i = 0; i < Loc.Languages.Length; i++)
            {
                string code = Loc.Languages[i];
                langs.Add(UiKit.ActionPill(Loc.NativeNames[i], () => main.SetLanguage(code), true, L.Lang == code ? "primary" : "tonal"));
            }
            Add(langs);
            Section(L.T("settings.title"));
            Card(L.T("settings.music"), "", L.T(st.Music ? "settings.on" : "settings.off"), () => main.SetAudio(!st.Music, st.Effects), true, st.Music ? "primary" : "tonal");
            Card(L.T("settings.effects"), "", L.T(st.Effects ? "settings.on" : "settings.off"), () => main.SetAudio(st.Music, !st.Effects), true, st.Effects ? "primary" : "tonal");
            Section(L.T("settings.graphics"));
            var tiers = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            foreach (var t in new[] { Tier.Low, Tier.Medium, Tier.High })
            {
                var tt = t;
                tiers.Add(UiKit.ActionPill(L.T("graphics." + t.ToString().ToLowerInvariant()), () => main.SetTier(tt), true, main.CurrentTier == t ? "primary" : "tonal"));
            }
            Add(tiers);
            Section(L.T("settings.privacy"));
            Card(L.T("settings.guide"), "", L.T("intro.start"), main.ReplayGuide, true, "tonal");
            Card(L.T("settings.privacy"), "", L.T("common.ok"), main.ShowPrivacyOptions, true, "tonal");
            Card(L.T("settings.policy"), "", L.T("common.ok"), () => Application.OpenURL(RidgebackRangers.App.RidgebackGameConfig.PrivacyUrl), true, "tonal");
            Card(L.T("settings.restore"), "", L.T("common.ok"), () => main.Shop?.Restore(), true, "tonal");
        }

        // ------------------------------------------------------------------ report / offline

        void FillReport(DayStats d, bool withNext)
        {
            if (d == null) { Title(L.T("phase.report")); return; }
            Title(L.F("report.title", "n", d.Day), L.F("report.rating", "r", Fmt.Rating(Sim.S.Rating)));
            var stars = new VisualElement();
            stars.AddToClassList("br-report__stars");
            void Star(string key, int n)
            {
                var c = new VisualElement();
                c.AddToClassList("br-report__star");
                c.Add(UiKit.Text(new string('★', n) + new string('☆', 3 - n), "br-report__star-value"));
                c.Add(UiKit.Text(L.T(key), "coddy-caption-muted"));
                stars.Add(c);
            }
            Star("report.service", d.StarService);
            Star("report.efficiency", d.StarEfficiency);
            Star("report.throughput", d.StarThroughput);
            Add(stars);
            KV(L.T("report.collected"), Fmt.Int(d.Collected, L.Lang));
            KV(L.T("report.km"), Fmt.Km(d.Km, L.Lang));
            KV(L.T("report.perkm"), d.PerKm.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            KV(L.T("report.turned"), Fmt.Int(d.Turned, L.Lang), d.TurnedRate > 0.06 ? "br-row__v--bad" : "");
            KV(L.T("report.fees"), Money(d.Fees));
            KV(L.T("report.sales"), Money(d.Sales));
            if (d.Bonus > 0) KV(L.T("report.bonus"), Money(d.Bonus));
            KV(L.T("report.fuel"), "−" + Money(d.Fuel));
            KV(L.T("report.wages"), "−" + Money(d.Wages));
            KV(L.T("report.leases"), "−" + Money(d.Leases));
            KV(L.T("report.power"), "−" + Money(d.Power));
            if (d.Fines > 0) KV(L.T("report.fines"), "−" + Money(d.Fines), "br-row__v--bad");
            KV(L.T("report.net"), Money(d.Net), d.Net >= 0 ? "br-row__v--good" : "br-row__v--bad");
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
            Add(UiKit.Text(L.F("offline.body", "cash", Money(o.Amount), "time", Fmt.Dur(o.Seconds, L)), "coddy-body"));
            if (o.Capped) Add(UiKit.Text(L.F("offline.capped", "h", (long)Math.Round(o.Hours)), "coddy-caption-muted"));
            RewardCard("offline_x2", "offline");
            sheet.Actions.Add(UiKit.PrimaryButton(L.T("common.ok"), CloseSheet));
        }
    }
}
