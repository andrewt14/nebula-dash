Shader "NebulaDash/HoloBeam"
{
    // Vertical light column projecting up from the pedestal through the
    // character, the way old sci-fi hologram projectors read at a glance.
    // Meant for a cylinder mesh (local Y from -0.5 at the base to +0.5 at
    // the top) — brightest at the base, fading out toward the top.
    Properties
    {
        _Color ("Color", Color) = (0.5, 0.9, 1, 1)
        _BaseAlpha ("Base Alpha", Range(0,1)) = 0.35
        _FlickerSpeed ("Flicker Speed", Float) = 5
        _FlickerAmount ("Flicker Amount", Range(0,1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _BaseAlpha;
                float _FlickerSpeed;
                float _FlickerAmount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionHCS : SV_POSITION; float heightT : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewDirWS : TEXCOORD2; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = posInputs.positionCS;
                OUT.normalWS = normInputs.normalWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                OUT.heightT = saturate(IN.positionOS.y + 0.5); // 0 at base, 1 at top
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Soft glow instead of a hard-edged tube: only the silhouette
                // edge (grazing angle, via fresnel) picks up brightness, so
                // the beam reads as a translucent light column you can see
                // the character through, not a solid cylinder wall.
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float edgeFade = pow(1.0 - saturate(abs(dot(N, V))), 2.0);

                float heightFade = 1.0 - IN.heightT;
                float flicker = 1.0 - _FlickerAmount * (0.5 + 0.5 * sin(_Time.y * _FlickerSpeed));
                float alpha = _BaseAlpha * heightFade * heightFade * flicker * edgeFade;
                return half4(_Color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
