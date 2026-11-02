// Docket Day — the firm scene's entry point. Wires the engine-free game layer (GameSession + GameActions + Coach +
// Commerce + DocketRewards + ReviewHook) to the diorama (RegionWorld), the UI (GameScreen) and the CoddyUnityKit (tiers,
// purchases cache, house ads, rewarded, review, toasts):
//   * data from Resources/data, strings from Resources/i18n/strings.csv, the career from
//     persistentDataPath/docketday/career.json through the kit SaveRepository (GameSession);
//   * time: the sim advances with real unscaled time; the guide pauses it while it waits for the player and runs it fast
//     while it waits for the world; the hearing scene and full-screen ads pause it; "skip ahead" runs the day fast until
//     the next decision (a walk-in, a hearing, a crisis, the report);
//   * events -> world walkers / toasts / the hearing scene / first-time tips / review moments / offers;
//   * house banner: only after the pager + guide, never over the shop, the pager, the hearing scene or the curtain (kit
//     rule: hidden for ad_free); the screen's bottom bar is lifted by the banner height (SafeAreaPadding);
//   * house interstitial: only at "Next day" after the billing run, never in session 1, the guide or a hearing (kit pacing);
//   * save on pause / quit, offline pay on resume (welcome-back card with the ×2 rewarded opt-in).
// Opening the scene directly in the editor boots the kit itself.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using DocketDay.App;
using DocketDay.Game;
using DocketDay.Sim;
using DocketDay.World;

namespace DocketDay.UI
{
    public sealed class DocketMain : MonoBehaviour
    {
        const int FastForwardSpeed = 8;

        public UIDocument uiDocument;
        public RegionWorld world;

        public GameSession Session { get; private set; }
        public Loc Loc { get; private set; }
        public GameActions Actions { get; private set; }
        public Coach Coach { get; private set; }
        public Commerce Shop { get; private set; }
        public DocketRewards Rewards { get; private set; }
        public GameScreen Screen { get; private set; }
        public DocketAudio Audio { get; private set; }
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
            if (kit == null) kit = CoddyKitHost.Boot(DocketGameConfig.Kit(), uiDocument != null ? uiDocument.panelSettings : Resources.Load<PanelSettings>("CoddyPanelSettings"));
            low = kit.Tiers.Current == Tier.Low;
            UiKit.Low = low;

            var gd = GameData.FromTexts(n => Resources.Load<TextAsset>("data/" + n)?.text);
            if (!gd.Ok) Debug.LogError("[DocketDay] data: " + string.Join("; ", gd.Errors));
            Session = new GameSession(gd, Path.Combine(Application.persistentDataPath, "docketday")) { Analytics = Track };
            Session.LoadOrNew(Math.Abs(DateTime.UtcNow.Ticks % 2147483000L) + 1);
            var st = Session.Settings;
            st.Sessions = st.Sessions + 1;
            st.Save();
            review.Deferred = st.ReviewDeferred;

            var csv = Resources.Load<TextAsset>("i18n/strings");
            Loc = Loc.FromCsv(csv != null ? csv.text : "key,en\n", st.Lang != "" ? st.Lang : DocketGameConfig.DeviceLanguage());
            if (Loc.Errors.Count > 0) Debug.LogWarning("[DocketDay] strings: " + string.Join("; ", Loc.Errors));
            kit.Config.UiLanguage = () => Loc.Lang;
            kit.Config.InstallLabel = () => null;
            kit.Config.LowEnd = () => kit.Tiers.Current == Tier.Low;
            kit.Config.PauseGame = p => { adPause = p; };
            kit.Config.BannerAllowed = BannerAllowed;

