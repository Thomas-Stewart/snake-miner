Shader "DrillSnake/Procedural Cel"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.35, 0.38, 0.4, 1)
        _ShadowColor("Shadow Color", Color) = (0.06, 0.07, 0.08, 1)
        _AccentColor("Pattern Accent", Color) = (0.55, 0.58, 0.6, 1)
        _EmissionColor("Emission", Color) = (0, 0, 0, 0)
        _PatternScale("Pattern Scale", Range(0.1, 16)) = 4
        _PatternStrength("Pattern Strength", Range(0, 1)) = 0.2
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
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                half4 _AccentColor;
                half4 _EmissionColor;
                float _PatternScale;
                float _PatternStrength;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half lightAmount = saturate(dot(normalWS, mainLight.direction));
                lightAmount *= mainLight.shadowAttenuation *
                               mainLight.distanceAttenuation;

                half celBand = lightAmount > 0.72h
                    ? 1.0h
                    : lightAmount > 0.28h
                        ? 0.76h
                        : 0.38h;
                half3 color = lerp(
                    _ShadowColor.rgb,
                    _BaseColor.rgb,
                    celBand);

                float3 triplanarPosition =
                    abs(normalWS.y) > 0.6h
                        ? input.positionWS.xzy
                        : abs(normalWS.x) > 0.6h
                            ? input.positionWS.zyx
                            : input.positionWS.xyz;
                float2 patternPosition =
                    triplanarPosition.xy * max(0.1, _PatternScale);
                float2 patternCell = floor(patternPosition);
                float grain = Hash21(patternCell);
                float diagonal = abs(
                    frac((patternPosition.x + patternPosition.y) * 0.22) -
                    0.5) * 2.0;
                float hatch = smoothstep(0.88, 0.98, diagonal);
                float fleck = smoothstep(0.72, 0.94, grain);
                float pattern = saturate(fleck * 0.72 + hatch * 0.28);
                color = lerp(
                    color,
                    _AccentColor.rgb * (0.72h + celBand * 0.28h),
                    pattern * _PatternStrength);

                half3 viewDirection =
                    SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(
                    1.0h - saturate(dot(normalWS, viewDirection)),
                    3.0h);
                color += _AccentColor.rgb * rim * 0.08h;
                color *= mainLight.color;
                color += _EmissionColor.rgb;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    Fallback "Universal Render Pipeline/Lit"
}
