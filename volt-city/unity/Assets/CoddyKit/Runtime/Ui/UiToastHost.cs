// CoddyKit (Unity) — toast / snackbar card (icon-less on Low): <= 2 lines, 2.5-5.5 s by length, tap dismisses, the queue
// compresses (ToastQueue). Lives on its own overlay UIDocument (sortingOrder above the game UI), anchored to the top
// inside OverlayEdges. Kartsız toast yasak (games/CLAUDE.md §17): always a card. UNCOMPILED until the editor is installed.
using UnityEngine;
using UnityEngine.UIElements;

namespace CoddyKit.Ui
{
    public sealed class UiToastHost : MonoBehaviour
    {
        public static UiToastHost Instance { get; private set; }
        readonly ToastQueue queue = new ToastQueue();
        VisualElement layer;
        VisualElement card;
        Label label;
        Toast shown;

        public static void Show(string text, ToastKind kind = ToastKind.Info)
        {
            if (Instance != null) Instance.Push(text, kind);
            else Debug.Log("[CoddyKit] toast (no host): " + text);
        }

        public void Init(VisualElement overlayRoot)
        {
            Instance = this;
            layer = new VisualElement();
            layer.name = "ToastLayer";
            layer.AddToClassList("coddy-toast-layer");
            layer.pickingMode = PickingMode.Ignore;
            overlayRoot.Add(layer);
            SafeAreaPadding.Attach(layer, overlay: true);
            card = new VisualElement();
            card.AddToClassList("coddy-toast");
            card.RegisterCallback<PointerUpEvent>(_ => { queue.Dismiss(); Refresh(); });
            label = UiKit.Text("", "coddy-body");
            label.AddToClassList("coddy-toast__text");
            card.Add(label);
        }

        /// <summary>Drops the shown + queued toasts (a language switch: their text is in the old language).</summary>
        public static void ClearAll()
        {
            if (Instance == null) return;
            while (Instance.queue.Count > 0) Instance.queue.Dismiss();
            Instance.Refresh();
        }

        public void Push(string text, ToastKind kind)
        {
            queue.Push(text, kind);
            Refresh();
        }

        void Update()
        {
            if (queue.Count == 0) return;
            if (queue.Tick(Time.unscaledDeltaTime)) Refresh();
            else if (shown != null && shown.Repeats > 1) UiKit.SetText(label, shown.Text + "  ×" + shown.Repeats);
        }

        void Refresh()
        {
            var t = queue.Current;
            if (t == shown) return;
            shown = t;
            if (t == null)
            {
                card.RemoveFromHierarchy();
                return;
            }
            foreach (var k in new[] { "info", "success", "warning", "danger" }) card.RemoveFromClassList("coddy-toast--" + k);
            card.AddToClassList("coddy-toast--" + t.Kind.ToString().ToLowerInvariant());
            UiKit.SetText(label, t.Repeats > 1 ? t.Text + "  ×" + t.Repeats : t.Text);
            // the label's width in pixels from the layer's real width: a percentage resolved on the toast's first layout
            // pass measured a two-line text one line tall (3:4 tablet, layout gate 10-29)
            float lw = layer.resolvedStyle.width;
            if (!float.IsNaN(lw) && lw > 0f) label.style.width = lw * 0.92f - 60f;
            if (card.parent == null) layer.Add(card);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
