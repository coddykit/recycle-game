// Volt City UI — the portrait game screen (UI.md section 1), kit design 2, UI Toolkit:
//   SafeArea root (all edges from the kit SafeArea, bottom = max(nav bar, house banner))
//   ├ HUD glass card: row 1 cash · rating · clock + phase chip · speed pill · gear; row 2 Balance needle + goal line
//   ├ diorama band (pickingMode Ignore: world taps / pans pass through; the camera fits the hooked districts into it)
//   ├ bottom sheet: pill handle (tap = half / full) · page header (title + ONE summary line) · primary action · rows
//   └ tab bar: Grid · Crews · City · Build · Shop
//   overlays: modal sheet host · coach spotlight · pager
// Live labels refresh at <= 4 Hz and only when the text changed; lists reconcile by row id and wait while a finger is
// down. Every control that the guide can point at carries its target id (element name). UNCOMPILED until the editor.
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.UIElements;
using VoltCity.Game;
using TabView = VoltCity.Game.TabView; // Unity 6 UI Toolkit also has a TabView control
using VoltCity.Sim;

namespace VoltCity.UI
{
    public sealed class GameScreen
    {
        public const float RefreshS = 0.25f;

        readonly VisualElement doc;
        readonly Loc loc;
        readonly Func<Simulation> sim;
        readonly GameActions actions;
        readonly Coach coach;
        readonly Func<float> bannerHeight;
        public VisualElement Root { get; private set; }
        public SheetHost Sheets { get; private set; }
        public CoachOverlay CoachUi { get; private set; }
        public string Tab { get; private set; } = "grid";
        /// <summary>P4 shop + rewarded rows (VoltCityMain sets it; null = P3 views).</summary>
        public Extras Extras;
        string theme = "";
        public bool SheetFull { get; private set; }

        /// <summary>Navigation the main controller handles (settings → language / graphics / replay / restore, shop…).</summary>
        public event Action<string> Navigate;
        /// <summary>The free band for the diorama changed (normalised viewport, y up).</summary>
        public event Action<Rect> DioramaBand;

        VisualElement safe, hud, band, tabSheet, tabBar, sheetHandle, primaryRow, explainer;
        Label cash, rating, clock, phase, goal, explainerText;
        Button speed, gear, primary;
        RowAction primaryAct;
        NeedleView needle;
        UiKit.PageHeader header;
        RowList list;
        readonly Dictionary<string, Button> tabButtons = new Dictionary<string, Button>();
        Pager pager;
        float acc = 99f;
        Rect lastBand;

        public GameScreen(VisualElement docRoot, Loc loc, Func<Simulation> sim, GameActions actions, Coach coach, Func<float> bannerHeight)
        {
            doc = docRoot;
            this.loc = loc;
            this.sim = sim;
            this.actions = actions;
            this.coach = coach;
            this.bannerHeight = bannerHeight;
            Build();
        }

        // =========================================================================================== build

        public void Build()
        {
            doc.Clear();
            var ss = Resources.Load<StyleSheet>("ui/VoltCity");
            if (ss != null && !doc.styleSheets.Contains(ss)) doc.styleSheets.Add(ss);
            Root = new VisualElement { name = "VoltScreen", pickingMode = PickingMode.Ignore };
            Root.AddToClassList("vc-screen");
            if (theme != "") Root.AddToClassList(theme);
            doc.Add(Root);

            safe = new VisualElement { name = "Safe", pickingMode = PickingMode.Ignore };
            safe.AddToClassList("vc-safe");
            Root.Add(safe);
            // the HUD's background runs up under the status bar / cutout and the tab bar's down under the navigation bar
            // (Patron 10-28: no empty dark bands); each pads its own content by the inset (EdgesChanged)
            var safePad = SafeAreaPadding.Attach(safe, false, bannerHeight, padTop: false, padBottom: false);

            BuildHud();
            band = new VisualElement { name = "DioramaBand", pickingMode = PickingMode.Ignore };
            band.AddToClassList("vc-band");
            safe.Add(band);
            BuildTabSheet();
            BuildTabBar();

            var sheetLayer = new VisualElement { name = "Sheets", pickingMode = PickingMode.Ignore };
            sheetLayer.AddToClassList("vc-layer");
            Root.Add(sheetLayer);
            Sheets = new SheetHost(sheetLayer, () => SafeArea.Bottom(PanelW, PanelH) + bannerHeight());

            CoachUi = new CoachOverlay(OnWrongTap, () => Navigate?.Invoke("coach.skip"), loc.T("coach.skip"));
            Root.Add(CoachUi.Root);

            safePad.Changed += EdgesChanged;
            EdgesChanged(safePad.Applied);
            band.RegisterCallback<GeometryChangedEvent>(_ => ReportBand());
            tabSheet.RegisterCallback<GeometryChangedEvent>(_ => ReportBand());
            SelectTab(Tab, true);
            acc = 99f;
        }

