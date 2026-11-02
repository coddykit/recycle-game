// Volt City P5 — layout gate: 10 languages x 4 aspect ratios, every screen, no clipped / overflowing / off-screen text,
// and the runtime language switch re-localises what is already open (no string of the previous language left).
//   Screens: pager pages 1-3, graphics question, HUD with each of the 5 tabs (half + full sheet), Settings, Language.
//   Aspects: 9:16 (1080x1920), 9:19.5 (1080x2340), 9:21 (1080x2520), 3:4 tablet (1536x2048), phone safe insets simulated.
//   Rules per visible TextElement (Label, Button):
//     offscreen  — its box leaves the panel (> 1 px);
//     clipped    — one-line text (white-space: nowrap) wider than its box (text-overflow: ellipsis included: an ellipsis is
//                  clipping too, the gate wants every label to fit);
//     cut        — wrapping text taller than its box;
//     word-split — a single word wider than its wrapping box (UI Toolkit then breaks it mid-word).
//   Language switch: per language, after VoltCityMain.ChangeLanguage on the OPEN screen, no visible text may equal an
//   English string of the table that differs in the target language ("stale").
// Report: <persistentDataPath>/volt_layout_report.txt (+ VOLT_LAYOUT_DIR to also save one frame per aspect x language).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CoddyKit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VoltCity.Game;
using VoltCity.UI;

namespace VoltCity.PlayMode.Tests
{
    public class VoltLayoutTest
    {
        static readonly (string name, int w, int h)[] Aspects =
        {
            ("9x16", 1080, 1920), ("9x19.5", 1080, 2340), ("9x21", 1080, 2520), ("3x4", 1536, 2048),
        };

        readonly List<string> errors = new List<string>();
        readonly StringBuilder report = new StringBuilder();
        string dataDir, backupDir;
        PanelSettings panelSettings;
        RenderTexture oldTarget;

        void OnLog(string c, string s, LogType t)
        {
            if (c != null && c.StartsWith("External Gfx Allocation")) return;
            if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(t + ": " + c);
        }

        [SetUp]
        public void SetUp()
        {
            Application.logMessageReceived += OnLog;
            LogAssert.ignoreFailingMessages = true;
            dataDir = Path.Combine(Application.persistentDataPath, "voltcity");
            backupDir = dataDir + ".layout-backup";
            if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
            if (Directory.Exists(dataDir)) Directory.Move(dataDir, backupDir);
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            if (panelSettings != null) panelSettings.targetTexture = oldTarget;
            SafeArea.StopSimulating();
            File.WriteAllText(Path.Combine(Application.persistentDataPath, "volt_layout_report.txt"), report.ToString());
            if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
            if (Directory.Exists(backupDir)) Directory.Move(backupDir, dataDir);
        }

        static object Call(VoltCityMain m, string method, params object[] args)
        {
            var mi = typeof(VoltCityMain).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(mi, "VoltCityMain." + method);
            return mi.Invoke(m, args);
        }

        static void Submit(VisualElement root, string name)
        {
            var b = root.Q<Button>(name);
            if (b == null) return;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        // ------------------------------------------------------------------ checks
        static bool Visible(VisualElement e)
        {
            for (var p = e; p != null; p = p.parent)
            {
                if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden || p.resolvedStyle.opacity < 0.05f) return false;
            }
            return true;
        }

        /// <summary>The visible part of e: its box intersected with every ancestor that clips (overflow: hidden, scroll views).</summary>
        static Rect ClipOf(VisualElement e, Rect panel)
        {
            var r = panel;
            for (var p = e.parent; p != null; p = p.parent)
                if (Clips(p)) r = Intersect(r, p.worldBound);
            return r;
        }

        // overflow is not on IResolvedStyle (and computedStyle is by-ref): the clipping containers are the USS selectors
        // that set overflow: hidden on a container + the scroll views' viewport
        static readonly string[] ClipClasses = { "vc-bar", "vc-chip", "coddy-banner__texts", "coddy-interstitial__texts", "coddy-curtain__progress", "unity-scroll-view__content-viewport" };
        static bool Clips(VisualElement e)
        {
            if (e.style.overflow.keyword == StyleKeyword.Undefined && e.style.overflow.value == Overflow.Hidden) return true;
            foreach (var c in ClipClasses) if (e.ClassListContains(c)) return true;
            return false;
        }

        static Rect Intersect(Rect a, Rect b)
        {
            float x0 = Mathf.Max(a.xMin, b.xMin), y0 = Mathf.Max(a.yMin, b.yMin), x1 = Mathf.Min(a.xMax, b.xMax), y1 = Mathf.Min(a.yMax, b.yMax);
            return x1 > x0 && y1 > y0 ? Rect.MinMaxRect(x0, y0, x1, y1) : Rect.zero;
        }

        static bool InScroller(VisualElement e)
        {
            for (var p = e.parent; p != null; p = p.parent) if (p is ScrollView) return true;
            return false;
        }

