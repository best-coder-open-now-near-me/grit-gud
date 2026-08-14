#ifndef GRIT_GUD_PLAYER_OCCLUSION_CUTOUT_INCLUDED
#define GRIT_GUD_PLAYER_OCCLUSION_CUTOUT_INCLUDED

float4 _GritGudPlayerCutout;
float _GritGudPlayerCutoutLeftExtension;

float PlayerCutoutNoise(float2 pixelPosition)
{
    return frac(52.9829189 * frac(dot(pixelPosition, float2(0.06711056, 0.00583715))));
}

void ClipPlayerOcclusion(
    float4 positionHCS,
    float viewDepth,
    half cutoutEnabled)
{
    if (cutoutEnabled < 0.5h
        || _GritGudPlayerCutout.z <= 0.0
        || viewDepth >= _GritGudPlayerCutout.w - 0.04)
    {
        return;
    }

    float2 screenUV = GetNormalizedScreenSpaceUV(positionHCS);
    float2 offset = screenUV - _GritGudPlayerCutout.xy;
    // Preserve the player-centered circle on the right while adding a short
    // viewport-space capsule segment toward the left-shoulder view corridor.
    offset.x += min(
        max(-offset.x, 0.0),
        _GritGudPlayerCutoutLeftExtension);
    offset.x *= _ScaledScreenParams.x / max(_ScaledScreenParams.y, 1.0);
    float distanceFromPlayer = length(offset);
    float feather = max(0.018, _GritGudPlayerCutout.z * 0.2);
    float coverage = smoothstep(
        _GritGudPlayerCutout.z - feather,
        _GritGudPlayerCutout.z,
        distanceFromPlayer);
    clip(coverage - PlayerCutoutNoise(positionHCS.xy));
}

#endif
