Shader "POLYMULATOR/Chamber Grid"
{
    Properties
    {
        _BaseColor ("Base Tile Color", Color) = (0.11765, 0.11765, 0.18039, 1)
        _TileAltColor ("Alternate Tile Color", Color) = (0.14118, 0.14510, 0.21569, 1)
        _MinorLineColor ("Minor Grid (1 GU)", Color) = (0.19216, 0.19608, 0.26667, 1)
        _MajorLineColor ("Major Grid (5 GU)", Color) = (0.27059, 0.27843, 0.35294, 1)
        _BorderColor ("Boundary Glow Color", Color) = (0.45490, 0.78039, 0.92549, 1)
        _PulseColor ("Reactive Pulse Color", Color) = (0.45490, 0.78039, 0.92549, 1)
        _GridUnitSize ("Grid Unit Size (m)", Float) = 1.0
        _MajorStep ("Major Grid Step (GU)", Float) = 5.0
        _MinorWidth ("Minor Line Width", Range(0.005, 0.15)) = 0.030
        _MajorWidth ("Major Line Width", Range(0.01, 0.25)) = 0.065
        _BoundaryHalfExtent ("Boundary Half Extent (GU)", Float) = 14.4
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.32
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.05
        _EmissionStrength ("Grid & Pulse Emission", Range(0.0, 3.0)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _TileAltColor;
            half4 _MinorLineColor;
            half4 _MajorLineColor;
            half4 _BorderColor;
            half4 _PulseColor;
            float _GridUnitSize;
            float _MajorStep;
            float _MinorWidth;
            float _MajorWidth;
            float _BoundaryHalfExtent;
            float _Smoothness;
            float _Metallic;
            float _EmissionStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GridVert
            #pragma fragment GridFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _SimPlayerWorldPos;
            float4 _SimAnchorWorldPos;
            float4 _SimModeTint;
            float4 _SimRippleOrigin;
            float4 _SimRippleParams;
            float4 _SimRippleColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half3 vertexSH : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float GridMask(float2 coord, float lineWidth)
            {
                float2 deriv = max(fwidth(coord), 1e-4);
                float2 grid = abs(frac(coord - 0.5) - 0.5) / deriv;
                float lineDist = min(grid.x, grid.y);
                float halfPx = lineWidth / max(deriv.x + deriv.y, 1e-4) * 0.5;
                return 1.0 - smoothstep(max(0.0, halfPx - 0.75), halfPx + 0.75, lineDist);
            }

            Varyings GridVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.shadowCoord = GetShadowCoord(vertexInput);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            half4 GridFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float unit = max(0.05, _GridUnitSize);
                float2 guXZ = input.positionWS.xz / unit;

                float topMask = smoothstep(0.65, 0.9, abs(normalWS.y));
                float2 cell = floor(guXZ + 0.0005);
                float checker = fmod(abs(cell.x + cell.y), 2.0);
                half3 albedo = lerp(_BaseColor.rgb, _TileAltColor.rgb, checker * 0.65 * topMask);

                // Anti-aliased 1 GU minor grid & N-GU major grid
                float minorLine = GridMask(guXZ, _MinorWidth) * topMask;
                float majorStep = max(1.0, _MajorStep);
                float majorLine = GridMask(guXZ / majorStep, _MajorWidth / majorStep) * topMask;

                albedo = lerp(albedo, _MinorLineColor.rgb, minorLine * 0.85);
                albedo = lerp(albedo, _MajorLineColor.rgb, majorLine);

                // Subtle vertex cross markers at major intersections
                float2 majorLocal = abs(frac(guXZ / majorStep - 0.5) - 0.5) * majorStep;
                float nodeGlow = (1.0 - smoothstep(0.05, 0.14, length(majorLocal))) * topMask;

                // Boundary confinement band near +/- _BoundaryHalfExtent
                float edgeDist = _BoundaryHalfExtent - max(abs(guXZ.x), abs(guXZ.y));
                float borderBand = (1.0 - smoothstep(0.05, 0.45, abs(edgeDist - 0.18))) * step(-0.2, edgeDist) * topMask;
                float outerDarken = smoothstep(0.4, -0.3, edgeDist) * 0.45;
                albedo *= (1.0 - outerDarken);

                // Reactive pulse around Rect & Anchor on the floor plane
                half3 activeTint = _SimModeTint.a > 0.01 ? _SimModeTint.rgb : _PulseColor.rgb;
                float playerDist = length(input.positionWS.xz - _SimPlayerWorldPos.xz) / unit;
                float playerRing = (1.0 - smoothstep(0.02, 0.09, abs(playerDist - 0.95))) * smoothstep(0.01, 0.5, _SimPlayerWorldPos.w) * topMask;
                float playerHalo = exp(-playerDist * 0.9) * 0.18 * smoothstep(0.01, 0.5, _SimPlayerWorldPos.w) * topMask;

                float anchorDist = length(input.positionWS.xz - _SimAnchorWorldPos.xz) / unit;
                float pulseRadius = 1.15 + 0.15 * sin(_Time.y * 2.2);
                float anchorRing = (1.0 - smoothstep(0.02, 0.10, abs(anchorDist - pulseRadius))) * smoothstep(0.01, 0.5, _SimAnchorWorldPos.w) * topMask;
                float anchorGridBoost = (minorLine * 0.35 + majorLine * 0.65) * exp(-anchorDist * 0.35) * smoothstep(0.01, 0.5, _SimAnchorWorldPos.w);

                // Radial GPU shockwave ripple emitted on heavy impacts, conduit lock & anchor completion
                float rippleDist = length(input.positionWS.xz - _SimRippleOrigin.xz) / unit;
                float rippleBand = 1.0 - smoothstep(0.04, max(0.18, _SimRippleParams.y), abs(rippleDist - _SimRippleParams.x));
                float rippleMask = rippleBand * _SimRippleParams.z * _SimRippleOrigin.w * (minorLine * 0.55 + majorLine * 0.85 + 0.28) * topMask;

                half3 emission = 0;
                emission += _MajorLineColor.rgb * majorLine * (0.12 * _EmissionStrength);
                emission += activeTint * (nodeGlow * 0.35 + playerRing * 1.35 + playerHalo) * _EmissionStrength;
                emission += _PulseColor.rgb * (anchorRing * 1.45 + anchorGridBoost * 0.9) * _EmissionStrength;
                emission += _SimRippleColor.rgb * rippleMask * _EmissionStrength;
                emission += _BorderColor.rgb * borderBand * (0.9 * _EmissionStrength);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) || defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI = SampleSHPixel(input.vertexSH, normalWS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = lerp(_Smoothness * 0.85, min(0.85, _Smoothness + 0.2), minorLine + majorLine);
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = emission;
                surfaceData.occlusion = 1.0 - minorLine * 0.15;
                surfaceData.alpha = 1.0;

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                output.positionCS = ApplyShadowClamping(positionCS);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DNVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = NormalizeNormalPerVertex(TransformObjectToWorldNormal(input.normalOS));
                return output;
            }

            half4 DNFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normalize(input.normalWS));
                    half3 packed = PackFloat2To888(saturate(oct * 0.5 + 0.5));
                    return half4(packed, 0.0);
                #else
                    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
