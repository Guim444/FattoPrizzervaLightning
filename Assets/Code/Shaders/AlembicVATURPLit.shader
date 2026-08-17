Shader "FattoPrizzerva/VAT/URP Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Toggle] _AlphaClip("Alpha Clipping", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2

        [NoScaleOffset] _VAT_PositionTex("VAT Positions", 2D) = "black" {}
        [NoScaleOffset] _VAT_NormalTex("VAT Normals", 2D) = "gray" {}
        _VAT_TextureWidth("VAT Texture Width", Float) = 1
        _VAT_TextureHeight("VAT Texture Height", Float) = 1
        _VAT_RowsPerFrame("VAT Rows Per Frame", Float) = 1
        _VAT_FrameCount("VAT Frame Count", Float) = 1
        _VAT_FPS("VAT FPS", Float) = 15
        _VAT_PlaybackSpeed("VAT Playback Speed", Float) = 1
        _VAT_TimeOffset("VAT Time Offset", Float) = 0
        _VAT_RandomPhase("VAT Random Phase", Range(0,1)) = 1
        [Toggle] _VAT_Interpolate("VAT Interpolate", Float) = 1
        [Toggle] _VAT_HasNormals("VAT Has Normals", Float) = 1
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
        TEXTURE2D(_VAT_PositionTex);
        SAMPLER(sampler_VAT_PositionTex);
        TEXTURE2D(_VAT_NormalTex);
        SAMPLER(sampler_VAT_NormalTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _AlphaClip;
            float _Cutoff;
            float _Cull;
            float _VAT_TextureWidth;
            float _VAT_TextureHeight;
            float _VAT_RowsPerFrame;
            float _VAT_FrameCount;
            float _VAT_FPS;
            float _VAT_PlaybackSpeed;
            float _VAT_TimeOffset;
            float _VAT_RandomPhase;
            float _VAT_Interpolate;
            float _VAT_HasNormals;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
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

        float2 VatTextureUv(float vertexIndex, float frame)
        {
            float column = fmod(vertexIndex, _VAT_TextureWidth);
            float rowInFrame = floor(vertexIndex / _VAT_TextureWidth);
            float row = rowInFrame + frame * _VAT_RowsPerFrame;
            return float2(
                (column + 0.5) / _VAT_TextureWidth,
                (row + 0.5) / _VAT_TextureHeight);
        }

        VatSample SampleVat(Attributes input)
        {
            UNITY_SETUP_INSTANCE_ID(input);

            float3 objectOriginWS = GetObjectToWorldMatrix()._m03_m13_m23;
            float phaseFrames = VatHash(objectOriginWS.xz) * _VAT_RandomPhase * _VAT_FrameCount;
            float animationFrames = _Time.y * _VAT_PlaybackSpeed * _VAT_FPS
                                  + _VAT_TimeOffset * _VAT_FPS
                                  + phaseFrames;
            float frameValue = frac(animationFrames / max(_VAT_FrameCount, 1.0)) * _VAT_FrameCount;
            float frame0 = floor(frameValue);
            float frame1 = fmod(frame0 + 1.0, max(_VAT_FrameCount, 1.0));
            float blend = frac(frameValue) * _VAT_Interpolate;
            float vertexIndex = round(input.vatVertex.x);

            float2 uv0 = VatTextureUv(vertexIndex, frame0);
            float2 uv1 = VatTextureUv(vertexIndex, frame1);
            float3 position0 = SAMPLE_TEXTURE2D_LOD(_VAT_PositionTex, sampler_VAT_PositionTex, uv0, 0).xyz;
            float3 position1 = SAMPLE_TEXTURE2D_LOD(_VAT_PositionTex, sampler_VAT_PositionTex, uv1, 0).xyz;

            VatSample output;
            output.positionOS = lerp(position0, position1, blend);

            float3 normal0 = SAMPLE_TEXTURE2D_LOD(_VAT_NormalTex, sampler_VAT_NormalTex, uv0, 0).xyz * 2.0 - 1.0;
            float3 normal1 = SAMPLE_TEXTURE2D_LOD(_VAT_NormalTex, sampler_VAT_NormalTex, uv1, 0).xyz * 2.0 - 1.0;
            float3 animatedNormal = normalize(lerp(normal0, normal1, blend));
            output.normalOS = normalize(lerp(input.normalOS, animatedNormal, _VAT_HasNormals));
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

            struct ForwardVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ForwardVaryings VatForwardVertex(Attributes input)
            {
                ForwardVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VatSample vat = SampleVat(input);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(vat.positionOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(vat.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = TransformWorldToShadowCoord(positionInputs.positionWS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 VatForwardFragment(ForwardVaryings input) : SV_Target
            {
                VatAlphaClip(input.uv);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                half direct = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = SampleSH(normalWS)
                               + mainLight.color * direct * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half3 color = MixFog(albedo.rgb * lighting, input.fogFactor);
                return half4(color, albedo.a);
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
    }

    FallBack Off
}
