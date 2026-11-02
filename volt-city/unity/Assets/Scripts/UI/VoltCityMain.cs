// Volt City — the City scene's entry point (P3). Wires the engine-free game layer (GameSession + GameActions + Coach +
// Feed + ReviewHook) to the diorama (CityWorld), the UI (GameScreen) and the CoddyUnityKit (tiers, purchases cache,
// house ads, rewarded, review, toasts):
//   * data from Resources/data (P1), strings from Resources/i18n/strings.csv, the career from
//     persistentDataPath/voltcity/career.json through the kit SaveRepository (GameSession);
//   * time: the sim advances with real unscaled time; paused while a guide step waits or a full-screen ad shows;
//   * events -> toasts / event cards / lightning / hints / review moments; the world resyncs on events or a new step;
//   * house banner: allowed after the pager + guide, never over the shop / a purchase or the pager (kit rule: hidden for
//     ad_free, loading, gate); the bar's slot is reserved (ReserveSlot) and the screen's bottom = max(nav bar, banner);
//   * house interstitial: only at the shift report after Collect, never in session 1 or the guide (kit pacing decides);
//   * save on pause / quit, night-crew offline summary on resume.
//   * P4: shop (Commerce on the kit's RevenueCat layer: catalogue, restore, offers, looks, city slots), rewarded opt-ins
//     (VoltRewards: offline x2, shift bonus, emergency import, crew overtime), sounds (VoltAudio + SoundCues, music off by
//     default), the analytics layer (VoltAnalytics, no-op sink until Firebase), Settings privacy options (UMP).
// Opening the City scene directly in the editor boots the kit itself (no Boot scene needed for a play-test).
// UNCOMPILED until the Unity editor is installed.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VoltCity.App;
using VoltCity.Game;
using VoltCity.Sim;
using VoltCity.World;

namespace VoltCity.UI
{
    public sealed class VoltCityMain : MonoBehaviour
    {
        public const string PrivacyUrl = "https://www.coddykit.com/pages/privacy-policy";

        [Header("Scene (VoltCitySceneBuilder wires these)")]
        public UIDocument uiDocument;
        public CityWorld world;
        public Texture2D curtainArt;

        public GameSession Session { get; private set; }
        public Loc Loc { get; private set; }
        public GameActions Actions { get; private set; }
        public Coach Coach { get; private set; }
        public GameScreen Screen { get; private set; }
        public bool Ready { get; private set; }
        public Commerce Shop { get; private set; }
        public VoltRewards Rewards { get; private set; }
        public VoltAudio Audio { get; private set; }

        CoddyKitHost kit;
        readonly ReviewHook review = new ReviewHook();
        bool adPause, low;
        float settingsSaveAcc;
        string pendingCard = "";
        int hardEvents;
        Action perksChanged;
        Action<Tier> tierChanged;
        bool dailyOfferChecked, stormShift;
        float reportOpenFor;
        bool lastWindow;
        string lastTab = "";

