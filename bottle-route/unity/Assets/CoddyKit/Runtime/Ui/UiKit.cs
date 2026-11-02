// CoddyKit (Unity) — UI Toolkit factories with the fleet design-2 look (games/tools/ui README §1 + ui_kit.gd, owner
// 2026-10-26 redesign): calm night console, hue-tinted surfaces, ONE primary per card (the pill on the right), cards
// flat on surface_variant, sheets one step darker with a pill grab handle, round close chip, overline section heads,
// 84 px (48 dp) touch targets, Body >= 25 px / Caption >= 21 px. Styles: CoddyKit.uss + generated CoddyTokens.uss,
// imported by the runtime theme CoddyRuntimeTheme.tss (the PanelSettings theme). Low tier (#114): no icon tiles, action
// pills become coloured text, flat cards — at most +2 draws per page.
// Input rule (UI.md §4, LESSONS W66): kit buttons act through Button.clicked (one Clickable per button: one tap = one
// action). Never also register MouseDownEvent / MouseUpEvent on the same element (UI Toolkit synthesises mouse events from
// touches) — Tools/check_unity_sources.py fails the build when game code does.
// UNCOMPILED until the Unity editor is installed.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoddyKit.Ui
{
    public static class UiKit
    {
        /// <summary>The game's current tier is Low: flat variants (set by the game whenever the tier changes, before building).</summary>
        public static bool Low;
        /// <summary>Optional tap sound for every kit button.</summary>
        public static Action TapSound;

        public const float TouchMin = 84f;

        // ------------------------------------------------------------ text

        public static Label Text(string text, string cls = "coddy-body")
        {
            var l = new Label(text ?? "");
            l.AddToClassList(cls);
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        /// <summary>Sets a label's text only when it changed (LESSONS A10: no per-frame text work).</summary>
        public static void SetText(TextElement l, string text)
        {
            text ??= "";
            if (l != null && l.text != text) l.text = text;
        }

        // ------------------------------------------------------------ buttons

        static void Wire(Button b, Action onPress)
        {
            b.clicked += () =>
            {
                TapSound?.Invoke();
                onPress?.Invoke();
            };
            b.focusable = false;
        }

        /// <summary>The single call to action of a card: primary = do it now (the game's primary colour), tonal = secondary.
        /// Low: coloured text without the pill box. A disabled pill stays readable (dimmed), never hidden.</summary>
        public static Button ActionPill(string text, Action onPress, bool enabled = true, string kind = "primary")
        {
            var b = new Button { text = text ?? "" };
            b.AddToClassList(Low ? "coddy-action-text" : "coddy-pill");
            if (kind != "primary") b.AddToClassList(Low ? "coddy-action-text--tonal" : "coddy-pill--" + kind);
            b.SetEnabled(enabled);
            Wire(b, onPress);
            return b;
        }

        public static Button PrimaryButton(string text, Action onPress)
        {
            var b = new Button { text = text ?? "" };
            b.AddToClassList("coddy-button");
            Wire(b, onPress);
            return b;
        }

        // ------------------------------------------------------------ icons (never unicode symbols on buttons)

        /// <summary>Kit icon (Resources/coddykit/icons/&lt;name&gt;.png, white on transparent, Tools/gen_kit_icons.py):
        /// close, back, home, mail, lock, pin, check, gear, star. A button glyph from a font is drawn only when some font
        /// has it — ✉ and ⌂ were invisible / broken on a device in every language (SE 2.0.0, 2026-10-30).</summary>
        public static Texture2D IconTexture(string name) => Resources.Load<Texture2D>("coddykit/icons/" + name);

        /// <summary>An icon element tinted by USS (-unity-background-image-tint-color, default = the text colour token).</summary>
        public static VisualElement Icon(string name, string cls = "coddy-icon")
        {
            var e = new VisualElement { name = "Icon-" + name, pickingMode = PickingMode.Ignore };
            foreach (var c in cls.Split(' ')) if (c.Length > 0) e.AddToClassList(c);
            var t = IconTexture(name);
            if (t != null) e.style.backgroundImage = new StyleBackground(t);
            return e;
        }

        /// <summary>Puts an icon (and an optional short label such as a count) inside a button instead of its text.</summary>
        public static Button WithIcon(Button b, string icon, string label = null, string iconCls = "coddy-icon")
        {
            b.text = "";
            b.Clear();
            b.AddToClassList("coddy-has-icon");
            b.Add(Icon(icon, iconCls));
            if (!string.IsNullOrEmpty(label))
            {
                var l = new Label(label) { pickingMode = PickingMode.Ignore };
                l.AddToClassList("coddy-icon-label");
                b.Add(l);
            }
            return b;
        }

        /// <summary>Top-right close of every page / sheet / window: 84 px touch rect, 56 px round chip (Low: glyph only).</summary>
        public static Button CloseButton(Action onClose, string tooltip = "Close")
        {
            var b = WithIcon(new Button { tooltip = tooltip }, "close");
            b.name = "Close";
            b.AddToClassList(Low ? "coddy-close--flat" : "coddy-close");
            Wire(b, onClose);
            return b;
        }

        // ------------------------------------------------------------ page header

        public sealed class PageHeader
        {
            public VisualElement Root;
            public VisualElement Tile;
            public Label Title;
            public Label Sub;
            public Button Close;

            /// <summary>Summary line ("Supply 46 MW · Demand 44 MW · +4 %"); set only when it changed, hidden when empty.</summary>
            public void SetSub(string sub)
            {
                bool show = !string.IsNullOrEmpty(sub);
                Sub.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show) SetText(Sub, sub);
            }

            public void SetTitle(string title) => SetText(Title, title);
        }

        /// <summary>[52 px icon tile] Title + one summary line [close], a hairline under it.</summary>
        public static PageHeader Header(string title, string sub = "", Action onClose = null, Texture2D icon = null, string closeLabel = "Close")
        {
            var h = new PageHeader { Root = new VisualElement() };
            h.Root.name = "PageHeader";
            h.Root.AddToClassList("coddy-page-header");
            var row = new VisualElement();
            row.AddToClassList("coddy-page-header__row");
            row.pickingMode = PickingMode.Ignore;
            h.Root.Add(row);
            if (!Low && icon != null)
            {
                h.Tile = IconTile(icon, "coddy-page-header__tile");
                row.Add(h.Tile);
            }
            var texts = new VisualElement();
            texts.AddToClassList("coddy-page-header__texts");
            texts.pickingMode = PickingMode.Ignore;
            row.Add(texts);
            h.Title = Text(title, "coddy-title");
            texts.Add(h.Title);
            h.Sub = Text(sub, "coddy-caption-muted");
            texts.Add(h.Sub);
            h.SetSub(sub);
            if (onClose != null)
            {
                h.Close = CloseButton(onClose, closeLabel);
                row.Add(h.Close);
            }
            var line = new VisualElement();
            line.AddToClassList("coddy-divider");
            h.Root.Add(line);
            return h;
        }

        // ------------------------------------------------------------ cards + rows

        public static VisualElement IconTile(Texture2D icon, string cls = "coddy-tile")
        {
            var tile = new VisualElement();
            tile.AddToClassList(cls);
            tile.pickingMode = PickingMode.Ignore;
            var img = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            img.AddToClassList("coddy-tile__icon");
            tile.Add(img);
            return tile;
        }

        public sealed class ListCard
        {
            public VisualElement Root;
            public VisualElement Head;
            public Label Title;
            public Label Sub;
            public VisualElement Side;

            public void SetSub(string sub)
            {
                bool show = !string.IsNullOrEmpty(sub);
                Sub.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show) SetText(Sub, sub);
            }

            /// <summary>ONE action on the right of the head row (a pill, or two small stepper pills in one container).</summary>
            public void SetSide(VisualElement action)
            {
                Side.Clear();
                if (action != null) Side.Add(action);
            }
        }

        /// <summary>The list row of every tab: [tile] title (whole words) + one grey line [badge] [side action].
        /// Low: no tile; a badge becomes the head of the grey line (no extra text draw).</summary>
        public static ListCard Card(string title, string sub = "", Texture2D icon = null, bool locked = false, bool selected = false, string badge = "")
        {
            var c = new ListCard { Root = new VisualElement() };
            c.Root.name = "ListCard";
            c.Root.AddToClassList(Low ? "coddy-list-card--flat" : "coddy-list-card");
            if (locked) c.Root.AddToClassList("coddy-list-card--locked");
            if (selected) c.Root.AddToClassList("coddy-list-card--selected");
            c.Head = new VisualElement();
            c.Head.AddToClassList("coddy-list-card__head");
            c.Root.Add(c.Head);
            if (!Low && icon != null) c.Head.Add(IconTile(icon));
            var texts = new VisualElement();
            texts.AddToClassList("coddy-list-card__texts");
            texts.pickingMode = PickingMode.Ignore;
            c.Head.Add(texts);
            c.Title = Text(title, "coddy-label-strong");
            c.Title.AddToClassList("coddy-list-card__title");
            texts.Add(c.Title);
            string line = Low && !string.IsNullOrEmpty(badge) ? (string.IsNullOrEmpty(sub) ? badge : badge + "  ·  " + sub) : sub;
            c.Sub = Text(line, "coddy-caption-muted");
            texts.Add(c.Sub);
            c.SetSub(line);
            if (!Low && !string.IsNullOrEmpty(badge))
            {
                var b = Text(badge, "coddy-badge");
                c.Head.Add(b);
            }
            c.Side = new VisualElement();
            c.Side.AddToClassList("coddy-list-card__side");
            c.Head.Add(c.Side);
            return c;
        }

        /// <summary>OVERLINE section head inside a list (+12 px air above), optional trailing value. UI Toolkit has no
        /// text-transform: the upper-casing is done here with the current culture.</summary>
        public static VisualElement SectionHeader(string text, string trailing = "")
        {
            var row = new VisualElement();
            row.name = "Section";
            row.AddToClassList("coddy-section");
            row.pickingMode = PickingMode.Ignore;
            row.Add(Text((text ?? "").ToUpper(System.Globalization.CultureInfo.CurrentCulture), "coddy-overline"));
            if (!string.IsNullOrEmpty(trailing)) row.Add(Text(trailing, "coddy-caption-muted"));
            return row;
        }

        /// <summary>Nothing in the list yet: a quiet card — title, one or two lines, optional tonal action.</summary>
        public static VisualElement EmptyCard(string title, string body, string action = null, Action onAction = null)
        {
            var card = new VisualElement();
            card.name = "EmptyCard";
            card.AddToClassList(Low ? "coddy-list-card--flat" : "coddy-empty-card");
            card.Add(Text(title, "coddy-label-strong"));
            if (!Low && !string.IsNullOrEmpty(body)) card.Add(Text(body, "coddy-body-muted"));
            if (!string.IsNullOrEmpty(action)) card.Add(ActionPill(action, onAction, true, "tonal"));
            return card;
        }

        // ------------------------------------------------------------ scrolling (games/CLAUDE.md §16)

        // Which pointers are down on a list. Device pass 10-28: a plain counter leaked — a press on a row's pill is captured
        // by the button, its PointerUp never reached the list, the count stayed > 0 and the list stopped refreshing for the
        // rest of the session (stale "Set to 5/10", an empty Grid tab). Ids, ups caught at the panel root too, and a press
        // that saw no event for 3 s is dropped.
        sealed class HoldState
        {
            public readonly HashSet<int> Ids = new HashSet<int>();
            public float LastEvent;
            public bool RootHooked;
            public int Pointers => Ids.Count;
        }

        /// <summary>A finger-scrolling list: elastic touch scroll with inertia, no visible scroller, a drag that starts on a
        /// button / card scrolls (UI Toolkit ScrollView claims the pointer after its drag threshold), a short touch clicks.
        /// Held(list) tells a live refresh to wait while a finger is down (W62/W63: never rebuild under a finger).</summary>
        public static ScrollView List(ScrollViewMode mode = ScrollViewMode.Vertical)
        {
            var sv = new ScrollView(mode);
            sv.AddToClassList("coddy-list");
            sv.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            sv.scrollDecelerationRate = 0.135f;
            sv.elasticity = 0.1f;
            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sv.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            var hold = new HoldState();
            sv.userData = hold;
            sv.RegisterCallback<PointerDownEvent>(e => { hold.Ids.Add(e.pointerId); hold.LastEvent = Time.unscaledTime; }, TrickleDown.TrickleDown);
            sv.RegisterCallback<PointerMoveEvent>(e => { if (hold.Ids.Contains(e.pointerId)) hold.LastEvent = Time.unscaledTime; }, TrickleDown.TrickleDown);
            sv.RegisterCallback<PointerUpEvent>(e => hold.Ids.Remove(e.pointerId), TrickleDown.TrickleDown);
            sv.RegisterCallback<PointerCancelEvent>(_ => hold.Ids.Clear(), TrickleDown.TrickleDown);
            sv.RegisterCallback<AttachToPanelEvent>(e =>
            {
                if (hold.RootHooked || e.destinationPanel == null) return;
                hold.RootHooked = true;
                var root = e.destinationPanel.visualTree;
                root.RegisterCallback<PointerUpEvent>(u => hold.Ids.Remove(u.pointerId), TrickleDown.TrickleDown);
                root.RegisterCallback<PointerCancelEvent>(_ => hold.Ids.Clear(), TrickleDown.TrickleDown);
            });
            return sv;
        }

        public const float HoldStaleS = 3f;

        public static bool Held(ScrollView list)
        {
            if (!(list?.userData is HoldState h) || h.Ids.Count == 0) return false;
            if (Time.unscaledTime - h.LastEvent > HoldStaleS) { h.Ids.Clear(); return false; }
            return true;
        }

        // ------------------------------------------------------------ sheet (portrait window)

        public sealed class Sheet
        {
            public VisualElement Scrim;
            public VisualElement Panel;
            public PageHeader Header;
            public ScrollView Body;
            public VisualElement Actions;
            public event Action Closed;

            public bool IsOpen => Scrim.parent != null;

            public void Open(VisualElement host)
            {
                if (IsOpen) return;
                host.Add(Scrim);
                Scrim.BringToFront();
            }

            public void Close()
            {
                if (!IsOpen) return;
                Scrim.RemoveFromHierarchy();
                Closed?.Invoke();
            }
        }

        /// <summary>Bottom sheet over the dimmed live game: content-sized up to maxShare of the panel height, rounded top only,
        /// pill grab handle, header with close chip, body scrolls inside, optional action row. Bottom padding = the safe
        /// area + house banner inset the caller passes (SafeAreaPadding.ContentBottom).</summary>
        public static Sheet BottomSheet(string title, string sub = "", Texture2D icon = null, float maxShare = 0.86f, string closeLabel = "Close")
        {
            var s = new Sheet { Scrim = new VisualElement(), Panel = new VisualElement() };
            s.Scrim.name = "SheetScrim";
            s.Scrim.AddToClassList("coddy-scrim");
            // a tap on the dim area closes the sheet; the panel stops the event from reaching the scrim
            s.Scrim.RegisterCallback<PointerUpEvent>(e => { if (e.target == s.Scrim) s.Close(); });
            s.Panel.AddToClassList("coddy-sheet");
            s.Panel.style.maxHeight = Length.Percent(maxShare * 100f);
            s.Scrim.Add(s.Panel);
            var handle = new VisualElement();
            handle.AddToClassList("coddy-sheet__handle");
            s.Panel.Add(handle);
            s.Header = Header(title, sub, s.Close, icon, closeLabel);
            s.Panel.Add(s.Header.Root);
            s.Body = List();
            s.Body.AddToClassList("coddy-sheet__body");
            s.Panel.Add(s.Body);
            s.Actions = new VisualElement();
            s.Actions.AddToClassList("coddy-sheet__actions");
            s.Panel.Add(s.Actions);
            return s;
        }
    }
}
