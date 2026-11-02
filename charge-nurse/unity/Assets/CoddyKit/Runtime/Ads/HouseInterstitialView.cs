// CoddyKit (Unity) — the house interstitial card (CoddyInterstitial.gd): full screen over the paused game; the promoted
// game's hero frames (<= 3, player's language + orientation; aspect kept — store text is never cropped) with a slow
// cross-fade + light pan on Medium / High, ONE still frame and no tween on Low; without a usable hero: a big icon on the
// game's colour (brandColor). Icon + title (one line, short name before an ellipsis) + one-line slogan in the player's
// language (banner rules, hidden when it does not fit) + a big translated Install (Play page + referrer) + a close button
// with a 3 s countdown (then ✕). No "free / trial" anywhere. UNCOMPILED until the editor is installed.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using CoddyKit.Ui;

namespace CoddyKit.Ads
{
    public sealed class HouseInterstitialView : MonoBehaviour
    {
        public const float CountdownS = 3f;
        public const float FrameS = 2.8f;

        HouseAdsService svc;
        UIDocument doc;
        VisualElement root;
        VisualElement card;
        readonly List<Texture2D> frames = new List<Texture2D>();
        readonly List<Image> heroImages = new List<Image>();
        Action<bool> finished;
        Button close;
        float shownFor;
        float frameFor;
        int frameIndex;
        bool animate;

        public bool Visible => card != null;

        public void Init(HouseAdsService service, PanelSettings panel)
        {
            svc = service;
            var go = new GameObject("CoddyInterstitial");
            go.transform.SetParent(transform, false);
            doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.sortingOrder = 100;
            root = doc.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
        }

        public void Show(Dictionary<string, object> c, List<Texture2D> heroes, Texture2D icon, string lang, Action<bool> onFinished)
        {
            Clear();
            finished = onFinished;
            frames.AddRange(heroes ?? new List<Texture2D>());
            animate = !svc.Config.LowEnd() && frames.Count > 1;
            shownFor = 0f;
            frameFor = 0f;
            frameIndex = 0;

            card = new VisualElement { name = "HouseInterstitial" };
            card.AddToClassList("coddy-interstitial");
            card.style.backgroundColor = BrandColor(c, icon);
            root.Add(card);

            var stage = new VisualElement { pickingMode = PickingMode.Ignore };
            stage.AddToClassList("coddy-interstitial__stage");
            card.Add(stage);
            if (frames.Count > 0)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    var img = new Image { image = frames[i], scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                    img.AddToClassList("coddy-interstitial__hero");
                    if (animate) img.AddToClassList("coddy-interstitial__hero--animated");
                    if (i == 0) img.AddToClassList("coddy-interstitial__hero--on");
                    stage.Add(img);
                    heroImages.Add(img);
                }
            }
            else if (icon != null)
            {
                var big = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                big.AddToClassList("coddy-interstitial__big-icon");
                stage.Add(big);
            }

            var content = new VisualElement { pickingMode = PickingMode.Ignore };
            content.AddToClassList("coddy-interstitial__content");
            card.Add(content);
            SafeAreaPadding.Attach(content, overlay: true);

            close = new Button { text = Mathf.CeilToInt(CountdownS).ToString(), tooltip = "Close" };
            close.AddToClassList("coddy-close");
            close.AddToClassList("coddy-interstitial__close");
            close.focusable = false;
            close.SetEnabled(false);
            close.clicked += () => Finish(false);
            content.Add(close);

            var panel = new VisualElement();
            panel.AddToClassList("coddy-interstitial__panel");
            content.Add(panel);
            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("coddy-interstitial__head");
            panel.Add(head);
            if (icon != null)
            {
                var ic = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                ic.AddToClassList("coddy-interstitial__icon");
                head.Add(ic);
            }
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.AddToClassList("coddy-interstitial__texts");
            head.Add(texts);
            string full = HouseText.PickI18n(MiniJson.Get(c, "titleI18n"), lang);
            if (full == "") full = MiniJson.Str(c, "title");
            string shortT = HouseText.ShortTitle(c, full, lang);
            var title = UiKit.Text(full, "coddy-interstitial__title");
            texts.Add(title);
            title.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (shortT != "" && title.text == full && title.MeasureTextSize(full, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x > title.layout.width)
                    title.text = shortT;
            });
            string slogan = HouseText.SloganFor(c, lang);
            if (slogan != "")
            {
                var sub = UiKit.Text(slogan, "coddy-interstitial__slogan");
                texts.Add(sub);
                sub.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    bool fits = sub.MeasureTextSize(slogan, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x <= sub.layout.width + 0.5f;
                    sub.style.display = fits ? DisplayStyle.Flex : DisplayStyle.None;
                });
            }
            var install = UiKit.PrimaryButton(HouseText.InstallText(lang, svc.Config.InstallLabel?.Invoke()), () => Finish(true));
            install.AddToClassList("coddy-interstitial__install");
            panel.Add(install);
        }

        void Update()
        {
            if (card == null) return;
            float dt = Time.unscaledDeltaTime;
            shownFor += dt;
            if (!close.enabledSelf)
            {
                int left = Mathf.CeilToInt(CountdownS - shownFor);
                if (left <= 0)
                {
                    UiKit.WithIcon(close, "close");
                    close.SetEnabled(true);
                }
                else UiKit.SetText(close, left.ToString());
            }
            if (!animate) return;
            frameFor += dt;
            if (frameFor < FrameS) return;
            frameFor = 0f;
            heroImages[frameIndex].RemoveFromClassList("coddy-interstitial__hero--on");
            frameIndex = (frameIndex + 1) % heroImages.Count;
            heroImages[frameIndex].AddToClassList("coddy-interstitial__hero--on");
        }

        static Color BrandColor(Dictionary<string, object> c, Texture2D icon)
        {
            string hex = MiniJson.Str(c, "brandColor");
            if (hex != "" && ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out Color col)) return Darken(col);
            if (icon != null && icon.isReadable)
            {
                // the icon's average colour (a 4 × 4 sample is enough and costs nothing)
                Color sum = Color.black;
                int n = 0;
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                    {
                        sum += icon.GetPixelBilinear((x + 0.5f) / 4f, (y + 0.5f) / 4f);
                        n++;
                    }
                return Darken(sum / n);
            }
            return new Color(0.039f, 0.090f, 0.129f, 1f);
        }

        static Color Darken(Color c) => new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 1f);

        void Finish(bool opened)
        {
            var cb = finished;
            Clear();
            cb?.Invoke(opened);
        }

        void Clear()
        {
            card?.RemoveFromHierarchy();
            card = null;
            heroImages.Clear();
            foreach (var t in frames) if (t != null) Destroy(t); // the card's frames go with the card (disk keeps them)
            frames.Clear();
            finished = null;
        }
    }
}
