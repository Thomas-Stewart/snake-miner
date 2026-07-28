Shader "BallBounce/Sky Sanctuary Background"
{
    Properties
    {
        _MainTex("Painted Sky", 2D) = "white" {}
        _Aspect("Screen Aspect", Float) = 1.7778
        _WorldScale("Background World Scale", Range(1, 5)) = 1
        _MotionSpeed("Motion Speed", Range(0, 1)) = 0.2
        _AuroraIntensity("Atmosphere Intensity", Range(0, 1.5)) = 0.3
        _OrbitIntensity("Sunbeam Intensity", Range(0, 1)) = 0.2
        _StarIntensity("Pollen Intensity", Range(0, 1)) = 0.2
        [HideInInspector] _BackgroundTime("Background Time", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PaintedSkySanctuary"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Aspect;
                float _WorldScale;
                float _MotionSpeed;
                float _AuroraIntensity;
                float _OrbitIntensity;
                float _StarIntensity;
                float _BackgroundTime;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float SmoothNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);
                return lerp(
                    lerp(Hash21(cell), Hash21(cell + float2(1.0, 0.0)), local.x),
                    lerp(Hash21(cell + float2(0.0, 1.0)), Hash21(cell + 1.0), local.x),
                    local.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Aspect-fill the fixed 16:9 painting without stretching it.
                const float artworkAspect = 1.7768332;
                float2 artworkUv = uv;
                if (_Aspect > artworkAspect)
                {
                    float visibleHeight = artworkAspect / _Aspect;
                    artworkUv.y = (uv.y - 0.5) * visibleHeight + 0.5;
                }
                else
                {
                    float visibleWidth = _Aspect / artworkAspect;
                    artworkUv.x = (uv.x - 0.5) * visibleWidth + 0.5;
                }

                half3 painted = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    saturate(artworkUv)).rgb;

                float2 centered = uv * 2.0 - 1.0;
                centered.x *= _Aspect / artworkAspect;
                float centerDistance = length(centered * float2(0.86, 1.1));
                float playfieldVeil =
                    1.0 - smoothstep(0.16, 0.82, centerDistance);

                // A translucent blue veil keeps the playfield legible while
                // preserving the visible brushwork of the environment plate.
                half3 calmSky = half3(0.075, 0.43, 0.72);
                painted = lerp(
                    painted,
                    calmSky,
                    playfieldVeil * (0.17 + _AuroraIntensity * 0.08));

                float time = _BackgroundTime * max(0.05, _MotionSpeed);
                float cloudNoise =
                    SmoothNoise(uv * float2(3.2, 2.2) + float2(time * 0.018, 0.0));
                float highHaze =
                    smoothstep(0.52, 0.96, uv.y) *
                    smoothstep(0.34, 0.78, cloudNoise);
                painted = lerp(
                    painted,
                    half3(0.86, 0.95, 1.0),
                    highHaze * 0.045 * _AuroraIntensity);

                float sunRay =
                    smoothstep(0.0, 0.72, uv.y) *
                    (0.5 + 0.5 * sin((uv.x + uv.y * 0.23) * 18.0 - time * 0.12));
                sunRay = pow(saturate(sunRay), 8.0);
                painted +=
                    half3(1.0, 0.86, 0.54) *
                    sunRay *
                    _OrbitIntensity *
                    0.055;

                float grain = Hash21(
                    floor(uv * _ScreenParams.xy * 0.34) +
                    floor(_BackgroundTime * 3.0));
                painted *= lerp(0.985, 1.015, grain);

                float edgeShade =
                    smoothstep(0.55, 1.3, centerDistance) * 0.08;
                painted *= 1.0 - edgeShade;
                painted = pow(max(painted, 0.0), half3(0.97, 0.97, 0.95));
                return half4(painted, 1.0);
            }
            ENDHLSL
        }
    }
}
