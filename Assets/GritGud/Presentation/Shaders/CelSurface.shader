Shader "GritGud/CelSurface"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Color ("Legacy Color", Color) = (1, 1, 1, 1)
        _CelThreshold ("Cel Threshold", Range(0, 1)) = 0.48
        _CelSoftness ("Cel Softness", Range(0.001, 0.25)) = 0.035
        _ShadowColor ("Shadow Tint", Color) = (0.34, 0.42, 0.55, 1)
        _ShadowStrength ("Shadow Light", Range(0, 1)) = 0.44
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.9
        _OutlineColor ("Silhouette Color", Color) = (0.008, 0.015, 0.03, 1)
        _OutlineWidth ("Silhouette Width", Range(0, 0.5)) = 0.13
        _OutlineSoftness ("Silhouette Softness", Range(0.001, 0.3)) = 0.075
        _Smoothness ("Surface Smoothness", Range(0, 1)) = 0.15
        _SpecularStrength ("Specular Strength", Range(0, 1)) = 0.06
        _SpecularColor ("Specular Color", Color) = (0.8, 0.9, 1, 1)
        _EdgeSheenStrength ("Edge Sheen Strength", Range(0, 1)) = 0
        [Toggle] _TerrainSlopeEnabled ("Terrain Slope Tint", Float) = 0
        _SteepColor ("Terrain Steep Color", Color) = (0.2, 0.2, 0.2, 1)
        _SlopeBlendStartCos ("Terrain Slope Start Cosine", Float) = 0.848
        _SlopeBlendEndCos ("Terrain Slope End Cosine", Float) = 0.53
        [Toggle] _TerrainDiagnosticsEnabled ("Terrain Diagnostics", Float) = 0
        _DiagnosticSlopeCos ("Diagnostic Slope Cosine", Float) = 0.707
        [Toggle] _PlayerCutoutEnabled ("Player Occlusion Cutout", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
        }

        Pass
        {
            Name "CelForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Color;
                half4 _ShadowColor;
                half4 _OutlineColor;
                half4 _SpecularColor;
                half _CelThreshold;
                half _CelSoftness;
                half _ShadowStrength;
                half _AmbientStrength;
                half _OutlineWidth;
                half _OutlineSoftness;
                half _Smoothness;
                half _SpecularStrength;
                half _EdgeSheenStrength;
                half4 _SteepColor;
                half _TerrainSlopeEnabled;
                half _SlopeBlendStartCos;
                half _SlopeBlendEndCos;
                half _TerrainDiagnosticsEnabled;
                half _DiagnosticSlopeCos;
                half _PlayerCutoutEnabled;
            CBUFFER_END

            #include "PlayerOcclusionCutout.hlsl"

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half CelBand(half normalDotLight)
            {
                return smoothstep(
                    _CelThreshold - _CelSoftness,
                    _CelThreshold + _CelSoftness,
                    normalDotLight);
            }

            half3 EvaluateCelLight(Light light, half3 normalWS)
            {
                half band = CelBand(saturate(dot(normalWS, light.direction)));
                half attenuation =
                    light.distanceAttenuation * light.shadowAttenuation;
                half lightLevel = lerp(_ShadowStrength, 1.0h, band);
                return light.color * lightLevel * attenuation;
            }

            half3 EvaluateSpecular(
                Light light,
                half3 normalWS,
                half3 viewDirectionWS)
            {
                half3 halfDirection = SafeNormalize(
                    light.direction + viewDirectionWS);
                half exponent = lerp(10.0h, 92.0h, _Smoothness);
                half lobe = pow(
                    saturate(dot(normalWS, halfDirection)),
                    exponent);
                half attenuation =
                    light.distanceAttenuation * light.shadowAttenuation;
                return _SpecularColor.rgb * light.color * lobe
                    * _SpecularStrength * attenuation;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float viewDepth = -TransformWorldToView(input.positionWS).z;
                ClipPlayerOcclusion(
                    input.positionHCS,
                    viewDepth,
                    _PlayerCutoutEnabled);
                half3 normalWS = normalize(input.normalWS);
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = baseSample * _BaseColor;
                half slopeBlend = 1.0h - smoothstep(
                    _SlopeBlendEndCos,
                    _SlopeBlendStartCos,
                    saturate(normalWS.y));
                baseColor.rgb = lerp(
                    baseColor.rgb,
                    _SteepColor.rgb,
                    slopeBlend * saturate(_TerrainSlopeEnabled));
                half diagnosticSteep = 1.0h - smoothstep(
                    _DiagnosticSlopeCos - 0.04h,
                    _DiagnosticSlopeCos + 0.04h,
                    saturate(normalWS.y));
                half3 diagnosticColor = lerp(
                    half3(0.12h, 0.72h, 0.24h),
                    half3(0.95h, 0.12h, 0.06h),
                    diagnosticSteep);
                baseColor.rgb = lerp(
                    baseColor.rgb,
                    diagnosticColor,
                    saturate(_TerrainDiagnosticsEnabled));
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(
                    input.positionWS);
                half mainBand = CelBand(saturate(dot(normalWS, mainLight.direction)));
                mainBand *= mainLight.shadowAttenuation;

                half3 surfaceTint = lerp(
                    _ShadowColor.rgb,
                    half3(1.0h, 1.0h, 1.0h),
                    mainBand);
                half3 lighting = SampleSH(normalWS) * _AmbientStrength;
                lighting += EvaluateCelLight(mainLight, normalWS);
                half3 highlights = EvaluateSpecular(
                    mainLight,
                    normalWS,
                    viewDirectionWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV =
                    GetNormalizedScreenSpaceUV(input.positionHCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS);
                    lighting += EvaluateCelLight(additionalLight, normalWS);
                    highlights += EvaluateSpecular(
                        additionalLight,
                        normalWS,
                        viewDirectionWS);
                LIGHT_LOOP_END

                half3 shadedColor = baseColor.rgb * surfaceTint * lighting
                    + highlights;
                half facing = abs(dot(normalWS, viewDirectionWS));
                half edgeSheen = pow(1.0h - saturate(facing), 4.0h)
                    * _EdgeSheenStrength;
                shadedColor += _SpecularColor.rgb * edgeSheen;
                half silhouette = smoothstep(
                    _OutlineWidth,
                    _OutlineWidth + _OutlineSoftness,
                    facing);
                shadedColor = lerp(_OutlineColor.rgb, shadedColor, silhouette);
                shadedColor = MixFog(shadedColor, input.fogFactor);
                return half4(shadedColor, baseColor.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthOnlyAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthOnlyVaryings
            {
                float4 positionHCS : SV_POSITION;
                float viewDepth : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Color;
                half4 _ShadowColor;
                half4 _OutlineColor;
                half4 _SpecularColor;
                half _CelThreshold;
                half _CelSoftness;
                half _ShadowStrength;
                half _AmbientStrength;
                half _OutlineWidth;
                half _OutlineSoftness;
                half _Smoothness;
                half _SpecularStrength;
                half _EdgeSheenStrength;
                half4 _SteepColor;
                half _TerrainSlopeEnabled;
                half _SlopeBlendStartCos;
                half _SlopeBlendEndCos;
                half _TerrainDiagnosticsEnabled;
                half _DiagnosticSlopeCos;
                half _PlayerCutoutEnabled;
            CBUFFER_END

            #include "PlayerOcclusionCutout.hlsl"

            DepthOnlyVaryings DepthOnlyVertex(DepthOnlyAttributes input)
            {
                DepthOnlyVaryings output;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.viewDepth = -positionInputs.positionVS.z;
                return output;
            }

            half DepthOnlyFragment(DepthOnlyVaryings input) : SV_Target
            {
                ClipPlayerOcclusion(
                    input.positionHCS,
                    input.viewDepth,
                    _PlayerCutoutEnabled);
                return input.positionHCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
            };

            struct DepthNormalsVaryings
            {
                float4 positionHCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float viewDepth : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Color;
                half4 _ShadowColor;
                half4 _OutlineColor;
                half4 _SpecularColor;
                half _CelThreshold;
                half _CelSoftness;
                half _ShadowStrength;
                half _AmbientStrength;
                half _OutlineWidth;
                half _OutlineSoftness;
                half _Smoothness;
                half _SpecularStrength;
                half _EdgeSheenStrength;
                half4 _SteepColor;
                half _TerrainSlopeEnabled;
                half _SlopeBlendStartCos;
                half _SlopeBlendEndCos;
                half _TerrainDiagnosticsEnabled;
                half _DiagnosticSlopeCos;
                half _PlayerCutoutEnabled;
            CBUFFER_END

            #include "PlayerOcclusionCutout.hlsl"

            DepthNormalsVaryings DepthNormalsVertex(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.viewDepth = -positionInputs.positionVS.z;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(DepthNormalsVaryings input) : SV_Target
            {
                ClipPlayerOcclusion(
                    input.positionHCS,
                    input.viewDepth,
                    _PlayerCutoutEnabled);
#if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormal = PackNormalOctQuadEncode(normalize(input.normalWS));
                float2 remappedNormal = saturate(octNormal * 0.5 + 0.5);
                return half4(PackFloat2To888(remappedNormal), 0.0);
#else
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
#endif
            }
            ENDHLSL
        }
    }
}
