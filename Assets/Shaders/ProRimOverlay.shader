Shader "POLYMULATOR/Pro Rim Overlay"
{
    Properties
    {
        _BaseColor ("Base Hue", Color) = (0.53725, 0.86275, 0.92157, 1)
        _RimTint ("Crisp Rim Tint", Color) = (0.80392, 0.83922, 0.95686, 1)
        _RimPower ("Fresnel Power", Range(1.0, 5.0)) = 2.1
        _RimIntensity ("Rim Intensity", Range(0.1, 2.5)) = 1.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+2"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex RimVert
            #pragma fragment RimFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimTint;
                float _RimPower;
                float _RimIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings RimVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vtx = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vtx.positionCS;
                output.positionWS = vtx.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 RimFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceNormalizeViewDir(input.positionWS));
                float fresnel = pow(saturate(1.0 - saturate(dot(n, v))), _RimPower);
                float pulse = 0.85 + 0.15 * sin(_Time.y * 4.5);

                half3 col = lerp(_BaseColor.rgb, _RimTint.rgb, 0.45) * (_RimIntensity * pulse);
                half alpha = saturate(fresnel * 0.78);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
