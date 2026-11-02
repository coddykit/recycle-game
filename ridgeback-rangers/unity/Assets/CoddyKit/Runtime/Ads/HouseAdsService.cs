// CoddyKit (Unity) — house ads runtime (the Unity CoddyAds): ads-config fetch (+ disk cache + 10 s..300 s retry), the
// interstitial pacing / rotation (HouseInterstitialPacing), telemetry to ads-track, store opening with the Play referrer,
// image prefetch, and the banner + interstitial views. HOUSE ONLY: no AdMob interstitial / banner exists in this kit.
// Mobile only (Android); editor / desktop = no-op unless ForceDesktop (previews, tests). UNCOMPILED until the editor exists.
//   Game code:  Kit.Ads.Interstitial("shift_report")   // at a natural break (Volt City: the shift report after Collect)
//               Kit.Ads.BannerHeightChanged += px => <lift the tab bar / sheets>; content bottom = max(safe, px)
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CoddyKit.Ads
{
    public sealed class HouseAdsService : MonoBehaviour
    {
        public KitConfig Config;
        public Func<bool> AdFree = () => false;
        public bool ForceDesktop;
        public readonly HouseAdsConfig Remote = new HouseAdsConfig();
        public HouseInterstitialPacing Pacing { get; private set; }
        public HouseImageCache Images { get; private set; }
        public HouseBannerView Banner { get; private set; }
        public HouseInterstitialView Card { get; private set; }
        /// <summary>A house interstitial or a rewarded video is on screen (no banner, no second ad).</summary>
        public bool Showing;
        public float BannerHeight { get; private set; }
        public event Action<float> BannerHeightChanged;
        public event Action Closed;
        /// <summary>ads-config rewardedEnabled / rcAdTracking changed (cached or live payload).</summary>
        public event Action<HouseAdsConfig> RemoteChanged;

        string kitDir;
        JsonStateFile stateFile;
        double retryS = HouseAdsProtocol.ConfigRetryS;
        float saveTimer;
        public int ConfigRetries { get; private set; }

        public bool Mobile => ForceDesktop || Application.platform == RuntimePlatform.Android;
        double Now => (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        string Lang => HouseText.Lang2(Config?.UiLanguage?.Invoke() ?? "en");

        public void Init(KitConfig config, string dir, Func<bool> adFree, UnityEngine.UIElements.PanelSettings panel)
        {
            Config = config;
            kitDir = dir;
            AdFree = adFree ?? (() => false);
            Directory.CreateDirectory(Path.Combine(dir, "coddyads"));
            stateFile = new JsonStateFile(Path.Combine(dir, "coddyads", "state.json")).Load();
            Pacing = new HouseInterstitialPacing(stateFile.Data);
            if (Pacing.EnsureDeviceId(() => Guid.NewGuid().ToString("N"))) stateFile.Save();
            Images = new HouseImageCache(dir);
            if (!config.Kids && panel != null)
            {
                Banner = gameObject.AddComponent<HouseBannerView>();
                Banner.Init(this, panel, Path.Combine(dir, "coddyads", "banner.json"));
                Banner.HeightChanged += h =>
                {
                    BannerHeight = h;
                    BannerHeightChanged?.Invoke(h);
                };
                Card = gameObject.AddComponent<HouseInterstitialView>();
                Card.Init(this, panel);
            }
            if (!Mobile) return;
            LoadCachedConfig();
            StartCoroutine(FetchConfig());
        }

        void Update()
        {
            if (!Mobile || Showing) return;
            Pacing.Tick(Time.unscaledDeltaTime);
            saveTimer += Time.unscaledDeltaTime;
            if (saveTimer > 30f)
            {
                saveTimer = 0f;
                stateFile.Save();
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && stateFile != null) stateFile.Save();
        }

        void OnApplicationQuit()
        {
            stateFile?.Save();
        }

        // ------------------------------------------------------------ config

        string CachePath => Path.Combine(kitDir, "coddyads", "config.json");

        void LoadCachedConfig()
        {
            var j = MiniJson.TryParseObject(SaveRepository.SafeRead(CachePath));
            if (j != null) ApplyConfig(j, fromCache: true);
        }

        IEnumerator FetchConfig()
        {
            while (true)
            {
                if (string.IsNullOrEmpty(Config.AppId)) yield break;
                using (var req = UnityWebRequest.Get(HouseAdsProtocol.ConfigUrl))
                {
                    foreach (var kv in HouseAdsProtocol.Headers(Config.AppId)) req.SetRequestHeader(kv.Key, kv.Value);
                    req.timeout = 10;
                    yield return req.SendWebRequest();
                    string text = req.result == UnityWebRequest.Result.Success && req.responseCode == 200 ? req.downloadHandler.text : null;
                    var j = MiniJson.TryParseObject(text);
                    if (j != null)
                    {
                        retryS = HouseAdsProtocol.ConfigRetryS;
                        ApplyConfig(j, fromCache: false);
                        SaveRepository.Write(CachePath, text);
                        yield break;
                    }
                }
                // first launch right after install / boot: the network is often not up yet (Kaiten 2026-10-24)
                ConfigRetries++;
                yield return new WaitForSecondsRealtime((float)retryS);
                retryS = HouseAdsProtocol.NextRetry(retryS);
            }
        }

        void ApplyConfig(Dictionary<string, object> j, bool fromCache)
        {
            bool versionChanged = Remote.Apply(j, MiniJson.Str(Pacing.State, "version"));
            if (versionChanged && !fromCache)
            {
                Pacing.OnVersionChanged(Remote.Version);
                stateFile.Save();
                Banner?.ResetRound();
            }
            else if (versionChanged)
            {
                Pacing.State["version"] = Remote.Version;
            }
            RemoteChanged?.Invoke(Remote);
            PrefetchNext();
        }

        // ------------------------------------------------------------ interstitial

        /// <summary>Ask for a house interstitial at a natural break. Never blocks; nothing when pacing / ad-free / no fill.</summary>
        public void Interstitial(string placement = "")
        {
            if (!Mobile || Config.Kids || Card == null) return;
            if (Pacing.Blocker(Now, AdFree(), Showing) != "") return;
            var seq = Remote.Sequence("interstitial", Config.AppId, Config.Platform);
            var c = Pacing.Next(seq, Remote.PerAppBudget("interstitial", Config.AppId), Remote.ResetDays("interstitial", Config.AppId), Now);
            if (c == null) return; // pool empty / budget spent: no ad (no AdMob fallback, owner 2026-10-25)
            var heroes = HeroList(c);
            bool heroesUsable = heroes.Exists(u => Images.HeroOnDisk(u));
            bool allHeroesFailed = heroes.Count > 0 && heroes.TrueForAll(u => Images.Failed(u));
            string icon = MiniJson.Str(c, "iconUrl");
            bool iconReady = Images.IconReady(icon);
            if (!heroesUsable && !(iconReady && (heroes.Count == 0 || allHeroesFailed)))
            {
                Prefetch(c); // not downloaded yet: no ad this break, ready for the next one
                return;
            }
            Showing = true;
            Pacing.MarkShown(Now);
            stateFile.Save();
            Track("impression", HouseAdsProtocol.InterstitialImpression(Pacing.DeviceId, MiniJson.Str(c, "promotedAppId"), placement));
            Config.PauseGame?.Invoke(true);
            Card.Show(c, Images.LoadHeroes(heroes, Config.LowEnd()), Images.Icon(icon), Lang, opened =>
            {
                if (opened) OpenStore(c, "interstitial", 1);
                Config.PauseGame?.Invoke(false);
                Showing = false;
                PrefetchNext();
                Closed?.Invoke();
            });
        }

        public List<string> HeroList(Dictionary<string, object> c) => HouseText.HeroUrls(c, Lang, Screen.height >= Screen.width);

        void PrefetchNext()
        {
            if (!Mobile || Remote.Config == null) return;
            var seq = Remote.Sequence("interstitial", Config.AppId, Config.Platform);
            if (seq.Count == 0) return;
            var next = seq[(int)(Pacing.Count % seq.Count)];
            Prefetch(next);
            Banner?.PrefetchIcons();
        }

        void Prefetch(Dictionary<string, object> c)
        {
            foreach (string u in HeroList(c)) StartCoroutine(Images.FetchHero(u));
            StartCoroutine(Images.FetchIcon(MiniJson.Str(c, "iconUrl")));
        }

        public void RewardedCompleted() => Pacing.RewardedCompleted(Now);

        // ------------------------------------------------------------ telemetry + store

        public void Track(string kind, Dictionary<string, object> body)
        {
            if (!Mobile) return;
            StartCoroutine(Post(HouseAdsProtocol.TrackUrl + kind, body, null));
        }

        IEnumerator Post(string url, Dictionary<string, object> body, Action<string> done)
        {
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(MiniJson.Write(body)));
                req.downloadHandler = new DownloadHandlerBuffer();
                foreach (var kv in HouseAdsProtocol.Headers(Config.AppId)) req.SetRequestHeader(kv.Key, kv.Value);
                req.timeout = 8;
                yield return req.SendWebRequest();
                done?.Invoke(req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null);
            }
        }

        /// <summary>Tracks the click (format / level), then opens the Play page with the backend referrer (else utm_*).
        /// Waits at most 2 s for the click response so a slow network never blocks the store.</summary>
        public void OpenStore(Dictionary<string, object> c, string format, int level)
        {
            string url = HouseText.StoreUrl(c, Config.Platform);
            if (url == "") return;
            var body = HouseAdsProtocol.Click(Pacing.DeviceId, MiniJson.Str(c, "promotedAppId"), format, level, Config.Platform, SystemInfo.deviceModel);
            StartCoroutine(ClickThenOpen(url, body, format));
        }

        IEnumerator ClickThenOpen(string url, Dictionary<string, object> body, string format)
        {
            string response = null;
            bool done = false;
            StartCoroutine(Post(HouseAdsProtocol.TrackUrl + "click", body, r => { response = r; done = true; }));
            float waited = 0f;
            while (!done && waited < 2f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            string referrer = MiniJson.Str(MiniJson.TryParseObject(response), "androidReferrer");
            Application.OpenURL(HouseText.StoreLink(url, referrer, Config.AppId, format));
        }
    }
}
