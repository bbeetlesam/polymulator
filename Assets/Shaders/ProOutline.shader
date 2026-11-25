Shader "POLYMULATOR/Pro Outline"
{
    Properties
    {
        _BaseColor ("Outline Color", Color) = (0.53725, 0.86275, 0.92157, 1)
        _RimTint ("Accent Pulse Tint", Color) = (0.80392, 0.83922, 0.95686, 1)
        _OutlineWidth ("World Thickness (GU)", Range(0.01, 0.16)) = 0.052
        _PulseSpeed ("Pulse Speed", Float) = 4.5
        _PulseAmount ("Pulse Amount", Range(0.0, 0.4)) = 0.14
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite On
            ZTest LEqual
            Offset 1, 1

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimTint;
                float _OutlineWidth;
                float _PulseSpeed;
                float _PulseAmount;
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(input.normalOS);
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);
                posWS += normalize(nrmWS) * (_OutlineWidth * pulse);

                output.positionWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);
                return output;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float wave = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed - input.positionWS.y * 5.0);
                half3 col = lerp(_BaseColor.rgb, _RimTint.rgb, 0.25 + 0.30 * wave);
                return half4(col * (1.18 + 0.18 * wave), 1.0);
            }
            ENDHLSL
        }
    }
}
