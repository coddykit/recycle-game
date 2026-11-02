// Docket Day — the hearing scene (plan §4 step 6, §8): a 15-25 s courtroom moment over the diorama. Fixed-pose portraits
// (the judge, your lawyer, the client — Meshy heroes pre-rendered by tools/art/render_ui.py) cross-fade through four
// lines; the optional "Objection!" timing tap (±5 % odds, Settings can turn it off) sits on the third line; then the
// verdict the sim computes from prep + staff skill + the judge's habit (the odds are on screen the whole time, so the
// court flow is always explained). The sim is paused while the scene is open; a tap on the scene skips ahead. When the
// scene is not shown (background, offline, bots) the session ends by itself after 45 game minutes.
// Low tier: no tweens — one portrait at a time, instant swaps (no extra draw, no per-frame layout).
using System;
using System.Collections.Generic;
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
        const float LineS = 3.2f, VerdictS = 3f;
        VisualElement hearing, hearingBar, hearingMarker, judgeArt, counselArt, clientArt;
        Label hearingTitle, hearingLine, hearingOdds, hearingVerdict;
        Button objectionBtn, hearingContinue;
        readonly Queue<int> hearingQueue = new Queue<int>();
        int hearingMatter = -1, objection;
        float hearingT;
        bool verdictShown;
        public bool HearingOpen => hearing != null && hearing.style.display == DisplayStyle.Flex;

        void BuildHearing()
        {
            hearing = new VisualElement { name = "Hearing" };
            hearing.AddToClassList("dd-hearing");
            hearing.style.display = DisplayStyle.None;
            hearing.RegisterCallback<ClickEvent>(e => { if (e.target == hearing && !verdictShown) hearingT = Mathf.Max(hearingT, LineS * 4f); });
            hearingTitle = UiKit.Text("", "dd-hearing__title");
            hearing.Add(hearingTitle);
            var stage = new VisualElement { pickingMode = PickingMode.Ignore };
            stage.AddToClassList("dd-hearing__stage");
            judgeArt = Portrait(stage, "ui/hearing_judge", "dd-hearing__judge");
            counselArt = Portrait(stage, "ui/coach_counsel", "dd-hearing__counsel");
            clientArt = Portrait(stage, "ui/hearing_client", "dd-hearing__client");
            hearing.Add(stage);
            hearingLine = UiKit.Text("", "dd-hearing__line");
            hearing.Add(hearingLine);
            hearingOdds = UiKit.Text("", "dd-hearing__odds");
            hearing.Add(hearingOdds);
            hearingBar = new VisualElement { pickingMode = PickingMode.Ignore };
            hearingBar.AddToClassList("dd-hearing__bar");
            var green = new VisualElement { pickingMode = PickingMode.Ignore };
            green.AddToClassList("dd-hearing__green");
            hearingBar.Add(green);
            hearingMarker = new VisualElement { pickingMode = PickingMode.Ignore };
            hearingMarker.AddToClassList("dd-hearing__marker");
            hearingBar.Add(hearingMarker);
            hearing.Add(hearingBar);
            objectionBtn = UiKit.PrimaryButton("", OnObjection);
            objectionBtn.AddToClassList("dd-hearing__objection");
            hearing.Add(objectionBtn);
            hearingVerdict = UiKit.Text("", "dd-hearing__verdict");
            hearing.Add(hearingVerdict);
            hearingContinue = UiKit.PrimaryButton("", CloseHearing);
            hearing.Add(hearingContinue);
            root.Add(hearing);
        }

        static VisualElement Portrait(VisualElement parent, string res, string cls)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.AddToClassList("dd-hearing__portrait");
            v.AddToClassList(cls);
            var tex = Resources.Load<Texture2D>(res);
            if (tex != null) v.style.backgroundImage = new StyleBackground(tex);
            parent.Add(v);
            return v;
        }

        /// <summary>A hearing started (sim event): show its scene now or after the current one.</summary>
        public void QueueHearing(int matterId)
        {
            if (hearingMatter == matterId || hearingQueue.Contains(matterId)) return;
            hearingQueue.Enqueue(matterId);
            if (!HearingOpen) NextHearing();
        }

        void NextHearing()
        {
            while (hearingQueue.Count > 0)
            {
                int id = hearingQueue.Dequeue();
                var m = Sim.MatterById(id);
                if (m == null || m.Status != MatterStatus.Hearing) continue;
                hearingMatter = id;
                hearingT = 0;
                objection = 0;
                verdictShown = false;
                if (sheet != null && sheet.IsOpen) CloseSheet();
                hearing.style.display = DisplayStyle.Flex;
                hearing.BringToFront();
                UiKit.SetText(hearingTitle, L.F("hearing.title", "name", L.T("matter." + m.Area + "." + m.Title)) + " · " + L.T("judge." + Sim.Gd.Judges[m.Judge].Id));
                hearingVerdict.style.display = DisplayStyle.None;
                hearingContinue.style.display = DisplayStyle.None;
                UiKit.SetText(objectionBtn, L.T("hearing.objection"));
                UiKit.SetText(hearingContinue, L.T("hearing.continue"));
                main.Audio?.Play("gavel");
                return;
            }
            hearingMatter = -1;
            hearing.style.display = DisplayStyle.None;
        }

        void HearingTick(float dt)
        {
            if (!HearingOpen) return;
            var m = Sim.MatterById(hearingMatter);
            if (!verdictShown && (m == null || m.Status != MatterStatus.Hearing)) { NextHearing(); return; }
            hearingT += dt;
            if (verdictShown)
            {
                if (hearingT > VerdictS + 6f) CloseHearing();
                return;
            }
            int line = Mathf.Min(3, (int)(hearingT / LineS));
            var h = m.HearingTask;
            UiKit.SetText(hearingLine, h != null && h.Staff == Attend.None && line == 1 ? L.T("hearing.nolawyer") : L.T("hearing.line." + line));
            UiKit.SetText(hearingOdds, L.F("hearing.odds", "pct", Fmt.Pct(Sim.Odds(m, objection, m.Revealed), L.Lang)));
            // fixed-pose portraits: who speaks is lit (Low: instant, Medium/High: a soft fade)
            float judgeA = line == 0 || line == 3 ? 1f : 0.45f, counselA = line == 1 || line == 2 ? 1f : 0.45f;
            judgeArt.style.opacity = UiKit.Low ? judgeA : Mathf.MoveTowards(judgeArt.resolvedStyle.opacity, judgeA, dt * 3f);
            counselArt.style.opacity = UiKit.Low ? counselA : Mathf.MoveTowards(counselArt.resolvedStyle.opacity, counselA, dt * 3f);
            clientArt.style.opacity = 0.8f;
            bool window = line == 2 && objection == 0 && main.Session.Settings.ObjectionTap;
            hearingBar.style.display = window ? DisplayStyle.Flex : DisplayStyle.None;
            objectionBtn.style.display = window ? DisplayStyle.Flex : DisplayStyle.None;
            if (window) hearingMarker.style.left = Length.Percent(MarkerPct() * 100f);
            if (hearingT >= LineS * 4f) ShowVerdict();
        }

        float MarkerPct() => Mathf.PingPong((hearingT - LineS * 2f) * 0.9f, 1f);

        void OnObjection()
        {
            if (objection != 0) return;
            float p = MarkerPct();
            objection = p >= 0.4f && p <= 0.6f ? 1 : 2;
            Toast(L.T(objection == 1 ? "hearing.good" : "hearing.bad"), objection == 1 ? ToastKind.Success : ToastKind.Warning);
            main.Audio?.Play(objection == 1 ? "success" : "warning");
        }

        void ShowVerdict()
        {
            var m = Sim.MatterById(hearingMatter);
            if (m == null) { NextHearing(); return; }
            string name = L.T("matter." + m.Area + "." + m.Title);
            double before = Sim.S.Wip;
            var r = main.Run("resolve:" + hearingMatter + ":" + objection);
            if (!r.Ok) { NextHearing(); return; }
            double fee = Sim.S.Wip - before;
            bool won = r.Value >= 1;
            verdictShown = true;
            hearingT = 0;
            hearingBar.style.display = DisplayStyle.None;
            objectionBtn.style.display = DisplayStyle.None;
            hearingVerdict.style.display = DisplayStyle.Flex;
            hearingContinue.style.display = DisplayStyle.Flex;
            hearingVerdict.EnableInClassList("dd-hearing__verdict--won", won);
            UiKit.SetText(hearingVerdict, L.F(won ? "hearing.won" : "hearing.lost", "cash", Money(fee)));
            UiKit.SetText(hearingLine, name);
            main.Audio?.Play(won ? "verdict_win" : "verdict_lose");
        }

        void CloseHearing()
        {
            hearing.style.display = DisplayStyle.None;
            hearingMatter = -1;
            NextHearing();
        }
    }
}
