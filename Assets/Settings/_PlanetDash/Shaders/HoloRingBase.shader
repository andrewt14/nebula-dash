Shader "NebulaDash/HoloRingBase"
{
    // Classic sci-fi character-projector base: concentric rings pulsing
    // outward from the center plus a bright rim at the platform's edge.
    // Meant for a flat disc mesh (e.g. a squashed cylinder) so UV (0.5,0.5)
    // is the center and the circle inscribed in the UV square is the edge.
    Properties
    {
        _Color ("Color", Color) = (0.4, 0.85, 1, 1)
        _RingSpeed ("Ring Scroll Speed", Float) = 0.6
        _RingCount ("Ring Count", Float) = 5
        _RingSharpness ("Ring Sharpness", Range(1, 12)) = 4
        _RingStrength ("Ring Strength", Range(0, 2)) = 0.7
        _EdgeGlow ("Edge Glow Strength", Range(0, 4)) = 1.6
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend One One // additive — reads as light, not a solid disc
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _RingSpeed;
                float _RingCount;
                float _RingSharpness;
                float _RingStrength;
                float _EdgeGlow;
                float _BaseAlpha;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 c = (IN.uv - 0.5) * 2.0;
                float r = length(c);
                clip(1.0 - r); // circular cutout of the square mesh UV

                float rings = frac(r * _RingCount - _Time.y * _RingSpeed);
                float ringPulse = pow(1.0 - abs(rings - 0.5) * 2.0, _RingSharpness) * _RingStrength;

                float edge = pow(saturate(r), 8.0) * _EdgeGlow;

                float alpha = saturate(_BaseAlpha * (1.0 - r) + ringPulse + edge);
                return half4(_Color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
