// Volt City UI — small shared pieces: icon cache, the one-tap gesture for non-button elements (card bodies, needle, goal
// line, coach dim, world spotlight), the stepper / segmented side controls. UNCOMPILED until the editor is installed.
//
// One tap = one action (UI.md section 4, kit TapFilter): non-button elements register ONE PointerDownEvent and ONE
// PointerUpEvent (never Mouse*/Click events — Tools/check_unity_sources.py G3) and act on RELEASE only when the pointer
// moved < 12 dp and the press lasted < 400 ms; a press that turns into a list scroll is captured by the ScrollView and
// never releases here. A different device echo within 0.35 s / 24 dp is dropped by the TapFilter.
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using VoltCity.Game;

namespace VoltCity.UI
{
    public static class Icons
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>Assets/Resources/ui/icons/&lt;name&gt;.png (white glyphs, tinted by USS); null when missing.</summary>
        public static Texture2D Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (cache.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("ui/icons/" + name);
            if (t == null && name.StartsWith("event_")) t = Resources.Load<Texture2D>("ui/icons/event");
            if (t == null && name.StartsWith("wx_")) t = Resources.Load<Texture2D>("ui/icons/cloud");
            cache[name] = t;
            return t;
        }
    }

    public static class TapGesture
    {
        sealed class State { public readonly TapFilter Filter = new TapFilter(); public int Pointer = -1; }

        static PointerDevice Dev(string pointerType) =>
            pointerType == UnityEngine.UIElements.PointerType.touch ? PointerDevice.Touch : pointerType == UnityEngine.UIElements.PointerType.pen ? PointerDevice.Pen : PointerDevice.Mouse;

        /// <summary>The press landed on a kit button inside el (the button acts; the card body must not).</summary>
        static bool OnButton(IEventHandler target, VisualElement el)
        {
            for (var v = target as VisualElement; v != null && v != el; v = v.parent)
                if (v is Button) return true;
            return false;
        }

        /// <summary>Calls onTap once per physical tap on el (release, short, still). The element must be pickable.
        /// Presses on a Button inside el are left to the button (one tap = one action).</summary>
        public static void Attach(VisualElement el, Action onTap)
        {
            var st = new State();
            st.Filter.PxPerDp = 1.75f; // panel px per dp at the 720 px reference width (UI.md section 1)
            el.RegisterCallback<PointerDownEvent>(e =>
            {
                if (OnButton(e.target, el)) return;
                if (st.Pointer >= 0 && e.pointerId != st.Pointer) { st.Filter.Cancel(); return; }
                st.Pointer = e.pointerId;
                st.Filter.Press(new TapEvent { X = e.position.x, Y = e.position.y, Time = Time.realtimeSinceStartupAsDouble, Device = Dev(e.pointerType) });
            });
            el.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.pointerId != st.Pointer) return;
                st.Pointer = -1;
                if (st.Filter.Release(new TapEvent { X = e.position.x, Y = e.position.y, Time = Time.realtimeSinceStartupAsDouble, Device = Dev(e.pointerType) }))
                    onTap?.Invoke();
            });
            el.RegisterCallback<PointerCancelEvent>(_ => { st.Filter.Cancel(); st.Pointer = -1; });
            el.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (e.pointerId == st.Pointer)
                    st.Filter.Move(new TapEvent { X = e.position.x, Y = e.position.y, Time = Time.realtimeSinceStartupAsDouble, Device = Dev(e.pointerType) });
            });
        }
    }

    /// <summary>The side control of a list card built from its RowActions: one pill, a −/+ stepper (two 84 px pills = one
    /// control) or a segmented control. Update() changes labels / enabled / selected in place (no rebuild).</summary>
    public sealed class SideControl
    {
        public readonly VisualElement Root = new VisualElement();
        readonly List<Button> buttons = new List<Button>();
        readonly List<RowAction> acts = new List<RowAction>();

        public SideControl(List<RowAction> actions, Func<RowAction, bool> run)
        {
            bool segmented = actions.Count > 0 && actions[0].Kind == "segment";
            Root.AddToClassList(segmented ? "vc-segmented" : actions.Count > 1 ? "vc-stepper" : "vc-side");
            foreach (var a0 in actions)
            {
                var a = a0;
                acts.Add(a);
                Button b;
                if (segmented)
                {
                    b = new Button { text = a.Label, focusable = false };
                    b.AddToClassList("vc-segment");
                    b.clicked += () => run(acts[buttons.IndexOf(b)]);
                }
                else if (a.Kind == "status")
                {
                    // why it cannot be done yet: a lock + the reason, not a greyed price that reads as "broken" (Patron #173);
                    // a tap opens the row's card with the details
                    b = new Button { focusable = false };
                    b.AddToClassList("vc-status");
                    var icon = new Image { image = Icons.Get("lock"), pickingMode = PickingMode.Ignore };
                    icon.AddToClassList("vc-status__icon");
                    var text = UiKit.Text(a.Label, "vc-status__text");
                    text.pickingMode = PickingMode.Ignore;
                    b.Add(icon);
                    b.Add(text);
                    b.clicked += () => run(acts[buttons.IndexOf(b)]);
                }
                else
                {
                    b = UiKit.ActionPill(a.Label, null, a.Enabled, a.Kind == "segment" ? "tonal" : a.Kind);
                    if (actions.Count > 1) b.AddToClassList("vc-stepper__pill");
                    b.clicked += () => run(acts[buttons.IndexOf(b)]);
                }
                b.name = a.Target;
                buttons.Add(b);
                Root.Add(b);
            }
            Update(actions);
        }

        public IReadOnlyList<Button> Buttons => buttons;

        public void Update(List<RowAction> actions)
        {
            for (int i = 0; i < buttons.Count && i < actions.Count; i++)
            {
                acts[i] = actions[i];
                var b = buttons[i];
                if (actions[i].Kind == "status") { var l = b.Q<Label>(); if (l != null && l.text != actions[i].Label) UiKit.SetText(l, actions[i].Label); }
                else if (b.text != actions[i].Label) b.text = actions[i].Label;
                if (b.enabledSelf != actions[i].Enabled) b.SetEnabled(actions[i].Enabled);
                b.EnableInClassList("vc-segment--on", actions[i].Selected);
                b.name = actions[i].Target;
            }
        }
    }
}
