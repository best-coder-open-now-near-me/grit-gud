Shader "GritGud/RuntimeOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.006, 0.012, 0.025, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.1)) = 0.028
        [Toggle] _OutlineScreenSpace ("Screen-Space Outline", Float) = 0
        _OutlineScreenWidth ("Screen-Space Width (Pixels)", Range(0.5, 12)) = 4
        [Toggle] _OutlineEnabled ("Outline Enabled", Float) = 1
        [Toggle] _PlayerCutoutEnabled ("Player Occlusion Cutout", Float) = 0
        [HideInInspector] _PlayerCutoutOvalEnabled ("Player Cutout Oval", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
                float viewDepth : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _OutlineWidth;
                half _OutlineScreenSpace;
                half _OutlineScreenWidth;
                half _OutlineEnabled;
                half _PlayerCutoutEnabled;
                half _PlayerCutoutOvalEnabled;
            CBUFFER_END

            #include "PlayerOcclusionCutout.hlsl"

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs basePosition =
                    GetVertexPositionInputs(input.positionOS.xyz);
                if (_OutlineScreenSpace > 0.5h)
                {
                    float3 normalWS = TransformObjectToWorldNormal(
                        input.normalOS);
                    float2 normalVS = TransformWorldToViewDir(normalWS).xy;
                    float normalLength = length(normalVS);
                    if (normalLength > 0.0001f)
                    {
                        float2 pixelToClip = 2.0f / _ScreenParams.xy;
                        basePosition.positionCS.xy +=
                            (normalVS / normalLength)
                            * pixelToClip
                            * _OutlineScreenWidth
                            * basePosition.positionCS.w;
                    }
                    output.positionHCS = basePosition.positionCS;
                }
                else
                {
                    float3 expandedPositionOS = input.positionOS.xyz
                        + (normalize(input.normalOS) * _OutlineWidth);
                    output.positionHCS = GetVertexPositionInputs(
                        expandedPositionOS).positionCS;
                }
                output.fogFactor = ComputeFogFactor(
                    output.positionHCS.z);
                output.viewDepth = -basePosition.positionVS.z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(_OutlineEnabled - 0.5h);
                ClipPlayerOcclusion(
                    input.positionHCS,
                    input.viewDepth,
                    _PlayerCutoutEnabled,
                    _PlayerCutoutOvalEnabled);
                half3 foggedColor = MixFog(
                    _OutlineColor.rgb,
                    input.fogFactor);
                half3 color = lerp(
                    foggedColor,
                    _OutlineColor.rgb,
                    saturate(_OutlineScreenSpace));
                return half4(color, _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