        IEnumerator Start()
        {
            kit = CoddyKitHost.Instance;
            if (kit == null)
            {
                VoltAnalytics.Install();
                var cfg = VoltGameConfig.Kit();
                kit = CoddyKitHost.Boot(cfg, uiDocument != null ? uiDocument.panelSettings : Resources.Load<PanelSettings>("CoddyPanelSettings"));
            }
            low = kit.Tiers.Current == Tier.Low;
            UiKit.Low = low;

            var gd = GameData.FromTexts(n => Resources.Load<TextAsset>("data/" + n)?.text);
            if (!gd.Ok) Debug.LogError("[VoltCity] data: " + string.Join("; ", gd.Errors));
            string dir = Path.Combine(Application.persistentDataPath, "voltcity");
            Session = new GameSession(gd, dir) { Analytics = (n, p) => CoddyServices.Analytics.Event(n, p) };
            if (Session.Settings.Slot > (kit.Purchases.Has("slots") ? 4 : 1)) Session.Settings.Slot = 1;   // slots refunded: slot 1 (other saves stay on disk)
            Session.LoadOrNew(Math.Abs(DateTime.UtcNow.Ticks % 2147483000L) + 1);
            Session.ApplyPerks(id => kit.Purchases.Has(id));
            perksChanged = () => Session.ApplyPerks(id => kit.Purchases.Has(id));
            kit.Purchases.Changed += perksChanged;
            var st = Session.Settings;
            st.Sessions = st.Sessions + 1;
            review.Deferred = st.ReviewDeferred;

            var csv = Resources.Load<TextAsset>("i18n/strings");
            Loc = Loc.FromCsv(csv != null ? csv.text : "key,en\n", st.Lang != "" ? st.Lang : VoltGameConfig.DeviceLanguage());
            if (Loc.Errors.Count > 0) Debug.LogWarning("[VoltCity] strings: " + string.Join("; ", Loc.Errors));
            kit.Config.UiLanguage = () => Loc.Lang;

            Actions = new GameActions(Session, Loc);

            // P4: shop + rewarded + audio + analytics
            var cat = IapCatalog.Parse(Resources.Load<TextAsset>("data/iap")?.text);
            if (cat.Errors.Count > 0) Debug.LogError("[VoltCity] iap: " + string.Join("; ", cat.Errors));
            Shop = new Commerce(cat, kit.Purchases, Session)
            {
                Analytics = (n, p) => CoddyServices.Analytics.Event(n, p),
                Toast = (t, k) => { if (!string.IsNullOrEmpty(t)) ShowToast(t, k == "success" ? ToastKind.Success : k == "warning" ? ToastKind.Warning : ToastKind.Info); },
                LocSource = () => Loc,
            };
            Rewards = new VoltRewards(() => Session.Sim, kit.Rewarded) { AdFree = () => kit.Purchases.AdFree };
            Rewards.Granted += (placement, r) =>
            {
                ShowToast(VoltRewards.GrantedText(placement, r, Loc), r != null && r.Ok ? ToastKind.Success : ToastKind.Warning);
                Session.Save();
                world.MarkDirty();
            };
            var ext = new Extras { Shop = Shop, Rewards = Rewards };
            Actions.Extras = ext;
            Audio = VoltAudio.Create(transform, (int)kit.Tiers.Current, st.Effects, st.Music);
            Shop.Cue = id => Audio.Play(id);
            UiKit.TapSound = () => Audio.Play("ui_tap");
            tierChanged = t => Audio.SetTier((int)t);
            kit.Tiers.Changed += tierChanged;
            Shop.LoadPrices();
            CoddyServices.Analytics.Event("vc_session", new Dictionary<string, object> { { "n", (long)st.Sessions }, { "tier", kit.Tiers.Current.ToString().ToLowerInvariant() } });
            Coach = new Coach
            {
                Analytics = (n, p) => CoddyServices.Analytics.Event(n, p),
                Persist = (step, done) => { st.CoachStep = step; st.CoachDone = done; st.Save(); },
            };
            Actions.Ran += (id, r) => Coach.OnAction(Session.Sim, id, r);
            Actions.Ran += (id, r) => { if (id == "collect" && r.Ok) AfterCollect(); };

            // the first 3D frame runs inside the GPU breadcrumb (a shader-compiler crash -> safe shaders next launch)
            kit.Tiers.BeginShaderWork("city_first_frame");
            world.Init(Session.Sim, low);
            world.Picked += hit => Screen?.WorldPick(hit);
            if (world.tap != null)
            {
                world.tap.BlockingPanels.Add(uiDocument.rootVisualElement.panel);
                world.tap.Enabled = () => !Screen.WindowOpen && (!Coach.Spot || Coach.Target == "world.fault");
            }

            Screen = new GameScreen(uiDocument.rootVisualElement, Loc, () => Session.Sim, Actions, Coach, () => kit.Ads != null ? kit.Ads.BannerHeight : 0f);
            Screen.Extras = ext;
            ApplyScriptClass();
            Screen.Navigate += Navigate;
            Screen.ActionRan += (id, r) => Audio.Play(SoundCues.ForAction(id, r.Ok, r.Reason));
            Shop.Changed += ApplyLooks;
            ApplyLooks();
            Screen.DioramaBand += r => { if (world.rig != null) world.rig.SetViewport(r); };
            if (world.rig != null)
            {
                world.rig.OverUi = p => IsOverUi(p);
                world.rig.Enabled = () => !Coach.Spot && !Screen.WindowOpen;
                world.rig.Refit(world.LitBoxes(), true);
            }

            // kit hooks
            kit.Config.BannerAllowed = () => st.IntroDone && st.CoachDone && !Screen.WindowOpen && Screen.Tab != "shop";
            kit.Config.LowEnd = () => low;
            kit.Config.PauseGame = p => adPause = p;
            if (kit.Ads?.Banner != null) kit.Ads.Banner.ReserveSlot = true;
            kit.Review.Context = () => new ReviewGate.Context
            {
                Tutorial = Coach.Active,
                WindowOpen = Screen.WindowOpen || (kit.Ads != null && kit.Ads.Showing),
                Background = !Application.isFocused,
            };
            kit.Tiers.Changed += t => Debug.Log("[VoltCity] graphics tier -> " + t);

            yield return null;
            yield return null;
            kit.Tiers.EndShaderWork();
            VoltBoot.Ready();

            // first frame is up: offline pay, then the pager / guide / graphics popup
            var off = Session.Resume();
            if (Session.Sim.Phase == "report") Screen.OpenView("report");
            if (!st.IntroDone)
                Screen.ShowPager(curtainArt, () =>
                {
                    st.IntroDone = true;
                    st.Save();
                    if (!st.GraphicsAsked) OpenGraphicsPopup(StartGuide);
                    else StartGuide();
                });
            else if (!st.GraphicsAsked) OpenGraphicsPopup(StartGuide);
            else if (!st.CoachDone) StartGuide();
            else if (off != null) OpenOffline(off);
            Ready = true;
        }

