// Volt City / CoddyKit — the list "finger down" state can never stick (device pass 10-28: a pill press captured by its
// button never sent PointerUp to the list, the list stopped refreshing for the session: stale Grid rows, an empty tab).
using System.Collections;
using NUnit.Framework;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VoltCity.UI;

namespace VoltCity.PlayMode.Tests
{
    public class ListHoldTest
    {
        [UnityTest]
        public IEnumerator ListHold_ReleasedByPanelUp_AndStaleTimeout()
        {
            SceneManager.LoadScene("City");
            yield return null;
            VoltCityMain main = null;
            while ((main = Object.FindFirstObjectByType<VoltCityMain>()) == null || !main.Ready) yield return null;
            var root = main.uiDocument.rootVisualElement;
            var list = UiKit.List();
            var pill = new Button { text = "+" };
            list.Add(pill);
            root.Add(list);
            yield return null;

            // down on the pill (inside the list), up delivered to the panel root only (the button captured the pointer)
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = pill.worldBound.center })) { down.target = pill; pill.SendEvent(down); }
            Assert.IsTrue(UiKit.Held(list), "a press on a row holds the list");
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = pill.worldBound.center })) { up.target = root.panel.visualTree; root.panel.visualTree.SendEvent(up); }
            Assert.IsFalse(UiKit.Held(list), "an up anywhere on the panel releases the list");

            // a down whose up never arrives at all: released after UiKit.HoldStaleS without events
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = pill.worldBound.center })) { down.target = pill; pill.SendEvent(down); }
            Assert.IsTrue(UiKit.Held(list));
            yield return new WaitForSecondsRealtime(UiKit.HoldStaleS + 0.2f);
            Assert.IsFalse(UiKit.Held(list), "a lost up cannot freeze the list");
            list.RemoveFromHierarchy();
        }
    }
}
