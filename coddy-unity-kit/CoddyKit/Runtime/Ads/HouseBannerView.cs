// CoddyKit (Unity) — bottom house banner (games/CLAUDE.md §2, CoddyBanner.gd): our other games only, ~58 dp tall,
// <= 12 % of the panel, sitting ABOVE the bottom safe area (its background runs on to the screen edge, Height counts the
// gap), one game in narrow portrait / two side by side in landscape or >= 600 dp, rotating every 45 s, most related
// first, never the host, never a kids game. Shown to every non-ad-free player once onboarding is over (no play-time wait);
// hidden for ad_free (at the next 0.5 s check), kids games, boot / loading curtains, while the game says no
// (BannerAllowed: onboarding, guide, shop, purchase, modals) and while an interstitial / rewarded video is up.
// Anti-mistap (Patron 10-27): a NEW bar waits for a calm moment (no finger down + 3 s without input); taps within 1.5 s
// after the bar appeared / moved are swallowed. UNCOMPILED until the editor is installed.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using CoddyKit.Ui;

namespace CoddyKit.Ads
{
    public sealed class HouseBannerView : MonoBehaviour
    {
        public const float CheckS = 0.5f;
        public event Action<float> HeightChanged;
        /// <summary>Opt-in: report the bar height while the bar waits for its calm moment (the layout never jumps).</summary>
        public bool ReserveSlot;
        public float Height { get; private set; }
        public readonly HouseBannerRotation Rotation = new HouseBannerRotation();

        HouseAdsService svc;
        UIDocument doc;
        VisualElement root;
        VisualElement bar;
        JsonStateFile file;
        float check;
        bool pressed;
        string shownKey = "";

        double Now => Time.realtimeSinceStartupAsDouble;

        public void Init(HouseAdsService service, PanelSettings panel, string statePath)
        {
            svc = service;
            file = new JsonStateFile(statePath).Load();
            Rotation.FromJson(file.Data);
            Rotation.OnPhone = Application.isMobilePlatform;
            var go = new GameObject("CoddyBanner");
            go.transform.SetParent(transform, false);
            doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.sortingOrder = 90;
            root = doc.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
        }

        public void ResetRound()
        {
            Rotation.Counts.Clear();
            Save();
        }

        void Update()
        {
            Rotation.Tick(Time.unscaledDeltaTime);
            bool p = Pointer.current != null && Pointer.current.press.isPressed;
            if (p != pressed)
            {
                Rotation.Input(Now, p, !p);
                pressed = p;
            }
            check += Time.unscaledDeltaTime;
            if (check < CheckS) return;
            check = 0f;
            Refresh();
        }

        bool Allowed()
        {
            var cfg = svc.Config;
            if (cfg == null || cfg.Kids || !svc.Mobile || svc.Showing) return false;
            if (svc.AdFree()) return false;
            if (cfg.Loading != null && cfg.Loading()) return false;
            if (cfg.BannerAllowed != null && !cfg.BannerAllowed()) return false;
            return true;
        }

        List<Dictionary<string, object>> Ready()
        {
            var outList = new List<Dictionary<string, object>>();
            foreach (var c in svc.Remote.Sequence("banner", svc.Config.AppId, svc.Config.Platform))
                if (svc.Images.IconReady(MiniJson.Str(c, "iconUrl"))) outList.Add(c);
            return outList;
        }

        public void PrefetchIcons()
        {
            foreach (var c in svc.Remote.Sequence("banner", svc.Config.AppId, svc.Config.Platform))
                StartCoroutine(svc.Images.FetchIcon(MiniJson.Str(c, "iconUrl")));
        }

        (float w, float h, float u) PanelMetrics()
        {
            var vt = root.panel?.visualTree;
            float w = vt != null ? vt.layout.width : 720f, h = vt != null ? vt.layout.height : 1280f;
            if (float.IsNaN(w) || w <= 0) w = 720f;
            if (float.IsNaN(h) || h <= 0) h = 1280f;
            float u = SafeArea.PxPerDp() * w / Mathf.Max(1, Screen.width);
            return (w, h, u);
        }

        /// <summary>Re-evaluates what may be shown and shows / hides it. Idempotent; every CheckS.</summary>
        public void Refresh()
        {
            var ready = Allowed() ? Ready() : null;
            if (ready == null || ready.Count == 0)
            {
                Hide();
                SetHeight(0f);
                return;
            }
            var (w, h, u) = PanelMetrics();
            var m = BannerMetrics.Compute(w, h, u);
            float gap = Mathf.Round(SafeArea.Bottom(w, h));
            if (bar == null && !Rotation.Calm(Now))
            {
                SetHeight(ReserveSlot ? m.Height + gap : 0f); // a new bar never pops in under a finger
                return;
            }
            int tiles = HouseBannerRotation.Dual(w, h, u) ? 2 : 1;
            int budget = svc.Remote.PerAppBudget("banner", svc.Config.AppId);
            var cs = Rotation.Current(ready, tiles, budget);
            if (cs.Count == 0) { Hide(); SetHeight(0f); return; }
            string key = string.Join("|", cs.ConvertAll(c => MiniJson.Str(c, "promotedAppId"))) + $"@{m.Height}:{gap}:{w}";
            if (key != shownKey)
            {
                bool wasHidden = bar == null;
                Build(cs, m, gap, u);
                shownKey = key;
                foreach (var (slot, id) in Rotation.OnShown(cs, Now, wasHidden))
                    svc.Track("impression", HouseAdsProtocol.BannerImpression(svc.Pacing.DeviceId, id, slot));
                Save();
            }
            SetHeight(m.Height + gap);
        }

