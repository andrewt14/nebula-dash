Shader "NebulaDash/HologramCharacter"
{
    // Menu-preview-only look: keeps the character's own base texture
    // (via _BaseMap, set per-renderer through a MaterialPropertyBlock so
    // one shared material works for every character) but tints it cyan,
    // scans it with scrolling horizontal lines, flickers the alpha, and
    // brightens a fresnel rim — reads as a holographic projection instead
    // of a solid character. Not used in actual gameplay.
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _Tint ("Tint", Color) = (0.55, 0.85, 1, 1)
        // How much of the tint to blend in over the character's real
        // texture — 0 shows the texture untouched, 1 is a flat cyan wash.
        // Kept low so the actual model/skin detail still reads clearly.
        _TintStrength ("Tint Strength", Range(0,1)) = 0.35
        // Multiplies the sampled texture before tinting — tint/alpha alone
        // can't dim a character whose base texture is naturally light
        // colored (e.g. a white/cream suit), since they only shift hue or
        // transparency, not raw brightness. 1 = texture's own brightness.
        _Brightness ("Texture Brightness", Range(0,2)) = 1
        _Alpha ("Base Alpha", Range(0,1)) = 0.8
        _ScanSpeed ("Scanline Speed", Float) = 1.2
        _ScanDensity ("Scanline Density", Float) = 55
        _ScanStrength ("Scanline Strength", Range(0,1)) = 0.18
        _FlickerSpeed ("Flicker Speed", Float) = 6
        _FlickerAmount ("Flicker Amount", Range(0,1)) = 0.06
        _RimColor ("Rim Color", Color) = (0.6, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 4)) = 1.8
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float _TintStrength;
                float _Brightness;
                float _Alpha;
                float _ScanSpeed;
                float _ScanDensity;
                float _ScanStrength;
                float _FlickerSpeed;
                float _FlickerAmount;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
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
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = posInputs.positionCS;
                OUT.normalWS = normInputs.normalWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                OUT.positionWS = posInputs.positionWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                tex.rgb *= _Brightness;

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float fresnel = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimStrength;

                float scan = sin(IN.positionWS.y * _ScanDensity - _Time.y * _ScanSpeed) * 0.5 + 0.5;
                float scanEffect = 1.0 - scan * _ScanStrength;

                float flicker = 1.0 - _FlickerAmount * (0.5 + 0.5 * sin(_Time.y * _FlickerSpeed));

                half3 tintedColor = tex.rgb * _Tint.rgb;
                // Flat rim-color wash independent of fresnel/edge angle —
                // without it, characters with a dark base texture (e.g. a
                // black ninja outfit) only glow at silhouette edges and
                // read as a solid dark shape everywhere else.
                half3 color = lerp(tex.rgb, tintedColor, _TintStrength)
                    + _RimColor.rgb * fresnel + _RimColor.rgb * 0.12;
                float alpha = saturate(_Alpha * scanEffect * flicker + fresnel * 0.4);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
