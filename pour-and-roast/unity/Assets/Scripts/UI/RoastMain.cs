// Pour & Roast — the Cafe scene's entry point. Wires the engine-free game layer (GameSession + GameActions + Coach +
// Commerce + RoastRewards + ReviewHook) to the diorama (CafeWorld), the UI (GameScreen) and the CoddyUnityKit (tiers,
// purchases cache, house ads, rewarded, review, toasts):
//   * data from Resources/data, strings from Resources/i18n/strings.csv, the career from
//     persistentDataPath/pourandroast/career(.n).json through the kit SaveRepository (GameSession; save slots);
//   * time: the sim advances with real unscaled time; the guide pauses it while it waits for the player and runs it fast
//     while it waits for the world; full-screen ads pause it;
//   * events -> customers in the diorama / toasts / first-time tips / review moments / offers / sounds;
//   * house banner: only after the pager + guide, never over the shop, the pager or the loading curtain (kit rule: hidden
//     for ad_free); the screen's bottom bar is lifted by the banner height (SafeAreaPadding);
//   * house interstitial: only at "Next day" after a report, never in session 1 or the guide (kit pacing decides);
//   * save on pause / quit, offline pay on resume (welcome-back card with the ×2 rewarded opt-in).
// Opening the Cafe scene directly in the editor boots the kit itself.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using PourAndRoast.App;
using PourAndRoast.Game;
using PourAndRoast.Sim;
using PourAndRoast.World;

namespace PourAndRoast.UI
{
    public sealed class RoastMain : MonoBehaviour
    {
        public UIDocument uiDocument;
        public CafeWorld world;

        public GameSession Session { get; private set; }
        public Loc Loc { get; private set; }
        public GameActions Actions { get; private set; }
        public Coach Coach { get; private set; }
        public Commerce Shop { get; private set; }
        public RoastRewards Rewards { get; private set; }
        public GameScreen Screen { get; private set; }
        public RoastAudio Audio { get; private set; }
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
            if (kit == null) kit = CoddyKitHost.Boot(RoastGameConfig.Kit(), uiDocument != null ? uiDocument.panelSettings : Resources.Load<PanelSettings>("CoddyPanelSettings"));
            low = kit.Tiers.Current == Tier.Low;
            UiKit.Low = low;

            var gd = GameData.FromTexts(n => Resources.Load<TextAsset>("data/" + n)?.text);
            if (!gd.Ok) Debug.LogError("[PourAndRoast] data: " + string.Join("; ", gd.Errors));
            Session = new GameSession(gd, Path.Combine(Application.persistentDataPath, "pourandroast")) { Analytics = Track };
            Session.LoadOrNew(Math.Abs(DateTime.UtcNow.Ticks % 2147483000L) + 1);
            var st = Session.Settings;
            st.Sessions = st.Sessions + 1;
            st.Save();
            review.Deferred = st.ReviewDeferred;

            var csv = Resources.Load<TextAsset>("i18n/strings");
            Loc = Loc.FromCsv(csv != null ? csv.text : "key,en\n", st.Lang != "" ? st.Lang : RoastGameConfig.DeviceLanguage());
            if (Loc.Errors.Count > 0) Debug.LogWarning("[PourAndRoast] strings: " + string.Join("; ", Loc.Errors));
            kit.Config.UiLanguage = () => Loc.Lang;
            kit.Config.InstallLabel = () => null;
            kit.Config.LowEnd = () => kit.Tiers.Current == Tier.Low;
            kit.Config.PauseGame = p => { adPause = p; };
            kit.Config.BannerAllowed = BannerAllowed;

            // shop + rewards + perks
            var cat = IapCatalog.Parse(Resources.Load<TextAsset>("data/iap")?.text);
            if (cat.Errors.Count > 0) Debug.LogError("[PourAndRoast] iap: " + string.Join("; ", cat.Errors));
            Shop = new Commerce(cat, kit.Purchases, Session)
            {
                Analytics = Track,
                Toast = (t, k) => UiToastHost.Show(t, k == "success" ? ToastKind.Success : k == "warning" ? ToastKind.Warning : ToastKind.Info),
                LocSource = () => Loc,
                Cue = id => Audio?.Play(id),
            };
            Shop.LoadPrices();
            Session.ApplyPerks(kit.Purchases.Has);
            perksChanged = () => { Session.ApplyPerks(kit.Purchases.Has); ApplyLooks(); Screen?.Refresh(true); };
            kit.Purchases.Changed += perksChanged;
            Rewards = new RoastRewards(() => Session.Sim, kit.Rewarded) { AdFree = () => kit.Purchases.AdFree };
            Rewards.Granted += (placement, r) =>
            {
                UiToastHost.Show(RoastRewards.GrantedText(placement, r, Loc), r != null && r.Ok ? ToastKind.Success : ToastKind.Warning);
                Session.Save();
                world.MarkDirty();
                Screen?.MarkSheetDirty();
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
            Actions.Ran += (id, r) => { Screen?.AfterAction(id, r); if (r.Ok) Audio?.Play(SoundFor(id, r)); };

            Audio = RoastAudio.Create(transform, st.Music, st.Effects);
            UiKit.TapSound = () => Audio.Play("ui_tap");

            // the first 3D frame runs inside the GPU breadcrumb (a shader-compiler crash -> safe shaders next launch)
            kit.Tiers.BeginShaderWork("cafe_first_frame");
            world.Init(Session.Sim, low);
            ApplyLooks();
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
            RoastBoot.Ready();
            Track("pr_session", new Dictionary<string, object> { { "n", (long)st.Sessions }, { "tier", kit.Tiers.Current.ToString().ToLowerInvariant() }, { "slot", (long)Session.Slot } });

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
            return Screen.SheetKind != "shop";
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
            Audio.PlayMusic(sim.Phase == Phases.Live ? "music_busy" : "music_calm");
            if (!Coach.Active && !Screen.WindowOpen && sim.Phase == Phases.Live) kit.Review.TickPlay(dt);
            string deferred = review.TakeDeferred(Coach.Active);
            if (deferred != "") Wow(deferred);
        }

