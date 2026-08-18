#ifndef GRIT_GUD_PLAYER_OCCLUSION_CUTOUT_INCLUDED
#define GRIT_GUD_PLAYER_OCCLUSION_CUTOUT_INCLUDED

float4 _GritGudPlayerCutout;
float _GritGudPlayerCutoutLeftExtension;
float _GritGudPlayerCutoutVerticalRadius;
TEXTURE2D(_GritGudPlayerSilhouetteMask);
SAMPLER(sampler_GritGudPlayerSilhouetteMask);
float4 _GritGudPlayerSilhouetteMask_TexelSize;

float PlayerCutoutNoise(float2 pixelPosition)
{
    return frac(52.9829189 * frac(dot(pixelPosition, float2(0.06711056, 0.00583715))));
}

float SampleSilhouetteAlpha(float2 screenUV)
{
    return SAMPLE_TEXTURE2D(
        _GritGudPlayerSilhouetteMask,
        sampler_GritGudPlayerSilhouetteMask,
        screenUV).a;
}

float SampleSilhouetteRing(float2 screenUV, float2 radius)
{
    float2 diagonal = radius * 0.70710678;
    float coverage = SampleSilhouetteAlpha(
        screenUV + float2(radius.x, 0.0));
    coverage = max(coverage, SampleSilhouetteAlpha(
        screenUV - float2(radius.x, 0.0)));
    coverage = max(coverage, SampleSilhouetteAlpha(
        screenUV + float2(0.0, radius.y)));
    coverage = max(coverage, SampleSilhouetteAlpha(
        screenUV - float2(0.0, radius.y)));
    coverage = max(coverage, SampleSilhouetteAlpha(
        screenUV + diagonal));
    coverage = max(coverage, SampleSilhouetteAlpha(
        screenUV - diagonal));
    coverage = max(coverage, SampleSilhouetteAlpha(
        screenUV + float2(diagonal.x, -diagonal.y)));
    coverage = max(coverage, SampleSilhouetteAlpha(
        screenUV + float2(-diagonal.x, diagonal.y)));
    return coverage;
}

float SamplePlayerSilhouette(float2 screenUV)
{
    float2 texel = _GritGudPlayerSilhouetteMask_TexelSize.xy;
    float coverage = SampleSilhouetteAlpha(screenUV);
    coverage = max(
        coverage,
        SampleSilhouetteRing(screenUV, texel * 1.25) * 0.86);
    coverage = max(
        coverage,
        SampleSilhouetteRing(screenUV, texel * 2.25) * 0.58);
    coverage = max(
        coverage,
        SampleSilhouetteRing(screenUV, texel * 3.25) * 0.30);
    return coverage;
}

void ClipPlayerOcclusion(
    float4 positionHCS,
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

    float2 screenUV = GetNormalizedScreenSpaceUV(positionHCS);
    float2 offset = screenUV - _GritGudPlayerCutout.xy;
    // Keep the reveal close to the character silhouette. The broader ellipse
    // remains only as a safety bound around the player-only render mask.
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
    if (coverage >= 1.0)
    {
        return;
    }

    float playerSilhouette = SamplePlayerSilhouette(screenUV);
    if (playerSilhouette <= 0.001)
    {
        return;
    }

    float retainedCoverage = 1.0
        - (playerSilhouette * (1.0 - coverage));
    clip(retainedCoverage - PlayerCutoutNoise(positionHCS.xy));
}

#endif
