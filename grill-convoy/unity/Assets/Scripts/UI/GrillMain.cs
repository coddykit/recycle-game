// Grill Convoy — the Region scene's entry point. Wires the engine-free game layer (GameSession + GameActions + Coach +
// Commerce + GrillRewards + ReviewHook) to the diorama (RegionWorld), the UI (GameScreen) and the CoddyUnityKit (tiers,
// purchases cache, house ads, rewarded, review, toasts):
//   * data from Resources/data, strings from Resources/i18n/strings.csv, the career from
//     persistentDataPath/grillconvoy/career.json through the kit SaveRepository (GameSession);
//   * time: the sim advances with real unscaled time; the guide pauses it while it waits for the player and runs it fast
//     while it waits for the world; full-screen ads pause it;
//   * events -> world walkers / toasts / first-time tips / review moments / offers;
//   * house banner: only after the pager + guide, never over a sheet with a purchase, the pager or the loading curtain
//     (kit rule: hidden for ad_free); the screen's bottom bar is lifted by the banner height (SafeAreaPadding);
//   * house interstitial: only at "Next day" after a report (a natural break), never in session 1 or the guide (kit pacing);
//   * career slots (Extra Saves / the Edition): Settings switches the slot, the world rebinds;
//   * save on pause / quit, offline pay on resume (welcome-back card with the ×2 rewarded opt-in).
// Opening the Region scene directly in the editor boots the kit itself.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using GrillConvoy.App;
using GrillConvoy.Game;
using GrillConvoy.Sim;
using GrillConvoy.World;

namespace GrillConvoy.UI
{
    public sealed class GrillMain : MonoBehaviour
    {
        public UIDocument uiDocument;
        public RegionWorld world;

        public GameSession Session { get; private set; }
        public Loc Loc { get; private set; }
        public GameActions Actions { get; private set; }
        public Coach Coach { get; private set; }
        public Commerce Shop { get; private set; }
        public GrillRewards Rewards { get; private set; }
        public GameScreen Screen { get; private set; }
        public GrillAudio Audio { get; private set; }
        public OfflineSummary LastOffline { get; private set; }
        public Tier CurrentTier => kit != null ? kit.Tiers.Current : Tier.Medium;
        public bool Ready { get; private set; }

        CoddyKitHost kit;
        readonly ReviewHook review = new ReviewHook();
        bool adPause, low, inBackground;
        Action perksChanged;
        Action<Tier> tierChanged;

