#ifndef VULPS_SPS_BEND_INCLUDED
#define VULPS_SPS_BEND_INCLUDED

// Set per material by Community Modules' SPSTrigger, in world space, while an SPS target is near.
float4 _Pivot;
float4 _End;
float4 _Target;
float _Enable;

// Rodrigues: v rotated by angle about unit axis k.
float3 SpsRotate(float3 v, float3 k, float angle)
{
    float c = cos(angle), s = sin(angle);
    return v * c + cross(k, v) * s + k * dot(k, v) * (1.0 - c);
}

// Bends the mesh from its base toward the target: nothing at the base, the full turn at the tip.
void SpsBend(inout float3 positionOS, inout float3 normalOS)
{
    if (_Enable < 0.5) return;

    float3 axis = _End.xyz - _Pivot.xyz;
    float len = length(axis);
    float3 toTarget = _Target.xyz - _Pivot.xyz;
    if (len < 1e-4 || dot(toTarget, toTarget) < 1e-8) return;

    float3 a = axis / len;
    float3 b = normalize(toTarget);
    float3 k = cross(a, b);
    float sinAngle = length(k);
    if (sinAngle < 1e-4) return;
    k /= sinAngle;
    float angle = atan2(sinAngle, dot(a, b));

    float3 wp = TransformObjectToWorld(positionOS);
    float3 d = wp - _Pivot.xyz;
    float t = saturate(dot(d, a) / len);
    float turn = angle * t;

    positionOS = TransformWorldToObject(_Pivot.xyz + SpsRotate(d, k, turn));
    float3 nw = TransformObjectToWorldNormal(normalOS);
    normalOS = TransformWorldToObjectNormal(SpsRotate(nw, k, turn));
}

#endif
