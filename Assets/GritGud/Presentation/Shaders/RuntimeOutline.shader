Shader "GritGud/RuntimeOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.006, 0.012, 0.025, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.1)) = 0.028
        [Toggle] _OutlineEnabled ("Outline Enabled", Float) = 1
        [Toggle] _PlayerCutoutEnabled ("Player Occlusion Cutout", Float) = 0
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
                half _OutlineEnabled;
                half _PlayerCutoutEnabled;
            CBUFFER_END

            #include "PlayerOcclusionCutout.hlsl"

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 expandedPositionOS = input.positionOS.xyz
                    + (normalize(input.normalOS) * _OutlineWidth);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(expandedPositionOS);
                output.positionHCS = positionInputs.positionCS;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.viewDepth = -positionInputs.positionVS.z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(_OutlineEnabled - 0.5h);
                ClipPlayerOcclusion(
                    input.positionHCS,
                    input.viewDepth,
                    _PlayerCutoutEnabled);
                half3 color = MixFog(_OutlineColor.rgb, input.fogFactor);
                return half4(color, _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
