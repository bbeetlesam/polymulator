Shader "POLYMULATOR/Volumetric Beam"
{
    Properties
    {
        [MainColor] _BeamColor ("Beam Color", Color) = (0.45490, 0.78039, 0.92549, 0.14)
        _EdgeFade ("Fresnel Edge Fade", Range(0.5, 5.0)) = 2.0
        _NoiseSpeed ("Shimmer Speed", Float) = 0.20
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "VolumetricShaft"
            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BeamVert
            #pragma fragment BeamFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BeamColor;
                float _EdgeFade;
                float _NoiseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings BeamVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = input.uv;
                return output;
            }

            half4 BeamFrag(Varyings input) : SV_Target
            {
                float3 nWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rimSoft = pow(abs(dot(nWS, viewDirWS)), _EdgeFade);

                float verticalFade = smoothstep(0.0, 0.65, input.uv.y) * (1.0 - smoothstep(0.92, 1.0, input.uv.y) * 0.45);
                float streak = 0.85 + 0.15 * sin(input.uv.x * 18.849 + _Time.y * _NoiseSpeed * 4.0);
                float alpha = saturate(_BeamColor.a * rimSoft * verticalFade * streak);
                return half4(_BeamColor.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
