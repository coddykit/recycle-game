// Volt City static world — Low tier (games/CLAUDE.md section 10): per-vertex lighting (main light + SH in the vertex
// shader), no shadows, no specular, no animation, one forward pass (no shadow-caster pass: Low has shadows off, so
// the shadow pass can never add draws). Same VcWorldCore.hlsl as Medium = the same ripple branch on every tier.
Shader "VoltCity/WorldLow"
{
    Properties
    {
        [Enum(Terrain,0,District,1,Grid,2,Plants,3)] _MeshKind ("Mesh kind", Float) = 0
        _Ripple ("Ripple (front, target)", Vector) = (2, 1, 0, 0)
        [NoScaleOffset] _Surface ("Surface detail (R grain, G blotch, B streak)", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex VcWorldVert
            #pragma fragment VcWorldFrag
            #pragma multi_compile_fog
            #define VC_LOW 1
            #include "VcWorldCore.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
