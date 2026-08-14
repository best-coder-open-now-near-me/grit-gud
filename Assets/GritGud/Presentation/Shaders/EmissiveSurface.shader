Shader "GritGud/EmissiveSurface"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (0.02, 0.04, 0.08, 1)
        _EmissionColor ("Emission Color", Color) = (0.28, 0.72, 1, 1)
        _EmissionIntensity ("Emission Intensity", Range(0, 12)) = 5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+5"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Emissive"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                half _EmissionIntensity;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = _BaseColor.rgb
                    + (_EmissionColor.rgb * _EmissionIntensity);
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
