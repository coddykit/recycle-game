// Volt City — builds Assets/Scenes/City.unity from code (P3), so the scene is reproducible and reviewable as text:
//   Main Camera (URP, no post) · Sun (directional) · City: VcCityArt (P2 meshes + the 5 tier materials) + VcLightCurves +
//   CityWorld + CameraRig + kit TapInput · UI: UIDocument (kit PanelSettings) + VoltCityMain.
// Materials (Assets/Art/Materials/*.mat) are created from P2's shaders on first run: World / WorldLow (+ surface.png),
// Prop / PropLow (GPU instancing on), Blades. Then the build scenes are registered (Boot, City).
//   Menu: Coddy › Volt City › Build City Scene
//   Batch: Unity -batchmode -quit -projectPath games/volt-city/unity -executeMethod VoltCity.WorldEditor.VoltCitySceneBuilder.Build
// Commit the generated City.unity, the materials and their .meta files with the first editor commit (docs/UNITY.md §3).
// UNCOMPILED until the Unity editor is installed.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using CoddyKit;
using CoddyKit.EditorTools;
using VoltCity.Art;
using VoltCity.EditorTools;
using VoltCity.UI;
using VoltCity.World;

namespace VoltCity.WorldEditor
{
    public static class VoltCitySceneBuilder
    {
        const string Models = "Assets/Art/Models/";
        const string MatDir = "Assets/Art/Materials/";
        static readonly string[] DistrictIds = { "old_harbor", "riverside", "hilltop", "market_square", "tech_park", "stadium_row", "hospital_hill", "airport_flats" };
        static readonly string[] PlantTypes = { "steam", "peaker", "hydro", "wind", "solar", "battery" };

        [MenuItem("Coddy/Volt City/Build City Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(10 / 255f, 23 / 255f, 33 / 255f);
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;   // the URP asset of the tier decides (Low has no shadows)

            var cityGo = new GameObject("City");
            var art = cityGo.AddComponent<VcCityArt>();
            art.terrain = LoadMesh("world/terrain_city");
            art.sky = LoadMesh("world/sky_dome");
            art.gridLines = LoadMesh("world/grid_lines");
            art.substation = LoadMesh("grid/substation");
            art.substationSmart = LoadMesh("grid/substation_smart");
            art.truck = LoadMesh("props/truck_crew");
            art.car = LoadMesh("props/car");
            art.faultMarker = LoadMesh("props/fault_marker");
            art.tree = LoadMesh("props/tree");
            art.windBlades = LoadMesh("props/wind_blades");
            art.districts = DistrictIds.Select(id => new VcCityArt.DistrictRef { id = id, mesh = LoadMesh("world/district_" + id) }).ToArray();
            art.plants = PlantTypes.Select(t => new VcCityArt.PlantRef
            {
                type = t,
                levels = new[] { LoadMesh("plants/plant_" + t + "_l1"), LoadMesh("plants/plant_" + t + "_l2"), LoadMesh("plants/plant_" + t + "_l3") },
            }).ToArray();
            art.layoutJson = AssetDatabase.LoadAssetAtPath<TextAsset>(Models + "layout.json");
            art.worldMedium = Mat("World_Medium", "VoltCity/World", true, false);
            art.worldLow = Mat("World_Low", "VoltCity/WorldLow", true, false);
            art.propMedium = Mat("Prop_Medium", "VoltCity/Prop", false, true);
            art.propLow = Mat("Prop_Low", "VoltCity/PropLow", false, true);
            art.blades = Mat("Blades", "VoltCity/Blades", false, true);

            var lights = cityGo.AddComponent<VcLightCurves>();
            lights.sun = sun;
            lights.cam = cam;
            var rig = cityGo.AddComponent<CameraRig>();
            rig.cam = cam;
            var tap = cityGo.AddComponent<TapInput>();
            var world = cityGo.AddComponent<CityWorld>();
            world.art = art;
            world.lights = lights;
            world.cam = cam;
            world.rig = rig;
            world.tap = tap;
            world.layoutJson = art.layoutJson;

            var uiGo = new GameObject("UI");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = ProjectSetup.EnsurePanelSettings(VoltProjectSetup.Spec);
            doc.sortingOrder = 10;
            var main = uiGo.AddComponent<VoltCityMain>();
            main.uiDocument = doc;
            main.world = world;
            main.curtainArt = AssetDatabase.LoadAssetAtPath<Texture2D>(VoltProjectSetup.CurtainArt);

            Directory.CreateDirectory(Path.GetDirectoryName(VoltProjectSetup.CityScene));
            EditorSceneManager.SaveScene(scene, VoltProjectSetup.CityScene);
            VoltProjectSetup.EnsureBootScene();   // registers Boot + City as build scenes
            AssetDatabase.SaveAssets();
            int missing = art.districts.Count(d => d.mesh == null) + art.plants.Sum(p => p.levels.Count(m => m == null))
                          + new[] { art.terrain, art.sky, art.gridLines, art.substation, art.substationSmart, art.truck, art.car, art.faultMarker, art.tree, art.windBlades }.Count(m => m == null);
            if (missing > 0) Debug.LogError("[VoltCity] CITY_SCENE: " + missing + " meshes missing (import Assets/Art/Models first)");
            else Debug.Log("[VoltCity] CITY_SCENE_OK " + VoltProjectSetup.CityScene);
        }

        static Mesh LoadMesh(string rel)
        {
            string path = Models + rel + ".fbx";
            var m = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault();
            if (m == null) Debug.LogWarning("[VoltCity] mesh missing: " + path);
            return m;
        }

        static Material Mat(string name, string shader, bool world, bool instancing)
        {
            Directory.CreateDirectory(MatDir);
            string path = MatDir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find(shader);
            if (sh == null) { Debug.LogError("[VoltCity] shader missing: " + shader); return null; }
            if (m == null)
            {
                m = new Material(sh) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = sh;
            m.enableInstancing = instancing;
            if (world)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/surface.png");
                if (tex != null && m.HasProperty("_Surface")) m.SetTexture("_Surface", tex);
            }
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
