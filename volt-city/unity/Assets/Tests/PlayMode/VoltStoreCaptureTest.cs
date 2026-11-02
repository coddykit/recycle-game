// Volt City — Play store raw frames for games/tools/store/fullbleed.py (owner rule 2026-10-28: full-bleed gameplay, no
// device frame). EXPLICIT: never runs in the normal PlayMode suite.
//   VOLT_STORE_DIR=/Volumes/BuildSSD/store-assets/voltcity-play-<date>-fb-raw \
//   Unity -batchmode -projectPath games/volt-city/unity -runTests -testPlatform PlayMode \
//     -testFilter VoltCity.PlayMode.Tests.VoltStoreCaptureTest -testResults /tmp/store.xml -logFile -
// Env: VOLT_STORE_DIR (required) · VOLT_STORE_LANGS (default all 10) · VOLT_STORE_SLIDES (ids, default all 8) ·
//      VOLT_STORE_SS (supersample, default 2) · VOLT_STORE_SEED (greedy career seed, default 7).
//
// Earned states, no staging: the game's own greedy bot (Policies: the balance gates' operator + buy phase, the same
// commands a player taps) plays one career from a fresh start; the career is saved at real idle moments (SaveCodec, the
// game's own save) and loaded by the real City scene, where the shift is started with the game action and fast-forwarded
// by the same operator to the moment of the slide (sim steps are identical to live play). Slide 3 adds three player taps
// (ramp, gas start, battery discharge), slide 5 the real hook-up action. Free game only: no purchase, no perk, the free
// 1-shift forecast; Medium tier (store/SCREENSHOTS.md); house banner gate closed and verified per frame (height 0, no
// HouseBanner element, no house / rewarded ad on screen), toast layer hidden, debug overlays none.
// Frames: the camera renders once per slide x device kind into a render texture (world frozen: timeScale 0, the scene
// driver paused), then the UI Toolkit panel is rebuilt in each language and drawn over it in the SAME render texture (the
// city is frozen, so every language shows the same pixels of it). Sizes = the full-bleed game areas (band 12 %): phone 1080 x 1690, tablet 1600 x 2254, x supersample.
// Output: <dir>/<locale>/{phone,tablet}/<id>.png, <dir>/feature/<id>.png (world only, 1024 x 500 x ss), <dir>/scenes.json.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using CoddyKit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VoltCity.Game;
using VoltCity.Sim;
using VoltCity.UI;

namespace VoltCity.PlayMode.Tests
{
    [Explicit("store frame capture tool, run with -testFilter")]
    public class VoltStoreCaptureTest
    {
        static readonly string[] Langs = { "en", "tr", "de", "es", "fr", "pt", "ru", "ja", "ko", "hi" };
        static readonly Dictionary<string, string> PlayLocale = new Dictionary<string, string>
        {
            ["en"] = "en-US", ["tr"] = "tr-TR", ["de"] = "de-DE", ["es"] = "es-ES", ["fr"] = "fr-FR", ["pt"] = "pt-BR",
            ["ru"] = "ru-RU", ["ja"] = "ja-JP", ["ko"] = "ko-KR", ["hi"] = "hi-IN"
        };

        sealed class Kind
        {
            public string Id; public int W, H; public Insets Inset;
        }

        // game area of the full-bleed slide (fullbleed.py: band = round(0.12 H)); tablet height covers tablet-7 (1690 * 1600/1200)
        static readonly Kind[] Kinds =
        {
            new Kind { Id = "phone", W = 1080, H = 1690, Inset = new Insets(0, 84, 0, 36) },
            new Kind { Id = "tablet", W = 1600, H = 2254, Inset = new Insets(0, 64, 0, 32) },
        };

        sealed class Slide
        {
            public string Id, Snap, Tab = "grid", View = "";
            public int Step = -1;          // live: fast-forward to this sim step (-1 = stay idle, -2 = play to the report)
            public float Settle = 1f;      // real seconds of normal 1x play before the freeze
            public string[] Taps = new string[0];
            public string Hookup = "", Scroll = "";
            public bool Feature, Storm, Full;
            public string[] Focus = new string[0];   // districts the camera frames (a player's pinch + pan); empty = whole city
        }

