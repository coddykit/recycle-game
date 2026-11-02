// Volt City portrait diorama camera (ART section 3). Pure math, no per-frame cost: call Fit when the set of lit
// districts changes (or the safe area / bottom sheet moves). Same algorithm as blender/render_frames.py fit_camera.
// ~55 deg tilt, perspective FOV 35 deg (vertical), fitted to the LIT districts' boxes (layout.json box_min / box_max),
// not the scene bounds, inside a normalised viewport rect (the diorama area between the HUD and the bottom sheet).
// Uncompiled until the Unity project exists (P3).
using System.Collections.Generic;
using UnityEngine;

namespace VoltCity.Art
{
    public static class VcCameraFit
    {
        public const float Tilt = 55f;
        public const float Fov = 35f;
        public const float Near = 20f;
        public const float Far = 9000f;
        public const float MinDist = 260f;     // readability floor (crews stay chunky)
        public const float MaxDist = 2600f;    // zoom-out floor = the Low budget view (whole city + margin)

        /// rect: 0..1 viewport rect, y up (Unity convention). margin: fraction of the rect's short side.
        public static void Apply(Camera cam, IReadOnlyList<Bounds> boxes, Rect rect, float margin = 0.04f, float yaw = 0f)
        {
            cam.orthographic = false;
            cam.fieldOfView = Fov;
            cam.nearClipPlane = Near;
            cam.farClipPlane = Far;
            var rot = Quaternion.Euler(Tilt, yaw, 0f);
            cam.transform.SetPositionAndRotation(Fit(boxes, cam.aspect, rect, margin, rot), rot);
        }

        public static Vector3 Fit(IReadOnlyList<Bounds> boxes, float aspect, Rect rect, float margin, Quaternion rot)
        {
            var pts = new List<Vector3>();
            foreach (var b in boxes)
                for (int i = 0; i < 8; i++)
                    pts.Add(new Vector3((i & 1) != 0 ? b.max.x : b.min.x, (i & 2) != 0 ? b.max.y : b.min.y,
                        (i & 4) != 0 ? b.max.z : b.min.z));
            Vector3 fwd = rot * Vector3.forward, right = rot * Vector3.right, up = rot * Vector3.up;
            float ty = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad), tx = ty * aspect;
            float m = margin * Mathf.Min(rect.width, rect.height);
            float nx0 = 2f * (rect.xMin + m) - 1f, nx1 = 2f * (rect.xMax - m) - 1f;
            float ny0 = 2f * (rect.yMin + m) - 1f, ny1 = 2f * (rect.yMax - m) - 1f;
            var target = Vector3.zero;
            foreach (var p in pts)
                target += p;
            target /= Mathf.Max(1, pts.Count);
            float dist = 800f;
            for (int iter = 0; iter < 6; iter++)
            {
                float lo = 50f, hi = 20000f;
                for (int k = 0; k < 40; k++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Fits(pts, target - fwd * mid, fwd, right, up, tx, ty, nx0, nx1, ny0, ny1)) hi = mid; else lo = mid;
                }
                // centre at the distance the camera will really use: centring for an unclamped 10 km fit and clamping
                // afterwards aimed a small band's camera off the city (10-28 capture: the band showed only sky)
                dist = Mathf.Clamp(hi, MinDist, MaxDist);
                var eye = target - fwd * dist;
                float bx0 = float.MaxValue, bx1 = float.MinValue, by0 = float.MaxValue, by1 = float.MinValue, depth = 0f;
                foreach (var p in pts)
                {
                    var q = p - eye;
                    float z = Mathf.Max(Vector3.Dot(q, fwd), 1f);
                    float x = Vector3.Dot(q, right) / (z * tx), y = Vector3.Dot(q, up) / (z * ty);
                    bx0 = Mathf.Min(bx0, x); bx1 = Mathf.Max(bx1, x); by0 = Mathf.Min(by0, y); by1 = Mathf.Max(by1, y);
                    depth += z;
                }
                depth /= Mathf.Max(1, pts.Count);
                float cx = (bx0 + bx1) * 0.5f - (nx0 + nx1) * 0.5f, cy = (by0 + by1) * 0.5f - (ny0 + ny1) * 0.5f;
                target += right * (cx * depth * tx) + up * (cy * depth * ty);
            }
            return target - fwd * Mathf.Clamp(dist, MinDist, MaxDist);
        }

        static bool Fits(List<Vector3> pts, Vector3 eye, Vector3 fwd, Vector3 right, Vector3 up, float tx, float ty,
            float nx0, float nx1, float ny0, float ny1)
        {
            foreach (var p in pts)
            {
                var q = p - eye;
                float z = Vector3.Dot(q, fwd);
                if (z <= Near) return false;
                float x = Vector3.Dot(q, right) / (z * tx), y = Vector3.Dot(q, up) / (z * ty);
                if (x < nx0 || x > nx1 || y < ny0 || y > ny1) return false;
            }
            return true;
        }
    }
}
