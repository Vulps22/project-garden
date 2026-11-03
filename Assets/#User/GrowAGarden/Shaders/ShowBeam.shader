// A beam of light: additive, on an open tube or cone whose uv.y runs along its length (0 at the source) and uv.x
// around it. Bright down its middle as seen from anywhere, soft at its silhouette, fading toward its far end.
Shader "GrowAGarden/ShowBeam"
{
    Properties
    {
        [HDR]_Color ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1
        _Falloff ("Fade along length", Range(0, 8)) = 1
        _Softness ("Silhouette softness", Range(0.5, 8)) = 2
        _Pulses ("Pulse ages (s)", Vector) = (99, 99, 99, 99)
        _PulseSpeed ("Pulse speed (lengths per second)", Float) = 1.3
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float facing : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 _Color;
            float _Intensity, _Falloff, _Softness, _PulseSpeed;
            float4 _Pulses;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                o.facing = abs(dot(n, normalize(_WorldSpaceCameraPos - world)));
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float along = pow(saturate(1 - i.uv.y), _Falloff);
                float core = pow(saturate(i.facing), _Softness);
                float pulse = 0;
                for (int k = 0; k < 4; k++)
                    pulse += exp(-abs(i.uv.y - _Pulses[k] * _PulseSpeed) * 18) * step(_Pulses[k] * _PulseSpeed, 1.1);
                // A pulse also lifts the far end's fade, so it arrives at the star still bright.
                return half4(_Color.rgb * (_Intensity * core * (along + pulse * 2.5)), 1);
            }
            ENDCG
        }
    }
}
