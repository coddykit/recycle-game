// Charge Nurse — the guide's view (fleet §9 / §17): the intro pager (art + step dots + Skip), then the coach bubble with
// the charge nurse's portrait (Meshy hero, pre-rendered), a pulsing spotlight ring on the target (a waiting patient, a
// bay badge, a HUD button or a pill inside a sheet), and "Skip". The sheet a step needs is opened for the player (the
// Build sheet for the X-ray room, Staff for the porter), so a first-time player never has to find it.
using System;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using ChargeNurse.Game;

namespace ChargeNurse.UI
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
            spot.AddToClassList("cn-spot");
            spot.style.display = DisplayStyle.None;
            root.Add(spot);
            coachBubble = new VisualElement { name = "Coach" };
            coachBubble.AddToClassList("cn-coach");
            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.AddToClassList("cn-coach__face");
            var portrait = Resources.Load<Texture2D>("ui/coach_nurse");
            if (portrait != null) face.style.backgroundImage = new StyleBackground(portrait);
            coachBubble.Add(face);
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.AddToClassList("cn-coach__texts");
            coachText = UiKit.Text("", "cn-coach__text");
            coachStep = UiKit.Text("", "cn-coach__step");
            texts.Add(coachText);
            texts.Add(coachStep);
            coachBubble.Add(texts);
            coachSkip = UiKit.ActionPill("", () => main.SkipGuide(), true, "tonal");
            coachSkip.AddToClassList("cn-coach__skip");
            coachBubble.Add(coachSkip);
            coachBubble.style.display = DisplayStyle.None;
            root.Add(coachBubble);
        }

        void CoachTick(float dt)
        {
            var c = main.Coach;
            bool on = c.Active && !PagerOpen;
            coachBubble.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) { spot.style.display = DisplayStyle.None; shownStep = -1; return; }
            if (c.Step != shownStep)
            {
                shownStep = c.Step;
                OnCoachStep(c.Step);
            }
            if (c.Flash != shownFlash) { shownFlash = c.Flash; pulse = 1.2f; }
            UiKit.SetText(coachText, L.T(c.BubbleKey));
            UiKit.SetText(coachStep, L.F("coach.step", "n", c.Step, "m", Coach.Steps));
            UiKit.SetText(coachSkip, L.T("coach.skip"));
            // spotlight (none while the guide waits for the world)
            Rect? r = c.FastForward(main.Session.Sim) ? null : UiTarget(c.Target);
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
                coachBubble.EnableInClassList("cn-coach--top", rc.center.y > root.layout.height * 0.55f);
            }
            else spot.style.display = DisplayStyle.None;
            pulse = Mathf.Max(0, pulse - dt);
            float s = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f) + pulse * 0.25f;
            spot.style.scale = new Scale(new Vector3(s, s, 1));
        }

        void OnCoachStep(int step)
        {
            switch (step)
            {
                case 2: OpenSheet("patient", Coach.FirstPatient); break;
                case 7: OpenSheet("build"); break;
                case 8: OpenSheet("staff"); break;
                default: if (sheet != null && sheet.IsOpen) CloseSheet(); break;
            }
            if (World != null && main.world.rig != null)
            {
                var gd = main.Session.Sim.Gd;
                if (step == 1) main.world.rig.Focus(World.ZoneCenter(gd.Zone("waiting")), 0.6f);
                if (step == 3 || step == 4 || step == 5) main.world.rig.Focus(World.ZoneCenter(gd.Zone("bays_c")), 0.6f);
                if (step == 6) main.world.rig.Focus(World.AmbulanceBay, 0.7f);
                if (step == 9) main.world.rig.Focus(World.ZoneCenter(gd.Zone("obs")), 0.7f);
            }
        }

        // ------------------------------------------------------------------ intro pager

        public void ShowPager(Action done)
        {
            pagerPage = 0;
            pager = new VisualElement { name = "Pager" };
            pager.AddToClassList("cn-pager");
            root.Add(pager);
            void Render()
            {
                pager.Clear();
                int n = pagerPage + 1;
                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("cn-pager__art");
                var tex = Resources.Load<Texture2D>("ui/intro_" + n);
                if (tex != null) art.style.backgroundImage = new StyleBackground(tex);
                pager.Add(art);
                pager.Add(UiKit.Text(L.T("intro." + n + ".t"), "cn-pager__title"));
                pager.Add(UiKit.Text(L.T("intro." + n + ".b"), "cn-pager__body"));
                var dots = new VisualElement { pickingMode = PickingMode.Ignore };
                dots.AddToClassList("cn-pager__dots");
                for (int i = 0; i < 3; i++)
                {
                    var d = new VisualElement();
                    d.AddToClassList("cn-pager__dot");
                    d.EnableInClassList("cn-pager__dot--on", i == pagerPage);
                    dots.Add(d);
                }
                pager.Add(dots);
                var actions = new VisualElement();
                actions.AddToClassList("cn-pager__actions");
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
