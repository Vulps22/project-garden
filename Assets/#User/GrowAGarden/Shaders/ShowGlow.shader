// A soft pool of coloured light on a quad: additive, brightest at the centre, gone at the rim. The stalls' glow on the
// ground, coloured and driven per stall by StallGroove; bigger by night (Show_Night). See docs/light-show.md.
Shader "GrowAGarden/ShowGlow"
{
    Properties
    {
        [HDR]_Color ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1
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
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 _Color;
            float _Intensity, Show_Night;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv - 0.5) * 2;
                float pool = pow(saturate(1 - r), 2.2);
                return half4(_Color.rgb * _Intensity * pool * lerp(0.35, 1, Show_Night), 1);
            }
            ENDCG
        }
    }
}
