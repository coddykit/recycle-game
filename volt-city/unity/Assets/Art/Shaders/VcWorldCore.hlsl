// Volt City shared STATIC shader core (P2): terrain + sky, the 8 districts, the merged plants, the merged grid.
// VcWorld.shader (Medium / High: per pixel, shadows, specular, animated smoke) and VcWorldLow.shader (#define VC_LOW:
// per-vertex lighting, no shadows, no specular, no animation) include THIS file, so the district light ripple, the
// windows and the corridor states are the same code on every tier (ART section 1: one branch on every tier).
//
// Vertex colour = palette x baked AO (sRGB-encoded), ALPHA = material code, index = round(a * 20) (blender/vckit.py):
//   20 matte · 18 gloss · 17 pylon steel · 16 road asphalt (uv = lateral m, m along) · 15 district street (uv.x ripple,
//   uv.y lamp pool) · 14 ground (height picks water / sand / grass / rock) · 13 battery strip (uv.x along, uv.y plant)
//   · 12 roof · 11 smoke puff (uv.y plant) · 10 water · 9 reservoir (level) · 8 window wall (uv.x ripple, uv.y seed,
//   uv2 = (style, base height)) · 7 district lamp (ripple) · 6 always lit (plants: gated by output) · 5 conductor
//   (corridor) · 4 sky (uv.y height) · 3 cyan flow accent · 1 foliage
// _MeshKind: 0 terrain / sky, 1 district, 2 grid (uv.x = corridor index, -1 = substation), 3 plants (uv.y = type + .5)
#ifndef VC_WORLD_CORE_INCLUDED
#define VC_WORLD_CORE_INCLUDED

#include "VcCommon.hlsl"

TEXTURE2D(_Surface);
SAMPLER(sampler_Surface);

CBUFFER_START(UnityPerMaterial)
    float _MeshKind;
    float4 _Ripple;        // districts: x = wave front 0..1.3, y = target (1 lit, 0 dark); one material per district
    float4 _Surface_ST;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    float2 uv2 : TEXCOORD1;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float4 color : TEXCOORD2;
    float4 uvs : TEXCOORD3;          // uv.xy, uv2.xy
    float fogFactor : TEXCOORD4;
#if defined(VC_LOW)
    half3 light : TEXCOORD5;         // per-vertex diffuse light (main + SH)
#endif
};

static const float RIPPLE_JITTER = 0.12;

float DistrictOn(float u)
{
    return u < _Ripple.x ? _Ripple.y : 1.0 - _Ripple.y;
}

// box-filtered pulse train: fraction f of every unit cell (centred) is 1; w = filter width in cells (fwidth)
float PulseInt(float x, float f)
{
    return floor(x) * f + clamp(frac(x) - (1.0 - f) * 0.5, 0.0, f);
}

float PulseAA(float x, float f, float w)
{
    w = max(w, 1e-4);
    return (PulseInt(x + 0.5 * w, f) - PulseInt(x - 0.5 * w, f)) / w;
}

// window styles (blender/kit_buildings.py STYLE): cell width, floor height, window width / height fraction, sill, occupancy, warmth
void StyleOf(int s, out float4 geo, out float sill, out float occ, out float warm)
{
    geo = float4(3.2, 3.2, 0.45, 0.45); sill = 0.32; occ = 0.62; warm = 1.0;
    if (s == 2) { geo = float4(3.0, 3.6, 0.82, 0.42); sill = 0.3; occ = 0.7; warm = 0.55; }
    else if (s == 3) { geo = float4(1.6, 3.8, 0.86, 0.78); sill = 0.1; occ = 0.58; warm = 0.3; }
    else if (s == 4) { geo = float4(4.0, 6.0, 0.7, 0.16); sill = 0.74; occ = 0.38; warm = 0.7; }
    else if (s == 5) { geo = float4(3.2, 3.4, 0.5, 0.45); sill = 0.32; occ = 0.6; warm = 1.0; }
    else if (s == 6) { geo = float4(2.4, 2.0, 0.92, 0.12); sill = 0.5; occ = 0.96; warm = 0.0; }
    else if (s == 7) { geo = float4(3.0, 3.4, 0.62, 0.5); sill = 0.3; occ = 0.88; warm = 0.2; }
    else if (s == 8) { geo = float4(4.2, 3.2, 0.5, 0.56); sill = 0.24; occ = 0.46; warm = 1.0; }
    else if (s == 9) { geo = float4(2.2, 11.0, 0.9, 0.78); sill = 0.08; occ = 0.95; warm = 0.6; }
    else if (s == 10) { geo = float4(6.0, 4.0, 0.7, 0.3); sill = 0.55; occ = 0.92; warm = 0.6; }
}

