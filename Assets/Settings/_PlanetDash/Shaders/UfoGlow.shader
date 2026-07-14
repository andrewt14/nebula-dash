Shader "NebulaDash/UfoGlow"
{
    // Unlit textured shader for the background UFOs: shows the model's own
    // texture, adds a neon fresnel-rim glow in _GlowColor (tinted per zone),
    // and skips fog so distant saucers stay visible against the sky.
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Base Tint", Color) = (1, 1, 1, 1)
        _GlowColor ("Glow Color", Color) = (0.6, 0.3, 1, 1)
        _GlowPower ("Glow Power", Range(0.5, 8)) = 2.5
        _GlowStrength ("Glow Strength", Range(0, 6)) = 2.5
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "UfoGlow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _GlowColor;
                float _GlowPower;
                float _GlowStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = p.positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS = n.normalWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(p.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv)
                            * _BaseColor;

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(N, V)), _GlowPower);
                // Bright rim + a subtle overall neon wash so the whole
                // saucer reads as glowing, not just its edge.
                float glow = fresnel * _GlowStrength + 0.12;

                float3 rgb = tex.rgb + _GlowColor.rgb * glow;
                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
