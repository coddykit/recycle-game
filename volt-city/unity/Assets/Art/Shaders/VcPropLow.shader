// Volt City instanced props — Low (per vertex, no shadows, no pulse / sway).
// Drawn with Graphics.RenderMeshInstanced (one draw per prop kind); enable GPU Instancing on every material.
Shader "VoltCity/PropLow"
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

            #define VC_LOW 1
            #include "VcPropCore.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