// window wall: facade coordinates from world position + world normal (any yaw), floors from the base height in uv2.y
half3 WindowWall(half3 wall, half3 tf, Varyings i, out half3 emit)
{
    emit = 0;
    int style = (int)(i.uvs.z + 0.5);
    float base = i.uvs.w;
    float seed = i.uvs.y;
    float3 n = normalize(i.normalWS);
    if (style == 0 || abs(n.y) > 0.5)
        return wall * (0.92 + 0.12 * tf.r);
    float4 geo; float sill; float occ; float warm;
    StyleOf(style, geo, sill, occ, warm);
    float2 t = normalize(float2(-n.z, n.x));
    float s = dot(i.positionWS.xz, t) / geo.x + seed * 7.0;
    float h = (i.positionWS.y - base) / geo.y;
    if (style == 5 && i.positionWS.y - base < 4.2)
    {
        h = (i.positionWS.y - base) / 4.2;   // shop fronts: one tall glazed ground floor
        geo.z = 0.86; geo.w = 0.62; sill = 0.12; occ = 0.9; warm = 1.0;
    }
    float ws = fwidth(s);
    float wh = fwidth(h);
    float mx = PulseAA(s, geo.z, ws);
    float my = PulseAA(h - sill - 0.5 * geo.w + 0.5, geo.w, wh);
    float plinth = smoothstep(0.55, 0.75, i.positionWS.y - base);
    float mask = mx * my * plinth;
    float2 cell = float2(floor(s), floor(h));
    float hsh = VcHash21(cell + seed * 31.0);
    float farAway = smoothstep(0.45, 0.9, max(ws, wh));       // sub-pixel windows fade to the average: no shimmer
    float litWin = lerp(step(1.0 - occ, hsh), occ, farAway);
    float on = DistrictOn(i.uvs.x + RIPPLE_JITTER * lerp(hsh, 0.5, farAway));
    half3 tint = lerp(_VcWindowCool.rgb, _VcWindowWarm.rgb, saturate(warm + 0.3 * (VcHash21(cell + 7.0) - 0.5)));
    if (_VcSkylineStyle > 0.5)
        tint = lerp(half3(1.0, 0.36, 0.78), half3(0.3, 0.92, 1.0), step(0.5, VcHash21(cell + 3.0)));
    if (style == 6)
        tint = lerp(_VcPowerCyan.rgb, _VcWindowCool.rgb, 0.45);
    float tone = 0.75 + 0.5 * VcHash21(cell + 11.0);
    half3 glassDay = lerp(half3(0.20, 0.27, 0.34), _VcSkyTop.rgb * 1.3, 0.35) * (0.8 + 0.3 * VcHash21(cell + 5.0));
    half3 glass = lerp(glassDay, half3(0.035, 0.05, 0.08), _VcNight);
    half3 albedo = lerp(wall * (0.9 + 0.14 * tf.r), glass, mask);
    emit = tint * mask * litWin * on * _VcNight * tone * _VcWindowGlow;
    albedo *= lerp(1.0, lerp(0.55, 1.0, on), _VcNight);   // a dark district reads "lights out" even from far away
    return albedo;
}

