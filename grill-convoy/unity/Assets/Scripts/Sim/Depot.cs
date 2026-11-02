// Grill Convoy sim core — the depot pipeline and the bale market.
//   intake (containers) -> count & verify line(s) (containers/hour; the handling fee is earned HERE, into the till)
//   -> sorter -> PET / aluminium to the baler (kg/hour, one bale at a time, jams) -> bale yard (cap)
//                glass to the glass bays (tonnes, cap; the crusher research doubles it)
// Bales are sold at the day's spot price (random walk around 1.0, crash events) or held. A full yard stops the baler; a
// full glass bay stops glass on the count line; a full intake makes trucks queue at the gate.
using System;

namespace GrillConvoy.Sim
{
    public sealed partial class Simulation
    {
        public double CountPerMinute => S.Depot.Lines * Gd.CountPerHour * (Has("count_speed") ? 1.25 : 1.0) / 60.0;
        public double BalerKgPerMinute => Gd.BalerKgPerHour * (Has("baler_speed") ? 1.4 : 1.0) / 60.0;
        public int GlassBayCap => (int)Math.Round(Gd.GlassBayT * (Has("glass_crusher") ? 2 : 1));
        public bool YardFull => S.Depot.Yard >= S.Depot.YardCap;
        public bool GlassBayFull => S.Depot.Bales[Mats.Glass] >= GlassBayCap;

        /// <summary>Spot price multiplier today (market × crash event).</summary>
        public double MarketMult(int m)
        {
            double x = S.Market[m];
            foreach (var e in S.Events) if (e.Id == "bale_crash" && e.Target == m) x *= e.Mult;
            return x;
        }
        public double BalePrice(int m) => Gd.Materials[m].BalePrice * MarketMult(m);

        void StepDepot(double dt)
        {
            var d = S.Depot;
            // count & verify
            double cap = CountPerMinute * dt;
            double avail = d.Intake[Mats.Pet] + d.Intake[Mats.Alu] + (GlassBayFull ? 0 : d.Intake[Mats.Glass]);
            if (avail > 0 && cap > 0)
            {
                double f = Math.Min(1.0, cap / avail);
                double counted = 0;
                for (int m = 0; m < Mats.N; m++)
                {
                    if (m == Mats.Glass && GlassBayFull) continue;
                    double c = d.Intake[m] * f;
                    d.Intake[m] -= c;
                    counted += c;
                    d.BalerIn[m] += c * Gd.Materials[m].Kg;
                }
                double fee = counted * Gd.Fee * FeeMult;
                d.Till += fee;
                S.Today.Fees += fee;
                S.Today.Counted += (int)Math.Round(counted);
            }
            // glass bay: every full tonne is a sellable unit
            while (d.BalerIn[Mats.Glass] >= Gd.Materials[Mats.Glass].BaleKg && !GlassBayFull)
            {
                d.BalerIn[Mats.Glass] -= Gd.Materials[Mats.Glass].BaleKg;
                d.Bales[Mats.Glass]++;
            }
            // baler
            if (d.BalerDown || YardFull) return;
            double rate = BalerKgPerMinute * dt;
            int mat = d.Press[Mats.Alu] > 0 && d.Press[Mats.Alu] >= d.Press[Mats.Pet] ? Mats.Alu
                : d.Press[Mats.Pet] > 0 ? Mats.Pet
                : d.BalerIn[Mats.Alu] / Gd.Materials[Mats.Alu].BaleKg > d.BalerIn[Mats.Pet] / Gd.Materials[Mats.Pet].BaleKg ? Mats.Alu : Mats.Pet;
            double move = Math.Min(rate, d.BalerIn[mat]);
            if (move <= 0) return;
            d.BalerIn[mat] -= move;
            d.Press[mat] += move;
            if (d.Press[mat] >= Gd.Materials[mat].BaleKg - 1e-9)
            {
                d.Press[mat] = Math.Max(0, d.Press[mat] - Gd.Materials[mat].BaleKg);
                d.Bales[mat]++;
                S.Bales++;
                Emit("bale", mat, d.Bales[mat]);
                if (YardFull) Emit("yard_full");
                if (JamAllowed && Rng.Frand() < Gd.BalerJamPerBale) BalerDown("jam");
            }
        }

        public void BalerDown(string why)
        {
            if (S.Depot.BalerDown) return;
            S.Depot.BalerDown = true;
            Emit("baler_down", -1, -1, 0, why);
        }

        /// <summary>Banks the till (the coins at the count line). Returns the amount.</summary>
        public double CollectTill()
        {
            double a = Math.Floor(S.Depot.Till);
            if (a <= 0) return 0;
            S.Depot.Till -= a;
            S.Cash += a;
            Emit("till", -1, -1, a);
            return a;
        }

        /// <summary>Sells every bale of a material at today's price. Returns the money (0 = nothing to sell).</summary>
        public double Sell(int m)
        {
            if (m < 0 || m >= Mats.N) return 0;
            int n = S.Depot.Bales[m];
            if (n <= 0) return 0;
            double amount = Math.Round(n * BalePrice(m));
            S.Depot.Bales[m] = 0;
            S.Cash += amount;
            S.Today.Sales += amount;
            Emit("sold", m, n, amount);
            return amount;
        }

        void UpdateMarket()
        {
            for (int m = 0; m < Mats.N; m++)
            {
                double x = S.Market[m];
                double shock = (Rng.Frand() - 0.5) * 0.16;
                x = x + shock + 0.12 * (1.0 - x);
                S.Market[m] = Math.Max(0.6, Math.Min(1.4, x));
            }
        }
    }
}
