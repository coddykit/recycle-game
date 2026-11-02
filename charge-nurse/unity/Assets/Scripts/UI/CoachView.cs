// Charge Nurse — the guide's view (fleet §9 / §17): the intro pager (art + step dots + Skip), then the coach bubble with
// the operator's portrait (Meshy hero, pre-rendered), a pulsing spotlight ring on the target (a world point, the van, the
// depot buttons, a HUD badge or a pill inside a sheet), and "Skip". The sheet a step needs is opened for the player
// (point 1 for the rule and the bigger bin, Fleet for the hire), so a first-time player never has to find it.
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
            spot.AddToClassList("br-spot");
            spot.style.display = DisplayStyle.None;
            root.Add(spot);
            coachBubble = new VisualElement { name = "Coach" };
            coachBubble.AddToClassList("br-coach");
            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.AddToClassList("br-coach__face");
            var portrait = Resources.Load<Texture2D>("ui/coach_operator");
            if (portrait != null) face.style.backgroundImage = new StyleBackground(portrait);
            coachBubble.Add(face);
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.AddToClassList("br-coach__texts");
            coachText = UiKit.Text("", "br-coach__text");
            coachStep = UiKit.Text("", "br-coach__step");
            texts.Add(coachText);
            texts.Add(coachStep);
            coachBubble.Add(texts);
            coachSkip = UiKit.ActionPill("", () => main.SkipGuide(), true, "tonal");
            coachSkip.AddToClassList("br-coach__skip");
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
            // spotlight
            Rect? r = null;
            string tgt = c.Target;
            if (tgt == "truck:0" && World != null)
            {
                var sp = World.ScreenOf(World.TruckWorld(0) + Vector3.up * 3f);
                if (sp != null) { var pp = ToPanel(sp.Value); r = new Rect(pp.x - 40, pp.y - 40, 80, 80); }
            }
            else r = UiTarget(tgt);
            if (r != null && c.Step != 6)
            {
                spot.style.display = DisplayStyle.Flex;
                var rc = r.Value;
                float size = Mathf.Max(96f, Mathf.Max(rc.width, rc.height) + 24f);
                spot.style.width = spot.style.height = size;
                spot.style.marginLeft = spot.style.marginTop = -size * 0.5f;
                spot.style.borderTopLeftRadius = spot.style.borderTopRightRadius = spot.style.borderBottomLeftRadius = spot.style.borderBottomRightRadius = size * 0.5f;
                spot.style.translate = new Translate(rc.center.x, rc.center.y);
                // the bubble moves to the top when the target is in the lower half
                coachBubble.EnableInClassList("br-coach--top", rc.center.y > root.layout.height * 0.55f);
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
                case 1: case 2: case 3: case 4: case 5: case 6: case 7: case 8: case 11:
                    if (sheet != null && sheet.IsOpen) CloseSheet();
                    break;
                case 9: case 10: OpenSheet("depot"); break;
                case 12: case 13: OpenSheet("point", 1); break;
                case 14: OpenSheet("fleet"); break;
            }
            if (World != null && main.world.rig != null)
            {
                if (step == 1 || step == 3) main.world.rig.Focus(World.PointPos(0), 0.55f);
                if (step == 4) main.world.rig.Focus((World.PointPos(0) + World.PointPos(1)) * 0.5f, 0.6f);
                if (step == 7) main.world.rig.Focus(World.DepotPos, 0.55f);
            }
        }

        // ------------------------------------------------------------------ intro pager

        public void ShowPager(Action done)
        {
            pagerPage = 0;
            pager = new VisualElement { name = "Pager" };
            pager.AddToClassList("br-pager");
            root.Add(pager);
            void Render()
            {
                pager.Clear();
                int n = pagerPage + 1;
                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("br-pager__art");
                var tex = Resources.Load<Texture2D>("ui/intro_" + n);
                if (tex != null) art.style.backgroundImage = new StyleBackground(tex);
                pager.Add(art);
                pager.Add(UiKit.Text(L.T("intro." + n + ".t"), "br-pager__title"));
                pager.Add(UiKit.Text(L.T("intro." + n + ".b"), "br-pager__body"));
                var dots = new VisualElement { pickingMode = PickingMode.Ignore };
                dots.AddToClassList("br-pager__dots");
                for (int i = 0; i < 3; i++)
                {
                    var d = new VisualElement();
                    d.AddToClassList("br-pager__dot");
                    d.EnableInClassList("br-pager__dot--on", i == pagerPage);
                    dots.Add(d);
                }
                pager.Add(dots);
                var actions = new VisualElement();
                actions.AddToClassList("br-pager__actions");
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
