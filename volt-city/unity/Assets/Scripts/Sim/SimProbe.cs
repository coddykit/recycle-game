// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System.Collections.Generic;

namespace VoltCity.Sim
{
    /// <summary>Fixed scenarios shared by the tests and the CLI (the cross-process determinism check prints these hashes).</summary>
    public static class SimProbe
    {
        /// <summary>Two greedy days, then a forced storm + plant-trip shift operated by the reference bot. "hash:cash".</summary>
        public static string ScriptedShiftHash(GameData gd, long seed)
        {
            var sim = CareerAfterDays(gd, seed, 2);
            sim.StartShift("rainy", new List<string> { "thunderstorm", "plant_trip" });
            DriveToEnd(sim);
            return sim.S.Report.Hash + ":" + sim.S.Cash;
        }

        public static Simulation CareerAfterDays(GameData gd, long seed, int days)
        {
            var sim = new Simulation(gd, seed);
            long now = BalanceRunner.Epoch;
            for (int d = 0; d < days; d++)
            {
                Policies.PlayDay(sim, "greedy", now);
                now += 86400;
            }
            return sim;
        }

        /// <summary>The reference operator every 4 steps until the shift ends.</summary>
        public static void DriveToEnd(Simulation sim)
        {
            while (sim.Phase == "live") DriveStep(sim);
        }

        public static void DriveStep(Simulation sim)
        {
            if (sim.Grid.Step % 4 == 0)
                foreach (var c in Dispatcher.Operate(sim, Policies.OperatorFor("greedy"))) sim.Queue(c);
            sim.RunSteps(1);
        }
    }
}
