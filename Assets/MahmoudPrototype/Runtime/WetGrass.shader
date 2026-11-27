Shader "Butterfly Effect/Wet Grass"
{
    Properties
    {
        _WetAmount ("Soaked fraction", Range(0, 1)) = 1
        [HideInInspector] _Impact ("Last water contact", Vector) = (0, 0, -1, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            // Tint the colour already on the ground so blades and shadows show through.
            Blend DstColor OneMinusSrcAlpha
            ColorMask RGB
            ZWrite Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _WetAmount;
                float4 _Impact;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                // Bias depth without moving the visible wet edge when the terrain changes LOD.
                float3 towardEye = GetWorldSpaceNormalizeViewDir(world);
                float4 nearer = TransformWorldToHClip(world + 0.03 * towardEye);
                o.positionCS.z = nearer.z * o.positionCS.w / nearer.w;
                o.positionWS = world;
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p = i.positionWS.xz;
                float irregularity = 0.09 * sin(9.3 * p.x + 3.1 * p.y)
                                   + 0.06 * sin(10.7 * p.y - 5.5 * p.x)
                                   + 0.035 * sin(26.4 * p.x + 18.1 * p.y);
                float radius = (0.10 + 0.66 * sqrt(_WetAmount)) * (1.0 + irregularity);
                float mask = 1.0 - smoothstep(radius * 0.68, radius * 1.10, length(i.uv));
                half opacity = mask * saturate(_WetAmount * 4.0);
                // Damp blades catch more cool sky light at shallow viewing angles.
                half grazing = 1.0h - saturate(GetWorldSpaceNormalizeViewDir(i.positionWS).y);
                half mottling = 0.5h + 0.5h * sin(13.7 * p.y + 2.1 * sin(9.2 * p.x));
                half sheen = saturate(grazing * grazing + 0.28h * mottling);
                half3 damp = half3(0.25h, 0.38h, 0.64h) + sheen * half3(0.16h, 0.25h, 0.52h);
                // A little reflected light travels out from the falling water and fades after release.
                float contact = length(p - _Impact.xy);
                float phase = frac(_Time.y * 2.2);
                float ringDistance = abs(contact - lerp(0.025, 0.19, phase));
                half ripple = (1.0h - smoothstep(0.005, 0.025, ringDistance)) * (1.0h - phase);
                half splash = 0.55h * (1.0h - smoothstep(0.012, 0.050, contact));
                half active = 1.0h - smoothstep(0.0, 0.35, _Time.y - _Impact.z);
                half glint = active * (splash + ripple * (0.55h + 0.45h * mottling));
                return half4(damp * opacity + glint * half3(0.85h, 1.65h, 2.65h), opacity);
            }
            ENDHLSL
        }
    }
}
