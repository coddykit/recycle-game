// CoddyKit (Unity) — purchase state that must work offline (RevenueCat is the source of truth when reachable).
//   EntitlementCache: the active RC entitlement ids, persisted, so a player who bought Remove Ads stays ad-free on a plane.
//                     ad_free = ANY of the game's ad-free entitlements (games/CLAUDE.md §3: Remove Ads + big packs only —
//                     never "bought anything"). Volt City: entitlement `noads` (granted by noads / premium / grid_pass /
//                     bundle, docs/IAP.md). A fresh CustomerInfo REPLACES the set (refunds remove rights), an error keeps it.
//   ConsumableLedger: consumables are delivered once per store transaction id, never restored, <= N per UTC day.
using System.Collections.Generic;

namespace CoddyKit
{
    public sealed class EntitlementCache
    {
        public readonly HashSet<string> Active = new HashSet<string>();
        public readonly HashSet<string> AdFreeEntitlements;
        public double UpdatedAt;

        public EntitlementCache(IEnumerable<string> adFreeEntitlements)
        {
            AdFreeEntitlements = new HashSet<string>(adFreeEntitlements ?? new string[0]);
        }

        public bool Has(string entitlement) => Active.Contains(entitlement);

        public bool AdFree
        {
            get
            {
                foreach (string e in AdFreeEntitlements) if (Active.Contains(e)) return true;
                return false;
            }
        }

        /// <summary>A fresh CustomerInfo from the store: replaces the set. Returns true when something changed.</summary>
        public bool Replace(IEnumerable<string> active, double now)
        {
            var fresh = new HashSet<string>(active ?? new string[0]);
            UpdatedAt = now;
            if (fresh.SetEquals(Active)) return false;
            Active.Clear();
            Active.UnionWith(fresh);
            return true;
        }

        public Dictionary<string, object> ToJson()
        {
            var list = new List<object>();
            foreach (string e in Active) list.Add(e);
            list.Sort((a, b) => string.CompareOrdinal((string)a, (string)b));
            return new Dictionary<string, object> { { "v", 1L }, { "active", list }, { "updated_at", UpdatedAt } };
        }

        public void FromJson(Dictionary<string, object> j)
        {
            Active.Clear();
            if (j == null) return;
            foreach (object o in MiniJson.Arr(j, "active") ?? new List<object>())
                if (o is string s && s != "") Active.Add(s);
            UpdatedAt = MiniJson.Num(j, "updated_at");
        }
    }

    public sealed class ConsumableLedger
    {
        public int DailyCap = 3;
        /// <summary>transaction id -> unix time delivered.</summary>
        public readonly Dictionary<string, double> Delivered = new Dictionary<string, double>();

        /// <summary>"" when this transaction may be delivered now (then call Deliver), else the reason.</summary>
        public string Blocker(string transactionId, double now)
        {
            if (string.IsNullOrEmpty(transactionId)) return "no_transaction_id";
            if (Delivered.ContainsKey(transactionId)) return "already_delivered";
            if (DailyCap > 0 && CountOn(UtcDay.Of(now)) >= DailyCap) return "daily_cap";
            return "";
        }

        public bool Deliver(string transactionId, double now)
        {
            if (Blocker(transactionId, now) != "") return false;
            Delivered[transactionId] = now;
            return true;
        }

        /// <summary>The daily cap is a PRE-purchase gate (the shop disables the pack): true = no more packs today.</summary>
        public bool AtCap(double now) => DailyCap > 0 && CountOn(UtcDay.Of(now)) >= DailyCap;

        /// <summary>A store-CONFIRMED (charged) transaction: recorded once, never refused by the daily cap — a paid pack is
        /// always delivered (Volt City P4 fix: the cap used to swallow a 4th pack that the store had already charged).
        /// "" = record it now, "already_delivered" / "no_transaction_id" = do not deliver.</summary>
        public string ConfirmedBlocker(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId)) return "no_transaction_id";
            return Delivered.ContainsKey(transactionId) ? "already_delivered" : "";
        }

        public bool Record(string transactionId, double now)
        {
            if (ConfirmedBlocker(transactionId) != "") return false;
            Delivered[transactionId] = now;
            return true;
        }

        int CountOn(string day)
        {
            int n = 0;
            foreach (var kv in Delivered) if (UtcDay.Of(kv.Value) == day) n++;
            return n;
        }

        public Dictionary<string, object> ToJson()
        {
            var m = new Dictionary<string, object>();
            foreach (var kv in Delivered) m[kv.Key] = kv.Value;
            return new Dictionary<string, object> { { "v", 1L }, { "delivered", m } };
        }

        public void FromJson(Dictionary<string, object> j)
        {
            Delivered.Clear();
            if (MiniJson.Obj(j, "delivered") is Dictionary<string, object> m)
                foreach (var kv in m) Delivered[kv.Key] = MiniJson.Num(kv.Value);
        }
    }
}
