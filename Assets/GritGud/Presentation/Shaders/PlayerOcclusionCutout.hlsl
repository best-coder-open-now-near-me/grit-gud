#ifndef GRIT_GUD_PLAYER_OCCLUSION_CUTOUT_INCLUDED
#define GRIT_GUD_PLAYER_OCCLUSION_CUTOUT_INCLUDED

float4 _GritGudPlayerCutout;
float _GritGudPlayerCutoutLeftExtension;
float _GritGudPlayerCutoutVerticalRadius;

float PlayerCutoutNoise(float2 pixelPosition)
{
    return frac(52.9829189 * frac(dot(pixelPosition, float2(0.06711056, 0.00583715))));
}

void ClipPlayerOcclusionAtScreenUV(
    float2 screenUV,
    float2 pixelPosition,
    float viewDepth,
    half cutoutEnabled)
{
    if (cutoutEnabled < 0.5h
        || _GritGudPlayerCutout.z <= 0.0
        || _GritGudPlayerCutoutVerticalRadius <= 0.0
        || viewDepth >= _GritGudPlayerCutout.w - 0.04)
    {
        return;
    }

    float2 offset = screenUV - _GritGudPlayerCutout.xy;
    // Keep the reveal close to the character silhouette. The former large,
    // circular screen-space mask could reach sideways through unrelated walls
    // and expose neighboring rooms.
    offset.x += min(
        max(-offset.x, 0.0),
        _GritGudPlayerCutoutLeftExtension);
    float2 normalizedOffset = float2(
        offset.x / _GritGudPlayerCutout.z,
        offset.y / _GritGudPlayerCutoutVerticalRadius);
    float distanceFromPlayer = length(normalizedOffset);
    float feather = 0.2;
    float coverage = smoothstep(
        1.0 - feather,
        1.0,
        distanceFromPlayer);
    clip(coverage - PlayerCutoutNoise(pixelPosition));
}

void ClipPlayerOcclusion(
    float4 positionHCS,
    float viewDepth,
    half cutoutEnabled)
{
    ClipPlayerOcclusionAtScreenUV(
        GetNormalizedScreenSpaceUV(positionHCS),
        positionHCS.xy,
        viewDepth,
        cutoutEnabled);
}

#endif
