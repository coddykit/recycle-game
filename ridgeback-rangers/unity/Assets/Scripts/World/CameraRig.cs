// Ridgeback Rangers — tilt-shift diorama camera: fitted to the unlocked districts (portrait), then clamped pan (one finger /
// mouse drag) and pinch / wheel zoom. Pan and pinch never select (the kit TapInput cancels a tap after 12 dp of travel or
// a second finger). A press that starts on a pickable UI element never pans (OverUi). The transform is written only when
// the view changed (no per-frame work while idle).
using System;
using System.Collections.Generic;
using CoddyKit;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace RidgebackRangers.World
{
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera cam;
        public Func<Vector2, bool> OverUi = _ => false;
        public Func<bool> Enabled = () => true;
        /// <summary>Share of the screen the HUD + bottom bar cover (the fit keeps the town in the free band).</summary>
        public float TopUi = 0.12f, BottomUi = 0.22f;

        public const float Pitch = 52f, Fov = 32f, MinZoom = 0.35f, MaxZoom = 1.3f, PanStartDp = 12f;
        public float Zoom => zoom;

        Vector3 center;
        float fitDist = 900f;
        Vector2 pan, panLimit = new Vector2(300, 300);
        float zoom = 1f;
        bool dirty = true, dragging, pressBlocked;
        Vector2 pressPos, lastPos;
        float pinchStart, zoomStart;

        void OnEnable() => EnhancedTouchSupport.Enable();
        void OnDisable() => EnhancedTouchSupport.Disable();

        public void Fit(List<Bounds> boxes, bool resetView)
        {
            if (cam == null || boxes == null || boxes.Count == 0) return;
            var b = boxes[0];
            foreach (var x in boxes) b.Encapsulate(x);
            cam.fieldOfView = Fov;
            center = b.center;
            float aspect = Mathf.Max(0.3f, cam.aspect);
            float freeShare = Mathf.Clamp(1f - TopUi - BottomUi, 0.4f, 1f);
            float vHalf = Fov * 0.5f * Mathf.Deg2Rad;
            float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * aspect);
            float byWidth = b.extents.x / Mathf.Tan(hHalf);
            float byDepth = b.extents.z * Mathf.Sin(Pitch * Mathf.Deg2Rad) / (Mathf.Tan(vHalf) * freeShare);
            fitDist = Mathf.Clamp(Mathf.Max(byWidth, byDepth) * 1.05f, 150f, 3000f);
            panLimit = new Vector2(b.extents.x + 40f, b.extents.z + 40f);
            if (resetView) { pan = Vector2.zero; zoom = resetView ? 0.75f : zoom; }
            dirty = true;
        }

        /// <summary>Moves the view so a world point is centred (the guide points the camera at its target).</summary>
        public void Focus(Vector3 world, float zoomTo = -1f)
        {
            pan = new Vector2(world.x - center.x, world.z - center.z);
            if (zoomTo > 0) zoom = zoomTo;
            dirty = true;
        }

        void Update()
        {
            HandleInput();
            if (!dirty || cam == null) return;
            dirty = false;
            pan.x = Mathf.Clamp(pan.x, -panLimit.x, panLimit.x);
            pan.y = Mathf.Clamp(pan.y, -panLimit.y, panLimit.y);
            zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            var rot = Quaternion.Euler(Pitch, 0, 0);
            float dist = fitDist * zoom;
            // shift the look point down a little so the free band (between HUD and bottom bar) is centred on the town
            float shift = (BottomUi - TopUi) * 0.5f * dist * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) * 2f;
            var target = center + new Vector3(pan.x, 0, pan.y - shift);
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * dist, rot);
            cam.nearClipPlane = Mathf.Max(1f, dist * 0.05f);
            cam.farClipPlane = dist * 3f + 500f;
        }

        float UnitsPerPixel() => 2f * fitDist * zoom * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);

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
                    else if (d > 0f) { zoom = zoomStart * pinchStart / d; dirty = true; }
                    dragging = false;
                    pressBlocked = true;
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
            float k = UnitsPerPixel();
            pan -= new Vector2(delta.x * k, delta.y * k / Mathf.Sin(Pitch * Mathf.Deg2Rad));
            dirty = true;
        }
    }
}
