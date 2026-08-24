Shader "NebulaDash/LaserBeam"
{
    // Jagged electric hazard beam. A hot orange-red core zigzags along the
    // beam's length via hashed-noise displacement (not a flat line), with
    // white-hot crackle sparks flashing along it and a deep red halo
    // bleeding past the quad edges. Everything is _Time-driven so pooled
    // instances share one material with zero per-instance script state.
    Properties
    {
        _BeamColor ("Outer Halo", Color) = (0.85, 0.08, 0.02, 1)
        _CoreColor ("Hot Core", Color) = (1, 0.55, 0.25, 1)
        _EdgeColor ("Post Anchor Glow", Color) = (1, 0.4, 0.05, 1)
        _HotColor ("Crackle Spark", Color) = (1, 0.85, 0.55, 1)
        _JagAmount ("Jag Amount", Range(0, 0.5)) = 0.22
        _JagScale ("Jag Frequency", Range(1, 40)) = 16
        _JagSpeed ("Jag Crawl Speed", Range(0, 20)) = 5
        _FlowSpeed ("Energy Flow Speed", Range(0, 30)) = 7
        _PulseSpeed ("Pulse Speed", Range(0, 12)) = 3
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.16
        _Flicker ("Flicker", Range(0, 1)) = 0.16
        _CoreFalloff ("Core Falloff", Range(0.5, 4)) = 1.6
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "LaserBeam"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BeamColor;
                float4 _CoreColor;
                float4 _EdgeColor;
                float4 _HotColor;
                float _JagAmount;
                float _JagScale;
                float _JagSpeed;
                float _FlowSpeed;
                float _PulseSpeed;
                float _PulseAmount;
                float _Flicker;
                float _CoreFalloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                real fogFactor : TEXCOORD1;
            };

            // Cheap hash — drives the jag noise and crackle, no texture needed.
            float Hash01(float x)
            {
                return frac(sin(x * 127.1) * 43758.5453);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float t = _Time.y;
                float x = IN.uv.x; // across the track (posts at 0 and 1)
                float y = IN.uv.y; // up the beam height

                // Jagged bolt centerline: three octaves of hashed value
                // noise crawling along x/time, so the bright band zigzags
                // like a lightning strike instead of sitting flat at y=0.5.
                float jag = 0.0;
                float amp = 1.0;
                float freq = _JagScale;
                float tt = t * _JagSpeed;
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    float seg = x * freq + tt * (i * 0.7 + 1.0);
                    float n = lerp(Hash01(floor(seg)), Hash01(floor(seg) + 1.0), smoothstep(0.0, 1.0, frac(seg)));
                    jag += (n - 0.5) * amp;
                    amp *= 0.45;
                    freq *= 2.4;
                }
                jag *= _JagAmount;

                // Distance from the (now jagged) centerline. On the quad UV
                // this is the up-axis (UV.y), since the quad is stretched
                // wide across the track and the beam thickness is its short
                // dimension.
                float d = abs(y - 0.5 - jag);

                // Hot core band with a wider red halo that spills past the
                // quad edges so the beam reads thick against the track.
                float core = pow(1.0 - saturate(d * 2.6), _CoreFalloff);
                float halo = pow(1.0 - saturate(d * 3.2), 2.0);

                // White-hot crackle sparks flashing along the bolt — sparse
                // segments briefly popping bright, the "electric" read.
                float sparkSeg = floor(x * _JagScale * 1.6 + tt * 2.0);
                float spark = step(0.88, Hash01(sparkSeg * 3.17)) * core;

                // Energy motes flowing along the beam toward the posts.
                float flow = smoothstep(0.88, 1.0, frac(x * 5.0 - t * _FlowSpeed * 0.35))
                           * smoothstep(0.0, 0.15, frac(x * 5.0 - t * _FlowSpeed * 0.35));

                // Bright anchor flare where the beam meets each post.
                float edgeT = 1.0 - abs(x - 0.5) * 2.0; // 0 center, 1 at posts
                float edgeGlow = pow(edgeT, 14.0);

                float3 col = _CoreColor.rgb * core
                           + _BeamColor.rgb * halo
                           + _HotColor.rgb * spark
                           + _EdgeColor.rgb * flow * core
                           + _EdgeColor.rgb * edgeGlow * halo;

                // Breathing pulse plus a faster crackle flicker — alive,
                // not static, but not a strobe either.
                float pulse = 1.0 + _PulseAmount * sin(t * _PulseSpeed);
                float flicker = 1.0 - _Flicker * Hash01(floor(t * 22.0));
                col *= pulse * flicker;

                // Soft top/bottom fade so the barrier doesn't clip hard at
                // the quad edge.
                float fade = smoothstep(0.0, 0.16, y) * smoothstep(1.0, 0.84, y);
                float alpha = saturate((core * 1.15 + halo * 0.6) * fade);

                half4 res = half4(col * alpha, 0);
                res.rgb = MixFog(res.rgb, IN.fogFactor);
                return res;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
