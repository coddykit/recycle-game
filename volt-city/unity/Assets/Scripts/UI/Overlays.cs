// Volt City UI — overlays above the live diorama: the modal bottom sheet host (report, offline, event cards, district /
// fault / plant cards, forecast, goals, settings, language, graphics), the guide's spotlight (CoachOverlay) and the
// first-start pager. Kit design 2 everywhere (UiKit.BottomSheet: rounded top, pill handle, round close chip).
// UNCOMPILED until the Unity editor is installed.
using System;
using System.Collections.Generic;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using VoltCity.Game;
using TabView = VoltCity.Game.TabView; // Unity 6 UI Toolkit also has a TabView control

namespace VoltCity.UI
{
    /// <summary>One modal sheet at a time; a live sheet refreshes at <= 4 Hz from its TabView source.</summary>
    public sealed class SheetHost
    {
        readonly VisualElement layer;
        readonly Func<float> bottomInset;
        UiKit.Sheet sheet;
        RowList list;
        Func<TabView> source;
        Button primary;
        RowAction primaryAct;
        Func<RowAction, bool> run;
        float acc;
        public string Id { get; private set; } = "";
        public bool IsOpen => sheet != null && sheet.IsOpen;
        /// <summary>The open sheet's panel (null when closed): the coach keeps its bubble above it.</summary>
        public VisualElement Panel => IsOpen ? sheet.Panel : null;
        public event Action<string> Closed;
        public readonly Dictionary<string, VisualElement> Targets = new Dictionary<string, VisualElement>();

        public SheetHost(VisualElement layer, Func<float> bottomInset)
        {
            this.layer = layer;
            this.bottomInset = bottomInset;
        }

        UiKit.Sheet Make(string id, string title, string sub, string icon, string closeLabel)
        {
            Close();
            Id = id;
            sheet = UiKit.BottomSheet(title, sub, UiKit.Low ? null : Icons.Get(icon), 0.86f, closeLabel);
            sheet.Panel.style.paddingBottom = 24f + bottomInset();
            sheet.Closed += () =>
            {
                string was = Id;
                Id = "";
                source = null;
                Targets.Clear();
                Closed?.Invoke(was);
            };
            return sheet;
        }

        /// <summary>A sheet rendered from a TabView (refreshed while open).</summary>
        public void Open(string id, Func<TabView> src, Func<RowAction, bool> runAction, Action<string, string> openCard, string closeLabel)
        {
            var tv = src();
            Make(id, tv.Title, tv.Sub, tv.Icon, closeLabel);
            source = src;
            run = runAction;
            list = new RowList(sheet.Body, runAction, openCard);
            primary = null;
            primaryAct = null;
            Refresh(tv);
            sheet.Open(layer);
        }

        /// <summary>A sheet with custom content (settings, language, graphics, explainer).</summary>
        public UiKit.Sheet OpenCustom(string id, string title, string sub, string icon, string closeLabel)
        {
            Make(id, title, sub, icon, closeLabel);
            source = null;
            list = null;
            sheet.Open(layer);
            return sheet;
        }

        public void Close()
        {
            if (sheet != null && sheet.IsOpen) sheet.Close();
            sheet = null;
        }

        public void Tick(float dt)
        {
            if (!IsOpen || source == null) return;
            acc += dt;
            if (acc < 0.25f) return;
            acc = 0f;
            Refresh(source());
        }

        void Refresh(TabView tv)
        {
            sheet.Header.SetTitle(tv.Title);
            sheet.Header.SetSub(tv.Sub);
            list.Render(tv);
            if (tv.Primary != null)
            {
                if (primary == null)
                {
                    primary = UiKit.PrimaryButton(tv.Primary.Label, () => { if (primaryAct != null) run(primaryAct); });
                    primary.AddToClassList("vc-sheet-primary");
                    sheet.Actions.Add(primary);
                }
                primaryAct = tv.Primary;
                UiKit.SetText(primary, tv.Primary.Label);
                primary.SetEnabled(tv.Primary.Enabled);
                primary.name = tv.Primary.Target;
            }
            else if (primary != null) { primary.RemoveFromHierarchy(); primary = null; primaryAct = null; }
            Targets.Clear();
            foreach (var kv in list.Targets) Targets[kv.Key] = kv.Value;
            if (primary != null && !string.IsNullOrEmpty(primary.name)) Targets[primary.name] = primary;
        }
    }

