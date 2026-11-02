// CoddyKit (Unity) — keeps a UI Toolkit root inside the safe area (SafeArea.Edges) and lifts its bottom above the house
// banner: content bottom = max(safe bottom, banner height) (CoddyBanner contract: the banner sits ABOVE the bottom safe
// area and its height already counts that gap). overlay = true uses OverlayEdges (status bar reserved at the top) for
// edge-anchored overlays. Re-checks on geometry change and every 500 ms (cheap: cached JNI read, 4 floats compared).
// UNCOMPILED until the Unity editor is installed.
using System;
using UnityEngine.UIElements;

namespace CoddyKit.Ui
{
    public sealed class SafeAreaPadding
    {
        public readonly VisualElement Root;
        public readonly bool Overlay;
        /// <summary>Extra bottom inset in panel px (the house banner height). Null = none.</summary>
        public Func<float> BannerHeight;
        /// <summary>Last applied gaps (panel px).</summary>
        public Insets Applied { get; private set; }
        /// <summary>false = the game paints that edge itself (a top bar / tab bar whose background runs under the status /
        /// navigation bar and pads its own content by Applied.Top / Applied.Bottom via Changed). Default: pad both.</summary>
        public bool PadTop = true, PadBottom = true;
        public event Action<Insets> Changed;

        SafeAreaPadding(VisualElement root, bool overlay)
        {
            Root = root;
            Overlay = overlay;
        }

        public static SafeAreaPadding Attach(VisualElement root, bool overlay = false, Func<float> bannerHeight = null, bool padTop = true, bool padBottom = true)
        {
            var p = new SafeAreaPadding(root, overlay) { BannerHeight = bannerHeight, PadTop = padTop, PadBottom = padBottom };
            root.RegisterCallback<GeometryChangedEvent>(_ => p.Apply());
            root.schedule.Execute(p.Apply).Every(500);
            p.Apply();
            return p;
        }

        public void Apply()
        {
            var panelRoot = Root.panel?.visualTree;
            if (panelRoot == null) return;
            float w = panelRoot.layout.width, h = panelRoot.layout.height;
            if (float.IsNaN(w) || w <= 0 || float.IsNaN(h) || h <= 0) return;
            Insets e = Overlay ? SafeArea.OverlayEdges(w, h) : SafeArea.Edges(w, h);
            float banner = BannerHeight != null ? BannerHeight() : 0f;
            e.Bottom = SafeAreaMath.ContentBottom(e.Bottom, banner);
            if (e.Equals(Applied)) return;
            Applied = e;
            Root.style.paddingLeft = e.Left;
            Root.style.paddingTop = PadTop ? e.Top : 0f;
            Root.style.paddingRight = e.Right;
            Root.style.paddingBottom = PadBottom ? e.Bottom : 0f;
            Changed?.Invoke(e);
        }
    }
}
