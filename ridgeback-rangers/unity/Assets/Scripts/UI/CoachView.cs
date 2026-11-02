// Ridgeback Rangers — the guide's view (fleet §9 / §17): the intro pager (art + step dots + Skip), then the coach bubble with
// the Chief Ranger's portrait (Meshy hero, pre-rendered), a pulsing spotlight ring on the target (a pin over a paddock, the
// gate, the tram, the Chief's badge, the "Fed!" button or a pill inside a sheet), and "Skip". The sheet a step needs is
// opened for the player (the pin card, the Compatibility card, the paddock for the fence, the day report, the hatchery), and
// the camera points at the target, so a first-time player never has to find it. Step 2 (move the island) shows a swipe hint.
using System;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using RidgebackRangers.Game;
using RidgebackRangers.Sim;

namespace RidgebackRangers.UI
{
    public sealed partial class GameScreen
    {
        VisualElement coachBubble, spot, pager, swipeHint;
        Label coachText, coachStep;
        Button coachSkip;
        int shownStep = -1, shownFlash = -1;
        string shownTarget = "";
        float pulse;
        int pagerPage;
        public bool PagerOpen => pager != null && pager.parent != null;

        void BuildCoach()
        {
            spot = new VisualElement { name = "Spot", pickingMode = PickingMode.Ignore };
            spot.AddToClassList("rr-spot");
            spot.style.display = DisplayStyle.None;
            root.Add(spot);
            swipeHint = UiKit.Text("⇆", "rr-swipe");
            swipeHint.pickingMode = PickingMode.Ignore;
            swipeHint.style.display = DisplayStyle.None;
            root.Add(swipeHint);
            coachBubble = new VisualElement { name = "Coach" };
            coachBubble.AddToClassList("rr-coach");
            var face = new VisualElement { pickingMode = PickingMode.Ignore };
            face.AddToClassList("rr-coach__face");
            var portrait = Resources.Load<Texture2D>("ui/coach_warden");
            if (portrait != null) face.style.backgroundImage = new StyleBackground(portrait);
            coachBubble.Add(face);
            var texts = new VisualElement { pickingMode = PickingMode.Ignore };
            texts.AddToClassList("rr-coach__texts");
            coachText = UiKit.Text("", "rr-coach__text");
            coachStep = UiKit.Text("", "rr-coach__step");
            texts.Add(coachText);
            texts.Add(coachStep);
            coachBubble.Add(texts);
            coachSkip = UiKit.ActionPill("", () => main.SkipGuide(), true, "tonal");
            coachSkip.AddToClassList("rr-coach__skip");
            coachBubble.Add(coachSkip);
            coachBubble.style.display = DisplayStyle.None;
            root.Add(coachBubble);
        }

        void CoachTick(float dt)
        {
            var c = main.Coach;
            bool on = c.Active && !PagerOpen;
            coachBubble.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) { spot.style.display = DisplayStyle.None; swipeHint.style.display = DisplayStyle.None; shownStep = -1; return; }
            string tgt = c.Target;
            if (c.Step != shownStep || tgt != shownTarget)
            {
                bool stepChanged = c.Step != shownStep;
                shownStep = c.Step;
                shownTarget = tgt;
                OnCoachTarget(c.Step, tgt, stepChanged);
            }
            if (c.Flash != shownFlash) { shownFlash = c.Flash; pulse = 1.2f; }
            UiKit.SetText(coachText, L.T(c.BubbleKey));
            UiKit.SetText(coachStep, L.F("coach.step", "n", c.Step, "m", Coach.Steps));
            UiKit.SetText(coachSkip, L.T("coach.skip"));
            swipeHint.style.display = tgt == "camera" ? DisplayStyle.Flex : DisplayStyle.None;
            if (tgt == "camera") swipeHint.style.translate = new Translate(Mathf.Sin(Time.unscaledTime * 2.5f) * 60f, 0);
            // spotlight
            Rect? r = UiTarget(tgt);
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
                coachBubble.EnableInClassList("rr-coach--top", rc.center.y > root.layout.height * 0.55f);
            }
            else spot.style.display = DisplayStyle.None;
            pulse = Mathf.Max(0, pulse - dt);
            float s = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f) + pulse * 0.25f;
            spot.style.scale = new Scale(new Vector3(s, s, 1));
        }

        /// <summary>Opens the sheet a step needs and points the camera (once per step / target change).</summary>
        void OnCoachTarget(int step, string target, bool stepChanged)
        {
            var rig = main.world != null ? main.world.rig : null;
            switch (step)
            {
                case 1: case 2: case 3: case 5: case 6: case 7:
                    if (sheet != null && sheet.IsOpen) CloseSheet();
                    break;
                case 4:
                {
                    var p = Sim.S.Pins.Find(x => x.Kind == PinKinds.Hungry);
                    if (p != null && (SheetKind != "pin" || sheetArg != p.Id)) OpenSheet("pin", p.Id);
                    break;
                }
                case 8:
                {
                    var p = Sim.S.Pins.Find(x => x.Kind == PinKinds.Squabble);
                    if (target == "ranger:handler" && p != null) OpenSheet("pin", p.Id);
                    else if (sheet != null && sheet.IsOpen) CloseSheet();
                    break;
                }
                case 9: if (SheetKind != "compare") OpenSheet("compare"); else sheetDirty = true; break;
                case 10: OpenSheet("paddock", Coach.SquabblePaddock); break;
                case 11: if (target == "next_day") OpenSheet("report"); else if (sheet != null && sheet.IsOpen) CloseSheet(); break;
                case 12: OpenSheet("hatchery"); break;
            }
            if (World == null || rig == null || !stepChanged) return;
            if (step == 3 || step == 5) rig.Focus(World.PaddockCenter(0), 0.55f);
            if (step == 6) rig.Focus(World.GatePos, 0.6f);
            if (step == 7) rig.Focus(World.StationPos, 0.6f);
            if (step == 8) rig.Focus(World.PaddockCenter(Coach.SquabblePaddock), 0.55f);
        }

        // ------------------------------------------------------------------ intro pager

        public void ShowPager(Action done)
        {
            pagerPage = 0;
            pager = new VisualElement { name = "Pager" };
            pager.AddToClassList("rr-pager");
            root.Add(pager);
            void Render()
            {
                pager.Clear();
                int n = pagerPage + 1;
                var art = new VisualElement { pickingMode = PickingMode.Ignore };
                art.AddToClassList("rr-pager__art");
                var tex = Resources.Load<Texture2D>("ui/intro_" + n);
                if (tex != null) art.style.backgroundImage = new StyleBackground(tex);
                pager.Add(art);
                pager.Add(UiKit.Text(L.T("intro." + n + ".t"), "rr-pager__title"));
                pager.Add(UiKit.Text(L.T("intro." + n + ".b"), "rr-pager__body"));
                var dots = new VisualElement { pickingMode = PickingMode.Ignore };
                dots.AddToClassList("rr-pager__dots");
                for (int i = 0; i < 3; i++)
                {
                    var d = new VisualElement();
                    d.AddToClassList("rr-pager__dot");
                    d.EnableInClassList("rr-pager__dot--on", i == pagerPage);
                    dots.Add(d);
                }
                pager.Add(dots);
                var actions = new VisualElement();
                actions.AddToClassList("rr-pager__actions");
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