    /// <summary>The guide's spotlight: four dim rects around a hole (taps in the hole reach the real control), a ring, and a
    /// bubble with "Step n of 9", one short sentence and Skip. A tap on the dim = ONE wrong tap (TapGesture: release-only,
    /// one registration per element), the ring flashes; the bubble never covers the hole (above / below it).</summary>
    public sealed class CoachOverlay
    {
        public readonly VisualElement Root = new VisualElement { name = "Coach", pickingMode = PickingMode.Ignore };
        readonly VisualElement[] dims = new VisualElement[4];
        readonly VisualElement ring, bubble;
        readonly Label stepLabel, text;
        readonly Button skip;
        Rect lastHole = new Rect(-1, -1, 0, 0);
        int lastFlash = -1;
        bool lastSoft;

        public CoachOverlay(Action onWrongTap, Action onSkip, string skipLabel)
        {
            Root.AddToClassList("vc-coach");
            for (int i = 0; i < 4; i++)
            {
                var d = new VisualElement { name = "CoachDim" };
                d.AddToClassList("vc-coach__dim");
                TapGesture.Attach(d, onWrongTap);
                dims[i] = d;
                Root.Add(d);
            }
            ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("vc-coach__ring");
            Root.Add(ring);
            bubble = new VisualElement();
            bubble.AddToClassList("vc-coach__bubble");
            stepLabel = UiKit.Text("", "coddy-overline");
            text = UiKit.Text("", "coddy-body");
            skip = UiKit.ActionPill(skipLabel, onSkip, true, "tonal");
            skip.name = "coach.skip";
            var row = new VisualElement();
            row.AddToClassList("vc-coach__bubble-row");
            row.Add(stepLabel);
            row.Add(skip);
            bubble.Add(row);
            bubble.Add(text);
            Root.Add(bubble);
            Hide();
        }

        float lastKeepAbove = -1f;
        public bool Visible => Root.style.display == DisplayStyle.Flex;

        public void Hide()
        {
            Root.style.display = DisplayStyle.None;
            lastHole = new Rect(-1, -1, 0, 0);
        }

        /// <summary>hole in panel coordinates (y down). Writes styles only when the hole moved.</summary>
        /// <summary>keepAboveY: a target inside an open sheet passes the sheet's top — the bubble then sits above the
        /// whole sheet instead of over its content (device play 10-29: it hid "3,700 homes dark · storm" and the truck row).</summary>
        public void Show(Rect hole, string step, string sentence, int flash, float panelW, float panelH, bool soft = false, float keepAboveY = -1f)
        {
            Root.style.display = DisplayStyle.Flex;
            if (soft != lastSoft)
            {
                // soft assist (after the guide): ring + bubble only — no dim, nothing blocks, Skip hidden
                lastSoft = soft;
                foreach (var d in dims) d.style.display = soft ? DisplayStyle.None : DisplayStyle.Flex;
                skip.style.display = soft ? DisplayStyle.None : DisplayStyle.Flex;
                bubble.pickingMode = soft ? PickingMode.Ignore : PickingMode.Position;
                lastHole = new Rect(-1, -1, 0, 0);
            }
            UiKit.SetText(stepLabel, step);
            UiKit.SetText(text, sentence);
            if (flash != lastFlash)
            {
                lastFlash = flash;
                ring.RemoveFromClassList("vc-coach__ring--flash");
                if (flash > 0)
                {
                    ring.AddToClassList("vc-coach__ring--flash");
                    ring.schedule.Execute(() => ring.RemoveFromClassList("vc-coach__ring--flash")).StartingIn(450);
                }
            }
            if (hole == lastHole && keepAboveY == lastKeepAbove) return;
            lastHole = hole;
            lastKeepAbove = keepAboveY;
            float pad = 10f;
            float x0 = Mathf.Max(0, hole.xMin - pad), y0 = Mathf.Max(0, hole.yMin - pad), x1 = Mathf.Min(panelW, hole.xMax + pad), y1 = Mathf.Min(panelH, hole.yMax + pad);
            Place(dims[0], 0, 0, panelW, y0);                    // top
            Place(dims[1], 0, y1, panelW, panelH - y1);          // bottom
            Place(dims[2], 0, y0, x0, y1 - y0);                  // left
            Place(dims[3], x1, y0, panelW - x1, y1 - y0);        // right
            Place(ring, x0, y0, x1 - x0, y1 - y0);
            // bubble: below the hole when it is in the upper half, else above it
            bool below = (y0 + y1) * 0.5f < panelH * 0.5f;
            bubble.style.left = 24f;
            bubble.style.right = 24f;
            if (below) { bubble.style.top = y1 + 18f; bubble.style.bottom = StyleKeyword.Auto; }
            else
            {
                float top = keepAboveY >= 0f ? Mathf.Min(y0, keepAboveY) : y0;
                bubble.style.bottom = panelH - top + 18f;
                bubble.style.top = StyleKeyword.Auto;
            }
        }

