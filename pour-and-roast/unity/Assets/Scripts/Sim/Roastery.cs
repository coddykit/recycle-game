// Pour & Roast sim core — the roastery (design §4 B, MVP v1: green market, roast, rest, one house blend, two wholesale
// contracts). Opens with the Roastery Hall (chapter 3); before that the bar runs on supplier beans (average quality).
//   * green market: 4 origins (fictional growing regions), lot price × a drifting market multiplier (frost spikes one);
//   * roast: one batch in the drum (drum kg), a profile (light / medium needs nothing, light + dark need the profiles
//     research), a timeline on REAL seconds (like research — it runs in the plan too); tap Drop inside the first-crack
//     sweet spot for bonus quality; the roaster assistant drops at an average point by himself; left alone the drum
//     drops at the end, a bit dark;
//   * rest: a batch rests RestDays in-game days, then Blend folds it into the house beans (weighted quality);
//   * wholesale: standing orders (kg every N days, minimum quality) paid at the day end; a shortfall costs a fee and the
//     contract's pay rate (soft, never a fail state).
using System;
using System.Collections.Generic;

namespace PourAndRoast.Sim
{
    public sealed partial class Simulation
    {
        public bool OriginOpen(OriginDef o) => RoasteryOpen && (o.Research == "" || Has(o.Research));
        public bool ProfileOpen(string p) => p == Profiles.Medium || Has("roast_profiles");

        public double MarketMult(int origin)
        {
            double m = origin >= 0 && origin < S.Market.Length ? S.Market[origin] : 1;
            foreach (var e in S.Events) if (e.Id == "frost" && e.Target == origin) m *= e.Mult;
            return m;
        }

        public double LotKg => Has("green_storage") ? Gd.LotKgStorage : Gd.LotKg;

        public double LotCost(int origin)
        {
            var o = Gd.Origins[origin];
            return Math.Round(o.Price * MarketMult(origin) * LotKg * (Has("green_storage") ? Gd.StorageDiscount : 1.0));
        }

        public double GreenTotal { get { double n = 0; foreach (var g in S.Green) n += g; return n; } }
        public double RestingKg { get { double n = 0; foreach (var b in S.Resting) n += b.Kg; return n; } }
        public double ReadyToBlendKg { get { double n = 0; foreach (var b in S.Resting) if (b.ReadyDay <= S.Day) n += b.Kg; return n; } }

        public string BuyGreen(string originId)
        {
            int i = Gd.OriginIndex(originId);
            if (i < 0) return "invalid";
            if (!RoasteryOpen) return "locked";
            if (!OriginOpen(Gd.Origins[i])) return "research";
            double cost = LotCost(i);
            if (S.Cash < cost) return "cash";
            S.Cash -= cost;
            S.Today.BeansCost += cost;
            S.Green[i] += LotKg;
            Emit("green_bought", i, -1, cost);
            return "";
        }

        public string CanRoast(string originId, string profile)
        {
            int i = Gd.OriginIndex(originId);
            if (i < 0 || !Profiles.Valid(profile)) return "invalid";
            if (!RoasteryOpen) return "locked";
            if (S.Roast != null) return "busy";
            if (!ProfileOpen(profile)) return "research";
            if (S.Green[i] < 1) return "no_green";
            return "";
        }

        public string StartRoast(string originId, string profile)
        {
            string why = CanRoast(originId, profile);
            if (why != "") return why;
            int i = Gd.OriginIndex(originId);
            double kg = Math.Min(DrumKg, S.Green[i]);
            S.Green[i] -= kg;
            S.Roast = new BatchState { Origin = originId, Profile = profile, Kg = kg, Progress = 0 };
            Emit("roast_start", i, -1, kg, profile);
            return "";
        }

        void TickRoast(double realS)
        {
            var b = S.Roast;
            if (b == null || realS <= 0) return;
            b.Progress += realS / Math.Max(1, Gd.RoastRealS);
            if (HasRoaster && b.Progress >= Gd.AssistAt) { DropRoast("assist"); return; }
            if (b.Progress >= 1) DropRoast("auto");
        }

        /// <summary>Roast quality for a drop at progress p (how = tap | assist | auto).</summary>
        public double RoastQuality(BatchState b, double p, string how)
        {
            int oi = Gd.OriginIndex(b.Origin);
            var o = oi >= 0 ? Gd.Origins[oi] : null;
            double q = o != null ? o.Q : 60;
            int diff = Math.Abs(Profiles.Index(b.Profile) - Profiles.Index(o != null ? o.Best : Profiles.Medium));
            q += diff == 0 ? Gd.ProfileMatch : diff == 1 ? 0 : -Gd.ProfileMatch;
            if (how == "assist") q += Gd.BonusAssist + (Perk("roast_pass") ? Gd.BonusPass : 0);
            else if (how == "auto") q += Gd.PenAuto;
            else if (p >= Gd.SweetFrom && p <= Gd.SweetTo) q += Gd.BonusSweet;
            else if (p < Gd.RoastEarly) q += Gd.PenEarly;
            else q += Gd.PenLate;
            if (Has("cupping_lab")) q += Gd.LabBonus;
            return Math.Max(20, Math.Min(100, q));
        }

