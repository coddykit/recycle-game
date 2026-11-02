// Charge Nurse sim core (from the Volt City sim) — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
namespace ChargeNurse.Sim
{
    /// <summary>
    /// Seeded Park–Miller generator (multiplier 48271). Pure integer state: the same seed gives the same numbers on
    /// every platform and the state survives a JSON round trip. Weather, events and each shift draw from their own
    /// stream (Stream()), so a player decision never shifts the weather or the next storm.
    /// Method names deliberately avoid engine global names (LL Pit Wall P1: a seeded RNG method called randf()
    /// silently resolved to the global one in GDScript).
    /// </summary>
    public sealed class Rng
    {
        public const long Mod = 2147483647;
        public long State;

        public Rng(long seed = 1)
        {
            State = PosMod(seed, Mod - 1) + 1;
        }

        public static long PosMod(long a, long m)
        {
            long r = a % m;
            return r < 0 ? r + m : r;
        }

        /// <summary>A child seed for an independent stream: same (seed, salt) gives the same stream.</summary>
        public static long Mix(long seed, long salt)
        {
            long h = PosMod(seed, Mod) ^ (PosMod(salt, 65536) * 2654435L);
            h = PosMod(h * 48271L + salt * 7919L + 12345L, Mod);
            h = PosMod(h * 48271L, Mod);
            return h;
        }

        public static Rng Stream(long seed, long salt) => new Rng(Mix(seed, salt));

        public long Next()
        {
            State = (State * 48271L) % Mod;
            return State;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public double Frand() => (double)(Next() - 1) / (double)(Mod - 1);

        /// <summary>Inclusive integer range.</summary>
        public int RangeI(int lo, int hi)
        {
            if (hi <= lo) return lo;
            return lo + (int)(Next() % (hi - lo + 1));
        }

        public int PickWeighted(double[] weights)
        {
            double total = 0;
            foreach (var w in weights) total += w > 0 ? w : 0;
            if (total <= 0) return -1;
            double r = Frand() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                r -= weights[i] > 0 ? weights[i] : 0;
                if (r < 0) return i;
            }
            return weights.Length - 1;
        }
    }
}
