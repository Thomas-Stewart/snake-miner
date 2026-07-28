Shader "BallBounce/Unlit Vertex Color"
{
    Properties
    {
        [HDR] _Tint("Tint", Color) = (1, 1, 1, 1)
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
            Name "Unlit"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 positionOS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Tint;
                output.positionOS = input.positionOS.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float brushVariation =
                    sin(input.positionOS.x * 41.0 +
                        input.positionOS.y * 29.0) *
                    sin(input.positionOS.x * 17.0 -
                        input.positionOS.y * 53.0);
                float sunWash =
                    saturate(0.52 +
                        input.positionOS.y * 0.035 -
                        input.positionOS.x * 0.018);
                half3 paintedColor =
                    input.color.rgb *
                    (0.975h +
                     brushVariation * 0.018h +
                     sunWash * 0.035h);
                return half4(paintedColor, input.color.a);
            }
            ENDHLSL
        }
    }
}