half3 Ground(half3 c, half3 tx, half3 tf, Varyings i, out half3 emit, out half rough)
{
    emit = 0;
    rough = 0.95;
    float y = i.positionWS.y;
    float3 n = normalize(i.normalWS);
    if (y < -0.08)
    {
        // water: depth tint; the shore is the height contour, smooth at any grid size
        float depth = saturate(-y / 6.0);
        half3 wc = lerp(half3(0.18, 0.46, 0.52), half3(0.07, 0.22, 0.34), depth) * (0.9 + 0.16 * tx.g);
        // water mirrors the sky ABOVE (blue), not the orange horizon (10-29 #174b: the dusk sea read purple / brown)
        half3 skyRef = lerp(_VcSkyTop.rgb, _VcSkyHorizon.rgb, 0.25);
        wc = lerp(wc, lerp(wc, skyRef, 0.35), smoothstep(0.4, 1.0, tx.b) * 0.25);
        rough = 0.12;
        emit = skyRef * (0.05 + 0.05 * tx.b) * (1.0 - _VcNight * 0.6);
        return lerp(wc, wc * half3(0.30, 0.36, 0.55), _VcNight);
    }
    half3 grass = c * (0.82 + 0.3 * tx.g) * (0.93 + 0.14 * tf.r);
    half3 sand = half3(0.80, 0.71, 0.52) * (0.9 + 0.15 * tf.r);
    half3 rock = half3(0.50, 0.48, 0.44) * (0.85 + 0.25 * tx.r);
    half3 g = lerp(grass, sand, 1.0 - smoothstep(0.35, 0.95, y));
    g = lerp(g, rock, smoothstep(0.78, 0.6, n.y));
    return g * lerp(1.0, half3(0.40, 0.46, 0.70), _VcNight) * lerp(1.0, 0.85, _VcWet);
}

half3 Asphalt(half3 c, half3 tf, half3 tx)
{
    return c * (0.84 + 0.24 * tf.r) * (0.94 + 0.12 * tx.g) * lerp(1.0, 0.68, _VcWet) * lerp(1.0, 0.82, _VcNight);
}

Varyings VcWorldVert(Attributes v)
{
    Varyings o = (Varyings)0;
    int code = VcCode(v.color.a);
    float3 p = v.positionOS.xyz;
    bool hide = false;
    int kind = (int)(_MeshKind + 0.5);
    if (kind == 2 && v.uv.x > -0.5)
        hide = _VcCorridorState[clamp((int)(v.uv.x + 0.5), 0, 15)].x > 2.5;
    if (kind == 3)
    {
        int pi = clamp((int)v.uv.y, 0, 5);
        if (code == 11)
        {
            hide = _VcPlantOn[pi] < 0.04;
#if !defined(VC_LOW)
            float tt = _Time.y;
            p += float3(sin(tt * 0.7 + p.y * 0.21), sin(tt * 0.9 + p.x * 0.17) * 0.6, cos(tt * 0.6 + p.z * 0.19)) * 0.9;
#endif
        }
        if (code == 9)
            p.y -= (1.0 - _VcReservoirLevel) * 6.0;
    }
    if (hide)
        p = 0;
    VertexPositionInputs pos = GetVertexPositionInputs(p);
    o.positionCS = pos.positionCS;
    o.positionWS = pos.positionWS;
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.color = float4(VcColourIn(v.color.rgb), v.color.a);
    o.uvs = float4(v.uv, v.uv2);
    o.fogFactor = ComputeFogFactor(pos.positionCS.z);
#if defined(VC_LOW)
    Light ml = GetMainLight();
    o.light = ml.color * saturate(dot(o.normalWS, ml.direction)) + SampleSHVertex(o.normalWS);
#endif
    return o;
}

