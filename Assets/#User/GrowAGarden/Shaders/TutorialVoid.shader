// The tutorial's white void: near-white everywhere, a faint blue grid in world space, and bright lines that sweep along
// it. Unlit and unfogged. Inside of the sky sphere (Cull Front) and the floor (Cull Back). See docs/tutorial.md.
Shader "GrowAGarden/TutorialVoid"
{
    Properties
    {
        _Base ("Base", Color) = (0.95, 0.97, 1, 1)
        [HDR]_Line ("Line", Color) = (0.35, 0.7, 1.4, 1)
        _Cell ("Grid cell (m)", Float) = 2
        _Thickness ("Line thickness (share of a cell)", Range(0.001, 0.2)) = 0.02
        _GridStrength ("Grid strength", Range(0, 1)) = 0.35
        _SweepSpeed ("Sweep speed (cells/s)", Float) = 3
        _Fade ("Grid fade distance (m)", Float) = 60
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Base;
                half4 _Line;
                float _Cell, _Thickness, _GridStrength, _SweepSpeed, _Fade, _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            // 1 on a grid line along each axis of a 2D cell coordinate, 0 between them.
            float Lines(float2 cell)
            {
                float2 d = abs(frac(cell) - 0.5);
                float2 on = smoothstep(0.5 - _Thickness, 0.5, d);
                return max(on.x, on.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = abs(normalize(i.normalWS));
                float3 c = i.positionWS / _Cell;
                // Triplanar: each face takes the grid of the two axes across it.
                float grid = Lines(c.yz) * n.x + Lines(c.xz) * n.y + Lines(c.xy) * n.z;
                float sweep = pow(saturate(1 - abs(frac((c.x + c.z) * 0.125 - _Time.y * _SweepSpeed * 0.125) - 0.5) * 8), 3);
                float fade = saturate(1 - distance(i.positionWS, GetCameraPositionWS()) / _Fade);
                float amount = grid * (_GridStrength + sweep) * fade;
                return half4(lerp(_Base.rgb, _Line.rgb, saturate(amount)), 1);
            }
            ENDHLSL
        }
        // Depth for URP's prepass: with depth priming, an opaque without one is never drawn.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half Frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
