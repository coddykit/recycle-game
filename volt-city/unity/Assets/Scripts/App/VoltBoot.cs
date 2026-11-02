// Volt City — boot scene: the own curtain (P2 Splash/curtain.png, background #0A1721, NO Unity / engine logo) on the
// first frame, then the kit (CoddyKitHost: tiers before any 3D, cached purchase rights, house ads, consent -> rewarded),
// then the City scene (P3) loads async under the curtain. No house banner over the curtain (KitConfig.Loading).
// If the City scene is not in the build yet (P0), the curtain stays with a status line — the boot itself is the test.
// UNCOMPILED until the Unity editor is installed.
using System.Collections;
using CoddyKit;
using CoddyKit.Ui;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace VoltCity.App
{
    public sealed class VoltBoot : MonoBehaviour
    {
        public const string CityScene = "City";

        [Tooltip("Kit PanelSettings (Assets/CoddyKit/UI/Resources/CoddyPanelSettings.asset)")]
        public PanelSettings panelSettings;
        [Tooltip("Assets/Art/Splash/curtain.png (P2)")]
        public Texture2D curtainArt;
        public UIDocument curtainDocument;

        public static bool Loading { get; private set; } = true;
        VisualElement bar;
        Label status;

        IEnumerator Start()
        {
            Loading = true;
            BuildCurtain();
            yield return null; // the curtain is on screen before anything heavy runs

            if (panelSettings == null) panelSettings = Resources.Load<PanelSettings>("CoddyPanelSettings");
            var cfg = VoltGameConfig.Kit();
            cfg.Loading = () => Loading;
            VoltCity.Game.VoltAnalytics.Install();   // P4: every kit + game event through the one validated catalogue
            CoddyKitHost.Boot(cfg, panelSettings);
            SetProgress(0.2f, "");
            yield return null;

            if (!Application.CanStreamedLevelBeLoaded(CityScene))
            {
                SetProgress(1f, "Volt City — P0 boot OK (City scene arrives in P3)");
                Debug.Log("[VoltCity] BOOT_OK: kit up, no City scene in the build yet");
                yield break;
            }
            var op = SceneManager.LoadSceneAsync(CityScene, LoadSceneMode.Single);
            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                SetProgress(0.2f + 0.8f * Mathf.Clamp01(op.progress / 0.9f), "");
                yield return null;
            }
            // the City scene owns its own curtain fade-out and sets Loading = false through VoltBoot.Ready()
        }

        /// <summary>The City scene calls this once its first frame is drawn (banner allowed from then on, onboarding gate permitting).</summary>
        public static void Ready() { Loading = false; }

        void BuildCurtain()
        {
            if (curtainDocument == null) return;
            var root = curtainDocument.rootVisualElement;
            var curtain = new VisualElement { name = "Curtain" };
            curtain.AddToClassList("coddy-curtain");
            if (curtainArt != null)
            {
                var art = new VisualElement { name = "CurtainArt", pickingMode = PickingMode.Ignore };
                art.AddToClassList("coddy-curtain__art");
                art.style.backgroundImage = new StyleBackground(curtainArt);
                curtain.Add(art);
            }
            status = UiKit.Text("", "coddy-curtain__status");
            curtain.Add(status);
            var progress = new VisualElement { pickingMode = PickingMode.Ignore };
            progress.AddToClassList("coddy-curtain__progress");
            bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("coddy-curtain__bar");
            progress.Add(bar);
            curtain.Add(progress);
            root.Add(curtain);
        }

        void SetProgress(float p, string text)
        {
            if (bar != null) bar.style.width = Length.Percent(Mathf.Clamp01(p) * 100f);
            if (status != null) UiKit.SetText(status, text);
        }
    }
}
