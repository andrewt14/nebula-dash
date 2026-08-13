Shader "NebulaDash/FlameParticle"
{
    // Additive-blended particle shader for the jetpack flame layers —
    // same "Blend SrcAlpha One" soft-additive trick as UnlitNoFog (proven
    // to render correctly under this project's URP setup), reading
    // per-particle vertex color so the core/outer/wisps color-over-lifetime
    // gradients show through.
    //
    // A plain radial falloff (previous version) just softens a quad into
    // a round blob/spark — recognizable as fire needs an actual tapered
    // teardrop silhouette plus a ragged, flickering edge, not a smooth
    // circle. Built procedurally (cheap hash noise) instead of a texture
    // since there's no flame sprite asset in the project.
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Teardrop flame silhouette: wide near one end, tapering
                // to a sharp point at the other — the axis a Stretch
                // particle's UV.y runs along matches its travel
                // direction, so this reads as a flame lick trailing off
                // the foot rather than a round puff.
                float v = IN.uv.y;
                float width = lerp(0.5, 0.03, v * v);
                float centeredX = abs(IN.uv.x - 0.5) * 2.0;
                float shape = saturate(1.0 - centeredX / max(width, 0.001));
                shape *= shape;
                // Fade the wide end in slightly too so it doesn't hard-cut.
                shape *= smoothstep(0.0, 0.1, v);

                // Scrolling noise ripples the edge and flickers alpha over
                // time — a static smooth silhouette reads as glass/gel,
                // not fire.
                float flicker = valueNoise(float2(IN.uv.x * 5.0 + v * 2.0, v * 3.0 - _Time.y * 5.0));
                shape *= lerp(0.55, 1.05, flicker);

                half4 col = IN.color;
                col.a *= saturate(shape);
                return col;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
