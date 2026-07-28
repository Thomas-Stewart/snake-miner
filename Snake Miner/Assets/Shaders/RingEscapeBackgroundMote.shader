Shader "BallBounce/Background Mote"
{
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
            Name "StellarGlint"
            Blend One OneMinusSrcAlpha
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
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = (input.uv - 0.5) * 2.0;
                float2 petalSpace = float2(
                    centered.x * 0.72 + centered.y * 0.24,
                    centered.y * 1.15 - centered.x * 0.12);
                float petalDistance = length(petalSpace);
                float petalAntialias = max(
                    fwidth(petalDistance) * 1.15,
                    0.018);
                half petal = 1.0h - smoothstep(
                    0.72h,
                    0.72h + petalAntialias,
                    petalDistance);
                float veinDistance = abs(
                    petalSpace.x +
                    sin(petalSpace.y * 3.0) * 0.06);
                half vein =
                    (1.0h - smoothstep(0.025h, 0.09h, veinDistance)) *
                    petal;
                float coreDistance = length(centered);
                float coreAntialias = max(
                    fwidth(coreDistance),
                    0.018);
                half core = 1.0h - smoothstep(
                    0.15h,
                    0.15h + coreAntialias,
                    coreDistance);
                half alpha = saturate(
                    petal * 0.72h +
                    vein * 0.18h +
                    core * 0.4h);
                half3 glintColor = lerp(
                    input.color.rgb,
                    half3(1.0h, 0.96h, 0.72h),
                    saturate(vein + core) * 0.72h);
                return half4(
                    glintColor * alpha * input.color.a,
                    alpha * input.color.a);
            }
            ENDHLSL
        }
    }
}