        // seed 7 greedy career (probe 2026-10-28): 55 = Stadium Row affordable · "buildable" = first late idle moment with a wind / solar upgrade affordable (before the buy phase) · 85 thunderstorm, Stadium Row's line down
        // 18:00, a crew on its way · 91 sunny, 7 districts, 3 stars · 92 heatwave + storm next · 96 festival + plant trip, the
        // greedy operator sheds Stadium Row 20:51-21:55 · 97 match night, half-time surge 20:30
        static readonly Slide[] Slides =
        {
            new Slide { Id = "01-peak", Snap = "pre91", Step = 516, Tab = "city", Feature = true, Focus = new[] { "riverside", "market_square", "tech_park", "stadium_row" } },
            new Slide { Id = "02-storm", Snap = "pre85", Step = 484, Tab = "crews", Settle = 0.15f, Feature = true, Storm = true, Focus = new[] { "old_harbor", "riverside", "stadium_row" } },
            new Slide { Id = "03-balance", Snap = "pre97", Step = 530,   /* ~19:20 dusk (Patron #174: dusk, not night) */ Tab = "grid", Settle = 0.8f, Taps = new[] { "ramp:steam:+1", "power:gas:on", "bat:discharge" } },
            new Slide { Id = "04-forecast", Snap = "pre92", Tab = "city", View = "forecast", Settle = 0.5f },
            new Slide { Id = "05-ripple", Snap = "post55", Tab = "city", Settle = 0.27f, Hookup = "stadium_row", Feature = true, Focus = new[] { "stadium_row", "riverside" } },
            new Slide { Id = "06-build", Snap = "buildable", Tab = "build", Settle = 0.5f },
            new Slide { Id = "07-shed", Snap = "pre96", Step = 536,   /* ~19:30 dusk */ Tab = "city", Settle = 0.4f, Full = true, Scroll = "city.hospital_hill", Taps = new[] { "shed:stadium_row" }, Feature = true, Focus = new[] { "stadium_row", "riverside" } },
            new Slide { Id = "08-report", Snap = "pre91", Step = -2, View = "report", Settle = 0.5f },
        };

        string outDir, dataDir, backupDir;
        int ss;
        readonly List<string> errors = new List<string>();
        readonly StringBuilder scenes = new StringBuilder();
        PanelSettings panelSettings;
        RenderTexture oldUiTarget, rtWorld, rtUi;

        void OnLog(string c, string s, LogType t)
        {
            if (c != null && c.StartsWith("External Gfx Allocation")) return;
            if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(t + ": " + c);
        }

        static string Env(string k, string d) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : v; }

