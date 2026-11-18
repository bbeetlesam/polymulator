Shader "POLYMULATOR/Placement Ghost Line"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.65098, 0.89020, 0.63137, 1)
        _DashCount ("Dashes", Float) = 6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "DashedLine"
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

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
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float viewZ : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _DashCount;
            CBUFFER_END

            float4 _GhostCenterVS;
            float _GhostRadius;
            float _GhostBackAlpha;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionVS = TransformWorldToView(positionWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.viewZ = positionVS.z - _GhostCenterVS.z;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (frac(input.uv.x * max(1.0, _DashCount)) > 0.55) discard;

                // View-space depth relative to the ghost centre: near edges stay
                // bright while far edges fade, which gives the wireframe volume.
                float depth01 = saturate(0.5 + input.viewZ / max(0.01, _GhostRadius) * 0.5);
                half alpha = lerp(max(0.05, _GhostBackAlpha), 1.0, depth01);
                return half4(_BaseColor.rgb, _BaseColor.a * alpha);
            }
            ENDHLSL
        }
    }
}
