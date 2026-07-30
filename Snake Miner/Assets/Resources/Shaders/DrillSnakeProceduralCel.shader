Shader "DrillSnake/Procedural Cel"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.35, 0.38, 0.4, 1)
        _ShadowColor("Shadow Color", Color) = (0.06, 0.07, 0.08, 1)
        _AccentColor("Pattern Accent", Color) = (0.55, 0.58, 0.6, 1)
        _EmissionColor("Emission", Color) = (0, 0, 0, 0)
        _HeatTintColor("Heat Tint", Color) = (1, 0.08, 0.025, 1)
        _HeatTintStrength("Heat Tint Strength", Range(0, 1)) = 0
        _PatternScale("Pattern Scale", Range(0.1, 16)) = 4
        _PatternStrength("Pattern Strength", Range(0, 1)) = 0.2
        _StoneSurface("Stone Surface", Range(0, 1)) = 0
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
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
                half4 _HeatTintColor;
                float _HeatTintStrength;
                float _PatternScale;
                float _PatternStrength;
                float _StoneSurface;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 blend = frac(value);
                blend = blend * blend * (3.0 - 2.0 * blend);
                float lowerLeft = Hash21(cell);
                float lowerRight = Hash21(cell + float2(1.0, 0.0));
                float upperLeft = Hash21(cell + float2(0.0, 1.0));
                float upperRight = Hash21(cell + float2(1.0, 1.0));
                return lerp(
                    lerp(lowerLeft, lowerRight, blend.x),
                    lerp(upperLeft, upperRight, blend.x),
                    blend.y);
            }

            float FractalNoise(float2 value)
            {
                float noise = ValueNoise(value) * 0.58;
                noise += ValueNoise(value * 2.07 + 17.3) * 0.28;
                noise += ValueNoise(value * 4.13 - 9.7) * 0.14;
                return noise;
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
                        ? 0.7h
                        : 0.28h;
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
                float fleck = smoothstep(0.78, 0.96, grain);
                float pattern = fleck;
                color = lerp(
                    color,
                    _AccentColor.rgb * (0.72h + celBand * 0.28h),
                    pattern * _PatternStrength);

                // Texture-free broad stone mottling and fine mineral breakup.
                // World-space sampling prevents every grid block from showing
                // the same repeated face.
                float broadNoise = FractalNoise(
                    patternPosition * 0.31 + normalWS.xz * 3.1);
                float fineNoise = ValueNoise(
                    patternPosition * 1.73 + normalWS.zy * 7.9);
                float surfaceNoise = broadNoise * 0.72 + fineNoise * 0.28;
                float variation = lerp(
                    1.0,
                    lerp(0.82, 1.12, surfaceNoise),
                    saturate(0.28 + _PatternStrength));
                color *= variation;
                float mineralChip = smoothstep(0.84, 0.97, fineNoise) *
                    _PatternStrength;
                color = lerp(
                    color,
                    _AccentColor.rgb * (0.7h + celBand * 0.3h),
                    mineralChip * 0.22);

                // Thin contour cracks and broad mineral staining are derived
                // entirely from world-space noise. The stone mask keeps the
                // machinery clean and graphic.
                half topFacing = smoothstep(0.42h, 0.78h, normalWS.y);
                float crackField = ValueNoise(
                    patternPosition * 0.22 + float2(13.7, -8.4));
                float crackDistance = abs(crackField - 0.52);
                float crack = 1.0 - smoothstep(0.012, 0.04, crackDistance);
                crack *= _StoneSurface * topFacing;
                color = lerp(color, _ShadowColor.rgb * 0.72h, crack * 0.7h);

                float stoneStain = smoothstep(
                    0.64,
                    0.9,
                    FractalNoise(patternPosition * 0.12 + 23.5));
                color = lerp(
                    color,
                    _AccentColor.rgb * 0.78h,
                    stoneStain * _StoneSurface * topFacing * 0.14h);

                half3 viewDirection =
                    SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(
                    1.0h - saturate(dot(normalWS, viewDirection)),
                    3.0h);
                color += _AccentColor.rgb * rim * 0.12h;
                color *= mainLight.color;
                color += SampleSH(normalWS) * _BaseColor.rgb * 0.28h;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u;
                         lightIndex < additionalLightCount;
                         ++lightIndex)
                    {
                        Light additionalLight = GetAdditionalLight(
                            lightIndex,
                            input.positionWS);
                        half additionalAmount = saturate(dot(
                            normalWS,
                            additionalLight.direction));
                        half attenuation =
                            additionalLight.distanceAttenuation *
                            additionalLight.shadowAttenuation;
                        color += _BaseColor.rgb *
                            additionalLight.color *
                            additionalAmount *
                            attenuation *
                            0.38h;
                    }
                #endif

                color += _EmissionColor.rgb;

                // A subtle screen-edge falloff keeps the playfield framed
                // without creating a visible spotlight.
                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                float vignetteDistance = length((screenUv - 0.5) * 1.35);
                half vignette = smoothstep(0.42h, 0.76h, vignetteDistance);
                color *= lerp(1.0h, 0.9h, vignette);

                half brightness = max(color.r, max(color.g, color.b));
                half3 heatedColor = _HeatTintColor.rgb *
                    (0.32h + brightness * 0.8h);
                color = lerp(
                    color,
                    lerp(color, heatedColor, 0.78h),
                    saturate(_HeatTintStrength));

                #if defined(_SCREEN_SPACE_OCCLUSION)
                    float2 screenSpaceUV =
                        GetNormalizedScreenSpaceUV(input.positionCS);
                    AmbientOcclusionFactor ambientOcclusion =
                        GetScreenSpaceAmbientOcclusion(screenSpaceUV);
                    color *= ambientOcclusion.directAmbientOcclusion;
                #endif

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    Fallback "Universal Render Pipeline/Lit"
}
