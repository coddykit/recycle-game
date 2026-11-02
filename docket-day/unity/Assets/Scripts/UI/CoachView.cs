// Docket Day — the guide's view (fleet §9 / §17): the intro pager (art + step dots + Skip), then the coach bubble with
// the senior counsel's portrait (Meshy hero, pre-rendered), a pulsing spotlight ring on the target (the reception
// marker, a task chip on the Docket Rail, the clock, a pill inside a sheet), the suggested person card glowing, and
// "Skip". The sheet a step needs is opened for the player (reception for Accept, the prep table, the office for the
// library), so a first-time player never has to find it.
using System;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using DocketDay.Game;
using DocketDay.Sim;

namespace DocketDay.UI
{
    public sealed partial class GameScreen
    {
        VisualElement coachBubble, spot, pager;
        Label coachText, coachStep;
        Button coachSkip;
        int shownStep = -1, shownFlash = -1;
        string shownPhase = "";
        float pulse;
        int pagerPage;
        public bool PagerOpen => pager != null && pager.parent != null;

        void BuildCoach()
        {
            spot = new VisualElement { name = "Spot", pickingMode = PickingMode.Ignore };
            spot.AddToClassList("dd-spot");
            spot.style.display = DisplayStyle.None;
            root.Add(spot);
            coachBubble = new VisualElement { name = "Coach" };
            coachBubble.AddToClassList("dd-coach");
            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.AddToClassList("dd-coach__face");
            var portrait = Resources.Load<Texture2D>("ui/coach_counsel");
            if (portrait != null) face.style.backgroundImage = new StyleBackground(portrait);
            coachBubble.Add(face);
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.AddToClassList("dd-coach__texts");
            coachText = UiKit.Text("", "dd-coach__text");
            coachStep = UiKit.Text("", "dd-coach__step");
            texts.Add(coachText);
            texts.Add(coachStep);
            coachBubble.Add(texts);
            coachSkip = UiKit.ActionPill("", () => main.SkipGuide(), true, "tonal");
            coachSkip.AddToClassList("dd-coach__skip");
            coachBubble.Add(coachSkip);
            coachBubble.style.display = DisplayStyle.None;
            root.Add(coachBubble);
        }

        void CoachTick(float dt)
        {
            var c = main.Coach;
            bool on = c.Active && !PagerOpen && !HearingOpen;
            coachBubble.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) { spot.style.display = DisplayStyle.None; shownStep = -1; return; }
            if (c.Step != shownStep || Sim.Phase != shownPhase)
            {
                shownStep = c.Step;
                shownPhase = Sim.Phase;
                OnCoachStep(c.Step);
                staffKey = "";
            }
            if (c.Flash != shownFlash) { shownFlash = c.Flash; pulse = 1.2f; }
            UiKit.SetText(coachText, L.T(c.BubbleKey));
            UiKit.SetText(coachStep, L.F("coach.step", "n", c.Step, "m", Coach.Steps));
            UiKit.SetText(coachSkip, L.T("coach.skip"));
            Rect? r = UiTarget(c.Target(Sim));
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
                coachBubble.EnableInClassList("dd-coach--top", rc.center.y > root.layout.height * 0.45f);
            }
            else spot.style.display = DisplayStyle.None;
            pulse = Mathf.Max(0, pulse - dt);
            float s = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f) + pulse * 0.25f;
            spot.style.scale = new Scale(new Vector3(s, s, 1));
        }

        void OnCoachStep(int step)
        {
            int t = Sim.S.TutorialMatter;
            switch (step)
            {
                case 1: case 3: case 4: case 5: case 9:
                    if (sheet != null && sheet.IsOpen) CloseSheet();
                    break;
                case 2: OpenSheet("intake"); break;
                case 6: OpenSheet("prep", t); break;
                case 7:
                    if (Sim.Phase == Phases.Report) OpenSheet("report");
                    else if (sheet != null && sheet.IsOpen) CloseSheet();
                    break;
                case 8: OpenSheet("office"); break;
            }
            if (main.world != null && main.world.rig != null)
            {
                if (step == 1) main.world.rig.Focus(DocketDay.World.RegionWorld.ReceptionPos, 0.6f);
                if (step == 7) main.world.rig.Focus(DocketDay.World.RegionWorld.CourtDoor, 0.7f);
            }
        }

        // ------------------------------------------------------------------ intro pager

        public void ShowPager(Action done)
        {
            pagerPage = 0;
            pager = new VisualElement { name = "Pager" };
            pager.AddToClassList("dd-pager");
            root.Add(pager);
            void Render()
            {
                pager.Clear();
                int n = pagerPage + 1;
                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("dd-pager__art");
                var tex = Resources.Load<Texture2D>("ui/intro_" + n);
                if (tex != null) art.style.backgroundImage = new StyleBackground(tex);
                pager.Add(art);
                pager.Add(UiKit.Text(L.T("intro." + n + ".t"), "dd-pager__title"));
                pager.Add(UiKit.Text(L.T("intro." + n + ".b"), "dd-pager__body"));
                var dots = new VisualElement { pickingMode = PickingMode.Ignore };
                dots.AddToClassList("dd-pager__dots");
                for (int i = 0; i < 3; i++)
                {
                    var d = new VisualElement();
                    d.AddToClassList("dd-pager__dot");
                    d.EnableInClassList("dd-pager__dot--on", i == pagerPage);
                    dots.Add(d);
                }
                pager.Add(dots);
                var actions = new VisualElement();
                actions.AddToClassList("dd-pager__actions");
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
