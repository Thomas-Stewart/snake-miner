Shader "BallBounce/Aurora Orbit Background"
{
    Properties
    {
        _AuroraIntensity("Contour Intensity", Range(0, 1.5)) = 0.26
        _OrbitIntensity("Technical Detail Intensity", Range(0, 1)) = 0.14
        _StarIntensity("Star Intensity", Range(0, 1)) = 0.18
        _MotionSpeed("Motion Speed", Range(0, 1)) = 0.32
        _Aspect("Screen Aspect", Float) = 1.7778
        _WorldScale("Background World Scale", Range(1, 5)) = 1
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
            Name "AstralCartography"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
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
                float _AuroraIntensity;
                float _OrbitIntensity;
                float _StarIntensity;
                float _MotionSpeed;
                float _Aspect;
                float _WorldScale;
                float _BackgroundTime;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 RotateCoordinates(float2 p, float angle)
            {
                float sineAngle = sin(angle);
                float cosineAngle = cos(angle);
                return float2(
                    cosineAngle * p.x - sineAngle * p.y,
                    sineAngle * p.x + cosineAngle * p.y);
            }

            float SoftDisc(float2 p, float2 center, float radius)
            {
                float radialDistance = length(p - center);
                return 1.0 - smoothstep(
                    radius * 0.28,
                    radius,
                    radialDistance);
            }

            float RingStroke(float radialDistance, float radius, float width)
            {
                float edgeDistance = abs(radialDistance - radius);
                float antialias = max(fwidth(edgeDistance), 0.00045);
                return 1.0 - smoothstep(
                    width,
                    width + antialias,
                    edgeDistance);
            }

            float AngularWindow(float angle, float center, float halfWidth)
            {
                float wrapped = abs(
                    atan2(sin(angle - center), cos(angle - center)));
                float antialias = max(fwidth(wrapped), 0.002);
                return 1.0 - smoothstep(
                    halfWidth,
                    halfWidth + antialias,
                    wrapped);
            }

            float CornerContour(
                float2 p,
                float2 center,
                float2 scale,
                float phase,
                float time)
            {
                float2 local = (p - center) / scale;
                float angle = atan2(local.y, local.x);
                float warpedRadius =
                    length(local) +
                    sin(angle * 3.0 + phase) * 0.036 +
                    sin(angle * 7.0 - phase * 0.7) * 0.012;

                float contourCoordinate =
                    warpedRadius * 12.0 - time * 0.15;
                float bandDistance = abs(frac(contourCoordinate) - 0.5);
                float antialias = max(fwidth(contourCoordinate) * 0.36, 0.018);
                float contourStroke = 1.0 - smoothstep(
                    0.035,
                    0.035 + antialias,
                    bandDistance);

                float radialMask =
                    smoothstep(0.25, 0.42, warpedRadius) *
                    (1.0 - smoothstep(1.1, 1.62, warpedRadius));
                float brokenMask = lerp(
                    0.34,
                    1.0,
                    smoothstep(-0.35, 0.6,
                        sin(angle * 5.0 + phase * 2.2)));
                return contourStroke * radialMask * brokenMask;
            }

            float PrecisionArc(
                float2 p,
                float2 center,
                float radius,
                float width,
                float arcCenter,
                float arcHalfWidth,
                float phase,
                float time,
                float segmented)
            {
                float2 local = p - center;
                float radialDistance = length(local);
                float angle = atan2(local.y, local.x);
                float strokeValue = RingStroke(
                    radialDistance,
                    radius,
                    width);
                float arcMask = AngularWindow(
                    angle,
                    arcCenter,
                    arcHalfWidth);
                float segmentWave = sin(
                    angle * 72.0 + phase - time * 0.8);
                float segmentMask = smoothstep(0.05, 0.32, segmentWave);
                return strokeValue * arcMask *
                    lerp(1.0, segmentMask, segmented);
            }

            float ArcNode(
                float2 p,
                float2 center,
                float radius,
                float angle,
                float size)
            {
                float2 nodePosition =
                    center + float2(cos(angle), sin(angle)) * radius;
                float nodeDistance = length(p - nodePosition);
                float antialias = max(fwidth(nodeDistance), 0.0007);
                return 1.0 - smoothstep(
                    size,
                    size + antialias,
                    nodeDistance);
            }

            float TechnicalLattice(float2 p, float phase, float time)
            {
                float2 local = RotateCoordinates(
                    p + float2(
                        time * 0.018,
                        sin(time * 0.31) * 0.016),
                    0.52);
                float spacing = 0.18;
                float familyA = abs(frac(local.x / spacing) - 0.5);
                float familyB = abs(frac(
                    (local.x * 0.5 + local.y * 0.866) / spacing) - 0.5);
                float familyC = abs(frac(
                    (-local.x * 0.5 + local.y * 0.866) / spacing) - 0.5);
                float nearestEdge = min(familyA, min(familyB, familyC));
                float antialias = max(fwidth(nearestEdge), 0.006);
                float latticeStroke = 1.0 - smoothstep(
                    0.008,
                    0.008 + antialias,
                    nearestEdge);

                float cellVariation = Hash21(
                    floor(local / spacing) + phase);
                return latticeStroke * lerp(0.22, 0.55, cellVariation);
            }

            float StarLayer(
                float2 uv,
                float density,
                float threshold,
                float drift,
                float time,
                out float glint)
            {
                float2 movingUv =
                    uv + float2(time * drift, -time * drift * 0.37);
                float2 grid = movingUv * density;
                float2 cell = floor(grid);
                float2 local = frac(grid);
                float randomValue = Hash21(cell);
                float2 starPosition = float2(
                    Hash21(cell + 7.3),
                    Hash21(cell + 19.1));
                float2 delta = local - starPosition;
                float starDistance = length(delta);
                float starSize = lerp(
                    0.024,
                    0.055,
                    Hash21(cell + 31.7));
                float antialias = max(fwidth(starDistance), 0.003);
                float starCore = 1.0 - smoothstep(
                    starSize,
                    starSize + antialias,
                    starDistance);
                float visibility = step(threshold, randomValue);
                float twinkle = 0.72 + 0.28 * sin(
                    time * 1.7 + randomValue * 37.0);

                float crossX = 1.0 - smoothstep(
                    0.008,
                    0.008 + max(fwidth(abs(delta.x)), 0.002),
                    abs(delta.x));
                float crossY = 1.0 - smoothstep(
                    0.008,
                    0.008 + max(fwidth(abs(delta.y)), 0.002),
                    abs(delta.y));
                float crossFade = 1.0 - smoothstep(
                    0.025,
                    0.16,
                    starDistance);
                glint = max(crossX, crossY) * crossFade *
                    step(0.993, randomValue);
                return starCore * visibility * twinkle;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(
                    input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 screenP = uv * 2.0 - 1.0;
                screenP.x *= _Aspect;
                float2 worldP = screenP * _WorldScale;
                float2 worldUv =
                    (uv - 0.5) * _WorldScale + 0.5;
                float time = _BackgroundTime * _MotionSpeed;

                float3 upperColor = float3(0.002, 0.008, 0.026);
                float3 lowerColor = float3(0.006, 0.014, 0.044);
                float3 color = lerp(lowerColor, upperColor, uv.y);

                float centerDistance = length(float2(
                    screenP.x / max(_Aspect, 0.001),
                    screenP.y * 0.84));
                float vignette = smoothstep(0.2, 1.38, centerDistance);
                color *= lerp(1.08, 0.55, vignette);

                float2 depthDrift = float2(
                    sin(time * 0.19),
                    cos(time * 0.14)) * 0.075;
                float violetDepth = SoftDisc(
                    screenP,
                    float2(-_Aspect * 0.77, -0.88) + depthDrift,
                    1.22);
                float cyanDepth = SoftDisc(
                    screenP,
                    float2(_Aspect * 0.84, 0.68) - depthDrift * 0.72,
                    1.05);
                float depthBreath =
                    0.92 + sin(time * 0.56) * 0.08;
                color += float3(0.055, 0.012, 0.12) *
                    violetDepth * 0.34 * depthBreath;
                color += float3(0.006, 0.07, 0.115) *
                    cyanDepth * 0.28 * (2.0 - depthBreath);

                float edgeDistance = max(
                    abs(screenP.x) / max(_Aspect, 0.001),
                    abs(screenP.y));
                float edgeField = smoothstep(0.37, 0.94, edgeDistance);
                float quietCenter = smoothstep(0.24, 0.76, edgeDistance);

                float2 leftAnchor = float2(
                    -_Aspect * 0.95,
                    -0.84);
                float2 rightAnchor = float2(
                    _Aspect * 1.02,
                    0.82);
                float2 topAnchor = float2(
                    -_Aspect * 0.22,
                    1.42);
                float2 leftDetailP = RotateCoordinates(
                    (screenP - leftAnchor) * _WorldScale,
                    time * 0.075) + leftAnchor;
                float2 rightDetailP = RotateCoordinates(
                    (screenP - rightAnchor) * _WorldScale,
                    -time * 0.058) + rightAnchor;
                float2 topDetailP = RotateCoordinates(
                    (screenP - topAnchor) * _WorldScale,
                    sin(time * 0.23) * 0.055) + topAnchor;

                float contourLeft = CornerContour(
                    leftDetailP,
                    leftAnchor,
                    float2(1.28, 0.94),
                    0.6,
                    time);
                float contourRight = CornerContour(
                    rightDetailP,
                    rightAnchor,
                    float2(1.16, 0.86),
                    2.7,
                    -time * 0.82);
                float contourTop = CornerContour(
                    topDetailP,
                    topAnchor,
                    float2(1.52, 0.72),
                    4.4,
                    time * 0.64);

                float3 cyanContourColor = float3(0.06, 0.47, 0.68);
                float3 violetContourColor = float3(0.43, 0.12, 0.7);
                color += cyanContourColor * contourRight *
                    edgeField * _AuroraIntensity * 0.55;
                color += violetContourColor * contourLeft *
                    edgeField * _AuroraIntensity * 0.46;
                color += float3(0.15, 0.23, 0.58) * contourTop *
                    edgeField * _AuroraIntensity * 0.34;

                float lattice = TechnicalLattice(worldP, 2.3, time);
                float latticeMask =
                    saturate(violetDepth * 0.55 + cyanDepth * 0.48) *
                    edgeField;
                color += float3(0.05, 0.22, 0.34) * lattice *
                    latticeMask * _OrbitIntensity * 0.42;

                float leftArc = 0.0;
                leftArc += PrecisionArc(
                    leftDetailP, leftAnchor,
                    0.72, 0.0018, 0.72, 0.82, 0.3, time, 0.0);
                leftArc += PrecisionArc(
                    leftDetailP, leftAnchor,
                    0.82, 0.0015, 0.72, 0.69, 1.7, time, 1.0);
                leftArc += PrecisionArc(
                    leftDetailP, leftAnchor,
                    0.93, 0.0014, 0.72, 0.55, 3.2, time, 0.0);

                float rightArc = 0.0;
                rightArc += PrecisionArc(
                    rightDetailP, rightAnchor,
                    0.62, 0.0017, -2.35, 0.76, 2.1, -time, 0.0);
                rightArc += PrecisionArc(
                    rightDetailP, rightAnchor,
                    0.73, 0.0014, -2.35, 0.62, 4.0, -time, 1.0);
                rightArc += PrecisionArc(
                    rightDetailP, rightAnchor,
                    0.84, 0.0014, -2.35, 0.49, 5.3, -time, 0.0);

                float technicalArcs = saturate(leftArc + rightArc);
                color += float3(0.2, 0.56, 0.78) * technicalArcs *
                    _OrbitIntensity * 0.72;

                float nodeValue = 0.0;
                nodeValue += ArcNode(
                    leftDetailP,
                    leftAnchor,
                    0.82,
                    0.4 + time * 0.28,
                    0.007);
                nodeValue += ArcNode(
                    rightDetailP,
                    rightAnchor,
                    0.73,
                    -2.65 - time * 0.23,
                    0.006);
                color += float3(0.96, 0.55, 0.17) * nodeValue *
                    edgeField * _OrbitIntensity * 1.15;

                float glintA;
                float glintB;
                float starsA = StarLayer(
                    worldUv, 73.0, 0.972, 0.0024, time, glintA);
                float starsB = StarLayer(
                    worldUv + 0.173, 119.0, 0.986, -0.00135,
                    time * 0.83, glintB);
                float stars = starsA + starsB * 0.58;
                float temperature = Hash21(floor(worldUv * 83.0));
                float3 starColor = lerp(
                    float3(0.35, 0.64, 1.0),
                    float3(0.82, 0.7, 1.0),
                    temperature);
                color += starColor * stars * _StarIntensity *
                    lerp(0.78, 1.0, quietCenter);
                color += float3(0.55, 0.82, 1.0) *
                    saturate(glintA + glintB) *
                    _StarIntensity * 0.72;

                float innerLift = 1.0 - smoothstep(0.0, 0.72, centerDistance);
                color += float3(0.003, 0.008, 0.018) * innerLift;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
