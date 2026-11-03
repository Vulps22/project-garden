// The fence panels as the equaliser: an additive layer drawn over a fence's own material (a second material on the
// renderer). Each panel is a whole equaliser in its own object space: frequency runs along it (bass at its left end),
// and its pickets light up in rows of LED cells, from the bottom to the spectrum's level (ARS_Texture1), green through
// yellow to magenta, with a peak cap held from the spectrum's recent history. Under the dome only (Show_Night), where
// the fences help light the Garden; nothing by day, or when the music is silent. See docs/light-show.md.
Shader "GrowAGarden/ShowFenceEq"
{
    Properties
    {
        _Along ("Panel start and length along x (object space)", Vector) = (-2.58, 2.63, 0, 0)
        _Rise ("Panel bottom and height along y (object space)", Vector) = (-0.05, 1.47, 0, 0)
        _Cells ("LED rows", Float) = 10
        _Gain ("Gain", Range(0, 8)) = 2.4
        _Day ("Brightness by day: 0, under the dome only", Range(0, 2)) = 0
        _Night ("Brightness at night", Range(0, 4)) = 1.6
        [HDR]_Low ("Bottom rows", Color) = (0.2, 1.4, 0.4, 1)
        [HDR]_Mid ("Middle rows", Color) = (1.5, 1.2, 0.2, 1)
        [HDR]_Top ("Top rows", Color) = (1.6, 0.2, 1.2, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 panel : TEXCOORD0;   // x along 0..1, y up 0..1
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D ARS_Texture1;
            float Show_Kick, Show_Night;
            float4 _Along, _Rise;
            float _Cells, _Gain, _Day, _Night;
            half4 _Low, _Mid, _Top;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.panel = float2((v.vertex.x - _Along.x) / _Along.y, (v.vertex.y - _Rise.x) / _Rise.y);
                return o;
            }

            float level(float f, float age) { return saturate(tex2Dlod(ARS_Texture1, float4(f, age, 0, 0)).g * _Gain); }

            half4 frag(v2f i) : SV_Target
            {
                float f = pow(saturate(i.panel.x), 1.7) * 0.6;
                float y = saturate(i.panel.y);
                float row = floor(y * _Cells) / _Cells;
                float gap = step(0.15, frac(y * _Cells));   // dark lines between rows: LED cells
                float now = level(f, 0);
                float peak = now;
                for (int k = 1; k < 8; k++) peak = max(peak, level(f, k / 64.0));

                half3 ramp = row < 0.5 ? lerp(_Low.rgb, _Mid.rgb, row * 2) : lerp(_Mid.rgb, _Top.rgb, row * 2 - 1);
                float lit = step(row, now - 0.001) * gap;
                float cap = (abs(row - floor(peak * _Cells) / _Cells) < 0.5 / _Cells) * step(0.02, peak) * gap;
                half3 c = ramp * lit * (1 + Show_Kick * 0.5) + half3(1.2, 1.2, 1.4) * cap * (1 - lit);
                return half4(c * lerp(_Day, _Night, Show_Night), 1);
            }
            ENDCG
        }
    }
}