        void EdgesChanged(Insets e)
        {
            // top: the status bar is reserved even while hidden (it comes back on a swipe), cutout included
            float top = Mathf.Max(e.Top, SafeArea.OverlayEdges(PanelW, PanelH).Top);
            hud.style.paddingTop = 8f + top;
            tabBar.style.paddingBottom = e.Bottom;
            tabBar.style.height = 100f + e.Bottom;
        }

        float PanelW => Root.panel?.visualTree.layout.width ?? 720f;
        float PanelH => Root.panel?.visualTree.layout.height ?? 1280f;

        Label Stat(VisualElement row, string name, string cls)
        {
            var box = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            box.AddToClassList("vc-stat");
            var l = UiKit.Text("", cls);
            box.Add(l);
            row.Add(box);
            return l;
        }

        void BuildHud()
        {
            hud = new VisualElement { name = "Hud" };
            hud.AddToClassList(UiKit.Low ? "vc-hud--flat" : "vc-hud");
            safe.Add(hud);
            var row1 = new VisualElement { pickingMode = PickingMode.Ignore };
            row1.AddToClassList("vc-hud__row");
            hud.Add(row1);
            cash = Stat(row1, "hud.cash", "vc-stat__big");
            rating = Stat(row1, "hud.rating", "vc-stat__big");
            var clockBox = new VisualElement { name = "hud.clock", pickingMode = PickingMode.Ignore };
            clockBox.AddToClassList("vc-stat");
            clockBox.AddToClassList("vc-stat--grow");
            clock = UiKit.Text("", "vc-stat__big");
            phase = UiKit.Text("", "vc-chip");
            clockBox.Add(clock);
            clockBox.Add(phase);
            row1.Add(clockBox);
            speed = UiKit.ActionPill("1×", () => Run(new RowAction { Id = "speed_next", Target = "hud.speed" }), true, "tonal");
            speed.name = "hud.speed";
            speed.AddToClassList("vc-speed");
            row1.Add(speed);
            gear = UiKit.CloseButton(() => { if (Gate("hud.settings")) Navigate?.Invoke("settings"); }, loc.T("settings.title"));
            UiKit.WithIcon(gear, "gear");   // kit icon, never a ⚙ glyph (SE device check 2026-10-30)
            gear.name = "hud.settings";
            row1.Add(gear);

            var row2 = new VisualElement { pickingMode = PickingMode.Ignore };
            row2.AddToClassList("vc-hud__row2");
            hud.Add(row2);
            needle = new NeedleView(() => Run(new RowAction { Id = "open:needle", Target = "hud.needle" }));
            row2.Add(needle);
            var goalBox = new VisualElement { name = "hud.goal" };
            goalBox.AddToClassList("vc-goal");
            goal = UiKit.Text("", "vc-goal__text");
            goalBox.Add(goal);
            TapGesture.Attach(goalBox, () => { if (Gate("hud.goal")) Navigate?.Invoke("goals"); });
            row2.Add(goalBox);

            explainer = new VisualElement { name = "NeedleExplainer" };
            explainer.AddToClassList("vc-explainer");
            explainerText = UiKit.Text("", "coddy-body");
            explainer.Add(explainerText);
            explainer.style.display = DisplayStyle.None;
            TapGesture.Attach(explainer, () => explainer.style.display = DisplayStyle.None);
            hud.Add(explainer);
        }

