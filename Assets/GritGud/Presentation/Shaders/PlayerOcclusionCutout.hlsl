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

float SamplePlayerSilhouette(float2 screenUV)
{
    float2 texel = _GritGudPlayerSilhouetteMask_TexelSize.xy * 0.75;
    float coverage = SAMPLE_TEXTURE2D(
        _GritGudPlayerSilhouetteMask,
        sampler_GritGudPlayerSilhouetteMask,
        screenUV).a;
    coverage = max(coverage, SAMPLE_TEXTURE2D(
        _GritGudPlayerSilhouetteMask,
        sampler_GritGudPlayerSilhouetteMask,
        screenUV + float2(texel.x, 0.0)).a);
    coverage = max(coverage, SAMPLE_TEXTURE2D(
        _GritGudPlayerSilhouetteMask,
        sampler_GritGudPlayerSilhouetteMask,
        screenUV - float2(texel.x, 0.0)).a);
    coverage = max(coverage, SAMPLE_TEXTURE2D(
        _GritGudPlayerSilhouetteMask,
        sampler_GritGudPlayerSilhouetteMask,
        screenUV + float2(0.0, texel.y)).a);
    coverage = max(coverage, SAMPLE_TEXTURE2D(
        _GritGudPlayerSilhouetteMask,
        sampler_GritGudPlayerSilhouetteMask,
        screenUV - float2(0.0, texel.y)).a);
    return coverage;
}

void ClipPlayerOcclusion(
    float4 positionHCS,
    float viewDepth,
    half cutoutEnabled,
    half ovalEnabled)
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
    offset.x += min(
        max(-offset.x, 0.0),
        _GritGudPlayerCutoutLeftExtension);
    float2 normalizedOffset = float2(
        offset.x / _GritGudPlayerCutout.z,
        offset.y / _GritGudPlayerCutoutVerticalRadius);
    float distanceFromPlayer = length(normalizedOffset);
    float feather = 0.2;
    float ovalRetainedCoverage = smoothstep(
        1.0 - feather,
        1.0,
        distanceFromPlayer);
    if (ovalRetainedCoverage >= 1.0)
    {
        return;
    }

    float cutoutCoverage = 1.0 - ovalRetainedCoverage;
    if (ovalEnabled < 0.5h)
    {
        cutoutCoverage *= SamplePlayerSilhouette(screenUV);
    }

    float retainedCoverage = 1.0 - cutoutCoverage;
    clip(retainedCoverage - PlayerCutoutNoise(positionHCS.xy));
}

#endif
