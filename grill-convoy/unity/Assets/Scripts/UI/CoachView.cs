// Grill Convoy — the guide's view (fleet §9 / §17): the intro pager (art + step dots + Skip), then the coach bubble with
// the head cook's portrait (Meshy hero, pre-rendered), a pulsing spotlight ring on the target (the HUD's main / cook / all
// hands / extinguish buttons, or a pill / layout tile inside a sheet), and "Skip". The sheet a step needs is opened for the
// player with the step's section on top (crew for the hire, the layout grid, the pack list, the commissary), so a
// first-time player never has to find it.
using System;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using GrillConvoy.Game;

namespace GrillConvoy.UI
{
    public sealed partial class GameScreen
    {
        VisualElement coachBubble, spot, pager;
        Label coachText, coachStep;
        Button coachSkip;
        int shownStep = -1, shownFlash = -1;
        float pulse;
        int pagerPage;
        public bool PagerOpen => pager != null && pager.parent != null;

        void BuildCoach()
        {
            spot = new VisualElement { name = "Spot", pickingMode = PickingMode.Ignore };
            spot.AddToClassList("gc-spot");
            spot.style.display = DisplayStyle.None;
            root.Add(spot);
            coachBubble = new VisualElement { name = "Coach" };
            coachBubble.AddToClassList("gc-coach");
            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.AddToClassList("gc-coach__face");
            var portrait = Resources.Load<Texture2D>("ui/coach_chef");
            if (portrait != null) face.style.backgroundImage = new StyleBackground(portrait);
            coachBubble.Add(face);
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.AddToClassList("gc-coach__texts");
            coachText = UiKit.Text("", "gc-coach__text");
            coachStep = UiKit.Text("", "gc-coach__step");
            texts.Add(coachText);
            texts.Add(coachStep);
            coachBubble.Add(texts);
            coachSkip = UiKit.ActionPill("", () => main.SkipGuide(), true, "tonal");
            coachSkip.AddToClassList("gc-coach__skip");
            coachBubble.Add(coachSkip);
            coachBubble.style.display = DisplayStyle.None;
            root.Add(coachBubble);
        }

        void CoachTick(float dt)
        {
            var c = main.Coach;
            bool on = c.Active && !PagerOpen;
            coachBubble.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) { spot.style.display = DisplayStyle.None; shownStep = -1; TruckFocus = ""; return; }
            if (c.Step != shownStep)
            {
                shownStep = c.Step;
                OnCoachStep(c.Step);
            }
            if (c.Flash != shownFlash) { shownFlash = c.Flash; pulse = 1.2f; }
            UiKit.SetText(coachText, L.T(c.BubbleKey));
            UiKit.SetText(coachStep, L.F("coach.step", "n", c.Step, "m", Coach.Steps));
            UiKit.SetText(coachSkip, L.T("coach.skip"));
            // spotlight
            Rect? r = c.FastForward(Sim) ? null : UiTarget(c.Target);
            if (r != null)
            {
                spot.style.display = DisplayStyle.Flex;
                var rc = r.Value;
                float size = Mathf.Max(96f, Mathf.Max(rc.width, rc.height) + 24f);
                spot.style.width = spot.style.height = size;
                spot.style.marginLeft = spot.style.marginTop = -size * 0.5f;
                spot.style.borderTopLeftRadius = spot.style.borderTopRightRadius = spot.style.borderBottomLeftRadius = spot.style.borderBottomRightRadius = size * 0.5f;
                spot.style.translate = new Translate(rc.center.x, rc.center.y);
                // the bubble moves to the top when the target is in the lower half
                coachBubble.EnableInClassList("gc-coach--top", rc.center.y > root.layout.height * 0.55f);
            }
            else spot.style.display = DisplayStyle.None;
            pulse = Mathf.Max(0, pulse - dt);
            float s = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f) + pulse * 0.25f;
            spot.style.scale = new Scale(new Vector3(s, s, 1));
        }

        void OnCoachStep(int step)
        {
            TruckFocus = "";
            switch (step)
            {
                case 8: TruckFocus = "crew"; OpenSheet("truck", 0); break;
                case 9: TruckFocus = "layout"; OpenSheet("truck", 0); break;
                case 10: OpenSheet("kitchen"); break;
                case 11: TruckFocus = "pack"; OpenSheet("truck", 0); break;
                case 7: if (SheetKind != "report" && Sim.Phase == GrillConvoy.Sim.Phases.Report) OpenSheet("report"); break;
                default: if (sheet != null && sheet.IsOpen) CloseSheet(); break;
            }
            if (World != null && main.world.rig != null && Sim.S.Trucks.Count > 0 && (step == 1 || step == 13))
                main.world.rig.Focus(World.TruckWorld(0), 0.5f);
        }

        // ------------------------------------------------------------------ intro pager

        public void ShowPager(Action done)
        {
            pagerPage = 0;
            pager = new VisualElement { name = "Pager" };
            pager.AddToClassList("gc-pager");
            root.Add(pager);
            void Render()
            {
                pager.Clear();
                int n = pagerPage + 1;
                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("gc-pager__art");
                var tex = Resources.Load<Texture2D>("ui/intro_" + n);
                if (tex != null) art.style.backgroundImage = new StyleBackground(tex);
                pager.Add(art);
                pager.Add(UiKit.Text(L.T("intro." + n + ".t"), "gc-pager__title"));
                pager.Add(UiKit.Text(L.T("intro." + n + ".b"), "gc-pager__body"));
                var dots = new VisualElement { pickingMode = PickingMode.Ignore };
                dots.AddToClassList("gc-pager__dots");
                for (int i = 0; i < 3; i++)
                {
                    var d = new VisualElement();
                    d.AddToClassList("gc-pager__dot");
                    d.EnableInClassList("gc-pager__dot--on", i == pagerPage);
                    dots.Add(d);
                }
                pager.Add(dots);
                var actions = new VisualElement();
                actions.AddToClassList("gc-pager__actions");
                actions.Add(UiKit.ActionPill(L.T("intro.skip"), () => { pager.RemoveFromHierarchy(); done(); }, true, "tonal"));
                bool last = pagerPage >= 2;
                var next = UiKit.PrimaryButton(L.T(last ? "intro.start" : "intro.next"), () =>
                {
                    if (last) { pager.RemoveFromHierarchy(); done(); }
                    else { pagerPage++; Render(); }
                });
                next.name = "PagerNext";
                actions.Add(next);
                pager.Add(actions);
            }
            Render();
        }
    }
}