            // shop + rewards + perks
            var cat = IapCatalog.Parse(Resources.Load<TextAsset>("data/iap")?.text);
            if (cat.Errors.Count > 0) Debug.LogError("[DocketDay] iap: " + string.Join("; ", cat.Errors));
            Shop = new Commerce(cat, kit.Purchases, Session)
            {
                Analytics = Track,
                Toast = (t, k) => UiToastHost.Show(t, k == "success" ? ToastKind.Success : k == "warning" ? ToastKind.Warning : ToastKind.Info),
                LocSource = () => Loc,
                Cue = id => Audio?.Play(id == "purchase" ? "success" : id),
            };
            Shop.LoadPrices();
            Session.ApplyPerks(kit.Purchases.Has);
            perksChanged = () => { Session.ApplyPerks(kit.Purchases.Has); ApplyLooks(); Screen?.Refresh(true); Screen?.MarkSheetDirty(); };
            kit.Purchases.Changed += perksChanged;
            Rewards = new DocketRewards(() => Session.Sim, kit.Rewarded) { AdFree = () => kit.Purchases.AdFree };
            Rewards.Granted += (placement, r) =>
            {
                UiToastHost.Show(DocketRewards.GrantedText(placement, r, Loc), r != null && r.Ok ? ToastKind.Success : ToastKind.Warning);
                Session.Save();
                Screen?.MarkSheetDirty();
            };

            // actions + guide
            Actions = new GameActions(Session) { Analytics = Track };
            Coach = new Coach
            {
                Analytics = Track,
                Persist = (step, done) => { st.CoachStep = step; st.CoachDone = done; st.Save(); },
            };
            Actions.Allowed = Coach.AllowsFor(() => Session.Sim);
            Actions.Ran += (id, r) => Coach.OnAction(Session.Sim, Actions, id, r);
            Actions.Ran += (id, r) => { Screen?.AfterAction(id, r); if (r.Ok) Audio?.Play(SoundFor(id)); };

            Audio = DocketAudio.Create(transform, st.Music, st.Effects);
            UiKit.TapSound = () => Audio.Play("ui_tap");

            // the first 3D frame runs inside the GPU breadcrumb (a shader-compiler crash -> safe shaders next launch)
            kit.Tiers.BeginShaderWork("firm_first_frame");
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
                world.rig.TopUi = 0.16f;
                world.rig.BottomUi = 0.42f;   // the Docket Rail + people row + tabs
                world.rig.OverUi = p => Screen.OverUi(p);
                world.rig.Enabled = () => !Screen.WindowOpen;
            }
            kit.Review.Context = () => new ReviewGate.Context { Tutorial = Coach.Active, WindowOpen = Screen.WindowOpen || adPause, Background = inBackground };