        void BuildTabSheet()
        {
            tabSheet = new VisualElement { name = "TabSheet" };
            tabSheet.AddToClassList("vc-tabsheet");
            safe.Add(tabSheet);
            sheetHandle = new VisualElement { name = "sheet.handle" };
            sheetHandle.AddToClassList("vc-tabsheet__handle-zone");
            var pill = new VisualElement { pickingMode = PickingMode.Ignore };
            pill.AddToClassList("coddy-sheet__handle");
            sheetHandle.Add(pill);
            TapGesture.Attach(sheetHandle, () => { if (Gate("sheet.handle")) SetSheetFull(!SheetFull); });
            tabSheet.Add(sheetHandle);
            header = UiKit.Header("", "", null, null);
            tabSheet.Add(header.Root);
            primaryRow = new VisualElement();
            primaryRow.AddToClassList("vc-primary-row");
            primary = UiKit.PrimaryButton("", () => { if (primaryAct != null) Run(primaryAct); });
            primaryRow.Add(primary);
            primaryRow.style.display = DisplayStyle.None;
            tabSheet.Add(primaryRow);
            list = new RowList(UiKit.List(), Run, OpenCard);
            list.View.AddToClassList("vc-tabsheet__list");
            tabSheet.Add(list.View);
            SetSheetFull(SheetFull);
        }

