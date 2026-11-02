// Volt City P3 — PlayMode smoke of the City scene (run once the editor exists; the EditMode / .NET suites cover the logic):
//   1. a fresh career boots: data + strings load, the diorama builds, the kit is up, NO error / exception in the log;
//   2. the pager (3 pages) and the graphics popup are answered through their real buttons (NavigationSubmitEvent);
//   3. the 9-step guide runs with the newbie bot (GuideBot: only the highlighted control), the spotlight is drawn on a
//      visible element / the spark for every step, wrong taps on the dim are counted once;
//   4. every tab opens in <= 100 ms to its first row (UI.md section 4 tab probe, editor timing);
//   5. relaunch: save, reload the scene, the career continues with the same cash and shift.
// Menu: Window › General › Test Runner › PlayMode. CLI: Unity -runTests -testPlatform PlayMode -testResults playmode.xml
// The test moves the editor's persistentDataPath/voltcity aside and restores it afterwards.
// UNCOMPILED until the Unity editor is installed.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VoltCity.Game;
using VoltCity.UI;
using Debug = UnityEngine.Debug;

namespace VoltCity.PlayMode.Tests
{
    public class CitySmokeTest
    {
        readonly List<string> errors = new List<string>();
        string dataDir, backupDir;

        void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(type + ": " + condition);
        }

        [SetUp]
        public void SetUp()
        {
            errors.Clear();
            Application.logMessageReceived += OnLog;
            dataDir = Path.Combine(Application.persistentDataPath, "voltcity");
            backupDir = dataDir + ".smoke-backup";
            if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
            if (Directory.Exists(dataDir)) Directory.Move(dataDir, backupDir);
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
            if (Directory.Exists(backupDir)) Directory.Move(backupDir, dataDir);
        }

        static void Submit(VisualElement root, string name)
        {
            var b = root.Q<Button>(name);
            Assert.IsNotNull(b, "button " + name);
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = b;
                b.SendEvent(e);
            }
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

        [UnityTest]
        public IEnumerator City_FreshCareer_GuideTabsRelaunch()
        {
            SceneManager.LoadScene("City");
            VoltCityMain main = null;
            yield return WaitFor(() => (main = UnityEngine.Object.FindFirstObjectByType<VoltCityMain>()) != null && main.Ready, 30f, "City ready");
            var root = main.uiDocument.rootVisualElement;
            Assert.IsEmpty(errors, "boot: " + string.Join("\n", errors));

            // pager (3 pages) + graphics popup through their own buttons
            Assert.IsNotNull(root.Q("Pager"), "first start shows the pager");
            for (int i = 0; i < 3; i++) { Submit(root, "pager.next"); yield return null; }
            yield return WaitFor(() => root.Q<Button>("graphics.ok") != null, 5f, "graphics popup");
            Submit(root, "graphics.ok");
            yield return WaitFor(() => main.Coach.Active, 5f, "guide starts");

            // the guide with the newbie bot; a wrong tap on the dim counts once
            bool wrongTried = false;
            float t = 0f, undrawn = 0f;
            while (main.Coach.Active && t < 420f)
            {
                t += Time.unscaledDeltaTime;
                if (main.Coach.Spot)
                {
                    // a target that just appeared (e.g. the report sheet's Collect pill) has no layout until the panel's
                    // next layout pass, so the overlay waits a frame or two; it must never stay hidden
                    var coachUi = root.Q("Coach");
                    undrawn = coachUi.resolvedStyle.display == DisplayStyle.Flex ? 0f : undrawn + Time.unscaledDeltaTime;
                    Assert.Less(undrawn, 1f, "spotlight drawn for " + main.Coach.Target);
                    if (!wrongTried && main.Coach.Showing && main.Coach.Step == 1)
                    {
                        int before = main.Coach.WrongTaps;
                        main.Coach.WrongTap();      // the dim's TapGesture calls exactly this once per physical tap
                        Assert.AreEqual(before + 1, main.Coach.WrongTaps);
                        wrongTried = true;
                    }
                    string act = GuideBot.ActionFor(main.Coach);
                    if (act != "") main.Screen.Follow(main.Actions.Run(act));
                }
                else if (main.Session.Sim.Phase == "idle") main.Actions.Run("shift:start");
                else if (main.Session.Sim.Phase == "live" && main.Session.Sim.S.Speed < 3) main.Actions.Run("speed:3");
                yield return null;
            }
            Assert.IsFalse(main.Coach.Active, "guide finished in the scene");
            Assert.IsEmpty(errors, "guide: " + string.Join("\n", errors));

            // tab probe: each tab shows its first row within 100 ms
            foreach (var tab in Tabs.Ids)
            {
                var sw = Stopwatch.StartNew();
                Submit(root, "tab." + tab);
                yield return null;
                var list = root.Q<ScrollView>(className: "vc-tabsheet__list");
                Assert.Greater(list.contentContainer.childCount, 0, "tab " + tab + " has rows");
                Assert.Less(sw.ElapsedMilliseconds, 100 + 1000 * Time.unscaledDeltaTime, "tab " + tab + " open time");
            }

            // relaunch
            long cash = main.Session.Sim.S.Cash;
            int shift = main.Session.Sim.S.ShiftNo;
            main.Session.Save();
            SceneManager.LoadScene("City");
            yield return null;
            VoltCityMain again = null;
            yield return WaitFor(() => (again = UnityEngine.Object.FindFirstObjectByType<VoltCityMain>()) != null && again != main && again.Ready, 30f, "relaunch ready");
            Assert.AreEqual(shift, again.Session.Sim.S.ShiftNo);
            Assert.GreaterOrEqual(again.Session.Sim.S.Cash, cash, "offline pay can only add");
            Assert.IsEmpty(errors, "relaunch: " + string.Join("\n", errors));
            Debug.Log("[VoltCity] PLAYMODE_SMOKE_PASS");
        }
    }
}