        /// <summary>Hindi needs complex shaping (conjuncts, matras): the panel root gets vc-lang-hi, whose USS switches UI
        /// Toolkit to the advanced (HarfBuzz) text generator. Other languages keep the standard generator (cheaper).</summary>
        void ApplyScriptClass()
        {
            var root = uiDocument.rootVisualElement;
            var top = root.panel != null ? root.panel.visualTree : root;
            top.EnableInClassList("vc-lang-hi", Loc.Lang == "hi");
            root.EnableInClassList("vc-lang-hi", Loc.Lang == "hi");
        }

        float surplusWarnCd;

        /// <summary>Oversupply trips a plant (the city can go dark). A warning toast halfway to the trip, at most every 30 s,
        /// never in the guide (it has its own tips).</summary>
        void SurplusWarning(VoltCity.Sim.Simulation sim, float dt)
        {
            surplusWarnCd -= dt;
            var g = sim.Grid;
            if (g == null || sim.Phase != "live" || Coach.Active || surplusWarnCd > 0f) return;
            // halfway to the trip (surplus > over_pct for over_trip_after_s / 2): every tab, the Grid one too — the red
            // needle alone did not stop a plant trip + city blackout in 3 of 3 post-guide shifts (play 10-29)
            // a district the grid switched off waits for room: off the Grid tab, say so (the Grid tab has the soft assist)
            int dark = Coach.DarkToRestore(g);
            if (dark >= 0 && Screen.Tab != "grid" && g.NeedlePm >= -30 && Coach.ProjectedKw(g) < g.LastDemand + Coach.RestoreNeedKw(g, dark))
            {
                surplusWarnCd = 30f;
                ShowToast(Loc.F("warn.restore_away", "district", Names.District(Loc, g.DIds[dark])), ToastKind.Warning);
                return;
            }
            // a district the PLAYER switched off stays dark until they restore it; once there is room, say so (#183: the
            // reckless profile left hand-shed districts dark all shift, 14 % lights on)
            for (int i = 0; i < g.DIds.Length; i++)
            {
                if (!g.DHooked[i] || g.DState[i] != VoltCity.Sim.Grid.ManualShed) continue;
                if (Coach.ProjectedKw(g) < g.LastDemand + Coach.RestoreNeedKw(g, i) || g.NeedlePm < 0) continue;
                surplusWarnCd = 30f;
                ShowToast(Loc.F("warn.restore_manual", "district", Names.District(Loc, g.DIds[i])), ToastKind.Warning);
                return;
            }
            // a quarter of the way to the trip (10-29 #174: halfway was too late for a player to react and ramp down)
            if (g.NeedlePm <= sim.Gd.OverPct * 10 || g.SurplusSteps < sim.Gd.OverTripAfterS * 4 / 4) return;
            surplusWarnCd = 30f;
            ShowToast(Loc.T("warn.surplus_away"), ToastKind.Warning);
        }

        bool coachWasActive;
        (string text, ToastKind kind)? heldToast;

        /// <summary>The first-start pager or the graphics question is on screen.</summary>
        bool IntroOpen => Screen != null && (Screen.PagerOpen || (Session.Settings.IntroDone && !Session.Settings.GraphicsAsked));

