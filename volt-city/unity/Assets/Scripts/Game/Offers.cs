// Volt City P4 — when a one-off offer may appear (port of games/tools/shop IapOffers.gd + docs/IAP.md section 2). Pure.
//   starter    real days 3-7 of play, once per game          (daily check, Event "")
//   bundle     from day 20, <= 2 per game                     (daily check)
//   comeback   first session after >= 3 days away, <= 2/game  (Event "comeback", AwayDays)
//   storm_kit  shift report after a 1-star storm shift, once per chapter (Event "storm_report", Stars, Chapter)
// General: never in the guide, never in the first session, never during a live shift; <= 1 offer per session; no offer
// at all for 3 days after the last one; a permanent product the player already owns (or a pack already covering it) is
// never offered. The record lives in the player's settings file (persisted; a restart never re-arms an offer).
using System;
using System.Collections.Generic;
using CoddyKit;

namespace VoltCity.Game
{
    public sealed class OfferContext
    {
        /// <summary>Whole real days since the first launch (0 = first day).</summary>
        public int Day;
        public string Event = "";
        public bool Tutorial, FirstSession, LiveShift;
        public int AwayDays, Stars = 3, Chapter;
    }

    public sealed class OfferRecord
    {
        public int LastDay = -999;
        public readonly Dictionary<string, int> Count = new Dictionary<string, int>();
        /// <summary>"slug@chapter" -> shown count.</summary>
        public readonly Dictionary<string, int> PerChapter = new Dictionary<string, int>();
        /// <summary>Offers shown in this app session (not persisted).</summary>
        public int ThisSession;

        public int Shown(string slug) => Count.TryGetValue(slug, out int n) ? n : 0;

        public Dictionary<string, object> ToJson()
        {
            var c = new Dictionary<string, object>();
            foreach (var kv in Count) c[kv.Key] = (long)kv.Value;
            var pc = new Dictionary<string, object>();
            foreach (var kv in PerChapter) pc[kv.Key] = (long)kv.Value;
            return new Dictionary<string, object> { { "last_day", (long)LastDay }, { "count", c }, { "chapter", pc } };
        }

        public static OfferRecord FromJson(Dictionary<string, object> j)
        {
            var r = new OfferRecord();
            if (j == null) return r;
            r.LastDay = MiniJson.Int(j, "last_day", -999);
            if (MiniJson.Obj(j, "count") is Dictionary<string, object> c) foreach (var kv in c) r.Count[kv.Key] = (int)MiniJson.Num(kv.Value);
            if (MiniJson.Obj(j, "chapter") is Dictionary<string, object> pc) foreach (var kv in pc) r.PerChapter[kv.Key] = (int)MiniJson.Num(kv.Value);
            return r;
        }
    }

    public static class Offers
    {
        /// <summary>The offer to show now ("" = none). has(entitlement) = the player's active rights.</summary>
        public static string Pick(IapCatalog cat, OfferRecord rec, OfferContext ctx, Func<string, bool> has)
        {
            if (cat == null || rec == null || ctx == null) return "";
            if (ctx.Tutorial || ctx.FirstSession || ctx.LiveShift) return "";
            if (rec.ThisSession >= Math.Max(1, cat.OffersPerSession)) return "";
            if (ctx.Day - rec.LastDay < cat.OfferCooldownDays) return "";
            foreach (string slug in cat.Order)
            {
                if (!cat.Offers.TryGetValue(slug, out var r)) continue;
                if (!cat.Products.TryGetValue(slug, out var p)) continue;
                if (!p.Consumable && cat.Owned(slug, has)) continue;
                if ((r.Event ?? "") != (ctx.Event ?? "")) continue;
                if (r.Event == "" && (ctx.Day < r.DayMin || ctx.Day > r.DayMax)) continue;
                if (r.AwayDaysMin > 0 && ctx.AwayDays < r.AwayDaysMin) continue;
                if (r.Event == "storm_report" && ctx.Stars > r.StarsMax) continue;
                if (rec.Shown(slug) >= r.PerGame) continue;
                if (r.PerChapter > 0 && (rec.PerChapter.TryGetValue(slug + "@" + ctx.Chapter, out int pc) ? pc : 0) >= r.PerChapter) continue;
                return slug;
            }
            return "";
        }

        /// <summary>The offer was put in front of the player (whatever they chose).</summary>
        public static void Shown(OfferRecord rec, string slug, OfferContext ctx)
        {
            rec.LastDay = ctx.Day;
            rec.Count[slug] = rec.Shown(slug) + 1;
            string k = slug + "@" + ctx.Chapter;
            rec.PerChapter[k] = (rec.PerChapter.TryGetValue(k, out int n) ? n : 0) + 1;
            rec.ThisSession++;
        }

        /// <summary>A storm shift = the report lists a storm event (thunderstorm today; any *storm* id later).</summary>
        public static bool StormShift(IList<string> events)
        {
            if (events == null) return false;
            foreach (string e in events) if (e != null && e.Contains("storm")) return true;
            return false;
        }
    }
}
