// Pour & Roast — bottom sheets (CoddyKit BottomSheet: content-sized, finger scroll with inertia, no visible scrollbar,
// one action pill per card, fleet §16 / §17). Every sheet is rebuilt from the sim when something changed, never under a
// finger. Guide targets inside a sheet are named "Target_<target>" so the spotlight can find them.
//   bar       the team (stations, training), hiring, the house rule + batching, the grinder, helper / dial-in rewards
//   menu      menu & stock: beans, milk order + supplier, ice, forecast prep, the menu board, pastry par levels
//   roastery  house beans, the drum (start / drop), green market, resting racks + blend, wholesale contracts
//   grow      branches (switch / open the next venue), equipment for this venue, research (≤ 20 min each)
//   forecast  today + the next days, what the weather does, prep hot drinks / ice, the daily bonus
//   reviews   the Review Wall          shop   offers, ad-free & packs, boosts, looks, cash (store prices only), Restore
//   settings  language, music / effects, graphics, looks, save slots, How to play, privacy options (UMP) + policy, restore
//   report    the day report + Next day       offline  the welcome-back card (×2 rewarded)
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using PourAndRoast.Game;
using PourAndRoast.Sim;

namespace PourAndRoast.UI
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
                case "bar": FillBar(); break;
                case "menu": FillMenu(); break;
                case "roastery": FillRoastery(); break;
                case "grow": FillGrow(); break;
                case "forecast": FillForecast(); break;
                case "reviews": FillReviews(); break;
                case "shop": FillShop(); break;
                case "settings": FillSettings(); break;
                case "report": FillReport(Sim.S.History.Count > 0 ? Sim.S.History[Sim.S.History.Count - 1] : null, true); break;
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
        void Caption(string text) => Add(UiKit.Text(text, "coddy-caption-muted"));

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

        VisualElement Chips()
        {
            var r = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            Add(r);
            return r;
        }

        Button Chip(VisualElement row, string text, string action, bool on, string target = null)
        {
            var b = UiKit.ActionPill(text, () => main.Run(action), true, on ? "primary" : "tonal");
            if (target != null) b.name = "Target_" + target.Replace(':', '_');
            row.Add(b);
            return b;
        }

        VisualElement KV(string k, string v, string cls = "")
        {
            var r = new VisualElement();
            r.AddToClassList("pr-row");
            r.Add(UiKit.Text(k, "pr-row__k"));
            var vl = UiKit.Text(v, "pr-row__v");
            if (cls != "") vl.AddToClassList(cls);
            r.Add(vl);
            Add(r);
            return r;
        }

        string BuyLabel(BuyInfo b) => b.Ok || b.Reason == "cash" ? Money(b.Cost) : L.T("err." + b.Reason);

        void BuyCard(string item, string titleKey, string descKey, string target = null)
        {
            var b = Sim.CanBuy(item);
            if (b.Reason == "max" || b.Reason == "owned" || b.Reason == "invalid") return;
            Card(L.T(titleKey), L.T(descKey), BuyLabel(b), () => main.Run("buy:" + item), b.Ok, "primary", target);
        }

        void RewardCard(string placement)
        {
            var o = main.Rewards?.Get(placement);
            if (o == null || !o.Show) return;
            string label = Simulation.RewardIsCash(placement)
                ? L.F(o.Video ? "reward.video_cash" : "reward.free_cash", "cash", Money(o.Amount))
                : L.T(o.Video ? "reward.video" : "reward.claim");
            Card(L.T("reward." + placement + ".t"), o.Video ? L.T("reward." + placement + ".b") : L.T("reward.free.b"), label, () => main.ClaimReward(placement), true, "tonal");
        }

        // ------------------------------------------------------------------ bar

        void FillBar()
        {
            var v = Sim.Venue;
            Title(L.T("tab.bar"), L.T("venue." + Sim.S.Venue));
            Section(L.T("bar.team"));
            foreach (var s in Sim.StaffHere())
            {
                var st = s;
                var train = Sim.CanBuy("train:" + st.Id);
                string sub = L.T("trait." + st.Trait) + " · " + L.F("bar.level", "n", st.Level) + (st.Role == Roles.Barista ? " · " + L.T("station." + st.Station) : "");
                Card(L.T("role." + st.Role), sub, train.Reason == "max" ? L.T("bar.max_level") : L.F("bar.train", "cost", Money(train.Cost)), () => main.Run("buy:train:" + st.Id), train.Ok, "tonal");
                if (st.Role == Roles.Barista)
                {
                    var row = Chips();
                    foreach (var station in Stations.All) Chip(row, L.T("station." + station), "assign:" + st.Id + ":" + station, st.Station == station);
                }
            }
            RewardCard("extra_barista");
            Section(L.T("bar.hire"));
            foreach (var role in Roles.All)
            {
                if (role == Roles.Roaster && !Sim.RoasteryOpen) continue;
                var b = Sim.CanBuy("staff:" + role);
                if (b.Reason == "max") continue;
                Card(L.F("bar.hire_role", "role", L.T("role." + role)), L.T("buy.staff." + role + ".d"), BuyLabel(b), () => main.Run("buy:staff:" + role), b.Ok, "primary", "hire:" + role);
            }
            Section(L.T("bar.rule"));
            Caption(L.T("rule." + Sim.S.Rule + ".d"));
            var rules = Chips();
            foreach (var r in Rules.All) Chip(rules, L.T("rule." + r), "rule:" + r, Sim.S.Rule == r, "rule:" + r);
            Card(L.T("bar.batch"), L.T("bar.batch.d"), L.T(Sim.S.Batch ? "settings.on" : "settings.off"), () => main.Run("batch:" + (Sim.S.Batch ? "off" : "on")), true, Sim.S.Batch ? "primary" : "tonal", "batch_sheet");
            Section(L.T("bar.grinder"), Sim.Jammed ? L.T("marker.jam") : "");
            KV(L.T("bar.tune"), Fmt.Pct(v.Tune / 100.0, L.Lang), Sim.OffTune ? "pr-row__v--bad" : "");
            KV(L.T("bar.wear"), Fmt.Pct(v.Wear / 100.0, L.Lang), v.Wear > 70 ? "pr-row__v--bad" : "");
            if (Sim.Jammed) Card(L.T("bar.repair"), L.T("bar.repair.d"), L.T("bar.repair"), () => main.Run("repair"));
            else Card(L.T("bar.dial_in"), L.T("bar.dial_in.d"), L.T("bar.dial_in"), () => main.Run("dial_in"), true, "tonal");
            BuyCard("service_grinder", "buy.service_grinder", "buy.service_grinder.d");
            RewardCard("auto_dialin");
            RewardCard("crisis_halve");
        }

        // ------------------------------------------------------------------ menu & stock

        void FillMenu()
        {
            var s = Sim.S;
            Title(L.T("tab.menu"), L.F("menu.count", "n", Sim.MenuCount));
            Section(L.T("stock.beans"), L.F("stock.q", "q", Fmt.Q(s.BeanQ)));
            KV(L.T("stock.house_beans"), Fmt.Amount(s.BeanKg, "kg", L.Lang), s.BeanKg < 1 ? "pr-row__v--bad" : "");
            BuyCard("beans", "buy.beans", "buy.beans.d");
            Card(L.T("stock.auto_beans"), L.T("stock.auto_beans.d"), L.T(s.AutoBeans ? "settings.on" : "settings.off"), () => main.Run("auto_beans:" + (s.AutoBeans ? 0 : 1)), true, s.AutoBeans ? "primary" : "tonal");
            Section(L.T("stock.milk"), s.MilkArriveAt > s.Minute ? L.F("stock.milk_eta", "time", Fmt.ClockMin(s.MilkArriveAt)) : "");
            for (int k = 0; k < 2; k++)
            {
                int kk = k;
                string name = L.T(k == 0 ? "stock.whole" : "stock.oat");
                var c = Card(name + " · " + Fmt.Amount(s.Milk[k], "L", L.Lang), L.F("stock.order", "n", Fmt.Amount(s.MilkOrder[k], "L", L.Lang)), null, null);
                var pm = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                pm.Add(UiKit.ActionPill("−", () => main.Run("milk:" + kk + ":" + (int)Math.Max(0, s.MilkOrder[kk] - 2)), true, "tonal"));
                pm.Add(UiKit.ActionPill("+", () => main.Run("milk:" + kk + ":" + (int)(s.MilkOrder[kk] + 2)), true, "tonal"));
                c.SetSide(pm);
            }
            Caption(L.T("stock.milk_help"));
            var sup = Chips();
            foreach (var id in Sim.Gd.SupplierOrder) Chip(sup, L.T("supplier." + id), "supplier:" + id, s.Supplier == id);
            BuyCard("milk_run", "buy.milk_run", "buy.milk_run.d");
            if (Sim.IceCap > 0 || Sim.Tier >= 2)
            {
                Section(L.T("stock.ice"), Fmt.Amount(s.Ice, "kg", L.Lang));
                BuyCard("ice_bag", "buy.ice_bag", "buy.ice_bag.d");
            }
            RewardCard("emergency_stock");
            Section(L.T("menu.board"));
            Caption(L.T("menu.help"));
            var drinks = Chips();
            foreach (var m in Sim.Gd.Menu)
            {
                if (m.IsPastry || !Sim.Available(m)) continue;
                bool on = Sim.OnMenu(m);
                Chip(drinks, L.T("menu." + m.Id), "menu:" + m.Id + ":" + (on ? 0 : 1), on);
            }
            bool anyPastry = false;
            foreach (var m in Sim.Gd.Menu)
            {
                if (!m.IsPastry || !Sim.Available(m)) continue;
                if (!anyPastry) { Section(L.T("menu.pastries")); Caption(L.T("menu.par_help")); anyPastry = true; }
                var mm = m;
                bool on = Sim.OnMenu(m);
                int par = s.Par[m.Index];
                var c = Card(L.T("menu." + m.Id), on ? L.F("menu.par", "n", par, "stock", s.Pastry[m.Index]) : L.T("menu.off"), null, null);
                var pm = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                if (on)
                {
                    pm.Add(UiKit.ActionPill("−", () => main.Run("par:" + mm.Id + ":" + Math.Max(0, par - 2)), true, "tonal"));
                    pm.Add(UiKit.ActionPill("+", () => main.Run("par:" + mm.Id + ":" + (par + 2)), true, "tonal"));
                }
                pm.Add(UiKit.ActionPill(L.T(on ? "menu.remove" : "menu.add"), () => main.Run("menu:" + mm.Id + ":" + (on ? 0 : 1)), true, on ? "tonal" : "primary"));
                c.SetSide(pm);
            }
        }

        // ------------------------------------------------------------------ roastery

        void FillRoastery()
        {
            var s = Sim.S;
            Title(L.T("tab.roastery"), Sim.RoasteryOpen ? L.F("roast.house", "kg", Fmt.Amount(s.BeanKg, "kg", L.Lang), "q", Fmt.Q(s.BeanQ)) : "");
            if (!Sim.RoasteryOpen)
            {
                Add(UiKit.EmptyCard(L.T("roast.locked.t"), L.F("roast.locked.b", "name", L.T("venue.roastery"))));
                return;
            }
            Section(L.T("roast.drum"), Fmt.Amount(Sim.DrumKg, "kg", L.Lang));
            if (s.Roast != null)
            {
                var b = s.Roast;
                string sub = L.T("origin." + b.Origin) + " · " + L.T("profile." + b.Profile) + " · " + Fmt.Pct(b.Progress, L.Lang);
                Card(Sim.InSweetSpot ? L.T("roast.sweet") : L.T("roast.roasting"), sub, L.T("roast.drop"), () => main.Run("drop"), true, Sim.InSweetSpot ? "primary" : "tonal");
                Caption(L.T(Sim.HasRoaster ? "roast.assist_help" : "roast.drop_help"));
            }
            else
            {
                Caption(L.T("roast.start_help"));
                foreach (var o in Sim.Gd.Origins)
                {
                    if (!Sim.OriginOpen(o) || s.Green[o.Index] < 1) continue;
                    Add(UiKit.Text(L.T("origin." + o.Id) + " · " + Fmt.Amount(s.Green[o.Index], "kg", L.Lang) + " · " + L.F("roast.best", "p", L.T("profile." + o.Best)), "coddy-label-strong"));
                    var row = Chips();
                    foreach (var p in Profiles.All)
                    {
                        if (!Sim.ProfileOpen(p)) continue;
                        Chip(row, L.T("profile." + p), "roast:" + o.Id + ":" + p, p == o.Best);
                    }
                }
            }
            Section(L.T("roast.market"));
            foreach (var o in Sim.Gd.Origins)
            {
                var c = Sim.CanBuy("green:" + o.Id);
                string sub = L.T("origin." + o.Id + ".d") + " · " + L.F("roast.price", "pct", Fmt.Pct(Sim.MarketMult(o.Index), L.Lang)) + " · " + L.F("roast.in_stock", "kg", Fmt.Amount(s.Green[o.Index], "kg", L.Lang));
                Card(L.T("origin." + o.Id), sub, c.Reason == "research" ? L.T("err.research") : L.F("roast.buy_lot", "kg", Fmt.Amount(Sim.LotKg, "kg", L.Lang), "cost", Money(c.Cost)), () => main.Run("buy:green:" + o.Id), c.Ok);
            }
            Section(L.T("roast.resting"), Fmt.Amount(Sim.RestingKg, "kg", L.Lang));
            foreach (var b in s.Resting)
                Add(UiKit.Text(L.T("origin." + b.Origin) + " · " + Fmt.Amount(b.Kg, "kg", L.Lang) + " · " + L.F("stock.q", "q", Fmt.Q(b.Q)) + " · " + (b.ReadyDay <= s.Day ? L.T("roast.rested") : L.T("roast.resting_until")), "coddy-caption-muted"));
            if (s.Resting.Count == 0) Caption(L.T("roast.rest_help"));
            Card(L.T("roast.blend"), L.F("roast.blend.d", "kg", Fmt.Amount(Sim.ReadyToBlendKg, "kg", L.Lang)), L.T("roast.blend"), () => main.Run("blend"), Sim.ReadyToBlendKg > 0);
            Section(L.T("roast.contracts"));
            foreach (var cd in Sim.Gd.Contracts)
            {
                var cs = Sim.ContractOf(cd.Id);
                string why = Sim.CanSign(cd.Id);
                string sub = L.F("contract.terms", "kg", Fmt.Amount(cd.Kg, "kg", L.Lang), "days", cd.Every, "q", Fmt.Q(cd.MinQ), "cash", Money(cd.Kg * cd.Pay * cs.PayMult))
                    + (cs.Active ? " · " + L.F("contract.next", "n", cs.NextDay) : "");
                string act = cs.Active ? L.T("contract.pause") : why == "" ? L.T("contract.sign") : why == "rating" ? L.F("contract.needs", "r", Fmt.Stars(cd.Rating, L.Lang)) : L.T("err." + why);
                Card(L.T("contract." + cd.Id), L.T("contract." + cd.Id + ".d") + " " + sub, act, () => main.Run("contract:" + cd.Id), cs.Active || why == "", cs.Active ? "tonal" : "primary");
            }
            Section(L.T("upgrade.title"));
            BuyCard("drum", "buy.drum", "buy.drum.d");
        }

        // ------------------------------------------------------------------ grow

        void FillGrow()
        {
            Title(L.T("tab.grow"), "★ " + Fmt.Stars(Sim.S.Rating, L.Lang));
            Section(L.T("grow.branches"));
            foreach (var vd in Sim.Gd.Venues)
            {
                string id = vd.Id;
                if (Sim.Owns(id))
                {
                    bool here = Sim.S.Venue == id;
                    var vs = Sim.S.VenueOf(id);
                    string sub = here ? L.T("grow.here") : L.F("grow.branch_income", "cash", Money(Sim.BranchIncome(vs)));
                    Card(L.T("venue." + id), sub, here ? L.T("grow.here_short") : L.T("grow.switch"), () => main.Run("venue:" + id), !here && Sim.Phase == Phases.Plan, "tonal");
                    continue;
                }
                var b = Sim.CanBuy("venue:" + id);
                if (b.Reason == "locked") continue;
                Card(L.T("venue." + id), L.T("venue." + id + ".d") + " " + L.F("grow.needs", "r", Fmt.Stars(vd.Rating, L.Lang)), b.Reason == "rating" ? L.F("grow.needs_short", "r", Fmt.Stars(vd.Rating, L.Lang)) : BuyLabel(b), () => main.Run("buy:venue:" + id), b.Ok);
                break;
            }
            if (Sim.Phase != Phases.Plan && Sim.S.Venues.Count > 1) Caption(L.T("grow.switch_help"));
            Section(L.T("upgrade.title"));
            BuyCard("heads", "buy.heads", "buy.heads.d");
            BuyCard("wands", "buy.wands", "buy.wands.d");
            BuyCard("seats", "buy.seats", "buy.seats.d", "buy:seats");
            BuyCard("grinder", "buy.grinder", "buy.grinder.d");
            BuyCard("rail", "buy.rail", "buy.rail.d");
            BuyCard("decor", "buy.decor", "buy.decor.d");
            BuyCard("ice", "buy.ice", "buy.ice.d");
            string running = Sim.S.Researching;
            Section(L.T("tab.research"), running != "" ? L.T("research." + running + ".t") + " · " + Fmt.Dur(Sim.S.ResearchLeftS, L) : "");
            foreach (var branch in new[] { "bar", "cafe", "roastery" })
            {
                Add(UiKit.Text(L.T("research.branch." + branch), "coddy-label-strong"));
                foreach (var r in Sim.Gd.Research)
                {
                    if (r.Branch != branch) continue;
                    string why = Sim.CanResearch(r.Id);
                    string id = r.Id;
                    string action = why == "owned" ? L.T("research.done") : why == "running" ? Fmt.Dur(Sim.S.ResearchLeftS, L)
                        : why == "requires" ? L.F("research.requires", "name", L.T("research." + r.Requires[0] + ".t"))
                        : why == "locked" ? L.F("research.needs_venue", "name", L.T("venue." + r.Venue)) : L.F("research.start", "cost", Money(r.Cost));
                    Card(L.T("research." + id + ".t"), L.T("research." + id + ".d") + " · " + Fmt.Dur(r.Minutes * 60, L), action, () => main.Run("research:" + id), why == "", "primary");
                }
            }
        }

        // ------------------------------------------------------------------ forecast + reviews

        void FillForecast()
        {
            var s = Sim.S;
            Title(L.T("forecast.title"), L.F("hud.day", "n", s.Day));
            for (int d = 0; d <= Sim.ForecastDays && d < s.Forecast.Count; d++)
            {
                string w = s.Forecast[d];
                string when = d == 0 ? L.T("forecast.today") : d == 1 ? L.T("forecast.tomorrow") : L.F("forecast.day", "n", s.Day + d);
                Card(when + " · " + L.T("weather." + w), L.T("forecast." + w), null, null);
            }
            if (Sim.ForecastDays < 3) Caption(L.T("forecast.board_help"));
            Section(L.T("forecast.prep"));
            Card(L.T("forecast.prep_hot"), L.F("forecast.prep_hot.d", "n", Sim.Gd.PrepHotCups, "left", s.PrepHot), Money(Sim.Gd.PrepHotPrice), () => main.Run("prep:hot"), s.Cash >= Sim.Gd.PrepHotPrice, "primary", "prep:hot");
            if (Sim.Tier >= 2) Card(L.T("forecast.prep_ice"), L.F("forecast.prep_ice.d", "kg", Fmt.Amount(Sim.Gd.PrepIceKg, "kg", L.Lang)), Money(Sim.Gd.PrepIcePrice), () => main.Run("prep:ice"), s.Cash >= Sim.Gd.PrepIcePrice);
            RewardCard("daily_bonus");
        }

        void FillReviews()
        {
            Title(L.T("reviews.title"), "★ " + Fmt.Stars(Sim.S.Rating, L.Lang));
            Caption(L.T("reviews.help"));
            if (Sim.S.Reviews.Count == 0) Add(UiKit.EmptyCard(L.T("reviews.empty.t"), L.T("reviews.empty.b")));
            for (int i = Sim.S.Reviews.Count - 1; i >= 0; i--)
            {
                var r = Sim.S.Reviews[i];
                Card(new string('★', r.Stars) + new string('☆', 5 - r.Stars), L.T("cust." + Sim.Gd.Customers[Math.Max(0, Math.Min(Sim.Gd.Customers.Count - 1, r.Cust))].Id) + " — " + L.T("review." + r.Reason), null, null);
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
                    string sl = slug;
                    string why = shop.WhyNot(slug);
                    string price = shop.Price(slug);
                    string label = why == "owned" ? L.T("shop.owned") : price != "" ? price : Commerce.Loading;
                    Card(shop.Cat.Title(slug, L.Lang), shop.Cat.Desc(slug, L.Lang), label, () => shop.Buy(sl), why == "", "primary", null, shop.ActiveOffer == slug ? L.T("shop.offer") : "");
                }
            }
            sheet.Actions.Add(UiKit.ActionPill(L.T("shop.restore"), () => shop.Restore(), true, "tonal"));
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
                langs.Add(UiKit.ActionPill(Loc.NativeNames[i], () => main.SetLanguage(code), true, L.Lang == code ? "primary" : "tonal"));
            }
            Section(L.T("settings.sound"));
            Card(L.T("settings.music"), "", L.T(st.Music ? "settings.on" : "settings.off"), () => main.SetAudio(!st.Music, st.Effects), true, st.Music ? "primary" : "tonal");
            Card(L.T("settings.effects"), "", L.T(st.Effects ? "settings.on" : "settings.off"), () => main.SetAudio(st.Music, !st.Effects), true, st.Effects ? "primary" : "tonal");
            Section(L.T("settings.graphics"));
            var tiers = Chips();
            foreach (var t in new[] { Tier.Low, Tier.Medium, Tier.High })
            {
                var tt = t;
                tiers.Add(UiKit.ActionPill(L.T("graphics." + t.ToString().ToLowerInvariant()), () => main.SetTier(tt), true, main.CurrentTier == t ? "primary" : "tonal"));
            }
            var shop = main.Shop;
            if (shop != null && (shop.Has("theme_nordic") || shop.Has("theme_artdeco")))
            {
                Section(L.T("settings.looks"));
                var looks = Chips();
                looks.Add(UiKit.ActionPill(L.T("settings.theme_default"), () => main.SetTheme(""), true, st.Theme == "" ? "primary" : "tonal"));
                foreach (var th in new[] { "theme_nordic", "theme_artdeco" })
                {
                    if (!shop.Has(th)) continue;
                    string t2 = th;
                    looks.Add(UiKit.ActionPill(shop.Cat.Title(th, L.Lang), () => main.SetTheme(t2), true, st.Theme == th ? "primary" : "tonal"));
                }
            }
            if (shop != null)
            {
                Section(L.T("settings.slots"), L.F("settings.slots_n", "n", shop.SaveSlots));
                var slots = Chips();
                for (int i = 0; i < 5; i++)
                {
                    int si = i;
                    bool open = i < shop.SaveSlots;
                    string label = L.F("settings.slot", "n", i + 1) + (main.Session.Slot == i ? " ✓" : !open ? " 🔒" : main.Session.SlotUsed(i) ? "" : " +");
                    slots.Add(UiKit.ActionPill(label, () => { if (open) main.SwitchSlot(si); else OpenSheet("shop"); }, true, main.Session.Slot == i ? "primary" : "tonal"));
                }
                Caption(L.T("settings.slots_help"));
            }
            if (Sim.Perk("speed"))
                Card(L.T("settings.auto_report"), L.T("settings.auto_report.d"), L.T(st.AutoSkipReport ? "settings.on" : "settings.off"), () => { st.AutoSkipReport = !st.AutoSkipReport; st.Save(); FillSheet(); }, true, st.AutoSkipReport ? "primary" : "tonal");
            Section(L.T("settings.help"));
            Card(L.T("settings.guide"), L.T("settings.guide.d"), L.T("settings.replay"), main.ReplayGuide, true, "tonal");
            Section(L.T("settings.privacy"));
            Card(L.T("settings.privacy_options"), L.T("settings.privacy_options.d"), L.T("common.open"), main.ShowPrivacyOptions, true, "tonal");
            Card(L.T("settings.policy"), "", L.T("common.open"), () => Application.OpenURL(PourAndRoast.App.RoastGameConfig.PrivacyUrl), true, "tonal");
            Card(L.T("settings.restore"), "", L.T("common.ok"), () => main.Shop?.Restore(), true, "tonal");
        }

        // ------------------------------------------------------------------ report / offline

        void FillReport(DayStats d, bool withNext)
        {
            if (d == null) { Title(L.T("phase.report")); return; }
            Title(L.F("report.title", "n", d.Day), L.T("venue." + (d.Venue != "" ? d.Venue : Sim.S.Venue)) + " · " + L.T("weather." + (d.Weather != "" ? d.Weather : "sunny")));
            var stars = new VisualElement();
            stars.AddToClassList("pr-report__stars");
            stars.Add(UiKit.Text(new string('★', d.Stars) + new string('☆', 5 - d.Stars), "pr-report__star-value"));
            stars.Add(UiKit.Text(L.F("report.reviews", "avg", Fmt.Stars(d.AvgReview, L.Lang), "n", d.ReviewN, "rating", Fmt.Stars(Sim.S.Rating, L.Lang)), "coddy-caption-muted"));
            Add(stars);
            KV(L.T("report.served"), Fmt.Int(d.Served, L.Lang) + " / " + Fmt.Int(d.Customers, L.Lang));
            if (d.Walked + d.Balked > 0) KV(L.T("report.walked"), Fmt.Int(d.Walked + d.Balked, L.Lang), "pr-row__v--bad");
            KV(L.T("report.wait"), d.Served > 0 ? L.F("report.minutes", "n", (long)Math.Round(d.WaitSum / d.Served)) : "—");
            if (d.PreOrders > 0) KV(L.T("report.on_time"), Fmt.Pct(d.OnTime, L.Lang), d.OnTime < 0.8 ? "pr-row__v--bad" : "");
            KV(L.T("report.quality"), Fmt.Q(d.AvgQ));
            KV(L.T("report.revenue"), Money(d.Revenue));
            if (d.Tips > 0) KV(L.T("report.tips"), Money(d.Tips));
            if (d.Wholesale > 0) KV(L.T("report.wholesale"), Money(d.Wholesale));
            if (d.Branches > 0) KV(L.T("report.branches"), Money(d.Branches));
            if (d.Bonus > 0) KV(L.T("report.bonus"), Money(d.Bonus));
            KV(L.T("report.beans"), "−" + Money(d.BeansCost));
            KV(L.T("report.milk"), "−" + Money(d.MilkCost + d.IceCost));
            if (d.PastryCost > 0) KV(L.T("report.pastry"), "−" + Money(d.PastryCost));
            KV(L.T("report.wages"), "−" + Money(d.Wages));
            KV(L.T("report.rent"), "−" + Money(d.Rent));
            if (d.Fees > 0) KV(L.T("report.fees"), "−" + Money(d.Fees), "pr-row__v--bad");
            if (d.Waste > 0) KV(L.T("report.waste"), Money(d.Waste), "pr-row__v--bad");
            KV(L.T("report.net"), Money(d.Net), d.Net >= 0 ? "pr-row__v--good" : "pr-row__v--bad");
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
            if (o.Capped) Caption(L.F("offline.capped", "h", (long)Math.Round(o.Hours)));
            Caption(L.T("offline.help"));
            RewardCard("offline_x2");
            sheet.Actions.Add(UiKit.PrimaryButton(L.T("common.ok"), CloseSheet));
        }
    }
}
