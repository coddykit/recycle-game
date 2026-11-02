// Ridgeback Rangers sim core — the animals: hunger + feeding stations (the feed board), mood, harmony between paddock mates
// (the long-tail puzzle: Diet × Biome × Temperament), appeal for the tram riders (variety beats a mono-herd), the hatchery
// (incubators warmed on real time, a temperature check) and the dig sites (deterministic: each lists the species it yields;
// no random items, no gacha). Nobody is ever hurt: a missed vet visit sends the animal off-exhibit for the rest of the day.
using System;
using System.Collections.Generic;

namespace RidgebackRangers.Sim
{
    public sealed partial class Simulation
    {
        // ------------------------------------------------------------------------------------------------ paddocks

        public int PaddockSlots(int p) => Gd.PaddockSlots + S.Paddocks[p].Expand * Gd.ExpandSlots;

        public int SlotsUsed(int p)
        {
            int n = 0;
            foreach (var a in S.Animals) if (a.Paddock == p) n += SpeciesOf(a).Space;
            return n;
        }

        public List<int> AnimalsIn(int p)
        {
            var l = new List<int>();
            for (int i = 0; i < S.Animals.Count; i++) if (S.Animals[i].Paddock == p) l.Add(i);
            return l;
        }

        /// <summary>0..1 how well two animals share a paddock (the Compatibility card's bar).</summary>
        public double Harmony(AnimalState a, AnimalState b, string biome = null)
        {
            var sa = SpeciesOf(a);
            var sb = SpeciesOf(b);
            double h = Gd.Harmony(sa.Temperament, sb.Temperament);
            if (sa.Id == sb.Id) h += Gd.HarmonySame;
            else if (sa.Diet != sb.Diet) h += Gd.HarmonyOtherDiet;
            if (biome != null && (sa.Biome != biome || sb.Biome != biome)) h += Gd.HarmonyBiome;
            return Math.Max(0, Math.Min(1, h));
        }

        /// <summary>Average harmony of a paddock's pairs (1 when alone).</summary>
        public double PaddockHarmony(int p)
        {
            var ids = AnimalsIn(p);
            if (ids.Count < 2) return 1;
            string biome = BiomeOf(S.Paddocks[p]);
            double sum = 0; int n = 0;
            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++) { sum += Harmony(S.Animals[ids[i]], S.Animals[ids[j]], biome); n++; }
            return sum / n;
        }