        [SetUp]
        public void SetUp()
        {
            outDir = Env("VOLT_STORE_DIR", "");
            if (outDir == "") Assert.Ignore("VOLT_STORE_DIR not set");
            Directory.CreateDirectory(outDir);
            ss = Math.Max(1, int.Parse(Env("VOLT_STORE_SS", "2")));
            Application.logMessageReceived += OnLog;
            LogAssert.ignoreFailingMessages = true;
            dataDir = Path.Combine(Application.persistentDataPath, "voltcity");
            backupDir = dataDir + ".store-backup";
            if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
            if (Directory.Exists(dataDir)) Directory.Move(dataDir, backupDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (outDir == "") return;
            Time.timeScale = 1f;
            Application.logMessageReceived -= OnLog;
            if (panelSettings != null) panelSettings.targetTexture = oldUiTarget;
            SafeArea.StopSimulating();
            File.WriteAllText(Path.Combine(outDir, "scenes.json"), "[\n" + scenes.ToString().TrimEnd(',', '\n') + "\n]\n");
            if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
            if (Directory.Exists(backupDir)) Directory.Move(backupDir, dataDir);
        }

        // =========================================================================================== career (headless)

        static GameData Data() => GameData.FromTexts(n => Resources.Load<TextAsset>("data/" + n)?.text);

        /// <summary>One greedy shift exactly like Policies.PlayShift, stopping at a step (or the end).</summary>
        static void PlayTo(Simulation sim, int step)
        {
            var q = Policies.OperatorFor("greedy");
            if (sim.S.Auto.Contains("baseload") && (!sim.S.AutoOn.TryGetValue("baseload", out bool b) || b)) q.Setpoints = false;
            if (sim.S.Auto.Contains("crew") && (!sim.S.AutoOn.TryGetValue("crew", out bool c) || c)) q.Crew = false;
            int guard = 0;
            while (sim.Phase == "live" && (step < 0 || sim.Grid.Step < step) && guard++ < 5000)
            {
                if (sim.Grid.Step % 4 == 0)
                    foreach (var cmd in Dispatcher.Operate(sim, q)) sim.Queue(cmd);
                sim.RunSteps(1);
            }
        }

        /// <summary>The greedy career (Policies.PlayDay model: 6 shifts a day, buy after each, night crew, morning buy),
        /// saved at the idle moments the slides start from.</summary>
        static Dictionary<string, string> Career(GameData gd, long seed)
        {
            var snaps = new Dictionary<string, string>();
            var sim = Policies.NewCareer(gd, "greedy", seed);
            long now = 1800000000L;
            for (int k = 0; k < 97; k++)
            {
                if (k == 85 || k == 91 || k == 92 || k == 96 || k == 97) snaps["pre" + k] = Snap(sim);
                sim.TutorialSafe = sim.S.ShiftNo == 0;
                sim.StartShift();
                PlayTo(sim, -1);
                sim.DrainEvents();
                sim.Collect();
                if (k == 55) snaps["post55"] = Snap(sim);
                // the Build slide: the first late idle moment where a wind / solar upgrade is affordable (before buying)
                if (k >= 60 && !snaps.ContainsKey("buildable"))
                    foreach (var x in gd.Investments)
                        if ((x.Target == "wind" || x.Target == "solar") && !sim.S.Owned.ContainsKey(x.Id) && Economy.WhyNot(sim, x.Id) == "") { snaps["buildable"] = Snap(sim); break; }
                Policies.BuyPhase(sim, "greedy");
                if ((k + 1) % 6 == 0)
                {
                    sim.S.LastSeen = now;
                    sim.Offline(now + 84600);
                    now += 86400;
                    Policies.BuyPhase(sim, "greedy");
                }
                sim.DrainEvents();
            }
            if (!snaps.ContainsKey("pre97")) snaps["pre97"] = Snap(sim);
            return snaps;
        }

        static string Snap(Simulation sim)
        {
            long keep = sim.S.LastSeen;
            sim.S.LastSeen = GameSession.UnixNow();   // loaded "now": no night-crew pay on resume
            string t = SaveCodec.Encode(sim);
            sim.S.LastSeen = keep;
            return t;
        }

        // =========================================================================================== scene

        static IEnumerator WaitFor(Func<bool> cond, float timeoutS, string what)
        {
            float t = 0f;
            while (!cond())
            {
                t += Time.unscaledDeltaTime;
                if (t > timeoutS) Assert.Fail("timeout: " + what);
                yield return null;
            }
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator Boot(string career, string lang, Action<VoltCityMain> got)
        {
            Time.timeScale = 1f;
            if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, GameSession.SaveFile), career);
            var gs = GameSettings.Load(dataDir);
            gs.Lang = lang;
            gs.IntroDone = true;
            gs.CoachDone = true;
            gs.GraphicsAsked = true;
            gs.SpeedDefault = 1;
            gs.Music = false;
            gs.Effects = false;
            foreach (var h in new[] { "needle", "crews", "shed", "forecast", "build", "speed", "battery", "dr", "shop", "goal" }) gs.MarkHint(h);
            gs.Save();
            SceneManager.LoadScene("City");
            yield return null;
            VoltCityMain main = null;
            yield return WaitFor(() => (main = UnityEngine.Object.FindFirstObjectByType<VoltCityMain>()) != null && main.Ready, 60f, "City ready");
            var kit = CoddyKitHost.Instance;
            kit.Config.BannerAllowed = () => false;   // store frames never show the house banner (fleet capture policy)
            yield return Frames(2);
            got(main);
        }