            yield return null;
            kit.Tiers.EndShaderWork();
            DocketBoot.Ready();
            Track("dd_session", new Dictionary<string, object> { { "n", (long)st.Sessions }, { "tier", kit.Tiers.Current.ToString().ToLowerInvariant() } });

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
            if (Coach.Active || Screen.PagerOpen || Screen.HearingOpen || !Session.Settings.IntroDone) return false;
            return Screen.SheetKind != "shop";
        }

        void Update()
        {
            if (!Ready) return;
            float dt = Time.unscaledDeltaTime;
            var sim = Session.Sim;
            bool hold = adPause || Screen.PagerOpen || Screen.HearingOpen;
            if (Coach.Active && !Screen.PagerOpen) Coach.Drive(sim, Actions, dt, hold);
            sim.SetPaused(hold || (Coach.Active && Coach.WantsPause(sim)));
            // skip ahead: the day runs fast until the next decision (speed ≤ 8: the codec's range, saves stay valid)
            if (Coach.Active || sim.Phase != Phases.Live) Actions.FastForwarding = false;
            bool ff = Actions.FastForwarding && sim.Phase == Phases.Live;
            int keep = sim.S.Speed;
            if (ff) sim.S.Speed = FastForwardSpeed;
            List<SimEvent> events = Session.Tick(dt);
            if (ff) sim.S.Speed = keep;
            foreach (var e in events) OnEvent(e);
            Screen.Tick(dt, low);
            if (!Coach.Active && !Screen.WindowOpen && sim.Phase == Phases.Live) kit.Review.TickPlay(dt);
            string deferred = review.TakeDeferred(Coach.Active);
            if (deferred != "") Wow(deferred);
        }

        static readonly HashSet<string> StopsFastForward = new HashSet<string> { "walkin", "hearing_start", "crisis", "day_done", "late" };

        void OnEvent(SimEvent e)
        {
            world.OnSimEvent(e);
            var sim = Session.Sim;
            if (StopsFastForward.Contains(e.Type)) Actions.FastForwarding = false;
            switch (e.Type)
            {
                case "day_done":
                    Track("dd_day_done", new Dictionary<string, object> { { "day", (long)e.A }, { "stars", (long)e.B } });
                    if (!Coach.Active) Screen.OpenSheet("report");
                    Shop.TryOffer("day_report", Coach.Active);
                    if (sim.S.Cash < 0) Shop.TryOffer("cash_dip", Coach.Active);
                    Audio.PlayMusic("music_calm");
                    break;
                case "day_live": Audio.PlayMusic("music_busy"); break;
                case "walkin": if (!Coach.Active) UiToastHost.Show(Loc.T("toast.walkin"), ToastKind.Info); Audio.Play("door"); break;
                case "walked_out": UiToastHost.Show(Loc.T("toast.walked_out"), ToastKind.Warning); break;
                case "hearing_start": Screen.QueueHearing(e.A); break;
                case "verdict":
                    Track("hearing_result", new Dictionary<string, object> { { "won", (long)e.B }, { "area", e.Text } });
                    break;
                case "task_done": Audio.Play("paper"); break;
                case "room_done": UiToastHost.Show(Loc.F("toast.room_done", "name", Loc.T("room." + e.Text)), ToastKind.Success); break;
                case "chapter": UiToastHost.Show(Loc.F("toast.chapter", "name", Loc.T("chapter." + e.Text)), ToastKind.Success); break;
                case "hired":
                {
                    var s = sim.StaffById(e.A);
                    if (s != null) UiToastHost.Show(Loc.F("toast.hired", "name", Loc.T("staff.name." + s.Name), "role", Loc.T("role." + s.Role)), ToastKind.Success);
                    break;
                }
                case "billed": Audio.Play("coins"); break;
                case "late": UiToastHost.Show(Loc.F("toast.late", "kind", Loc.T("kind." + e.Text)), ToastKind.Warning); break;
                case "continued": UiToastHost.Show(Loc.F("toast.continued", "n", (long)e.V), ToastKind.Info); break;
                case "referred": UiToastHost.Show(Loc.F("toast.referred", "cash", Fmt.Money((long)e.V, Loc.Lang)), ToastKind.Success); break;
                case "crisis":
                    UiToastHost.Show(Loc.T("crisis." + e.Text + ".t") + " — " + Loc.T("crisis." + e.Text + ".b"), ToastKind.Warning);
                    Audio.Play("warning");
                    Track("crisis", new Dictionary<string, object> { { "id", e.Text } });
                    break;
            }
            if (e.Type != "task_done" && e.Type != "assigned") Screen.MarkSheetDirty();
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
            if (action.StartsWith("assign:")) return "paper";
            if (action.StartsWith("bill")) return "coins";
            if (action.StartsWith("build:") || action.StartsWith("hire:") || action.StartsWith("accept:") || action.StartsWith("move_up")) return "success";
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

        public void SetObjection(bool on)
        {
            Session.Settings.ObjectionTap = on;
            Session.Settings.Save();
            Screen.OpenSheet("settings");
        }

        public void SetTier(Tier t)
        {
            kit.Tiers.Choose(t);
            Screen.OpenSheet("settings");
        }

        public void SetLooks(string officeTheme, bool courtTheme, bool outfits)
        {
            var st = Session.Settings;
            st.OfficeTheme = officeTheme;
            st.CourtTheme = courtTheme;
            st.Outfits = outfits;
            st.Save();
            ApplyLooks();
            Screen.MarkSheetDirty();
        }

        public void ShowPrivacyOptions() => CoddyServices.Consent.ShowPrivacyOptions(_ => { });

        /// <summary>Equipped cosmetics, only while owned (an Edition owner gets the gold nameplate by default).</summary>
        void ApplyLooks()
        {
            var st = Session.Settings;
            string theme = st.OfficeTheme;
            if (theme == "" && kit.Purchases.Has("gold_nameplate")) theme = "gold_nameplate";
            if (theme != "" && !kit.Purchases.Has(theme)) theme = "";
            world.SetLooks(theme, st.CourtTheme && kit.Purchases.Has("court_themes"), st.Outfits && kit.Purchases.Has("outfits"));
        }

        void Track(string name, Dictionary<string, object> p)
        {
            string why = GameEvents.Validate(name, p);
            if (why != "") { Debug.LogWarning("[DocketDay] analytics dropped " + name + ": " + why); return; }
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
