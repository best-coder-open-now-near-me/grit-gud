Shader "GritGud/TacticalWireframe"
{
    Properties
    {
        [MainColor] _BaseColor ("Projection Color", Color) = (0.12, 0.72, 1, 0.72)
        _LineColor ("Wire Color", Color) = (0.28, 0.84, 1, 0.96)
        _FillColor ("Fill Color", Color) = (0.02, 0.18, 0.32, 0.16)
        _LineThickness ("Line Thickness", Range(0.25, 3)) = 1.15
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.4
        _ScanScale ("Scan Scale", Range(0.5, 20)) = 6
        _ScanSpeed ("Scan Speed", Range(-10, 10)) = 1.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "TacticalWireframe"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex Vert
            #pragma geometry Geo
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
            };

            struct VertexToGeometry
            {
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                noperspective float3 barycentric : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _LineColor;
                half4 _FillColor;
                half _LineThickness;
                half _FresnelPower;
                half _ScanScale;
                half _ScanSpeed;
            CBUFFER_END

            VertexToGeometry Vert(Attributes input)
            {
                VertexToGeometry output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            [maxvertexcount(3)]
            void Geo(
                triangle VertexToGeometry input[3],
                inout TriangleStream<Varyings> stream)
            {
                const float3 barycentrics[3] =
                {
                    float3(1, 0, 0),
                    float3(0, 1, 0),
                    float3(0, 0, 1)
                };

                [unroll]
                for (uint index = 0; index < 3; index++)
                {
                    Varyings output;
                    output.positionHCS = TransformWorldToHClip(input[index].positionWS);
                    output.positionWS = input[index].positionWS;
                    output.normalWS = input[index].normalWS;
                    output.barycentric = barycentrics[index];
                    stream.Append(output);
                }
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 derivatives = fwidth(input.barycentric);
                float3 edge = smoothstep(
                    0.0,
                    derivatives * _LineThickness,
                    input.barycentric);
                half wire = 1.0h - min(edge.x, min(edge.y, edge.z));
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(
                    1.0h - saturate(dot(normalWS, viewDirectionWS)),
                    _FresnelPower);
                half scanWave = abs(frac(
                    (input.positionWS.y * _ScanScale)
                    - (_Time.y * _ScanSpeed)) - 0.5h) * 2.0h;
                half scan = pow(saturate(1.0h - scanWave), 18.0h);
                half lineMask = saturate(wire + (scan * 0.2h));
                half3 fill = lerp(_FillColor.rgb, _BaseColor.rgb, fresnel * 0.6h);
                half3 color = lerp(fill, _LineColor.rgb, lineMask);
                half alpha = saturate(
                    _FillColor.a
                    + (fresnel * _BaseColor.a * 0.34h)
                    + (wire * _LineColor.a)
                    + (scan * 0.08h));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "TacticalProjectionFallback"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _LineColor;
                half4 _FillColor;
                half _LineThickness;
                half _FresnelPower;
                half _ScanScale;
                half _ScanSpeed;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(
                    1.0h - saturate(dot(normalWS, viewDirectionWS)),
                    _FresnelPower);
                half scanWave = abs(frac(
                    (input.positionWS.y * _ScanScale)
                    - (_Time.y * _ScanSpeed)) - 0.5h) * 2.0h;
                half scan = pow(saturate(1.0h - scanWave), 18.0h);
                half3 color = lerp(
                    _FillColor.rgb,
                    _LineColor.rgb,
                    saturate(fresnel + (scan * 0.3h)));
                half alpha = saturate(
                    _FillColor.a
                    + (fresnel * _BaseColor.a * 0.42h)
                    + (scan * 0.1h));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
