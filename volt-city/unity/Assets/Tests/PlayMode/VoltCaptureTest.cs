// Volt City — editor frame capture + perf probe (P5 / P7 evidence). EXPLICIT: never runs in the normal PlayMode suite.
//   Unity -batchmode -projectPath games/volt-city/unity -runTests -testPlatform PlayMode \
//     -testFilter VoltCity.PlayMode.Tests.VoltCaptureTest -testResults /tmp/cap.xml -logFile -
// Env: VOLT_CAPTURE_DIR (output, required) · VOLT_CAPTURE_TIERS ("low,medium") · VOLT_CAPTURE_LANGS ("en") ·
//      VOLT_CAPTURE_SET ("full" = pager, HUD, tabs, settings; "lang" = pager + HUD + one tab) ·
//      VOLT_SOAK_S (seconds of bot play after the frames, 0 = none; the 10-min play notes / 30-min soak).
// Frames: the camera renders into one RT, the UI Toolkit panel into a second (PanelSettings.targetTexture), composited on
// the CPU at 1080 x 2340 with a simulated phone safe area (status 110 px, nav bar 126 px). Perf lines come from the
// render ProfilerRecorders (draw calls / SetPass / triangles) — editor numbers: draw + triangle counts carry over to the
// device, frame times do not.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CoddyKit;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VoltCity.Game;
using VoltCity.UI;
using VoltCity.Art;
using Debug = UnityEngine.Debug;

namespace VoltCity.PlayMode.Tests
{
    [Explicit("frame capture tool, run with -testFilter")]
    public class VoltCaptureTest
    {
        const int W = 1080, H = 2340;
        string outDir, dataDir, backupDir;
        readonly List<string> errors = new List<string>();
        readonly StringBuilder report = new StringBuilder();
        RenderTexture rtWorld, rtUi;
        PanelSettings panelSettings;
        RenderTexture oldUiTarget;
        ProfilerRecorder draws, setpass, tris;

        void OnLog(string c, string s, LogType t)
        {
            // editor-only engine bookkeeping (render-texture readback), not game code
            if (c != null && c.StartsWith("External Gfx Allocation")) return;
            if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) errors.Add(t + ": " + c);
        }

