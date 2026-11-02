// Semester Rush — the guide's view (fleet §9 / §17): the intro pager (art + step dots + Skip), then the coach bubble with
// the dean's portrait (Meshy hero, pre-rendered), a pulsing spotlight ring on the target (a room, a board cell, a course,
// a professor pill, the Start button, the late counter, a gate, the bursar's coins, a plot, the lunch move, the grades)
// and "Skip". The sheet a step needs is opened for the player (the board for the professor and the lunch move), so a
// first-time player never has to find it.
using System;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using SemesterRush.Game;

namespace SemesterRush.UI
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
            spot.AddToClassList("sr-spot");
            spot.style.display = DisplayStyle.None;
            root.Add(spot);
            coachBubble = new VisualElement { name = "Coach" };
            coachBubble.AddToClassList("sr-coach");
            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.AddToClassList("sr-coach__face");
            var portrait = Resources.Load<Texture2D>("ui/coach_dean");
            if (portrait != null) face.style.backgroundImage = new StyleBackground(portrait);
            coachBubble.Add(face);
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.AddToClassList("sr-coach__texts");
            coachText = UiKit.Text("", "sr-coach__text");
            coachStep = UiKit.Text("", "sr-coach__step");
            texts.Add(coachText);
            texts.Add(coachStep);
            coachBubble.Add(texts);
            coachSkip = UiKit.ActionPill("", () => main.SkipGuide(), true, "tonal");
            coachSkip.AddToClassList("sr-coach__skip");
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
            Rect? r = UiTarget(c.Target);
            if (r != null)
            {
                spot.style.display = DisplayStyle.Flex;
                var rc = r.Value;
                float size = Mathf.Max(80f, Mathf.Max(rc.width, rc.height) + 24f);
                spot.style.width = spot.style.height = size;
                spot.style.marginLeft = spot.style.marginTop = -size * 0.5f;
                spot.style.borderTopLeftRadius = spot.style.borderTopRightRadius = spot.style.borderBottomLeftRadius = spot.style.borderBottomRightRadius = size * 0.5f;
                spot.style.translate = new Translate(rc.center.x, rc.center.y);
                // the bubble moves to the top when the target is in the lower half
                coachBubble.EnableInClassList("sr-coach--top", rc.center.y > root.layout.height * 0.5f);
            }
            else spot.style.display = DisplayStyle.None;
            pulse = Mathf.Max(0, pulse - dt);
            float s = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f) + pulse * 0.25f;
            spot.style.scale = new Scale(new Vector3(s, s, 1));
        }

        void OnCoachStep(int step)
        {
            var sim = Sim;
            switch (step)
            {
                case 1: case 5: case 6: case 7: case 8: case 9: case 11:
                    if (sheet != null && sheet.IsOpen) CloseSheet();
                    break;
                case 4: OpenSheet("board", sim.MainHall()); break;
                case 10:
                {
                    var fix = main.Coach.Fix;
                    if (fix != null) { Act.SelectPlacement(fix.Placement); OpenSheet("board", sim.MainHall()); }
                    break;
                }
            }
            if (World != null && main.world.rig != null)
            {
                if (step == 1) main.world.rig.Focus(World.NodePos(sim.MainHall()), 0.6f);
                if (step == 7) { int g = Coach.GuideGate(sim); if (g >= 0) main.world.rig.Focus(World.EdgeMid(g), 0.6f); }
                if (step == 8) main.world.rig.Focus(World.NodePos(sim.Gd.Node("admin")), 0.6f);
                if (step == 9) { int n = sim.Gd.Node(Coach.LibrarySite(sim)); if (n >= 0) main.world.rig.Focus(World.NodePos(n), 0.6f); }
            }
        }

        // ------------------------------------------------------------------ intro pager

        public void ShowPager(Action done)
        {
            pagerPage = 0;
            pager = new VisualElement { name = "Pager" };
            pager.AddToClassList("sr-pager");
            root.Add(pager);
            void Render()
            {
                pager.Clear();
                int n = pagerPage + 1;
                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("sr-pager__art");
                var tex = Resources.Load<Texture2D>("ui/intro_" + n);
                if (tex != null) art.style.backgroundImage = new StyleBackground(tex);
                pager.Add(art);
                pager.Add(UiKit.Text(L.T("intro." + n + ".t"), "sr-pager__title"));
                pager.Add(UiKit.Text(L.T("intro." + n + ".b"), "sr-pager__body"));
                var dots = new VisualElement { pickingMode = PickingMode.Ignore };
                dots.AddToClassList("sr-pager__dots");
                for (int i = 0; i < 3; i++)
                {
                    var d = new VisualElement();
                    d.AddToClassList("sr-pager__dot");
                    d.EnableInClassList("sr-pager__dot--on", i == pagerPage);
                    dots.Add(d);
                }
                pager.Add(dots);
                var actions = new VisualElement();
                actions.AddToClassList("sr-pager__actions");
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
