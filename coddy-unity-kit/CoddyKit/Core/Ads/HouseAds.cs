// CoddyKit (Unity) — house ads (our other games only; owner 2026-10-25, Emir #125: NO AdMob interstitial / banner, no
// fallback — house pool empty / budget spent / images not cached = no ad). Pure ports of CoddyAds.gd + CoddyBanner.gd:
//   * HouseAdsConfig      — the `ads-config` edge-function payload (adsEnabled, version, houseAds.{banner,interstitial},
//                           rewardedEnabled, rcAdTracking) + eligibility of creatives;
//   * HouseInterstitialPacing — INSTALL_GRACE_S / SESSION_GRACE_S / MIN_GAP_S, quiet_until after a rewarded video,
//                           perAppBudget × min(sequence, 10) per round, reset after resetDays, wiped when the config version changes,
//                           persisted `count` = the rotation cursor;
//   * HouseBannerRotation — 1 tile (narrow portrait) or 2 (landscape / >= 600 dp), never the same game twice / never the
//                           host, persisted index (+tiles every ROTATE_S and every launch), perAppBudget per tile, new round
//                           when too few games have budget left, calm moment before a NEW bar, tap grace after it appears;
//   * BannerMetrics        — ~58 dp tall, <= 12 % of the canvas, Install >= 44 dp when the cap allows.
// Protocol (same Supabase edge functions + headers as the Godot kit): GET {SUPABASE}/functions/v1/ads-config,
// POST {SUPABASE}/functions/v1/ads-track/{impression|click} with X-App-ID / apikey / Bearer anon key (public by design,
// RLS). Impression bodies: interstitial {promotedAppId, format:"interstitial", level:1, placement, deviceId}; banner
// {promotedAppId, format:"banner", level:3, slot, deviceId}; click {deviceId, promotedAppId, destinationType:"store",
// format, level, platform, deviceModel} -> response {androidReferrer?}.
using System;
using System.Collections.Generic;

namespace CoddyKit.Ads
{
    public static class HouseAdsProtocol
    {
        public const string Supabase = "https://zkzmcpjowgttetrscpjt.supabase.co";
        public const string AnonKey = "sb_publishable_NmKNLcE663RhBtAyKijTQg_p4xDUSmQ"; // public by design (RLS), same as the Godot kit
        public const string ConfigUrl = Supabase + "/functions/v1/ads-config";
        public const string TrackUrl = Supabase + "/functions/v1/ads-track/";
        public const double ConfigRetryS = 10.0;
        public const double ConfigRetryMaxS = 300.0;