        void Hook(VoltCityMain main, Kind k)
        {
            int w = k.W * ss, h = k.H * ss;
            var i = k.Inset;
            SafeArea.Simulate(w, h, new Insets(i.Left * ss, i.Top * ss, i.Right * ss, i.Bottom * ss));
            if (rtWorld != null) { rtWorld.Release(); UnityEngine.Object.Destroy(rtWorld); }
            if (rtUi != null) { rtUi.Release(); UnityEngine.Object.Destroy(rtUi); }
            rtWorld = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rtUi = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            panelSettings = main.uiDocument.panelSettings;
            if (oldUiTarget == null) oldUiTarget = panelSettings.targetTexture;
            // camera and UI render into ONE texture, the panel over the camera's frame as on screen (10-29: compositing two
            // textures on the CPU with a premultiplied guess washed the translucent HUD phase chip out to a white pill)
            panelSettings.targetTexture = rtWorld;
            panelSettings.clearColor = false;
            main.world.cam.targetTexture = rtWorld;
        }

        static void HideToasts()
        {
            var l = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (var d in l)
            {
                var top = d.rootVisualElement?.panel?.visualTree;
                if (top == null) continue;
                top.Query<VisualElement>(name: "ToastLayer").ForEach(e => e.style.display = DisplayStyle.None);
            }
        }

        static Func<int, (bool, Rect)> FaultRect(VoltCityMain main)
        {
            var m = typeof(VoltCityMain).GetMethod("FaultRect", BindingFlags.NonPublic | BindingFlags.Instance);
            return id => ((bool, Rect))m.Invoke(main, new object[] { id });
        }

