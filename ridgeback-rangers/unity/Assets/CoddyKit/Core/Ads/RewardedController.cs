// CoddyKit (Unity) — the rewarded-video flow, engine-free so EditMode tests drive it with the fakes (CoddyAds.gd W45):
//   Start():   UMP consent first (IConsentProvider.Gather) -> only when ads may be requested AND a unit exists AND
//              ads-config rewardedEnabled is not false: IRewardedProvider.Initialize (the ONE MobileAds.Initialize call
//              site of the game) -> Load. Nothing is ever loaded before Initialize completed.
//   Ready():   the offer button may be shown (ad loaded, or ad_free; caps / cooldown / budget allow). Logs
//              rewarded_offer_shown once per placement per session.
//   Show():    ad_free players get the capped reward at once without a video (rewarded_skipped_paid_user); everyone
//              else watches; the reward is granted once, only after earned AND closed; a completed video postpones the
//              next house interstitial (OnVideoCompleted -> HouseInterstitialPacing.RewardedCompleted).
//   Failed loads retry after 60 s (Schedule). RC ad tracking events go through RcAdTracker (never house ads, never
//   video-less rewards).
using System;
using System.Collections.Generic;

namespace CoddyKit.Ads
{
    public sealed class RewardedController
    {
        public const string TestUnitAndroid = "ca-app-pub-3940256099942544/5224354917";
        public const double RetryS = 60.0;

        public readonly RewardedRules Rules;
        public Func<bool> AdFree = () => false;
        public bool RemoteEnabled = true;
        public bool Mobile = true;
        public string UnitId = "";
        /// <summary>Pause / resume the game around a video (true = pause).</summary>
        public Action<bool> PauseGame = _ => { };
        /// <summary>Run an action after N seconds (runtime: a coroutine / timer; tests: a list).</summary>
        public Action<double, Action> Schedule = (s, a) => { };
        public Action OnVideoCompleted = () => { };
        public Action Persist = () => { };
        public event Action Changed;

        readonly IConsentProvider consent;
        readonly IRewardedProvider ads;
        readonly IAnalytics analytics;
        readonly RcAdTracker rc;
        readonly HashSet<string> offered = new HashSet<string>();
        bool started, loading, active;

        public bool Showing => active;

        public RewardedController(RewardedRules rules, IConsentProvider consent, IRewardedProvider ads, IAnalytics analytics, RcAdTracker rc)
        {
            Rules = rules;
            this.consent = consent;
            this.ads = ads;
            this.analytics = analytics;
            this.rc = rc;
            if (ads != null) ads.Tracked += e => rc?.Track(e);
        }

        /// <summary>The unit for this build: debug builds ALWAYS use Google's test unit; release uses the configured id
        /// (empty = no rewarded video at all, offers hidden for non-ad_free players).</summary>
        public static string UnitFor(string releaseUnit, bool debugBuild) => debugBuild ? TestUnitAndroid : (releaseUnit ?? "");

        bool VideoOn => Mobile && RemoteEnabled && !string.IsNullOrEmpty(UnitId) && consent != null && consent.CanRequestAds && ads != null;

        public void Start()
        {
            if (started || !Mobile || ads == null || consent == null) return;
            started = true;
            consent.Gather((can, err) =>
            {
                if (!can || string.IsNullOrEmpty(UnitId) || !RemoteEnabled) { Changed?.Invoke(); return; }
                if (ads.Initialized) { Load(); return; }
                ads.Initialize(() => { Load(); Changed?.Invoke(); });
            });
        }

        /// <summary>ads-config rewardedEnabled changed (cached / live payload).</summary>
        public void SetRemote(bool enabled)
        {
            if (RemoteEnabled == enabled) return;
            RemoteEnabled = enabled;
            if (enabled && started && ads.Initialized) Load();
            Changed?.Invoke();
        }

        void Load()
        {
            if (!ads.Initialized || loading || ads.IsLoaded || !VideoOn || AdFree()) return;
            loading = true;
            rc?.Flush();
            ads.Load(UnitId, (ok, err) =>
            {
                loading = false;
                if (!ok) Schedule(RetryS, Load);
                Changed?.Invoke();
            });
        }