        public bool InSweetSpot => S.Roast != null && S.Roast.Progress >= Gd.SweetFrom && S.Roast.Progress <= Gd.SweetTo;

        /// <summary>The player taps Drop. "" = ok.</summary>
        public string Drop()
        {
            if (S.Roast == null) return "invalid";
            DropRoast("tap");
            return "";
        }

        void DropRoast(string how)
        {
            var b = S.Roast;
            b.Q = Math.Round(RoastQuality(b, b.Progress, how), 1);
            b.Kg = Math.Round(b.Kg * (1 - Gd.RoastLoss), 3);
            b.ReadyDay = S.Day + Gd.RestDays;
            b.Progress = Math.Min(1, b.Progress);
            S.Resting.Add(b);
            S.Roast = null;
            Emit("roast_done", Gd.OriginIndex(b.Origin), -1, b.Q, how);
        }

        /// <summary>Folds every rested batch into the house beans. Returns the kg blended.</summary>
        public double Blend()
        {
            double kg = 0;
            for (int i = S.Resting.Count - 1; i >= 0; i--)
            {
                var b = S.Resting[i];
                if (b.ReadyDay > S.Day) continue;
                double total = S.BeanKg + b.Kg;
                S.BeanQ = total <= 1e-9 ? b.Q : (S.BeanQ * Math.Max(0, S.BeanKg) + b.Q * b.Kg) / total;
                S.BeanKg = total;
                kg += b.Kg;
                S.Resting.RemoveAt(i);
            }
            if (kg > 0) { S.BeanQ = Math.Round(S.BeanQ, 2); Emit("blended", -1, -1, kg); }
            return kg;
        }

        /// <summary>Supplier beans (before the roastery, and an emergency top-up). auto = the forgiving top-up when a shot finds the hopper empty.</summary>
        public string BuySupplierBeans(bool auto = false)
        {
            double cost = Math.Round(Gd.SupplierPrice * Gd.SupplierLot);
            if (S.Cash < cost && !auto) return "cash";
            if (S.Cash < cost && auto && S.Cash < cost * 0.5) return "cash";
            S.Cash -= cost;
            S.Today.BeansCost += cost;
            double total = S.BeanKg + Gd.SupplierLot;
            S.BeanQ = Math.Round((S.BeanQ * Math.Max(0, S.BeanKg) + Gd.SupplierQ * Gd.SupplierLot) / total, 2);
            S.BeanKg = total;
            Emit("beans_bought", -1, -1, cost, auto ? "auto" : "");
            return "";
        }

        // ------------------------------------------------------------------------------------------------ wholesale

        public ContractState ContractOf(string id) => S.Contracts.Find(c => c.Id == id);

        public string CanSign(string id)
        {
            var d = Gd.Contract(id);
            var c = ContractOf(id);
            if (d == null || c == null) return "invalid";
            if (c.Active) return "owned";
            if (!RoasteryOpen) return "locked";
            if (d.Research != "" && !Has(d.Research)) return "research";
            if (S.Rating < d.Rating) return "rating";
            return "";
        }

        /// <summary>Signs a contract, or pauses an active one.</summary>
        public string ToggleContract(string id)
        {
            var c = ContractOf(id);
            if (c == null) return "invalid";
            if (c.Active) { c.Active = false; Emit("contract", -1, -1, 0, id); return ""; }
            string why = CanSign(id);
            if (why != "") return why;
            c.Active = true;
            c.NextDay = S.Day + Gd.Contract(id).Every;
            Emit("contract", 1, -1, 0, id);
            return "";
        }

        void DeliverContracts()
        {
            foreach (var c in S.Contracts)
            {
                if (!c.Active || c.NextDay > S.Day) continue;
                var d = Gd.Contract(c.Id);
                if (d == null) continue;
                c.NextDay = S.Day + d.Every;
                double value = d.Kg * d.Pay * c.PayMult * (Perk("roast_pass") ? Gd.PassWholesale : 1.0);
                if (S.BeanKg >= d.Kg && S.BeanQ >= d.MinQ)
                {
                    S.BeanKg -= d.Kg;
                    value = Math.Round(value);
                    S.Cash += value;
                    S.Today.Wholesale += value;
                    c.Delivered++;
                    Emit("wholesale", -1, -1, value, c.Id);
                }
                else
                {
                    double fee = CrisesAllowed ? Math.Round(value * Gd.ContractMissFee) : 0;
                    S.Cash -= fee;
                    S.Today.Fees += fee;
                    c.Missed++;
                    if (CrisesAllowed) c.PayMult = Math.Max(Gd.ContractPayFloor, Math.Round(c.PayMult * Gd.ContractMissMult, 3));
                    Emit("crisis", -1, -1, fee, "wholesale_short");
                }
            }
        }

        void UpdateMarket()
        {
            for (int i = 0; i < S.Market.Length; i++)
            {
                double m = S.Market[i];
                m += (1 - m) * Gd.MarketRevert + (Rng.Frand() * 2 - 1) * Gd.MarketDrift;
                S.Market[i] = Math.Round(Math.Max(0.7, Math.Min(1.4, m)), 3);
            }
        }
    }
}
