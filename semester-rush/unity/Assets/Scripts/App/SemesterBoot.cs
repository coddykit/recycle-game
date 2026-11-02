// Semester Rush — boot scene: the game's own curtain (no Unity / engine logo; splash background #0F3B2E) on the first
// frame, then the kit (CoddyKitHost: tiers before any 3D, cached purchase rights, house ads, consent -> rewarded), then
// the Campus scene loads async under the curtain. No house banner over the curtain (KitConfig.Loading).
using System.Collections;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SemesterRush.App
{
    public sealed class SemesterBoot : MonoBehaviour
    {
        public const string CampusScene = "Campus";

        public PanelSettings panelSettings;
        public Texture2D curtainArt;
        public UIDocument curtainDocument;

        public static bool Loading { get; private set; } = true;
        VisualElement bar;
        Label status;

        IEnumerator Start()
        {
            Loading = true;
            BuildCurtain();
            yield return null;   // the curtain is on screen before anything heavy runs
            if (panelSettings == null) panelSettings = Resources.Load<PanelSettings>("CoddyPanelSettings");
            var cfg = SemesterGameConfig.Kit();
            cfg.Loading = () => Loading;
            CoddyKitHost.Boot(cfg, panelSettings);
            SetProgress(0.2f);
            yield return null;
            if (!Application.CanStreamedLevelBeLoaded(CampusScene))
            {
                SetProgress(1f);
                Debug.Log("[SemesterRush] BOOT_OK: kit up, no Campus scene in the build yet");
                yield break;
            }
            var op = SceneManager.LoadSceneAsync(CampusScene, LoadSceneMode.Single);
            while (!op.isDone)
            {
                SetProgress(0.2f + 0.8f * Mathf.Clamp01(op.progress / 0.9f));
                yield return null;
            }
        }

        /// <summary>The Campus scene calls this once its first frame is drawn (banner allowed from then on).</summary>
        public static void Ready() { Loading = false; }

        void BuildCurtain()
        {
            if (curtainDocument == null) return;
            var root = curtainDocument.rootVisualElement;
            var curtain = new VisualElement { name = "Curtain" };
            curtain.AddToClassList("coddy-curtain");
            curtain.style.backgroundColor = new Color(0.059f, 0.231f, 0.180f);
            if (curtainArt != null)
            {
                var art = new VisualElement { name = "CurtainArt", pickingMode = PickingMode.Ignore };
                art.AddToClassList("coddy-curtain__art");
                art.style.backgroundImage = new StyleBackground(curtainArt);
                curtain.Add(art);
            }
            status = UiKit.Text("Semester Rush", "coddy-curtain__status");
            curtain.Add(status);
            var progress = new VisualElement { pickingMode = PickingMode.Ignore };
            progress.AddToClassList("coddy-curtain__progress");
            bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("coddy-curtain__bar");
            progress.Add(bar);
            curtain.Add(progress);
            root.Add(curtain);
        }

        void SetProgress(float p)
        {
            if (bar != null) bar.style.width = Length.Percent(Mathf.Clamp01(p) * 100f);
        }
    }
}
