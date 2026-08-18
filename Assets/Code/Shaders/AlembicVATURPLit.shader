Shader "FattoPrizzerva/Vegetation VAT/Dual Wind URP Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1
        _MetallicGlossMap("Metallic Map", 2D) = "white" {}
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 1
        [Toggle] _AlphaClip("Alpha Clipping", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2

        [NoScaleOffset] _VAT_PositionTex("VAT Positions", 2D) = "black" {}
        [NoScaleOffset] _VAT_NormalTex("VAT Normals", 2D) = "gray" {}
        [NoScaleOffset] _VAT_FastPositionTex("VAT Fast Positions", 2D) = "black" {}
        [NoScaleOffset] _VAT_FastNormalTex("VAT Fast Normals", 2D) = "gray" {}
        _VAT_TextureWidth("VAT Texture Width", Float) = 1
        _VAT_TextureHeight("VAT Texture Height", Float) = 1
        _VAT_RowsPerFrame("VAT Rows Per Frame", Float) = 1
        _VAT_FrameCount("VAT Frame Count", Float) = 1
        _VAT_FPS("VAT FPS", Float) = 15
        _VAT_FastTextureWidth("VAT Fast Texture Width", Float) = 1
        _VAT_FastTextureHeight("VAT Fast Texture Height", Float) = 1
        _VAT_FastRowsPerFrame("VAT Fast Rows Per Frame", Float) = 1
        _VAT_FastFrameCount("VAT Fast Frame Count", Float) = 1
        _VAT_FastFPS("VAT Fast FPS", Float) = 15
        _VAT_PlaybackSpeed("VAT Playback Speed", Float) = 1
        _VAT_TimeOffset("VAT Time Offset", Float) = 0
        _VAT_RandomPhase("VAT Random Phase", Range(0,1)) = 1
        [Toggle] _VAT_Interpolate("VAT Interpolate", Float) = 1
        [Toggle] _VAT_HasNormals("VAT Has Normals", Float) = 1
        [Toggle] _VAT_FastHasNormals("VAT Fast Has Normals", Float) = 1
        [Toggle] _VAT_DualMode("VAT Dual Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300
        Cull [_Cull]

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap);
        SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_OcclusionMap);
        SAMPLER(sampler_OcclusionMap);
        TEXTURE2D(_VAT_PositionTex);
        SAMPLER(sampler_VAT_PositionTex);
        TEXTURE2D(_VAT_NormalTex);
        SAMPLER(sampler_VAT_NormalTex);
        TEXTURE2D(_VAT_FastPositionTex);
        SAMPLER(sampler_VAT_FastPositionTex);
        TEXTURE2D(_VAT_FastNormalTex);
        SAMPLER(sampler_VAT_FastNormalTex);

        // Global: WindStateManager la actualiza una sola vez para toda la vegetación VAT.
        float _VAT_WindBlend;

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _Metallic;
            half _Smoothness;
            half _OcclusionStrength;
            float _AlphaClip;
            float _Cutoff;
            float _Cull;
            float _VAT_TextureWidth;
            float _VAT_TextureHeight;
            float _VAT_RowsPerFrame;
            float _VAT_FrameCount;
            float _VAT_FPS;
            float _VAT_FastTextureWidth;
            float _VAT_FastTextureHeight;
            float _VAT_FastRowsPerFrame;
            float _VAT_FastFrameCount;
            float _VAT_FastFPS;
            float _VAT_PlaybackSpeed;
            float _VAT_TimeOffset;
            float _VAT_RandomPhase;
            float _VAT_Interpolate;
            float _VAT_HasNormals;
            float _VAT_FastHasNormals;
            float _VAT_DualMode;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            float2 vatVertex : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct VatSample
        {
            float3 positionOS;
            float3 normalOS;
        };

        float VatHash(float2 value)
        {
            return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
        }

        float2 VatTextureUv(
            float vertexIndex,
            float frame,
            float textureWidth,
            float textureHeight,
            float rowsPerFrame)
        {
            float column = fmod(vertexIndex, textureWidth);
            float rowInFrame = floor(vertexIndex / textureWidth);
            float row = rowInFrame + frame * rowsPerFrame;
            return float2(
                (column + 0.5) / textureWidth,
                (row + 0.5) / textureHeight);
        }

        VatSample SampleVat(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);

            float3 objectOriginWS = GetObjectToWorldMatrix()._m03_m13_m23;
            float slowFrameCount = max(_VAT_FrameCount, 1.0);
            float slowDuration = slowFrameCount / max(_VAT_FPS, 0.0001);
            float phase = VatHash(objectOriginWS.xz) * _VAT_RandomPhase;
            float cycle = frac(((_Time.y * _VAT_PlaybackSpeed) + _VAT_TimeOffset) / slowDuration + phase);

            float slowFrameValue = cycle * slowFrameCount;
            // frac() is always below 1 mathematically, but the multiplication can
            // round up to frameCount on the GPU. Clamp the index so multi-row VATs
            // never sample the padded row beyond the texture at the loop boundary.
            float slowFrame0 = min(floor(slowFrameValue), slowFrameCount - 1.0);
            float slowFrame1 = fmod(slowFrame0 + 1.0, slowFrameCount);
            float slowBlend = frac(slowFrameValue) * _VAT_Interpolate;
            float vertexIndex = round(input.vatVertex.x);

            float2 slowUv0 = VatTextureUv(
                vertexIndex, slowFrame0, _VAT_TextureWidth, _VAT_TextureHeight, _VAT_RowsPerFrame);
            float2 slowUv1 = VatTextureUv(
                vertexIndex, slowFrame1, _VAT_TextureWidth, _VAT_TextureHeight, _VAT_RowsPerFrame);
            float3 slowPosition0 = SAMPLE_TEXTURE2D_LOD(
                _VAT_PositionTex, sampler_VAT_PositionTex, slowUv0, 0).xyz;
            float3 slowPosition1 = SAMPLE_TEXTURE2D_LOD(
                _VAT_PositionTex, sampler_VAT_PositionTex, slowUv1, 0).xyz;
            float3 slowPosition = lerp(slowPosition0, slowPosition1, slowBlend);

            float fastFrameCount = max(_VAT_FastFrameCount, 1.0);
            float fastFrameValue = cycle * fastFrameCount;
            float fastFrame0 = min(floor(fastFrameValue), fastFrameCount - 1.0);
            float fastFrame1 = fmod(fastFrame0 + 1.0, fastFrameCount);
            float fastBlend = frac(fastFrameValue) * _VAT_Interpolate;
            float2 fastUv0 = VatTextureUv(
                vertexIndex,
                fastFrame0,
                _VAT_FastTextureWidth,
                _VAT_FastTextureHeight,
                _VAT_FastRowsPerFrame);
            float2 fastUv1 = VatTextureUv(
                vertexIndex,
                fastFrame1,
                _VAT_FastTextureWidth,
                _VAT_FastTextureHeight,
                _VAT_FastRowsPerFrame);
            float3 fastPosition0 = SAMPLE_TEXTURE2D_LOD(
                _VAT_FastPositionTex, sampler_VAT_FastPositionTex, fastUv0, 0).xyz;
            float3 fastPosition1 = SAMPLE_TEXTURE2D_LOD(
                _VAT_FastPositionTex, sampler_VAT_FastPositionTex, fastUv1, 0).xyz;
            float3 fastPosition = lerp(fastPosition0, fastPosition1, fastBlend);

            float windBlend = saturate(_VAT_WindBlend) * _VAT_DualMode;

            VatSample output;
            output.positionOS = lerp(slowPosition, fastPosition, windBlend);

            float3 slowNormal0 = SAMPLE_TEXTURE2D_LOD(
                _VAT_NormalTex, sampler_VAT_NormalTex, slowUv0, 0).xyz * 2.0 - 1.0;
            float3 slowNormal1 = SAMPLE_TEXTURE2D_LOD(
                _VAT_NormalTex, sampler_VAT_NormalTex, slowUv1, 0).xyz * 2.0 - 1.0;
            float3 slowNormal = normalize(lerp(slowNormal0, slowNormal1, slowBlend));
            slowNormal = normalize(lerp(input.normalOS, slowNormal, _VAT_HasNormals));

            float3 fastNormal0 = SAMPLE_TEXTURE2D_LOD(
                _VAT_FastNormalTex, sampler_VAT_FastNormalTex, fastUv0, 0).xyz * 2.0 - 1.0;
            float3 fastNormal1 = SAMPLE_TEXTURE2D_LOD(
                _VAT_FastNormalTex, sampler_VAT_FastNormalTex, fastUv1, 0).xyz * 2.0 - 1.0;
            float3 fastNormal = normalize(lerp(fastNormal0, fastNormal1, fastBlend));
            fastNormal = normalize(lerp(input.normalOS, fastNormal, _VAT_FastHasNormals));
            output.normalOS = normalize(lerp(slowNormal, fastNormal, windBlend));
            return output;
        }

        void VatAlphaClip(float2 uv)
        {
            half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
            if (_AlphaClip > 0.5)
                clip(alpha - _Cutoff);
        }
        ENDHLSL

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex VatForwardVertex
            #pragma fragment VatForwardFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS

            struct ForwardVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half4 tangentWS : TEXCOORD4;
                half4 fogFactorAndVertexLight : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ForwardVaryings VatForwardVertex(Attributes input)
            {
                ForwardVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VatSample vat = SampleVat(input);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(vat.positionOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(TransformObjectToWorldNormal(vat.normalOS));
                half3 tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.tangentWS = half4(
                    NormalizeNormalPerVertex(tangentWS),
                    input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = TransformWorldToShadowCoord(positionInputs.positionWS);
                half fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                half3 vertexLight = VertexLighting(positionInputs.positionWS, output.normalWS);
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
                return output;
            }

            half4 VatForwardFragment(ForwardVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                VatAlphaClip(input.uv);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 baseNormalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 tangentWS = normalize(input.tangentWS.xyz);
                tangentWS = normalize(tangentWS - baseNormalWS * dot(baseNormalWS, tangentWS));
                half3 bitangentWS = input.tangentWS.w * cross(baseNormalWS, tangentWS);
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv),
                    _BumpScale);
                half3 normalWS = NormalizeNormalPerPixel(
                    TransformTangentToWorld(normalTS, half3x3(tangentWS, bitangentWS, baseNormalWS)));

                half4 metallicGloss = SAMPLE_TEXTURE2D(
                    _MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                half occlusionSample = SAMPLE_TEXTURE2D(
                    _OcclusionMap, sampler_OcclusionMap, input.uv).g;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha = albedo.a;
                surfaceData.metallic = metallicGloss.r * _Metallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = metallicGloss.a * _Smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = lerp(1.0h, occlusionSample, _OcclusionStrength);
                surfaceData.emission = half3(0, 0, 0);
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = input.fogFactorAndVertexLight.x;
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = unity_ProbesOcclusion;
                inputData.tangentToWorld = half3x3(tangentWS, bitangentWS, baseNormalWS);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = albedo.a;
                return color;
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

            HLSLPROGRAM
            #pragma vertex VatDepthVertex
            #pragma fragment VatDepthFragment
            #pragma multi_compile_instancing

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings VatDepthVertex(Attributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VatSample vat = SampleVat(input);
                output.positionCS = TransformObjectToHClip(vat.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 VatDepthFragment(DepthVaryings input) : SV_Target
            {
                VatAlphaClip(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex VatDepthVertex
            #pragma fragment VatDepthFragment
            #pragma multi_compile_instancing

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings VatDepthVertex(Attributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VatSample vat = SampleVat(input);
                output.positionCS = TransformObjectToHClip(vat.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 VatDepthFragment(DepthVaryings input) : SV_Target
            {
                VatAlphaClip(input.uv);
                return 0;
            }
            ENDHLSL
        }

        // SSAO configured from DepthNormals makes URP build the camera depth
        // texture with this pass. Volumetric fog also consumes that texture, so
        // the VAT deformation and alpha clipping must be represented here.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex VatDepthNormalsVertex
            #pragma fragment VatDepthNormalsFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthNormalsVaryings VatDepthNormalsVertex(Attributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VatSample vat = SampleVat(input);
                output.positionCS = TransformObjectToHClip(vat.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = NormalizeNormalPerVertex(TransformObjectToWorldNormal(vat.normalOS));
                return output;
            }

            void VatDepthNormalsFragment(
                DepthNormalsVaryings input,
                out half4 outNormalWS : SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                    , out float4 outRenderingLayers : SV_Target1
                #endif
            )
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                VatAlphaClip(input.uv);

                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalize(input.normalWS));
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    outNormalWS = half4(packedNormalWS, 0.0);
                #else
                    outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
                #endif

                #ifdef _WRITE_RENDERING_LAYERS
                    uint renderingLayers = GetMeshRenderingLayer();
                    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
