// The Moonflower's dome: a black sphere around the Garden with a light show on it, driven by the ARS spectrum
// (ARS_Texture1: x frequency, y history with row 0 now, r raw, g smoothed). Night spreads from _Apex, where the beam
// meets the dome, out to _Reveal metres. Aurora ribbons climb from the horizon, shaped by the spectrum and scrolling
// its history up the sky; stars twinkle on the highs; rings run out from the apex on bass kicks. See docs/moonflower.md.
Shader "GrowAGarden/ShowDome"
{
    Properties
    {
        _Centre ("Centre (world)", Vector) = (0, 0, 0, 0)
        _Apex   ("Apex (world)", Vector) = (0, 30, 0, 0)
        _Reveal ("Reveal radius from apex (m)", Float) = 1000
        _Edge   ("Reveal edge glow width (m)", Float) = 3
        _Bass   ("Bass level", Range(0, 1)) = 0
        _Mid    ("Mid level", Range(0, 1)) = 0
        _High   ("High level", Range(0, 1)) = 0
        _Rings  ("Kick ring ages (s)", Vector) = (99, 99, 99, 99)
        _Idle   ("Idle floor when quiet", Range(0, 1)) = 0.15
        [HDR]_AuroraLow  ("Aurora low", Color) = (0.1, 1.2, 0.6, 1)
        [HDR]_AuroraHigh ("Aurora high", Color) = (1.0, 0.2, 1.4, 1)
        [HDR]_RingColor  ("Ring", Color) = (0.6, 0.9, 1.6, 1)
        [HDR]_EdgeColor  ("Reveal edge", Color) = (0.8, 0.8, 1.5, 1)
    }

    SubShader
    {
        // Drawn just before the transparents, not with the opaques: URP's depth priming draws an opaque only where its
        // own depth pre-pass marked it, and this shader has none, so in the opaque queue it never appeared.
        Tags { "Queue" = "Transparent-1" "RenderType" = "Opaque" }
        Cull Off
        ZWrite On

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
                float3 world : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D ARS_Texture1;
            float4 _Centre, _Apex, _Rings;
            float _Reveal, _Edge, _Bass, _Mid, _High, _Idle;
            half4 _AuroraLow, _AuroraHigh, _RingColor, _EdgeColor;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + 1), u.x), u.y);
            }

            // The spectrum at frequency f (0 low, 1 high), age a (0 now, 1 oldest).
            float spectrum(float f, float a) { return tex2Dlod(ARS_Texture1, float4(f * 0.6, a, 0, 0)).g; }

            half4 frag(v2f i) : SV_Target
            {
                float fromApex = distance(i.world, _Apex.xyz);
                clip(_Reveal - fromApex);

                float3 dir = normalize(i.world - _Centre.xyz);
                float t = _Time.y;
                float az = atan2(dir.z, dir.x) / 6.2831853 + 0.5;
                float el = dir.y;
                half3 col = 0;

                // Aurora: curtains above the horizon. Frequency runs around the horizon, mirrored four times, so the
                // bass sits in four places; the spectrum's history climbs the curtain.
                float f = abs(frac(az * 2 + 0.03 * sin(t * 0.1)) * 2 - 1);
                float drift = noise(float2(az * 9 + t * 0.05, t * 0.07));
                float height = 0.12 + 0.55 * saturate(spectrum(f, 0) * 1.5 + _Idle * drift);
                float rise = saturate(el / height);
                float inCurtain = smoothstep(-0.02, 0.04, el) * (1 - smoothstep(0.75, 1.0, rise));
                float streaks = 0.35 + 0.65 * noise(float2(az * 160 + drift * 6, t * 0.6));
                float history = spectrum(f, rise * 0.9);
                float glow = inCurtain * streaks * (_Idle * (0.4 + drift) + history * 2.0 + _Mid * 0.6);
                col += lerp(_AuroraLow.rgb, _AuroraHigh.rgb, rise) * glow;

                // Stars: a 3D grid of cells over the sphere (a latitude grid pinches into streaks at the top); about
                // one cell in twelve holds a round star, twinkling faster and brighter on the highs.
                float3 p = dir * 90;
                float3 cell = floor(p);
                float h = hash(cell.xy + cell.z * 17.31);
                float dotSize = 1 - smoothstep(0.1, 0.22, length(frac(p) - 0.5));
                float star = step(0.92, h) * dotSize * (0.25 + 0.75 * saturate(sin(t * (2 + h * 8) + h * 40) * 0.5 + 0.5));
                col += star * (0.35 + _High * 2.5) * smoothstep(0.0, 0.15, el) * half3(0.9, 0.95, 1.2);

                // Kick rings: run from the apex across the dome, fading as they go.
                float3 apexDir = normalize(_Apex.xyz - _Centre.xyz);
                float ang = acos(clamp(dot(dir, apexDir), -1, 1));
                for (int k = 0; k < 4; k++)
                {
                    float age = _Rings[k];
                    float ring = exp(-abs(ang - age * 0.9) * 30) * saturate(1 - age / 3.5);
                    col += _RingColor.rgb * ring * 1.5;
                }

                // The front of the night as it spreads.
                col += _EdgeColor.rgb * saturate(1 - (_Reveal - fromApex) / max(_Edge, 0.01)) * step(_Reveal, 900);

                return half4(col, 1);
            }
            ENDCG
        }
    }
}
