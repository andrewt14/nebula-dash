Shader "NebulaDash/SkyPlanet"
{
    // Unlit, fog-immune sphere shader for distant background planets.
    // The default Unlit shader applies scene fog, which fully swallowed
    // the planets at background distance — this one skips fog so they
    // always read against the sky. HDR base color blooms.
    Properties
    {
        _BaseColor ("Color", Color) = (0.6, 0.3, 0.9, 1)
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
            Name "SkyPlanet"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
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
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = p.positionCS;
                OUT.normalWS = n.normalWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(p.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Soft shading + gentle rim so it reads as a sphere.
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float facing = saturate(dot(N, V));
                float rim = pow(1.0 - facing, 3.0) * 0.6;
                float shade = 0.55 + 0.45 * facing;
                return half4(_BaseColor.rgb * shade + _BaseColor.rgb * rim, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
