#ifndef GAG_SHOW_LIT_INCLUDED
#define GAG_SHOW_LIT_INCLUDED

// Props that join the light show under the dome (docs/fun-seeds.md → The Garden dances), lit like URP Lit.
// Keywords: _SHOW_PLANKS (a board's planks), _SHOW_TARP (bright vertex-coloured cloth), _SHOW_PIECES (a cut-up mesh).
// Every vertex also melts with the trip (TripWobble.hlsl), so these need no trip twins.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "TripWobble.hlsl"

TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_OcclusionMap);     SAMPLER(sampler_OcclusionMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _BumpScale;
    half _Smoothness;
    half _Metallic;
    half _OcclusionStrength;
    half4 _EmissionColor;
    half _Glow;
    float4 _PanelMin;
    float4 _PanelMax;
    float _Planks;
    float _Stripes;
    float4 _Wave;       // x front (0 at _WaveFrom, 1 at _WaveTo), y width, z 1 for a ring out from _WaveFrom, w strength
    float4 _WaveFrom;   // world space
    float4 _WaveTo;
    float _Reach;
CBUFFER_END

// Set by ShowManager every frame.
float4 Show_Levels;
float Show_Beat, Show_Kick, Show_Night;

half3 ShowHue(float h)
{
    return saturate(abs(frac(h + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
}

float ShowHash(float n) { return frac(sin(n * 91.345) * 47453.5453); }

// How far apart a piece centred at c (world) is: only where the pulse's front is passing; solid ahead and behind.
float ShowPieceAmount(float3 c)
{
    float3 span = _WaveTo.xyz - _WaveFrom.xyz;
    float t = _Wave.z > 0.5 ? length(c - _WaveFrom.xyz) / max(length(span), 0.001)
                            : dot(c - _WaveFrom.xyz, span) / max(dot(span, span), 0.001);
    float bump = 1.0 - smoothstep(0.0, max(_Wave.y, 0.001), abs(t - _Wave.x));
    return bump * _Wave.w * Show_Night;
}

float3 ShowRotate(float3 v, float3 axis, float angle)
{
    float s, c;
    sincos(angle, s, c);
    return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
}

// Moves one vertex of a piece, in world space, as the pulse passes: out along its way, shrunk about its centre, turned.
void ShowPieces(inout float3 positionWS, inout float3 normalWS, inout float3 tangentWS, float4 piece, float4 way, out float a)
{
    float3 centre = TransformObjectToWorld(piece.xyz);
    a = ShowPieceAmount(centre);
    if (a <= 0.0001) return;
    float3 dir = TransformObjectToWorldDir(way.xyz);
    float rnd = way.w;
    float3 axis = normalize(cross(float3(0, 1, 0), dir) + float3(rnd - 0.5, 0.3, 0.5 - frac(rnd * 7.0)));
    float angle = a * (0.2 + 0.5 * rnd) * (rnd > 0.5 ? 1.0 : -1.0);
    float3 local = ShowRotate(positionWS - centre, axis, angle) * (1.0 - 0.3 * a);
    positionWS = centre + local + dir * _Reach * a * (0.6 + 0.8 * rnd);
    normalWS = ShowRotate(normalWS, axis, angle);
    tangentWS = ShowRotate(tangentWS, axis, angle);
}

// Where a vertex ends up: melted by the trip, then broken apart.
void ShowVertex(float3 positionOS, float3 normalOS, float3 tangentOS, float4 piece, float4 way,
                out float3 positionWS, out float3 normalWS, out float3 tangentWS, out float a)
{
    positionWS = TransformObjectToWorld(TripDisplace(positionOS));
    normalWS = TransformObjectToWorldNormal(normalOS);
    tangentWS = TransformObjectToWorldDir(tangentOS);
    a = 0.0;
    #if defined(_SHOW_PIECES)
        ShowPieces(positionWS, normalWS, tangentWS, piece, way, a);
    #endif
}

// The show's colour on a surface: albedo is recoloured, emission added. positionOS is the undisplaced object position.
void ShowColour(float3 positionOS, half4 vertexColour, float a, float h, inout half3 albedo, inout half3 emission)
{
    float night = Show_Night;
    float beatStep = floor(Show_Beat);
    float kick = Show_Kick;
    half grain = dot(albedo, half3(0.3, 0.59, 0.11));

    #if defined(_SHOW_PLANKS)
        float3 o = positionOS;
        if (night > 0.0 && all(o >= _PanelMin.xyz) && all(o <= _PanelMax.xyz))
        {
            float t = saturate((o.y - _PanelMin.y) / (_PanelMax.y - _PanelMin.y)) * _Planks;
            float plank = min(floor(t), _Planks - 1.0);
            float seam = smoothstep(0.0, 0.04, frac(t)) * smoothstep(0.0, 0.04, 1.0 - frac(t));
            half3 colour = ShowHue((plank + beatStep) / _Planks);
            float level = Show_Levels[(uint)plank % 4u];
            albedo = lerp(albedo, colour * (0.2 + grain * 2.0), night * 0.7) * lerp(1.0, seam, night);
            emission += colour * seam * (0.15 + grain * 1.5) * (0.3 + 0.8 * level + 0.5 * kick) * night * _Glow;
        }
    #endif

    #if defined(_SHOW_TARP)
        half bright = max(vertexColour.r, max(vertexColour.g, vertexColour.b));
        if (night > 0.0 && bright > 0.7)
        {
            float stripe = fmod(floor(positionOS.x * _Stripes + 1000.0), 2.0);
            half3 colour = ShowHue(beatStep * 0.25 + stripe * 0.5);
            albedo = lerp(albedo, colour, night * 0.9);
            emission += colour * (0.15 + 0.5 * Show_Levels.y + 0.4 * kick) * night * _Glow;
        }
    #endif

    #if defined(_SHOW_PIECES)
        half3 party = ShowHue(h * 0.7 + Show_Beat * 0.125);
        emission += party * grain * (a * 1.5 + 0.1 * Show_Levels.x * night) * _Glow;
    #endif
}

#endif
