Shader "Custom/Ocean"
{
    Properties
    {
        [MainColor] _BaseOceanColor("Base Ocean Color", Color) = (0.0, 0.5, 1.0, 1.0)
            [SecondaryColor] _BaseSkyColor("Base Sky Color", Color) = (0.5, 0.7, 1.0, 1.0)
                [Vector] _LightDirection("Light Direction", Vector) = (0.0, 1.0, 0.0, 0.0)
                    [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
    }

    SubShader
    {
        Tags{"RenderType" = "Transparent"
                            "RenderPipeline" = "UniversalPipeline"}

        Pass
        {
            HLSLPROGRAM

#pragma vertex vert
#pragma fragment frag

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float3> _DisplacedVertices;
            StructuredBuffer<float3> _Normals;

            TEXTURE2D(_DisplacementTexture);
            TEXTURE2D(_SlopeTexture);

            // Declare the first texture (displacement) and its sampler.
            // UNITY_DECLARE_TEX2D(_DisplacementTexture);

            // Declare the second texture (slope) without samplers.
            // UNITY_DECLARE_TEX2D_NOSAMPLER(_SlopeTexture);

            int _ActiveWaveGenerator;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseOceanColor;
            half4 _BaseSkyColor;
            float3 _LightDirection;
            float4 _BaseMap_ST;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionOS = float3(0.0, 0.0, 0.0);
                float3 normalOS = float3(0.0, 1.0, 0.0);

                // Gerstner
                if (_ActiveWaveGenerator == 0)
                {
                    positionOS = _DisplacedVertices[IN.vertexID];
                    normalOS = TransformObjectToWorldNormal(_Normals[IN.vertexID]);
                }
                // FFT
                else if (_ActiveWaveGenerator == 1)
                {
                    // todo: maybe remove this
                    float2 uv = float2(
                        IN.uv.x,
                        1.0 - IN.uv.y);
                    float4 displacement = SAMPLE_TEXTURE2D_LOD(_DisplacementTexture, sampler_LinearClamp, IN.uv, 0);
                    positionOS = IN.positionOS.xyz + displacement.xyz;
                }
                // Hybrid
                // todo: add hybrid
                else
                {
                    positionOS = IN.positionOS.xyz + float3(0.0, 10.0, 0.0);
                }

                OUT.normalWS = TransformObjectToWorldNormal(normalOS);

                OUT.positionHCS = TransformObjectToHClip(positionOS);

                OUT.positionWS = TransformObjectToWorld(positionOS);

                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                return OUT;
            }

            float Fresnel(float3 normal, float3 viewDir)
            {
                float cosTheta = saturate(dot(normal, viewDir));

                float r0 = 0.02; // Base reflectivity for water

                return r0 + (1 - r0) * pow(1 - cosTheta, 5);
            }

            float Highlights(float3 normal, float3 viewDir, float3 lightDir)
            {
                float3 halfVector = normalize(lightDir + viewDir);
                float specular = pow(saturate(dot(normal, halfVector)), 128);
                return specular;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normal = float3(0.0, 0.0, 0.0);

                // Gerstner
                if (_ActiveWaveGenerator == 0)
                {
                    normal = normalize(IN.normalWS);
                }
                // FFT
                else if (_ActiveWaveGenerator == 1)
                {
                    float4 slope = SAMPLE_TEXTURE2D(_SlopeTexture, sampler_LinearClamp, IN.uv);
                    normal = normalize(float3(-slope.x, 1.0, -slope.y));
                }
                // Hybrid
                // todo: implement hybrid
                else
                {
                }

                float3 viewDir = normalize(_WorldSpaceCameraPos - IN.positionWS);

                float fresnel = Fresnel(normal, viewDir);

                float3 lightDir = normalize(_LightDirection);

                float specular = Highlights(normal, viewDir, lightDir);

                float3 colour = lerp(
                    _BaseOceanColor.rgb,
                    _BaseSkyColor.rgb,
                    fresnel);

                colour += specular;

                return float4(colour, 1.0);
            }
            ENDHLSL
        }
    }
}
