// Volt City — diorama camera: fitted to the HOOKED districts' boxes inside the free band between the HUD and the bottom
// sheet (VcCameraFit, P2), then clamped pan (one finger / mouse drag) and pinch / wheel zoom. Pan and pinch never select:
// the kit TapInput cancels a tap after 12 dp of travel or a second finger. A press that starts on a pickable UI element
// never pans (OverUi). Transform written only when the view changed (no per-frame work while idle).
// UNCOMPILED until the Unity editor is installed.
using System;
using System.Collections.Generic;
using CoddyKit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using VoltCity.Art;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace VoltCity.World
{
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera cam;
        /// <summary>Free band for the diorama, normalised viewport (y up): the UI sets it from the HUD / sheet geometry.</summary>
        public Rect viewport = new Rect(0f, 0.40f, 1f, 0.46f);
        /// <summary>A screen point (top-left px) that lies on a pickable UI element (pan must not start there).</summary>
        public Func<Vector2, bool> OverUi = _ => false;
        /// <summary>false while the guide spotlight / a modal sheet is up (the view stays where the guide put it).</summary>
        public Func<bool> Enabled = () => true;

        public const float MinZoom = 0.45f, MaxZoom = 1.15f;
        public const float PanStartDp = 12f;

        Vector3 fitPos;
        Quaternion fitRot;
        float fitDist = 800f;
        Vector2 pan;          // metres on the ground (x east, z north)
        float zoom = 1f;      // 1 = fitted distance
        Vector2 panLimit = new Vector2(300f, 300f);
        List<Bounds> lastBoxes;
        Rect lastViewport;
        bool dirty;

        // gesture state
        bool dragging, pressBlocked;
        Vector2 pressPos, lastPos;
        float pinchStart, zoomStart;

        void OnEnable() => EnhancedTouchSupport.Enable();
        void OnDisable() => EnhancedTouchSupport.Disable();

        /// <summary>Re-fits when the hooked set or the free band changed (keeps the player's pan / zoom otherwise).</summary>
        public void Refit(List<Bounds> boxes, bool resetView = false)
        {
            if (cam == null || boxes == null || boxes.Count == 0) return;
            lastBoxes = boxes;
            lastViewport = viewport;
            VcCameraFit.Apply(cam, boxes, viewport);
            fitPos = cam.transform.position;
            fitRot = cam.transform.rotation;
            var b = boxes[0];
            foreach (var x in boxes) b.Encapsulate(x);
            fitDist = Vector3.Distance(fitPos, b.center);
            panLimit = new Vector2(b.extents.x + 60f, b.extents.z + 60f);
            if (resetView) { pan = Vector2.zero; zoom = 1f; }
            dirty = true;
        }

        public void SetViewport(Rect r)
        {
            if (Mathf.Abs(r.y - viewport.y) < 0.005f && Mathf.Abs(r.height - viewport.height) < 0.005f) return;
            viewport = r;
            if (lastBoxes != null) Refit(lastBoxes);
        }

        void Update()
        {
            HandleInput();
            if (!dirty || cam == null) return;
            dirty = false;
            pan.x = Mathf.Clamp(pan.x, -panLimit.x, panLimit.x);
            pan.y = Mathf.Clamp(pan.y, -panLimit.y, panLimit.y);
            zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            var fwd = fitRot * Vector3.forward;
            float dist = Mathf.Clamp(fitDist * zoom, VcCameraFit.MinDist, VcCameraFit.MaxDist);
            var target = fitPos + fwd * fitDist;
            var pos = target - fwd * dist + new Vector3(pan.x, 0f, pan.y);
            cam.transform.SetPositionAndRotation(pos, fitRot);
            VcLightCurves.SetViewDistance(dist);
        }

        float MetresPerPixel()
        {
            float dist = fitDist * zoom;
            return 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
        }

        void HandleInput()
        {
            if (cam == null) return;
            if (!Enabled()) { dragging = false; return; }
            float px = SafeArea.PxPerDp();
            if (Touchscreen.current != null)
            {
                var t = ETouch.activeTouches;
                if (t.Count >= 2)
                {
                    float d = Vector2.Distance(t[0].screenPosition, t[1].screenPosition);
                    if (t[1].phase == UnityEngine.InputSystem.TouchPhase.Began || pinchStart <= 0f) { pinchStart = d; zoomStart = zoom; }
                    else if (pinchStart > 0f && d > 0f) { zoom = zoomStart * pinchStart / d; dirty = true; }
                    dragging = false;
                    pressBlocked = true;   // after a pinch the remaining finger never pans
                    return;
                }
                pinchStart = 0f;
                if (t.Count == 1)
                {
                    var f = t[0];
                    Vector2 p = f.screenPosition;
                    if (f.phase == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        pressPos = lastPos = p;
                        dragging = false;
                        pressBlocked = OverUi(new Vector2(p.x, Screen.height - p.y));
                    }
                    else if (!pressBlocked) Drag(p, px);
                    if (f.phase == UnityEngine.InputSystem.TouchPhase.Ended || f.phase == UnityEngine.InputSystem.TouchPhase.Canceled) dragging = false;
                }
                else { dragging = false; pressBlocked = false; }
                return;
            }
            var m = Mouse.current;
            if (m == null) return;
            Vector2 mp = m.position.ReadValue();
            float wheel = m.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f && !OverUi(new Vector2(mp.x, Screen.height - mp.y))) { zoom *= wheel > 0 ? 0.9f : 1.1f; dirty = true; }
            if (m.leftButton.wasPressedThisFrame)
            {
                pressPos = lastPos = mp;
                dragging = false;
                pressBlocked = OverUi(new Vector2(mp.x, Screen.height - mp.y));
            }
            else if (m.leftButton.isPressed && !pressBlocked) Drag(mp, px);
            if (m.leftButton.wasReleasedThisFrame) dragging = false;
        }

        void Drag(Vector2 p, float pxPerDp)
        {
            if (!dragging && Vector2.Distance(p, pressPos) >= PanStartDp * pxPerDp) dragging = true;
            if (!dragging) { lastPos = p; return; }
            Vector2 delta = p - lastPos;
            lastPos = p;
            float k = MetresPerPixel();
            var right = fitRot * Vector3.right;
            var fwd = fitRot * Vector3.forward;
            var ground = new Vector2(fwd.x, fwd.z).normalized;
            pan -= new Vector2(right.x, right.z) * (delta.x * k) + ground * (delta.y * k * 1.4f);
            dirty = true;
        }
    }
}
