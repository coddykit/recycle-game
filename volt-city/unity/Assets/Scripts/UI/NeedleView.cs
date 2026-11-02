// Volt City UI — the Balance needle (UI.md section 6): a dial drawn ONCE (generateVisualContent, repainted only on a
// size change) + a needle element rotated through style.rotate only when the quantised angle changes + icon + text
// ("Short 4 MW" — colour is never the only signal). Tap (release) opens the one-line explainer (guide step 2).
// UNCOMPILED until the Unity editor is installed.
using System;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using VoltCity.Game;

namespace VoltCity.UI
{
    public sealed class NeedleView : VisualElement
    {
        readonly VisualElement dial, needle, hub;
        readonly Image icon;
        readonly Label label;
        float angle = float.NaN;
        NeedleBand band = (NeedleBand)(-1);
        string iconName = "";

        static readonly Color Green = new Color(0.373f, 0.827f, 0.553f), Amber = new Color(1f, 0.761f, 0.302f), Red = new Color(1f, 0.42f, 0.369f);

        public NeedleView(Action onTap)
        {
            name = "hud.needle";
            AddToClassList("vc-needle");
            dial = new VisualElement { pickingMode = PickingMode.Ignore };
            dial.AddToClassList("vc-needle__dial");
            dial.generateVisualContent += DrawDial;
            Add(dial);
            needle = new VisualElement { pickingMode = PickingMode.Ignore };
            needle.AddToClassList("vc-needle__hand");
            dial.Add(needle);
            hub = new VisualElement { pickingMode = PickingMode.Ignore };
            hub.AddToClassList("vc-needle__hub");
            dial.Add(hub);
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.AddToClassList("vc-needle__texts");
            icon = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList("vc-needle__icon");
            texts.Add(icon);
            label = UiKit.Text("", "vc-needle__label");
            texts.Add(label);
            Add(texts);
            TapGesture.Attach(this, onTap);
        }

        static void DrawDial(MeshGenerationContext mgc)
        {
            var r = mgc.visualElement.contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            var p = mgc.painter2D;
            var c = new Vector2(r.width * 0.5f, r.height - 6f);
            float rad = Mathf.Min(r.width * 0.5f, r.height) - 10f;
            p.lineWidth = 12f;
            p.lineCap = LineCap.Butt;
            // -60..+60 deg around straight up: red | amber | green | amber | red (±3 % / ±5 % of a ±10 % scale)
            void Band(float fromDeg, float toDeg, Color col)
            {
                p.strokeColor = col;
                p.BeginPath();
                p.Arc(c, rad, Angle.Degrees(-90f + fromDeg), Angle.Degrees(-90f + toDeg));
                p.Stroke();
            }
            Band(-60f, -30f, Red);
            Band(-30f, -18f, Amber);
            Band(-18f, 18f, Green);
            Band(18f, 30f, Amber);
            Band(30f, 60f, Red);
        }

        public void Set(HudView v)
        {
            if (!Mathf.Approximately(angle, v.NeedleDeg))
            {
                angle = v.NeedleDeg;
                needle.style.rotate = new Rotate(Angle.Degrees(angle));
            }
            if (band != v.Band)
            {
                band = v.Band;
                RemoveFromClassList("vc-needle--green");
                RemoveFromClassList("vc-needle--amber");
                RemoveFromClassList("vc-needle--red");
                AddToClassList(band == NeedleBand.Green ? "vc-needle--green" : band == NeedleBand.Amber ? "vc-needle--amber" : "vc-needle--red");
            }
            if (iconName != v.NeedleIcon)
            {
                iconName = v.NeedleIcon;
                icon.image = Icons.Get(iconName);
            }
            UiKit.SetText(label, v.NeedleLabel);
        }
    }
}