half4 VcWorldFrag(Varyings i) : SV_Target
{
    int code = VcCode(i.color.a);
    int kind = (int)(_MeshKind + 0.5);
    half3 c = i.color.rgb;
    half3 emit = 0;
    half rough = 0.85;
    half spec = 0.12;
    bool unlit = false;
    bool water = false;
    half3 tx = SAMPLE_TEXTURE2D(_Surface, sampler_Surface, i.positionWS.xz * 0.013).rgb;
    half3 tf = SAMPLE_TEXTURE2D(_Surface, sampler_Surface, i.positionWS.xz * 0.21).rgb;
    if (code == 8)
    {
        c = WindowWall(c, tf, i, emit);
        spec = 0.2;
    }
    else if (code == 14)
    {
        c = Ground(c, tx, tf, i, emit, rough);
        spec = rough < 0.5 ? 0.4 : 0.06;
        water = i.positionWS.y < -0.08;
    }
    else if (code == 16)
    {
        c = Asphalt(c, tf, tx);
        float dash = step(0.5, frac(i.uvs.y / 9.0)) * (1.0 - smoothstep(0.12, 0.2, abs(i.uvs.x)));
        float edge = smoothstep(3.55, 3.7, abs(i.uvs.x)) * (1.0 - smoothstep(3.85, 4.0, abs(i.uvs.x)));
        c = lerp(c, half3(0.86, 0.84, 0.76) * lerp(1.0, 0.6, _VcNight), saturate(dash * 0.8 + edge * 0.7));
        rough = lerp(0.9, 0.3, _VcWet);
        spec = lerp(0.1, 0.55, _VcWet);
        emit = _VcSkyHorizon.rgb * _VcWet * 0.04;
    }
    else if (code == 15)
    {
        float on = DistrictOn(i.uvs.x);
        c = Asphalt(c, tf, tx);
        emit = _VcWindowWarm.rgb * pow(saturate(i.uvs.y), 1.4) * on * _VcNight * _VcPoolGlow * (1.0 + 0.6 * _VcWet);
        c *= lerp(1.0, lerp(0.6, 1.0, on), _VcNight);
        rough = lerp(0.9, 0.3, _VcWet);
        spec = lerp(0.1, 0.55, _VcWet);
    }
    else if (code == 7)
    {
        emit = c * DistrictOn(i.uvs.x) * _VcNight * 2.2;
        c *= lerp(1.0, 0.3, _VcNight);
    }
    else if (code == 12)
    {
        c *= (0.82 + 0.3 * tf.r) * (0.92 + 0.14 * VcHash21(floor(i.positionWS.xz / 9.0)));
        if (kind == 1)
            c *= lerp(1.0, lerp(0.6, 1.0, DistrictOn(i.uvs.x)), _VcNight);
    }
    else if (code == 10 || code == 9)
    {
        water = true;
        c = half3(0.11, 0.33, 0.43) * (0.9 + 0.16 * tx.g);
        rough = 0.12;
        spec = 0.5;
        emit = lerp(_VcSkyTop.rgb, _VcSkyHorizon.rgb, 0.25) * 0.06 * (1.0 - _VcNight * 0.6);
    }
    else if (code == 6)
    {
        float gate = 1.0;
        if (kind == 3 && c.g > 0.3)
            gate = step(0.04, _VcPlantOn[clamp((int)i.uvs.y, 0, 5)]);
        if (kind == 1)
            gate = DistrictOn(i.uvs.x);
        emit = c * gate * lerp(0.35, 1.8, _VcNight);
        c *= 0.35;
    }
    else if (code == 5)
    {
        float4 st = _VcCorridorState[clamp((int)(i.uvs.x + 0.5), 0, 15)];
        half3 glow = _VcPowerCyan.rgb * (0.25 + 0.75 * st.y) * _VcGridFlow;
        if (st.x > 1.5)
        {
            glow = half3(1.0, 0.18, 0.12);
            c = half3(0.35, 0.08, 0.06);
        }
        else if (st.x > 0.5)
        {
#if defined(VC_LOW)
            half pulse = 1.0;
#else
            half pulse = 0.7 + 0.3 * sin(_Time.y * 6.0);
#endif
            glow = half3(1.0, 0.55, 0.12) * pulse;
        }
        emit = glow * lerp(0.25, 1.4, _VcNight);
    }
    else if (code == 17)
    {
        float band = step(0.5, frac(i.positionWS.y / 5.0));
        c = lerp(c, lerp(half3(0.93, 0.93, 0.92), half3(0.82, 0.16, 0.12), band), _VcPylonDeco);
        c *= lerp(1.0, 0.7, _VcNight);
        spec = 0.3;
        rough = 0.5;
    }
    else if (code == 4)
    {
        float t = saturate(i.uvs.y);
        half3 sky = lerp(_VcSkyHorizon.rgb, _VcSkyTop.rgb, smoothstep(0.0, 0.55, t));
        float2 sc = floor(float2(atan2(i.positionWS.x, i.positionWS.z) * 140.0, t * 220.0));
        float star = step(0.985, VcHash21(sc)) * smoothstep(0.15, 0.5, t) * _VcNight;
        emit = sky + star;
        c = 0;
        unlit = true;
    }
    else if (code == 3)
    {
        emit = _VcPowerCyan.rgb * (0.5 + 1.2 * _VcGridFlow) * lerp(0.5, 1.5, _VcNight);
        c *= 0.4;
    }
    else if (code == 13)
    {
        float lit = step(i.uvs.x, _VcBatteryCharge);
        emit = half3(0.45, 1.0, 0.62) * lit * lerp(0.6, 1.6, _VcNight);
        c = lerp(half3(0.12, 0.14, 0.16), c * 0.4, lit);
    }
    else if (code == 11)
    {
        c = c * lerp(1.0, 0.55, _VcNight);
        emit = _VcWindowWarm.rgb * 0.06 * _VcNight;
        rough = 1.0;
        spec = 0.0;
    }
    else if (code == 1)
    {
        c *= 0.85 + 0.3 * tf.g;
    }
    else if (code == 18)
    {
        rough = 0.35;
        spec = 0.5;
        c *= lerp(1.0, 0.72, _VcNight);
    }
    else
    {
        c *= (0.94 + 0.1 * tf.r) * lerp(1.0, 0.82, _VcWet);
        if (kind == 1)
            c *= lerp(1.0, lerp(0.62, 1.0, DistrictOn(i.uvs.x)), _VcNight);
    }

    half3 col;
    if (unlit)
    {
        col = emit;
    }
    else
    {
#if defined(VC_LOW)
        // water takes the light's brightness, mostly not its hue: the low sun at dusk is orange (1, .56, .30) and a
        // per-vertex diffuse turned the teal river brown-orange on Low (device pass 10-28); the sky emission above still
        // tints it. A few ALU on the existing path — no variant, no pass, no draw.
        half3 lit = i.light;
        if (water) lit = lerp(lit, dot(lit, half3(0.299, 0.587, 0.114)).xxx, 0.8);
        col = c * lit + emit;
#else
        float3 n = normalize(i.normalWS);
        float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
        Light ml = GetMainLight(shadowCoord);
        half ndl = saturate(dot(n, ml.direction));
        // water: the sun's brightness, not its hue (same rule as Low)
        half3 sunCol = water ? lerp(ml.color, dot(ml.color, half3(0.299, 0.587, 0.114)).xxx, 0.8) : ml.color;
        half3 diffuse = c * (sunCol * ndl * ml.shadowAttenuation + SampleSH(n));
        float3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));
        float3 hv = normalize(ml.direction + vdir);
        half sp = pow(saturate(dot(n, hv)), lerp(8.0, 96.0, 1.0 - rough)) * spec * ml.shadowAttenuation;
        col = diffuse + ml.color * sp + emit;
#endif
    }
    col = MixFog(col, i.fogFactor);
    return half4(col, 1.0);
}

// ---------------------------------------------------------------------------------------------- shadow / depth (Medium)
float3 _LightDirection;

struct ShadowVaryings
{
    float4 positionCS : SV_POSITION;
};

ShadowVaryings VcWorldShadowVert(Attributes v)
{
    ShadowVaryings o;
    float3 p = v.positionOS.xyz;
    int code = VcCode(v.color.a);
    int kind = (int)(_MeshKind + 0.5);
    if ((kind == 2 && v.uv.x > -0.5 && _VcCorridorState[clamp((int)(v.uv.x + 0.5), 0, 15)].x > 2.5) ||
        (kind == 3 && code == 11 && _VcPlantOn[clamp((int)v.uv.y, 0, 5)] < 0.04) || code == 4)
        p = 0;
    float3 ws = TransformObjectToWorld(p);
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

ShadowVaryings VcWorldDepthVert(Attributes v)
{
    ShadowVaryings o;
    o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
    return o;
}

half4 VcNullFrag(ShadowVaryings i) : SV_Target
{
    return 0;
}

#endif