        public static Dictionary<string, string> Headers(string appId)
        {
            return new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "X-App-ID", appId ?? "" },
                { "apikey", AnonKey },
                { "Authorization", "Bearer " + AnonKey },
            };
        }

        public static Dictionary<string, object> InterstitialImpression(string deviceId, string promotedAppId, string placement)
        {
            return new Dictionary<string, object>
            {
                { "promotedAppId", promotedAppId }, { "format", "interstitial" }, { "level", 1L }, { "placement", placement ?? "" },
                { "deviceId", deviceId },
            };
        }

        public static Dictionary<string, object> BannerImpression(string deviceId, string promotedAppId, int slot)
        {
            return new Dictionary<string, object>
            {
                { "promotedAppId", promotedAppId }, { "format", "banner" }, { "level", 3L }, { "slot", (long)slot }, { "deviceId", deviceId },
            };
        }

        public static Dictionary<string, object> Click(string deviceId, string promotedAppId, string format, int level, string platform, string model)
        {
            return new Dictionary<string, object>
            {
                { "deviceId", deviceId }, { "promotedAppId", promotedAppId }, { "destinationType", "store" }, { "format", format },
                { "level", (long)level }, { "platform", platform }, { "deviceModel", model ?? "" },
            };
        }

        /// <summary>Next retry delay after a failed config fetch (10 s doubling to 300 s).</summary>
        public static double NextRetry(double current) => Math.Min(Math.Max(current, ConfigRetryS) * 2.0, ConfigRetryMaxS);
    }

    public sealed class HouseAdsConfig
    {
        /// <summary>The live / cached payload when adsEnabled, else null (no house ads at all).</summary>
        public Dictionary<string, object> Config;
        public bool RewardedEnabled = true;
        public bool RcAdTracking = true;
        public string Version = "";

        /// <summary>Applies a payload (cached or live). Returns true when the config version changed (counters reset).</summary>
        public bool Apply(Dictionary<string, object> j, string previousVersion)
        {
            if (j == null) return false;
            RcAdTracking = MiniJson.Bool(j, "rcAdTracking", true);
            RewardedEnabled = MiniJson.Bool(j, "rewardedEnabled", true);
            if (!MiniJson.Bool(j, "adsEnabled", false))
            {
                Config = null;
                return false;
            }
            Config = j;
            Version = MiniJson.Str(j, "version");
            return Version != "" && Version != (previousVersion ?? "");
        }

        public Dictionary<string, object> Format(string format, string hostAppId)
        {
            if (Config == null) return null;
            HouseRelated.Apply(Config, hostAppId);
            return MiniJson.Get(MiniJson.Obj(Config, "houseAds"), format) as Dictionary<string, object>;
        }

        /// <summary>Creatives of a format that may be shown on this host (enabled, not the host, store link for the platform,
        /// interstitial: a hero frame or an http icon; banner: an icon url).</summary>
        public List<Dictionary<string, object>> Sequence(string format, string hostAppId, string platform = "android")
        {
            var outList = new List<Dictionary<string, object>>();
            var cfg = Format(format, hostAppId);
            if (cfg == null || !MiniJson.Bool(cfg, "enabled", false)) return outList;
            foreach (object o in MiniJson.Arr(cfg, "sequence") ?? new List<object>())
            {
                if (!(o is Dictionary<string, object> c)) continue;
                if (MiniJson.Str(c, "promotedAppId") == hostAppId || HouseText.StoreUrl(c, platform) == "") continue;
                bool icon = MiniJson.Str(c, "iconUrl").StartsWith("http", StringComparison.Ordinal);
                if (format == "interstitial" && HouseText.HeroUrls(c).Count == 0 && !icon) continue;
                if (format == "banner" && !icon) continue;
                outList.Add(c);
            }
            return outList;
        }

        public int PerAppBudget(string format, string host) => MiniJson.Int(Format(format, host), "perAppBudget", 0);
        public int ResetDays(string format, string host) => MiniJson.Int(Format(format, host), "resetDays", 14);
    }

    /// <summary>Interstitial pacing + rotation cursor. State keys match the Godot kit's user://coddyads/state.json.</summary>
    public sealed class HouseInterstitialPacing
    {
        public const double InstallGraceS = 600.0;
        public const double SessionGraceS = 300.0;
        public const double MinGapS = 240.0;

        public readonly Dictionary<string, object> State;
        public double SessionS;
        public double QuietUntil;

        public HouseInterstitialPacing(Dictionary<string, object> state)
        {
            State = state ?? new Dictionary<string, object>();
            foreach (var kv in new Dictionary<string, object> { { "device_id", "" }, { "count", 0L }, { "reset_at", 0.0 }, { "version", "" }, { "play_s", 0.0 }, { "last_shown", 0.0 } })
                if (!State.ContainsKey(kv.Key)) State[kv.Key] = kv.Value;
        }

        public string DeviceId => MiniJson.Str(State, "device_id");
        public double PlayS => MiniJson.Num(State, "play_s");
        public long Count => MiniJson.Long(State, "count");

        public bool EnsureDeviceId(Func<string> make)
        {
            if (DeviceId != "") return false;
            State["device_id"] = make();
            return true;
        }

        /// <summary>Unpaused, foreground play time.</summary>
        public void Tick(double dt)
        {
            if (dt <= 0 || dt > 5) return; // a resume after hours is not play time
            SessionS += dt;
            State["play_s"] = PlayS + dt;
        }

        public void OnVersionChanged(string version)
        {
            State["version"] = version;
            State["count"] = 0L;
            State["reset_at"] = 0.0;
        }

        /// <summary>"" when an interstitial may be asked for now, else the reason (tests, debug overlay).</summary>
        public string Blocker(double now, bool adFree, bool showing)
        {
            if (showing) return "showing";
            if (adFree) return "ad_free";
            if (PlayS < InstallGraceS) return "install_grace";
            if (SessionS < SessionGraceS) return "session_grace";
            if (now - MiniJson.Num(State, "last_shown") < MinGapS) return "min_gap";
            if (now < QuietUntil) return "quiet_after_rewarded";
            return "";
        }

        /// <summary>Interstitials per round = perAppBudget × min(sequence, HouseCapGames) (owner 2026-10-28, CoddyAds.gd
        /// HOUSE_CAP_GAMES): the sequence is now every one of our other games (HouseRelated.TopN = 0), but the per-host
        /// interstitial total stays at the old 10-game maximum (budget 3 → 30 per resetDays, not 60).</summary>
        public const int HouseCapGames = 10;

        /// <summary>The next creative (perAppBudget × min(sequence, HouseCapGames) per round; null = round spent). Mutates
        /// reset_at / count at a reset.</summary>
        public Dictionary<string, object> Next(List<Dictionary<string, object>> seq, int perAppBudget, int resetDays, double now)
        {
            if (seq == null || seq.Count == 0 || perAppBudget <= 0) return null;
            if (now >= MiniJson.Num(State, "reset_at"))
            {
                State["count"] = 0L;
                State["reset_at"] = resetDays > 0 ? now + resetDays * 86400.0 : 1.0e15;
            }
            long total = (long)perAppBudget * Math.Min(seq.Count, HouseCapGames);
            long count = Count;
            if (count >= total) return null;
            long rotation = total > 0 ? count / total : 0;
            return seq[(int)((count + rotation) % seq.Count)];
        }

        public void MarkShown(double now)
        {
            State["count"] = Count + 1;
            State["last_shown"] = now;
        }

        /// <summary>§4.6: a completed rewarded video postpones the next interstitial of the session by one interval.</summary>
        public void RewardedCompleted(double now)
        {
            QuietUntil = Math.Max(Math.Max(now, MiniJson.Num(State, "last_shown") + MinGapS), QuietUntil) + MinGapS;
        }
    }

    /// <summary>Bottom house banner selection + anti-mistap timing (CoddyBanner.gd).</summary>
    public sealed class HouseBannerRotation
    {
        public const double RotateS = 45.0;
        public const double TapGraceS = 1.5;
        public const double CalmS = 3.0;
        public const float DualMinDp = 600f;

        /// <summary>Persisted: next launch starts after the creatives shown last.</summary>
        public int Index;
        /// <summary>Persisted per round: promotedAppId -> impressions.</summary>
        public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        public List<string> ShownIds = new List<string>();
        double rot;
        double visibleSince = -1e9;
        double lastInput = -1e9;
        int fingers;

        public bool OnPhone = true;

        public void Tick(double dt) { rot += dt; }

        public void Input(double now, bool down, bool up)
        {
            lastInput = now;
            if (down) fingers++;
            if (up) fingers = Math.Max(0, fingers - 1);
        }

        public bool Calm(double now) => !OnPhone || (fingers == 0 && now - lastInput >= CalmS);

        /// <summary>A tap on the bar right after it appeared / moved was meant for what used to be there: swallow it.</summary>
        public bool TapAccepted(double now) => !OnPhone || now - visibleSince >= TapGraceS;

        public void LayoutMoved(double now) { visibleSince = now; }

        public static bool Dual(float canvasW, float canvasH, float pxPerDp)
        {
            return canvasW > canvasH || canvasW / Math.Max(pxPerDp, 0.05f) >= DualMinDp;
        }

        /// <summary>The 1 or 2 creatives to show now. Advances by the number of tiles every RotateS.</summary>
        public List<Dictionary<string, object>> Current(List<Dictionary<string, object>> ready, int tiles, int perAppBudget)
        {
            var outList = new List<Dictionary<string, object>>();
            if (ready == null || ready.Count == 0) return outList;
            int n = Math.Max(1, Math.Min(tiles, ready.Count));
            if (rot >= RotateS)
            {
                rot = 0;
                Index += n;
            }
            if (perAppBudget > 0)
            {
                int withBudget = 0;
                foreach (var c in ready) if (CountOf(MiniJson.Str(c, "promotedAppId")) < perAppBudget) withBudget++;
                if (withBudget < n) Counts.Clear(); // new round: the budget is a fair share, it never empties the bar
            }
            var ids = new List<string>();
            for (int k = 0; k < ready.Count; k++)
            {
                var c = ready[(int)(((long)Index + k) % ready.Count)];
                string id = MiniJson.Str(c, "promotedAppId");
                if (ids.Contains(id) || (perAppBudget > 0 && CountOf(id) >= perAppBudget)) continue;
                outList.Add(c);
                ids.Add(id);
                if (outList.Count >= n) break;
            }
            return outList;
        }

        int CountOf(string id) => Counts.TryGetValue(id, out int v) ? v : 0;

        /// <summary>Call when the bar shows `shown`. Returns (slot, id) pairs that are NEW impressions (a relayout of the same
        /// games is not one). A bar that was hidden before starts its tap grace.</summary>
        public List<(int slot, string id)> OnShown(List<Dictionary<string, object>> shown, double now, bool wasHidden)
        {
            var ids = new List<string>();
            foreach (var c in shown) ids.Add(MiniJson.Str(c, "promotedAppId"));
            if (wasHidden) visibleSince = now;
            var fresh = new List<(int, string)>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (ShownIds.Contains(ids[i])) continue;
                Counts[ids[i]] = CountOf(ids[i]) + 1;
                fresh.Add((i, ids[i]));
            }
            ShownIds = ids;
            return fresh;
        }

        public void OnHidden() { ShownIds = new List<string>(); }

        /// <summary>The index to persist (next launch starts after the creatives on screen now).</summary>
        public int IndexToPersist => Index + Math.Max(1, ShownIds.Count);

        public Dictionary<string, object> ToJson()
        {
            var counts = new Dictionary<string, object>();
            foreach (var kv in Counts) counts[kv.Key] = (long)kv.Value;
            return new Dictionary<string, object> { { "index", (long)IndexToPersist }, { "counts", counts } };
        }

        public void FromJson(Dictionary<string, object> j)
        {
            if (j == null) return;
            Index = Math.Max(0, MiniJson.Int(j, "index"));
            Counts.Clear();
            if (MiniJson.Obj(j, "counts") is Dictionary<string, object> m)
                foreach (var kv in m) Counts[kv.Key] = (int)MiniJson.Num(kv.Value);
        }
    }

    /// <summary>House bar sizing (CoddyBanner.house_metrics): all values in canvas px; u = canvas px per dp.</summary>
    public struct BannerMetrics
    {
        const double MaxShareD = 0.12;
        public const float HouseDp = 58f, MaxShare = 0.12f, TouchDp = 44f, TitleDp = 16f, TitleMinDp = 14f, SubDp = 12f, SubMinDp = 11f;

        public float Height, Dp, Share, BtnH, VPad, HPad, Gap, Icon, TitlePx, SubPx, BtnPx, BtnPad;
        public bool ShowSub;

        public static BannerMetrics Compute(float canvasW, float canvasH, float u)
        {
            u = Math.Max(u, 0.05f);
            // double maths like the Godot kit: Mono (editor) evaluates float*float in double precision, so 0.12f * 400
            // floored to 47 there and 48 under CoreCLR / IL2CPP
            float cap = (float)Math.Floor(MaxShareD * canvasH);
            float h = Math.Min((float)Math.Round(HouseDp * u), cap);
            float k = h / (HouseDp * u);
            float btnH = Math.Min((float)Math.Round(TouchDp * u), h);
            float vpad = (float)Math.Floor((h - btnH) / 2f);
            float title = Math.Max((float)Math.Round(TitleDp * u * k), (float)Math.Round(TitleMinDp * u));
            float sub = Math.Max((float)Math.Round(SubDp * u * k), (float)Math.Round(SubMinDp * u));
            const float line = 1.3f;
            float inner = h - 2f * Math.Max(vpad, (float)Math.Round(2f * u));
            bool showSub = (title + sub) * line <= inner;
            if (title * line > h) title = (float)Math.Floor(h / line);
            return new BannerMetrics
            {
                Height = h, Dp = h / u, Share = h / Math.Max(canvasH, 1f), BtnH = btnH, VPad = vpad, HPad = (float)Math.Round(10f * u),
                Gap = (float)Math.Round(10f * u), Icon = btnH, TitlePx = title, SubPx = sub, ShowSub = showSub,
                BtnPx = Math.Max((float)Math.Round(15f * u * k), (float)Math.Round(13f * u)), BtnPad = (float)Math.Round(12f * u * k),
            };
        }
    }
}
