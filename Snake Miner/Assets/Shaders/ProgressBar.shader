Shader "Custom/ProgressBar"
{
    Properties
    {
        _Progress("Progress", Range(0, 1)) = 1
        [HDR] _FillColor("Fill Color", Color) = (1, 0.78, 0.18, 1)
        _BackgroundColor("Background Color", Color) = (0.08, 0.06, 0.13, 0.8)
        _Width("Width", Float) = 1
        _Height("Height", Float) = 0.25
        _Roundness("Roundness", Range(0, 1)) = 0.5
        _EdgeSoftness("Edge Softness", Float) = 1
        _Pulse("Pulse", Range(0, 1)) = 0
        _Shine("Shine", Range(0, 1)) = 0
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
            Name "ProgressBar"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float _Progress;
                half4 _FillColor;
                half4 _BackgroundColor;
                float _Width;
                float _Height;
                float _Roundness;
                float _EdgeSoftness;
                float _Pulse;
                float _Shine;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 position = input.positionOS.xyz;
                position.xy *= float2(_Width, _Height);
                output.positionCS = TransformObjectToHClip(position);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = abs(input.uv - 0.5) * 2.0;
                float aspect = max(0.001, _Width / max(0.001, _Height));
                float cornerRadius = lerp(0.02, 0.98, _Roundness);
                float2 rectangle = float2(aspect, 1.0);
                float2 local = centered * rectangle;
                float2 halfSize = rectangle - cornerRadius;
                float2 roundedOffset = max(local - halfSize, 0.0);
                float signedDistance =
                    length(roundedOffset) - cornerRadius;
                float antialias = max(
                    fwidth(signedDistance) * max(0.35, _EdgeSoftness),
                    0.002);
                half shape = 1.0h - smoothstep(
                    0.0,
                    antialias,
                    signedDistance);
                half innerShape = 1.0h - smoothstep(
                    -antialias * 3.8,
                    -antialias * 1.2,
                    signedDistance);
                half paintedBorder = saturate(shape - innerShape);
                float progress = saturate(_Progress);
                float fillSoftness = max(fwidth(input.uv.x) * 1.5, 0.001);
                half fill = 1.0h - smoothstep(
                    progress - fillSoftness,
                    progress + fillSoftness,
                    input.uv.x);
                float shinePosition =
                    frac(_Time.y * 0.58) * 1.36 - 0.18;
                half shineBand =
                    (1.0h - smoothstep(
                        0.025,
                        0.13,
                        abs(input.uv.x - shinePosition))) *
                    fill *
                    _Shine;
                half leadingGlow =
                    (1.0h - smoothstep(
                        0.0,
                        0.055,
                        abs(input.uv.x - progress))) *
                    step(input.uv.x, progress) *
                    saturate(progress * 8.0);
                half3 animatedFill =
                    _FillColor.rgb *
                    (1.0h + _Pulse * 0.72h) +
                    shineBand * half3(0.8h, 0.95h, 1.0h) +
                    leadingGlow * _Pulse * half3(0.42h, 0.62h, 0.8h);
                half4 color = lerp(
                    _BackgroundColor,
                    half4(animatedFill, _FillColor.a),
                    fill);
                color.rgb = lerp(
                    color.rgb,
                    half3(1.0h, 0.96h, 0.72h),
                    paintedBorder * 0.82h);
                color.rgb += _BackgroundColor.rgb * (_Pulse * 0.16h);
                color.a *= shape;
                return color;
            }
            ENDHLSL
        }
    }
}
