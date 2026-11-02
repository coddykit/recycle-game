// Volt City instanced props — Medium / High (per pixel, shadows, marker pulse, tree sway).
// Drawn with Graphics.RenderMeshInstanced (one draw per prop kind); enable GPU Instancing on every material.
Shader "VoltCity/Prop"
{
    Properties
    {
        [Enum(Truck,0,Car,1,Marker,2,Tree,3)] _PropKind ("Prop kind", Float) = 0
        _LiveryA ("Livery primary", Color) = (0.95, 0.72, 0.12, 1)
        _LiveryB ("Livery secondary", Color) = (0.77, 0.56, 0.07, 1)
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
            #pragma vertex VcPropVert
            #pragma fragment VcPropFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "VcPropCore.hlsl"
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
            #pragma vertex VcPropShadowVert
            #pragma fragment VcPropNullFrag
            #pragma multi_compile_instancing
            #include "VcPropCore.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
