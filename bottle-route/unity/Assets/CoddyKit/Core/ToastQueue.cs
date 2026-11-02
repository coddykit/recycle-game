// CoddyKit (Unity) — toast timing (UiToast in the Godot kit; UI.md: <= 2 lines, 2.5-5.5 s by length, tap dismisses,
// the queue compresses). Pure: the runtime view (UiToastHost) asks Current / Tick.
using System;
using System.Collections.Generic;

namespace CoddyKit
{
    public enum ToastKind { Info, Success, Warning, Danger }

    public sealed class Toast
    {
        public string Text;
        public ToastKind Kind;
        public double Hold;
        public int Repeats = 1;
    }

    public sealed class ToastQueue
    {
        public const double MinHold = 2.5, MaxHold = 5.5, PerCharS = 0.045;
        public const int MaxQueued = 3;
        readonly List<Toast> queue = new List<Toast>();
        double shownFor;

        public Toast Current => queue.Count > 0 ? queue[0] : null;
        public int Count => queue.Count;

        public static double HoldFor(string text)
        {
            return Math.Min(MaxHold, Math.Max(MinHold, 1.6 + (text ?? "").Length * PerCharS));
        }

        /// <summary>Adds a toast. The same text as the one on screen or queued is merged (×N), the oldest queued (not the
        /// one on screen) is dropped when the queue is full.</summary>
        public void Push(string text, ToastKind kind = ToastKind.Info)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var t in queue)
            {
                if (t.Text == text && t.Kind == kind)
                {
                    t.Repeats++;
                    if (t == Current) shownFor = Math.Min(shownFor, 0.5);
                    return;
                }
            }
            if (queue.Count >= MaxQueued) queue.RemoveAt(1);
            queue.Add(new Toast { Text = text, Kind = kind, Hold = HoldFor(text) });
        }

        /// <summary>Advances time. Returns true when the visible toast changed (show the next / hide).</summary>
        public bool Tick(double dt)
        {
            if (queue.Count == 0) return false;
            shownFor += dt;
            if (shownFor < queue[0].Hold) return false;
            Dismiss();
            return true;
        }

        public void Dismiss()
        {
            if (queue.Count == 0) return;
            queue.RemoveAt(0);
            shownFor = 0;
        }
    }
}
