// Volt City — turn-based play harness for a person (or an agent) playing the real game in the editor: a fresh career,
// pager and guide included. EXPLICIT (never in the suite).
//   VOLT_PLAY_DIR=<dir> Unity -batchmode -projectPath games/volt-city/unity -runTests -testPlatform PlayMode \
//     -testFilter VoltCity.PlayMode.Tests.VoltPlaySession -testResults <dir>/play.xml
// Each turn the harness writes <dir>/turn_NNN.png (1080 x 2340, world + UI) and <dir>/state.txt (numbers, open view,
// every visible button by name + label), then waits for <dir>/cmd.txt (deleted once read). Commands, one per line:
//   tap <buttonName>          a real button press (NavigationSubmitEvent on the named Button)
//   taptext <label text>      the first visible button whose label is that text (rows' side pills have no names)
//   press <elementName>       a real touch (pointer down + up at the element's centre) on any named element — the kit's
//                             TapGesture controls (needle, cards) listen to pointer events, not to Button clicks
//   target <id>               a real touch on a row / card target by its id (the same ids the guide uses, e.g.
//                             city.hilltop, grid.steam.plus): resolved through GameScreen.FindTarget
//   pick                      tap the highlighted world fault (the coach's target / first fault on the map)
//   run <seconds>             let the game run (unscaled real time, the game's own speed)
//   quit                      end the session
// Nothing is decided for the player: no bot, no assists beyond what the game itself shows.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using CoddyKit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VoltCity.UI;

namespace VoltCity.PlayMode.Tests
{
    [Explicit("interactive play harness")]
    public class VoltPlaySession
    {
        const int W = 1080, H = 2340;
        string dir, dataDir, backupDir;
        RenderTexture rtWorld, rtUi;
        PanelSettings ps;
        RenderTexture oldUi;
        int turn;
        readonly StringBuilder log = new StringBuilder();

        [SetUp]
        public void SetUp()
        {
            dir = Environment.GetEnvironmentVariable("VOLT_PLAY_DIR") ?? "";
            if (dir == "") Assert.Ignore("VOLT_PLAY_DIR not set");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += (c, s, t) =>
            {
                if (t == LogType.Error || t == LogType.Exception) log.AppendLine("ERROR " + c);
            };
            dataDir = Path.Combine(Application.persistentDataPath, "voltcity");
            backupDir = dataDir + ".play-backup";
            if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
            if (Directory.Exists(dataDir)) Directory.Move(dataDir, backupDir);
            SafeArea.Simulate(W, H, new Insets(0, 110, 0, 126));
            rtWorld = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rtUi = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32);
        }

        [TearDown]
        public void TearDown()
        {
            if (dir == "") return;
            if (ps != null) ps.targetTexture = oldUi;
            SafeArea.StopSimulating();
            File.WriteAllText(Path.Combine(dir, "session_log.txt"), log.ToString());
            if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
            if (Directory.Exists(backupDir)) Directory.Move(backupDir, dataDir);
        }