        /// <summary>Every game toast goes through here: while onboarding is open nothing is shown; the first warning / danger
        /// (e.g. a district switched off) is held and shown once onboarding is over.</summary>
        void ShowToast(string text, ToastKind kind)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (IntroOpen)
            {
                if (heldToast == null && (kind == ToastKind.Warning || kind == ToastKind.Danger)) heldToast = (text, kind);
                return;
            }
            UiToastHost.Show(text, kind);
        }

        void StartGuide()
        {
            var st = Session.Settings;
            if (st.CoachDone) return;
            Coach.Begin(Session.Sim, st.CoachStep);
            // a brand-new career starts its guided first shift right away (the 30-second hook, DESIGN section 4)
            if (Session.Sim.Phase == "idle" && Session.Sim.S.ShiftNo == 0) Actions.Run("shift:start");
        }

        bool IsOverUi(Vector2 topLeft)
        {
            var panel = uiDocument.rootVisualElement.panel;
            if (panel == null) return false;
            var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, topLeft));
            return picked != null && picked.pickingMode == PickingMode.Position && picked != panel.visualTree;
        }

        void Update()
        {
            if (!Ready) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
            var sim = Session.Sim;
            Coach.SoftAssists = Session.Settings.CoachDone;
            Coach.Update(sim);
            if (coachWasActive && !Coach.Active) Screen.HideExplainer();   // guide finished / skipped: its tip cards go
            coachWasActive = Coach.Active;
            // onboarding (pager + graphics question): the city waits — no shift time, no events, no toasts over it (10-28)
            sim.SetPaused(Coach.WantsPause || adPause || IntroOpen);
            if (!IntroOpen && heldToast != null)
            {
                var h = heldToast.Value;
                heldToast = null;
                UiToastHost.Show(h.text, h.kind);
            }
            bool wasLive = sim.Phase == "live";
            var events = Session.Tick(dt);
            if (events.Count > 0) world.MarkDirty();
            foreach (var e in events) OnEvent(e);
            if (wasLive && sim.Phase == "report" && !Coach.Active) Screen.OpenView("report");
            if (pendingCard != "" && !Screen.WindowOpen && !Coach.Spot)
            {
                string id = pendingCard;
                pendingCard = "";
                Screen.OpenTabView("event:" + id, () => Cards.Event(id, Session.Sim, Loc));
            }
            world.Tick(dt);
            Screen.Tick(dt, FaultRect);
            Audio.Tick(SoundCues.MusicFor(sim, hardEvents > 0), dt);
            if (Screen.WindowOpen != lastWindow) { lastWindow = Screen.WindowOpen; Audio.Play(lastWindow ? "ui_open" : "ui_close"); }
            if (Screen.Tab != lastTab)
            {
                if (Screen.Tab == "shop") CoddyServices.Analytics.Event("shop_open", new Dictionary<string, object> { { "source", Shop.Focus != "" ? Shop.Focus : "tab" } });
                if (Screen.Tab == "shop" && !Shop.StoreReady) Shop.LoadPrices();
                lastTab = Screen.Tab;
            }
            SurplusWarning(sim, dt);
            AutoCollect(dt);
            DailyOffer();
            // review: real play time only (live, unpaused), the moment when no window is open
            if (sim.Phase == "live" && !sim.S.Paused) kit.Review.TickPlay(dt);
            string moment = review.Tick(dt, Coach.Active, Screen.WindowOpen || (kit.Ads != null && kit.Ads.Showing));
            if (moment != "") kit.Review.Wow(moment);
            // hints (once each, never in the guide)
            settingsSaveAcc += dt;
            if (settingsSaveAcc > 1f)
            {
                settingsSaveAcc = 0f;
                string hint = Hints.Next(sim, Session.Settings, events, Screen.Tab, Session.Settings.Sessions);
                if (hint != "" && !Screen.WindowOpen)
                {
                    Session.Settings.MarkHint(hint);
                    Session.Settings.Save();
                    ShowToast(Loc.T("hint." + hint), ToastKind.Info);
                }
            }
        }

        (bool, Rect) FaultRect(int faultId)
        {
            if (!world.FaultScreen(faultId, out var px, out float rpx)) return (false, default);
            var panel = uiDocument.rootVisualElement.panel;
            var c = RuntimePanelUtils.ScreenToPanel(panel, px);
            var e = RuntimePanelUtils.ScreenToPanel(panel, px + new Vector2(rpx, 0f));
            float r = Mathf.Abs(e.x - c.x);
            return (true, new Rect(c.x - r, c.y - r, 2 * r, 2 * r));
        }

        void OnEvent(SimEvent e)
        {
            var it = Feed.From(e, Session.Sim, Loc, Coach.Active);
            if (it.Toast != "")
            {
                var k = it.Kind == "success" ? ToastKind.Success : it.Kind == "warning" ? ToastKind.Warning : it.Kind == "danger" ? ToastKind.Danger : ToastKind.Info;
                ShowToast(it.Toast, k);
            }
            if (it.Card != "") pendingCard = it.Card;
            Audio.Play(SoundCues.ForSim(e));
            var fa = VoltAnalytics.FromSim(e, Session.Sim);
            if (fa.HasValue) CoddyServices.Analytics.Event(fa.Value.Key, fa.Value.Value);
            if (e.Type == "event_start" && SoundCues.HardEvent(e.Str("id"))) hardEvents++;
            if (e.Type == "event_end" && SoundCues.HardEvent(e.Str("id"))) hardEvents = Math.Max(0, hardEvents - 1);
            if (e.Type == "shift_start") { hardEvents = 0; stormShift = false; }
            if (e.Type == "event_start" && e.Str("id").Contains("storm")) stormShift = true;
            if (e.Type == "shift_done")
            {
                hardEvents = 0;
                // storm recovery offer: the report after a 1-star storm shift (IAP.md section 2), as a row on that report
                bool storm = stormShift || Offers.StormShift(Session.Sim.S.Report?.Events);
                if (storm) Shop.TryOffer(Shop.OfferCtx("storm_report", Coach.Active, false, 0, (int)e.Num("stars")));
            }
            if (it.Lightning) world.Lightning();
            if (e.Type == "event_start" && e.Str("id") == "thunderstorm") world.StormActive = true;
            if ((e.Type == "event_end" && e.Str("id") == "thunderstorm") || e.Type == "shift_end") world.StormActive = false;
            if (it.ReviewMoment != "")
            {
                review.Offer(it.ReviewMoment, it.ReviewReady);
                Session.Settings.ReviewDeferred = review.Deferred;
            }
            if (it.Success)
            {
                review.Success(Coach.Active);
                Session.Settings.ReviewDeferred = review.Deferred;
            }
            if (e.Type == "shift_done") Session.Settings.Save();
        }

        // =========================================================================================== navigation

        void Navigate(string what)
        {
            var st = Session.Settings;
            switch (what)
            {
                case "coach.skip":
                    Coach.Skip(Session.Sim);
                    Screen.HideExplainer();
                    return;
                case "settings": OpenSettings(); return;
                case "restore_purchases":
                    Shop.Restore();
                    return;
            }
            if (what.StartsWith("shop:"))
            {
                // a locked paid feature (Build tab dispatcher row, 4x speed toast, Cities sheet) opens the shop ON that product
                Shop.Focus = what.Substring(5);
                Screen.ScrollTo("shop.prod." + Shop.Focus);
            }
        }

        void OpenOffline(OfflineSummary o)
        {
            // comeback offer: the first session after >= 3 days away, as a row on the welcome-back card
            Shop.TryOffer(Shop.OfferCtx("comeback", Coach.Active, Session.Sim.Phase == "live", o.AwayDays));
            Screen.OpenTabView("offline", () => Cards.Offline(o, Loc, Screen.Extras));
        }

        /// <summary>Equipped looks -> diorama uniforms + UI theme (on purchase / restore / Use).</summary>
        void ApplyLooks()
        {
            var l = Shop.Look;
            world.ApplyLooks(l);
            Screen?.SetTheme(l.RoomClass);
        }

        /// <summary>Rush Hour Speed's optional auto-collect: the report collects itself after 1.5 s (never in the guide).</summary>
        void AutoCollect(float dt)
        {
            var s = Session.Sim;
            bool on = Session.Settings.AutoCollect && s.Perk("speed4") && s.Phase == "report" && !Coach.Active && !(kit.Ads != null && kit.Ads.Showing);
            reportOpenFor = on ? reportOpenFor + dt : 0f;
            if (reportOpenFor < 1.5f) return;
            reportOpenFor = 0f;
            Screen.Follow(Actions.Run("collect"));
        }

        /// <summary>Starter / bundle offers: checked once per session at a calm moment (between shifts, no window, guide
        /// done, never the first session — Offers rules); shown as the product card.</summary>
        void DailyOffer()
        {
            if (dailyOfferChecked || Coach.Active || !Session.Settings.CoachDone || Screen.WindowOpen || Session.Sim.Phase != "idle") return;
            dailyOfferChecked = true;
            string slug = Shop.TryOffer(Shop.OfferCtx("", false, false));
            if (slug != "") Screen.OpenView("card:product:" + slug);
        }

        /// <summary>After Collect (shift report): the house interstitial may show (kit pacing; never session 1 / the guide).</summary>
        public void AfterCollect()
        {
            if (Coach.Active || Session.Settings.Sessions < 2 || kit.Ads == null) return;
            kit.Ads.Interstitial("shift_report");
        }

        /// <summary>The scene reloads on a tier change / city switch while the kit lives on: drop every kit subscription.</summary>
        void OnDestroy()
        {
            if (kit == null) return;
            if (perksChanged != null) kit.Purchases.Changed -= perksChanged;
            if (tierChanged != null && kit.Tiers != null) kit.Tiers.Changed -= tierChanged;
            Shop?.Dispose();
            UiKit.TapSound = null;
        }

        void OnApplicationPause(bool paused)
        {
            if (!Ready) return;
            if (paused)
            {
                Session.Pause();
                Session.Settings.Save();
            }
            else
            {
                var off = Session.Resume();
                if (off != null && !Coach.Active) OpenOffline(off);
            }
        }

        void OnApplicationQuit()
        {
            if (!Ready) return;
            Session.Pause();
            Session.Settings.Save();
        }

        // =========================================================================================== settings

        VisualElement SettingRow(string title, string sub, VisualElement side)
        {
            var c = UiKit.Card(title, sub);
            if (side != null) c.SetSide(side);
            return c.Root;
        }

        VisualElement Segmented(string[] labels, int selected, Action<int> pick, string targetPrefix)
        {
            var row = new VisualElement();
            row.AddToClassList("vc-segmented");
            for (int i = 0; i < labels.Length; i++)
            {
                int k = i;
                var b = new Button { text = labels[i], focusable = false, name = targetPrefix + "." + i };
                b.AddToClassList("vc-segment");
                b.EnableInClassList("vc-segment--on", i == selected);
                b.clicked += () =>
                {
                    foreach (var child in row.Children()) child.EnableInClassList("vc-segment--on", child == b);
                    pick(k);
                };
                row.Add(b);
            }
            return row;
        }

        Button OnOff(bool on, Action<bool> set, string name)
        {
            Button b = null;
            b = UiKit.ActionPill(Loc.T(on ? "settings.on" : "settings.off"), () =>
            {
                bool now = b.text != Loc.T("settings.on");
                b.text = Loc.T(now ? "settings.on" : "settings.off");
                b.EnableInClassList("coddy-pill--tonal", !now);
                set(now);
            }, true, on ? "primary" : "tonal");
            b.name = name;
            return b;
        }

        void OpenSettings()
        {
            var st = Session.Settings;
            var sheet = Screen.Sheets.OpenCustom("settings", Loc.T("settings.title"), Loc.T("settings.sub"), "gear", Loc.T("sheet.close"));
            var body = sheet.Body;
            body.Add(UiKit.SectionHeader(Loc.T("settings.sound")));
            body.Add(SettingRow(Loc.T("settings.music"), "", OnOff(st.Music, v => { st.Music = v; st.Save(); Audio.SetMusic(v); Changed("music", v); }, "settings.music")));
            body.Add(SettingRow(Loc.T("settings.effects"), "", OnOff(st.Effects, v => { st.Effects = v; st.Save(); Audio.Effects = v; Changed("sfx", v); }, "settings.effects")));

            body.Add(UiKit.SectionHeader(Loc.T("settings.graphics")));
            var tiers = new[] { Loc.T("graphics.low"), Loc.T("graphics.medium"), Loc.T("graphics.high") };
            body.Add(SettingRow(Loc.T("settings.graphics_q"), Loc.T("settings.graphics_hint"),
                Segmented(tiers, (int)kit.Tiers.Current, i => ChangeTier((Tier)i), "settings.graphics")));

            body.Add(UiKit.SectionHeader(Loc.T("settings.play")));
            var speeds = new List<string>();
            foreach (var x in Session.Gd.Speeds) speeds.Add(x + "×");
            int sel = Math.Max(0, Array.IndexOf(Session.Gd.Speeds, st.SpeedDefault));
            body.Add(SettingRow(Loc.T("settings.speed"), Loc.T("settings.speed_sub"),
                Segmented(speeds.ToArray(), sel, i => { st.SpeedDefault = Session.Gd.Speeds[i]; st.Save(); }, "settings.speed")));
            body.Add(SettingRow(Loc.T("settings.language"), Loc.NativeNames[Loc.LangIndex],
                UiKit.ActionPill(Loc.T("settings.change"), OpenLanguage, true, "tonal")));
            body.Add(SettingRow(Loc.T("settings.cities"), Loc.F("settings.cities_sub", "n", UsedSlots(), "max", Shop.SlotCount),
                UiKit.ActionPill(Loc.T("settings.open"), OpenCities, true, "tonal")));
            if (Session.Sim.Perk("speed4"))
                body.Add(SettingRow(Loc.T("settings.auto_collect"), Loc.T("settings.auto_collect_sub"),
                    OnOff(st.AutoCollect, v => { st.AutoCollect = v; st.Save(); Changed("auto_collect", v); }, "settings.auto_collect")));

            body.Add(UiKit.SectionHeader(Loc.T("settings.help")));
            body.Add(SettingRow(Loc.T("settings.replay"), Loc.T("settings.replay_sub"), UiKit.ActionPill(Loc.T("settings.replay_go"), ReplayGuide, true, "tonal")));
            body.Add(SettingRow(Loc.T("settings.forecast"), "", UiKit.ActionPill(Loc.T("settings.open"), () => Screen.OpenView("forecast"), true, "tonal")));

            body.Add(UiKit.SectionHeader(Loc.T("settings.purchases")));
            body.Add(SettingRow(Loc.T("shop.restore.t"), Loc.T("shop.restore.b"), UiKit.ActionPill(Loc.T("shop.restore"), () => Navigate("restore_purchases"), true, "tonal")));

            body.Add(UiKit.SectionHeader(Loc.T("settings.privacy")));
            if (CoddyServices.Consent.PrivacyOptionsRequired)   // UMP: the consent form again (EEA / UK / US states)
                body.Add(SettingRow(Loc.T("settings.privacy_options"), Loc.T("settings.privacy_options_sub"),
                    UiKit.ActionPill(Loc.T("settings.open"), () => CoddyServices.Consent.ShowPrivacyOptions(_ => { }), true, "tonal")));
            body.Add(SettingRow(Loc.T("settings.privacy_policy"), Loc.T("settings.privacy_sub"), UiKit.ActionPill(Loc.T("settings.open"), () => Application.OpenURL(PrivacyUrl), true, "tonal")));
            body.Add(UiKit.Text(Loc.F("settings.version", "v", Application.version), "coddy-caption-muted"));
        }

        void Changed(string setting, bool on) =>
            CoddyServices.Analytics.Event("settings_change", new Dictionary<string, object> { { "setting", setting }, { "value", on ? "on" : "off" } });

        // =========================================================================================== city slots (P4)

        int UsedSlots()
        {
            int n = 0;
            for (int i = 1; i <= 4; i++) if (i == Session.Settings.Slot || Session.SlotInfo(i).ok) n++;
            return n;
        }

        /// <summary>Settings → Cities: slot 1 free, 2-4 with Extra City Slots (or the Edition / Pass / Collection).</summary>
        void OpenCities()
        {
            var sheet = Screen.Sheets.OpenCustom("cities", Loc.T("settings.cities"), Loc.F("settings.cities_sub", "n", UsedSlots(), "max", Shop.SlotCount), "city", Loc.T("sheet.close"));
            for (int i = 1; i <= 4; i++)
            {
                int slot = i;
                var info = Session.SlotInfo(slot);
                bool current = slot == Session.Settings.Slot;
                bool open = slot <= Shop.SlotCount;
                string sub = info.ok ? Loc.F("city.slot_sub", "shift", info.shift + 1, "cash", Fmt.Money(info.cash, Loc.Lang)) : open ? Loc.T("city.empty") : Loc.T("city.locked");
                var c = UiKit.Card(Loc.F("city.slot", "n", slot), sub, null, !open, current);
                if (current) c.SetSide(UiKit.Text(Loc.T("city.current"), "vc-check"));
                else if (open) c.SetSide(UiKit.ActionPill(Loc.T(info.ok ? "city.play" : "city.new"), () => SwitchSlot(slot, !info.ok), true, "tonal"));
                else c.SetSide(UiKit.ActionPill(Loc.T("shop.see"), () => { Screen.Sheets.Close(); Screen.OpenView("shop:slots"); }, true, "tonal"));
                c.Root.name = "cities." + slot;
                sheet.Body.Add(c.Root);
            }
        }

        void SwitchSlot(int slot, bool fresh)
        {
            if (slot == Session.Settings.Slot || slot > Shop.SlotCount) return;
            Session.Save();
            Session.Settings.Slot = slot;
            Session.Settings.Save();
            CoddyServices.Analytics.Event("city_slot", new Dictionary<string, object> { { "slot", (long)slot }, { "fresh", fresh ? 1L : 0L } });
            SceneManager.LoadScene(gameObject.scene.name);   // the scene loads the slot's career (GameSession.SavePath)
        }

        /// <summary>First start: the tier the device class picked (flagship High / strong Medium / weak Low), changeable
        /// here and in Settings. Closing it in any way counts as answered.</summary>
        void OpenGraphicsPopup(Action then)
        {
            var st = Session.Settings;
            var sheet = Screen.Sheets.OpenCustom("graphics", Loc.T("graphics.title"),
                Loc.F("graphics.sub", "tier", Loc.T("graphics." + kit.Tiers.Current.ToString().ToLowerInvariant())), "gear", Loc.T("sheet.close"));
            var tiers = new[] { Loc.T("graphics.low"), Loc.T("graphics.medium"), Loc.T("graphics.high") };
            Tier chosen = kit.Tiers.Current;
            sheet.Body.Add(SettingRow(Loc.T("settings.graphics_q"), Loc.T("settings.graphics_hint"), Segmented(tiers, (int)chosen, i => chosen = (Tier)i, "graphics")));
            var ok = UiKit.PrimaryButton(Loc.T("graphics.ok"), () => Screen.Sheets.Close());
            ok.name = "graphics.ok";
            sheet.Actions.Add(ok);
            void Closed(string id)
            {
                if (id != "graphics") return;
                Screen.Sheets.Closed -= Closed;
                st.GraphicsAsked = true;
                st.Save();
                if (chosen != kit.Tiers.Current) { ChangeTier(chosen); return; }   // reloads the scene; the guide resumes there
                then?.Invoke();
            }
            Screen.Sheets.Closed += Closed;
        }

        void OpenLanguage()
        {
            var sheet = Screen.Sheets.OpenCustom("language", Loc.T("settings.language"), "", "globe", Loc.T("sheet.close"));
            for (int i = 0; i < Loc.Languages.Length; i++)
            {
                string code = Loc.Languages[i];
                bool on = i == Loc.LangIndex;
                var c = UiKit.Card(Loc.NativeNames[i], "", null, false, on);
                c.SetSide(on ? UiKit.Icon("check", "coddy-icon vc-check-icon") : UiKit.ActionPill(Loc.T("settings.choose"), () => ChangeLanguage(code), true, "tonal"));
                sheet.Body.Add(c.Root);
            }
        }

        void ChangeLanguage(string code)
        {
            Session.Settings.Lang = code;
            Session.Settings.Save();
            Loc.SetLanguage(code);
            ApplyScriptClass();
            UiToastHost.ClearAll();   // a toast still up is in the old language (P5 layout gate 10-29)
            Screen.Sheets.Close();
            Screen.Build();    // every open view re-localises (P5 gate)
        }

        void ChangeTier(Tier t)
        {
            if (t == kit.Tiers.Current) return;
            kit.Tiers.Choose(t);
            Session.Save();
            Session.Settings.Save();
            SceneManager.LoadScene(gameObject.scene.name);   // rebuild the diorama with the tier's materials / budgets
        }

        void ReplayGuide()
        {
            var st = Session.Settings;
            st.ReplayGuide();
            st.Save();
            Screen.Sheets.Close();
            Screen.ShowPager(curtainArt, () =>
            {
                st.IntroDone = true;
                st.Save();
                StartGuide();
            });
        }
    }
}
