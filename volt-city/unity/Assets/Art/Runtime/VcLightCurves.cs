// Volt City light curves (P2): time of day = main directional light + ambient + fog + sky gradient + shader globals
// (VcCommon.hlsl). 0 draws; Apply() writes only when the moment changes — no per-frame CPU. Lightning = a short
// coroutine on the light intensity during storm events only. Night keeps ambient >= AmbientFloor (the old "too dark
// scene hides the decor" lesson). Same values as blender/render_frames.py CURVES. Uncompiled until P3.
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoltCity.Art
{
    public class VcLightCurves : MonoBehaviour
    {
        public const float AmbientFloor = 0.62f;

        public struct Moment
        {
            public Vector2 Sun;            // pitch, yaw (deg, Unity: yaw 0 = towards +Z north)
            public Color SunColor;
            public float SunIntensity;
            public Color Ambient;
            public float AmbientIntensity;
            public Color Fog;
            public float FogDensity;       // exponential fog
            public Color SkyTop, SkyHorizon;
            public float Night, Wet, WindowGlow;
        }

        public static readonly Moment Day = new()
        {
            Sun = new(48f, -28f), SunColor = new(1f, .95f, .88f), SunIntensity = 1.25f, Ambient = new(.62f, .70f, .84f),
            AmbientIntensity = .62f, Fog = new(.70f, .80f, .92f), FogDensity = .00016f, SkyTop = new(.32f, .54f, .86f),
            SkyHorizon = new(.80f, .87f, .95f), Night = 0f, Wet = 0f, WindowGlow = 2.0f,
        };
        public static readonly Moment Dusk = new()
        {
            Sun = new(14f, 62f), SunColor = new(1f, .56f, .30f), SunIntensity = 1.5f, Ambient = new(.36f, .36f, .62f),
            AmbientIntensity = .66f, Fog = new(.50f, .50f, .62f), FogDensity = .00024f, SkyTop = new(.17f, .22f, .45f),   // fog was (.74, .48, .44): the far sea read pink-brown (#174b)
            SkyHorizon = new(.98f, .58f, .36f), Night = .6f, Wet = 0f, WindowGlow = 2.5f,
        };
        public static readonly Moment NightM = new()
        {
            // moonlight floor (10-29, Patron #174): a blacked-out city at night must still read as a map — constants only,
            // no draw, no pass (Low identical cost); was SunIntensity .42, Ambient (.26, .33, .52)
            Sun = new(40f, -140f), SunColor = new(.56f, .66f, .95f), SunIntensity = .56f, Ambient = new(.31f, .39f, .58f),
            AmbientIntensity = .78f, Fog = new(.07f, .12f, .21f), FogDensity = .0002f, SkyTop = new(.03f, .06f, .14f),
            SkyHorizon = new(.12f, .20f, .33f), Night = 1f, Wet = 0f, WindowGlow = 2.7f,
        };
        public static readonly Moment Storm = new()
        {
            Sun = new(32f, -20f), SunColor = new(.70f, .76f, .86f), SunIntensity = .5f, Ambient = new(.32f, .38f, .50f),
            AmbientIntensity = .66f, Fog = new(.30f, .35f, .43f), FogDensity = .00046f, SkyTop = new(.18f, .22f, .29f),
            SkyHorizon = new(.40f, .44f, .51f), Night = .75f, Wet = 1f, WindowGlow = 2.6f,
        };

        public Light sun;
        public Camera cam;
        public Moment current;
        Coroutine flash;

        static readonly int NightId = Shader.PropertyToID("_VcNight"), WetId = Shader.PropertyToID("_VcWet"),
            SkyTopId = Shader.PropertyToID("_VcSkyTop"), SkyHorId = Shader.PropertyToID("_VcSkyHorizon"),
            WarmId = Shader.PropertyToID("_VcWindowWarm"), CoolId = Shader.PropertyToID("_VcWindowCool"),
            CyanId = Shader.PropertyToID("_VcPowerCyan"), GlowId = Shader.PropertyToID("_VcWindowGlow"),
            PoolId = Shader.PropertyToID("_VcPoolGlow");

        public void Apply(Moment m)
        {
            current = m;
            sun.transform.rotation = Quaternion.Euler(m.Sun.x, m.Sun.y, 0f);
            sun.color = m.SunColor;
            sun.intensity = m.SunIntensity;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = m.Ambient * Mathf.Max(m.AmbientIntensity, AmbientFloor);
            // Linear fog that starts BEYOND the diorama (Patron 10-28: the exponential fog at an 800-2000 m top-down camera
            // hazed the whole city — "murky"). The camera rig places it per view distance (SetViewDistance); the moment's
            // density only sets how fast the far edge fades (storm = shorter). Same per-vertex cost on every tier.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = m.Fog;
            RenderSettings.fogDensity = m.FogDensity;
            fogFadeMul = Mathf.Clamp(0.00035f / Mathf.Max(m.FogDensity, 1e-5f), 0.6f, 3f);
            PlaceFog();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = m.Fog;
            }
            Shader.SetGlobalFloat(NightId, m.Night);
            Shader.SetGlobalFloat(WetId, m.Wet);
            Shader.SetGlobalColor(SkyTopId, m.SkyTop.linear);
            Shader.SetGlobalColor(SkyHorId, m.SkyHorizon.linear);
            Shader.SetGlobalColor(WarmId, new Color(1f, .62f, .26f).linear);
            Shader.SetGlobalColor(CoolId, new Color(.86f, .92f, 1f).linear);
            Shader.SetGlobalColor(CyanId, new Color(.30f, .88f, 1f).linear);
            Shader.SetGlobalFloat(GlowId, m.WindowGlow);
            Shader.SetGlobalFloat(PoolId, .85f);
        }

        static float fogFadeMul = 1.5f, viewDist = 1200f;

        /// <summary>Camera rig: the camera-to-diorama distance changed (fit / zoom). Writes two RenderSettings floats.</summary>
        public static void SetViewDistance(float d)
        {
            if (Mathf.Abs(d - viewDist) < 1f) return;
            viewDist = d;
            PlaceFog();
        }

        static void PlaceFog()
        {
            RenderSettings.fogStartDistance = viewDist * 1.3f;
            RenderSettings.fogEndDistance = viewDist * (1.3f + fogFadeMul);
        }

        /// Lightning (storm events only): ~0.27 s blue-white flash of the main light.
        public void Flash()
        {
            if (flash != null)
                StopCoroutine(flash);
            flash = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            float baseI = current.SunIntensity;
            for (float t = 0f; t < .27f; t += Time.deltaTime)
            {
                sun.intensity = t < .05f ? Mathf.Lerp(baseI, 3.2f, t / .05f) : Mathf.Lerp(3.2f, baseI, (t - .05f) / .22f);
                yield return null;
            }
            sun.intensity = baseI;
            flash = null;
        }
    }
}
