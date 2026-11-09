// The tutorial's highlight: a pulsing shell just outside a mesh, brightest at its silhouette. Additive, so it reads on the
// white void and on dark props alike. Drawn by TutorialGlow on a copy of each highlighted mesh. See docs/tutorial.md.
Shader "GrowAGarden/TutorialGlow"
{
    Properties
    {
        [HDR]_Color ("Color", Color) = (0.25, 0.75, 1.6, 1)
        _Width ("Shell width (m)", Range(0, 0.1)) = 0.015
        _Pulse ("Pulses per second", Range(0, 4)) = 1.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width, _Pulse;
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
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz) + normalWS * _Width;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = normalWS;
                o.viewWS = GetCameraPositionWS() - positionWS;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float rim = 1 - saturate(abs(dot(normalize(i.normalWS), normalize(i.viewWS))));
                float pulse = 0.55 + 0.45 * sin(_Time.y * _Pulse * 6.2831853);
                return half4(_Color.rgb * (0.25 + rim * rim * 1.5) * pulse, 1);
            }
            ENDHLSL
        }
    }
}
