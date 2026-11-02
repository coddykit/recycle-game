// Volt City wind rotors: ONE instanced batch for every turbine (Graphics.RenderMeshInstanced). The vertex shader spins
// each rotor about its local Z axis: angle = _Time.y * _InstCustom.x (rad/s, written only when the sim's wind speed
// changes) + _InstCustom.y (phase 0..2pi). Zero per-frame CPU on every tier. _InstColor stays white.
// Cosmetic blades_candy: _BladeStyle = 1 paints red / white tip bands. Two-sided (Cull Off), lit per vertex on every
// tier (18 triangles: per-pixel would buy nothing).
Shader "VoltCity/Blades"
{
    Properties
    {
        _BladeStyle ("Candy stripes", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "VcCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _BladeStyle;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(VcBlades)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstCustom)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstColor)
            UNITY_INSTANCING_BUFFER_END(VcBlades)

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; half3 col : TEXCOORD0; float r : TEXCOORD1; float fog : TEXCOORD2; };

            V Vert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                float4 cu = UNITY_ACCESS_INSTANCED_PROP(VcBlades, _InstCustom);
                half4 ic = UNITY_ACCESS_INSTANCED_PROP(VcBlades, _InstColor);
                float a = _Time.y * cu.x + cu.y;
                float ca = cos(a), sa = sin(a);
                float3 p = v.positionOS.xyz;
                o.r = length(p.xy);
                p.xy = float2(p.x * ca - p.y * sa, p.x * sa + p.y * ca);
                float3 n = v.normalOS;
                n.xy = float2(n.x * ca - n.y * sa, n.x * sa + n.y * ca);
                VertexPositionInputs pos = GetVertexPositionInputs(p);
                o.positionCS = pos.positionCS;
                float3 nws = TransformObjectToWorldNormal(n);
                Light ml = GetMainLight();
                half3 light = ml.color * abs(dot(nws, ml.direction)) + SampleSHVertex(nws);
                o.col = VcColourIn(v.color.rgb) * ic.rgb * light * lerp(1.0, 0.7, _VcNight);
                o.fog = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                float band = step(0.5, frac(i.r / 4.0)) * step(12.0, i.r) * _BladeStyle;
                half3 c = lerp(i.col, half3(0.82, 0.14, 0.12) * dot(i.col, 0.33), band);
                return half4(MixFog(c, i.fog), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
