#ifndef VULPS_TRIP_WOBBLE_INCLUDED
#define VULPS_TRIP_WOBBLE_INCLUDED

// Set by TripManager on the tripping player's client only: 0 at rest, 1 at the intense trip.
float _GAG_Trip;

// The world sags and wobbles: a slow droop downward that rolls across the world, and a gentle wobble on top.
// Object-space amounts, so a small crop moves a little and a great rock a lot. At 0 nothing moves.
float3 TripDisplace(float3 positionOS)
{
    if (_GAG_Trip <= 0.0001) return positionOS;

    float3 wp = TransformObjectToWorld(positionOS);
    float t = _Time.y;
    float3 wobble = float3(sin(t * 1.3 + wp.y * 1.7 + wp.z * 0.9),
                           sin(t * 1.1 + wp.x * 1.3 + wp.z * 1.6) * 0.5,
                           sin(t * 1.7 + wp.x * 0.8 + wp.y * 1.4));
    float droop = 0.5 + 0.5 * sin(t * 0.4 + wp.x * 0.35 + wp.z * 0.45);
    float3 down = TransformWorldToObjectDir(float3(0, -1, 0));
    return positionOS + _GAG_Trip * (wobble * 0.04 + down * droop * 0.08);
}

#endif
