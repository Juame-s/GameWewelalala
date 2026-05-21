Shader "Custom/VolumetricSnow"
{
    Properties
    {
        _BaseColor ("Snow Color", Color) = (0.9, 0.95, 1, 1)
        _MeltColor ("Melted/Trampled Color", Color) = (0.4, 0.5, 0.6, 1)
        _EdgeColor ("Edge Highlight Color", Color) = (1.2, 1.2, 1.2, 1)
        _EdgeWidth ("Edge Width", Range(0.01, 1.0)) = 0.2
        _Displacement ("Displacement Amount", Float) = 0.5
        _NormalStrength ("Normal Sharpness", Range(1, 10)) = 3.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MeltColor;
                float4 _EdgeColor;
                float _EdgeWidth;
                float _Displacement;
                float _NormalStrength;
            CBUFFER_END

            TEXTURE2D(_SnowMap);
            SAMPLER(sampler_SnowMap);
            float _SnowMapSize;

            Varyings vert (Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                float2 snowUV = positionWS.xz / _SnowMapSize + 0.5;
                float snowDepth = SAMPLE_TEXTURE2D_LOD(_SnowMap, sampler_SnowMap, snowUV, 0).r;
                
                float offset = 1.0 / 1024.0;
                float depthR = SAMPLE_TEXTURE2D_LOD(_SnowMap, sampler_SnowMap, snowUV + float2(offset, 0), 0).r;
                float depthL = SAMPLE_TEXTURE2D_LOD(_SnowMap, sampler_SnowMap, snowUV + float2(-offset, 0), 0).r;
                float depthU = SAMPLE_TEXTURE2D_LOD(_SnowMap, sampler_SnowMap, snowUV + float2(0, offset), 0).r;
                float depthD = SAMPLE_TEXTURE2D_LOD(_SnowMap, sampler_SnowMap, snowUV + float2(0, -offset), 0).r;
                
                float dR = -depthR * _Displacement * _NormalStrength;
                float dL = -depthL * _Displacement * _NormalStrength;
                float dU = -depthU * _Displacement * _NormalStrength;
                float dD = -depthD * _Displacement * _NormalStrength;
                
                float3 dx = float3(_SnowMapSize * offset * 2.0, dR - dL, 0);
                float3 dz = float3(0, dU - dD, _SnowMapSize * offset * 2.0);
                
                float3 newNormal = normalize(cross(dz, dx));
                
                positionWS.y -= snowDepth * _Displacement;
                
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = newNormal;
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                float2 snowUV = input.positionWS.xz / _SnowMapSize + 0.5;
                float snowDepth = SAMPLE_TEXTURE2D(_SnowMap, sampler_SnowMap, snowUV).r;
                
                float4 color = lerp(_BaseColor, _MeltColor, snowDepth);
                
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(input.normalWS, mainLight.direction));
                float3 lighting = mainLight.color * NdotL;
                
                lighting += float3(0.3, 0.35, 0.4); 
                
                // Create a glowing rim at the very edge of the crater
                float edgeFactor = smoothstep(0.0, _EdgeWidth, snowDepth) * smoothstep(_EdgeWidth * 2.5, _EdgeWidth, snowDepth);
                
                float3 finalColor = color.rgb * lighting + (_EdgeColor.rgb * edgeFactor * NdotL);
                
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float _Displacement;
            TEXTURE2D(_SnowMap);
            SAMPLER(sampler_SnowMap);
            float _SnowMapSize;
            float3 _LightDirection;

            Varyings vert (Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                float2 snowUV = positionWS.xz / _SnowMapSize + 0.5;
                float snowDepth = SAMPLE_TEXTURE2D_LOD(_SnowMap, sampler_SnowMap, snowUV, 0).r;
                
                float offset = 1.0 / 1024.0;
                float depthR = SAMPLE_TEXTURE2D_LOD(_SnowMap, sampler_SnowMap, snowUV + float2(offset, 0), 0).r;
                float depthL = SAMPLE_TEXTURE2D_LOD(_SnowMap, sampler_SnowMap, snowUV + float2(-offset, 0), 0).r;
                float depthU = SAMPLE_TEXTURE2D_LOD(_SnowMap, sampler_SnowMap, snowUV + float2(0, offset), 0).r;
                float depthD = SAMPLE_TEXTURE2D_LOD(_SnowMap, sampler_SnowMap, snowUV + float2(0, -offset), 0).r;
                
                float dR = -depthR * _Displacement;
                float dL = -depthL * _Displacement;
                float dU = -depthU * _Displacement;
                float dD = -depthD * _Displacement;
                
                float3 dx = float3(_SnowMapSize * offset * 2.0, dR - dL, 0);
                float3 dz = float3(0, dU - dD, _SnowMapSize * offset * 2.0);
                float3 newNormal = normalize(cross(dz, dx));
                
                positionWS.y -= snowDepth * _Displacement;
                
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, newNormal, _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