        void OnEvent(SimEvent e)
        {
            world.OnSimEvent(e);
            var sim = Session.Sim;
            switch (e.Type)
            {
                case "order": if (!Coach.Active) Audio.Play("order"); break;
                case "served": Audio.Play("coins"); break;
                case "walked": Audio.Play("warning"); break;
                case "day_done":
                    Track("pr_day_done", new Dictionary<string, object> { { "day", (long)e.A }, { "stars", (long)e.B }, { "venue", sim.S.Venue } });
                    if (!Coach.Active)
                    {
                        if (Session.Settings.AutoSkipReport && sim.Perk("speed")) Run("next_day");
                        else Screen.OpenSheet("report");
                    }
                    Shop.TryOffer("day_report", Coach.Active);
                    break;
                case "roast_done":
                    Audio.Play("roast");
                    UiToastHost.Show(Loc.F("toast.roast_done", "q", Fmt.Q(e.V)), ToastKind.Success);
                    Track("roast_done", new Dictionary<string, object> { { "how", e.Text }, { "q", (long)Math.Round(e.V) } });
                    break;
                case "blended": UiToastHost.Show(Loc.F("toast.blended", "kg", Fmt.Amount(e.V, "kg", Loc.Lang)), ToastKind.Success); break;
                case "wholesale":
                    UiToastHost.Show(Loc.F("toast.wholesale", "cash", Fmt.Money((long)e.V, Loc.Lang)), ToastKind.Success);
                    Track("wholesale_delivered", new Dictionary<string, object> { { "contract", e.Text } });
                    break;
                case "research_done": UiToastHost.Show(Loc.F("research.finished", "name", Loc.T("research." + e.Text + ".t")), ToastKind.Success); break;
                case "venue":
                    UiToastHost.Show(Loc.F("toast.venue", "name", Loc.T("venue." + e.Text)), ToastKind.Success);
                    Shop.TryOffer("venue", Coach.Active);
                    break;
                case "milk_arrived": if (e.A == 1) UiToastHost.Show(Loc.T("toast.milk_run"), ToastKind.Info); break;
                case "crisis":
                    UiToastHost.Show(Loc.T("crisis." + e.Text + ".t") + " — " + Loc.T("crisis." + e.Text + ".b"), ToastKind.Warning);
                    Audio.Play("warning");
                    Track("crisis", new Dictionary<string, object> { { "id", e.Text } });
                    if (e.Text == "milk_late") Shop.TryOffer("milk_late", Coach.Active);
                    break;
            }
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
            if (Coach.Active) Session.Settings.ReviewDeferred = review.Deferred;
            if (e.Type == "served" || e.Type == "walked" || e.Type == "ready" || e.Type == "order") Screen.MarkRailDirty();
        }

        void Wow(string moment)
        {
            if (kit.Review.Wow(moment)) Track("review_moment", new Dictionary<string, object> { { "moment", moment } });
        }

        static string SoundFor(string action, ActionResult r)
        {
            if (action.StartsWith("brew")) return "espresso";
            if (action.StartsWith("handover")) return "coins";
            if (action.StartsWith("dial_in") || action.StartsWith("repair")) return "grind";
            if (action.StartsWith("drop")) return r.Value > 0 ? "success" : "roast";
            if (action.StartsWith("buy:") || action.StartsWith("research:")) return "success";
            if (action.StartsWith("fridge")) return "milk";
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

        public void ClaimReward(string placement)
        {
            if (!Rewards.Claim(placement)) UiToastHost.Show(Loc.T("reward.unavailable"), ToastKind.Info);
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

        public void SetTheme(string theme)
        {
            Session.Settings.Theme = theme;
            Session.Settings.Save();
            ApplyLooks();
            Screen.OpenSheet("settings");
        }

        /// <summary>Switches the career save slot: saves this one and reloads the scene on the other.</summary>
        public void SwitchSlot(int slot)
        {
            if (slot == Session.Slot || slot < 0 || slot >= Shop.SaveSlots) return;
            Session.Save();
            Session.Settings.Slot = slot;
            Session.Settings.Save();
            Track("slot_switch", new Dictionary<string, object> { { "slot", (long)slot } });
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ShowPrivacyOptions() => CoddyServices.Consent.ShowPrivacyOptions(_ => { });

        void ApplyLooks()
        {
            string theme = Session.Settings.Theme;
            if (theme != "" && !kit.Purchases.Has(theme)) theme = "";
            world.SetLooks(theme, kit.Purchases.Has("gold_trim"), kit.Purchases.Has("aprons"), kit.Purchases.Has("latte_art") ? "latte_art" : kit.Purchases.Has("cups") ? "cups" : "");
        }

        void Track(string name, Dictionary<string, object> p)
        {
            string why = GameEvents.Validate(name, p);
            if (why != "") { Debug.LogWarning("[PourAndRoast] analytics dropped " + name + ": " + why); return; }
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
            if (Ready) Session.Pause();
            if (kit != null && perksChanged != null) kit.Purchases.Changed -= perksChanged;
            if (kit != null && tierChanged != null) kit.Tiers.Changed -= tierChanged;
            Shop?.Dispose();
        }
    }
}