        public bool Ready(string placement, int wanted = 1)
        {
            if (active || !Rules.Allows(placement, wanted)) return false;
            bool ok = Mobile && (AdFree() || (VideoOn && ads.IsLoaded));
            if (ok && offered.Add(placement)) Log(RewardedEvents.OfferShown, Rules.Params(placement, Rules.Amount(wanted)));
            return ok;
        }

        /// <summary>False = not available now (nothing happens). onEarned(units) runs once.</summary>
        public bool Show(string placement, int wanted, Action<int> onEarned, Action onClosed = null)
        {
            if (!Ready(placement, wanted)) return false;
            int units = Rules.Amount(wanted);
            var p = Rules.Params(placement, units);
            Log(RewardedEvents.OfferAccepted, p);
            if (AdFree())
            {
                Rules.Count(placement, units);
                offered.Remove(placement);
                Persist();
                Log(RewardedEvents.SkippedPaidUser, p);
                Log(RewardedEvents.Completed, p);
                onEarned?.Invoke(units);
                onClosed?.Invoke();
                Changed?.Invoke();
                return true;
            }
            active = true;
            PauseGame(true);
            var st = new RewardedShowState();
            Action grant = () =>
            {
                Log(RewardedEvents.Completed, p);
                OnVideoCompleted();
                onEarned?.Invoke(units);
            };
            Action finish = () =>
            {
                active = false;
                PauseGame(false);
                Load();
                Changed?.Invoke();
            };
            ads.Show(placement,
                () =>
                {
                    if (!st.Earned && !st.Failed)
                    {
                        Rules.Count(placement, units);
                        offered.Remove(placement);
                        Persist();
                    }
                    if (st.OnEarned()) grant();
                },
                () =>
                {
                    bool g = st.OnClosed();
                    finish();
                    if (g) grant();
                    onClosed?.Invoke();
                },
                err =>
                {
                    st.OnFailed();
                    st.OnClosed();
                    finish();
                    onClosed?.Invoke();
                });
            Changed?.Invoke();
            return true;
        }

        void Log(string name, Dictionary<string, object> p) => analytics?.Event(name, new Dictionary<string, object>(p));
    }

    /// <summary>RevenueCat ad tracking helper (port of RcAdTracking.gd semantics, games/CLAUDE.md §5b): dedupes callbacks
    /// per ad instance + kind (revenue per ad instance + value; load failures are never deduped), keeps a bounded queue
    /// while RC is not configured, `rcAdTracking: false` (ads-config) drops the queue and stops sending. House ads and
    /// video-less rewards never reach it.</summary>
    public sealed class RcAdTracker
    {
        public const int QueueMax = 50;
        public bool Enabled = true;
        public Func<bool> Ready = () => false;
        public Func<AdTrackEvent, bool> Sink = _ => false;
        public readonly List<AdTrackEvent> Queue = new List<AdTrackEvent>();
        readonly HashSet<string> seen = new HashSet<string>();
        public int Sent;
        public bool SinkUnsupported;

        public void SetEnabled(bool on)
        {
            Enabled = on;
            if (!on) Queue.Clear();
            else Flush();
        }

        public void Track(AdTrackEvent e)
        {
            if (!Enabled || string.IsNullOrEmpty(e.Kind)) return;
            if (e.Kind == "revenue" && e.ValueMicros <= 0 && string.IsNullOrEmpty(e.Currency)) return; // no invented revenue
            string key = (e.AdId ?? e.Unit ?? "") + "|" + e.Kind + (e.Kind == "revenue" ? "|" + e.ValueMicros : "");
            if (e.Kind != "failed_to_load" && !seen.Add(key)) return;
            Queue.Add(e);
            if (Queue.Count > QueueMax) Queue.RemoveAt(0);
            Flush();
        }

        public void Flush()
        {
            if (!Enabled || !Ready()) return;
            while (Queue.Count > 0)
            {
                var e = Queue[0];
                Queue.RemoveAt(0);
                if (Sink(e)) Sent++;
                else SinkUnsupported = true;
            }
        }

        /// <summary>IAP switches the RevenueCat user: the old user's queue is dropped (RcAdTracking.set_user).</summary>
        public void UserChanged() { Queue.Clear(); seen.Clear(); }
    }
}
