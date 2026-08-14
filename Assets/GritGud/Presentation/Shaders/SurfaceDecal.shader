Shader "GritGud/SurfaceDecal"
{
    Properties
    {
        _Color ("Color", Color) = (0.01, 0.01, 0.01, 0.65)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

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
                half2 centered = (input.uv - 0.5h) * 2.0h;
                half radius = length(centered);
                half chippedEdge = 1.0h
                    + (sin(atan2(centered.y, centered.x) * 7.0h) * 0.07h);
                half alpha = 1.0h - smoothstep(chippedEdge - 0.16h, chippedEdge, radius);
                return half4(_Color.rgb, _Color.a * alpha);
            }
            ENDHLSL
        }
    }
}
