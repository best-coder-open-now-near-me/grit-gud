Shader "GritGud/ContactGrounding"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0.3)
        _EdgeSoftness ("Edge Softness", Range(0.01, 1)) = 0.64
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-20"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _EdgeSoftness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half radius = length((input.uv - 0.5h) * 2.0h);
                half alpha = 1.0h - smoothstep(
                    1.0h - _EdgeSoftness,
                    1.0h,
                    radius);
                return half4(_Color.rgb, _Color.a * alpha);
            }
            ENDHLSL
        }
    }
}