        IEnumerator Start()
        {
            kit = CoddyKitHost.Instance;
            if (kit == null) kit = CoddyKitHost.Boot(GrillGameConfig.Kit(), uiDocument != null ? uiDocument.panelSettings : Resources.Load<PanelSettings>("CoddyPanelSettings"));
            low = kit.Tiers.Current == Tier.Low;
            UiKit.Low = low;

            var gd = GameData.FromTexts(n => Resources.Load<TextAsset>("data/" + n)?.text);
            if (!gd.Ok) Debug.LogError("[GrillConvoy] data: " + string.Join("; ", gd.Errors));
            Session = new GameSession(gd, Path.Combine(Application.persistentDataPath, "grillconvoy")) { Analytics = Track };
            Session.LoadOrNew(Math.Abs(DateTime.UtcNow.Ticks % 2147483000L) + 1);
            var st = Session.Settings;
            st.Sessions = st.Sessions + 1;
            st.Save();
            review.Deferred = st.ReviewDeferred;

            var csv = Resources.Load<TextAsset>("i18n/strings");
            Loc = Loc.FromCsv(csv != null ? csv.text : "key,en\n", st.Lang != "" ? st.Lang : GrillGameConfig.DeviceLanguage());
            if (Loc.Errors.Count > 0) Debug.LogWarning("[GrillConvoy] strings: " + string.Join("; ", Loc.Errors));
            kit.Config.UiLanguage = () => Loc.Lang;
            kit.Config.InstallLabel = () => null;
            kit.Config.LowEnd = () => kit.Tiers.Current == Tier.Low;
            kit.Config.PauseGame = p => { adPause = p; };
            kit.Config.BannerAllowed = BannerAllowed;

            // shop + rewards + perks
            var cat = IapCatalog.Parse(Resources.Load<TextAsset>("data/iap")?.text);
            if (cat.Errors.Count > 0) Debug.LogError("[GrillConvoy] iap: " + string.Join("; ", cat.Errors));
            Shop = new Commerce(cat, kit.Purchases, Session)
            {
                Analytics = Track,
                Toast = (t, k) => UiToastHost.Show(t, k == "success" ? ToastKind.Success : k == "warning" ? ToastKind.Warning : ToastKind.Info),
                LocSource = () => Loc,
                Cue = id => Audio?.Play(id),
            };
            Shop.LoadPrices();
            Session.ApplyPerks(kit.Purchases.Has);
            perksChanged = () => { Session.ApplyPerks(kit.Purchases.Has); ApplyLivery(); Screen?.Refresh(true); Screen?.MarkSheetDirty(); };
            kit.Purchases.Changed += perksChanged;
            Rewards = new GrillRewards(() => Session.Sim, kit.Rewarded) { AdFree = () => kit.Purchases.AdFree };
            Rewards.Granted += (placement, r) =>
            {
                UiToastHost.Show(GrillRewards.GrantedText(placement, r, Loc), r != null && r.Ok ? ToastKind.Success : ToastKind.Warning);
                Session.Save();
                world.MarkDirty();
            };

            // actions + guide
            Actions = new GameActions(Session) { Analytics = Track };
            Coach = new Coach
            {
                Analytics = Track,
                Persist = (step, done) => { st.CoachStep = step; st.CoachDone = done; st.Save(); },
            };
            Actions.Allowed = Coach.Allows;
            Actions.Ran += (id, r) => Coach.OnAction(Session.Sim, Actions, id, r);
            Actions.Ran += (id, r) => { Screen?.AfterAction(id, r); if (r.Ok) Audio?.Play(SoundFor(id)); };

            Audio = GrillAudio.Create(transform, st.Music, st.Effects);
            UiKit.TapSound = () => Audio.Play("ui_tap");

            // the first 3D frame runs inside the GPU breadcrumb (a shader-compiler crash -> safe shaders next launch)
            kit.Tiers.BeginShaderWork("region_first_frame");
            world.Init(Session.Sim, low);
            ApplyLivery();
            world.Picked += hit => Screen?.WorldPick(hit);
            tierChanged = t => { low = t == Tier.Low; UiKit.Low = low; world.SetLow(low); };
            kit.Tiers.Changed += tierChanged;
            kit.Tiers.SteppedDown += () => UiToastHost.Show(Loc.T("graphics.dropped"), ToastKind.Info);

            Screen = new GameScreen(uiDocument.rootVisualElement, this);
            SafeAreaPadding.Attach(uiDocument.rootVisualElement, false, () => kit.Ads != null ? kit.Ads.BannerHeight : 0f);
            if (world.tap != null)
            {
                world.tap.BlockingPanels.Add(uiDocument.rootVisualElement.panel);
                world.tap.Enabled = () => !Screen.WindowOpen;
            }
            if (world.rig != null)
            {
                world.rig.OverUi = p => Screen.OverUi(p);
                world.rig.Enabled = () => !Screen.WindowOpen;
            }
            kit.Review.Context = () => new ReviewGate.Context { Tutorial = Coach.Active, WindowOpen = Screen.WindowOpen || adPause, Background = inBackground };

            yield return null;
            kit.Tiers.EndShaderWork();
            GrillBoot.Ready();
            Track("gc_session", new Dictionary<string, object> { { "n", (long)st.Sessions }, { "tier", kit.Tiers.Current.ToString().ToLowerInvariant() } });

            // onboarding: pager (once) -> guide (until done or skipped)
            if (!st.IntroDone) Screen.ShowPager(() => { st.IntroDone = true; st.Save(); BeginGuideIfNeeded(); });
            else BeginGuideIfNeeded();
            Resume();
            Ready = true;
        }

        void BeginGuideIfNeeded()
        {
            if (Session.Settings.CoachDone) { Session.Sim.EndTutorial(); return; }
            Coach.Begin(Session.Sim, Actions, Session.Settings.CoachStep);
        }

        bool BannerAllowed()
        {
            if (!Ready || Screen == null) return false;
            if (Coach.Active || Screen.PagerOpen || !Session.Settings.IntroDone) return false;
            string k = Screen.SheetKind;
            return k != "shop" && k != "offline";
        }

