// CoddyKit (Unity) — one physical tap = one action (port of UiKit.is_tap + the Volt City world-pick rule, UI.md §4).
// Godot delivered every phone touch twice (ScreenTouch + emulated mouse) and handlers that took both acted twice
// (W66 Sky Tower). In Unity the same class of bug comes from reading Touchscreen AND Mouse (Editor touch simulation,
// ChromeOS, Samsung DeX, a legacy Input.GetMouseButton next to the Input System) or from registering both a
// MouseDownEvent and a PointerDownEvent in UI Toolkit. Rules:
//   * one press is tracked at a time (the first pointer wins; a second finger turns it into a pinch -> no tap);
//   * a tap fires on RELEASE only if the pointer moved < MaxMoveDp and the press lasted < MaxPressS; anything longer is a
//     pan / long press and never selects;
//   * a press from a DIFFERENT device within DedupeS and DedupeDp of the last accepted tap is the echo of the same
//     physical tap and is dropped.
// Pure logic; CoddyKit.Runtime.TapInput feeds it from the Input System.
using System;

namespace CoddyKit
{
    public enum PointerDevice { Touch = 0, Mouse = 1, Pen = 2 }

    public struct TapEvent
    {
        public float X, Y;       // screen px, top-left origin
        public double Time;      // monotonic seconds
        public PointerDevice Device;
    }

    public sealed class TapFilter
    {
        public float MaxMoveDp = 12f;
        public float MaxPressS = 0.4f;
        public float DedupeS = 0.35f;
        public float DedupeDp = 24f;
        /// <summary>Physical px per dp (Screen.dpi / 160; 1 when unknown).</summary>
        public float PxPerDp = 1f;

        bool down;
        bool cancelled;
        PointerDevice downDevice;
        float downX, downY;
        double downTime;
        bool haveLast;
        TapEvent last;

        public bool Pressed => down;

        public void Press(TapEvent e)
        {
            if (haveLast && e.Device != last.Device && e.Time - last.Time <= DedupeS && Dist(e.X, e.Y, last.X, last.Y) <= DedupeDp * PxPerDp)
                return; // echo of the tap we already accepted
            if (down)
            {
                if (e.Device == downDevice) cancelled = true; // second finger: pinch / two-finger pan, never a tap
                return;
            }
            down = true;
            cancelled = false;
            downDevice = e.Device;
            downX = e.X;
            downY = e.Y;
            downTime = e.Time;
        }

        public void Move(TapEvent e)
        {
            if (!down || e.Device != downDevice) return;
            if (Dist(e.X, e.Y, downX, downY) >= MaxMoveDp * PxPerDp) cancelled = true;
        }

        /// <summary>Second pointer of the same device went down (pinch).</summary>
        public void Cancel() { if (down) cancelled = true; }

        /// <summary>Returns true when this release is a tap (fire the action once, at (e.X, e.Y)).</summary>
        public bool Release(TapEvent e)
        {
            if (!down || e.Device != downDevice) return false;
            down = false;
            bool tap = !cancelled && e.Time - downTime < MaxPressS && Dist(e.X, e.Y, downX, downY) < MaxMoveDp * PxPerDp;
            if (tap)
            {
                haveLast = true;
                last = e;
            }
            return tap;
        }

        static float Dist(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx, dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
