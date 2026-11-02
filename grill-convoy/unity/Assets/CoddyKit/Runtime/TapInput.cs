// CoddyKit (Unity) — world taps (district / plant / fault picking) from the Input System, ONE action per physical tap
// (TapFilter: release-only, < 12 dp / < 400 ms, second finger = pinch, cross-device echo dropped). Reads touches through
// EnhancedTouch on a touchscreen and the mouse only when there is no touchscreen (editor / desktop) — never both, so the
// Godot "ScreenTouch + emulated mouse" double action cannot happen here. A press that starts on a pickable UI Toolkit
// element never becomes a world tap (IsOverUi). UNCOMPILED until the editor is installed.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UIElements;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace CoddyKit
{
    public sealed class TapInput : MonoBehaviour
    {
        /// <summary>Fired once per accepted tap; screen px, TOP-LEFT origin.</summary>
        public event Action<Vector2> Tapped;
        /// <summary>UI panels whose pickable elements block world taps (UIDocument.rootVisualElement.panel).</summary>
        public readonly List<IPanel> BlockingPanels = new List<IPanel>();
        /// <summary>Extra game gate (tutorial spotlight, modal open…): false = ignore world taps.</summary>
        public Func<bool> Enabled = () => true;

        readonly TapFilter filter = new TapFilter();
        int trackedTouchId = -1;
        bool pressBlocked;

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            filter.PxPerDp = SafeArea.PxPerDp();
        }

        void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        void Update()
        {
            double t = Time.realtimeSinceStartupAsDouble;
            if (Touchscreen.current != null)
            {
                var touches = ETouch.activeTouches;
                if (touches.Count > 1) filter.Cancel();
                for (int i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    Vector2 p = TopLeft(touch.screenPosition);
                    var ev = new TapEvent { X = p.x, Y = p.y, Time = t, Device = PointerDevice.Touch };
                    switch (touch.phase)
                    {
                        case UnityEngine.InputSystem.TouchPhase.Began:
                            if (trackedTouchId < 0)
                            {
                                trackedTouchId = touch.touchId;
                                pressBlocked = IsOverUi(p) || !Enabled();
                                filter.Press(ev);
                            }
                            break;
                        case UnityEngine.InputSystem.TouchPhase.Moved:
                            if (touch.touchId == trackedTouchId) filter.Move(ev);
                            break;
                        case UnityEngine.InputSystem.TouchPhase.Ended:
                        case UnityEngine.InputSystem.TouchPhase.Canceled:
                            if (touch.touchId != trackedTouchId) break;
                            trackedTouchId = -1;
                            bool tap = touch.phase == UnityEngine.InputSystem.TouchPhase.Ended && filter.Release(ev);
                            if (!tap && touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled) filter.Release(new TapEvent { X = -1e6f, Y = -1e6f, Time = t, Device = PointerDevice.Touch });
                            if (tap && !pressBlocked && Enabled()) Tapped?.Invoke(p);
                            break;
                    }
                }
                return; // a touchscreen exists: the mouse is never read (no double handling)
            }
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 mp = TopLeft(mouse.position.ReadValue());
            var mev = new TapEvent { X = mp.x, Y = mp.y, Time = t, Device = PointerDevice.Mouse };
            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressBlocked = IsOverUi(mp) || !Enabled();
                filter.Press(mev);
            }
            else if (mouse.leftButton.isPressed) filter.Move(mev);
            if (mouse.leftButton.wasReleasedThisFrame && filter.Release(mev) && !pressBlocked && Enabled()) Tapped?.Invoke(mp);
        }

        static Vector2 TopLeft(Vector2 bottomLeft) => new Vector2(bottomLeft.x, Screen.height - bottomLeft.y);

        bool IsOverUi(Vector2 topLeft)
        {
            foreach (var panel in BlockingPanels)
            {
                if (panel == null) continue;
                Vector2 pp = RuntimePanelUtils.ScreenToPanel(panel, topLeft);
                var picked = panel.Pick(pp);
                if (picked != null && picked.pickingMode == PickingMode.Position && picked != panel.visualTree) return true;
            }
            return false;
        }
    }
}