        void Update()
        {
            if (!Ready) return;
            float dt = Time.unscaledDeltaTime;
            var sim = Session.Sim;
            if (Coach.Active && !Screen.PagerOpen) Coach.Drive(sim, Actions, dt);
            sim.SetPaused(adPause || Screen.PagerOpen || (Coach.Active && Coach.WantsPause(sim)));
            List<SimEvent> events = Session.Tick(dt);
            foreach (var e in events) OnEvent(e);
            Screen.Tick(dt, low);
            Audio.PlayMusic(sim.Phase == Phases.Live && !sim.S.Paused ? "music_busy" : "music_calm");
            if (!Coach.Active && !Screen.WindowOpen && sim.Phase == Phases.Live) kit.Review.TickPlay(dt);
            string deferred = review.TakeDeferred(Coach.Active);
            if (deferred != "") Wow(deferred);
        }

        void OnEvent(SimEvent e)
        {
            world.OnSimEvent(e);
            if (e.Type == "served" && e.Text != "hand") Audio.Play("coins");
            var sim = Session.Sim;
            switch (e.Type)
            {
                case "day_done":
                    Track("gc_day_done", new Dictionary<string, object> { { "day", (long)e.A }, { "stars", (long)e.B } });
                    Screen.OpenSheet("report");
                    Shop.TryOffer("day_report", Coach.Active);
                    break;
                case "opened": Audio.Play("open"); break;
                case "research_done": UiToastHost.Show(Loc.F("research.finished", "name", Loc.T("research." + e.Text + ".t")), ToastKind.Success); break;
                case "prep_done": UiToastHost.Show(Loc.F("toast.prep_done", "n", e.B, "name", Loc.T("stock." + Stock.Ids[Math.Max(0, Math.Min(Stock.N - 1, e.A))])), ToastKind.Info); break;
                case "district":
                    UiToastHost.Show(Loc.F("toast.district", "name", Loc.T("district." + e.Text)), ToastKind.Success);
                    Track("pitch_unlock", new Dictionary<string, object> { { "district", e.Text } });
                    break;
                case "truck_new": Shop.TryOffer("truck_new", Coach.Active); break;
                case "crisis":
                    if (!Coach.Active)
                    {
                        UiToastHost.Show(Loc.T("crisis." + e.Text + ".t") + " — " + Loc.T("crisis." + e.Text + ".b"), ToastKind.Warning);
                        Track("crisis", new Dictionary<string, object> { { "id", e.Text } });
                        if (e.Text == "propane") Shop.TryOffer("propane", Coach.Active);
                    }
                    Audio.Play("warning");
                    break;
                case "propane_low": Audio.Play("warning"); break;
                case "extinguished": Audio.Play("hiss"); break;
                case "all_hands": Audio.Play("bell"); break;
                case "hand": Audio.Play(e.B == 3 ? "sizzle" : "ui_tap"); break;
            }
            if (e.Type != "joined" && e.Type != "served" && e.Type != "hand") Screen.MarkSheetDirty();
            if (!Coach.Active)
            {
                string tip = Coach.TipFor(e, sim);
                if (tip != "" && !Session.Settings.HintSeen(tip))
                {
                    Session.Settings.MarkHint(tip);
                    Session.Settings.Save();
                    UiToastHost.Show(Loc.T(tip), ToastKind.Info);
                    Track("tip_shown", new Dictionary<string, object> { { "tip", tip } });
                }
            }
            string m = review.Check(e, sim, Coach.Active);
            if (m != "") Wow(m);
            if (Coach.Active) { Session.Settings.ReviewDeferred = review.Deferred; }
        }

        void Wow(string moment)
        {
            if (kit.Review.Wow(moment)) Track("review_moment", new Dictionary<string, object> { { "moment", moment } });
        }

        static string SoundFor(string action)
        {
            if (action.StartsWith("send") || action.StartsWith("start_day") || action.StartsWith("restock")) return "truck";
            if (action.StartsWith("cook:serve")) return "coins";
            if (action.StartsWith("buy:") || action.StartsWith("research:") || action.StartsWith("prep:")) return "success";
            if (action.StartsWith("move:") || action.StartsWith("auto_layout")) return "place";
            return "";
        }

        // ------------------------------------------------------------------ called by the screen

        public ActionResult Run(string id)
        {
            var r = Actions.Run(id);
            if (id == "next_day" && r.Ok && !Coach.Active && Session.Settings.Sessions > 1) kit.Ads?.Interstitial("day_report");
            return r;
        }

