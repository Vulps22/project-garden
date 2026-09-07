// A painted three-color gradient sky, not a physically simulated one. Skybox/Procedural draws a
// flat, hard-edged "ground" half with no gradient at all — it assumes a real ground plane under
// the camera and was never meant to be looked down into. This shader has no ground/sky split to
// begin with: zenith, horizon and nadir blend continuously, so there is no seam to hide.
Shader "Skybox/GrowAGardenGradient"
{
    Properties
    {
        [HDR]_TopColor     ("Zenith Color", Color) = (0.35, 0.55, 0.85, 1)
        [HDR]_HorizonColor ("Horizon Color", Color) = (0.78, 0.85, 0.92, 1)
        [HDR]_BottomColor  ("Nadir Color", Color)   = (0.55, 0.68, 0.8, 1)
        _HorizonHeight  ("Horizon Height", Range(-0.5, 0.5)) = 0.0
        _TopExponent    ("Top Blend Softness", Range(0.2, 8)) = 1.2
        _BottomExponent ("Bottom Blend Softness", Range(0.2, 8)) = 1.5

        [HDR]_SunColor ("Sun Glow Color", Color) = (1, 0.97, 0.85, 1)
        _SunSize    ("Sun Glow Size", Range(0.001, 0.5)) = 0.06
        _SunFalloff ("Sun Glow Falloff", Range(1, 64)) = 12
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            half4 _TopColor;
            half4 _HorizonColor;
            half4 _BottomColor;
            half _HorizonHeight;
            half _TopExponent;
            half _BottomExponent;

            half4 _SunColor;
            half _SunSize;
            half _SunFalloff;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // The skybox is drawn as a large cube around the camera, so the object-space
                // vertex position already points in the right direction from the center.
                o.dir = v.vertex.xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                half h = dir.y - _HorizonHeight;

                half3 col;
                if (h >= 0)
                {
                    half t = saturate(pow(h, 1.0 / _TopExponent));
                    col = lerp(_HorizonColor.rgb, _TopColor.rgb, t);
                }
                else
                {
                    half t = saturate(pow(-h, 1.0 / _BottomExponent));
                    col = lerp(_HorizonColor.rgb, _BottomColor.rgb, t);
                }

                // _WorldSpaceLightPos0 is a built-in-pipeline global, but Unity keeps populating
                // it for the skybox pass under URP too — Skybox/Procedural relies on the same
                // thing, and that shader already works correctly in this project.
                float3 sunDir = normalize(_WorldSpaceLightPos0.xyz);
                half sunAmount = saturate(dot(dir, sunDir));
                half glow = pow(sunAmount, _SunFalloff / max(_SunSize, 0.001));
                col += _SunColor.rgb * glow;

                return half4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