        static void Place(VisualElement e, float x, float y, float w, float h)
        {
            e.style.left = x;
            e.style.top = y;
            e.style.width = Mathf.Max(0, w);
            e.style.height = Mathf.Max(0, h);
        }
    }

    /// <summary>First-start pager (3 pages, DESIGN section 9): hero frame, title, one line, dots, Next / Start, Skip.
    /// Hero frames: Resources/ui/onboarding/hero_1..3 (bot-captured from the real loop once the editor exists — P3 open
    /// item); until then the P2 curtain art.</summary>
    public sealed class Pager
    {
        public readonly VisualElement Root = new VisualElement { name = "Pager" };
        readonly Loc loc;
        readonly Action done;
        readonly Image hero;
        readonly Label title, body;
        readonly VisualElement dots;
        readonly Button next;
        int page;
        public const int Pages = 3;

        public Pager(Loc loc, Texture2D fallbackHero, Action done)
        {
            this.loc = loc;
            this.done = done;
            Root.AddToClassList("vc-pager");
            // the hero is full-bleed (under the status bar too — 10-28: no dark band at the top); only the bottom panel
            // keeps its buttons above the navigation bar
            hero = new Image { scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore };
            hero.AddToClassList("vc-pager__hero");
            hero.userData = fallbackHero;
            Root.Add(hero);
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.AddToClassList("vc-pager__spacer");
            Root.Add(spacer);
            var foot = new VisualElement();
            foot.AddToClassList("vc-pager__foot");
            SafeAreaPadding.Attach(foot, true, null, padTop: false);
            var panel = new VisualElement();
            panel.AddToClassList("vc-pager__panel");
            title = UiKit.Text("", "coddy-headline");
            body = UiKit.Text("", "coddy-body-muted");
            dots = new VisualElement { pickingMode = PickingMode.Ignore };
            dots.AddToClassList("vc-pager__dots");
            for (int i = 0; i < Pages; i++) { var d = new VisualElement(); d.AddToClassList("vc-pager__dot"); dots.Add(d); }
            var row = new VisualElement();
            row.AddToClassList("vc-pager__row");
            var skip = UiKit.ActionPill(loc.T("pager.skip"), () => done(), true, "tonal");
            skip.name = "pager.skip";
            next = UiKit.PrimaryButton("", Next);
            next.name = "pager.next";
            next.AddToClassList("vc-pager__next");
            row.Add(skip);
            row.Add(next);
            panel.Add(title);
            panel.Add(body);
            panel.Add(dots);
            panel.Add(row);
            foot.Add(panel);
            Root.Add(foot);
            Show(0);
        }

        void Next()
        {
            if (page + 1 >= Pages) { done(); return; }
            Show(page + 1);
        }

        void Show(int p)
        {
            page = p;
            var tex = Resources.Load<Texture2D>("ui/onboarding/hero_" + (p + 1)) ?? hero.userData as Texture2D;
            hero.image = tex;
            UiKit.SetText(title, loc.T("pager." + (p + 1) + ".t"));
            UiKit.SetText(body, loc.T("pager." + (p + 1) + ".b"));
            UiKit.SetText(next, loc.T(p + 1 >= Pages ? "pager.start" : "pager.next"));
            for (int i = 0; i < dots.childCount; i++) dots[i].EnableInClassList("vc-pager__dot--on", i == p);
        }
    }
}
