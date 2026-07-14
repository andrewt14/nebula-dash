Shader "NebulaDash/SuitRimGlow"
{
    // Additive fresnel rim. Rendered as a second, additive pass over the
    // player's skinned mesh so it outlines the silhouette in neon magenta
    // without touching the character's own materials. Skinning is applied
    // upstream by the SkinnedMeshRenderer, so a standard object-space
    // shader animates with the character for free.
    Properties
    {
        _RimColor ("Rim Color", Color) = (0.85, 0.15, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 6)) = 2.2
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SuitRim"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One          // additive
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs =
                    GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs =
                    GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = posInputs.positionCS;
                OUT.normalWS = normInputs.normalWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float fresnel = 1.0 - saturate(dot(N, V));
                fresnel = pow(fresnel, _RimPower) * _RimStrength;
                return half4(_RimColor.rgb * fresnel, fresnel);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