        [UnityTest, Timeout(int.MaxValue)]
        public IEnumerator Play()
        {
            SceneManager.LoadScene("City");
            yield return null;
            VoltCityMain main = null;
            while ((main = UnityEngine.Object.FindFirstObjectByType<VoltCityMain>()) == null || !main.Ready) yield return null;
            ps = main.uiDocument.panelSettings;
            oldUi = ps.targetTexture;
            ps.targetTexture = rtWorld;   // same texture as the camera: the panel draws over it as on screen
            ps.clearColor = false;
            main.world.cam.targetTexture = rtWorld;
            yield return null;
            main.world.rig.Refit(main.world.LitBoxes(), true);
            float gameSeconds = 0f;
            while (true)
            {
                yield return Snapshot(main, gameSeconds);
                string cmdPath = Path.Combine(dir, "cmd.txt");
                float waited = 0f;
                main.Session.Sim.SetPaused(true);   // the world waits for the player's decision (turn-based harness)
                while (!File.Exists(cmdPath))
                {
                    waited += Time.unscaledDeltaTime;
                    if (waited > 900f) { log.AppendLine("TIMEOUT waiting for a command"); yield break; }
                    yield return null;
                }
                string[] lines = File.ReadAllLines(cmdPath);
                File.Delete(cmdPath);
                var root = main.uiDocument.rootVisualElement;
                foreach (var raw in lines)
                {
                    string line = raw.Trim();
                    if (line == "") continue;
                    log.AppendLine($"[{gameSeconds:F0}s] > {line}");
                    if (line == "quit") yield break;
                    if (line.StartsWith("tap ")) Press(root.panel.visualTree.Query<Button>(line.Substring(4)).ToList().FirstOrDefault(Visible), line);
                    else if (line.StartsWith("taptext ")) { string tx = line.Substring(8); Press(root.panel.visualTree.Query<Button>().ToList().FirstOrDefault(b => Visible(b) && b.text == tx), line); }
                    else if (line.StartsWith("press ") || line.StartsWith("target "))
                    {
                        VisualElement el = line.StartsWith("target ")
                            ? typeof(GameScreen).GetMethod("FindTarget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                                ?.Invoke(main.Screen, new object[] { line.Substring(7) }) as VisualElement
                            : root.panel.visualTree.Q(line.Substring(6));
                        if (el == null || !Visible(el)) log.AppendLine("  NOT FOUND: " + line);
                        else
                        {
                            var c = el.worldBound.center;
                            using (var d = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = c, clickCount = 1 })) { d.target = el; el.SendEvent(d); }
                            yield return null;
                            using (var u = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = c, clickCount = 1 })) { u.target = el; el.SendEvent(u); }
                        }
                    }
                    else if (line == "pick") { int f = main.Coach.FaultId > 0 ? main.Coach.FaultId : FirstFault(main); if (f > 0) main.Screen.WorldPick(new VoltCity.Game.Picker.Hit { Kind = "fault", Id = f.ToString() }); else log.AppendLine("  no fault on the map"); }
                    else if (line.StartsWith("run "))
                    {
                        float secs = float.Parse(line.Substring(4));
                        float t = 0f;
                        while (t < secs) { t += Time.unscaledDeltaTime; yield return null; }
                        gameSeconds += secs;
                    }
                    yield return null;
                    yield return null;
                }
            }
        }

        static int FirstFault(VoltCityMain m)
        {
            var g = m.Session.Sim.Grid;
            var open = g?.OpenFaults();
            return open != null && open.Count > 0 ? open[0].Id : 0;
        }

        void Press(Button b, string what)
        {
            if (b == null) { log.AppendLine("  NOT FOUND: " + what); return; }
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        static bool Visible(VisualElement e)
        {
            for (var p = e; p != null; p = p.parent)
                if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden || p.resolvedStyle.opacity < 0.05f) return false;
            return e.worldBound.width > 1f;
        }

        IEnumerator Snapshot(VoltCityMain main, float gameSeconds)
        {
            for (int i = 0; i < 4; i++) yield return null;
            var a = Read(rtWorld);
            File.WriteAllBytes(Path.Combine(dir, $"turn_{turn:000}.png"), a.EncodeToJPG(82));
            UnityEngine.Object.Destroy(a);
            var sim = main.Session.Sim; var s = sim.S;
            var sb = new StringBuilder();
            sb.AppendLine($"turn {turn} game_t={gameSeconds:F0}s phase={sim.Phase} shift={s.ShiftNo} cash={s.Cash} speed={s.Speed} last_stars={s.LastStars}");
            if (sim.Grid != null) sb.AppendLine($"supply={sim.Grid.LastSupply} demand={sim.Grid.LastDemand} needle_pm={sim.Grid.NeedlePm} step={sim.Grid.Step} faults={sim.Grid.OpenFaults().Count}");
            sb.AppendLine($"tab={main.Screen.Tab} window_open={main.Screen.WindowOpen} pager={main.Screen.PagerOpen} coach_active={main.Coach.Active} coach_target={main.Coach.Target} coach_step={main.Coach.Step}");
            sb.AppendLine("named touch targets: " + string.Join(" ", main.uiDocument.rootVisualElement.panel.visualTree.Query<VisualElement>().ToList()
                .Where(x => !(x is Button) && !string.IsNullOrEmpty(x.name) && x.name.Contains(".") && Visible(x)).Select(x => x.name).Distinct().Take(30)));
            sb.AppendLine("visible buttons:");
            foreach (var btn in main.uiDocument.rootVisualElement.panel.visualTree.Query<Button>().ToList().Where(Visible))
                sb.AppendLine($"  [{btn.name}] \"{btn.text}\"");
            sb.AppendLine("visible text (first 40):");
            foreach (var l in main.uiDocument.rootVisualElement.panel.visualTree.Query<Label>().ToList().Where(x => Visible(x) && !string.IsNullOrWhiteSpace(x.text)).Take(40))
                sb.AppendLine("  " + l.text.Replace("\n", " "));
            File.WriteAllText(Path.Combine(dir, "state.txt"), sb.ToString());
            File.WriteAllText(Path.Combine(dir, $"state_{turn:000}.txt"), sb.ToString());
            log.AppendLine($"turn {turn}: {sb.ToString().Split('\n')[0]}");
            turn++;
        }

        static Texture2D Read(RenderTexture rt)
        {
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply();
            RenderTexture.active = prev; return t;
        }
    }
}