        static string Env(string k, string d) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : v; }

        [SetUp]
        public void SetUp()
        {
            outDir = Env("VOLT_CAPTURE_DIR", "");
            if (outDir == "") Assert.Ignore("VOLT_CAPTURE_DIR not set");
            Directory.CreateDirectory(outDir);
            Application.logMessageReceived += OnLog;
            LogAssert.ignoreFailingMessages = true;   // OnLog collects the real errors (and asserts them at the end)
            dataDir = Path.Combine(Application.persistentDataPath, "voltcity");
            backupDir = dataDir + ".capture-backup";
            if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
            if (Directory.Exists(dataDir)) Directory.Move(dataDir, backupDir);
            SafeArea.Simulate(W, H, new Insets(0, 110, 0, 126));
            rtWorld = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rtUi = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32);
            draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            setpass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        [TearDown]
        public void TearDown()
        {
            if (outDir == "") return;
            Application.logMessageReceived -= OnLog;
            if (panelSettings != null) panelSettings.targetTexture = oldUiTarget;
            SafeArea.StopSimulating();
            draws.Dispose(); setpass.Dispose(); tris.Dispose();
            File.WriteAllText(Path.Combine(outDir, "capture_report.txt"), report.ToString());
            if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
            if (Directory.Exists(backupDir)) Directory.Move(backupDir, dataDir);
        }

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

        static void Submit(VisualElement root, string name)
        {
            var b = root.Q<Button>(name);
            if (b == null) return;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        IEnumerator Boot(Action<VoltCityMain> got)
        {
            SceneManager.LoadScene("City");
            yield return null;
            VoltCityMain main = null;
            yield return WaitFor(() => (main = UnityEngine.Object.FindFirstObjectByType<VoltCityMain>()) != null && main.Ready, 60f, "City ready");
            Hook(main);
            LogAssert.ignoreFailingMessages = true;   // reset by scene loads; OnLog keeps the real errors
            // the first fit used the editor window's aspect; refit for the 1080 x 2340 frame
            yield return null;
            if (main.world.rig != null) main.world.rig.Refit(main.world.LitBoxes(), true);
            got(main);
        }

        void Hook(VoltCityMain main)
        {
            panelSettings = main.uiDocument.panelSettings;
            // camera and UI render into the SAME texture (the panel draws over the camera's frame, as on screen): compositing
            // two textures on the CPU washed a translucent HUD chip out to a white pill (10-29)
            if (panelSettings.targetTexture != rtWorld)
            {
                oldUiTarget = panelSettings.targetTexture;
                panelSettings.targetTexture = rtWorld;
                panelSettings.clearColor = false;
            }
            if (main.world != null && main.world.cam != null) main.world.cam.targetTexture = rtWorld;
        }

        IEnumerator Shot(VoltCityMain main, string name)
        {
            Hook(main);
            for (int i = 0; i < 6; i++) yield return null;
            var a = Read(rtWorld);
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), a.EncodeToPNG());
            UnityEngine.Object.Destroy(a);
            report.AppendLine($"frame {name}");
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

        // world budget (what the device draws for the diorama): enabled static renderers under the art root + one
        // RenderMeshInstanced call per <= 511 instances of each prop kind. Editor-independent, comparable to perf_gate.
        static (int draws, long tris) WorldBudget(VoltCityMain main)
        {
            var art = main.world.art;
            int d = 0; long t = 0;
            foreach (var r in art.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                d += r.sharedMaterials.Length;
                t += Tris(mf.sharedMesh);
            }
            void Kind(VcInstancedProps k, Mesh m) { if (k == null || m == null || k.Count == 0) return; d += (k.Count + VcInstancedProps.MaxPerBatch - 1) / VcInstancedProps.MaxPerBatch; t += Tris(m) * k.Count; }
            Kind(art.Trucks, art.truck); Kind(art.Cars, art.car); Kind(art.Markers, art.faultMarker); Kind(art.Trees, art.tree); Kind(art.Rotors, art.windBlades);
            return (d, t);
        }

        static long Tris(Mesh m) { long n = 0; for (int i = 0; i < m.subMeshCount; i++) n += (long)m.GetIndexCount(i) / 3; return n; }

        IEnumerator Perf(VoltCityMain main, string label)
        {
            int n = 0;
            for (int i = 0; i < 30; i++) { yield return null; n++; }
            var (d, t) = WorldBudget(main);
            string line = $"PERF {label}: tier={CoddyKitHost.Instance.Tiers.Current} world_draws={d} world_tris={t} cam_far={main.world.cam.farClipPlane:F0} pipeline={QualitySettings.renderPipeline?.name} cam_pos={main.world.cam.transform.position} cam_rect={main.world.cam.rect} fog={RenderSettings.fogMode} {RenderSettings.fogStartDistance:F0}-{RenderSettings.fogEndDistance:F0} dens={RenderSettings.fogDensity}";
            var vp = main.world.rig != null ? typeof(VoltCity.World.CameraRig).GetField("viewport", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(main.world.rig) : null;
            var r = main.uiDocument.rootVisualElement;
            line += $" viewport={vp} hud={r.Q("Hud")?.worldBound} sheet={r.Q(className: "vc-tabsheet")?.worldBound} panel={r.panel?.visualTree.layout}";
            report.AppendLine(line);
            Debug.Log("[VoltCity] " + line);
        }

        // a competent player after the guide: the balance gates' "greedy" operator (setpoints, battery, crews, shedding,
        // Demand Response — only commands a player can tap) every 4 sim steps, and the greedy buy phase between shifts
        int lastOpStep = -1;
        public int Purchases;
        IEnumerator BotStep(VoltCityMain main)
        {
            var sim = main.Session.Sim;
            if (main.Coach.Active && main.Coach.Spot)
            {
                string act = GuideBot.ActionFor(main.Coach);
                if (act != "") main.Screen.Follow(main.Actions.Run(act));
            }
            else if (!main.Coach.Active && sim.Phase == "live" && sim.Grid != null && sim.Grid.Step != lastOpStep && sim.Grid.Step % 4 == 0)
            {
                lastOpStep = sim.Grid.Step;
                foreach (var c in VoltCity.Sim.Dispatcher.Operate(sim, VoltCity.Sim.Policies.OperatorFor("greedy"))) sim.Queue(c);
            }
            if (sim.Phase == "idle")
            {
                if (!main.Coach.Active) Purchases += VoltCity.Sim.Policies.BuyPhase(sim, "greedy");
                main.Actions.Run("shift:start");
            }
            else if (sim.Phase == "report") main.Actions.Run("collect");
            else if (sim.Phase == "live" && sim.S.Speed < 3) main.Actions.Run("speed:3");
            yield return null;
        }

        [UnityTest, Timeout(int.MaxValue)]
        public IEnumerator Capture()
        {
            LogAssert.ignoreFailingMessages = true;
            var tiers = Env("VOLT_CAPTURE_TIERS", "low,medium").Split(',');
            var langs = Env("VOLT_CAPTURE_LANGS", "en").Split(',');
            string set = Env("VOLT_CAPTURE_SET", "full");
            VoltCityMain main = null;
            yield return Boot(m => main = m);
            foreach (var tierName in tiers)
            foreach (var lang in langs)
            {
                var tier = tierName == "high" ? Tier.High : tierName == "medium" ? Tier.Medium : Tier.Low;
                // fresh career per (tier, language): the pager shows again
                if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
                CoddyKitHost.Instance.Tiers.Choose(tier);
                Directory.CreateDirectory(dataDir);
                var gs = GameSettings.Load(dataDir);
                gs.Lang = lang;
                gs.GraphicsAsked = true;
                gs.Save();
                yield return Boot(m => main = m);
                var root = main.uiDocument.rootVisualElement;
                string p = $"{tierName}_{lang}_";
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot(main, p + "01_pager");
                yield return Perf(main, p + "start");
                for (int i = 0; i < 3; i++) { Submit(root, "pager.next"); yield return null; }
                Submit(root, "graphics.ok");
                // guided first shift with the newbie bot, frames along the way
                float t = 0f; bool hud = false;
                while (main.Coach.Active && t < 420f)
                {
                    t += Time.unscaledDeltaTime;
                    yield return BotStep(main);
                    if (!hud && t > 8f) { hud = true; yield return Shot(main, p + "02_guide"); }
                }
                // free play until the next shift is live and busy
                t = 0f;
                while (t < 40f) { t += Time.unscaledDeltaTime; yield return BotStep(main); }
                main.Screen.Sheets.Close();
                yield return Shot(main, p + "03_hud");
                if (Env("VOLT_HERO", "") == "1" && tier != Tier.Low && lang == langs[0])
                    for (int h = 1; h <= 3; h++)
                    {
                        // onboarding hero frames: the real city from the running loop, framed for the pager (its panel
                        // covers the bottom ~30 %), world only, 1080 x 2340 JPEG
                        main.world.rig.SetViewport(new Rect(0f, 0.30f, 1f, 0.70f));
                        float w = 0f;
                        while (w < 12f) { w += Time.unscaledDeltaTime; yield return BotStep(main); }
                        // one light moment per page, matching its line: 1 "lives through its day" = day, 2 "fix what
                        // storms break" = storm (wet streets), 3 "grow a whole city" = night (every lit window)
                        var moment = h == 1 ? VcLightCurves.Day : h == 2 ? VcLightCurves.Storm : VcLightCurves.NightM;
                        main.world.StormActive = h == 2;
                        main.world.lights.Apply(moment);
                        for (int i = 0; i < 6; i++) { main.world.lights.Apply(moment); yield return null; }
                        var hero = Read(rtWorld);
                        File.WriteAllBytes(Path.Combine(outDir, "hero_" + h + ".jpg"), hero.EncodeToJPG(86));
                        UnityEngine.Object.Destroy(hero);
                    }
                yield return Perf(main, p + "late");
                if (set == "full")
                {
                    foreach (var tab in Tabs.Ids)
                    {
                        Submit(root, "tab." + tab);
                        yield return Shot(main, p + "04_tab_" + tab);
                    }
                }
                else
                {
                    Submit(root, "tab." + Tabs.Ids[0]);
                    yield return Shot(main, p + "04_tab_" + Tabs.Ids[0]);
                }
            }
            float soak = float.Parse(Env("VOLT_SOAK_S", "0"));
            if (soak > 0f) yield return Soak(main, soak);
            report.AppendLine("errors: " + errors.Count);
            foreach (var e in errors) report.AppendLine("  " + e);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        IEnumerator Soak(VoltCityMain main, float seconds)
        {
            float t = 0f, nextLog = 0f;
            long mem0 = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            int frames = 0; float worst = 0f;
            report.AppendLine($"SOAK start shift={main.Session.Sim.S.ShiftNo} cash={main.Session.Sim.S.Cash} mem={mem0 / 1048576}MB");
            while (t < seconds)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt; frames++; worst = Mathf.Max(worst, dt);
                yield return BotStep(main);
                if (t >= nextLog)
                {
                    nextLog += 60f;
                    var s = main.Session.Sim.S;
                    long mem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                    report.AppendLine($"SOAK t={t:F0}s phase={main.Session.Sim.Phase} shift={s.ShiftNo} cash={s.Cash} stars_last={(s.Report != null ? s.Report.Stars.ToString() : "-")} mem={mem / 1048576}MB gc={GC.CollectionCount(0)} worst_dt={worst * 1000:F0}ms errors={errors.Count}");
                    worst = 0f;
                }
            }
            long mem1 = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            report.AppendLine($"SOAK end {seconds:F0}s frames={frames} mem {mem0 / 1048576}MB -> {mem1 / 1048576}MB purchases={Purchases}");
        }
    }
}
