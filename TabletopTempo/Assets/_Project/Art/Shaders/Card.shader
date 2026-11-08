// Move card on the note highway: lane frame (tinted) + white pictogram, with a noise dissolve.
// Unlit, transparent, single-pass-instanced stereo (Quest). Original, MIT.
Shader "TabletopTempo/Card"
{
    Properties
    {
        _FrameTex ("Frame (white ring + dark fill)", 2D) = "white" {}
        _IconTex ("Icon (white on transparent)", 2D) = "black" {}
        _LaneColor ("Lane colour", Color) = (1, 1, 1, 1)
        _IconScale ("Icon scale inside frame", Float) = 0.72
        _Dissolve ("Dissolve 0..1", Range(0, 1)) = 0
        _Glow ("Hit glow 0..1", Range(0, 1)) = 0
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FrameTex); SAMPLER(sampler_FrameTex);
            TEXTURE2D(_IconTex);  SAMPLER(sampler_IconTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _LaneColor;
                float _IconScale;
                float _Dissolve;
                float _Glow;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Smooth value noise in [0,1].
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float4 frame = SAMPLE_TEXTURE2D(_FrameTex, sampler_FrameTex, i.uv);
                float3 col = frame.rgb * _LaneColor.rgb;
                float a = frame.a;

                float2 iuv = (i.uv - 0.5) / _IconScale + 0.5;
                float inside = step(0.0, iuv.x) * step(iuv.x, 1.0) * step(0.0, iuv.y) * step(iuv.y, 1.0);
                float icon = SAMPLE_TEXTURE2D(_IconTex, sampler_IconTex, iuv).a * inside;
                col = lerp(col, float3(1, 1, 1), icon);
                a = max(a, icon);

                // Dissolve: blotchy noise threshold with a lane-coloured burning edge.
                float n = 0.65 * valueNoise(i.uv * 6.0) + 0.35 * valueNoise(i.uv * 13.0 + 7.0);
                clip(n - _Dissolve * 1.02 + 0.001);
                float edge = 1.0 - smoothstep(_Dissolve, _Dissolve + 0.08, n);
                col += edge * step(0.001, _Dissolve) * (_LaneColor.rgb * 1.2 + 0.2);

                col += _Glow * 0.45;
                return half4(col, a * _Alpha);
            }
            ENDHLSL
        }
    }
}