        void BuildTabBar()
        {
            tabBar = new VisualElement { name = "TabBar" };
            tabBar.AddToClassList("vc-tabbar");
            safe.Add(tabBar);
            tabButtons.Clear();
            for (int i = 0; i < Tabs.Ids.Length; i++)
            {
                string id = Tabs.Ids[i];
                var b = new Button { name = "tab." + id, focusable = false };
                b.AddToClassList("vc-tab");
                if (!UiKit.Low)
                {
                    var img = new Image { image = Icons.Get(Tabs.Icons[i]), pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
                    img.AddToClassList("vc-tab__icon");
                    b.Add(img);
                }
                var l = UiKit.Text(loc.T("tab." + id), "vc-tab__label");
                b.Add(l);
                b.clicked += () => { if (Gate("tab." + id)) SelectTab(id); };
                tabBar.Add(b);
                tabButtons[id] = b;
            }
        }

        // =========================================================================================== state

        public void SetSheetFull(bool full)
        {
            SheetFull = full;
            tabSheet.EnableInClassList("vc-tabsheet--full", full);
        }

        public void SelectTab(string id, bool force = false)
        {
            if (!force && id == Tab) return;
            Tab = id;
            foreach (var kv in tabButtons) kv.Value.EnableInClassList("vc-tab--on", kv.Key == id);
            list.Clear();
            acc = 99f; // render now
            RefreshTab(true);
            list.View.scrollOffset = Vector2.zero;
        }

        public bool PagerOpen => pager != null;
        public bool WindowOpen => Sheets.IsOpen || (pager != null && pager.Root.parent != null);

        // =========================================================================================== input

        /// <summary>Coach gate for one physical tap on target: true = it may act; false = it was a wrong tap.</summary>
        public bool Gate(string target) => coach.Tap(target);

        void OnWrongTap()
        {
            coach.WrongTap();
        }

        /// <summary>A pill / primary button: coach gate, run, follow the result.</summary>
        public bool Run(RowAction a)
        {
            if (a == null || !a.Enabled) return false;
            if (a.Id == "speed_next" && coach.Active)
            {
                // the guided first shift runs at 1x: a speed tap answers instead of doing nothing (Patron #173)
                PulseSpeed();
                UiToastHost.Show(loc.T("hud.speed_guide"), ToastKind.Info);
                return false;
            }
            if (!Gate(a.Target)) return false;
            var r = actions.Run(a.Id);
            if (a.Id == "speed_next" && r.Ok) { UiKit.SetText(speed, Hud.Build(sim(), loc).Speed); PulseSpeed(); }
            Follow(r);
            acc = 99f; // reflect the change right away
            ActionRan?.Invoke(a.Id, r);
            return r.Ok;
        }

        void PulseSpeed()
        {
            speed.AddToClassList("vc-speed--pulse");
            speed.schedule.Execute(() => speed.RemoveFromClassList("vc-speed--pulse")).StartingIn(160);
        }

        /// <summary>Every UI-run action with its result (sound cues, analytics).</summary>
        public event Action<string, ActionResult> ActionRan;

        void OpenCard(string open, string target)
        {
            if (!Gate(target)) return;
            var r = actions.Run(open);
            Follow(r);
            acc = 99f; // e.g. the shop's policy strip opens on this very tap
            ActionRan?.Invoke(open, r);
        }

        /// <summary>Scrolls the tab list to a row / pill by its target id (the shop opened on a product).</summary>
        public void ScrollTo(string target)
        {
            RefreshTab(true);
            if (list.Targets.TryGetValue(target, out var el)) list.View.schedule.Execute(() => list.View.ScrollTo(el)).StartingIn(50);
        }

        /// <summary>The control-room UI theme of the equipped look ("" = default palette; Looks.RoomClass).</summary>
        public void SetTheme(string cls)
        {
            if (cls == theme) return;
            if (theme != "") Root.RemoveFromClassList(theme);
            theme = cls ?? "";
            if (theme != "") Root.AddToClassList(theme);
        }

        /// <summary>World tap already resolved (fault / plant / district) — same gate + actions as the UI.</summary>
        public void WorldPick(Picker.Hit hit)
        {
            if (hit == null || hit.Kind == "") return;
            string target = hit.Kind == "fault" ? "world.fault" : "world." + hit.Kind;
            if (!Gate(target)) return;
            if (hit.Kind == "fault") Follow(actions.Run("select:" + hit.Id));
            else if (hit.Kind == "plant") Follow(actions.Run("card:plant:" + hit.Id));
            else Follow(actions.Run("card:district:" + hit.Id));
        }

        public void Follow(ActionResult r)
        {
            if (r == null) return;
            if (!string.IsNullOrEmpty(r.Toast)) UiToastHost.Show(r.Toast, r.ToastKind == "success" ? ToastKind.Success : r.ToastKind == "warning" ? ToastKind.Warning : r.ToastKind == "danger" ? ToastKind.Danger : ToastKind.Info);
            if (!r.Ok && !string.IsNullOrEmpty(r.Reason) && r.Reason != "unknown")
            {
                string key = "fail." + r.Reason;
                if (loc.Has(key)) UiToastHost.Show(loc.T(key), ToastKind.Warning);
            }
            if (!string.IsNullOrEmpty(r.Open)) OpenView(r.Open);
        }

        /// <summary>Routes an Open id: report, needle, forecast, goals, settings, close, tab:&lt;id&gt;, shop[:product],
        /// card:fault|district|plant:&lt;id&gt;, restore_purchases.</summary>
        public void OpenView(string open)
        {
            var s = sim();
            string close = loc.T("sheet.close");
            if (open == "close") { Sheets.Close(); return; }
            if (open == "needle") { ToggleExplainer(); return; }
            if (open.StartsWith("tab:")) { Sheets.Close(); SelectTab(open.Substring(4)); return; }
            if (open == "shop" || open.StartsWith("shop:")) { Sheets.Close(); SelectTab("shop"); Navigate?.Invoke(open); return; }
            switch (open)
            {
                case "report": Sheets.Open("report", () => Cards.Report(sim(), loc, Extras), Run, OpenCard, close); return;
                case "forecast": Sheets.Open("forecast", () => Cards.Forecast(sim(), loc), Run, OpenCard, close); return;
                case "goals": Sheets.Open("goals", () => Goals.Sheet(sim(), loc), Run, OpenCard, close); return;
                case "settings":
                case "restore_purchases":
                    Navigate?.Invoke(open);
                    return;
            }
            var p = open.Split(':');
            if (p.Length == 3 && p[0] == "card")
            {
                string id = p[2];
                switch (p[1])
                {
                    case "fault":
                        if (int.TryParse(id, out int fid))
                            Sheets.Open(open, () => Cards.FaultCard(fid, sim(), loc, actions.Dispatch, Extras), Run, OpenCard, close);
                        return;
                    case "district": Sheets.Open(open, () => Cards.District(id, sim(), loc), Run, OpenCard, close); return;
                    case "plant": Sheets.Open(open, () => Cards.Plant(id, sim(), loc), Run, OpenCard, close); return;
                    case "product":
                        if (Extras?.Shop != null) Sheets.Open(open, () => Extras.Shop.ProductCard(id, loc), Run, OpenCard, close);
                        return;
                }
            }
            if (s != null) Debug.Log("[VoltCity] unhandled open: " + open);
        }

        public void OpenTabView(string id, Func<TabView> src) => Sheets.Open(id, src, Run, OpenCard, loc.T("sheet.close"));

        // the needle's explainer card closes by itself (device pass 10-28: it stayed after Skip guide, for the whole
        // session): 8 s after it opened, or at once when the guide is skipped / finished (HideExplainer)
        public const float ExplainerS = 8f;
        float explainerLeft;
        public bool ExplainerOpen => explainer.style.display == DisplayStyle.Flex;

        void ToggleExplainer()
        {
            bool show = explainer.style.display == DisplayStyle.None;
            explainer.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            explainerLeft = ExplainerS;
            if (show) UiKit.SetText(explainerText, Hud.Explainer(sim(), loc));
        }

        public void HideExplainer() => explainer.style.display = DisplayStyle.None;

        // =========================================================================================== pager

        public void ShowPager(Texture2D fallbackHero, Action done)
        {
            UiToastHost.ClearAll();   // nothing from before stays over the onboarding pager (toasts raised while it is open are held)
            pager = new Pager(loc, fallbackHero, () =>
            {
                pager.Root.RemoveFromHierarchy();
                pager = null;
                done();
            });
            Root.Add(pager.Root);
        }

        // =========================================================================================== per frame

        string StepText() => loc.F("coach.step_of", "n", coach.Step, "total", Coach.Steps);

        /// <summary>Called every frame; label / list work happens at <= 4 Hz.</summary>
        public void Tick(float dt, Func<int, (bool ok, Rect panelRect)> faultRect)
        {
            Sheets.Tick(dt);
            // a modal sheet (card, report) is open over the tab sheet: one drag handle on screen, not two stacked (#174)
            var hv = Sheets.IsOpen ? Visibility.Hidden : Visibility.Visible;
            if (sheetHandle.style.visibility != hv) sheetHandle.style.visibility = hv;
            UpdateCoach(faultRect);
            if (ExplainerOpen && !(coach.Active && coach.Showing && coach.Target == "hud.needle"))
            {
                explainerLeft -= dt;
                if (explainerLeft <= 0f) HideExplainer();
            }
            acc += dt;
            if (acc < RefreshS) return;
            acc = 0f;
            var s = sim();
            var v = Hud.Build(s, loc);
            UiKit.SetText(cash, v.Cash);
            UiKit.SetText(rating, v.Rating);
            UiKit.SetText(clock, v.Clock);
            UiKit.SetText(phase, v.Phase);
            phase.style.display = string.IsNullOrEmpty(v.Phase) ? DisplayStyle.None : DisplayStyle.Flex;
            UiKit.SetText(speed, v.Speed);
            speed.style.display = v.Live ? DisplayStyle.Flex : DisplayStyle.None;
            UiKit.SetText(goal, v.Goal);
            needle.Set(v);
            if (explainer.style.display == DisplayStyle.Flex) UiKit.SetText(explainerText, Hud.Explainer(s, loc));
            actions.Dispatch.Validate(s);
            RefreshTab(false);
        }

        void RefreshTab(bool force)
        {
            var tv = Tabs.Build(Tab, sim(), loc, actions.Dispatch, Extras);
            header.SetTitle(tv.Title);
            header.SetSub(tv.Sub);
            if (tv.Primary != null)
            {
                primaryAct = tv.Primary;
                primaryRow.style.display = DisplayStyle.Flex;
                UiKit.SetText(primary, tv.Primary.Label);
                primary.SetEnabled(tv.Primary.Enabled);
                primary.name = tv.Primary.Target;
            }
            else
            {
                primaryAct = null;
                primaryRow.style.display = DisplayStyle.None;
            }
            list.Render(tv, Tab == "crews" ? loc.T("crews.none.t") : null, Tab == "crews" ? loc.T("crews.none.b") : null);
        }

        void ReportBand()
        {
            float H = PanelH;
            if (H <= 0 || float.IsNaN(H)) return;
            float top = hud.worldBound.yMax, bottom = tabSheet.worldBound.yMin;
            if (float.IsNaN(top) || float.IsNaN(bottom) || bottom <= top) return;
            var r = new Rect(0f, 1f - bottom / H, 1f, (bottom - top) / H);
            if (r == lastBand) return;
            lastBand = r;
            DioramaBand?.Invoke(r);
        }

        /// <summary>Finds the element of a coach target (HUD, tabs, tab list, open sheet).</summary>
        VisualElement FindTarget(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;
            if (Sheets.IsOpen && Sheets.Targets.TryGetValue(target, out var se)) return se;
            if (list.Targets.TryGetValue(target, out var le)) return le;
            if (primaryAct != null && primary.name == target && primaryRow.style.display == DisplayStyle.Flex) return primary;
            return Root.Q(target);
        }

        void UpdateCoach(Func<int, (bool ok, Rect panelRect)> faultRect)
        {
            bool any = coach.Spot || (coach.AssistOn && coach.AssistSoft);
            if (!any || pager != null)
            {
                if (CoachUi.Visible) CoachUi.Hide();
                return;
            }
            bool soft = !coach.Spot;
            if (!soft)
            {
                // the step's tab is opened for the player (one action per step: the action, not the navigation)
                if (coach.Tab != "" && Tab != coach.Tab) SelectTab(coach.Tab);
                if (coach.Showing && coach.Step != 4 && coach.Step != 5 && Sheets.IsOpen) Sheets.Close();   // 4 = fault card, 5 = report
                if (!coach.Showing && Sheets.IsOpen) Sheets.Close();          // a balance assist points at the Grid tab
                if (coach.Showing && coach.Step == 5 && Sheets.Id != "report") OpenView("report");
            }
            else if (Tab != "grid" || Sheets.IsOpen) { if (CoachUi.Visible) CoachUi.Hide(); return; }   // soft: only where it is
            Rect hole;
            if (coach.Target == "world.fault")
            {
                var (ok, r) = faultRect(coach.FaultId);
                if (!ok) { CoachUi.Hide(); return; }
                hole = r;
            }
            else
            {
                var el = FindTarget(coach.Target);
                if (el == null || float.IsNaN(el.worldBound.width) || el.worldBound.width <= 0) { CoachUi.Hide(); return; }
                // scroll the list so the target is visible (scrolled into view, kit README)
                if (list.Targets.ContainsValue(el) && !list.Held) list.View.ScrollTo(el);
                hole = el.worldBound;
            }
            string step = coach.Showing ? StepText() : loc.T("coach.tip");
            // a target inside the open sheet (fault card Send, report Collect): the bubble goes above the sheet
            float keepAbove = -1f;
            var sp = Sheets.Panel;
            if (sp != null && sp.worldBound.Contains(hole.center)) keepAbove = sp.worldBound.yMin;
            string sentence = coach.BubbleKey == "coach.assist_up" ? loc.N("coach.assist_up_n", coach.AssistTaps)
                : coach.BubbleKey == "coach.assist_restore" ? loc.F("coach.assist_restore", "district", Names.District(loc, coach.AssistDistrict))
                : loc.T(coach.BubbleKey);
            CoachUi.Show(hole, step, sentence, coach.Flash, PanelW, PanelH, soft, keepAbove);
        }
    }
}
