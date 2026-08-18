#ifndef GRIT_GUD_PLAYER_OCCLUSION_CUTOUT_INCLUDED
#define GRIT_GUD_PLAYER_OCCLUSION_CUTOUT_INCLUDED

float4 _GritGudPlayerCutout;
float _GritGudPlayerCutoutLeftExtension;
float _GritGudPlayerCutoutVerticalRadius;
float3 _GritGudPlayerCutoutRayStart;
float3 _GritGudPlayerCutoutRayEnd;
float3 _GritGudPlayerCutoutCameraRight;
float3 _GritGudPlayerCutoutCameraUp;
float4 _GritGudPlayerCutoutCorridorWidths;

float PlayerCutoutNoise(float2 pixelPosition)
{
    return frac(52.9829189 * frac(dot(pixelPosition, float2(0.06711056, 0.00583715))));
}

void ClipPlayerOcclusionAtScreenUV(
    float2 screenUV,
    float2 pixelPosition,
    float3 positionWS,
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

    float3 corridor = _GritGudPlayerCutoutRayEnd
        - _GritGudPlayerCutoutRayStart;
    float corridorLengthSquared = dot(corridor, corridor);
    float corridorProgress = dot(
        positionWS - _GritGudPlayerCutoutRayStart,
        corridor) / max(corridorLengthSquared, 0.0001);
    if (corridorProgress <= 0.0 || corridorProgress >= 1.0)
    {
        return;
    }

    float3 corridorCenter = _GritGudPlayerCutoutRayStart
        + corridor * corridorProgress;
    float lateralDistance = abs(dot(
        positionWS - corridorCenter,
        _GritGudPlayerCutoutCameraRight));
    float corridorHalfWidth = lerp(
        _GritGudPlayerCutoutCorridorWidths.x,
        _GritGudPlayerCutoutCorridorWidths.y,
        corridorProgress);
    if (lateralDistance >= corridorHalfWidth)
    {
        return;
    }

    float verticalDistance = abs(dot(
        positionWS - corridorCenter,
        _GritGudPlayerCutoutCameraUp));
    float corridorHalfHeight = lerp(
        _GritGudPlayerCutoutCorridorWidths.z,
        _GritGudPlayerCutoutCorridorWidths.w,
        corridorProgress);
    if (verticalDistance >= corridorHalfHeight)
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
    float3 positionWS,
    float viewDepth,
    half cutoutEnabled)
{
    ClipPlayerOcclusionAtScreenUV(
        GetNormalizedScreenSpaceUV(positionHCS),
        positionHCS.xy,
        positionWS,
        viewDepth,
        cutoutEnabled);
}

#endif
