// Volt City props (P2): crew trucks, cars, fault markers, trees — each drawn as ONE instanced batch
// (Graphics.RenderMeshInstanced, Runtime/VcInstancedProps.cs). Per-instance data via the instancing buffer:
//   _InstCustom (x, y, z, w): truck x = boom raise 0..1 · car x = shape (0 hatch, 1 van, 2 city car), y = paint 0..7 ·
//   marker x = pulse phase 0..1 · trees: unused (tint from the instance position hash)
//   _InstColor: always WHITE (Godot-era rule LL W68 carried over: never rely on a zero default instance colour)
// Values are small and bounded (no absolute time / age per instance) so half-precision buffers stay exact.
// Codes (alpha * 20): 20 matte · 18 gloss · 17 livery primary · 16 livery secondary · 19 car paint · 6 lights · 1 foliage
// _PropKind: 0 truck (uv.x = 1 on the boom), 1 car (uv.x roof flag, uv.y tail flag), 2 fault marker, 3 tree.
#ifndef VC_PROP_CORE_INCLUDED
#define VC_PROP_CORE_INCLUDED

#include "VcCommon.hlsl"

CBUFFER_START(UnityPerMaterial)
    float _PropKind;
    float4 _LiveryA;
    float4 _LiveryB;
CBUFFER_END

float4 _VcPaints[8];   // global: generic car paints (VcInstancedProps)

UNITY_INSTANCING_BUFFER_START(VcProps)
    UNITY_DEFINE_INSTANCED_PROP(float4, _InstCustom)
    UNITY_DEFINE_INSTANCED_PROP(float4, _InstColor)
UNITY_INSTANCING_BUFFER_END(VcProps)

struct PAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct PVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float4 color : TEXCOORD2;
    half3 tint : TEXCOORD3;
    float fogFactor : TEXCOORD4;
#if defined(VC_LOW)
    half3 light : TEXCOORD5;
#endif
};

PVaryings VcPropVert(PAttributes v)
{
    PVaryings o = (PVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    float4 cu = UNITY_ACCESS_INSTANCED_PROP(VcProps, _InstCustom);
    half4 ic = UNITY_ACCESS_INSTANCED_PROP(VcProps, _InstColor);
    float3 p = v.positionOS.xyz;
    half3 tint = 1;
    int kind = (int)(_PropKind + 0.5);
    if (kind == 0 && v.uv.x > 0.5)
    {
        // boom + bucket swing up about the turret pivot (local X axis through y 2.9, z -1.6; the truck's nose is +Z in
        // Unity local space), up to 62 degrees
        float a = saturate(cu.x) * 1.08;
        float2 q = p.zy - float2(-1.6, 2.9);
        p.zy = float2(-1.6, 2.9) + float2(q.x * cos(a) - q.y * sin(a), q.x * sin(a) + q.y * cos(a));
    }
    else if (kind == 1)
    {
        int shape = (int)(cu.x + 0.5);
        if (shape == 1 && v.uv.x > 0.5)
            p.y += max(0.0, p.y - 0.9) * 1.1;      // van: tall roof
        if (shape == 2 && v.uv.y > 0.5)
            p.z += 0.55;                           // city car: short tail (the tail is local -Z, the nose +Z)
        tint = _VcPaints[clamp((int)(cu.y + 0.5), 0, 7)].rgb;
    }
    else if (kind == 2)
    {
#if !defined(VC_LOW)
        p.xz *= 1.0 + 0.12 * sin(_Time.y * 5.0 + cu.x * 6.28);
#endif
    }
    else if (kind == 3)
    {
        float3 org = TransformObjectToWorld(float3(0, 0, 0));
        float h = VcHash11(dot(org.xz, float2(0.37, 0.71)));
        tint = lerp(half3(0.82, 0.95, 0.72), half3(1.08, 1.02, 0.86), h);
#if !defined(VC_LOW)
        p.xz += float2(sin(_Time.y * 1.3 + h * 6.28), cos(_Time.y * 1.1 + h * 4.0)) * max(0.0, p.y - 3.0) * 0.012 * (1.0 + 3.0 * _VcWet);
#endif
    }
    VertexPositionInputs pos = GetVertexPositionInputs(p);
    o.positionCS = pos.positionCS;
    o.positionWS = pos.positionWS;
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.color = float4(VcColourIn(v.color.rgb) * ic.rgb, v.color.a);
    o.tint = tint;
    o.fogFactor = ComputeFogFactor(pos.positionCS.z);
#if defined(VC_LOW)
    Light ml = GetMainLight();
    o.light = ml.color * saturate(dot(o.normalWS, ml.direction)) + SampleSHVertex(o.normalWS);
#endif
    return o;
}

half4 VcPropFrag(PVaryings i) : SV_Target
{
    int code = VcCode(i.color.a);
    half3 c = i.color.rgb;
    half3 emit = 0;
    half rough = 0.7;
    if (code == 17) { c *= _LiveryA.rgb; rough = 0.45; }
    else if (code == 16) { c = _LiveryB.rgb * (c.r + c.g + c.b) / 3.0 * 1.3; }
    else if (code == 19) { c *= i.tint; rough = 0.35; }
    else if (code == 6) { emit = c * lerp(0.6, 2.4, _VcNight); c *= 0.3; }
    else if (code == 1) { c *= i.tint * lerp(1.0, 0.7, _VcNight); }
    else if (code == 18) { rough = 0.25; }
    c *= lerp(1.0, 0.8, _VcWet);
#if defined(VC_LOW)
    half3 col = c * i.light + emit;
#else
    float3 n = normalize(i.normalWS);
    Light ml = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half3 col = c * (ml.color * saturate(dot(n, ml.direction)) * ml.shadowAttenuation + SampleSH(n)) + emit;
    float3 hv = normalize(ml.direction + normalize(GetWorldSpaceViewDir(i.positionWS)));
    col += ml.color * pow(saturate(dot(n, hv)), lerp(8.0, 96.0, 1.0 - rough)) * 0.25 * ml.shadowAttenuation;
#endif
    return half4(MixFog(col, i.fogFactor), 1.0);
}

float3 _LightDirection;

struct PShadowVaryings
{
    float4 positionCS : SV_POSITION;
};

PShadowVaryings VcPropShadowVert(PAttributes v)
{
    PShadowVaryings o;
    UNITY_SETUP_INSTANCE_ID(v);
    float3 ws = TransformObjectToWorld(v.positionOS.xyz);
    float3 nws = TransformObjectToWorldNormal(v.normalOS);
    float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, nws, _LightDirection));
#if UNITY_REVERSED_Z
    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
#else
    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
#endif
    o.positionCS = cs;
    return o;
}

half4 VcPropNullFrag(PShadowVaryings i) : SV_Target
{
    return 0;
}

#endif