        public void SheetOpened(string kind) { }
        public void SheetClosed(string kind) { if (kind == "offline") LastOffline = null; }

        public void ClaimReward(string placement, int target)
        {
            if (!Rewards.Claim(placement, target)) UiToastHost.Show(Loc.T("reward.unavailable"), ToastKind.Info);
        }

        public void SkipGuide()
        {
            Coach.Skip(Session.Sim);
            Session.Save();
            Screen.Refresh(true);
        }

        public void ReplayGuide()
        {
            Screen.CloseSheet();
            Session.Settings.ReplayGuide();
            Session.Settings.Save();
            Screen.ShowPager(() => { Session.Settings.IntroDone = true; Session.Settings.Save(); Coach.Begin(Session.Sim, Actions, 0); });
        }

        public void SetLanguage(string lang)
        {
            Session.Settings.Lang = lang;
            Session.Settings.Save();
            Loc.SetLanguage(lang);
            Track("settings_change", new Dictionary<string, object> { { "lang", lang } });
        }

        public void SetAudio(bool music, bool effects)
        {
            Session.Settings.Music = music;
            Session.Settings.Effects = effects;
            Session.Settings.Save();
            Audio.Set(music, effects);
            Screen.OpenSheet("settings");
        }

        public void SetTier(Tier t)
        {
            kit.Tiers.Choose(t);
            Screen.OpenSheet("settings");
        }

        /// <summary>Settings → Looks: the truck paint among the owned liveries ("" = the default).</summary>
        public bool Owns(string entitlement) => kit.Purchases.Has(entitlement);

        public void SetLivery(string livery)
        {
            Session.Settings.Livery = livery ?? "";
            Session.Settings.Save();
            ApplyLivery();
            Screen.OpenSheet("settings");
        }

        public int SlotCount => GameSession.SlotCount(kit.Purchases.Has);

        public void SwitchSlot(int slot)
        {
            if (Coach.Active) { UiToastHost.Show(Loc.T("hud.speed_guide"), ToastKind.Info); return; }
            string e = Session.SwitchSlot(slot, SlotCount, Math.Abs(DateTime.UtcNow.Ticks % 2147483000L) + 1);
            if (e != "") { UiToastHost.Show(Loc.T("err." + e), ToastKind.Warning); return; }
            Session.ApplyPerks(kit.Purchases.Has);
            world.Rebind(Session.Sim);
            Track("slot_switch", new Dictionary<string, object> { { "slot", (long)(slot + 1) } });
            Screen.OpenSheet("settings");
            Screen.Refresh(true);
        }

        public void ShowPrivacyOptions() => CoddyServices.Consent.ShowPrivacyOptions(_ => { });

        void ApplyLivery()
        {
            string l = Session.Settings.Livery;
            if (l == "" && kit.Purchases.Has("gold_trim")) l = "gold_trim";
            if (l != "" && !kit.Purchases.Has(l)) l = "";
            world.SetLivery(l);
            world.SetCosmetics(kit.Purchases.Has("uniforms"), kit.Purchases.Has("window_awnings"), kit.Purchases.Has("menuboards"));
        }

        void Track(string name, Dictionary<string, object> p)
        {
            string why = GameEvents.Validate(name, p);
            if (why != "") { Debug.LogWarning("[GrillConvoy] analytics dropped " + name + ": " + why); return; }
            CoddyServices.Analytics.Event(name, p);
        }

        // ------------------------------------------------------------------ lifecycle

        void Resume()
        {
            var o = Session.Resume();
            if (o == null) return;
            LastOffline = o;
            int awayDays = (int)(o.Seconds / 86400);
            if (!Coach.Active && !Screen.PagerOpen) Screen.OpenSheet("offline");
            Shop.TryOffer("resume", Coach.Active, awayDays);
        }

        void OnApplicationPause(bool paused)
        {
            inBackground = paused;
            if (!Ready) return;
            if (paused) { Session.Pause(); Session.Settings.Save(); }
            else Resume();
        }

        void OnApplicationQuit()
        {
            if (Ready) Session.Pause();
        }

        void OnDestroy()
        {
            if (kit != null && perksChanged != null) kit.Purchases.Changed -= perksChanged;
            if (kit != null && tierChanged != null) kit.Tiers.Changed -= tierChanged;
            Shop?.Dispose();
        }
    }
}
