Shader "GritGud/Portable Gradient Skybox"
{
    Properties
    {
        _SkyColor ("Sky", Color) = (0.055, 0.11, 0.23, 1)
        _HorizonColor ("Horizon", Color) = (0.028, 0.075, 0.17, 1)
        _GroundColor ("Ground", Color) = (0.012, 0.026, 0.065, 1)
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            float4 _SkyColor;
            float4 _HorizonColor;
            float4 _GroundColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float height = normalize(input.direction).y;
                float skyBlend = smoothstep(0.0, 0.65, height);
                float groundBlend = smoothstep(0.0, 0.45, -height);
                half3 color = lerp(_HorizonColor.rgb, _SkyColor.rgb, skyBlend);
                color = lerp(color, _GroundColor.rgb, groundBlend);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
