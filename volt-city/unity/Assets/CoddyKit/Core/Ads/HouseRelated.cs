// CoddyKit (Unity) — relatedness order for the house banner + interstitial, port of CoddyHouseRelated.gd apply().
// apply(config, host) rewrites ads-config `houseAds` IN PLACE (idempotent, marker houseAds._related = "<host>:<Version>"):
//   * pool: each format's sequence gets the other format's creatives it lacks (older backends served a different window
//     per format, so a host's closest games could sit in the OTHER format's list);
//   * drops the host itself and kids games (never self-promotion);
//   * orders by Related[host] (ids missing from this table — newer games — after the known ones, in backend order, never
//     cut) and keeps them ALL when TopN = 0 (owner 2026-10-28: every one of our games in every other game), else TopN.
// The table is HouseRelatedTable.g.cs (generated from games/tools/ads/house_related.py).
using System.Collections.Generic;

namespace CoddyKit.Ads
{
    public static partial class HouseRelated
    {
        public const string Mark = "_related";

        public static int Rank(string host, string id)
        {
            if (host != null && Related.TryGetValue(host, out string[] list))
            {
                int i = System.Array.IndexOf(list, id);
                if (i >= 0) return i;
            }
            return 1000;
        }

        static bool IsNever(string id) => System.Array.IndexOf(Never, id) >= 0;

        /// <summary>Stable order: known ids by rank, then unknown ids in incoming order; never the host, a kids game or a
        /// duplicate. top > 0 keeps that many.</summary>
        public static List<object> Ordered(List<object> seq, string host, int top = TopN)
        {
            var keyed = new List<(int rank, int index, object c)>();
            var seen = new HashSet<string>();
            for (int i = 0; i < seq.Count; i++)
            {
                if (!(seq[i] is Dictionary<string, object> c)) continue;
                string id = MiniJson.Str(c, "promotedAppId");
                if (id == "" || id == host || IsNever(id) || !seen.Add(id)) continue;
                keyed.Add((Rank(host, id), i, c));
            }
            keyed.Sort((a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank) : a.index.CompareTo(b.index));
            var outList = new List<object>();
            foreach (var k in keyed)
            {
                outList.Add(k.c);
                if (top > 0 && outList.Count >= top) break;
            }
            return outList;
        }

        /// <summary>In place on an ads-config object. Returns true when it changed something; cheap when already applied.</summary>
        public static bool Apply(Dictionary<string, object> config, string host)
        {
            if (string.IsNullOrEmpty(host) || config == null) return false;
            if (!(MiniJson.Get(config, "houseAds") is Dictionary<string, object> h)) return false;
            string mark = host + ":" + Version;
            if (MiniJson.Str(h, Mark) == mark) return false;
            string[] fmts = { "banner", "interstitial" };
            var src = new Dictionary<string, List<object>>();
            foreach (string f in fmts)
                src[f] = (MiniJson.Get(h, f) is Dictionary<string, object> fc ? MiniJson.Arr(fc, "sequence") : null) ?? new List<object>();
            foreach (string f in fmts)
            {
                if (!(MiniJson.Get(h, f) is Dictionary<string, object> fc)) continue;
                string other = f == fmts[0] ? fmts[1] : fmts[0];
                var pooled = new List<object>(src[f]);
                pooled.AddRange(src[other]);
                fc["sequence"] = Ordered(pooled, host);
            }
            h[Mark] = mark;
            return true;
        }
    }
}
