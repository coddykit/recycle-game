// Volt City shared helpers + city-wide globals (P2). Globals are set ONCE per change from C# (VcLightCurves /
// VcCityArt: Shader.SetGlobal*), never per frame; per-material values live in UnityPerMaterial (SRP Batcher safe).
#ifndef VC_COMMON_INCLUDED
#define VC_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// time of day (VcLightCurves)
float _VcNight;            // 0 day .. 1 deep night: windows, lamps, pools
float _VcWet;              // storm: darker wet asphalt + sheen
float4 _VcSkyTop;
float4 _VcSkyHorizon;
float4 _VcWindowWarm;
float4 _VcWindowCool;
float4 _VcPowerCyan;
float _VcWindowGlow;
float _VcPoolGlow;
// grid / plants (VcCityArt)
float _VcGridFlow;
float4 _VcCorridorState[16];   // x: 0 normal, 1 overheat, 2 down, 3 not built; y: flow 0..1
float _VcPlantOn[6];           // output fraction per plant type (layout.json plant_types order)
float _VcBatteryCharge;
float _VcReservoirLevel;
// cosmetics (IAP.md): drawn on the default models, visible on every tier
float _VcSkylineStyle;     // skyline_neon = 1
float _VcPylonDeco;        // pylons_deco = 1

int VcCode(float a)
{
    return (int)floor(a * 20.0 + 0.5);
}

// vertex colours are stored sRGB-encoded (blender/vckit.py); the project is Linear colour space
half3 VcColourIn(half3 c)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return c;
#else
    return SRGBToLinear(c);
#endif
}

float VcHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float VcHash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

#endif
