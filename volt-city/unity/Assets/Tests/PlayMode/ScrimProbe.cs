// Volt City — probe (EXPLICIT): is the city drawn behind an open modal sheet, and how dark does the scrim make it?
// Measures the mean brightness of the diorama band in one render texture (camera + UI, as on screen):
//   A sheet closed · B sheet open · C sheet open with the scrim made transparent. Writes PNGs + numbers to VOLT_PROBE_DIR.
using System.Collections;
using System.IO;
using System.Reflection;
using CoddyKit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VoltCity.UI;

namespace VoltCity.PlayMode.Tests
{
    [Explicit("probe")]
    public class ScrimProbe
    {
        [UnityTest, Timeout(int.MaxValue)]
        public IEnumerator SheetScrim_OverTheCity()
        {
            string dir = System.Environment.GetEnvironmentVariable("VOLT_PROBE_DIR") ?? "";
            if (dir == "") Assert.Ignore("VOLT_PROBE_DIR");
            Directory.CreateDirectory(dir);
            LogAssert.ignoreFailingMessages = true;
            SafeArea.Simulate(1080, 2340, new Insets(0, 110, 0, 126));
            SceneManager.LoadScene("City");
            yield return null;
            VoltCityMain main = null;
            while ((main = Object.FindFirstObjectByType<VoltCityMain>()) == null || !main.Ready) yield return null;
            var rt = new RenderTexture(1080, 2340, 24, RenderTextureFormat.ARGB32);
            var ps = main.uiDocument.panelSettings;
            var old = ps.targetTexture;
            ps.targetTexture = rt; ps.clearColor = false;
            main.world.cam.targetTexture = rt;
            var root = main.uiDocument.rootVisualElement;
            // past the pager
            for (int i = 0; i < 3; i++) { var b = root.Q<Button>("pager.next"); if (b != null) using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); } yield return null; }
            for (int i = 0; i < 20; i++) yield return null;
            main.Screen.Sheets.Close();
            string A = Shot(rt, dir, "a_closed");
            typeof(VoltCityMain).GetMethod("OpenSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(main, null);
            for (int i = 0; i < 20; i++) yield return null;
            string B = Shot(rt, dir, "b_open");
            root.panel.visualTree.Query<VisualElement>(className: "coddy-scrim").ForEach(e => e.style.backgroundColor = new Color(0, 0, 0, 0));
            for (int i = 0; i < 6; i++) yield return null;
            string C = Shot(rt, dir, "c_open_no_scrim");
            // D: sheet still open, UI off -> what the camera alone draws
            ps.targetTexture = null;
            for (int i = 0; i < 6; i++) yield return null;
            string D = Shot(rt, dir, "d_open_camera_only");
            var cam = main.world.cam;
            string camInfo = $"cam enabled={cam.enabled} active={cam.gameObject.activeInHierarchy} rect={cam.rect} pos={cam.transform.position} rot={cam.transform.eulerAngles} cull={cam.cullingMask} clear={cam.clearFlags} bg={cam.backgroundColor} tt={(cam.targetTexture != null)}";
            string scrim = "";
            root.panel.visualTree.Query<VisualElement>(className: "coddy-scrim").ForEach(e => scrim += e.resolvedStyle.backgroundColor + " ");
            File.WriteAllText(Path.Combine(dir, "probe.txt"), $"A closed {A}\nB open {B}\nC open, scrim transparent {C}\nD open, camera only {D}\n{camInfo}\nscrim resolved now {scrim}\n");
            ps.targetTexture = old;
            SafeArea.StopSimulating();
        }

        static string Shot(RenderTexture rt, string dir, string name)
        {
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), t.EncodeToPNG());
            // mean brightness of the diorama band (y 25-45 % from the top)
            double sum = 0; int n = 0;
            for (int y = (int)(rt.height * 0.55); y < (int)(rt.height * 0.75); y += 7)
                for (int x = 0; x < rt.width; x += 7) { var c = t.GetPixel(x, y); sum += (c.r + c.g + c.b) / 3.0; n++; }
            Object.Destroy(t);
            return (sum / n * 255).ToString("F1");
        }
    }
}
