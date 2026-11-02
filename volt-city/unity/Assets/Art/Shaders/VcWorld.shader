// Volt City static world — Medium / High tier (per pixel, main-light shadows, specular, animated smoke puffs).
// Low tier = VcWorldLow.shader (same VcWorldCore.hlsl: the ripple / windows / corridor code is identical on every tier).
// Material instances (all this shader, SRP Batcher compatible): World_Terrain (_MeshKind 0), District_<id> x 8
// (_MeshKind 1, each its own _Ripple), World_Grid (2), World_Plants (3). City-wide values are globals (VcCommon.hlsl).
Shader "VoltCity/World"
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "VcWorldCore.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex VcWorldShadowVert
            #pragma fragment VcNullFrag
            #include "VcWorldCore.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex VcWorldDepthVert
            #pragma fragment VcNullFrag
            #include "VcWorldCore.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