        static void SetLang(VoltCityMain main, string lang)
        {
            if (main.Loc.Lang == lang) { main.Screen.Build(); return; }
            typeof(VoltCityMain).GetMethod("ChangeLanguage", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(main, new object[] { lang });
        }

        static void Arrange(VoltCityMain main, Slide s)
        {
            main.Screen.Sheets.Close();
            main.Screen.SelectTab(s.Tab, true);
            main.Screen.SetSheetFull(s.Full);
            if (s.View != "") main.Screen.OpenView(s.View);
            if (s.Scroll != "") main.Screen.ScrollTo(s.Scroll);
            var r = main.uiDocument.rootVisualElement;
            var ex = r.Q("NeedleExplainer");
            if (ex != null) ex.style.display = DisplayStyle.None;
            main.Screen.Tick(1f, FaultRect(main));
        }

        static List<Bounds> FocusBoxes(VoltCityMain main, Slide s)
        {
            if (s.Focus.Length == 0) return main.world.LitBoxes();
            var o = new List<Bounds>();
            foreach (var id in s.Focus)
            {
                var d = main.world.Layout.DistrictById[id];
                var b = new Bounds();
                b.SetMinMax(new Vector3((float)d.Min.X, (float)d.Min.Y, (float)d.Min.Z), new Vector3((float)d.Max.X, (float)d.Max.Y, (float)d.Max.Z));
                o.Add(b);
            }
            return o;
        }

        static Texture2D Read(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            return t;
        }

        /// <summary>Banner / ad / toast policy for this frame; violations fail the run (and the frame is not written).</summary>
        List<string> Verify(VoltCityMain main)
        {
            var bad = new List<string>();
            var kit = CoddyKitHost.Instance;
            if (kit.Ads != null && kit.Ads.BannerHeight > 0f) bad.Add("banner height " + kit.Ads.BannerHeight);
            if (kit.Ads != null && kit.Ads.Showing) bad.Add("ad showing");
            var top = main.uiDocument.rootVisualElement.panel.visualTree;
            top.Query<VisualElement>().ForEach(e =>
            {
                if (e.resolvedStyle.display == DisplayStyle.None || e.worldBound.height < 1f) return;
                string n = e.name ?? "";
                if (n == "HouseBanner" || e.ClassListContains("coddy-banner")) bad.Add("house banner element visible");
                if (n.StartsWith("reward.")) bad.Add("rewarded ad offer visible: " + n);
                if (n == "ToastLayer" || e.ClassListContains("coddy-toast")) bad.Add("toast visible");
                if (e.ClassListContains("coddy-interstitial")) bad.Add("interstitial visible");
            });
            return bad;
        }

        IEnumerator CaptureSlide(VoltCityMain main, Slide s, string[] langs)
        {
            // freeze the city exactly here: no sim, no world animation, no toasts / cards from the scene driver
            Time.timeScale = 0f;
            main.enabled = false;
            HideToasts();
            var sim = main.Session.Sim;
            var g = sim.Grid;
            var lit = new List<string>();
            var dark = new List<string>();
            if (g != null) for (int i = 0; i < g.DIds.Length; i++) if (g.DHooked[i]) (g.IsLit(i) ? lit : dark).Add(g.DIds[i]);
            var faults = g != null ? g.OpenFaults().Count : 0;
            int trav = 0;
            if (g != null) foreach (var c in g.Crews) if (c.State == CrewTruck.Travel) trav++;
            foreach (var k in Kinds)
            {
                Hook(main, k);
                SetLang(main, langs[0]);
                Arrange(main, s);
                yield return Frames(4);
                main.world.rig.Refit(FocusBoxes(main, s), s.Focus.Length > 0);
                yield return Frames(4);
                var world = Read(rtWorld);
                // behind a MODAL sheet (forecast, report) the shared render texture comes back as a flat scrim colour
                // (probe 10-29, q174/scrim: the camera alone draws the city, the black appears only with the UI in the same
                // RT, scrim alpha 0 or not). On a device the UI is composited over the screen: the city, dimmed by the
                // scrim's real alpha. So: the camera's own frame, and every flat scrim pixel = camera x (1 - a) + scrim x a.
                Color32[] camOnly = null;
                if (s.View != "")
                {
                    var keepUi = panelSettings.targetTexture;
                    panelSettings.targetTexture = null;
                    yield return Frames(3);
                    var co = Read(rtWorld);
                    camOnly = co.GetPixels32();
                    UnityEngine.Object.Destroy(co);
                    panelSettings.targetTexture = keepUi;
                    yield return Frames(3);
                }
                foreach (var lang in langs)
                {
                    SetLang(main, lang);
                    Arrange(main, s);
                    HideToasts();
                    yield return Frames(4);
                    if (s.Scroll != "") { float tw = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - tw < 0.2f) yield return null; }
                    var bad = Verify(main);
                    if (bad.Count > 0) { errors.Add(s.Id + " " + k.Id + " " + lang + ": " + string.Join("; ", bad)); continue; }
                    var ui = Read(rtWorld);   // the frozen city + this language's UI, one render
                    if (camOnly != null)
                    {
                        var px = ui.GetPixels32();
                        const float A = 0.42f;    // .coddy-scrim in VoltCity.uss
                        var sc = new Color32(4, 10, 15, 255);
                        int fixedPx = 0;
                        for (int i = 0; i < px.Length; i++)
                        {
                            var p = px[i];
                            if (Mathf.Abs(p.r - sc.r) <= 2 && Mathf.Abs(p.g - sc.g) <= 2 && Mathf.Abs(p.b - sc.b) <= 2)
                            {
                                var c = camOnly[i];
                                px[i] = new Color32((byte)(c.r * (1 - A) + sc.r * A), (byte)(c.g * (1 - A) + sc.g * A), (byte)(c.b * (1 - A) + sc.b * A), 255);
                                fixedPx++;
                            }
                        }
                        ui.SetPixels32(px);
                        ui.Apply();
                        Debug.Log($"[VoltStore] {s.Id} {k.Id} {lang}: {fixedPx} scrim pixels composited over the camera frame");
                    }
                    string dir = Path.Combine(outDir, PlayLocale[lang], k.Id);
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, s.Id + ".png"), ui.EncodeToPNG());
                    UnityEngine.Object.Destroy(ui);
                }
                UnityEngine.Object.Destroy(world);
            }
            if (s.Feature)
            {
                // feature graphic candidate: the city alone, 1024 x 500, framed on the lit districts
                int w = 1024 * ss, h = 500 * ss;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                main.world.cam.targetTexture = rt;
                var keep = main.world.rig.viewport;
                main.world.rig.SetViewport(new Rect(0f, 0f, 1f, 1f));
                main.world.rig.Refit(main.world.LitBoxes(), false);
                yield return Frames(4);
                var t = Read(rt);
                Directory.CreateDirectory(Path.Combine(outDir, "feature"));
                File.WriteAllBytes(Path.Combine(outDir, "feature", s.Id + ".png"), t.EncodeToPNG());
                UnityEngine.Object.Destroy(t);
                main.world.rig.SetViewport(keep);
                main.world.cam.targetTexture = rtWorld;
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }
            string hour = g != null ? g.HourOfDay.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "null";
            scenes.Append("  {\"id\":\"" + s.Id + "\",\"shift\":" + sim.S.ShiftNo + ",\"phase\":\"" + sim.Phase + "\",\"step\":" + (g != null ? g.Step : -1) +
                          ",\"hour\":" + hour + ",\"chapter\":" + sim.S.Chapter + ",\"cash\":" + sim.S.Cash + ",\"rating_x100\":" + sim.S.RatingX100 +
                          ",\"weather\":\"" + (sim.S.Plan != null ? sim.S.Plan.Weather : "") + "\",\"lit\":\"" + string.Join(" ", lit) + "\",\"dark\":\"" + string.Join(" ", dark) +
                          "\",\"open_faults\":" + faults + ",\"crews_travelling\":" + trav + ",\"stars_last\":" + (sim.S.Report != null ? sim.S.Report.Stars : 0) +
                          ",\"owned\":" + sim.S.Owned.Count + ",\"tier\":\"" + CoddyKitHost.Instance.Tiers.Current + "\",\"tab\":\"" + s.Tab + "\",\"view\":\"" + s.View +
                          "\",\"taps\":\"" + string.Join(" ", s.Taps) + "\",\"hookup\":\"" + s.Hookup + "\",\"purchases\":\"none\",\"banner\":\"gate closed, height 0, no HouseBanner\",\"toasts\":\"hidden\"},\n");
            main.enabled = true;
            Time.timeScale = 1f;
        }

        [UnityTest, Timeout(int.MaxValue)]
        public IEnumerator Capture()
        {
            var langs = Env("VOLT_STORE_LANGS", string.Join(",", Langs)).Split(',');
            var only = new HashSet<string>(Env("VOLT_STORE_SLIDES", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            long seed = long.Parse(Env("VOLT_STORE_SEED", "7"));
            var gd = Data();
            Assert.IsTrue(gd.Ok, string.Join("; ", gd.Errors));
            var snaps = Career(gd, seed);
            // Medium tier (store/SCREENSHOTS.md), chosen before the first scene load
            VoltCityMain main = null;
            yield return Boot(snaps["pre91"], langs[0], m => main = m);
            CoddyKitHost.Instance.Tiers.Choose(Tier.Medium);
            foreach (var s in Slides)
            {
                if (only.Count > 0 && !only.Contains(s.Id)) continue;
                yield return Boot(snaps[s.Snap], langs[0], m => main = m);
                Assert.AreEqual(Tier.Medium, CoddyKitHost.Instance.Tiers.Current, "tier");
                var sim = main.Session.Sim;
                if (s.Step != -1)
                {
                    var r = main.Actions.Run("shift:start");
                    Assert.IsTrue(r.Ok, s.Id + ": shift start " + r.Reason);
                    PlayTo(sim, s.Step == -2 ? -1 : s.Step);
                    sim.DrainEvents();
                    main.world.MarkDirty();
                    if (s.Step == -2) Assert.AreEqual("report", sim.Phase, s.Id);
                }
                main.Screen.SelectTab(s.Tab, true);
                foreach (var tap in s.Taps)
                {
                    var r = main.Actions.Run(tap);
                    main.Screen.Follow(r);
                    Debug.Log("[VoltStore] " + s.Id + " tap " + tap + " -> " + r.Ok + " " + r.Reason);
                }
                if (s.Hookup != "")
                {
                    var r = main.Actions.Run("hookup:" + s.Hookup);
                    Assert.IsTrue(r.Ok, s.Id + ": hookup " + r.Reason);
                }
                // a storm reached by fast-forward: the scene driver never saw its event_start (drained), so set the
                // diorama's storm look the way VoltCityMain.OnEvent does, and catch one real lightning flash
                if (s.Storm) main.world.StormActive = true;
                float t0 = Time.time;
                yield return WaitFor(() => Time.time - t0 >= s.Settle, 30f, "settle");
                if (s.Storm)
                {
                    main.world.Lightning();
                    float t1 = Time.time;
                    yield return WaitFor(() => Time.time - t1 >= 0.07f, 5f, "flash");
                }
                yield return CaptureSlide(main, s, langs);
                Debug.Log("[VoltStore] " + s.Id + " done");
            }
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }
    }
}
