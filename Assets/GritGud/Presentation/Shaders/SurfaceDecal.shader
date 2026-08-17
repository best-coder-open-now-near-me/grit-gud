Shader "GritGud/SurfaceDecal"
{
    Properties
    {
        _Color ("Color", Color) = (0.01, 0.01, 0.01, 0.65)
        _Style ("Style", Float) = 0
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
                half _Style;
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
                half alpha;
                if (_Style < 0.5h)
                {
                    half radius = length(centered);
                    half chippedEdge = 1.0h
                        + (sin(atan2(centered.y, centered.x) * 7.0h) * 0.07h);
                    alpha = 1.0h - smoothstep(chippedEdge - 0.16h, chippedEdge, radius);
                }
                else if (_Style < 1.5h)
                {
                    half angle = atan2(centered.y, centered.x);
                    half radius = length(half2(centered.x, centered.y * 1.35h));
                    half irregularEdge = 0.82h
                        + sin(angle * 5.0h) * 0.11h
                        + sin(angle * 9.0h + 1.7h) * 0.045h;
                    alpha = 1.0h - smoothstep(irregularEdge - 0.12h, irregularEdge, radius);
                }
                else if (_Style < 2.5h)
                {
                    half rectangularMask = 1.0h - smoothstep(
                        0.88h,
                        1.0h,
                        max(abs(centered.x), abs(centered.y)));
                    half stripe = step(0.34h, frac((input.uv.x + input.uv.y) * 5.0h));
                    alpha = rectangularMask * stripe;
                }
                else
                {
                    half shaft = (1.0h - smoothstep(0.16h, 0.22h, abs(centered.x)))
                        * step(-0.78h, centered.y)
                        * step(centered.y, 0.16h);
                    half headRange = step(0.02h, centered.y) * step(centered.y, 0.82h);
                    half headWidth = (0.82h - centered.y) * 0.86h;
                    half head = (1.0h - smoothstep(headWidth - 0.06h, headWidth, abs(centered.x)))
                        * headRange;
                    alpha = saturate(shaft + head);
                }
                return half4(_Color.rgb, _Color.a * alpha);
            }
            ENDHLSL
        }
    }
}