        int Check(VisualElement root, string where, List<string> problems)
        {
            var panel = root.panel.visualTree.worldBound;
            int n = 0;
            root.panel.visualTree.Query<TextElement>().ForEach(t =>
            {
                string text = t.text;
                if (string.IsNullOrWhiteSpace(text) || !Visible(t)) return;
                var b = t.worldBound;
                if (b.width < 1f || b.height < 1f) return;
                n++;
                string id = $"{where} [{t.name}|{string.Join(".", t.GetClasses().Take(3))} < {string.Join(".", t.parent?.GetClasses().Take(2) ?? new string[0])} < {string.Join(".", t.parent?.parent?.GetClasses().Take(2) ?? new string[0])}] \"{Short(text)}\"";
                bool scroller = InScroller(t);
                // a row scrolled out of a list is fine; everything else must be on the panel
                if (!scroller && (b.xMin < panel.xMin - 1f || b.xMax > panel.xMax + 1f || b.yMin < panel.yMin - 1f || b.yMax > panel.yMax + 1f))
                    problems.Add("offscreen " + id);
                var cr = t.contentRect;
                bool nowrap = t.resolvedStyle.whiteSpace == WhiteSpace.NoWrap || t.resolvedStyle.whiteSpace == WhiteSpace.Pre;
                if (nowrap)
                {
                    var need = t.MeasureTextSize(text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                    if (need.x > cr.width + 2f) problems.Add($"clipped {id} need={need.x:F0} have={cr.width:F0}");
                }
                else
                {
                    var need = t.MeasureTextSize(text, cr.width + 2f, VisualElement.MeasureMode.AtMost, 0, VisualElement.MeasureMode.Undefined);   // +2 px: sub-pixel rounding re-wrapped a line that fits
                    if (need.y > cr.height + 2f) problems.Add($"cut {id} need_h={need.y:F0} have_h={cr.height:F0}");
                    // a word wider than its box breaks mid-word ("Stadi/um/Row", German tablet City list, Patron #172)
                    // break points: spaces, after a hyphen / slash; CJK text breaks between characters (no spaces in ja)
                    foreach (var word in text.Split(new[] { ' ', '\n', '\u00A0', '-', '/', '‐' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (word.Length < 2 || word.Any(ch => (ch >= 0x3040 && ch <= 0x30FF) || (ch >= 0x4E00 && ch <= 0x9FFF) || (ch >= 0xFF00 && ch <= 0xFFEF))) continue;
                        float w = t.MeasureTextSize(word, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                        if (w > cr.width + 2f) { problems.Add($"word-split {id} \"{word}\" need={w:F0} have={cr.width:F0}"); break; }
                    }
                }
                // squeezed out of a clipping parent (a row's side pill pushed past the card edge)
                if (!scroller)
                {
                    var vis = Intersect(b, ClipOf(t, panel));
                    if (vis.width < b.width - 2f || vis.height < b.height - 2f) problems.Add("hidden-part " + id);
                }
            });
            return n;
        }

        // a toast that appeared during the last frames has not had its first full layout pass: give it 5 frames and keep
        // only what is still wrong (a person never sees that frame)
        IEnumerator Settled(VisualElement root, string where, List<string> problems, Action<int> counted)
        {
            var tmp = new List<string>();
            int n = Check(root, where, tmp);
            if (tmp.Any(x => x.Contains("coddy-toast")))
            {
                yield return Frames(5);
                tmp.Clear();
                n = Check(root, where, tmp);
            }
            counted(n);
            problems.AddRange(tmp);
        }

        static string Short(string s) => s.Length > 40 ? s.Substring(0, 40) + "…" : s;

        HashSet<string> StaleSet(Loc loc, string from, string to)
        {
            // English (or previous-language) strings that differ in the target language: seeing one after the switch = stale
            var set = new HashSet<string>();
            foreach (var key in loc.Keys)
            {
                string a = loc.Cell(key, from), b = loc.Cell(key, to);
                if (!string.IsNullOrEmpty(a) && a != b && a.Length > 3 && a.IndexOf('{') < 0) set.Add(a);
            }
            return set;
        }

        int Stale(VisualElement root, HashSet<string> stale, string where, List<string> problems)
        {
            int n = 0;
            root.panel.visualTree.Query<TextElement>().ForEach(t =>
            {
                if (!Visible(t) || string.IsNullOrEmpty(t.text)) return;
                if (stale.Contains(t.text)) { problems.Add($"stale {where} \"{Short(t.text)}\""); n++; }
            });
            return n;
        }

        // ------------------------------------------------------------------ the gate
        [UnityTest, Timeout(int.MaxValue)]
        public IEnumerator TenLanguages_FourAspects_NoOverflow_AndSwitchRelocalises()
        {
            var langs = Loc.Languages;
            var problems = new List<string>();
            int checkedTexts = 0, screens = 0;
            string frameDir = Environment.GetEnvironmentVariable("VOLT_LAYOUT_DIR") ?? "";
            foreach (var asp in Aspects)
            {
                if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
                Directory.CreateDirectory(dataDir);
                var gs = GameSettings.Load(dataDir);
                gs.Lang = "en"; gs.IntroDone = true; gs.GraphicsAsked = true; gs.CoachDone = true;
                gs.Save();
                SafeArea.Simulate(asp.w, asp.h, new Insets(0, asp.w > 1300 ? 48 : 110, 0, asp.w > 1300 ? 64 : 126));
                var rt = new RenderTexture(asp.w, asp.h, 24, RenderTextureFormat.ARGB32);
                SceneManager.LoadScene("City");
                yield return null;
                VoltCityMain main = null;
                float t = 0f;
                while ((main = UnityEngine.Object.FindFirstObjectByType<VoltCityMain>()) == null || !main.Ready)
                {
                    t += Time.unscaledDeltaTime;
                    if (t > 60f) Assert.Fail("City ready " + asp.name);
                    yield return null;
                }
                panelSettings = main.uiDocument.panelSettings;
                if (panelSettings.targetTexture == null || oldTarget == null) oldTarget = panelSettings.targetTexture;
                panelSettings.targetTexture = rt;
                main.world.cam.targetTexture = rt;
                yield return Frames(4);
                var root = main.uiDocument.rootVisualElement;
                // a live shift so the HUD and tabs carry real numbers
                main.Actions.Run("shift:start");
                yield return Frames(30);

                string prev = "en";
                foreach (var lang in langs)
                {
                    var tabsOpen = Tabs.Ids;
                    // switch while a tab sheet is open: the open view must re-localise
                    Submit(root, "tab." + tabsOpen[1]);
                    yield return Frames(3);
                    if (lang != prev)
                    {
                        Call(main, "ChangeLanguage", lang);
                        yield return Frames(4);
                        var stale = StaleSet(main.Loc, prev, lang);
                        Stale(root, stale, $"{asp.name}/{lang}/after-switch/tab.{tabsOpen[1]}", problems);
                    }
                    prev = lang;
                    // tabs, half and full sheet
                    foreach (var tab in tabsOpen)
                    {
                        Submit(root, "tab." + tab);
                        yield return Frames(3);
                        yield return Settled(root, $"{asp.name}/{lang}/tab.{tab}", problems, c => checkedTexts += c); screens++;
                        Submit(root, "sheet.handle");
                        yield return Frames(3);
                        yield return Settled(root, $"{asp.name}/{lang}/tab.{tab}.full", problems, c => checkedTexts += c); screens++;
                        Submit(root, "sheet.handle");
                        yield return Frames(2);
                    }
                    // settings + language sheets
                    Call(main, "OpenSettings");
                    yield return Frames(3);
                    yield return Settled(root, $"{asp.name}/{lang}/settings", problems, c => checkedTexts += c); screens++;
                    main.Screen.Sheets.Close();
                    Call(main, "OpenLanguage");
                    yield return Frames(3);
                    yield return Settled(root, $"{asp.name}/{lang}/language", problems, c => checkedTexts += c); screens++;
                    main.Screen.Sheets.Close();
                    yield return Frames(2);
                    // pager (replayed from Settings, as a player would) + graphics question
                    Call(main, "ReplayGuide");
                    yield return Frames(3);
                    for (int pg = 1; pg <= 3; pg++)
                    {
                        yield return Settled(root, $"{asp.name}/{lang}/pager.{pg}", problems, c => checkedTexts += c); screens++;
                        if (pg == 1 && frameDir != "") yield return Save(rt, Path.Combine(frameDir, $"{asp.name}_{lang}_pager.png"));
                        if (pg < 3) Submit(root, "pager.next");
                        yield return Frames(2);
                    }
                    Submit(root, "pager.skip");
                    yield return Frames(2);
                    Call(main, "OpenGraphicsPopup", (Action)(() => { }));
                    yield return Frames(3);
                    yield return Settled(root, $"{asp.name}/{lang}/graphics", problems, c => checkedTexts += c); screens++;
                    main.Screen.Sheets.Close();
                    // the guide restarted by ReplayGuide: skip it again
                    if (main.Coach.Active) Submit(root, "coach.skip");
                    yield return Frames(3);
                    if (frameDir != "")
                    {
                        Submit(root, "tab." + tabsOpen[2]);
                        yield return Frames(3);
                        yield return Save(rt, Path.Combine(frameDir, $"{asp.name}_{lang}_hud.png"));
                    }
                }
                main.world.cam.targetTexture = null;
            }
            report.AppendLine($"LAYOUT: {Aspects.Length} aspects x {Loc.Languages.Length} languages, {screens} screens, {checkedTexts} texts checked, {problems.Count} problems, {errors.Count} errors");
            foreach (var p in problems.Distinct()) report.AppendLine("  " + p);
            foreach (var e in errors) report.AppendLine("  error " + e);
            Debug.Log("[VoltCity] " + report.ToString().Split('\n')[0]);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.IsEmpty(problems, string.Join("\n", problems.Distinct().Take(40)));
        }

        static IEnumerator Save(RenderTexture rt, string path)
        {
            yield return null;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToJPG(80));
            UnityEngine.Object.Destroy(tex);
        }
    }
}