        void Build(List<Dictionary<string, object>> cs, BannerMetrics m, float gap, float u)
        {
            bar?.RemoveFromHierarchy();
            bar = new VisualElement { name = "HouseBanner" };
            bar.AddToClassList("coddy-banner");
            bar.style.height = m.Height + gap;
            bar.style.paddingBottom = gap;
            string lang = HouseText.Lang2(svc.Config.UiLanguage());
            for (int i = 0; i < cs.Count; i++)
            {
                if (i > 0)
                {
                    var div = new VisualElement();
                    div.AddToClassList("coddy-banner__divider");
                    bar.Add(div);
                }
                bar.Add(Tile(cs[i], m, lang, cs.Count > 1, u));
            }
            root.Add(bar);
        }

        VisualElement Tile(Dictionary<string, object> c, BannerMetrics m, string lang, bool dual, float u)
        {
            var tile = new VisualElement();
            tile.AddToClassList("coddy-banner__tile");
            tile.style.paddingLeft = m.HPad;
            tile.style.paddingRight = m.HPad;
            var icon = new Image { image = svc.Images.Icon(MiniJson.Str(c, "iconUrl")), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            icon.AddToClassList("coddy-banner__icon");
            icon.style.width = m.Icon;
            icon.style.height = m.Icon;
            tile.Add(icon);
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.AddToClassList("coddy-banner__texts");
            texts.style.marginLeft = m.Gap;
            texts.style.marginRight = m.Gap;
            tile.Add(texts);
            string full = HouseText.PickI18n(MiniJson.Get(c, "titleI18n"), lang);
            if (full == "") full = MiniJson.Str(c, "title");
            string shortT = HouseText.ShortTitle(c, full, lang);
            var title = UiKit.Text(full, "coddy-banner__title");
            title.style.fontSize = m.TitlePx;
            texts.Add(title);
            title.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                // the full name does not fit: the short name; the title keeps its ellipsis as a last resort
                if (shortT != "" && title.text == full && title.MeasureTextSize(full, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x > title.layout.width)
                    title.text = shortT;
            });
            string slogan = HouseText.SloganFor(c, lang);
            if (m.ShowSub && slogan != "")
            {
                var sub = UiKit.Text(slogan, "coddy-banner__slogan");
                sub.style.fontSize = m.SubPx;
                texts.Add(sub);
                sub.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    // never a cut "…" slogan: hide it when it does not fit on one line
                    bool fits = sub.MeasureTextSize(slogan, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x <= sub.layout.width + 0.5f;
                    sub.style.display = fits ? DisplayStyle.Flex : DisplayStyle.None;
                });
            }
            var install = new Button { text = HouseText.InstallText(lang, svc.Config.InstallLabel?.Invoke()) };
            install.AddToClassList(dual ? "coddy-banner__chip" : "coddy-banner__install");
            install.style.height = dual ? Mathf.Min(m.BtnH, Mathf.Round(30f * u)) : m.BtnH;
            install.style.fontSize = m.BtnPx;
            install.style.paddingLeft = m.BtnPad;
            install.style.paddingRight = m.BtnPad;
            install.focusable = false;
            install.clicked += () => Click(c);
            tile.Add(install);
            // the whole tile is the tap target (>= 48 dp tall when the cap allows), released over the tile
            tile.RegisterCallback<PointerUpEvent>(e => { if (e.target != install) Click(c); });
            return tile;
        }

        void Click(Dictionary<string, object> c)
        {
            if (!Rotation.TapAccepted(Now)) return; // the bar just appeared / moved: this tap was meant for what was there
            svc.OpenStore(c, "banner", 3);
        }

        void Hide()
        {
            if (bar == null) return;
            bar.RemoveFromHierarchy();
            bar = null;
            shownKey = "";
            Rotation.OnHidden();
        }

        void SetHeight(float h)
        {
            if (Mathf.Abs(h - Height) < 0.5f) return;
            if (h > 0f && Height > 0f) Rotation.LayoutMoved(Now); // the layout under a showing bar moved: fresh grace
            Height = h;
            HeightChanged?.Invoke(h);
        }

        void Save()
        {
            file.Data = Rotation.ToJson();
            file.Save();
        }
    }
}
