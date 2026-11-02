// CoddyKit (Unity) — safe-area maths, the port of games/tools/ui/addons/coddy_ui/ui_safe_area.gd (owner 10-26 / 10-27).
// All rects here are TOP-LEFT origin in physical screen px (Unity's Screen.safeArea is bottom-left: the runtime converts).
//   Combine(safe, screen, nav)  the engine safe area pushed in so it also clears the Android navigation bar, whose insets
//                               are read IGNORING visibility (immersive mode hides the bar, an edge swipe brings it back
//                               over the game, Samsung Game Booster keeps it up) — left / right / bottom, never the top.
//   ToCanvas(...)               gaps in canvas px for a canvas covering the whole screen (UI Toolkit panel px).
//   OverlayEdges(...)           edges + the status bar height at the top even while it is hidden: edge-anchored overlays
//                               (onboarding, coach, toast, Skip, notices) keep their buttons and text inside these gaps.
//   ContentBottom(safe, banner) the content's bottom inset = max(safe-area bottom, house banner height): the banner already
//                               sits ABOVE the bottom safe area and its height counts that gap (CoddyBanner contract).
using System;
using System.Text.RegularExpressions;

namespace CoddyKit
{
    public struct Insets : IEquatable<Insets>
    {
        public float Left, Top, Right, Bottom;

        public Insets(float left, float top, float right, float bottom)
        {
            Left = left; Top = top; Right = right; Bottom = bottom;
        }

        public static readonly Insets Zero = new Insets(0, 0, 0, 0);
        public bool IsZero => Left <= 0 && Top <= 0 && Right <= 0 && Bottom <= 0;
        public bool Equals(Insets o) => Left == o.Left && Top == o.Top && Right == o.Right && Bottom == o.Bottom;
        public override bool Equals(object obj) => obj is Insets o && Equals(o);
        public override int GetHashCode() => (Left, Top, Right, Bottom).GetHashCode();
        public override string ToString() => $"(l {Left}, t {Top}, r {Right}, b {Bottom})";
    }

    /// <summary>Integer rect, top-left origin.</summary>
    public struct IRect
    {
        public int X, Y, W, H;
        public IRect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        public int Right => X + W;
        public int Bottom => Y + H;
        public bool Empty => W <= 0 || H <= 0;
        public override string ToString() => $"[{X},{Y} {W}x{H}]";
    }

    public static class SafeAreaMath
    {
        /// <summary>Unity Screen.safeArea (bottom-left origin) -> top-left rect.</summary>
        public static IRect FromUnity(float x, float yBottom, float w, float h, int screenH)
        {
            int top = (int)Math.Round(screenH - (yBottom + h));
            return new IRect((int)Math.Round(x), Math.Max(0, top), (int)Math.Round(w), (int)Math.Round(h));
        }

        public static IRect Combine(IRect safe, int screenW, int screenH, Insets nav)
        {
            if (nav.IsZero || screenW <= 0 || screenH <= 0) return safe;
            if (safe.Empty) safe = new IRect(0, 0, screenW, screenH);
            int l = Math.Max(safe.X, (int)Math.Ceiling(nav.Left));
            int t = Math.Max(safe.Y, (int)Math.Ceiling(nav.Top));
            int r = Math.Min(safe.Right, screenW - (int)Math.Ceiling(nav.Right));
            int b = Math.Min(safe.Bottom, screenH - (int)Math.Ceiling(nav.Bottom));
            if (r <= l || b <= t) return safe; // nonsense insets: keep the engine value
            return new IRect(l, t, r - l, b - t);
        }

        public static Insets ToCanvas(IRect safe, int screenW, int screenH, float canvasW, float canvasH)
        {
            if (safe.Empty || screenW <= 0 || screenH <= 0) return Insets.Zero;
            float kx = canvasW / screenW, ky = canvasH / screenH;
            return new Insets(
                Math.Max(0f, safe.X * kx),
                Math.Max(0f, safe.Y * ky),
                Math.Max(0f, (screenW - safe.Right) * kx),
                Math.Max(0f, (screenH - safe.Bottom) * ky));
        }

        /// <summary>Edges for edge-anchored overlays: ToCanvas + the status bar (physical px) reserved at the top.</summary>
        public static Insets OverlayEdges(IRect safe, int screenW, int screenH, float canvasW, float canvasH, float statusTopPx)
        {
            Insets e = ToCanvas(safe, screenW, screenH, canvasW, canvasH);
            if (screenH > 0) e.Top = Math.Max(e.Top, statusTopPx * canvasH / screenH);
            return e;
        }

        /// <summary>What a control at rect r (canvas px, top-left) must still keep free inside itself.</summary>
        public static Insets InnerGaps(Insets e, float rx, float ry, float rw, float rh, float canvasW, float canvasH)
        {
            return new Insets(Math.Max(0f, e.Left - rx), Math.Max(0f, e.Top - ry),
                Math.Max(0f, e.Right - (canvasW - (rx + rw))), Math.Max(0f, e.Bottom - (canvasH - (ry + rh))));
        }

        public static float ContentBottom(float safeBottom, float bannerHeight) => Math.Max(safeBottom, bannerHeight);

        static readonly Regex InsetRe = new Regex(@"(left|top|right|bottom)=(-?\d+)", RegexOptions.CultureInvariant);

        /// <summary>"Insets{left=0, top=63, right=0, bottom=126}" (android.graphics.Insets.toString) -> Insets.</summary>
        public static Insets ParseAndroidInsets(string s)
        {
            var v = Insets.Zero;
            if (string.IsNullOrEmpty(s)) return v;
            foreach (Match m in InsetRe.Matches(s))
            {
                float n = Math.Max(0f, float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
                switch (m.Groups[1].Value)
                {
                    case "left": v.Left = n; break;
                    case "top": v.Top = n; break;
                    case "right": v.Right = n; break;
                    case "bottom": v.Bottom = n; break;
                }
            }
            return v;
        }

        /// <summary>"1080x2400:0,110,0,126" (CODDY_SAFE_SIM) -> screen + insets; false when malformed.</summary>
        public static bool ParseSimulation(string env, out int w, out int h, out Insets insets)
        {
            w = h = 0;
            insets = Insets.Zero;
            if (string.IsNullOrEmpty(env)) return false;
            string[] halves = env.Split(':');
            if (halves.Length != 2) return false;
            string[] wh = halves[0].Split('x');
            string[] ins = halves[1].Split(',');
            if (wh.Length != 2 || ins.Length != 4) return false;
            if (!int.TryParse(wh[0], out w) || !int.TryParse(wh[1], out h)) return false;
            var f = new float[4];
            for (int i = 0; i < 4; i++)
                if (!float.TryParse(ins[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f[i])) return false;
            insets = new Insets(f[0], f[1], f[2], f[3]);
            return w > 0 && h > 0;
        }
    }
}