        double SquabbleRate(int p)
        {
            var ids = AnimalsIn(p);
            string biome = BiomeOf(S.Paddocks[p]);
            double r = 0;
            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++) { double d = 1 - Harmony(S.Animals[ids[i]], S.Animals[ids[j]], biome); r += d * d; }
            r *= Gd.SquabblePerH;
            r += Math.Max(0, SlotsUsed(p) - PaddockSlots(p)) * Gd.SquabbleCrowd;
            return r;
        }

        /// <summary>The paddock's fence is too weak for one of its animals (Bossy species need steel).</summary>
        public bool FenceTooWeak(int p)
        {
            foreach (int i in AnimalsIn(p)) if (SpeciesOf(S.Animals[i]).Fence > S.Paddocks[p].Fence) return true;
            return false;
        }

        // ------------------------------------------------------------------------------------------------ appeal

        public double AnimalAppeal(AnimalState a)
        {
            if (a.Paddock < 0 || a.Health == Sim.Health.Off) return 0;
            var sp = SpeciesOf(a);
            double v = sp.Appeal * (0.5 + a.Mood);
            if (a.Young) v *= EventActive("school_trip") ? Gd.AppealSchoolYoung : Gd.AppealYoung;
            if (a.Health != Sim.Health.Ok) v *= Gd.AppealSick;
            if (a.ShowUntil > S.Minute) v *= Gd.AppealShow;   // feeding show: animals doing something draw the crowd
            return v;
        }

        /// <summary>What a tram rider gets from one zone's viewpoint: the animals' appeal × variety (a mono-herd scores less).</summary>
        public double ZoneAppeal(string zone)
        {
            double sum = 0;
            var species = new HashSet<string>();
            foreach (var a in S.Animals)
            {
                if (a.Paddock < 0 || a.Paddock >= S.Paddocks.Count || ZoneOf(S.Paddocks[a.Paddock]) != zone) continue;
                double v = AnimalAppeal(a);
                if (v <= 0) continue;
                sum += v;
                species.Add(a.Species);
            }
            if (species.Count == 0) return 0;
            return sum * Gd.Variety[Math.Min(Gd.Variety.Length - 1, species.Count - 1)];
        }

        // ------------------------------------------------------------------------------------------------ animals per step

        void StepAnimals(double dt)
        {
            double h = dt / 60.0;
            bool heat = EventActive("heat_wave");
            // heat spoils crates at stations without a cooler (Dune)
            if (heat)
                foreach (var kv in S.Stations)
                    if (!kv.Value.Cooler && Gd.Zone(kv.Key)?.Biome == "dune") kv.Value.Crates = Math.Max(0, kv.Value.Crates * (1 - Gd.SpoilH * h));
            for (int p = 0; p < S.Paddocks.Count; p++)
            {
                var pd = S.Paddocks[p];
                var ids = AnimalsIn(p);
                if (ids.Count == 0) continue;
                string zone = ZoneOf(pd);
                string biome = BiomeOf(pd);
                S.Stations.TryGetValue(zone, out var station);
                double harmony = PaddockHarmony(p);
                double crowd = Math.Max(0, SlotsUsed(p) - PaddockSlots(p)) / (double)Math.Max(1, PaddockSlots(p));
                int bossy = 0;
                foreach (int i in ids)
                {
                    var a = S.Animals[i];
                    var sp = SpeciesOf(a);
                    if (sp.Temperament == "bossy") bossy++;
                    a.Hunger = Math.Min(1, a.Hunger + sp.HungerH * h);
                    if (a.Hunger >= Gd.EatAt && station != null && station.Crates >= sp.Crates * 0.5)
                    {
                        double take = Math.Min(station.Crates, sp.Crates);
                        station.Crates -= take;
                        a.Hunger = Math.Max(0, a.Hunger - Gd.EatGain * take / Math.Max(0.1, sp.Crates));
                        a.ShowUntil = S.Minute + Gd.EatShowMin;
                    }
                    // mood drifts to what the paddock gives
                    double target = Gd.MoodBase + Gd.MoodHarmony * (harmony - 0.5) * 2 + Gd.MoodFed * (0.5 - a.Hunger) * 2 - Gd.MoodCrowd * crowd;
                    if (sp.Biome != biome) target -= Gd.MoodBiome;
                    if (biome == "dune" && !pd.Water) target -= Gd.MoodWater;
                    if (heat && biome == "dune" && !pd.Shade) target -= Gd.MoodHeat;
                    target = Math.Max(0.05, Math.Min(1, target));
                    a.Mood += (target - a.Mood) * Math.Min(1, Gd.MoodRateH * h * 4);
                    a.Mood = Math.Max(0, Math.Min(1, a.Mood));
                    if (S.Tutorial) continue;
                    // sickness: rare, more likely when stressed (hungry, unhappy)
                    if (a.Health == Sim.Health.Ok)
                    {
                        double stress = Math.Max(0, a.Hunger - 0.5) + Math.Max(0, 0.5 - a.Mood);
                        if (Rng.Frand() < Gd.SickPerH * h * (1 + Gd.SickStressMult * stress)) MakeSick(i, "");
                    }
                }
                // a hungry herd at an empty station calls for a Feeder
                if (station != null && station.Crates < 0.5)
                {
                    bool starving = false;
                    foreach (int i in ids) if (S.Animals[i].Hunger >= Gd.HungryPinAt) starving = true;
                    if (starving) OpenPin(PinKinds.Hungry, p, -1);
                }
                if (S.Tutorial) continue;
                // fences wear (Bossy animals push them), a weak fence becomes a loose-tile pin
                pd.Integrity = Math.Max(0, pd.Integrity - Gd.FenceWearH * h * (1 + Gd.FenceBossyMult * bossy) / pd.Fence * (FenceTooWeak(p) ? 2 : 1));
                if (pd.Integrity < Gd.FencePinBelow) OpenPin(PinKinds.Fence, p, -1);
                // low harmony / crowding -> squabbles
                if (ids.Count >= 2 && Rng.Frand() < SquabbleRate(p) * h) OpenPin(PinKinds.Squabble, p, -1);
            }
        }

        void MakeSick(int animal, string severity)
        {
            var a = S.Animals[animal];
            if (severity == "")
            {
                int k = Rng.PickWeighted(Gd.SickSeverity);
                severity = Health.Sick[Math.Max(0, Math.Min(2, k))];
            }
            a.Health = severity;
            OpenPin(PinKinds.Sick, a.Paddock, animal, severity);
        }

        void SampleCare()
        {
            double sum = 0; int n = 0;
            foreach (var a in S.Animals)
            {
                if (a.Paddock < 0) continue;
                double health = a.Health == Sim.Health.Ok ? 1 : a.Health == Sim.Health.Off ? 0 : 0.5;
                sum += (1 - a.Hunger) * 0.4 + a.Mood * 0.4 + health * 0.2;
                n++;
            }
            if (n == 0) return;
            S.Today.CareSum += sum / n;
            S.Today.CareN++;
        }

        /// <summary>The feed board's morning delivery (and the midday run with the second feed truck).</summary>
        void DeliverFeed(bool midday)
        {
            double share = EventActive("supply_late") && !midday ? EventMult("supply_late") : 1.0;
            foreach (var z in S.Zones)
            {
                if (!S.Stations.TryGetValue(z, out var st)) continue;
                double cap = StationCap(z);
                double want = midday ? Math.Max(0, Math.Min(cap, st.Plan) - st.Crates) : Math.Min(cap, st.Plan) * share;
                want = Math.Max(0, Math.Min(cap - st.Crates, Math.Floor(want)));
                if (want <= 0) continue;
                st.Crates += want;
                double cost = want * Gd.CrateCost;
                S.Cash -= cost;
                S.Today.Feed += cost;
            }
        }

        public double StationCap(string zone) => Gd.StationCap + (S.Stations.TryGetValue(zone, out var st) && st.Cooler ? Gd.StationCapPlus : 0);

        /// <summary>Crates a zone's animals eat in a usual day (the feed board's suggestion).</summary>
        public int FeedNeed(string zone)
        {
            double crates = 0;
            foreach (var a in S.Animals)
            {
                if (a.Paddock < 0 || ZoneOf(S.Paddocks[a.Paddock]) != zone) continue;
                var sp = SpeciesOf(a);
                double meals = sp.HungerH * (Gd.DayMinutes / 60.0) / Math.Max(0.1, Gd.EatGain);
                crates += meals * sp.Crates;
            }
            return (int)Math.Ceiling(crates);
        }

        public string SetFeedPlan(string zone, int crates)
        {
            if (!S.Stations.TryGetValue(zone ?? "", out var st)) return "invalid";
            st.Plan = Math.Max(0, Math.Min((int)StationCap(zone), crates));
            Emit("feed_plan", -1, -1, st.Plan, zone);
            return "";
        }

        /// <summary>Moves an animal to another paddock (or -1 = the nursery). "" = ok.</summary>
        public string MoveAnimal(int animal, int paddock)
        {
            if (animal < 0 || animal >= S.Animals.Count) return "invalid";
            if (paddock < -1 || paddock >= S.Paddocks.Count) return "invalid";
            var a = S.Animals[animal];
            if (a.Paddock == paddock) return "same";
            if (paddock >= 0 && SlotsUsed(paddock) + SpeciesOf(a).Space > PaddockSlots(paddock)) return "full";
            if (S.Pins.Exists(p => p.Animal == animal && p.Kind == PinKinds.Escape)) return "busy";
            a.Paddock = paddock;
            // its open pins follow it / close
            S.Pins.RemoveAll(p => p.Animal == animal && p.Ranger < 0 && p.Kind == PinKinds.Sick && paddock < 0);
            Emit("moved", animal, paddock);
            return "";
        }

        /// <summary>Best paddock for a new animal: same biome first, room for it, best harmony; -1 = nursery.</summary>
        public int SuggestPaddock(string species)
        {
            var sp = Gd.Sp(species);
            if (sp == null) return -1;
            int best = -1; double bestScore = -1;
            var probe = new AnimalState { Species = species };
            for (int p = 0; p < S.Paddocks.Count; p++)
            {
                if (SlotsUsed(p) + sp.Space > PaddockSlots(p)) continue;
                string biome = BiomeOf(S.Paddocks[p]);
                double score = biome == sp.Biome ? 1 : 0.3;
                foreach (int i in AnimalsIn(p)) score = Math.Min(score, Harmony(probe, S.Animals[i], biome));
                if (sp.Fence > S.Paddocks[p].Fence) score -= 0.4;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        void GrowUp()
        {
            foreach (var a in S.Animals)
            {
                a.AgeDays++;
                if (a.Young && a.AgeDays >= Gd.YoungDays) { a.Young = false; Emit("grown", AnimalIndex(a.Id)); }
                if (a.Health == Sim.Health.Off) a.Health = Sim.Health.Ok;   // back on exhibit the next morning
            }
        }

        // ------------------------------------------------------------------------------------------------ hatchery

        void TickEggs(double realS)
        {
            for (int i = 0; i < S.Eggs.Count; i++)
            {
                var e = S.Eggs[i];
                double rate = e.Checked || Has("hatch_tech") ? 1.0 : 0.5;
                e.LeftS -= realS * rate;
                // half way the temperature drifts: one more check (the Hatch Tech upgrade handles it)
                if (!e.SecondCheckDone && e.LeftS <= e.TotalS * 0.5 && !Has("hatch_tech")) { e.SecondCheckDone = true; e.Checked = false; Emit("egg_cold", i); }
                if (e.LeftS > 0) continue;
                Hatch(i);
                i--;
            }
            while (S.EggsWaiting.Count > 0 && S.Eggs.Count < S.Incubators)
            {
                PlaceEgg(S.EggsWaiting[0]);
                S.EggsWaiting.RemoveAt(0);
            }
        }

        public void PlaceEgg(string species)
        {
            var sp = Gd.Sp(species);
            if (sp == null) return;
            if (S.Eggs.Count >= S.Incubators) { S.EggsWaiting.Add(species); Emit("egg_waiting", -1, -1, 0, species); return; }
            double total = sp.HatchMin * 60.0;
            S.Eggs.Add(new EggState { Species = species, LeftS = total, TotalS = total, Checked = Has("hatch_tech") });
            Emit("egg", S.Eggs.Count - 1, -1, 0, species);
        }

        void Hatch(int slot)
        {
            var e = S.Eggs[slot];
            S.Eggs.RemoveAt(slot);
            int p = SuggestPaddock(e.Species);
            var a = AddAnimal(e.Species, p, true);
            a.Hunger = 0.2;
            S.Hatched++;
            Emit("hatched", S.Animals.Count - 1, p, 0, e.Species);
        }

        /// <summary>The incubator's temperature slider (0..100). In the band = checked (the egg warms at full speed). "" = ok.</summary>
        public string CheckTemp(int slot, double value)
        {
            if (slot < 0 || slot >= S.Eggs.Count) return "invalid";
            double lo = S.Tutorial ? Gd.TutorialLo : Gd.BandLo, hi = S.Tutorial ? Gd.TutorialHi : Gd.BandHi;
            if (value < lo || value > hi) return value < lo ? "too_cold" : "too_hot";
            S.Eggs[slot].Checked = true;
            Emit("egg_ok", slot);
            return "";
        }

        // ------------------------------------------------------------------------------------------------ dig sites

        /// <summary>The species the site's shards go to now: the first one not in the species log yet, then (all found) the
        /// list in turn — deterministic, the chart says exactly what comes.</summary>
        public string DigTarget(int site)
        {
            if (site < 0 || site >= Gd.DigSites.Count) return "";
            var d = Gd.DigSites[site];
            if (d.Species.Count == 0) return "";
            foreach (var sp in d.Species) if (!S.Found.Contains(sp)) return sp;
            return d.Species[DigEggs(site) % d.Species.Count];
        }

        public double DigProgress(int site) => site >= 0 && site < Gd.DigSites.Count && S.Shards.TryGetValue(Gd.DigSites[site].Id, out var v) ? v : 0;
        public int DigEggs(int site) => site >= 0 && site < Gd.DigSites.Count && S.DigEggs.TryGetValue(Gd.DigSites[site].Id, out var v) ? v : 0;

        /// <summary>The third species of a site is hidden until scouted (rewarded tip) or until the first two are in the log.</summary>
        public bool DigRevealed(int site, int index)
        {
            if (index < 2) return true;
            var d = Gd.DigSites[site];
            if (S.Scouted.Contains(d.Id)) return true;
            for (int i = 0; i < 2 && i < d.Species.Count; i++) if (!S.Found.Contains(d.Species[i])) return false;
            return true;
        }

        public string CanDig(int site)
        {
            if (site < 0 || site >= Gd.DigSites.Count) return "invalid";
            if (!Unlocked(Gd.DigSites[site].Zone)) return "locked";
            if (S.Phase != Phases.Live) return "phase";
            if (S.Rangers.Exists(r => r.Dig == site)) return "running";
            if (FreeRanger(Roles.Handler, Gd.Nodes[Gd.DigSites[site].Node].Index) < 0) return "no_ranger";
            if (S.Cp < Gd.DigCp) return "cp";
            return "";
        }

        /// <summary>Sends a dig crew (one Handler, off pin duty) to the site. "" = ok.</summary>
        public string Dig(int site)
        {
            string why = CanDig(site);
            if (why != "") return why;
            int node = Gd.DigSites[site].Node;
            int ri = FreeRanger(Roles.Handler, node);
            S.Cp -= Gd.DigCp;
            var r = S.Rangers[ri];
            r.Dig = site;
            r.Pin = -1;
            r.Leg = 0;
            Route(r, node);
            r.Status = RangerStatus.Travel;
            Emit("dig_start", site, ri);
            return "";
        }

        void FinishDig(RangerState r)
        {
            int site = r.Dig;
            r.Dig = -1;
            if (site < 0 || site >= Gd.DigSites.Count) return;
            string sp = DigTarget(site);
            if (sp == "") return;
            string id = Gd.DigSites[site].Id;
            double after = DigProgress(site) + Gd.DigShards;
            if (after >= 100 - 1e-9)
            {
                after = 0;
                S.DigEggs[id] = DigEggs(site) + 1;
                if (!S.Found.Contains(sp)) S.Found.Add(sp);
                PlaceEgg(sp);
            }
            S.Shards[id] = after;
            Emit("dig_done", site, -1, after, sp);
        }
    }
}
