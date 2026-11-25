Shader "POLYMULATOR/Sim Surface"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (0.70588, 0.74510, 0.99608, 1)
        _ShadeColor ("Shade Tint Color", Color) = (0.09412, 0.09412, 0.14510, 1)
        _RimColor ("Fresnel Rim Color", Color) = (0.80392, 0.83922, 0.95686, 1)
        _SeamColor ("Seam / Bevel Color", Color) = (0.11765, 0.11765, 0.18039, 1)
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.08
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.42
        _RimPower ("Rim Sharpness", Range(1.0, 8.0)) = 3.2
        _RimStrength ("Rim Strength", Range(0.0, 1.5)) = 0.24
        _BevelWidth ("Panel Bevel Width", Range(0.0, 0.15)) = 0.045
        _SeamStrength ("Panel Seam Strength", Range(0.0, 1.0)) = 0.28
        _WorldGridSeam ("Use World Grid Seam (0=UV, 1=World)", Float) = 0.0
        _GridUnitSize ("Grid Unit Size (m)", Float) = 1.0
        _GrainStrength ("Surface Micro-Grain", Range(0.0, 1.0)) = 0.28
        _FogColor ("Abyss Mist Color", Color) = (0.06667, 0.06667, 0.10588, 1)
        _HeightFogStart ("Abyss Fog Start Y", Float) = -1.2
        _HeightFogEnd ("Abyss Fog End Y", Float) = -13.0
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
            half4 _ShadeColor;
            half4 _RimColor;
            half4 _SeamColor;
            half4 _EmissionColor;
            float _Metallic;
            float _Smoothness;
            float _RimPower;
            float _RimStrength;
            float _BevelWidth;
            float _SeamStrength;
            float _WorldGridSeam;
            float _GridUnitSize;
            float _GrainStrength;
            half4 _FogColor;
            float _HeightFogStart;
            float _HeightFogEnd;
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
            #pragma vertex SimVert
            #pragma fragment SimFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 normalOS : TEXCOORD3;
                half3 vertexSH : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings SimVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.normalOS = input.normalOS;
                output.shadowCoord = GetShadowCoord(vertexInput);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            void TriplanarGrain(float3 posWS, float3 nWS, out float macroGrain, out float fineGrain, out float3 bumpWS)
            {
                float3 blend = abs(nWS);
                blend = pow(blend + 1e-4, 4.0);
                blend /= max(dot(blend, 1.0), 1e-4);

                float2 uvX = posWS.zy;
                float2 uvY = posWS.xz;
                float2 uvZ = posWS.xy;

                float gX = ValueNoise2D(uvX * 3.2) * 0.65 + ValueNoise2D(uvX * 14.5) * 0.35;
                float gY = ValueNoise2D(uvY * 3.2) * 0.65 + ValueNoise2D(uvY * 14.5) * 0.35;
                float gZ = ValueNoise2D(uvZ * 3.2) * 0.65 + ValueNoise2D(uvZ * 14.5) * 0.35;
                macroGrain = gX * blend.x + gY * blend.y + gZ * blend.z;

                float fX = Hash21(floor(uvX * 48.0));
                float fY = Hash21(floor(uvY * 48.0));
                float fZ = Hash21(floor(uvZ * 48.0));
                fineGrain = fX * blend.x + fY * blend.y + fZ * blend.z;

                float dGx = ValueNoise2D((uvY + float2(0.03, 0.0)) * 12.0) - ValueNoise2D((uvY - float2(0.03, 0.0)) * 12.0);
                float dGz = ValueNoise2D((uvY + float2(0.0, 0.03)) * 12.0) - ValueNoise2D((uvY - float2(0.0, 0.03)) * 12.0);
                bumpWS = float3(dGx, 0.0, dGz) * blend.y + float3(0.0, macroGrain - 0.5, dGz) * (blend.x + blend.z);
            }

            half4 SimFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 geomNormalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float macroGrain, fineGrain;
                float3 grainBump;
                TriplanarGrain(input.positionWS, geomNormalWS, macroGrain, fineGrain, grainBump);

                float3 normalWS = normalize(geomNormalWS + grainBump * (0.09 * _GrainStrength));

                // Subtle top/underside Mocha separation gradient + architectural grain modulation
                float topLift = saturate(geomNormalWS.y * 0.5 + 0.5);
                half3 albedo = lerp(lerp(_BaseColor.rgb, _ShadeColor.rgb, 0.18), _BaseColor.rgb, topLift);
                float tonalDelta = (macroGrain - 0.5) * 0.10 + (fineGrain - 0.5) * 0.035;
                albedo = saturate(albedo * (1.0 + tonalDelta * _GrainStrength));

                float groundProximityAO = lerp(0.84, 1.0, smoothstep(-0.08, 0.55, abs(input.positionWS.y)));
                albedo *= groundProximityAO;

                // Architectural seam & machined panel bevel
                float seamMask = 0.0;
                float bevelHighlight = 0.0;
                if (_SeamStrength > 0.001 && _BevelWidth > 0.0001)
                {
                    if (_WorldGridSeam > 0.5)
                    {
                        float unit = max(0.05, _GridUnitSize);
                        float2 g = input.positionWS.xz / unit;
                        float2 deriv = max(fwidth(g), 1e-4);
                        float2 dist = abs(frac(g - 0.5) - 0.5) / deriv;
                        float pxWidth = _BevelWidth / max(deriv.x + deriv.y, 1e-4);
                        seamMask = (1.0 - smoothstep(max(0.0, pxWidth - 0.6), pxWidth + 0.6, min(dist.x, dist.y))) * smoothstep(0.55, 0.9, abs(geomNormalWS.y));
                        float2 innerG = abs(frac(g - 0.5) - 0.5);
                        float edgeBevel = 1.0 - smoothstep(0.42, 0.48, max(innerG.x, innerG.y));
                        bevelHighlight = (1.0 - edgeBevel) * 0.07 * smoothstep(0.55, 0.9, abs(geomNormalWS.y));
                    }
                    else
                    {
                        float2 edgeDist = min(input.uv, 1.0 - input.uv);
                        float edge = min(edgeDist.x, edgeDist.y);
                        float aa = max(fwidth(edge), 0.004);
                        float outerLine = 1.0 - smoothstep(_BevelWidth - aa, _BevelWidth + aa, edge);
                        float innerLine = smoothstep(_BevelWidth, _BevelWidth + aa * 2.0, edge)
                                        * (1.0 - smoothstep(_BevelWidth * 1.9, _BevelWidth * 1.9 + aa * 2.0, edge));
                        seamMask = outerLine;
                        bevelHighlight = innerLine * 0.22;
                    }
                }

                albedo = lerp(albedo, _SeamColor.rgb, seamMask * _SeamStrength);
                albedo += _RimColor.rgb * bevelHighlight;

                // Stylized Fresnel rim for clear silhouette separation
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), max(1.0, _RimPower));
                half3 rimEmission = _RimColor.rgb * (fresnel * _RimStrength);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
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
                float roughJitter = (macroGrain - 0.5) * 0.16 * _GrainStrength;
                surfaceData.smoothness = saturate(lerp(_Smoothness + roughJitter, _Smoothness * 0.52, seamMask * _SeamStrength));
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = _EmissionColor.rgb + rimEmission;
                surfaceData.occlusion = saturate((1.0 - seamMask * 0.22) * groundProximityAO);
                surfaceData.alpha = 1.0;

                half4 lit = UniversalFragmentPBR(inputData, surfaceData);

                float subMist = smoothstep(_HeightFogStart, _HeightFogEnd, input.positionWS.y);
                float radialDist = max(0.0, length(input.positionWS.xz) - 18.0);
                float radialMist = 1.0 - exp(-radialDist * 0.045);
                float mistFactor = saturate(max(subMist * 0.92, radialMist * 0.65));
                lit.rgb = lerp(lit.rgb, _FogColor.rgb, mistFactor);
                return lit;
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
