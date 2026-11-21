Shader "POLYMULATOR/Mocha Gradient Sky"
{
    Properties
    {
        _ZenithColor ("Zenith (Base)", Color) = (0.11765, 0.11765, 0.18039, 1)
        _HorizonColor ("Horizon (Surface0)", Color) = (0.19216, 0.19608, 0.26667, 1)
        _NadirColor ("Nadir (Crust)", Color) = (0.06667, 0.06667, 0.10588, 1)
        _GlowColor ("Atmospheric Accent (Sapphire)", Color) = (0.45490, 0.78039, 0.92549, 1)
        _HorizonExponent ("Horizon Falloff", Range(0.5, 6.0)) = 2.2
        _GlowStrength ("Horizon Accent Strength", Range(0.0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Background"
            "Queue" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "MochaSky"
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex SkyVert
            #pragma fragment SkyFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _NadirColor;
                half4 _GlowColor;
                float _HorizonExponent;
                float _GlowStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirOS : TEXCOORD0;
            };

            Varyings SkyVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.dirOS = input.positionOS.xyz;
                return output;
            }

            half4 SkyFrag(Varyings input) : SV_Target
            {
                float y = normalize(input.dirOS).y;
                float upMask = pow(saturate(y), 1.0 / max(0.25, _HorizonExponent));
                float downMask = pow(saturate(-y), 1.0 / max(0.25, _HorizonExponent));

                half3 color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, upMask);
                color = lerp(color, _NadirColor.rgb, downMask);

                float horizonBand = exp(-abs(y) * 9.0);
                color += _GlowColor.rgb * (horizonBand * _GlowStrength);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
