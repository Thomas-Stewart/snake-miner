using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BallBounce.RingEscape
{
    /// <summary>
    /// A deterministic, procedural recreation of the supplied ring-and-ball
    /// reference videos. The closest surviving ring is the only solid boundary;
    /// touching any ring beyond it disintegrates that ring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RingEscapeSimulation : MonoBehaviour
    {
        private const int FullscreenRingMeshSegments = 180;
        private const int GridRingMeshSegments = 48;
        private const int DotMeshSegments = 6;
        private const float InnerRadius = 0.24f;
        private const float OuterRadius = 4.473f;
        private const float MinimumRingGap = 0.008f;
        private const float BallRadius = 0.066f;
        private const float GapDegrees = 72f;
        private const float RingTwistDegrees = 1.35f;
        private const float SimulationStep = 1f / 240f;
        private const float GridSimulationStep = 1f / 120f;
        private const float GoldenAngleDegrees = 137.50776f;
        private const int CoinMeshSegments = 12;
        private const int MaxDemoSimulations = 8;

        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int BackgroundTextureId = Shader.PropertyToID("_MainTex");
        private static readonly int BackgroundAspectId = Shader.PropertyToID("_Aspect");
        private static readonly int AuroraIntensityId = Shader.PropertyToID("_AuroraIntensity");
        private static readonly int OrbitIntensityId = Shader.PropertyToID("_OrbitIntensity");
        private static readonly int StarIntensityId = Shader.PropertyToID("_StarIntensity");
        private static readonly int BackgroundMotionSpeedId = Shader.PropertyToID("_MotionSpeed");
        private static readonly int BackgroundWorldScaleId =
            Shader.PropertyToID("_WorldScale");
        private static readonly int BackgroundTimeId =
            Shader.PropertyToID("_BackgroundTime");
        private static readonly Color SpeedBoostButtonColor =
            new Color(0.95f, 0.37f, 0.25f, 1f);
        private static readonly Color CoinMagnetButtonColor =
            new Color(0.18f, 0.68f, 0.7f, 1f);
        private static readonly Color CoinProductionButtonColor =
            new Color(0.96f, 0.7f, 0.2f, 1f);
        private static readonly Color BallMultiplierButtonColor =
            new Color(0.38f, 0.72f, 0.32f, 1f);
        private static readonly Color LockedPowerButtonColor =
            new Color(0.58f, 0.69f, 0.7f, 0.96f);
        private static readonly Color LockedPowerOutlineColor =
            new Color(0.09f, 0.24f, 0.34f, 0.34f);
        private static readonly Color SanctuaryInk =
            new Color(0.045f, 0.17f, 0.27f, 1f);
        private static readonly Color SanctuaryCream =
            new Color(0.985f, 0.97f, 0.86f, 1f);
        private static readonly Color SanctuaryGlass =
            new Color(0.97f, 0.98f, 0.88f, 0.92f);
        private static readonly Color SanctuaryBlue =
            new Color(0.06f, 0.42f, 0.68f, 1f);
        private static readonly Color SanctuaryCoral =
            new Color(1.08f, 0.25f, 0.12f, 1f);
        private static readonly Color SanctuaryGold =
            new Color(1.08f, 0.72f, 0.14f, 1f);
        private static readonly int ColorSessionSeed =
            Environment.TickCount ^ Guid.NewGuid().GetHashCode();
        private static int _colorRerollSequence;
        private static readonly Vector2[] DotDirections =
        {
            new Vector2(1f, 0f),
            new Vector2(0.5f, 0.8660254f),
            new Vector2(-0.5f, 0.8660254f),
            new Vector2(-1f, 0f),
            new Vector2(-0.5f, -0.8660254f),
            new Vector2(0.5f, -0.8660254f)
        };

        [Header("Presentation")]
        [Tooltip("Reusable database containing complete visual color profiles.")]
        [SerializeField] private SimulationColorDatabase colorDatabase;
        [Tooltip("Font used by every generated Ring Escape HUD label.")]
        [SerializeField] private TMP_FontAsset uiFont;
        [Tooltip("Profile selected from the assigned color database.")]
        [SerializeField, Min(0)] private int colorProfileIndex;
        [SerializeField] private Palette palette = Palette.BlueGoldGradient;
        [SerializeField] private bool automaticallyRestart = true;
        [Tooltip("Quiet time after a boundary breaks before the newest innermost ring adopts the ball color.")]
        [SerializeField, Min(0f)] private float innermostTintUpdateBuffer = 0.18f;
        [Tooltip("Time spent rebuilding the ring stack whenever a simulation resets.")]
        [SerializeField, Min(0f)] private float resetRingRevealDuration = 0.85f;
        [Tooltip("Percentage of the reveal used to stagger ring starts from inner to outer.")]
        [SerializeField, Range(0f, 0.95f)] private float resetRingRevealStagger = 0.68f;

        [Header("Procedural Background")]
        [Tooltip("Strength of the sharp cartographic contour structures at the screen edges.")]
        [SerializeField, Range(0f, 1.5f)] private float backgroundAuroraIntensity = 0.28f;
        [Tooltip("Strength of the fine technical lattice, sectional arcs, and moving nodes.")]
        [SerializeField, Range(0f, 1f)] private float backgroundOrbitIntensity = 0.16f;
        [Tooltip("Strength of the layered procedurally generated star field.")]
        [SerializeField, Range(0f, 1f)] private float backgroundStarIntensity = 0.2f;
        [Tooltip("Animation rate for contour drift, technical nodes, and star twinkle.")]
        [SerializeField, Range(0f, 1f)] private float backgroundMotionSpeed = 0.32f;
        [Tooltip("Number of small stellar glints drifting behind the simulations.")]
        [SerializeField, Range(24, 240)] private int backgroundParticleCount = 82;
        [Tooltip("World-space drift speed of the background glints.")]
        [SerializeField, Range(0.01f, 0.2f)] private float backgroundParticleSpeed = 0.032f;
        [Tooltip("Visual size of the drifting stellar glints.")]
        [SerializeField] private Vector2 backgroundParticleSize = new Vector2(0.012f, 0.026f);
        [Tooltip("How strongly the background zooms out as simulation cells become smaller.")]
        [SerializeField, Range(0.2f, 1.4f)] private float backgroundZoomResponse = 0.78f;
        [Tooltip("Maximum amount of procedural background world visible at once.")]
        [SerializeField, Range(1f, 5f)] private float backgroundMaximumWorldScale = 3.4f;
        [Tooltip("Time used to ease the background outward after the grid changes.")]
        [SerializeField, Range(0.05f, 1.2f)] private float backgroundZoomSmoothTime = 0.42f;

        [Header("Simulation Grid")]
        [Tooltip("Turns this component into a host that creates independent simulation cells.")]
        [SerializeField] private bool useSimulationGrid = true;
        [SerializeField, Range(1, 8)] private int gridColumns = 4;
        [SerializeField, Range(1, 8)] private int gridRows = 2;
        [Tooltip("Number of simulations enabled when Play mode starts.")]
        [SerializeField, Min(1)] private int activeSimulationCount = 1;
        [Tooltip("Empty space retained between neighboring cells.")]
        [SerializeField, Range(0f, 0.45f)] private float gridCellPadding = 0.1f;
        [SerializeField] private int gridRandomSeed = 19770419;
        [Tooltip("Minimum and maximum multiplier applied to each cell's launch speed.")]
        [SerializeField] private Vector2 gridLaunchSpeedMultiplier = new Vector2(0.82f, 1.2f);
        [Tooltip("Minimum and maximum multiplier applied to each cell's ring rotation speed.")]
        [SerializeField] private Vector2 gridRotationSpeedMultiplier = new Vector2(0.72f, 1.3f);
        [Tooltip("Chance that a cell starts with the opposite ring rotation direction.")]
        [SerializeField, Range(0f, 1f)] private float gridReverseRotationChance = 0.5f;
        [Tooltip("Random multiplier range applied to the inner/outer rotation-speed relationship.")]
        [SerializeField] private Vector2 gridInnerOuterRatioMultiplier = new Vector2(0.85f, 1.15f);
        [Tooltip("Randomly selects a shape from Grid Shape Pool for every simulation slot.")]
        [SerializeField] private bool randomizeGridShapes = true;
        [Tooltip("Shapes available when grid shape randomization is enabled.")]
        [SerializeField] private SimulationShape[] gridShapePool =
        {
            SimulationShape.Circle,
            SimulationShape.Square,
            SimulationShape.Hexagon,
            SimulationShape.Octagon,
            SimulationShape.Dodecagon
        };

        [Header("Simulation Shop")]
        [Tooltip("Coin price for unlocking the second simulation.")]
        [SerializeField, Min(1)] private int baseSimulationPurchaseCost = 700;
        [Tooltip("Price multiplier applied for every simulation already purchased.")]
        [SerializeField, Min(1.01f)] private float simulationPurchaseCostGrowth = 1.38f;
        [Tooltip("Player level required before the second simulation can be purchased.")]
        [SerializeField, Min(1)] private int additionalSimulationUnlockLevel = 3;
        [Tooltip("Additional player levels required for each simulation after the second.")]
        [SerializeField, Min(0)] private int simulationUnlockLevelStep = 1;
        [Tooltip("Keeps the first shop purchase focused on Ball Overdrive.")]
        [SerializeField] private bool requireOverdriveBeforeAdditionalSimulations = true;
        [Tooltip("Simulation count that begins requiring Coin Magnet to be unlocked.")]
        [SerializeField, Range(2, MaxDemoSimulations)] private int coinMagnetSimulationGate = 3;
        [Tooltip("Simulation count that begins requiring Gold Rush to be unlocked.")]
        [SerializeField, Range(2, MaxDemoSimulations)] private int coinProductionSimulationGate = 5;
        [Tooltip("Simulation count that begins requiring Ball Multiplier to be unlocked.")]
        [SerializeField, Range(2, MaxDemoSimulations)] private int ballMultiplierSimulationGate = 7;

        [Header("Ball Physics")]
        [Tooltip("Magnitude of the ball's velocity when a round starts.")]
        [SerializeField, Min(0.01f)] private float launchSpeed = 1.05f;
        [Tooltip("Initial travel direction, in degrees counter-clockwise from right.")]
        [SerializeField, Range(-180f, 180f)] private float launchAngleDegrees = 8f;
        [Tooltip("Downward acceleration in simulation units per second squared.")]
        [SerializeField, Min(0f)] private float gravity = 1.8f;
        [Tooltip("1 preserves normal impact speed; lower values lose energy per bounce.")]
        [SerializeField, Range(0f, 1.35f)] private float bounciness = 0.99f;
        [Tooltip("Optional floor for total ball speed after a collision.")]
        [SerializeField, Min(0f)] private float minimumSpeed = 0.7f;
        [Tooltip("Safety cap for total ball speed after a collision.")]
        [SerializeField, Min(0.01f)] private float maximumSpeed = 2.4f;
        [Tooltip("Maximum distance traveled by the ball in one collision check, expressed as a fraction of its radius.")]
        [SerializeField, Range(0.15f, 0.8f)] private float collisionTravelRadiusFraction = 0.35f;
        [Tooltip("Safety limit for adaptive high-speed collision substeps.")]
        [SerializeField, Range(4, 64)] private int maximumCollisionSubsteps = 32;
        [Tooltip("Effective speed at which repeated straight-line bounces begin receiving a subtle tangential correction.")]
        [SerializeField, Min(0.1f)] private float antiLoopMinimumEffectiveSpeed = 3f;
        [Tooltip("How closely two consecutive outgoing directions must oppose one another before anti-loop correction applies.")]
        [SerializeField, Range(0.94f, 0.9999f)] private float antiLoopReversalAlignment = 0.985f;
        [Tooltip("Smallest angle added to break a high-speed straight-line bounce loop.")]
        [SerializeField, Range(0f, 20f)] private float antiLoopDeflectionDegrees = 6f;
        [Tooltip("Maximum correction after the ball repeatedly returns along the same line.")]
        [SerializeField, Range(0f, 30f)] private float antiLoopMaximumDeflectionDegrees = 13f;
        [Tooltip("Maximum time between impacts for them to count as the same bounce pattern.")]
        [SerializeField, Range(0.1f, 4f)] private float antiLoopBounceMemory = 1.8f;

        [Header("Ball Juice")]
        [Tooltip("Visual size multiplier applied on top of the ball's collision radius.")]
        [SerializeField, Range(1f, 1.6f)] private float ballVisualScale = 1.18f;
        [Tooltip("How long the ball's motion trail remains visible.")]
        [SerializeField, Range(0.05f, 1f)] private float ballTrailDuration = 0.42f;
        [Tooltip("Trail width relative to the ball's diameter.")]
        [SerializeField, Range(0.1f, 1.5f)] private float ballTrailWidth = 0.34f;
        [Tooltip("Strength of the ball's squash-and-stretch response on impact.")]
        [SerializeField, Range(0f, 0.55f)] private float ballImpactSquash = 0.3f;
        [Tooltip("Time for the ball to recover its round shape after an impact.")]
        [SerializeField, Range(0.04f, 0.5f)] private float ballImpactSquashDuration = 0.16f;
        [Tooltip("Number of pooled sparks emitted by a solid ring collision.")]
        [SerializeField, Range(0, 24)] private int ballImpactParticleCount = 9;
        [Tooltip("Lifetime of pooled collision sparks.")]
        [SerializeField, Range(0.05f, 1f)] private float ballImpactParticleLifetime = 0.3f;

        [Header("Ring Appearance and Motion")]
        [Tooltip("Boundary shape used by this simulation.")]
        [SerializeField] private SimulationShape simulationShape = SimulationShape.Circle;
        [Tooltip("Number of rings distributed between the fixed inner and outer radii.")]
        [SerializeField, Range(2, 120)] private int ringCount = 52;
        [Tooltip("Visual and collision width. Automatically limited so adjacent rings retain a visible gap.")]
        [SerializeField, Min(0.005f)] private float ringThickness = 0.04f;
        [Tooltip("Shrinks only the ball's gap-edge collision radius, allowing visually clear near-edge passes.")]
        [SerializeField, Range(0f, BallRadius * 0.75f)] private float gapEdgeForgiveness = 0.004f;
        [Tooltip("Polygon rings sharing one opening direction before the next offset is applied.")]
        [SerializeField, Range(1, 12)] private int polygonGapOffsetEveryRings = 4;
        [Tooltip("Angular offset applied to each successive polygon ring group.")]
        [SerializeField, Range(0f, 180f)] private float polygonGapOffsetStepDegrees = 90f;
        [Tooltip("Pre-advances circular gaps by this many seconds of their individual spin speed. Faster inner rings consequently spawn farther along the spin direction.")]
        [SerializeField, Range(0f, 5f)] private float circleSpawnRotationLeadTime = 4f;
        [InspectorName("Outer Ring Speed (deg/s)")]
        [Tooltip("Rotation speed of the outermost ring. Negative values rotate clockwise.")]
        [SerializeField] private float rotationSpeedDegrees = -34f;
        [InspectorName("Inner / Outer Speed Ratio")]
        [Tooltip("1 gives every ring the same speed; 2 makes the innermost ring rotate twice as fast as the outermost.")]
        [SerializeField, Min(1f)] private float innerToOuterSpeedRatio = 1.65f;
        [Tooltip("Minimum time after a reversal before another broken ring may reverse the stack again.")]
        [SerializeField, Min(0f)] private float rotationReverseBuffer = 0.45f;

        [Header("Broken Ring Drop")]
        [Tooltip("Initial downward speed shared by all fragments, keeping the broken ring recognizable.")]
        [SerializeField, Min(0f)] private float brokenRingDropSpeed = 0.22f;
        [Tooltip("Gravity multiplier applied to the falling dots before they gather.")]
        [SerializeField, Min(0f)] private float brokenRingGravity = 0.14f;
        [Tooltip("How long the dotted broken ring remains visible.")]
        [SerializeField, Min(0.1f)] private float brokenRingLifetime = 1.65f;
        [Tooltip("Diameter of every dot. A value close to Ring Thickness best matches the reference.")]
        [SerializeField, Min(0.005f)] private float brokenRingDotDiameter = 0.09f;
        [Tooltip("Empty space between neighboring dots around the broken ring.")]
        [SerializeField, Min(0f)] private float brokenRingDotGap = 0.025f;
        [Tooltip("How quickly individual dots separate while the recognizable ring shape falls.")]
        [SerializeField, Min(0f)] private float brokenRingDotScatter = 0.09f;

        [Header("Coin Rewards")]
        [Tooltip("Color the ring dots transition to while assembling into coins.")]
        [SerializeField, ColorUsage(true, true)] private Color coinGoldColor = new Color(1f, 0.69f, 0.08f, 1f);
        [Tooltip("Coins created by the innermost ring.")]
        [SerializeField, Range(1, 10)] private int innerRingCoinReward = 1;
        [Tooltip("Coins created by the outermost ring.")]
        [SerializeField, Range(1, 12)] private int outerRingCoinReward = 6;
        [Tooltip("Coin reward multiplier applied to whichever ring clears the board.")]
        [SerializeField, Range(1f, 10f)] private float lastRingRewardMultiplier = 3f;
        [Tooltip("Higher values postpone most of the reward increase until the outer rings.")]
        [SerializeField, Range(0.25f, 3f)] private float outerRingRewardBias = 1.15f;
        [Tooltip("Diameter of a formed, collectible coin.")]
        [SerializeField, Min(0.05f)] private float coinDiameter = 0.26f;
        [Tooltip("How far beyond the outermost ring coins settle.")]
        [SerializeField, Min(0f)] private float coinOutsideDistance = 0.24f;
        [Tooltip("Random additional distance applied independently to each dispersed coin.")]
        [SerializeField, Min(0f)] private float coinOutsideRandomSpread = 0.3f;
        [Tooltip("Maximum random deflection of each handful from the ring's impact direction.")]
        [SerializeField, Range(0f, 120f)] private float coinDumpDirectionScatter = 55f;
        [Tooltip("Angular looseness within a freshly tossed handful of coins.")]
        [SerializeField, Range(0f, 45f)] private float coinDumpHandfulSpread = 11f;
        [Tooltip("Height of the gravity-like arc followed by a tossed handful.")]
        [SerializeField, Min(0f)] private float coinDumpArcHeight = 0.72f;
        [Tooltip("Time for a formed coin to fan out to its collectible position.")]
        [SerializeField, Min(0.05f)] private float coinDisperseDuration = 0.7f;
        [Tooltip("World-space radius around the mouse that automatically collects a coin.")]
        [SerializeField, Min(0.05f)] private float coinPickupRadius = 0.52f;
        [Tooltip("Time for a collected coin to fly into the HUD.")]
        [SerializeField, Min(0.05f)] private float coinCollectionDuration = 0.55f;
        [Tooltip("Additional delay per world unit so the magnet reaches nearby coins first.")]
        [SerializeField, Range(0f, 0.25f)] private float coinMagnetDelayPerWorldUnit = 0.075f;
        [Tooltip("Additional magnet flight time per world unit from the HUD.")]
        [SerializeField, Range(0f, 0.1f)] private float coinMagnetFlightTimePerWorldUnit = 0.022f;
        [Tooltip("Preallocated collectible coins per simulation cell. No coin GameObjects are spawned at runtime.")]
        [SerializeField, Range(64, 1024)] private int coinPoolCapacity = 512;
        [Tooltip("Maximum simultaneously visible breakup dots per simulation cell.")]
        [SerializeField, Range(512, 8192)] private int fragmentBatchCapacity = 4096;

        [Header("Final Escape Celebration")]
        [Tooltip("Time the escaped ball spends growing before it collapses.")]
        [SerializeField, Min(0.05f)] private float finalBallGrowDuration = 1.35f;
        [Tooltip("Largest size reached by the escaped ball, relative to its normal size.")]
        [SerializeField, Range(1.1f, 10f)] private float finalBallGrowthScale = 7.2f;
        [Tooltip("Time the enlarged ball spends shrinking into its coin explosion.")]
        [SerializeField, Min(0.05f)] private float finalBallShrinkDuration = 0.34f;
        [Tooltip("Maximum local-space shake while the escaped ball is fully charged.")]
        [SerializeField, Range(0f, 0.3f)] private float finalBallShakeStrength = 0.09f;
        [Tooltip("Speed of the escalating shake during the final charge.")]
        [SerializeField, Range(1f, 80f)] private float finalBallShakeFrequency = 34f;
        [Tooltip("Delay before all still-visible ring fragments begin flying into the ball.")]
        [SerializeField, Min(0f)] private float finalFragmentAttractionDelay = 0.2f;
        [Tooltip("Time taken for the remaining ring fragments to reach the ball.")]
        [SerializeField, Min(0.05f)] private float finalFragmentAttractionDuration = 0.95f;
        [Tooltip("Amount of spiral motion applied while fragments are pulled into the ball.")]
        [SerializeField, Range(0f, 1f)] private float finalFragmentAttractionSwirl = 0.26f;
        [Tooltip("Pause after the coin explosion before the simulation rebuilds.")]
        [SerializeField, Min(0f)] private float finalBallResetDelay = 1.05f;
        [Tooltip("Minimum and maximum distance traveled by coins exploding from the ball.")]
        [SerializeField] private Vector2 finalBallCoinBurstDistance = new Vector2(0.75f, 1.8f);

        [Header("Experience & Upgrades")]
        [Tooltip("Experience awarded by the innermost ring.")]
        [SerializeField, Range(1, 10)] private int innerRingExperienceReward = 1;
        [Tooltip("Experience awarded by the outermost ring.")]
        [SerializeField, Range(1, 20)] private int outerRingExperienceReward = 4;
        [Tooltip("Experience multiplier when the last surviving ring is destroyed.")]
        [SerializeField, Range(1f, 10f)] private float finalRingExperienceMultiplier = 3f;
        [Tooltip("Experience required to reach level two.")]
        [SerializeField, Min(10)] private int baseExperienceRequirement = 100;
        [Tooltip("Required experience multiplier applied after every level.")]
        [SerializeField, Range(1f, 3f)] private float experienceRequirementGrowth = 1.42f;
        [Tooltip("Flight time for experience particles entering the progress bar.")]
        [SerializeField, Min(0.1f)] private float experienceCollectionDuration = 0.72f;
        [Tooltip("Maximum simultaneously visible experience particles per simulation.")]
        [SerializeField, Range(24, 256)] private int experienceParticlePoolCapacity = 96;
        [SerializeField, ColorUsage(true, true)] private Color experienceColor =
            new Color(0.12f, 0.82f, 1f, 1f);
        [Tooltip("How quickly the displayed bar catches up to newly awarded experience.")]
        [SerializeField, Range(2f, 30f)] private float experienceFillSmoothing = 11f;
        [Tooltip("Duration of the punch animation whenever experience reaches the bar.")]
        [SerializeField, Range(0.05f, 0.6f)] private float experienceFillPulseDuration = 0.2f;
        [Tooltip("Scale punch applied whenever experience reaches the bar.")]
        [SerializeField, Range(0f, 0.3f)] private float experienceFillPulseScale = 0.09f;
        [Tooltip("World-space width and height of the experience bar.")]
        [SerializeField] private Vector2 experienceBarSize = new Vector2(6.4f, 0.42f);
        [Tooltip("Distance from the top of the screen to the experience bar center.")]
        [SerializeField, Range(24f, 140f)] private float experienceBarTopOffset = 62f;
        [Tooltip("Maximum point size used by the experience label.")]
        [SerializeField, Range(16, 42)] private int experienceBarFontSize = 26;
        [Tooltip("Time the completed bar celebrates before upgrade choices appear.")]
        [SerializeField, Range(0.4f, 3f)] private float levelUpFanfareDuration = 1.15f;
        [Tooltip("Scale punch applied while the completed bar celebrates.")]
        [SerializeField, Range(0f, 0.5f)] private float levelUpFanfareScale = 0.2f;
        [Tooltip("Number of pooled UI sparks emitted when the experience bar fills.")]
        [SerializeField, Range(12, 64)] private int levelUpFanfareParticleCount = 32;

        [Header("Upgrade Colors")]
        [SerializeField, ColorUsage(true, true)] private Color ballVelocityUpgradeColor =
            new Color(0.04f, 0.34f, 0.72f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color coinYieldUpgradeColor =
            new Color(0.76f, 0.4f, 0.035f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color experienceGainUpgradeColor =
            new Color(0.42f, 0.18f, 0.72f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color pickupRadiusUpgradeColor =
            new Color(0.035f, 0.5f, 0.35f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color overdriveUpgradeColor =
            new Color(0.66f, 0.08f, 0.48f, 1f);
        [SerializeField, ColorUsage(true, true)] private Color elasticityUpgradeColor =
            new Color(0.72f, 0.17f, 0.11f, 1f);

        [Header("Power Ups")]
        [Tooltip("Coin price for unlocking Ball Overdrive.")]
        [SerializeField, Min(1)] private int speedBoostUnlockCost = 100;
        [Tooltip("Coin price for unlocking Coin Magnet.")]
        [SerializeField, Min(1)] private int coinMagnetUnlockCost = 400;
        [Tooltip("Player level at which Coin Magnet becomes purchasable.")]
        [SerializeField, Min(1)] private int coinMagnetUnlockLevel = 2;
        [Tooltip("Coin price for unlocking Gold Rush.")]
        [SerializeField, Min(1)] private int coinProductionUnlockCost = 850;
        [Tooltip("Player level at which Gold Rush becomes purchasable.")]
        [SerializeField, Min(1)] private int coinProductionUnlockLevel = 4;
        [Tooltip("Coin price for unlocking Ball Multiplier.")]
        [SerializeField, Min(1)] private int ballMultiplierUnlockCost = 1250;
        [Tooltip("Player level at which Ball Multiplier becomes purchasable.")]
        [SerializeField, Min(1)] private int ballMultiplierUnlockLevel = 6;
        [SerializeField] private bool speedBoostUnlocked;
        [SerializeField] private bool coinMagnetUnlocked;
        [SerializeField] private bool coinProductionUnlocked;
        [SerializeField] private bool ballMultiplierUnlocked;
        [SerializeField, Min(1f)] private float speedBoostMultiplier = 3.2f;
        [SerializeField, Min(0.1f)] private float speedBoostDuration = 15f;
        [SerializeField, Min(0.1f)] private float speedBoostCooldown = 25f;
        [SerializeField, Min(0.1f)] private float coinMagnetCooldown = 8f;
        [SerializeField, Min(1f)] private float coinProductionMultiplier = 2f;
        [SerializeField, Min(0.1f)] private float coinProductionDuration = 10f;
        [SerializeField, Min(0.1f)] private float coinProductionCooldown = 20f;
        [SerializeField, Min(0.1f)] private float ballMultiplierDuration = 12f;
        [SerializeField, Min(0.1f)] private float ballMultiplierCooldown = 26f;
        [Tooltip("Angle separating each temporary clone from its original ball trajectory.")]
        [SerializeField, Range(1f, 90f)] private float ballCloneDivergenceDegrees = 22f;

        private readonly List<Ring> _rings = new List<Ring>();
        private readonly List<Mesh> _runtimeMeshes = new List<Mesh>();
        private readonly List<Material> _runtimeMaterials = new List<Material>();
        private readonly List<Texture2D> _runtimeTextures = new List<Texture2D>();
        private readonly List<Sprite> _runtimeSprites = new List<Sprite>();
        private readonly List<CoinFormation> _coinFormations = new List<CoinFormation>();
        private readonly List<CollectibleCoin> _coins = new List<CollectibleCoin>();
        private readonly Stack<CollectibleCoin> _coinPool = new Stack<CollectibleCoin>();
        private readonly List<ExperienceParticle> _experienceParticles =
            new List<ExperienceParticle>();
        private readonly Stack<ExperienceParticle> _experienceParticlePool =
            new Stack<ExperienceParticle>();
        private readonly List<RingEscapeSimulation> _gridCells = new List<RingEscapeSimulation>();

        private Camera _camera;
        private Material _unlitMaterial;
        private Material _backgroundMaterial;
        private Transform _backgroundTransform;
        private Mesh _circleMesh;
        private Mesh _coinMesh;
        private Mesh _coinBatchMesh;
        private MeshRenderer _coinBatchRenderer;
        private Vector3[] _coinBaseVertices;
        private Color[] _coinBaseColors;
        private int[] _coinBaseTriangles;
        private Vector3[] _coinBatchVertices;
        private Color[] _coinBatchColors;
        private int[] _coinBatchTriangles;
        private Mesh _experienceBatchMesh;
        private MeshRenderer _experienceBatchRenderer;
        private Vector3[] _experienceBatchVertices;
        private Color[] _experienceBatchColors;
        private int[] _experienceBatchTriangles;
        private Mesh _combinedRingMesh;
        private MeshRenderer _combinedRingRenderer;
        private MeshRenderer _combinedRingShadowRenderer;
        private MeshRenderer _combinedRingHighlightRenderer;
        private Vector3[] _combinedRingBaseVertices;
        private Vector3[] _combinedRingVertices;
        private Color[] _combinedRingColors;
        private int _ringMeshSegmentCount = FullscreenRingMeshSegments;
        private float _simulationStep = SimulationStep;
        private Mesh _fragmentBatchMesh;
        private MeshRenderer _fragmentBatchRenderer;
        private Vector3[] _fragmentBatchVertices;
        private Color[] _fragmentBatchColors;
        private int[] _fragmentBatchTriangles;
        private MeshRenderer _ballRenderer;
        private MeshRenderer _ballGlowRenderer;
        private MeshRenderer _ballRimRenderer;
        private MeshRenderer _ballHighlightRenderer;
        private TrailRenderer _ballTrailRenderer;
        private MeshRenderer _cloneBallRenderer;
        private MeshRenderer _cloneBallGlowRenderer;
        private MeshRenderer _cloneBallRimRenderer;
        private MeshRenderer _cloneBallHighlightRenderer;
        private TrailRenderer _cloneBallTrailRenderer;
        private Transform _ballTransform;
        private Transform _ballGlowTransform;
        private Transform _ballTrailTransform;
        private Transform _cloneBallTransform;
        private Transform _cloneBallGlowTransform;
        private Transform _cloneBallTrailTransform;
        private Transform _pickupRadiusTransform;
        private MeshRenderer _pickupRadiusRenderer;
        private MaterialPropertyBlock _propertyBlock;

        private Vector2 _ballPosition;
        private Vector2 _ballVelocity;
        private Vector2 _cloneBallPosition;
        private Vector2 _cloneBallVelocity;
        private BouncePatternState _ballBouncePattern;
        private BouncePatternState _cloneBallBouncePattern;
        private bool _cloneBallActive;
        private float _accumulator;
        private float _emptyTimer;
        private float _rotationDirection;
        private float _rotationReverseCooldown;
        private float _innermostTintUpdateCooldown;
        private int _roundSequence;
        private int _coinCount;
        private bool _isPaused;
        private System.Random _random;
        private Ring _tintedInnermostRing;
        private RingEscapeSimulation _gridOwner;
        private bool _isGridRoot;
        private bool _showPickupRadiusIndicator = true;
        private int _simulationSeed = 19770419;
        private float _initialRingRotationDegrees = 67f;
        private float _lastGridAspect = -1f;
        private float _backgroundWorldScale = 1f;
        private float _targetBackgroundWorldScale = 1f;
        private float _backgroundWorldScaleVelocity;
        private float _speedBoostRemaining;
        private float _speedBoostCooldownRemaining;
        private float _coinMagnetCooldownRemaining;
        private float _coinProductionRemaining;
        private float _coinProductionCooldownRemaining;
        private float _ballMultiplierRemaining;
        private float _ballMultiplierCooldownRemaining;
        private float _resetRingRevealElapsed;
        private bool _resetRingRevealActive;
        private bool _coinColorLayoutDirty = true;
        private float _ballImpactSquashRemaining;
        private Vector2 _ballImpactNormal = Vector2.right;
        private bool _finalEscapeActive;
        private bool _finalBallExploded;
        private float _finalEscapeElapsed;
        private int _finalBallBurstCoinCount;
        private Color _ballColor;
        private Color _ballGlowColor;
        private Color _pickupRadiusColor;
        private SimulationColorDatabase.ColorProfile _activeColorProfile;
        private TMP_Text _coinHudText;
        private TMP_Text _buySimulationButtonText;
        private TMP_Text _speedBoostButtonText;
        private TMP_Text _coinMagnetButtonText;
        private TMP_Text _coinProductionButtonText;
        private TMP_Text _ballMultiplierButtonText;
        private Button _speedBoostButton;
        private Button _buySimulationButton;
        private Button _coinMagnetButton;
        private Button _coinProductionButton;
        private Button _ballMultiplierButton;
        private Image _speedBoostButtonImage;
        private Image _coinMagnetButtonImage;
        private Image _coinProductionButtonImage;
        private Image _ballMultiplierButtonImage;
        private Outline _speedBoostButtonOutline;
        private Outline _coinMagnetButtonOutline;
        private Outline _coinProductionButtonOutline;
        private Outline _ballMultiplierButtonOutline;
        private TMP_Text _simulationCountText;
        private Sprite _roundedUiSprite;
        private ParticleSystem _backgroundParticles;
        private ProgressBarView _experienceProgressBar;
        private TMP_Text _experienceProgressText;
        private Canvas _hudCanvas;
        private Image _experienceFanfareFlash;
        private GameObject _upgradeChoiceOverlay;
        private TMP_Text _upgradeChoiceTitle;
        private readonly Button[] _upgradeChoiceButtons = new Button[3];
        private readonly TMP_Text[] _upgradeChoiceLabels = new TMP_Text[3];
        private readonly UpgradeType[] _offeredUpgrades = new UpgradeType[3];
        private int _playerLevel = 1;
        private int _currentExperience;
        private int _experienceRequired;
        private bool _isChoosingUpgrade;
        private bool _levelUpFanfareActive;
        private bool _levelUpFanfareBurstStarted;
        private float _levelUpFanfareElapsed;
        private float _levelUpFullBurstElapsed;
        private float _displayedExperienceProgress;
        private float _targetExperienceProgress;
        private float _experienceFillPulseRemaining;
        private bool _experienceProgressInitialized;
        private float _coinYieldUpgradeMultiplier = 1f;
        private float _experienceGainMultiplier = 1f;
        private readonly int[] _upgradeRanks =
            new int[Enum.GetValues(typeof(UpgradeType)).Length];

        public enum Palette
        {
            BlueGoldGradient,
            Lavender
        }

        public enum SimulationShape
        {
            Circle = 0,
            Square = 4,
            Hexagon = 6,
            Octagon = 8,
            Dodecagon = 12
        }

        private sealed class Ring
        {
            public float Radius;
            public float AngleOffset;
            public float NormalizedRadius;
            public float RotationDegrees;
            public float GapOffsetDegrees;
            public Color Color;
            public int VertexStart;
            public bool IsAlive;
        }

        private struct ShapeBoundaryInfo
        {
            public float Distance;
            public bool IsInside;
            public Vector2 OutwardNormal;
            public Vector2 ClosestPoint;
        }

        private struct RingContactInfo
        {
            public Vector2 Point;
            public Vector2 Normal;
            public float Separation;
        }

        private struct BouncePatternState
        {
            public Vector2 LastOutgoingDirection;
            public float DeflectionSign;
            public float TimeSinceBounce;
            public int StraightReturnCount;
        }

        private sealed class FragmentMotion
        {
            public Vector2 StartPosition;
            public Vector2 InitialVelocity;
            public Vector2 GatherStartPosition;
            public Vector2 CoinTargetPosition;
        }

        private sealed class CoinFormation
        {
            public readonly List<FragmentMotion> FragmentMotions = new List<FragmentMotion>();
            public Color RingColor;
            public float Elapsed;
            public float ScatterDuration;
            public float GatherDuration;
            public Vector2[] CoinCenters;
            public Vector2[] DisperseTargets;
            public Vector2[] DisperseArcOffsets;
            public float[] DisperseDurationScales;
            public int StandardCoinCount;
        }

        private enum CoinState
        {
            Dispersing,
            Available,
            Collecting
        }

        private enum UpgradeType
        {
            BallVelocity,
            CoinYield,
            ExperienceGain,
            PickupRadius,
            Overdrive,
            Elasticity
        }

        private sealed class CollectibleCoin
        {
            public Vector2 Position;
            public Vector2 StartPosition;
            public Vector2 TargetPosition;
            public Vector2 CollectionStartPosition;
            public Vector2 DisperseArcOffset;
            public float Scale;
            public float DisperseDurationScale;
            public float Elapsed;
            public float PulseOffset;
            public float CollectionDuration;
            public float CollectionArcHeight;
            public bool IsBonus;
            public CoinState State;
        }

        private sealed class ExperienceParticle
        {
            public Vector2 Position;
            public Vector2 StartPosition;
            public Vector2 BurstPosition;
            public Vector2 ControlOffset;
            public float Elapsed;
            public float BurstDuration;
            public float FlightDuration;
            public float Scale;
            public float PulseOffset;
        }

        private sealed class LevelUpUiParticle
        {
            public RectTransform RectTransform;
            public Image Image;
            public Vector2 StartPosition;
            public Vector2 Velocity;
            public float Delay;
            public float SpinSpeed;
            public float Size;
            public Color Color;
        }

        private readonly List<LevelUpUiParticle> _levelUpUiParticles =
            new List<LevelUpUiParticle>(64);

        private sealed class ImpactParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Elapsed;
            public float Lifetime;
            public float Diameter;
        }

        private readonly List<ImpactParticle> _impactParticles = new List<ImpactParticle>(64);
        private readonly Stack<ImpactParticle> _impactParticlePool = new Stack<ImpactParticle>(64);

        private void Awake()
        {
            ClampRingGeometry();
            ApplySkySanctuaryThemeDefaults();
            Application.targetFrameRate = 60;
            _experienceRequired = ExperienceRequirementForLevel(_playerLevel);

            if (useSimulationGrid && _gridOwner == null)
            {
                _isGridRoot = true;
                BuildSimulationGrid();
                CreateProceduralBackground();
                CreateGameUi();
                return;
            }

            _propertyBlock = new MaterialPropertyBlock();
            _random = new System.Random(_simulationSeed);
            if (_gridOwner == null && colorDatabase != null && colorDatabase.ProfileCount > 0)
            {
                var colorRandom =
                    new System.Random(unchecked(_simulationSeed ^ ColorSessionSeed));
                colorProfileIndex = colorRandom.Next(colorDatabase.ProfileCount);
            }

            if (_gridOwner == null)
            {
                ConfigureCamera();
                CreateProceduralBackground();
            }
            else
            {
                _camera = Camera.main;
            }
            CreateMaterial();
            ApplySelectedColorProfile();
            CreateBall();
            CreateFragmentBatchRenderer();
            if (_showPickupRadiusIndicator)
            {
                CreatePickupRadiusIndicator();
            }
            BuildRings();
            ResetSimulation();
            if (_gridOwner == null)
            {
                CreateGameUi();
            }
        }

        private void ApplySkySanctuaryThemeDefaults()
        {
            experienceColor = new Color(0.17f, 0.78f, 0.7f, 1f);
            coinGoldColor = SanctuaryGold;
            ballVelocityUpgradeColor = new Color(0.16f, 0.6f, 0.84f, 1f);
            coinYieldUpgradeColor = new Color(0.96f, 0.67f, 0.18f, 1f);
            experienceGainUpgradeColor = new Color(0.5f, 0.7f, 0.3f, 1f);
            pickupRadiusUpgradeColor = new Color(0.14f, 0.68f, 0.63f, 1f);
            overdriveUpgradeColor = new Color(0.95f, 0.36f, 0.24f, 1f);
            elasticityUpgradeColor = new Color(0.65f, 0.46f, 0.82f, 1f);
        }

        private void OnValidate()
        {
            gridColumns = Mathf.Clamp(gridColumns, 1, MaxDemoSimulations);
            gridRows = Mathf.Clamp(gridRows, 1, MaxDemoSimulations);
            activeSimulationCount = Mathf.Clamp(
                activeSimulationCount,
                1,
                MaxDemoSimulations);
            coinMagnetSimulationGate = Mathf.Clamp(
                coinMagnetSimulationGate,
                2,
                MaxDemoSimulations);
            coinProductionSimulationGate = Mathf.Clamp(
                coinProductionSimulationGate,
                coinMagnetSimulationGate,
                MaxDemoSimulations);
            ballMultiplierSimulationGate = Mathf.Clamp(
                ballMultiplierSimulationGate,
                coinProductionSimulationGate,
                MaxDemoSimulations);
            ClampRingGeometry();
            outerRingCoinReward = Mathf.Max(innerRingCoinReward, outerRingCoinReward);
            if (Application.isPlaying && _rings.Count > 0 && _propertyBlock != null)
            {
                ApplySelectedColorProfile();
                ApplyPalette();
            }
            if (Application.isPlaying)
            {
                UpdateProceduralBackground();
            }
        }

        private void ClampRingGeometry()
        {
            ringCount = Mathf.Clamp(ringCount, 2, 120);
            float spacing =
                (MaximumRingApothem() - InnerRadius) / (ringCount - 1);
            float maximumThickness = Mathf.Max(0.005f, spacing - MinimumRingGap);
            ringThickness = Mathf.Clamp(ringThickness, 0.005f, maximumThickness);
        }

        private int ShapeSideCount()
        {
            return Mathf.Max(0, (int)simulationShape);
        }

        private float MaximumRingApothem()
        {
            int sideCount = ShapeSideCount();
            return sideCount >= 3
                ? OuterRadius * Mathf.Cos(Mathf.PI / sideCount)
                : OuterRadius;
        }

        private void Update()
        {
            if (_isGridRoot)
            {
                UpdateBackgroundAnimationClock();
                if (!Mathf.Approximately(_lastGridAspect, CameraAspect()))
                {
                    ConfigureGridCamera();
                }
                UpdateBackgroundZoom(Time.unscaledDeltaTime);
                UpdatePowerUps(Time.deltaTime);
                UpdateGameUi();
                UpdateProgressionEffects(Time.unscaledDeltaTime);
                return;
            }

            if (_gridOwner == null)
            {
                UpdateBackgroundAnimationClock();
                if (!Mathf.Approximately(_lastGridAspect, CameraAspect()))
                {
                    UpdateProceduralBackground();
                }
                UpdatePowerUps(Time.deltaTime);
                UpdateGameUi();
                UpdateProgressionEffects(Time.unscaledDeltaTime);
            }

            ReadControls();
            if (_isPaused)
            {
                UpdateCoinRewards(Time.deltaTime);
                UpdateVisuals();
                return;
            }

            if (_resetRingRevealActive)
            {
                _resetRingRevealElapsed += Time.deltaTime;
                if (_resetRingRevealElapsed >= resetRingRevealDuration)
                {
                    _resetRingRevealElapsed = resetRingRevealDuration;
                    _resetRingRevealActive = false;
                }

                UpdateCoinRewards(Time.deltaTime);
                UpdateVisuals();
                return;
            }

            _accumulator += Mathf.Min(Time.deltaTime, 0.05f);
            while (_accumulator >= _simulationStep)
            {
                int movementSubsteps = CalculateCollisionSubsteps();
                float movementStep = _simulationStep / movementSubsteps;
                for (int substep = 0; substep < movementSubsteps; substep++)
                {
                    Simulate(movementStep);
                }
                _accumulator -= _simulationStep;
            }

            UpdateCoinRewards(Time.deltaTime);
            UpdateVisuals();
        }

        private int CalculateCollisionSubsteps()
        {
            float fastestBallSpeed = _ballVelocity.magnitude;
            if (_cloneBallActive)
            {
                fastestBallSpeed = Mathf.Max(
                    fastestBallSpeed,
                    _cloneBallVelocity.magnitude);
            }

            float speedMultiplier = CurrentBallSpeedMultiplier();
            float predictedSpeed =
                fastestBallSpeed + gravity * _simulationStep;
            float predictedTravel =
                predictedSpeed * speedMultiplier * _simulationStep;
            float maximumTravel = Mathf.Max(
                0.008f,
                BallRadius * collisionTravelRadiusFraction);
            int travelSubsteps = Mathf.CeilToInt(
                predictedTravel / maximumTravel);
            int powerupSubsteps = Mathf.CeilToInt(speedMultiplier);
            return Mathf.Clamp(
                Mathf.Max(1, Mathf.Max(travelSubsteps, powerupSubsteps)),
                1,
                maximumCollisionSubsteps);
        }

        private void BuildSimulationGrid()
        {
            ConfigureGridCamera();
            string serializedSettings = JsonUtility.ToJson(this);
            int gridCapacity = Mathf.Clamp(
                gridColumns * gridRows,
                1,
                MaxDemoSimulations);

            for (int cellIndex = 0;
                 cellIndex < gridCapacity;
                 cellIndex++)
            {
                var cellObject =
                    new GameObject($"Simulation {cellIndex + 1:00}");
                cellObject.SetActive(false);
                cellObject.transform.SetParent(transform, false);
                cellObject.transform.localPosition = Vector3.zero;
                cellObject.transform.localScale = Vector3.one;

                var cell =
                    cellObject.AddComponent<RingEscapeSimulation>();
                JsonUtility.FromJsonOverwrite(serializedSettings, cell);
                cell.colorDatabase = colorDatabase;
                cell.colorProfileIndex = colorProfileIndex;
                cell.ConfigureAsGridCell(
                    this,
                    gridRandomSeed + cellIndex * 7919,
                    cellIndex == 0);
                _gridCells.Add(cell);
                cellObject.SetActive(true);
            }

            SetActiveSimulationCount(activeSimulationCount);
        }

        private void SetActiveSimulationCount(int requestedCount)
        {
            if (_gridCells.Count == 0)
            {
                return;
            }

            activeSimulationCount = Mathf.Clamp(requestedCount, 1, _gridCells.Count);
            for (int index = 0; index < _gridCells.Count; index++)
            {
                bool shouldBeActive = index < activeSimulationCount;
                GameObject cellObject = _gridCells[index].gameObject;
                if (cellObject.activeSelf != shouldBeActive)
                {
                    cellObject.SetActive(shouldBeActive);
                }
                if (shouldBeActive &&
                    _ballMultiplierRemaining > 0f)
                {
                    _gridCells[index].SpawnBallClone();
                }
                else if (!shouldBeActive)
                {
                    _gridCells[index].DisableBallClone(true);
                }
            }

            RelayoutSimulationGrid();
            UpdateGameUi();
        }

        private void RelayoutSimulationGrid()
        {
            if (_camera == null || _gridCells.Count == 0)
            {
                return;
            }

            int count = Mathf.Clamp(activeSimulationCount, 1, _gridCells.Count);
            float aspect = CameraAspect();
            float verticalExtent = _camera.orthographicSize;
            const float horizontalMargin = 0.28f;
            const float topMargin = 0.62f;
            const float bottomUiReserve = 1.48f;
            float availableWidth = verticalExtent * 2f * aspect - horizontalMargin * 2f;
            float availableHeight =
                verticalExtent * 2f - topMargin - bottomUiReserve;

            int bestColumns = 1;
            int bestRows = count;
            float bestCellSize = 0f;
            int bestGridDifference = int.MaxValue;
            int bestEmptyCellCount = int.MaxValue;
            bool useThreeWideShowcase =
                count == 3 && aspect >= 1.35f;
            if (useThreeWideShowcase)
            {
                // Three cells fit substantially larger in one row on a wide
                // display. A forced 2x2 layout wastes an entire slot and makes
                // every simulation roughly one third smaller.
                bestColumns = 3;
                bestRows = 1;
                bestCellSize = Mathf.Min(
                    availableWidth / bestColumns,
                    availableHeight);
            }
            else
            {
                for (int columns = 1; columns <= count; columns++)
                {
                    int rows = Mathf.CeilToInt(count / (float)columns);
                    int gridDifference = Mathf.Abs(columns - rows);
                    int emptyCellCount = columns * rows - count;
                    float candidateCellSize = Mathf.Min(
                        availableWidth / columns,
                        availableHeight / rows);
                    bool isBetterLayout =
                        gridDifference < bestGridDifference ||
                        (gridDifference == bestGridDifference &&
                         emptyCellCount < bestEmptyCellCount) ||
                        (gridDifference == bestGridDifference &&
                         emptyCellCount == bestEmptyCellCount &&
                         candidateCellSize > bestCellSize);
                    if (isBetterLayout)
                    {
                        bestGridDifference = gridDifference;
                        bestEmptyCellCount = emptyCellCount;
                        bestCellSize = candidateCellSize;
                        bestColumns = columns;
                        bestRows = rows;
                    }
                }
            }

            float cellWidth = availableWidth / bestColumns;
            float cellHeight = availableHeight / bestRows;
            float simulationExtent =
                OuterRadius + coinOutsideDistance + coinOutsideRandomSpread + coinDiameter;
            float cellScale = Mathf.Max(
                0.01f,
                (Mathf.Min(cellWidth, cellHeight) - gridCellPadding) /
                (simulationExtent * 2f));
            float singleSimulationScale = Mathf.Max(
                0.01f,
                (Mathf.Min(availableWidth, availableHeight) - gridCellPadding) /
                (simulationExtent * 2f));
            float layoutCompression = Mathf.Max(
                1f,
                singleSimulationScale / cellScale);
            _targetBackgroundWorldScale = Mathf.Clamp(
                Mathf.Pow(layoutCompression, backgroundZoomResponse),
                1f,
                backgroundMaximumWorldScale);
            if (_backgroundTransform == null)
            {
                _backgroundWorldScale = _targetBackgroundWorldScale;
            }
            float verticalCenter = (bottomUiReserve - topMargin) * 0.5f;
            float packedSimulationDiameter =
                simulationExtent * 2f * cellScale;
            float horizontalSpacing = Mathf.Min(
                cellWidth,
                packedSimulationDiameter + gridCellPadding);
            float verticalSpacing = Mathf.Min(
                cellHeight,
                packedSimulationDiameter + gridCellPadding);

            for (int index = 0; index < count; index++)
            {
                int row = index / bestColumns;
                int column = index % bestColumns;
                int itemsInRow = Mathf.Min(bestColumns, count - row * bestColumns);
                float x =
                    (column - (itemsInRow - 1) * 0.5f) *
                    horizontalSpacing;
                float y =
                    verticalCenter +
                    ((bestRows - 1) * 0.5f - row) *
                    verticalSpacing;
                Transform cellTransform = _gridCells[index].transform;
                cellTransform.localPosition = new Vector3(x, y, 0f);
                cellTransform.localScale = Vector3.one * cellScale;
            }
        }

        private void ConfigureAsGridCell(
            RingEscapeSimulation owner,
            int seed,
            bool showPickupRadius)
        {
            useSimulationGrid = false;
            _gridOwner = owner;
            _showPickupRadiusIndicator = showPickupRadius;
            _simulationSeed = seed;
            _ringMeshSegmentCount = GridRingMeshSegments;
            _simulationStep = GridSimulationStep;

            var cellRandom = new System.Random(seed);
            if (colorDatabase != null && colorDatabase.ProfileCount > 0)
            {
                var colorRandom = new System.Random(unchecked(seed ^ ColorSessionSeed));
                colorProfileIndex = colorRandom.Next(colorDatabase.ProfileCount);
            }
            if (randomizeGridShapes &&
                gridShapePool != null &&
                gridShapePool.Length > 0)
            {
                simulationShape =
                    gridShapePool[cellRandom.Next(gridShapePool.Length)];
            }
            launchAngleDegrees = Mathf.Lerp(-180f, 180f, NextRandom01(cellRandom));
            launchSpeed *= Mathf.Lerp(
                gridLaunchSpeedMultiplier.x,
                gridLaunchSpeedMultiplier.y,
                NextRandom01(cellRandom));

            float rotationMultiplier = Mathf.Lerp(
                gridRotationSpeedMultiplier.x,
                gridRotationSpeedMultiplier.y,
                NextRandom01(cellRandom));
            float randomizedDirection =
                NextRandom01(cellRandom) < gridReverseRotationChance ? -1f : 1f;
            rotationSpeedDegrees =
                Mathf.Abs(rotationSpeedDegrees) * rotationMultiplier * randomizedDirection;
            innerToOuterSpeedRatio = Mathf.Max(
                1f,
                innerToOuterSpeedRatio *
                Mathf.Lerp(
                    gridInnerOuterRatioMultiplier.x,
                    gridInnerOuterRatioMultiplier.y,
                    NextRandom01(cellRandom)));
            _initialRingRotationDegrees = Mathf.Lerp(0f, 360f, NextRandom01(cellRandom));
        }

        private static float NextRandom01(System.Random random)
        {
            return (float)random.NextDouble();
        }

        private void ReadControls()
        {
            RingEscapeSimulation host = _gridOwner != null ? _gridOwner : this;
            if (host._isChoosingUpgrade)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetSimulation();
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                _isPaused = !_isPaused;
            }
        }

        private void ConfigureCamera()
        {
            _camera = GetOrCreateCamera();
            _camera.orthographic = true;
            _camera.orthographicSize = OuterRadius + 0.72f;
            _camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.2f, 0.64f, 0.9f, 1f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 30f;
            _camera.allowHDR = true;
            _lastGridAspect = CameraAspect();
            UpdateProceduralBackground();
        }

        private void ConfigureGridCamera()
        {
            _camera = GetOrCreateCamera();
            float aspect = CameraAspect();

            _camera.orthographic = true;
            _camera.orthographicSize = OuterRadius + 0.72f;
            _camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.2f, 0.64f, 0.9f, 1f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 30f;
            _camera.allowHDR = true;
            _lastGridAspect = aspect;
            if (_gridCells.Count > 0)
            {
                RelayoutSimulationGrid();
            }
            UpdateProceduralBackground();
        }

        private void CreateProceduralBackground()
        {
            if (_backgroundTransform != null || _camera == null)
            {
                return;
            }

            Shader shader = Shader.Find("BallBounce/Sky Sanctuary Background");
            if (shader == null)
            {
                Debug.LogWarning("Sky Sanctuary background shader was not found.");
                return;
            }

            _backgroundMaterial = new Material(shader)
            {
                name = "Sky Sanctuary Background Material",
                hideFlags = HideFlags.DontSave
            };
            Texture2D backgroundTexture =
                Resources.Load<Texture2D>("Art/SkySanctuaryBackground");
            if (backgroundTexture != null)
            {
                _backgroundMaterial.SetTexture(
                    BackgroundTextureId,
                    backgroundTexture);
            }
            else
            {
                Debug.LogWarning(
                    "Sky Sanctuary background texture was not found in Resources/Art.");
            }
            _backgroundMaterial.SetFloat(AuroraIntensityId, backgroundAuroraIntensity);
            _backgroundMaterial.SetFloat(OrbitIntensityId, backgroundOrbitIntensity);
            _backgroundMaterial.SetFloat(StarIntensityId, backgroundStarIntensity);
            _backgroundMaterial.SetFloat(BackgroundMotionSpeedId, backgroundMotionSpeed);
            _runtimeMaterials.Add(_backgroundMaterial);

            var mesh = new Mesh { name = "Fullscreen Background Quad" };
            mesh.vertices = new[]
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3(1f, -1f, 0f),
                new Vector3(-1f, 1f, 0f),
                new Vector3(1f, 1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 0.1f));
            _runtimeMeshes.Add(mesh);

            var backgroundObject = new GameObject("Painted Sky Sanctuary");
            backgroundObject.transform.SetParent(transform, false);
            var filter = backgroundObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = backgroundObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _backgroundMaterial;
            renderer.sortingOrder = -100;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _backgroundTransform = backgroundObject.transform;
            CreateBackgroundParticles();
            UpdateProceduralBackground();
        }

        private void CreateBackgroundParticles()
        {
            if (_backgroundParticles != null || _camera == null)
            {
                return;
            }

            Shader particleShader = Shader.Find("BallBounce/Background Mote");
            if (particleShader == null)
            {
                Debug.LogWarning("Background mote shader was not found.");
                return;
            }

            var particleMaterial = new Material(particleShader)
            {
                name = "Background Mote Material",
                hideFlags = HideFlags.DontSave
            };
            _runtimeMaterials.Add(particleMaterial);

            var particleObject = new GameObject("Drifting Background Motes");
            particleObject.transform.SetParent(transform, false);
            particleObject.transform.position = new Vector3(
                _camera.transform.position.x,
                _camera.transform.position.y,
                7.2f);

            _backgroundParticles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _backgroundParticles.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = backgroundParticleCount;
            main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 32f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(
                backgroundParticleSize.x,
                backgroundParticleSize.y);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.91f, 0.54f, 0.5f),
                new Color(0.82f, 1f, 0.78f, 0.4f));
            main.startRotation = new ParticleSystem.MinMaxCurve(
                0f,
                Mathf.PI * 2f);

            ParticleSystem.EmissionModule emission = _backgroundParticles.emission;
            emission.rateOverTime = backgroundParticleCount / 24f;

            ParticleSystem.ShapeModule shape = _backgroundParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;

            ParticleSystem.VelocityOverLifetimeModule velocity =
                _backgroundParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(
                -backgroundParticleSpeed,
                backgroundParticleSpeed);
            velocity.y = new ParticleSystem.MinMaxCurve(
                backgroundParticleSpeed * 0.12f,
                backgroundParticleSpeed * 0.72f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.NoiseModule noise = _backgroundParticles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = backgroundParticleSpeed * 0.38f;
            noise.frequency = 0.1f;
            noise.scrollSpeed = backgroundMotionSpeed * 0.1f;

            ParticleSystem.RotationOverLifetimeModule rotation =
                _backgroundParticles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                _backgroundParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fadeGradient = new Gradient();
            fadeGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.94f, 0.62f), 0.55f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.64f, 0.18f),
                    new GradientAlphaKey(0.46f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = fadeGradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                _backgroundParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(0.24f, 1f),
                    new Keyframe(0.7f, 0.72f),
                    new Keyframe(1f, 0.22f)));

            var particleRenderer =
                particleObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = particleMaterial;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = -90;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            UpdateBackgroundParticleBounds();
            _backgroundParticles.Play();
        }

        private void UpdateBackgroundParticleBounds()
        {
            if (_backgroundParticles == null || _camera == null)
            {
                return;
            }

            float verticalExtent = _camera.orthographicSize;
            float horizontalExtent = verticalExtent * CameraAspect();
            Transform particleTransform = _backgroundParticles.transform;
            particleTransform.position = new Vector3(
                _camera.transform.position.x,
                _camera.transform.position.y,
                7.2f);
            float backgroundScale = Mathf.Max(
                1f,
                _backgroundWorldScale);
            particleTransform.localScale =
                Vector3.one / backgroundScale;
            ParticleSystem.ShapeModule shape = _backgroundParticles.shape;
            shape.scale = new Vector3(
                horizontalExtent * 2f * backgroundScale,
                verticalExtent * 2f * backgroundScale,
                0.01f);
        }

        private void UpdateProceduralBackground()
        {
            if (_backgroundTransform == null || _camera == null)
            {
                return;
            }

            float aspect = CameraAspect();
            float verticalExtent = _camera.orthographicSize;
            Vector3 cameraPosition = _camera.transform.position;
            _backgroundTransform.position =
                new Vector3(cameraPosition.x, cameraPosition.y, 8f);
            _backgroundTransform.localScale =
                new Vector3(verticalExtent * aspect, verticalExtent, 1f);
            UpdateBackgroundParticleBounds();
            if (_backgroundMaterial != null)
            {
                _backgroundMaterial.SetFloat(BackgroundAspectId, aspect);
                _backgroundMaterial.SetFloat(AuroraIntensityId, backgroundAuroraIntensity);
                _backgroundMaterial.SetFloat(OrbitIntensityId, backgroundOrbitIntensity);
                _backgroundMaterial.SetFloat(StarIntensityId, backgroundStarIntensity);
                _backgroundMaterial.SetFloat(BackgroundMotionSpeedId, backgroundMotionSpeed);
                _backgroundMaterial.SetFloat(
                    BackgroundWorldScaleId,
                    _backgroundWorldScale);
            }
            _lastGridAspect = aspect;
        }

        private void UpdateBackgroundZoom(float deltaTime)
        {
            if (Mathf.Abs(
                    _backgroundWorldScale -
                    _targetBackgroundWorldScale) < 0.0005f)
            {
                _backgroundWorldScale = _targetBackgroundWorldScale;
                _backgroundWorldScaleVelocity = 0f;
                return;
            }

            _backgroundWorldScale = Mathf.SmoothDamp(
                _backgroundWorldScale,
                _targetBackgroundWorldScale,
                ref _backgroundWorldScaleVelocity,
                backgroundZoomSmoothTime,
                Mathf.Infinity,
                Mathf.Max(0.0001f, deltaTime));
            UpdateProceduralBackground();
        }

        private void UpdateBackgroundAnimationClock()
        {
            if (_backgroundMaterial == null)
            {
                return;
            }

            _backgroundMaterial.SetFloat(
                BackgroundTimeId,
                Time.unscaledTime);
        }

        private Camera GetOrCreateCamera()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                return camera;
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            return cameraObject.AddComponent<Camera>();
        }

        private float CameraAspect()
        {
            if (_camera != null && _camera.aspect > 0.01f)
            {
                return _camera.aspect;
            }

            return Screen.height > 0
                ? Mathf.Max(0.1f, Screen.width / (float)Screen.height)
                : 16f / 9f;
        }

        private void CreateGameUi()
        {
            if (_coinHudText != null)
            {
                return;
            }

            if (EventSystem.current == null)
            {
                var eventSystemObject = new GameObject(
                    "Ring Escape Event System",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                eventSystemObject.transform.SetParent(transform, false);
            }

            var canvasObject = new GameObject(
                "Ring Escape HUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            _hudCanvas = canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            TMP_FontAsset font = uiFont != null
                ? uiFont
                : TMP_Settings.defaultFontAsset;
            _roundedUiSprite = CreateRoundedUiSprite();

            RectTransform coinPanel = CreateUiPanel(
                "Coin Counter",
                canvasObject.transform,
                SanctuaryGlass);
            coinPanel.anchorMin = Vector2.one;
            coinPanel.anchorMax = Vector2.one;
            coinPanel.pivot = Vector2.one;
            coinPanel.anchoredPosition = new Vector2(-28f, -22f);
            coinPanel.sizeDelta = new Vector2(300f, 82f);
            _coinHudText = CreateUiText(
                "Coin Count",
                coinPanel,
                font,
                36,
                TextAlignmentOptions.Center,
                coinGoldColor);
            _coinHudText.text = "SUN COINS  ·  0";

            if (_isGridRoot)
            {
                _buySimulationButton = CreatePowerButton(
                    canvasObject.transform,
                    Vector2.zero,
                    font,
                    out _buySimulationButtonText,
                    SanctuaryCoral,
                    "Buy New Simulation");
                RectTransform buySimulationRect =
                    _buySimulationButton.GetComponent<RectTransform>();
                buySimulationRect.anchorMin = Vector2.one;
                buySimulationRect.anchorMax = Vector2.one;
                buySimulationRect.pivot = Vector2.one;
                buySimulationRect.anchoredPosition = new Vector2(-28f, -116f);
                buySimulationRect.sizeDelta = new Vector2(310f, 82f);
                _buySimulationButtonText.fontSize = 22;
                _buySimulationButtonText.fontSizeMax = 22;
                _buySimulationButtonText.fontSizeMin = 14;
                _buySimulationButton.onClick.AddListener(BuyNewSimulation);

                RectTransform debugPanel = CreateUiPanel(
                    "Simulation Debug Menu",
                    canvasObject.transform,
                    SanctuaryGlass);
                debugPanel.anchorMin = new Vector2(0f, 1f);
                debugPanel.anchorMax = new Vector2(0f, 1f);
                debugPanel.pivot = new Vector2(0f, 1f);
                debugPanel.anchoredPosition = new Vector2(28f, -22f);
                debugPanel.sizeDelta = new Vector2(380f, 82f);

                _simulationCountText = CreateUiText(
                    "Simulation Count",
                    debugPanel,
                    font,
                    25,
                    TextAlignmentOptions.Center,
                    SanctuaryInk);
                _simulationCountText.rectTransform.offsetMin = new Vector2(72f, 8f);
                _simulationCountText.rectTransform.offsetMax = new Vector2(-72f, -8f);

                Button decreaseButton = CreatePowerButton(
                    debugPanel,
                    new Vector2(-155f, 0f),
                    font,
                    out TMP_Text decreaseLabel,
                    new Color(0.24f, 0.65f, 0.76f, 1f),
                    "Decrease Simulations");
                decreaseButton.GetComponent<RectTransform>().sizeDelta = new Vector2(54f, 54f);
                decreaseLabel.text = "-";
                decreaseButton.onClick.AddListener(DecreaseSimulationCount);

                Button increaseButton = CreatePowerButton(
                    debugPanel,
                    new Vector2(155f, 0f),
                    font,
                    out TMP_Text increaseLabel,
                    new Color(0.49f, 0.75f, 0.3f, 1f),
                    "Increase Simulations");
                increaseButton.GetComponent<RectTransform>().sizeDelta = new Vector2(54f, 54f);
                increaseLabel.text = "+";
                increaseButton.onClick.AddListener(IncreaseSimulationCount);
            }

            CreateProgressionHud(canvasObject.transform, font);

            RectTransform powerBar = CreateUiPanel(
                "Power Up Bar",
                canvasObject.transform,
                new Color(0.98f, 0.98f, 0.88f, 0.9f));
            powerBar.anchorMin = new Vector2(0.5f, 0f);
            powerBar.anchorMax = new Vector2(0.5f, 0f);
            powerBar.pivot = new Vector2(0.5f, 0f);
            powerBar.anchoredPosition = new Vector2(0f, 18f);
            powerBar.sizeDelta = new Vector2(1410f, 108f);

            _speedBoostButton = CreatePowerButton(
                powerBar,
                new Vector2(-510f, 0f),
                font,
                out _speedBoostButtonText,
                SpeedBoostButtonColor,
                "Ball Overdrive");
            _speedBoostButtonImage = _speedBoostButton.GetComponent<Image>();
            _speedBoostButtonOutline = _speedBoostButton.GetComponent<Outline>();
            _speedBoostButton.onClick.AddListener(ActivateSpeedBoost);

            _ballMultiplierButton = CreatePowerButton(
                powerBar,
                new Vector2(-170f, 0f),
                font,
                out _ballMultiplierButtonText,
                BallMultiplierButtonColor,
                "Ball Multiplier");
            _ballMultiplierButtonImage =
                _ballMultiplierButton.GetComponent<Image>();
            _ballMultiplierButtonOutline =
                _ballMultiplierButton.GetComponent<Outline>();
            _ballMultiplierButton.onClick.AddListener(
                ActivateBallMultiplier);

            _coinMagnetButton = CreatePowerButton(
                powerBar,
                new Vector2(170f, 0f),
                font,
                out _coinMagnetButtonText,
                CoinMagnetButtonColor,
                "Coin Magnet");
            _coinMagnetButtonImage = _coinMagnetButton.GetComponent<Image>();
            _coinMagnetButtonOutline = _coinMagnetButton.GetComponent<Outline>();
            _coinMagnetButton.onClick.AddListener(ActivateCoinMagnet);

            _coinProductionButton = CreatePowerButton(
                powerBar,
                new Vector2(510f, 0f),
                font,
                out _coinProductionButtonText,
                CoinProductionButtonColor,
                "Gold Rush");
            _coinProductionButtonImage =
                _coinProductionButton.GetComponent<Image>();
            _coinProductionButtonOutline =
                _coinProductionButton.GetComponent<Outline>();
            _coinProductionButton.onClick.AddListener(ActivateCoinProductionBoost);

            CreateUpgradeChoiceUi(canvasObject.transform, font);
            UpdateGameUi();
        }

        private void CreateProgressionHud(
            Transform canvasTransform,
            TMP_FontAsset font)
        {
            var barObject = new GameObject("Experience Progress Bar");
            barObject.transform.SetParent(transform, false);
            _experienceProgressBar = barObject.AddComponent<ProgressBarView>();
            _experienceProgressBar.SetProperties(
                experienceColor,
                new Color(0.04f, 0.2f, 0.3f, 0.82f),
                experienceBarSize,
                0.78f);

            var flashObject = new GameObject(
                "Experience Full Flash",
                typeof(RectTransform),
                typeof(Image));
            flashObject.transform.SetParent(canvasTransform, false);
            RectTransform flashRect =
                flashObject.GetComponent<RectTransform>();
            flashRect.anchorMin = new Vector2(0.5f, 1f);
            flashRect.anchorMax = new Vector2(0.5f, 1f);
            flashRect.pivot = new Vector2(0.5f, 0.5f);
            flashRect.anchoredPosition =
                new Vector2(0f, -experienceBarTopOffset);
            flashRect.sizeDelta = new Vector2(760f, 124f);
            _experienceFanfareFlash = flashObject.GetComponent<Image>();
            _experienceFanfareFlash.sprite = _roundedUiSprite;
            _experienceFanfareFlash.type = Image.Type.Sliced;
            _experienceFanfareFlash.raycastTarget = false;
            _experienceFanfareFlash.color =
                new Color(experienceColor.r, experienceColor.g, experienceColor.b, 0f);

            _experienceProgressText = CreateUiText(
                "Experience Progress Text",
                canvasTransform,
                font,
                experienceBarFontSize,
                TextAlignmentOptions.Center,
                Color.white);
            RectTransform textRect = _experienceProgressText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 1f);
            textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition =
                new Vector2(0f, -experienceBarTopOffset);
            textRect.sizeDelta = new Vector2(700f, 54f);
            _experienceProgressText.fontSizeMin = 18f;
            _experienceProgressText.fontSizeMax =
                experienceBarFontSize;
            var outline = _experienceProgressText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;

            for (int particleIndex = 0;
                 particleIndex < levelUpFanfareParticleCount;
                 particleIndex++)
            {
                var particleObject = new GameObject(
                    $"Level Up Spark {particleIndex + 1:00}",
                    typeof(RectTransform),
                    typeof(Image));
                particleObject.transform.SetParent(canvasTransform, false);
                RectTransform particleRect =
                    particleObject.GetComponent<RectTransform>();
                particleRect.anchorMin = new Vector2(0.5f, 1f);
                particleRect.anchorMax = new Vector2(0.5f, 1f);
                particleRect.pivot = new Vector2(0.5f, 0.5f);
                Image particleImage = particleObject.GetComponent<Image>();
                particleImage.sprite = _roundedUiSprite;
                particleImage.type = Image.Type.Sliced;
                particleImage.raycastTarget = false;
                particleImage.enabled = false;
                _levelUpUiParticles.Add(new LevelUpUiParticle
                {
                    RectTransform = particleRect,
                    Image = particleImage
                });
            }

            UpdateProgressionHud();
        }

        private void CreateUpgradeChoiceUi(
            Transform canvasTransform,
            TMP_FontAsset font)
        {
            _upgradeChoiceOverlay = new GameObject(
                "Level Up Upgrade Choices",
                typeof(RectTransform),
                typeof(Image));
            _upgradeChoiceOverlay.transform.SetParent(canvasTransform, false);
            RectTransform overlayRect =
                _upgradeChoiceOverlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            _upgradeChoiceOverlay.GetComponent<Image>().color =
                new Color(0.02f, 0.25f, 0.45f, 0.68f);

            RectTransform card = CreateUiPanel(
                "Upgrade Choice Card",
                _upgradeChoiceOverlay.transform,
                new Color(0.985f, 0.97f, 0.86f, 0.985f));
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(1120f, 590f);

            _upgradeChoiceTitle = CreateUiText(
                "Level Up Title",
                card,
                font,
                44,
                TextAlignmentOptions.Top,
                SanctuaryBlue);
            RectTransform titleRect = _upgradeChoiceTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -28f);
            titleRect.sizeDelta = new Vector2(820f, 84f);

            TMP_Text subtitle = CreateUiText(
                "Upgrade Subtitle",
                card,
                font,
                23,
                TextAlignmentOptions.Top,
                SanctuaryInk);
            subtitle.text = "CHOOSE A NEW BLESSING";
            RectTransform subtitleRect = subtitle.rectTransform;
            subtitleRect.anchorMin = new Vector2(0.5f, 1f);
            subtitleRect.anchorMax = new Vector2(0.5f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 1f);
            subtitleRect.anchoredPosition = new Vector2(0f, -102f);
            subtitleRect.sizeDelta = new Vector2(620f, 48f);

            for (int choiceIndex = 0; choiceIndex < 3; choiceIndex++)
            {
                int capturedIndex = choiceIndex;
                Button choiceButton = CreatePowerButton(
                    card,
                    new Vector2((choiceIndex - 1) * 350f, -70f),
                    font,
                    out TMP_Text choiceLabel,
                    LockedPowerButtonColor,
                    $"Upgrade Choice {choiceIndex + 1}");
                RectTransform choiceRect =
                    choiceButton.GetComponent<RectTransform>();
                choiceRect.sizeDelta = new Vector2(310f, 330f);
                choiceLabel.fontSize = 27;
                choiceLabel.fontSizeMin = 18;
                choiceLabel.rectTransform.offsetMin = new Vector2(22f, 22f);
                choiceLabel.rectTransform.offsetMax = new Vector2(-22f, -22f);
                var textOutline =
                    choiceLabel.gameObject.AddComponent<Outline>();
                textOutline.effectColor =
                    new Color(1f, 0.98f, 0.82f, 0.32f);
                textOutline.effectDistance =
                    new Vector2(1f, -1f);
                textOutline.useGraphicAlpha = true;
                choiceButton.onClick.AddListener(
                    () => SelectUpgradeChoice(capturedIndex));
                _upgradeChoiceButtons[choiceIndex] = choiceButton;
                _upgradeChoiceLabels[choiceIndex] = choiceLabel;
            }

            _upgradeChoiceOverlay.SetActive(false);
        }

        private void DecreaseSimulationCount()
        {
            SetActiveSimulationCount(activeSimulationCount - 1);
        }

        private void IncreaseSimulationCount()
        {
            SetActiveSimulationCount(activeSimulationCount + 1);
        }

        private int GetNextSimulationPurchaseCost()
        {
            int purchasedSimulationCount = Mathf.Max(0, activeSimulationCount - 1);
            double calculatedCost =
                baseSimulationPurchaseCost *
                Math.Pow(simulationPurchaseCostGrowth, purchasedSimulationCount);
            return (int)Math.Min(
                int.MaxValue,
                Math.Ceiling(calculatedCost));
        }

        private void BuyNewSimulation()
        {
            if (!_isGridRoot ||
                activeSimulationCount >= _gridCells.Count ||
                !MeetsAdditionalSimulationProgressionRequirements())
            {
                return;
            }

            int purchaseCost = GetNextSimulationPurchaseCost();
            if (_coinCount < purchaseCost)
            {
                return;
            }

            _coinCount -= purchaseCost;
            SetActiveSimulationCount(activeSimulationCount + 1);
        }

        private bool MeetsAdditionalSimulationProgressionRequirements()
        {
            return _playerLevel >= GetNextSimulationRequiredLevel() &&
                   string.IsNullOrEmpty(
                       GetMissingSimulationPowerupRequirement());
        }

        private int GetNextSimulationRequiredLevel()
        {
            int additionalSimulationIndex =
                Mathf.Max(0, activeSimulationCount - 1);
            return additionalSimulationUnlockLevel +
                   additionalSimulationIndex *
                   simulationUnlockLevelStep;
        }

        private string GetMissingSimulationPowerupRequirement()
        {
            int targetSimulationCount = activeSimulationCount + 1;
            if (requireOverdriveBeforeAdditionalSimulations &&
                !speedBoostUnlocked)
            {
                return "BALL OVERDRIVE";
            }
            if (targetSimulationCount >= coinMagnetSimulationGate &&
                !coinMagnetUnlocked)
            {
                return "COIN MAGNET";
            }
            if (targetSimulationCount >= coinProductionSimulationGate &&
                !coinProductionUnlocked)
            {
                return "GOLD RUSH";
            }
            if (targetSimulationCount >= ballMultiplierSimulationGate &&
                !ballMultiplierUnlocked)
            {
                return "BALL MULTIPLIER";
            }

            return string.Empty;
        }

        private void UpdateProgressionHud()
        {
            if (_experienceProgressBar != null)
            {
                float progress = _experienceRequired > 0
                    ? _currentExperience / (float)_experienceRequired
                    : 0f;
                _targetExperienceProgress = _levelUpFanfareActive
                    ? 1f
                    : Mathf.Clamp01(progress);
                if (!_experienceProgressInitialized)
                {
                    _displayedExperienceProgress =
                        _targetExperienceProgress;
                    _experienceProgressInitialized = true;
                }
                _experienceProgressBar.SetProgress(
                    _displayedExperienceProgress);
                _experienceProgressBar.SetFillColor(experienceColor);
                UpdateProgressionHudLayout();
            }

            if (_experienceProgressText != null)
            {
                int displayedExperience = _levelUpFanfareActive
                    ? Mathf.Min(_currentExperience, _experienceRequired)
                    : _currentExperience;
                _experienceProgressText.text =
                    _levelUpFanfareActive &&
                    _levelUpFanfareBurstStarted
                    ? "LEVEL UP!"
                    : $"LEVEL {_playerLevel}  |  {displayedExperience:N0} / " +
                      $"{_experienceRequired:N0} XP";
            }
        }

        private void UpdateProgressionHudLayout()
        {
            if (_experienceProgressBar == null || _camera == null)
            {
                return;
            }

            Vector3 screenPosition = new Vector3(
                Screen.width * 0.5f,
                Mathf.Max(
                    experienceBarSize.y * 50f,
                    Screen.height -
                    experienceBarTopOffset *
                    Mathf.Max(
                        0.01f,
                        _hudCanvas != null
                            ? _hudCanvas.scaleFactor
                            : 1f)),
                Mathf.Abs(_camera.transform.position.z) - 0.3f);
            Vector3 worldPosition = _camera.ScreenToWorldPoint(screenPosition);
            worldPosition.z = -0.3f;
            _experienceProgressBar.transform.position = worldPosition;
            float maximumWidth =
                _camera.orthographicSize * CameraAspect() * 0.78f;
            float displayedWidth =
                Mathf.Min(experienceBarSize.x, maximumWidth);
            _experienceProgressBar.SetSize(
                new Vector2(
                    displayedWidth,
                    experienceBarSize.y));
            if (_experienceProgressText != null)
            {
                float canvasScale = Mathf.Max(
                    0.01f,
                    _hudCanvas != null
                        ? _hudCanvas.scaleFactor
                        : 1f);
                float pixelsPerWorldUnit =
                    Screen.height /
                    Mathf.Max(0.01f, _camera.orthographicSize * 2f);
                RectTransform textRect =
                    _experienceProgressText.rectTransform;
                textRect.anchoredPosition =
                    new Vector2(0f, -experienceBarTopOffset);
                textRect.sizeDelta = new Vector2(
                    Mathf.Max(
                        120f,
                        displayedWidth * pixelsPerWorldUnit /
                        canvasScale - 24f),
                    Mathf.Max(
                        28f,
                        experienceBarSize.y * pixelsPerWorldUnit /
                        canvasScale - 4f));
            }
        }

        private void TryStartLevelUp()
        {
            if (_isChoosingUpgrade ||
                _levelUpFanfareActive ||
                _currentExperience < _experienceRequired ||
                _upgradeChoiceOverlay == null)
            {
                return;
            }

            _levelUpFanfareActive = true;
            _levelUpFanfareBurstStarted = false;
            _levelUpFanfareElapsed = 0f;
            _levelUpFullBurstElapsed = 0f;
            _targetExperienceProgress = 1f;
            _experienceFillPulseRemaining =
                Mathf.Max(
                    _experienceFillPulseRemaining,
                    experienceFillPulseDuration);
            SetUpgradePause(true);
            _upgradeChoiceOverlay.SetActive(false);
            UpdateProgressionHud();
        }

        private void CompleteLevelUpFanfare()
        {
            _currentExperience -= _experienceRequired;
            _playerLevel++;
            _experienceRequired = ExperienceRequirementForLevel(_playerLevel);
            PopulateUpgradeChoices();
            _isChoosingUpgrade = true;
            _levelUpFanfareActive = false;
            _levelUpFanfareBurstStarted = false;
            _targetExperienceProgress =
                _experienceRequired > 0
                    ? Mathf.Clamp01(
                        _currentExperience /
                        (float)_experienceRequired)
                    : 0f;
            _displayedExperienceProgress =
                _targetExperienceProgress;
            ResetLevelUpFanfareVisuals();
            _upgradeChoiceOverlay.SetActive(true);
            UpdateProgressionHud();
        }

        private void UpdateProgressionEffects(float deltaTime)
        {
            if (_experienceProgressBar == null)
            {
                return;
            }

            float smoothing = Mathf.Max(0.01f, experienceFillSmoothing);
            float blend = 1f - Mathf.Exp(-smoothing * deltaTime);
            _displayedExperienceProgress = Mathf.Lerp(
                _displayedExperienceProgress,
                _targetExperienceProgress,
                blend);
            if (Mathf.Abs(
                    _displayedExperienceProgress -
                    _targetExperienceProgress) < 0.0005f)
            {
                _displayedExperienceProgress =
                    _targetExperienceProgress;
            }

            _experienceFillPulseRemaining = Mathf.Max(
                0f,
                _experienceFillPulseRemaining - deltaTime);
            float arrivalPulseT = experienceFillPulseDuration > 0.0001f
                ? _experienceFillPulseRemaining /
                  experienceFillPulseDuration
                : 0f;
            float arrivalPulse =
                Mathf.Sin((1f - arrivalPulseT) * Mathf.PI) *
                Mathf.Sqrt(Mathf.Clamp01(arrivalPulseT));
            float barScale =
                1f + arrivalPulse * experienceFillPulseScale;
            float shaderPulse = arrivalPulse * 0.72f;
            float shine = _displayedExperienceProgress > 0.02f
                ? 0.34f
                : 0f;

            if (_levelUpFanfareActive)
            {
                _levelUpFanfareElapsed += deltaTime;
                if (!_levelUpFanfareBurstStarted &&
                    (_displayedExperienceProgress >= 0.995f ||
                     _levelUpFanfareElapsed >= 0.36f))
                {
                    _displayedExperienceProgress = 1f;
                    _levelUpFanfareBurstStarted = true;
                    _levelUpFullBurstElapsed = 0f;
                    BeginLevelUpFanfareVisuals();
                }

                if (_levelUpFanfareBurstStarted)
                {
                    _levelUpFullBurstElapsed += deltaTime;
                    float fanfareT = Mathf.Clamp01(
                        _levelUpFullBurstElapsed /
                        Mathf.Max(0.05f, levelUpFanfareDuration));
                    float fanfarePulse =
                        (1f - Smooth01(fanfareT)) *
                        (0.55f +
                         Mathf.Abs(
                             Mathf.Sin(
                                 _levelUpFullBurstElapsed *
                                 Mathf.PI * 4.5f)) *
                         0.45f);
                    barScale += levelUpFanfareScale * fanfarePulse;
                    shaderPulse = Mathf.Max(
                        shaderPulse,
                        0.72f + fanfarePulse * 0.28f);
                    shine = 1f;
                    UpdateLevelUpFanfareVisuals(
                        _levelUpFullBurstElapsed,
                        fanfareT);

                    if (_levelUpFullBurstElapsed >=
                        levelUpFanfareDuration)
                    {
                        CompleteLevelUpFanfare();
                        return;
                    }
                }
            }

            _experienceProgressBar.SetProgress(
                _displayedExperienceProgress);
            _experienceProgressBar.SetPulse(shaderPulse);
            _experienceProgressBar.SetShine(shine);
            _experienceProgressBar.transform.localScale =
                Vector3.one * barScale;
            if (_experienceProgressText != null)
            {
                _experienceProgressText.rectTransform.localScale =
                    Vector3.one *
                    (1f + (barScale - 1f) * 0.62f);
            }
        }

        private void BeginLevelUpFanfareVisuals()
        {
            if (_experienceProgressText != null)
            {
                _experienceProgressText.text = "LEVEL UP!";
            }
            var random = new System.Random(unchecked(
                ColorSessionSeed ^
                (_playerLevel * 83492791) ^
                (_currentExperience * 297121507)));
            for (int particleIndex = 0;
                 particleIndex < _levelUpUiParticles.Count;
                 particleIndex++)
            {
                LevelUpUiParticle particle =
                    _levelUpUiParticles[particleIndex];
                float startX =
                    Mathf.Lerp(
                        -270f,
                        270f,
                        (float)random.NextDouble());
                particle.StartPosition =
                    new Vector2(
                        startX,
                        Mathf.Lerp(
                            -53f,
                            -63f,
                            (float)random.NextDouble()));
                particle.Velocity = new Vector2(
                    startX * 0.52f +
                    Mathf.Lerp(
                        -135f,
                        135f,
                        (float)random.NextDouble()),
                    Mathf.Lerp(
                        -105f,
                        -255f,
                        (float)random.NextDouble()));
                particle.Delay =
                    Mathf.Lerp(
                        0f,
                        0.16f,
                        (float)random.NextDouble());
                particle.SpinSpeed =
                    Mathf.Lerp(
                        -420f,
                        420f,
                        (float)random.NextDouble());
                particle.Size =
                    Mathf.Lerp(
                        8f,
                        18f,
                        (float)random.NextDouble());
                particle.Color = Color.Lerp(
                    experienceColor,
                    Color.white,
                    Mathf.Lerp(
                        0.12f,
                        0.78f,
                        (float)random.NextDouble()));
                particle.RectTransform.anchoredPosition =
                    particle.StartPosition;
                particle.RectTransform.sizeDelta =
                    new Vector2(
                        particle.Size,
                        particle.Size *
                        (particleIndex % 3 == 0 ? 0.36f : 1f));
                particle.RectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, 45f);
                particle.RectTransform.localScale = Vector3.zero;
                particle.Image.color = particle.Color;
                particle.Image.enabled = true;
            }
        }

        private void UpdateLevelUpFanfareVisuals(
            float elapsed,
            float fanfareT)
        {
            if (_experienceFanfareFlash != null)
            {
                float flashIn = Smooth01(
                    Mathf.Clamp01(elapsed / 0.12f));
                float flashAlpha =
                    flashIn *
                    (1f - Smooth01(fanfareT)) *
                    0.2f;
                Color flashColor = experienceColor;
                flashColor.a = flashAlpha;
                _experienceFanfareFlash.color = flashColor;
                _experienceFanfareFlash.rectTransform.localScale =
                    new Vector3(
                        Mathf.Lerp(0.72f, 1.28f, EaseOutBack(flashIn)),
                        Mathf.Lerp(0.58f, 1.08f, flashIn),
                        1f);
            }

            for (int particleIndex = 0;
                 particleIndex < _levelUpUiParticles.Count;
                 particleIndex++)
            {
                LevelUpUiParticle particle =
                    _levelUpUiParticles[particleIndex];
                float age = elapsed - particle.Delay;
                if (age < 0f)
                {
                    particle.Image.enabled = false;
                    continue;
                }

                particle.Image.enabled = true;
                float lifetime = Mathf.Max(
                    0.2f,
                    levelUpFanfareDuration - particle.Delay);
                float particleT = Mathf.Clamp01(age / lifetime);
                particle.RectTransform.anchoredPosition =
                    particle.StartPosition +
                    particle.Velocity * age +
                    Vector2.down * (72f * age * age);
                particle.RectTransform.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        45f + particle.SpinSpeed * age);
                float pop = EaseOutBack(
                    Mathf.Clamp01(age / 0.12f));
                float fade = 1f - Smooth01(particleT);
                particle.RectTransform.localScale =
                    Vector3.one * (pop * fade);
                Color particleColor = particle.Color;
                particleColor.a = fade;
                particle.Image.color = particleColor;
            }
        }

        private void ResetLevelUpFanfareVisuals()
        {
            if (_experienceProgressBar != null)
            {
                _experienceProgressBar.SetPulse(0f);
                _experienceProgressBar.SetShine(0.34f);
                _experienceProgressBar.transform.localScale =
                    Vector3.one;
            }
            if (_experienceProgressText != null)
            {
                _experienceProgressText.rectTransform.localScale =
                    Vector3.one;
            }
            if (_experienceFanfareFlash != null)
            {
                Color flashColor = experienceColor;
                flashColor.a = 0f;
                _experienceFanfareFlash.color = flashColor;
                _experienceFanfareFlash.rectTransform.localScale =
                    Vector3.one;
            }
            foreach (LevelUpUiParticle particle in _levelUpUiParticles)
            {
                particle.Image.enabled = false;
            }
        }

        private void PopulateUpgradeChoices()
        {
            var candidates = new List<UpgradeType>(
                (UpgradeType[])Enum.GetValues(typeof(UpgradeType)));
            if (!speedBoostUnlocked)
            {
                candidates.Remove(UpgradeType.Overdrive);
            }
            var random = new System.Random(unchecked(
                ColorSessionSeed ^
                (_playerLevel * 73856093) ^
                (_currentExperience * 19349663)));
            for (int index = candidates.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                UpgradeType temporary = candidates[index];
                candidates[index] = candidates[swapIndex];
                candidates[swapIndex] = temporary;
            }

            if (_upgradeChoiceTitle != null)
            {
                _upgradeChoiceTitle.text = $"LEVEL {_playerLevel} REACHED";
            }

            for (int choiceIndex = 0; choiceIndex < 3; choiceIndex++)
            {
                UpgradeType upgrade = candidates[choiceIndex];
                _offeredUpgrades[choiceIndex] = upgrade;
                _upgradeChoiceLabels[choiceIndex].text =
                    GetUpgradeChoiceText(
                        upgrade,
                        _upgradeRanks[(int)upgrade] + 1);
                ApplyUpgradeChoiceColor(choiceIndex, upgrade);
            }
        }

        private void ApplyUpgradeChoiceColor(
            int choiceIndex,
            UpgradeType upgrade)
        {
            if (choiceIndex < 0 ||
                choiceIndex >= _upgradeChoiceButtons.Length ||
                _upgradeChoiceButtons[choiceIndex] == null)
            {
                return;
            }

            Color accentColor = GetUpgradeColor(upgrade);
            Button button = _upgradeChoiceButtons[choiceIndex];
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = accentColor;
            }

            Outline outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                Color outlineColor =
                    Color.Lerp(accentColor, Color.white, 0.46f);
                outlineColor.a = 0.9f;
                outline.effectColor = outlineColor;
                outline.effectDistance = new Vector2(2.5f, -2.5f);
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor =
                new Color(1.16f, 1.16f, 1.16f, 1f);
            colors.pressedColor =
                new Color(0.76f, 0.76f, 0.8f, 1f);
            colors.selectedColor =
                new Color(1.1f, 1.1f, 1.1f, 1f);
            button.colors = colors;

            if (_upgradeChoiceLabels[choiceIndex] != null)
            {
                _upgradeChoiceLabels[choiceIndex].color =
                    Color.Lerp(SanctuaryInk, accentColor, 0.12f);
            }
        }

        private Color GetUpgradeColor(UpgradeType upgrade)
        {
            switch (upgrade)
            {
                case UpgradeType.BallVelocity:
                    return ballVelocityUpgradeColor;
                case UpgradeType.CoinYield:
                    return coinYieldUpgradeColor;
                case UpgradeType.ExperienceGain:
                    return experienceGainUpgradeColor;
                case UpgradeType.PickupRadius:
                    return pickupRadiusUpgradeColor;
                case UpgradeType.Overdrive:
                    return overdriveUpgradeColor;
                case UpgradeType.Elasticity:
                    return elasticityUpgradeColor;
                default:
                    return LockedPowerButtonColor;
            }
        }

        private static string GetUpgradeChoiceText(UpgradeType upgrade, int nextRank)
        {
            switch (upgrade)
            {
                case UpgradeType.BallVelocity:
                    return $"BALL SPEED\n\n" +
                           $"+25%\n\nLEVEL {nextRank}";
                case UpgradeType.CoinYield:
                    return $"COIN REWARDS\n\n" +
                           $"+50%\n\nLEVEL {nextRank}";
                case UpgradeType.ExperienceGain:
                    return $"XP GAIN\n\n" +
                           $"+40%\n\nLEVEL {nextRank}";
                case UpgradeType.PickupRadius:
                    return $"COIN PICKUP RADIUS\n\n" +
                           $"+45%\n\nLEVEL {nextRank}";
                case UpgradeType.Overdrive:
                    return $"BALL OVERDRIVE\n\n" +
                           $"+1.0x SPEED\n+3s DURATION\n\nLEVEL {nextRank}";
                case UpgradeType.Elasticity:
                    return $"BALL BOUNCE\n\n" +
                           $"+8% BOUNCE\n+20% MIN SPEED\n\nLEVEL {nextRank}";
                default:
                    return "UNKNOWN UPGRADE";
            }
        }

        private void SelectUpgradeChoice(int choiceIndex)
        {
            if (!_isChoosingUpgrade ||
                choiceIndex < 0 ||
                choiceIndex >= _offeredUpgrades.Length)
            {
                return;
            }

            UpgradeType upgrade = _offeredUpgrades[choiceIndex];
            ApplyUpgrade(upgrade);
            _upgradeRanks[(int)upgrade]++;
            _isChoosingUpgrade = false;
            _upgradeChoiceOverlay.SetActive(false);
            SetUpgradePause(false);
            UpdateGameUi();
            TryStartLevelUp();
        }

        private void ApplyUpgrade(UpgradeType upgrade)
        {
            switch (upgrade)
            {
                case UpgradeType.BallVelocity:
                    ApplyToAllSimulations(simulation =>
                    {
                        simulation.launchSpeed *= 1.25f;
                        simulation.minimumSpeed *= 1.25f;
                        simulation.maximumSpeed *= 1.25f;
                        simulation._ballVelocity *= 1.25f;
                        simulation._cloneBallVelocity *= 1.25f;
                    });
                    break;
                case UpgradeType.CoinYield:
                    _coinYieldUpgradeMultiplier *= 1.5f;
                    break;
                case UpgradeType.ExperienceGain:
                    _experienceGainMultiplier *= 1.4f;
                    break;
                case UpgradeType.PickupRadius:
                    ApplyToAllSimulations(simulation =>
                        simulation.coinPickupRadius *= 1.45f);
                    break;
                case UpgradeType.Overdrive:
                    speedBoostMultiplier += 1f;
                    speedBoostDuration += 3f;
                    break;
                case UpgradeType.Elasticity:
                    ApplyToAllSimulations(simulation =>
                    {
                        simulation.bounciness =
                            Mathf.Min(1.35f, simulation.bounciness + 0.08f);
                        simulation.minimumSpeed *= 1.2f;
                        simulation._ballVelocity *= 1.12f;
                        simulation._cloneBallVelocity *= 1.12f;
                    });
                    break;
            }
        }

        private void ApplyToAllSimulations(Action<RingEscapeSimulation> action)
        {
            action(this);
            foreach (RingEscapeSimulation cell in _gridCells)
            {
                action(cell);
            }
        }

        private void SetUpgradePause(bool paused)
        {
            _isPaused = paused;
            foreach (RingEscapeSimulation cell in _gridCells)
            {
                cell._isPaused = paused;
            }
        }

        private RectTransform CreateUiPanel(
            string objectName,
            Transform parent,
            Color color)
        {
            var panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.GetComponent<Image>();
            image.color = color;
            image.sprite = _roundedUiSprite;
            image.type = Image.Type.Sliced;
            var outline = panelObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.99f, 0.88f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            var shadow = panelObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.035f, 0.19f, 0.3f, 0.34f);
            shadow.effectDistance = new Vector2(0f, -7f);
            shadow.useGraphicAlpha = true;
            return panelObject.GetComponent<RectTransform>();
        }

        private Sprite CreateRoundedUiSprite()
        {
            const int textureSize = 64;
            const float cornerRadius = 15f;
            var texture = new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "Runtime Rounded UI",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[textureSize * textureSize];
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float edgeX = Mathf.Min(x + 0.5f, textureSize - x - 0.5f);
                    float edgeY = Mathf.Min(y + 0.5f, textureSize - y - 0.5f);
                    float cornerX = Mathf.Max(0f, cornerRadius - edgeX);
                    float cornerY = Mathf.Max(0f, cornerRadius - edgeY);
                    float cornerDistance = Mathf.Sqrt(
                        cornerX * cornerX + cornerY * cornerY);
                    byte alpha = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(cornerRadius + 0.5f - cornerDistance) * 255f);
                    pixels[y * textureSize + x] =
                        new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _runtimeTextures.Add(texture);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(16f, 16f, 16f, 16f));
            sprite.name = "Runtime Rounded UI";
            sprite.hideFlags = HideFlags.DontSave;
            _runtimeSprites.Add(sprite);
            return sprite;
        }

        private static TMP_Text CreateUiText(
            string objectName,
            Transform parent,
            TMP_FontAsset font,
            int fontSize,
            TextAlignmentOptions alignment,
            Color color)
        {
            var textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 8f);
            rect.offsetMax = new Vector2(-12f, -8f);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
            text.alignment = alignment;
            text.color = color;
            text.characterSpacing = 1.2f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16;
            text.fontSizeMax = fontSize;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private Button CreatePowerButton(
            Transform parent,
            Vector2 anchoredPosition,
            TMP_FontAsset font,
            out TMP_Text label,
            Color accentColor,
            string objectName)
        {
            var buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(315f, 74f);

            var image = buttonObject.GetComponent<Image>();
            image.color = accentColor;
            image.sprite = _roundedUiSprite;
            image.type = Image.Type.Sliced;
            var outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(
                SanctuaryInk.r,
                SanctuaryInk.g,
                SanctuaryInk.b,
                0.28f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            var shadow = buttonObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(
                SanctuaryInk.r,
                SanctuaryInk.g,
                SanctuaryInk.b,
                0.24f);
            shadow.effectDistance = new Vector2(0f, -4f);
            shadow.useGraphicAlpha = true;
            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.07f, 0.96f, 1f);
            colors.pressedColor = new Color(0.82f, 0.86f, 0.78f, 1f);
            colors.selectedColor = new Color(1.06f, 1.04f, 0.94f, 1f);
            colors.disabledColor = new Color(0.7f, 0.75f, 0.72f, 0.76f);
            button.colors = colors;

            label = CreateUiText(
                "Label",
                buttonObject.transform,
                font,
                21,
                TextAlignmentOptions.Center,
                SanctuaryInk);
            return button;
        }

        private void UpdatePowerUps(float deltaTime)
        {
            _speedBoostRemaining = Mathf.Max(0f, _speedBoostRemaining - deltaTime);
            _speedBoostCooldownRemaining =
                Mathf.Max(0f, _speedBoostCooldownRemaining - deltaTime);
            _coinMagnetCooldownRemaining =
                Mathf.Max(0f, _coinMagnetCooldownRemaining - deltaTime);
            _coinProductionRemaining =
                Mathf.Max(0f, _coinProductionRemaining - deltaTime);
            _coinProductionCooldownRemaining =
                Mathf.Max(0f, _coinProductionCooldownRemaining - deltaTime);
            bool multiplierWasActive = _ballMultiplierRemaining > 0f;
            _ballMultiplierRemaining =
                Mathf.Max(0f, _ballMultiplierRemaining - deltaTime);
            _ballMultiplierCooldownRemaining =
                Mathf.Max(
                    0f,
                    _ballMultiplierCooldownRemaining - deltaTime);
            if (multiplierWasActive &&
                _ballMultiplierRemaining <= 0f)
            {
                ApplyToAllSimulations(simulation =>
                    simulation.DisableBallClone(false));
            }
        }

        private void ActivateSpeedBoost()
        {
            if (!speedBoostUnlocked)
            {
                if (TryPurchasePowerUpUnlock(
                    ref speedBoostUnlocked,
                    speedBoostUnlockCost,
                    false))
                {
                    _speedBoostRemaining = speedBoostDuration;
                    _speedBoostCooldownRemaining =
                        Mathf.Max(speedBoostDuration, speedBoostCooldown);
                    UpdateGameUi();
                }
                return;
            }

            if (_speedBoostCooldownRemaining > 0f)
            {
                return;
            }

            _speedBoostRemaining = speedBoostDuration;
            _speedBoostCooldownRemaining = Mathf.Max(speedBoostDuration, speedBoostCooldown);
            UpdateGameUi();
        }

        private void ActivateCoinMagnet()
        {
            if (!coinMagnetUnlocked)
            {
                TryPurchasePowerUpUnlock(
                    ref coinMagnetUnlocked,
                    coinMagnetUnlockCost,
                    true,
                    coinMagnetUnlockLevel);
                return;
            }

            if (_coinMagnetCooldownRemaining > 0f)
            {
                return;
            }

            if (_isGridRoot)
            {
                foreach (RingEscapeSimulation cell in _gridCells)
                {
                    cell.CollectAllCoins();
                }
            }
            else
            {
                CollectAllCoins();
            }

            _coinMagnetCooldownRemaining = coinMagnetCooldown;
            UpdateGameUi();
        }

        private void ActivateBallMultiplier()
        {
            if (!ballMultiplierUnlocked)
            {
                TryPurchasePowerUpUnlock(
                    ref ballMultiplierUnlocked,
                    ballMultiplierUnlockCost,
                    true,
                    ballMultiplierUnlockLevel);
                return;
            }

            if (_ballMultiplierCooldownRemaining > 0f)
            {
                return;
            }

            _ballMultiplierRemaining = ballMultiplierDuration;
            _ballMultiplierCooldownRemaining = Mathf.Max(
                ballMultiplierDuration,
                ballMultiplierCooldown);
            ApplyToAllSimulations(simulation =>
                simulation.SpawnBallClone());
            UpdateGameUi();
        }

        private void ActivateCoinProductionBoost()
        {
            if (!coinProductionUnlocked)
            {
                TryPurchasePowerUpUnlock(
                    ref coinProductionUnlocked,
                    coinProductionUnlockCost,
                    true,
                    coinProductionUnlockLevel);
                return;
            }

            if (_coinProductionCooldownRemaining > 0f)
            {
                return;
            }

            _coinProductionRemaining = coinProductionDuration;
            _coinProductionCooldownRemaining =
                Mathf.Max(coinProductionDuration, coinProductionCooldown);
            UpdateGameUi();
        }

        private bool TryPurchasePowerUpUnlock(
            ref bool isUnlocked,
            int unlockCost,
            bool requiresOverdrive = true,
            int requiredLevel = 1)
        {
            if (isUnlocked ||
                _coinCount < unlockCost ||
                _playerLevel < requiredLevel ||
                (requiresOverdrive && !speedBoostUnlocked))
            {
                return false;
            }

            _coinCount -= unlockCost;
            isUnlocked = true;
            UpdateGameUi();
            return true;
        }

        private void CollectAllCoins()
        {
            Vector2 hudLocalPosition = GetHudCoinLocalPosition();
            Vector2 hudWorldPosition = transform.TransformPoint(hudLocalPosition);
            foreach (CollectibleCoin coin in _coins)
            {
                if (coin.State == CoinState.Collecting)
                {
                    continue;
                }

                coin.State = CoinState.Collecting;
                coin.CollectionStartPosition = coin.Position;
                Vector2 coinWorldPosition = transform.TransformPoint(coin.Position);
                float worldDistance =
                    Vector2.Distance(coinWorldPosition, hudWorldPosition);
                coin.Elapsed = -Mathf.Min(
                    1.35f,
                    worldDistance * coinMagnetDelayPerWorldUnit);
                coin.CollectionDuration =
                    coinCollectionDuration +
                    Mathf.Min(
                        0.75f,
                        worldDistance * coinMagnetFlightTimePerWorldUnit);
                coin.CollectionArcHeight =
                    (coin.IsBonus ? 1.35f : 0.85f) +
                    Mathf.Min(0.8f, worldDistance * 0.035f);
            }
        }

        private float CurrentBallSpeedMultiplier()
        {
            RingEscapeSimulation host = _gridOwner != null ? _gridOwner : this;
            return host._speedBoostRemaining > 0f
                ? host.speedBoostMultiplier
                : 1f;
        }

        private bool CurrentBallMultiplierActive()
        {
            RingEscapeSimulation host =
                _gridOwner != null ? _gridOwner : this;
            return host._ballMultiplierRemaining > 0f;
        }

        private void SpawnBallClone()
        {
            if (_ballTransform == null ||
                _finalEscapeActive ||
                _cloneBallActive)
            {
                return;
            }

            float directionSign =
                ((_simulationSeed + _roundSequence) & 1) == 0
                    ? 1f
                    : -1f;
            _cloneBallPosition = _ballPosition;
            _cloneBallVelocity = RotateVector(
                _ballVelocity,
                ballCloneDivergenceDegrees *
                directionSign *
                Mathf.Deg2Rad);
            _cloneBallBouncePattern = CreateBouncePatternState(
                ~(_simulationSeed + _roundSequence));
            _cloneBallActive = true;
            SpawnBallCloneBurst();
            if (_cloneBallTrailRenderer != null)
            {
                _cloneBallTrailRenderer.Clear();
                _cloneBallTrailRenderer.emitting =
                    !_resetRingRevealActive;
            }
        }

        private void SpawnBallCloneBurst()
        {
            for (int particleIndex = 0;
                 particleIndex < 10;
                 particleIndex++)
            {
                ImpactParticle particle =
                    _impactParticlePool.Count > 0
                        ? _impactParticlePool.Pop()
                        : new ImpactParticle();
                float angle = RandomRange(0f, Mathf.PI * 2f);
                float speed = RandomRange(0.28f, 0.82f);
                particle.Position = _ballPosition;
                particle.Velocity =
                    new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle)) *
                    speed;
                particle.Color = Color.Lerp(
                    _ballColor,
                    Color.white,
                    RandomRange(0.18f, 0.72f));
                particle.Elapsed = 0f;
                particle.Lifetime = RandomRange(0.24f, 0.46f);
                particle.Diameter =
                    BallRadius * RandomRange(0.42f, 0.82f);
                _impactParticles.Add(particle);
            }
        }

        private void DisableBallClone(bool clearTrail)
        {
            _cloneBallActive = false;
            if (_cloneBallRenderer != null)
            {
                _cloneBallRenderer.enabled = false;
            }
            if (_cloneBallGlowRenderer != null)
            {
                _cloneBallGlowRenderer.enabled = false;
            }
            if (_cloneBallTrailRenderer != null)
            {
                _cloneBallTrailRenderer.emitting = false;
                if (clearTrail)
                {
                    _cloneBallTrailRenderer.Clear();
                }
            }
        }

        private float CurrentCoinProductionMultiplier()
        {
            RingEscapeSimulation host = _gridOwner != null ? _gridOwner : this;
            return host._coinProductionRemaining > 0f
                ? host.coinProductionMultiplier
                : 1f;
        }

        private float CurrentCoinYieldUpgradeMultiplier()
        {
            RingEscapeSimulation host = _gridOwner != null ? _gridOwner : this;
            return host._coinYieldUpgradeMultiplier;
        }

        private void UpdateGameUi()
        {
            if (_coinHudText == null)
            {
                return;
            }

            _coinHudText.color = coinGoldColor;
            _coinHudText.text = $"SUN COINS  ·  {_coinCount:N0}";
            UpdateProgressionHud();
            if (_simulationCountText != null)
            {
                _simulationCountText.text =
                    $"SKY GARDENS  {activeSimulationCount}/{_gridCells.Count}";
            }
            if (_buySimulationButton != null &&
                _buySimulationButtonText != null)
            {
                bool allSimulationsUnlocked =
                    activeSimulationCount >= _gridCells.Count;
                if (allSimulationsUnlocked)
                {
                    _buySimulationButton.interactable = false;
                    _buySimulationButtonText.text =
                        $"DEMO COMPLETE\n" +
                        $"{_gridCells.Count} SIMULATIONS ONLINE";
                }
                else
                {
                    int purchaseCost = GetNextSimulationPurchaseCost();
                    int requiredLevel =
                        GetNextSimulationRequiredLevel();
                    string missingPowerup =
                        GetMissingSimulationPowerupRequirement();
                    bool powerupRequirementMet =
                        string.IsNullOrEmpty(missingPowerup);
                    bool levelRequirementMet =
                        _playerLevel >= requiredLevel;
                    _buySimulationButton.interactable =
                        powerupRequirementMet &&
                        levelRequirementMet &&
                        _coinCount >= purchaseCost;
                    if (!powerupRequirementMet)
                    {
                        _buySimulationButtonText.text =
                            $"{missingPowerup} REQUIRED\n" +
                            $"{purchaseCost:N0} COINS";
                    }
                    else if (!levelRequirementMet)
                    {
                        _buySimulationButtonText.text =
                            $"UNLOCKS AT LEVEL {requiredLevel}\n" +
                            $"{purchaseCost:N0} COINS";
                    }
                    else
                    {
                        _buySimulationButtonText.text =
                            $"BUY NEW SIMULATION\n{purchaseCost:N0} COINS";
                    }
                }
            }

            UpdateSpeedBoostButton();
            UpdateBallMultiplierButton();
            UpdateCoinMagnetButton();
            UpdateCoinProductionButton();
        }

        private void UpdateSpeedBoostButton()
        {
            SetPowerButtonVisual(
                _speedBoostButtonImage,
                _speedBoostButtonOutline,
                SpeedBoostButtonColor,
                speedBoostUnlocked);
            if (!speedBoostUnlocked)
            {
                _speedBoostButton.interactable =
                    _coinCount >= speedBoostUnlockCost;
                _speedBoostButtonText.text =
                    $"BALL OVERDRIVE\nUNLOCK | {speedBoostUnlockCost:N0} COINS";
                return;
            }

            bool speedActive = _speedBoostRemaining > 0f;
            _speedBoostButton.interactable =
                !speedActive && _speedBoostCooldownRemaining <= 0f;
            _speedBoostButtonText.text = speedActive
                ? $"BALL OVERDRIVE\nACTIVE  {_speedBoostRemaining:0.0}s"
                : _speedBoostCooldownRemaining > 0f
                    ? $"BALL OVERDRIVE\nREADY IN  {_speedBoostCooldownRemaining:0.0}s"
                    : $"BALL OVERDRIVE\n{speedBoostMultiplier:0.##}x SPEED | {speedBoostDuration:0}s";
        }

        private void UpdateCoinMagnetButton()
        {
            SetPowerButtonVisual(
                _coinMagnetButtonImage,
                _coinMagnetButtonOutline,
                CoinMagnetButtonColor,
                coinMagnetUnlocked);
            if (!coinMagnetUnlocked)
            {
                bool levelRequirementMet =
                    _playerLevel >= coinMagnetUnlockLevel;
                _coinMagnetButton.interactable =
                    speedBoostUnlocked &&
                    levelRequirementMet &&
                    _coinCount >= coinMagnetUnlockCost;
                _coinMagnetButtonText.text = !speedBoostUnlocked
                    ? $"COIN MAGNET\nAFTER OVERDRIVE | {coinMagnetUnlockCost:N0} COINS"
                    : !levelRequirementMet
                        ? $"COIN MAGNET\nLEVEL {coinMagnetUnlockLevel} | {coinMagnetUnlockCost:N0} COINS"
                        : $"COIN MAGNET\nUNLOCK | {coinMagnetUnlockCost:N0} COINS";
                return;
            }

            _coinMagnetButton.interactable =
                _coinMagnetCooldownRemaining <= 0f;
            _coinMagnetButtonText.text = _coinMagnetCooldownRemaining > 0f
                ? $"COIN MAGNET\nREADY IN  {_coinMagnetCooldownRemaining:0.0}s"
                : "COIN MAGNET\nCOLLECT EVERYTHING";
        }

        private void UpdateBallMultiplierButton()
        {
            SetPowerButtonVisual(
                _ballMultiplierButtonImage,
                _ballMultiplierButtonOutline,
                BallMultiplierButtonColor,
                ballMultiplierUnlocked);
            if (!ballMultiplierUnlocked)
            {
                bool levelRequirementMet =
                    _playerLevel >= ballMultiplierUnlockLevel;
                _ballMultiplierButton.interactable =
                    speedBoostUnlocked &&
                    levelRequirementMet &&
                    _coinCount >= ballMultiplierUnlockCost;
                _ballMultiplierButtonText.text = !speedBoostUnlocked
                    ? $"BALL MULTIPLIER\nAFTER OVERDRIVE | {ballMultiplierUnlockCost:N0} COINS"
                    : !levelRequirementMet
                        ? $"BALL MULTIPLIER\nLEVEL {ballMultiplierUnlockLevel} | {ballMultiplierUnlockCost:N0} COINS"
                        : $"BALL MULTIPLIER\nUNLOCK | {ballMultiplierUnlockCost:N0} COINS";
                return;
            }

            bool multiplierActive = _ballMultiplierRemaining > 0f;
            _ballMultiplierButton.interactable =
                !multiplierActive &&
                _ballMultiplierCooldownRemaining <= 0f;
            _ballMultiplierButtonText.text = multiplierActive
                ? $"BALL MULTIPLIER\n2x BALLS | {_ballMultiplierRemaining:0.0}s"
                : _ballMultiplierCooldownRemaining > 0f
                    ? $"BALL MULTIPLIER\nREADY IN  {_ballMultiplierCooldownRemaining:0.0}s"
                    : $"BALL MULTIPLIER\n2x BALLS | {ballMultiplierDuration:0}s";
        }

        private void UpdateCoinProductionButton()
        {
            SetPowerButtonVisual(
                _coinProductionButtonImage,
                _coinProductionButtonOutline,
                CoinProductionButtonColor,
                coinProductionUnlocked);
            if (!coinProductionUnlocked)
            {
                bool levelRequirementMet =
                    _playerLevel >= coinProductionUnlockLevel;
                _coinProductionButton.interactable =
                    speedBoostUnlocked &&
                    levelRequirementMet &&
                    _coinCount >= coinProductionUnlockCost;
                _coinProductionButtonText.text = !speedBoostUnlocked
                    ? $"GOLD RUSH\nAFTER OVERDRIVE | {coinProductionUnlockCost:N0} COINS"
                    : !levelRequirementMet
                        ? $"GOLD RUSH\nLEVEL {coinProductionUnlockLevel} | {coinProductionUnlockCost:N0} COINS"
                        : $"GOLD RUSH\nUNLOCK | {coinProductionUnlockCost:N0} COINS";
                return;
            }

            bool productionActive = _coinProductionRemaining > 0f;
            _coinProductionButton.interactable =
                !productionActive && _coinProductionCooldownRemaining <= 0f;
            _coinProductionButtonText.text = productionActive
                ? $"GOLD RUSH\n{coinProductionMultiplier:0.##}x  {_coinProductionRemaining:0.0}s"
                : _coinProductionCooldownRemaining > 0f
                    ? $"GOLD RUSH\nREADY IN  {_coinProductionCooldownRemaining:0.0}s"
                    : $"GOLD RUSH\n{coinProductionMultiplier:0.##}x COINS | {coinProductionDuration:0}s";
        }

        private static void SetPowerButtonVisual(
            Image image,
            Outline outline,
            Color unlockedColor,
            bool isUnlocked)
        {
            if (image != null)
            {
                image.color = isUnlocked
                    ? unlockedColor
                    : LockedPowerButtonColor;
            }
            if (outline != null)
            {
                outline.effectColor = isUnlocked
                    ? new Color(
                        SanctuaryInk.r,
                        SanctuaryInk.g,
                        SanctuaryInk.b,
                        0.3f)
                    : LockedPowerOutlineColor;
            }
        }

        private void CreateMaterial()
        {
            Shader shader = Shader.Find("BallBounce/Unlit Vertex Color");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            _unlitMaterial = new Material(shader)
            {
                name = "Ring Escape Runtime Material",
                hideFlags = HideFlags.DontSave
            };
            _runtimeMaterials.Add(_unlitMaterial);
        }

        private void BuildRings()
        {
            var ringObject = new GameObject("Batched Rotating Rings");
            ringObject.transform.SetParent(transform, false);
            int shapeSideCount = ShapeSideCount();
            float ringSpacing =
                (MaximumRingApothem() - InnerRadius) / (ringCount - 1);
            int verticesPerRing = (_ringMeshSegmentCount + 1) * 2;
            int trianglesPerRing = _ringMeshSegmentCount * 6;
            _combinedRingBaseVertices = new Vector3[ringCount * verticesPerRing];
            _combinedRingVertices = new Vector3[_combinedRingBaseVertices.Length];
            _combinedRingColors = new Color[_combinedRingBaseVertices.Length];
            var triangles = new int[ringCount * trianglesPerRing];

            for (int i = 0; i < ringCount; i++)
            {
                float radius = InnerRadius + i * ringSpacing;
                float normalizedRadius = i / (float)(ringCount - 1);
                float circleSpeedMultiplier = Mathf.Lerp(
                    innerToOuterSpeedRatio,
                    1f,
                    normalizedRadius);
                float angleOffset = shapeSideCount >= 3
                    ? 0f
                    : i * RingTwistDegrees +
                      rotationSpeedDegrees *
                      circleSpeedMultiplier *
                      circleSpawnRotationLeadTime;
                float gapOffset = shapeSideCount >= 3
                    ? Mathf.FloorToInt(
                          i / (float)Mathf.Max(1, polygonGapOffsetEveryRings)) *
                      polygonGapOffsetStepDegrees
                    : 0f;
                int vertexStart = i * verticesPerRing;
                int triangleStart = i * trianglesPerRing;
                WriteRingBaseGeometry(
                    radius,
                    ringThickness,
                    GapDegrees,
                    gapOffset,
                    shapeSideCount,
                    _ringMeshSegmentCount,
                    vertexStart,
                    triangleStart,
                    _combinedRingBaseVertices,
                    triangles);
                Array.Copy(
                    _combinedRingBaseVertices,
                    vertexStart,
                    _combinedRingVertices,
                    vertexStart,
                    verticesPerRing);

                _rings.Add(new Ring
                {
                    Radius = radius,
                    AngleOffset = angleOffset,
                    GapOffsetDegrees = gapOffset,
                    NormalizedRadius = normalizedRadius,
                    VertexStart = vertexStart,
                    IsAlive = true
                });
            }

            _combinedRingMesh = new Mesh { name = "Batched Rotating Rings" };
            _combinedRingMesh.MarkDynamic();
            _combinedRingMesh.vertices = _combinedRingVertices;
            _combinedRingMesh.colors = _combinedRingColors;
            _combinedRingMesh.triangles = triangles;
            _combinedRingMesh.bounds = new Bounds(
                Vector3.zero,
                Vector3.one * ((OuterRadius + ringThickness) * 2.1f));
            _runtimeMeshes.Add(_combinedRingMesh);

            var shadowObject = new GameObject("Painted Ring Shadow");
            shadowObject.transform.SetParent(transform, false);
            shadowObject.transform.localPosition = new Vector3(0.035f, -0.045f, 0.03f);
            var shadowFilter = shadowObject.AddComponent<MeshFilter>();
            shadowFilter.sharedMesh = _combinedRingMesh;
            _combinedRingShadowRenderer = shadowObject.AddComponent<MeshRenderer>();
            _combinedRingShadowRenderer.sharedMaterial = _unlitMaterial;
            _combinedRingShadowRenderer.sortingOrder = -1;

            var filter = ringObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _combinedRingMesh;
            _combinedRingRenderer = ringObject.AddComponent<MeshRenderer>();
            _combinedRingRenderer.sharedMaterial = _unlitMaterial;
            _combinedRingRenderer.sortingOrder = 0;
            SetRendererTint(_combinedRingRenderer, Color.white);

            var highlightObject = new GameObject("Painted Ring Sun Edge");
            highlightObject.transform.SetParent(transform, false);
            highlightObject.transform.localPosition = new Vector3(-0.012f, 0.018f, -0.02f);
            var highlightFilter = highlightObject.AddComponent<MeshFilter>();
            highlightFilter.sharedMesh = _combinedRingMesh;
            _combinedRingHighlightRenderer = highlightObject.AddComponent<MeshRenderer>();
            _combinedRingHighlightRenderer.sharedMaterial = _unlitMaterial;
            _combinedRingHighlightRenderer.sortingOrder = 1;

            ApplyPalette();
            UpdateCombinedRingMesh();
        }

        private void CreateBall()
        {
            _circleMesh = CreateCircleMesh(40);
            _circleMesh.name = "Shared Circle";
            _runtimeMeshes.Add(_circleMesh);
            _coinMesh = CreateLayeredCoinMesh(coinGoldColor);
            _runtimeMeshes.Add(_coinMesh);
            CreateCoinPoolAndBatchRenderer();
            CreateExperiencePoolAndBatchRenderer();

            var glowObject = new GameObject("Ball Glow");
            glowObject.transform.SetParent(transform, false);
            var glowFilter = glowObject.AddComponent<MeshFilter>();
            glowFilter.sharedMesh = _circleMesh;
            _ballGlowRenderer = glowObject.AddComponent<MeshRenderer>();
            _ballGlowRenderer.sharedMaterial = _unlitMaterial;
            _ballGlowRenderer.sortingOrder = 20;
            _ballGlowTransform = glowObject.transform;
            _ballGlowTransform.localScale = Vector3.one * (BallRadius * 3.2f);

            var ballObject = new GameObject("Ball");
            ballObject.transform.SetParent(transform, false);
            var ballFilter = ballObject.AddComponent<MeshFilter>();
            ballFilter.sharedMesh = _circleMesh;
            _ballRenderer = ballObject.AddComponent<MeshRenderer>();
            _ballRenderer.sharedMaterial = _unlitMaterial;
            _ballRenderer.sortingOrder = 21;
            _ballTransform = ballObject.transform;
            _ballTransform.localScale = Vector3.one * (BallRadius * 2f);
            CreatePaintedBallDetails(
                ballObject.transform,
                false,
                out _ballRimRenderer,
                out _ballHighlightRenderer);

            var trailObject = new GameObject("Ball Trail");
            trailObject.transform.SetParent(transform, false);
            _ballTrailTransform = trailObject.transform;
            _ballTrailRenderer = trailObject.AddComponent<TrailRenderer>();
            _ballTrailRenderer.sharedMaterial = _unlitMaterial;
            _ballTrailRenderer.sortingOrder = 19;
            _ballTrailRenderer.time = ballTrailDuration;
            _ballTrailRenderer.widthMultiplier = BallRadius * 2f * ballTrailWidth;
            _ballTrailRenderer.minVertexDistance = BallRadius * 0.45f;
            _ballTrailRenderer.numCornerVertices = 2;
            _ballTrailRenderer.numCapVertices = 2;
            _ballTrailRenderer.alignment = LineAlignment.View;
            _ballTrailRenderer.textureMode = LineTextureMode.Stretch;
            _ballTrailRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _ballTrailRenderer.receiveShadows = false;
            _ballTrailRenderer.emitting = false;
            SetRendererTint(_ballTrailRenderer, Color.white);

            var cloneGlowObject = new GameObject("Clone Ball Glow");
            cloneGlowObject.transform.SetParent(transform, false);
            var cloneGlowFilter =
                cloneGlowObject.AddComponent<MeshFilter>();
            cloneGlowFilter.sharedMesh = _circleMesh;
            _cloneBallGlowRenderer =
                cloneGlowObject.AddComponent<MeshRenderer>();
            _cloneBallGlowRenderer.sharedMaterial = _unlitMaterial;
            _cloneBallGlowRenderer.sortingOrder = 20;
            _cloneBallGlowRenderer.enabled = false;
            _cloneBallGlowTransform = cloneGlowObject.transform;

            var cloneBallObject = new GameObject("Clone Ball");
            cloneBallObject.transform.SetParent(transform, false);
            var cloneBallFilter =
                cloneBallObject.AddComponent<MeshFilter>();
            cloneBallFilter.sharedMesh = _circleMesh;
            _cloneBallRenderer =
                cloneBallObject.AddComponent<MeshRenderer>();
            _cloneBallRenderer.sharedMaterial = _unlitMaterial;
            _cloneBallRenderer.sortingOrder = 21;
            _cloneBallRenderer.enabled = false;
            _cloneBallTransform = cloneBallObject.transform;
            CreatePaintedBallDetails(
                cloneBallObject.transform,
                true,
                out _cloneBallRimRenderer,
                out _cloneBallHighlightRenderer);

            var cloneTrailObject = new GameObject("Clone Ball Trail");
            cloneTrailObject.transform.SetParent(transform, false);
            _cloneBallTrailTransform = cloneTrailObject.transform;
            _cloneBallTrailRenderer =
                cloneTrailObject.AddComponent<TrailRenderer>();
            _cloneBallTrailRenderer.sharedMaterial = _unlitMaterial;
            _cloneBallTrailRenderer.sortingOrder = 19;
            _cloneBallTrailRenderer.time = ballTrailDuration;
            _cloneBallTrailRenderer.widthMultiplier =
                BallRadius * 2f * ballTrailWidth;
            _cloneBallTrailRenderer.minVertexDistance =
                BallRadius * 0.45f;
            _cloneBallTrailRenderer.numCornerVertices = 2;
            _cloneBallTrailRenderer.numCapVertices = 2;
            _cloneBallTrailRenderer.alignment = LineAlignment.View;
            _cloneBallTrailRenderer.textureMode =
                LineTextureMode.Stretch;
            _cloneBallTrailRenderer.shadowCastingMode =
                ShadowCastingMode.Off;
            _cloneBallTrailRenderer.receiveShadows = false;
            _cloneBallTrailRenderer.emitting = false;
            SetRendererTint(_cloneBallTrailRenderer, Color.white);
        }

        private void CreatePaintedBallDetails(
            Transform ballParent,
            bool initiallyHidden,
            out MeshRenderer rimRenderer,
            out MeshRenderer highlightRenderer)
        {
            var rimObject = new GameObject("Painted Orb Rim");
            rimObject.transform.SetParent(ballParent, false);
            rimObject.transform.localPosition = new Vector3(0.05f, -0.06f, 0.02f);
            rimObject.transform.localScale = Vector3.one * 1.18f;
            var rimFilter = rimObject.AddComponent<MeshFilter>();
            rimFilter.sharedMesh = _circleMesh;
            rimRenderer = rimObject.AddComponent<MeshRenderer>();
            rimRenderer.sharedMaterial = _unlitMaterial;
            rimRenderer.sortingOrder = 20;
            rimRenderer.enabled = !initiallyHidden;

            var highlightObject = new GameObject("Painted Orb Highlight");
            highlightObject.transform.SetParent(ballParent, false);
            highlightObject.transform.localPosition = new Vector3(-0.19f, 0.2f, -0.02f);
            highlightObject.transform.localScale = new Vector3(0.3f, 0.2f, 1f);
            var highlightFilter = highlightObject.AddComponent<MeshFilter>();
            highlightFilter.sharedMesh = _circleMesh;
            highlightRenderer = highlightObject.AddComponent<MeshRenderer>();
            highlightRenderer.sharedMaterial = _unlitMaterial;
            highlightRenderer.sortingOrder = 22;
            highlightRenderer.enabled = !initiallyHidden;
        }

        private void CreateCoinPoolAndBatchRenderer()
        {
            _coinBaseVertices = _coinMesh.vertices;
            _coinBaseColors = _coinMesh.colors;
            _coinBaseTriangles = _coinMesh.triangles;

            int verticesPerCoin = _coinBaseVertices.Length;
            int trianglesPerCoin = _coinBaseTriangles.Length;
            _coinBatchVertices = new Vector3[coinPoolCapacity * verticesPerCoin];
            _coinBatchColors = new Color[_coinBatchVertices.Length];
            _coinBatchTriangles = new int[coinPoolCapacity * trianglesPerCoin];

            for (int coinIndex = 0; coinIndex < coinPoolCapacity; coinIndex++)
            {
                int vertexStart = coinIndex * verticesPerCoin;
                int triangleStart = coinIndex * trianglesPerCoin;
                Array.Copy(
                    _coinBaseColors,
                    0,
                    _coinBatchColors,
                    vertexStart,
                    verticesPerCoin);
                for (int triangleIndex = 0; triangleIndex < trianglesPerCoin; triangleIndex++)
                {
                    _coinBatchTriangles[triangleStart + triangleIndex] =
                        vertexStart + _coinBaseTriangles[triangleIndex];
                }

                _coinPool.Push(new CollectibleCoin());
            }

            _coinBatchMesh = new Mesh { name = "Pooled Coin Batch" };
            InitializeDynamicBatchMesh(
                _coinBatchMesh,
                _coinBatchVertices,
                _coinBatchColors,
                _coinBatchTriangles,
                new Bounds(Vector3.zero, Vector3.one * 256f));
            _runtimeMeshes.Add(_coinBatchMesh);

            var batchObject = new GameObject("Pooled Coin Batch");
            batchObject.transform.SetParent(transform, false);
            var filter = batchObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _coinBatchMesh;
            _coinBatchRenderer = batchObject.AddComponent<MeshRenderer>();
            _coinBatchRenderer.sharedMaterial = _unlitMaterial;
            _coinBatchRenderer.sortingOrder = 30;
            _coinBatchRenderer.enabled = false;
            SetRendererTint(_coinBatchRenderer, Color.white);
        }

        private void CreateExperiencePoolAndBatchRenderer()
        {
            int verticesPerParticle = DotMeshSegments + 1;
            int indicesPerParticle = DotMeshSegments * 3;
            _experienceBatchVertices =
                new Vector3[experienceParticlePoolCapacity * verticesPerParticle];
            _experienceBatchColors = new Color[_experienceBatchVertices.Length];
            _experienceBatchTriangles =
                new int[experienceParticlePoolCapacity * indicesPerParticle];

            for (int particleIndex = 0;
                 particleIndex < experienceParticlePoolCapacity;
                 particleIndex++)
            {
                int vertexStart = particleIndex * verticesPerParticle;
                int triangleStart = particleIndex * indicesPerParticle;
                for (int vertexIndex = 0; vertexIndex < verticesPerParticle; vertexIndex++)
                {
                    _experienceBatchColors[vertexStart + vertexIndex] = experienceColor;
                }

                for (int segment = 0; segment < DotMeshSegments; segment++)
                {
                    int triangle = triangleStart + segment * 3;
                    _experienceBatchTriangles[triangle] = vertexStart;
                    _experienceBatchTriangles[triangle + 1] = vertexStart + segment + 1;
                    _experienceBatchTriangles[triangle + 2] =
                        vertexStart + ((segment + 1) % DotMeshSegments) + 1;
                }

                _experienceParticlePool.Push(new ExperienceParticle());
            }

            _experienceBatchMesh = new Mesh { name = "Pooled Experience Batch" };
            InitializeDynamicBatchMesh(
                _experienceBatchMesh,
                _experienceBatchVertices,
                _experienceBatchColors,
                _experienceBatchTriangles,
                new Bounds(Vector3.zero, Vector3.one * 256f));
            _runtimeMeshes.Add(_experienceBatchMesh);

            var batchObject = new GameObject("Pooled Experience Batch");
            batchObject.transform.SetParent(transform, false);
            var filter = batchObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _experienceBatchMesh;
            _experienceBatchRenderer = batchObject.AddComponent<MeshRenderer>();
            _experienceBatchRenderer.sharedMaterial = _unlitMaterial;
            _experienceBatchRenderer.sortingOrder = 35;
            _experienceBatchRenderer.enabled = false;
            SetRendererTint(_experienceBatchRenderer, Color.white);
        }

        private void CreateFragmentBatchRenderer()
        {
            int verticesPerDot = DotMeshSegments + 1;
            int trianglesPerDot = DotMeshSegments * 3;
            _fragmentBatchVertices = new Vector3[fragmentBatchCapacity * verticesPerDot];
            _fragmentBatchColors = new Color[_fragmentBatchVertices.Length];
            _fragmentBatchTriangles = new int[fragmentBatchCapacity * trianglesPerDot];

            for (int dotIndex = 0; dotIndex < fragmentBatchCapacity; dotIndex++)
            {
                int vertexStart = dotIndex * verticesPerDot;
                int triangleStart = dotIndex * trianglesPerDot;
                for (int vertexIndex = 0; vertexIndex < verticesPerDot; vertexIndex++)
                {
                    _fragmentBatchColors[vertexStart + vertexIndex] = Color.white;
                }
                for (int segment = 0; segment < DotMeshSegments; segment++)
                {
                    int triangle = triangleStart + segment * 3;
                    _fragmentBatchTriangles[triangle] = vertexStart;
                    _fragmentBatchTriangles[triangle + 1] = vertexStart + segment + 1;
                    _fragmentBatchTriangles[triangle + 2] =
                        vertexStart + ((segment + 1) % DotMeshSegments) + 1;
                }
            }

            _fragmentBatchMesh = new Mesh { name = "Batched Ring Fragments" };
            InitializeDynamicBatchMesh(
                _fragmentBatchMesh,
                _fragmentBatchVertices,
                _fragmentBatchColors,
                _fragmentBatchTriangles,
                new Bounds(Vector3.zero, Vector3.one * (OuterRadius * 3f)));
            _runtimeMeshes.Add(_fragmentBatchMesh);

            var batchObject = new GameObject("Batched Ring Fragments");
            batchObject.transform.SetParent(transform, false);
            var filter = batchObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _fragmentBatchMesh;
            _fragmentBatchRenderer = batchObject.AddComponent<MeshRenderer>();
            _fragmentBatchRenderer.sharedMaterial = _unlitMaterial;
            _fragmentBatchRenderer.sortingOrder = 10;
            _fragmentBatchRenderer.enabled = false;
            SetRendererTint(_fragmentBatchRenderer, Color.white);
        }

        private static void InitializeDynamicBatchMesh(
            Mesh mesh,
            Vector3[] positions,
            Color[] colors,
            int[] triangles,
            Bounds bounds)
        {
            const MeshUpdateFlags updateFlags =
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices;

            mesh.MarkDynamic();
            mesh.SetVertexBufferParams(
                positions.Length,
                new VertexAttributeDescriptor(
                    VertexAttribute.Position,
                    VertexAttributeFormat.Float32,
                    3,
                    0),
                new VertexAttributeDescriptor(
                    VertexAttribute.Color,
                    VertexAttributeFormat.Float32,
                    4,
                    1));
            mesh.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
            mesh.SetVertexBufferData(
                positions,
                0,
                0,
                positions.Length,
                0,
                updateFlags);
            mesh.SetVertexBufferData(
                colors,
                0,
                0,
                colors.Length,
                1,
                updateFlags);
            mesh.SetIndexBufferData(
                triangles,
                0,
                0,
                triangles.Length,
                updateFlags);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(
                0,
                new SubMeshDescriptor(0, 0, MeshTopology.Triangles),
                updateFlags);
            mesh.bounds = bounds;
        }

        private void CreatePickupRadiusIndicator()
        {
            var indicatorObject = new GameObject("Coin Pickup Radius");
            indicatorObject.transform.SetParent(transform, false);
            var filter = indicatorObject.AddComponent<MeshFilter>();
            Mesh indicatorMesh = CreateArcMesh(1f, 0.055f, 0f, 96);
            indicatorMesh.name = "Coin Pickup Radius";
            filter.sharedMesh = indicatorMesh;
            _runtimeMeshes.Add(indicatorMesh);

            _pickupRadiusRenderer = indicatorObject.AddComponent<MeshRenderer>();
            _pickupRadiusRenderer.sharedMaterial = _unlitMaterial;
            _pickupRadiusRenderer.sortingOrder = 40;
            _pickupRadiusTransform = indicatorObject.transform;
            _pickupRadiusTransform.localScale = Vector3.one * coinPickupRadius;
            SetRendererTint(
                _pickupRadiusRenderer,
                _pickupRadiusColor);
        }

        private void ResetSimulation()
        {
            int roundSeed = unchecked(_simulationSeed + _roundSequence * 104729);
            _roundSequence++;
            _random = new System.Random(roundSeed);

            _ballPosition = new Vector2(0.025f, 0.015f);
            float launchAngle = launchAngleDegrees * Mathf.Deg2Rad;
            _ballVelocity = new Vector2(Mathf.Cos(launchAngle), Mathf.Sin(launchAngle)) * launchSpeed;
            _ballBouncePattern = CreateBouncePatternState(roundSeed);
            _cloneBallBouncePattern = CreateBouncePatternState(~roundSeed);
            _accumulator = 0f;
            _emptyTimer = 0f;
            _rotationDirection = 1f;
            _rotationReverseCooldown = 0f;
            _innermostTintUpdateCooldown = 0f;
            _isPaused = false;
            _resetRingRevealElapsed = 0f;
            _resetRingRevealActive = resetRingRevealDuration > 0.0001f;
            _ballImpactSquashRemaining = 0f;
            _finalEscapeActive = false;
            _finalBallExploded = false;
            _finalEscapeElapsed = 0f;
            _finalBallBurstCoinCount = 0;
            if (_ballTrailRenderer != null)
            {
                _ballTrailRenderer.Clear();
                _ballTrailRenderer.emitting = !_resetRingRevealActive;
            }
            DisableBallClone(true);
            if (CurrentBallMultiplierActive())
            {
                SpawnBallClone();
            }

            foreach (Ring ring in _rings)
            {
                ring.IsAlive = true;
                ring.RotationDegrees = _initialRingRotationDegrees + ring.AngleOffset;
            }

            _tintedInnermostRing = FindInnermostRing();
            ApplyPalette();
            UpdateVisuals();
        }

        private void Simulate(float deltaTime)
        {
            if (_finalEscapeActive)
            {
                UpdateFinalEscapeSequence(deltaTime);
                return;
            }

            _rotationReverseCooldown = Mathf.Max(0f, _rotationReverseCooldown - deltaTime);
            UpdateInnermostTintPromotion(deltaTime);
            foreach (Ring ring in _rings)
            {
                if (!ring.IsAlive)
                {
                    continue;
                }

                float speedMultiplier = ShapeSideCount() >= 3
                    ? 1f
                    : Mathf.Lerp(
                        innerToOuterSpeedRatio,
                        1f,
                        ring.NormalizedRadius);
                ring.RotationDegrees = Mathf.Repeat(
                    ring.RotationDegrees + rotationSpeedDegrees * speedMultiplier * _rotationDirection * deltaTime,
                    360f);
            }

            SimulateCurrentBall(deltaTime, ref _ballBouncePattern);
            if (_finalEscapeActive || !_cloneBallActive)
            {
                return;
            }

            Vector2 originalBallPosition = _ballPosition;
            Vector2 originalBallVelocity = _ballVelocity;
            _ballPosition = _cloneBallPosition;
            _ballVelocity = _cloneBallVelocity;
            SimulateCurrentBall(deltaTime, ref _cloneBallBouncePattern);
            if (_finalEscapeActive)
            {
                // The clone completed the board. It becomes the ball used by
                // the final celebration, so retain its current position.
                return;
            }

            _cloneBallPosition = _ballPosition;
            _cloneBallVelocity = _ballVelocity;
            _ballPosition = originalBallPosition;
            _ballVelocity = originalBallVelocity;
        }

        private void SimulateCurrentBall(
            float deltaTime,
            ref BouncePatternState bouncePattern)
        {
            bouncePattern.TimeSinceBounce += deltaTime;
            _ballVelocity += Vector2.down * (gravity * deltaTime);
            _ballPosition += _ballVelocity * (deltaTime * CurrentBallSpeedMultiplier());

            Ring innermost = FindInnermostRing();
            if (innermost == null)
            {
                _emptyTimer += deltaTime;
                if (automaticallyRestart && _emptyTimer >= 1.4f)
                {
                    ResetSimulation();
                }
                return;
            }

            // While the ball is passing through the active gap, its diameter is
            // large enough to clip the next tightly packed ring. Those outer
            // contacts are the characteristic multi-ring dotted bursts in the clips.
            float collisionReach = BallRadius + ringThickness * 0.5f;
            foreach (Ring ring in _rings)
            {
                if (!ring.IsAlive || ring == innermost)
                {
                    continue;
                }

                if (!IsNearShapeBoundary(ring, _ballPosition, collisionReach))
                {
                    continue;
                }

                ShapeBoundaryInfo boundary =
                    GetShapeBoundaryInfo(ring, _ballPosition);
                if (boundary.Distance <= collisionReach &&
                    IsTouchingSolidArc(ring, _ballPosition))
                {
                    Shatter(ring);
                }
            }

            ShapeBoundaryInfo innermostBoundary =
                GetShapeBoundaryInfo(innermost, _ballPosition);
            if (!innermostBoundary.IsInside &&
                innermostBoundary.Distance > collisionReach)
            {
                // The ball fully cleared the gap; the old boundary is now behind it.
                Shatter(innermost);
                return;
            }

            if (innermostBoundary.Distance <= collisionReach &&
                TryGetRingContact(
                    innermost,
                    _ballPosition,
                    innermostBoundary,
                    out RingContactInfo contact))
            {
                BounceOff(contact, ref bouncePattern);
            }
        }

        private bool IsNearShapeBoundary(
            Ring ring,
            Vector2 position,
            float reach)
        {
            float radialDistance = position.magnitude;
            if (radialDistance < 0.0001f)
            {
                return false;
            }

            int sideCount = ShapeSideCount();
            float localAngle =
                Mathf.Atan2(position.y, position.x) -
                ring.RotationDegrees * Mathf.Deg2Rad;
            float boundaryRadius = ShapeRadiusAtLocalAngle(
                ring.Radius,
                localAngle,
                sideCount);
            float maximumRadialReach = sideCount >= 3
                ? reach / Mathf.Max(0.1f, Mathf.Cos(Mathf.PI / sideCount))
                : reach;
            return Mathf.Abs(radialDistance - boundaryRadius) <=
                   maximumRadialReach;
        }

        private Ring FindInnermostRing()
        {
            foreach (Ring ring in _rings)
            {
                if (ring.IsAlive)
                {
                    return ring;
                }
            }

            return null;
        }

        private ShapeBoundaryInfo GetShapeBoundaryInfo(
            Ring ring,
            Vector2 position)
        {
            int sideCount = ShapeSideCount();
            if (sideCount < 3)
            {
                float magnitude = position.magnitude;
                Vector2 normal = magnitude > 0.0001f
                    ? position / magnitude
                    : Vector2.up;
                return new ShapeBoundaryInfo
                {
                    Distance = Mathf.Abs(magnitude - ring.Radius),
                    IsInside = magnitude <= ring.Radius,
                    OutwardNormal = normal,
                    ClosestPoint = normal * ring.Radius
                };
            }

            float rotationRadians = ring.RotationDegrees * Mathf.Deg2Rad;
            Vector2 localPosition = RotateVector(position, -rotationRadians);
            float sector = Mathf.PI * 2f / sideCount;
            float halfSideLength =
                ring.Radius * Mathf.Tan(Mathf.PI / sideCount);
            float closestDistanceSquared = float.MaxValue;
            Vector2 closestLocalPoint = Vector2.zero;
            Vector2 closestSideNormal = Vector2.up;
            bool isInside = true;

            for (int sideIndex = 0; sideIndex < sideCount; sideIndex++)
            {
                float sideAngle = sideIndex * sector;
                Vector2 sideNormal =
                    new Vector2(Mathf.Cos(sideAngle), Mathf.Sin(sideAngle));
                Vector2 sideTangent =
                    new Vector2(-sideNormal.y, sideNormal.x);
                float normalProjection =
                    Vector2.Dot(localPosition, sideNormal);
                if (normalProjection > ring.Radius)
                {
                    isInside = false;
                }

                float tangentProjection = Mathf.Clamp(
                    Vector2.Dot(localPosition, sideTangent),
                    -halfSideLength,
                    halfSideLength);
                Vector2 candidate =
                    sideNormal * ring.Radius +
                    sideTangent * tangentProjection;
                float distanceSquared =
                    (localPosition - candidate).sqrMagnitude;
                if (distanceSquared < closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    closestLocalPoint = candidate;
                    closestSideNormal = sideNormal;
                }
            }

            Vector2 localNormal = closestSideNormal;
            if (!isInside)
            {
                Vector2 separation = localPosition - closestLocalPoint;
                if (separation.sqrMagnitude > 0.000001f)
                {
                    localNormal = separation.normalized;
                }
            }

            return new ShapeBoundaryInfo
            {
                Distance = Mathf.Sqrt(closestDistanceSquared),
                IsInside = isInside,
                OutwardNormal = RotateVector(localNormal, rotationRadians),
                ClosestPoint = RotateVector(closestLocalPoint, rotationRadians)
            };
        }

        private static Vector2 RotateVector(Vector2 vector, float angleRadians)
        {
            float cosine = Mathf.Cos(angleRadians);
            float sine = Mathf.Sin(angleRadians);
            return new Vector2(
                vector.x * cosine - vector.y * sine,
                vector.x * sine + vector.y * cosine);
        }

        private bool IsTouchingSolidArc(Ring ring, Vector2 position)
        {
            float distance = position.magnitude;
            if (distance < 0.0001f)
            {
                return false;
            }

            float ballAngle = Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg;
            float gapCenter =
                ring.RotationDegrees + ring.GapOffsetDegrees;
            float distanceFromGapCenter = Mathf.Abs(Mathf.DeltaAngle(ballAngle, gapCenter));
            float halfGap = GapDegrees * 0.5f;

            // When the ball's center is over the rendered arc, the radial broad
            // phase performed by the caller is sufficient to prove an overlap.
            if (distanceFromGapCenter >= halfGap)
            {
                return true;
            }

            // Inside the gap, test the ball exactly against both flat radial ends
            // of the annular mesh. The previous angular approximation inflated the
            // ball by the ring thickness and rejected visibly clear passes.
            float collisionRadius = Mathf.Max(0f, BallRadius - gapEdgeForgiveness);
            float collisionRadiusSquared = collisionRadius * collisionRadius;

            return DistanceSquaredToRingEnd(
                       position,
                       ring,
                       -halfGap) <= collisionRadiusSquared ||
                   DistanceSquaredToRingEnd(
                       position,
                       ring,
                       halfGap) <= collisionRadiusSquared;
        }

        private float DistanceSquaredToRingEnd(
            Vector2 point,
            Ring ring,
            float angleFromGapCenterDegrees)
        {
            Vector2 closestPoint = ClosestPointOnRingEnd(
                point,
                ring,
                angleFromGapCenterDegrees,
                out _);
            return (point - closestPoint).sqrMagnitude;
        }

        private Vector2 ClosestPointOnRingEnd(
            Vector2 point,
            Ring ring,
            float angleFromGapCenterDegrees,
            out Vector2 endDirection)
        {
            float localAngleDegrees =
                ring.GapOffsetDegrees + angleFromGapCenterDegrees;
            float worldAngleDegrees =
                ring.RotationDegrees + localAngleDegrees;
            float localAngle = localAngleDegrees * Mathf.Deg2Rad;
            float worldAngle = worldAngleDegrees * Mathf.Deg2Rad;
            endDirection =
                new Vector2(Mathf.Cos(worldAngle), Mathf.Sin(worldAngle));
            int sideCount = ShapeSideCount();
            float innerRadius = ShapeRadiusAtLocalAngle(
                ring.Radius - ringThickness * 0.5f,
                localAngle,
                sideCount);
            float outerRadius = ShapeRadiusAtLocalAngle(
                ring.Radius + ringThickness * 0.5f,
                localAngle,
                sideCount);
            Vector2 start = endDirection * innerRadius;
            Vector2 end = endDirection * outerRadius;
            Vector2 segment = end - start;
            float segmentLengthSquared = segment.sqrMagnitude;
            float t = segmentLengthSquared > 0f
                ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSquared)
                : 0f;
            return start + segment * t;
        }

        private bool TryGetRingContact(
            Ring ring,
            Vector2 position,
            ShapeBoundaryInfo boundary,
            out RingContactInfo contact)
        {
            contact = default;
            float collisionReach = BallRadius + ringThickness * 0.5f;
            if (boundary.Distance > collisionReach)
            {
                return false;
            }

            float distance = position.magnitude;
            if (distance < 0.0001f)
            {
                return false;
            }

            float ballAngle =
                Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg;
            float gapCenter =
                ring.RotationDegrees + ring.GapOffsetDegrees;
            float distanceFromGapCenter =
                Mathf.Abs(Mathf.DeltaAngle(ballAngle, gapCenter));
            float halfGap = GapDegrees * 0.5f;
            if (distanceFromGapCenter >= halfGap)
            {
                contact.Point = boundary.ClosestPoint;
                contact.Normal = boundary.IsInside
                    ? -boundary.OutwardNormal
                    : boundary.OutwardNormal;
                contact.Separation = collisionReach;
                return true;
            }

            float gapCollisionRadius = Mathf.Max(
                0f,
                BallRadius - gapEdgeForgiveness);
            Vector2 negativeEndPoint = ClosestPointOnRingEnd(
                position,
                ring,
                -halfGap,
                out Vector2 negativeEndDirection);
            Vector2 positiveEndPoint = ClosestPointOnRingEnd(
                position,
                ring,
                halfGap,
                out Vector2 positiveEndDirection);
            Vector2 negativeSeparation = position - negativeEndPoint;
            Vector2 positiveSeparation = position - positiveEndPoint;
            float negativeDistanceSquared =
                negativeSeparation.sqrMagnitude;
            float positiveDistanceSquared =
                positiveSeparation.sqrMagnitude;
            bool useNegativeEnd =
                negativeDistanceSquared <= positiveDistanceSquared;
            Vector2 closestPoint = useNegativeEnd
                ? negativeEndPoint
                : positiveEndPoint;
            Vector2 separation = useNegativeEnd
                ? negativeSeparation
                : positiveSeparation;
            float distanceSquared = useNegativeEnd
                ? negativeDistanceSquared
                : positiveDistanceSquared;
            if (distanceSquared >
                gapCollisionRadius * gapCollisionRadius)
            {
                return false;
            }

            Vector2 contactNormal;
            if (distanceSquared > 0.000001f)
            {
                contactNormal = separation.normalized;
            }
            else
            {
                Vector2 endDirection = useNegativeEnd
                    ? negativeEndDirection
                    : positiveEndDirection;
                Vector2 perpendicular =
                    new Vector2(-endDirection.y, endDirection.x);
                contactNormal =
                    Vector2.Dot(_ballVelocity, perpendicular) <= 0f
                        ? perpendicular
                        : -perpendicular;
            }

            contact.Point = closestPoint;
            contact.Normal = contactNormal;
            contact.Separation = gapCollisionRadius;
            return true;
        }

        private void BounceOff(
            RingContactInfo contact,
            ref BouncePatternState bouncePattern)
        {
            Vector2 normal = contact.Normal.sqrMagnitude > 0.000001f
                ? contact.Normal.normalized
                : -_ballVelocity.normalized;
            _ballPosition =
                contact.Point +
                normal * (contact.Separation + 0.0005f);
            float incomingNormalSpeed =
                Vector2.Dot(_ballVelocity, normal);
            bool reflected = incomingNormalSpeed < -0.0001f;
            if (reflected)
            {
                _ballVelocity -=
                    (1f + bounciness) *
                    incomingNormalSpeed *
                    normal;
            }

            float speed = _ballVelocity.magnitude;
            float clampedMaximum = Mathf.Max(minimumSpeed, maximumSpeed);
            if (speed > 0.0001f && speed < minimumSpeed)
            {
                _ballVelocity = _ballVelocity.normalized * minimumSpeed;
            }
            else if (speed > clampedMaximum)
            {
                _ballVelocity = _ballVelocity.normalized * clampedMaximum;
            }

            if (reflected)
            {
                ApplyNaturalAntiLoopDeflection(
                    normal,
                    ref bouncePattern);
            }

            _ballImpactNormal = normal;
            _ballImpactSquashRemaining = ballImpactSquashDuration;
            SpawnImpactParticles(normal);
        }

        private void ApplyNaturalAntiLoopDeflection(
            Vector2 collisionNormal,
            ref BouncePatternState bouncePattern)
        {
            float speed = _ballVelocity.magnitude;
            if (speed <= 0.0001f)
            {
                return;
            }

            Vector2 outgoingDirection = _ballVelocity / speed;
            bool remembersPreviousBounce =
                bouncePattern.LastOutgoingDirection.sqrMagnitude > 0.5f &&
                bouncePattern.TimeSinceBounce <= antiLoopBounceMemory;
            float reversalAlignment = remembersPreviousBounce
                ? Vector2.Dot(
                    outgoingDirection,
                    -bouncePattern.LastOutgoingDirection)
                : -1f;
            float effectiveSpeed =
                speed * CurrentBallSpeedMultiplier();
            bool isHighSpeedStraightReturn =
                effectiveSpeed >= antiLoopMinimumEffectiveSpeed &&
                reversalAlignment >= antiLoopReversalAlignment;

            if (isHighSpeedStraightReturn &&
                antiLoopMaximumDeflectionDegrees > 0f)
            {
                bouncePattern.StraightReturnCount =
                    Mathf.Min(4, bouncePattern.StraightReturnCount + 1);
                float alignmentSeverity = Mathf.InverseLerp(
                    antiLoopReversalAlignment,
                    1f,
                    reversalAlignment);
                float repeatSeverity = Mathf.Clamp01(
                    (bouncePattern.StraightReturnCount - 1) / 3f);
                float deflectionDegrees = Mathf.Lerp(
                    antiLoopDeflectionDegrees,
                    Mathf.Max(
                        antiLoopDeflectionDegrees,
                        antiLoopMaximumDeflectionDegrees),
                    Mathf.Max(
                        alignmentSeverity * 0.45f,
                        repeatSeverity));

                Vector2 surfaceTangent =
                    new Vector2(
                        -collisionNormal.y,
                        collisionNormal.x);
                float existingTangentialSpeed =
                    Vector2.Dot(
                        outgoingDirection,
                        surfaceTangent);
                float deflectionSign =
                    Mathf.Abs(existingTangentialSpeed) > 0.015f
                        ? Mathf.Sign(existingTangentialSpeed)
                        : bouncePattern.DeflectionSign;
                if (Mathf.Abs(deflectionSign) < 0.5f)
                {
                    deflectionSign = 1f;
                }

                Vector2 deflectedDirection = RotateVector(
                    outgoingDirection,
                    deflectionDegrees *
                    deflectionSign *
                    Mathf.Deg2Rad);
                if (Vector2.Dot(
                        deflectedDirection,
                        collisionNormal) <= 0.02f)
                {
                    deflectionSign = -deflectionSign;
                    deflectedDirection = RotateVector(
                        outgoingDirection,
                        deflectionDegrees *
                        deflectionSign *
                        Mathf.Deg2Rad);
                }

                bouncePattern.DeflectionSign = deflectionSign;
                _ballVelocity = deflectedDirection * speed;
                outgoingDirection = deflectedDirection;
            }
            else
            {
                bouncePattern.StraightReturnCount =
                    Mathf.Max(
                        0,
                        bouncePattern.StraightReturnCount - 1);
            }

            bouncePattern.LastOutgoingDirection = outgoingDirection;
            bouncePattern.TimeSinceBounce = 0f;
        }

        private static BouncePatternState CreateBouncePatternState(int seed)
        {
            return new BouncePatternState
            {
                LastOutgoingDirection = Vector2.zero,
                DeflectionSign = (seed & 1) == 0 ? 1f : -1f,
                TimeSinceBounce = float.MaxValue,
                StraightReturnCount = 0
            };
        }

        private void SpawnImpactParticles(Vector2 collisionNormal)
        {
            if (ballImpactParticleCount <= 0)
            {
                return;
            }

            Vector2 contactPosition =
                _ballPosition - collisionNormal * BallRadius;
            float normalAngle =
                Mathf.Atan2(-collisionNormal.y, -collisionNormal.x);
            for (int i = 0; i < ballImpactParticleCount; i++)
            {
                ImpactParticle particle = _impactParticlePool.Count > 0
                    ? _impactParticlePool.Pop()
                    : new ImpactParticle();
                float angle = normalAngle + RandomRange(-1.2f, 1.2f);
                float speed = RandomRange(0.45f, 1.2f);
                particle.Position = contactPosition;
                particle.Velocity =
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed +
                    _ballVelocity * 0.12f;
                particle.Color = Color.Lerp(_ballColor, Color.white, RandomRange(0.2f, 0.72f));
                particle.Elapsed = 0f;
                particle.Lifetime = ballImpactParticleLifetime * RandomRange(0.72f, 1.25f);
                particle.Diameter = BallRadius * RandomRange(0.38f, 0.78f);
                _impactParticles.Add(particle);
            }
        }

        private void Shatter(Ring ring)
        {
            if (!ring.IsAlive)
            {
                return;
            }

            bool destroyedTintedRing = ring == _tintedInnermostRing;
            ring.IsAlive = false;
            TryReverseRingRotation();
            bool clearedFinalRing = FindInnermostRing() == null;
            BeginCoinFormation(ring, false);
            SpawnExperienceBurst(ring, clearedFinalRing);
            if (clearedFinalRing)
            {
                BeginFinalEscapeSequence(ring);
            }
            if (destroyedTintedRing || _tintedInnermostRing == null)
            {
                ScheduleInnermostTintPromotion();
            }
        }

        private void BeginFinalEscapeSequence(Ring finalRing)
        {
            int normalStandardReward = GetStandardRingCoinReward(finalRing);
            int finalStandardReward = Mathf.CeilToInt(
                normalStandardReward * lastRingRewardMultiplier);
            float productionMultiplier = CurrentCoinProductionMultiplier();
            int normalReward = Mathf.CeilToInt(
                normalStandardReward * productionMultiplier);
            int finalReward = Mathf.CeilToInt(
                finalStandardReward * productionMultiplier);

            _finalBallBurstCoinCount = Mathf.Max(0, finalReward - normalReward);
            _finalEscapeElapsed = 0f;
            _finalBallExploded = false;
            _finalEscapeActive = true;
            _ballVelocity = Vector2.zero;
            _ballImpactSquashRemaining = 0f;
            DisableBallClone(true);
            if (_ballTrailRenderer != null)
            {
                _ballTrailRenderer.emitting = false;
            }
        }

        private void UpdateFinalEscapeSequence(float deltaTime)
        {
            _finalEscapeElapsed += deltaTime;
            float explosionTime =
                finalBallGrowDuration + finalBallShrinkDuration;
            if (!_finalBallExploded &&
                _finalEscapeElapsed >= explosionTime)
            {
                _finalBallExploded = true;
                int gatheredCoinCount = _finalBallBurstCoinCount;
                int gatheredStandardCoinCount = 0;
                foreach (CoinFormation formation in _coinFormations)
                {
                    gatheredCoinCount += formation.CoinCenters.Length;
                    gatheredStandardCoinCount +=
                        formation.StandardCoinCount;
                }
                _coinFormations.Clear();
                SpawnFinalBallCoinBurst(
                    _ballPosition,
                    gatheredCoinCount,
                    gatheredStandardCoinCount);
                if (_ballTrailRenderer != null)
                {
                    _ballTrailRenderer.Clear();
                }
            }

            if (automaticallyRestart &&
                _finalBallExploded &&
                _finalEscapeElapsed >= explosionTime + finalBallResetDelay)
            {
                ResetSimulation();
            }
        }

        private void ScheduleInnermostTintPromotion()
        {
            _tintedInnermostRing = null;
            _innermostTintUpdateCooldown = innermostTintUpdateBuffer;
            ApplyPalette();

            if (_innermostTintUpdateCooldown <= 0f)
            {
                _tintedInnermostRing = FindInnermostRing();
                ApplyPalette();
            }
        }

        private void UpdateInnermostTintPromotion(float deltaTime)
        {
            if (_tintedInnermostRing != null || _innermostTintUpdateCooldown <= 0f)
            {
                return;
            }

            _innermostTintUpdateCooldown =
                Mathf.Max(0f, _innermostTintUpdateCooldown - deltaTime);
            if (_innermostTintUpdateCooldown > 0f)
            {
                return;
            }

            _tintedInnermostRing = FindInnermostRing();
            ApplyPalette();
        }

        private void BeginCoinFormation(Ring ring, bool clearedFinalRing)
        {
            Color fragmentColor = GetDisplayedRingColor(ring);
            float visibleArc = 360f - GapDegrees;
            float startAngle =
                ring.GapOffsetDegrees + GapDegrees * 0.5f;
            int shapeSideCount = ShapeSideCount();
            float visibleArcLength =
                ShapePerimeter(ring.Radius, shapeSideCount) *
                (visibleArc / 360f);
            float dotPitch = Mathf.Max(0.01f, brokenRingDotDiameter + brokenRingDotGap);
            int fragmentCount = Mathf.Clamp(
                Mathf.FloorToInt(visibleArcLength / dotPitch),
                10,
                300);

            int standardRewardCount = GetStandardRingCoinReward(ring);
            if (clearedFinalRing)
            {
                standardRewardCount =
                    Mathf.CeilToInt(standardRewardCount * lastRingRewardMultiplier);
            }
            int rewardCount = Mathf.CeilToInt(
                standardRewardCount * CurrentCoinProductionMultiplier());
            rewardCount = Mathf.Min(rewardCount, fragmentCount);
            standardRewardCount = Mathf.Min(standardRewardCount, rewardCount);

            Vector2 impactDirection = _ballPosition.sqrMagnitude > 0.0001f
                ? _ballPosition.normalized
                : Vector2.down;
            Vector2 impactTangent = new Vector2(-impactDirection.y, impactDirection.x);
            float impactWorldAngle =
                Mathf.Atan2(impactDirection.y, impactDirection.x);
            float impactLocalAngle =
                impactWorldAngle - ring.RotationDegrees * Mathf.Deg2Rad;
            float impactRadius = ShapeRadiusAtLocalAngle(
                ring.Radius,
                impactLocalAngle,
                shapeSideCount);
            Vector2 assemblyCenter = impactDirection * impactRadius;
            float assemblySpacing = coinDiameter * 1.18f;
            float impactAngle = Mathf.Atan2(impactDirection.y, impactDirection.x) * Mathf.Rad2Deg;
            float dumpCenterAngle =
                impactAngle + RandomRange(-coinDumpDirectionScatter, coinDumpDirectionScatter);
            float dumpCenterRadius =
                OuterRadius + coinOutsideDistance + RandomRange(0f, coinOutsideRandomSpread);
            float dumpArcHeight = coinDumpArcHeight * RandomRange(0.78f, 1.24f);

            var formation = new CoinFormation
            {
                RingColor = fragmentColor,
                ScatterDuration = brokenRingLifetime * 0.42f,
                GatherDuration = brokenRingLifetime * 0.58f,
                CoinCenters = new Vector2[rewardCount],
                DisperseTargets = new Vector2[rewardCount],
                DisperseArcOffsets = new Vector2[rewardCount],
                DisperseDurationScales = new float[rewardCount],
                StandardCoinCount = standardRewardCount
            };

            for (int coinIndex = 0; coinIndex < rewardCount; coinIndex++)
            {
                float centeredIndex = coinIndex - (rewardCount - 1) * 0.5f;
                formation.CoinCenters[coinIndex] =
                    assemblyCenter + impactTangent * (centeredIndex * assemblySpacing);

                // Two samples produce a center-heavy spread like a tossed handful instead of
                // evenly spaced targets around a preselected set of perimeter locations.
                float handfulOffset =
                    (RandomRange(-1f, 1f) + RandomRange(-1f, 1f)) *
                    (coinDumpHandfulSpread * 0.5f);
                float disperseAngle =
                    (dumpCenterAngle + handfulOffset) * Mathf.Deg2Rad;
                float disperseRadius = Mathf.Max(
                    OuterRadius + coinOutsideDistance,
                    dumpCenterRadius +
                    RandomRange(-coinOutsideRandomSpread, coinOutsideRandomSpread) * 0.32f);
                formation.DisperseTargets[coinIndex] =
                    new Vector2(Mathf.Cos(disperseAngle), Mathf.Sin(disperseAngle)) * disperseRadius;

                Vector2 travel =
                    formation.DisperseTargets[coinIndex] - formation.CoinCenters[coinIndex];
                Vector2 perpendicular = travel.sqrMagnitude > 0.0001f
                    ? new Vector2(-travel.y, travel.x).normalized
                    : Vector2.up;
                formation.DisperseArcOffsets[coinIndex] =
                    Vector2.up * (dumpArcHeight * RandomRange(0.82f, 1.18f)) +
                    perpendicular * RandomRange(-0.12f, 0.12f);
                formation.DisperseDurationScales[coinIndex] = RandomRange(0.78f, 1.24f);
            }

            Vector2 gravityAcceleration = Vector2.down * (brokenRingGravity * Physics2D.gravity.magnitude);
            for (int i = 0; i < fragmentCount; i++)
            {
                float arcT = (i + 0.5f) / fragmentCount;
                float localAngle =
                    (startAngle + visibleArc * arcT) * Mathf.Deg2Rad;
                float worldAngle =
                    localAngle + ring.RotationDegrees * Mathf.Deg2Rad;
                Vector2 radial =
                    new Vector2(Mathf.Cos(worldAngle), Mathf.Sin(worldAngle));
                Vector2 tangent = new Vector2(-radial.y, radial.x);
                Vector2 sharedDrop = Vector2.down * brokenRingDropSpeed + _ballVelocity * 0.035f;
                float downwardScatter = RandomRange(0f, brokenRingDotScatter);
                float sidewaysScatter = RandomRange(-brokenRingDotScatter, brokenRingDotScatter) * 0.35f;
                Vector2 separatingVelocity =
                    Vector2.down * downwardScatter +
                    radial * sidewaysScatter +
                    tangent * RandomRange(-brokenRingDotScatter, brokenRingDotScatter) * 0.18f;
                Vector2 initialVelocity = sharedDrop + separatingVelocity;
                float boundaryRadius = ShapeRadiusAtLocalAngle(
                    ring.Radius,
                    localAngle,
                    shapeSideCount);
                Vector2 startPosition = radial * boundaryRadius;
                Vector2 gatherStart =
                    startPosition +
                    initialVelocity * formation.ScatterDuration +
                    gravityAcceleration * (0.5f * formation.ScatterDuration * formation.ScatterDuration);

                int coinIndex = i % rewardCount;
                int dotIndexWithinCoin = i / rewardCount;
                int dotsInThisCoin = (fragmentCount + rewardCount - 1 - coinIndex) / rewardCount;
                float fillRadius =
                    coinDiameter * 0.39f *
                    Mathf.Sqrt((dotIndexWithinCoin + 0.5f) / Mathf.Max(1f, dotsInThisCoin));
                float fillAngle = dotIndexWithinCoin * GoldenAngleDegrees * Mathf.Deg2Rad;
                Vector2 coinTarget =
                    formation.CoinCenters[coinIndex] +
                    new Vector2(Mathf.Cos(fillAngle), Mathf.Sin(fillAngle)) * fillRadius;

                formation.FragmentMotions.Add(new FragmentMotion
                {
                    StartPosition = startPosition,
                    InitialVelocity = initialVelocity,
                    GatherStartPosition = gatherStart,
                    CoinTargetPosition = coinTarget
                });
            }

            _coinFormations.Add(formation);
        }

        private int GetStandardRingCoinReward(Ring ring)
        {
            float rewardT = Mathf.Pow(
                ring.NormalizedRadius,
                outerRingRewardBias);
            return Mathf.Max(
                1,
                Mathf.RoundToInt(
                    Mathf.Lerp(
                        innerRingCoinReward,
                        outerRingCoinReward,
                        rewardT) *
                    CurrentCoinYieldUpgradeMultiplier()));
        }

        private void SpawnExperienceBurst(Ring ring, bool clearedFinalRing)
        {
            RingEscapeSimulation host = _gridOwner != null ? _gridOwner : this;
            int reward = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    Mathf.Lerp(
                        innerRingExperienceReward,
                        outerRingExperienceReward,
                        ring.NormalizedRadius) *
                    host._experienceGainMultiplier));
            if (clearedFinalRing)
            {
                reward = Mathf.CeilToInt(reward * finalRingExperienceMultiplier);
            }

            Vector2 origin = _ballPosition;
            Vector2 outward = origin.sqrMagnitude > 0.0001f
                ? origin.normalized
                : Vector2.up;
            for (int particleIndex = 0; particleIndex < reward; particleIndex++)
            {
                if (_experienceParticlePool.Count == 0)
                {
                    AwardExperience(1);
                    continue;
                }

                ExperienceParticle particle = _experienceParticlePool.Pop();
                float angle =
                    Mathf.Atan2(outward.y, outward.x) +
                    RandomRange(-1.15f, 1.15f);
                float burstDistance = RandomRange(0.28f, 0.72f);
                particle.StartPosition =
                    origin +
                    new Vector2(
                        RandomRange(-0.08f, 0.08f),
                        RandomRange(-0.08f, 0.08f));
                particle.BurstPosition =
                    particle.StartPosition +
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                    burstDistance;
                particle.ControlOffset = new Vector2(
                    RandomRange(-0.65f, 0.65f),
                    RandomRange(0.55f, 1.25f));
                particle.Position = particle.StartPosition;
                particle.Elapsed = -particleIndex * RandomRange(0.018f, 0.045f);
                particle.BurstDuration = RandomRange(0.16f, 0.28f);
                particle.FlightDuration =
                    experienceCollectionDuration * RandomRange(0.82f, 1.18f);
                particle.Scale = 0f;
                particle.PulseOffset = RandomRange(0f, Mathf.PI * 2f);
                _experienceParticles.Add(particle);
            }
        }

        private void TryReverseRingRotation()
        {
            if (_rotationReverseCooldown > 0f)
            {
                return;
            }

            _rotationDirection *= -1f;
            _rotationReverseCooldown = rotationReverseBuffer;
        }

        private float RandomRange(float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)_random.NextDouble());
        }

        private void UpdateCoinRewards(float deltaTime)
        {
            _ballImpactSquashRemaining =
                Mathf.Max(0f, _ballImpactSquashRemaining - deltaTime);
            UpdateImpactParticles(deltaTime);

            bool attractingFragments =
                _finalEscapeActive &&
                !_finalBallExploded;
            if (!attractingFragments)
            {
                for (int i = 0; i < _coinFormations.Count; i++)
                {
                    _coinFormations[i].Elapsed += deltaTime;
                }
            }

            UpdateFormationDotMeshes();

            if (!attractingFragments)
            {
                for (int i = _coinFormations.Count - 1; i >= 0; i--)
                {
                    CoinFormation formation = _coinFormations[i];
                    if (formation.Elapsed <
                        formation.ScatterDuration +
                        formation.GatherDuration)
                    {
                        continue;
                    }

                    SpawnCollectibleCoins(formation);
                    _coinFormations.RemoveAt(i);
                }
            }

            UpdateCollectibleCoins(deltaTime);
            UpdateExperienceParticles(deltaTime);
        }

        private void UpdateImpactParticles(float deltaTime)
        {
            Vector2 particleGravity = Vector2.down * (gravity * 0.42f);
            for (int i = _impactParticles.Count - 1; i >= 0; i--)
            {
                ImpactParticle particle = _impactParticles[i];
                particle.Elapsed += deltaTime;
                if (particle.Elapsed >= particle.Lifetime)
                {
                    _impactParticles.RemoveAt(i);
                    _impactParticlePool.Push(particle);
                    continue;
                }

                particle.Velocity += particleGravity * deltaTime;
                particle.Position += particle.Velocity * deltaTime;
            }
        }

        private void UpdateFormationDotMeshes()
        {
            if (_fragmentBatchMesh == null || _fragmentBatchRenderer == null)
            {
                return;
            }

            int visibleDotCount = 0;
            int verticesPerDot = DotMeshSegments + 1;
            Vector2 gravityAcceleration =
                Vector2.down * (brokenRingGravity * Physics2D.gravity.magnitude);
            float finalAttraction =
                GetFinalFragmentAttractionProgress();
            Vector2 finalAttractionTarget =
                _ballPosition + GetFinalBallShakeOffset();

            foreach (CoinFormation formation in _coinFormations)
            {
                if (visibleDotCount >= fragmentBatchCapacity)
                {
                    break;
                }

                float gatherT = Mathf.Clamp01(
                    (formation.Elapsed - formation.ScatterDuration) /
                    Mathf.Max(0.0001f, formation.GatherDuration));
                float easedGather = Smooth01(gatherT);
                Color formationColor = formation.Elapsed <= formation.ScatterDuration
                    ? formation.RingColor
                    : Color.Lerp(formation.RingColor, coinGoldColor, easedGather);

                foreach (FragmentMotion motion in formation.FragmentMotions)
                {
                    if (visibleDotCount >= fragmentBatchCapacity)
                    {
                        break;
                    }

                    Vector2 center;
                    float diameter;
                    if (formation.Elapsed <= formation.ScatterDuration)
                    {
                        float time = formation.Elapsed;
                        center =
                            motion.StartPosition +
                            motion.InitialVelocity * time +
                            gravityAcceleration * (0.5f * time * time);
                        diameter = brokenRingDotDiameter;
                    }
                    else
                    {
                        center = Vector2.Lerp(
                            motion.GatherStartPosition,
                            motion.CoinTargetPosition,
                            easedGather);
                        diameter = Mathf.Lerp(
                            brokenRingDotDiameter,
                            brokenRingDotDiameter * 0.72f,
                            easedGather);
                    }

                    Color dotColor = formationColor;
                    if (finalAttraction > 0f)
                    {
                        Vector2 toTarget =
                            finalAttractionTarget - center;
                        Vector2 tangent = toTarget.sqrMagnitude > 0.0001f
                            ? new Vector2(-toTarget.y, toTarget.x).normalized
                            : Vector2.up;
                        float spiralDirection =
                            (visibleDotCount & 1) == 0 ? 1f : -1f;
                        float spiralPhase =
                            0.42f +
                            (visibleDotCount % 11) / 10f * 0.58f;
                        Vector2 spiralOffset =
                            tangent *
                            (Mathf.Sin(finalAttraction * Mathf.PI) *
                             finalFragmentAttractionSwirl *
                             spiralDirection *
                             spiralPhase);
                        center =
                            Vector2.Lerp(
                                center,
                                finalAttractionTarget,
                                finalAttraction) +
                            spiralOffset;
                        diameter = Mathf.Lerp(
                            diameter,
                            brokenRingDotDiameter * 0.34f,
                            finalAttraction);
                        dotColor = Color.Lerp(
                            formationColor,
                            Color.Lerp(
                                _ballColor,
                                coinGoldColor,
                                0.58f),
                            finalAttraction);
                    }

                    int vertexStart = visibleDotCount * verticesPerDot;
                    _fragmentBatchVertices[vertexStart] =
                        new Vector3(center.x, center.y, -0.1f);
                    _fragmentBatchColors[vertexStart] = dotColor;
                    float radius = diameter * 0.5f;
                    for (int segment = 0; segment < DotMeshSegments; segment++)
                    {
                        Vector2 direction = DotDirections[segment];
                        int vertexIndex = vertexStart + segment + 1;
                        _fragmentBatchVertices[vertexIndex] = new Vector3(
                            center.x + direction.x * radius,
                            center.y + direction.y * radius,
                            -0.1f);
                        _fragmentBatchColors[vertexIndex] = dotColor;
                    }

                    visibleDotCount++;
                }
            }

            foreach (ImpactParticle particle in _impactParticles)
            {
                if (visibleDotCount >= fragmentBatchCapacity)
                {
                    break;
                }

                float lifetimeT = Mathf.Clamp01(particle.Elapsed / particle.Lifetime);
                float radius = particle.Diameter * (1f - lifetimeT * 0.72f) * 0.5f;
                Color color = particle.Color;
                color.a = 1f - Smooth01(lifetimeT);
                int vertexStart = visibleDotCount * verticesPerDot;
                _fragmentBatchVertices[vertexStart] =
                    new Vector3(particle.Position.x, particle.Position.y, -0.12f);
                _fragmentBatchColors[vertexStart] = color;
                for (int segment = 0; segment < DotMeshSegments; segment++)
                {
                    Vector2 direction = DotDirections[segment];
                    int vertexIndex = vertexStart + segment + 1;
                    _fragmentBatchVertices[vertexIndex] = new Vector3(
                        particle.Position.x + direction.x * radius,
                        particle.Position.y + direction.y * radius,
                        -0.12f);
                    _fragmentBatchColors[vertexIndex] = color;
                }

                visibleDotCount++;
            }

            _fragmentBatchRenderer.enabled = visibleDotCount > 0;
            if (visibleDotCount == 0)
            {
                return;
            }

            int usedVertexCount = visibleDotCount * verticesPerDot;
            _fragmentBatchMesh.SetVertexBufferData(
                _fragmentBatchVertices,
                0,
                0,
                usedVertexCount,
                0,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            _fragmentBatchMesh.SetVertexBufferData(
                _fragmentBatchColors,
                0,
                0,
                usedVertexCount,
                1,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            _fragmentBatchMesh.SetSubMesh(
                0,
                new SubMeshDescriptor(
                    0,
                    visibleDotCount * DotMeshSegments * 3,
                    MeshTopology.Triangles),
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        }

        private void SpawnCollectibleCoins(CoinFormation formation)
        {
            for (int i = 0; i < formation.CoinCenters.Length; i++)
            {
                if (_coinPool.Count == 0)
                {
                    AwardAndReleaseCoin(0);
                }

                CollectibleCoin coin = _coinPool.Pop();
                coin.Position = formation.CoinCenters[i];
                coin.StartPosition = formation.CoinCenters[i];
                coin.TargetPosition = formation.DisperseTargets[i];
                coin.DisperseArcOffset = formation.DisperseArcOffsets[i];
                coin.DisperseDurationScale = formation.DisperseDurationScales[i];
                coin.IsBonus = i >= formation.StandardCoinCount;
                if (coin.IsBonus)
                {
                    coin.DisperseArcOffset *= 1.6f;
                    coin.DisperseDurationScale *= 0.82f;
                }
                coin.Scale = 0f;
                coin.Elapsed = 0f;
                coin.PulseOffset = RandomRange(0f, Mathf.PI * 2f);
                coin.CollectionDuration = coinCollectionDuration;
                coin.CollectionArcHeight = coin.IsBonus ? 1.35f : 0.85f;
                coin.State = CoinState.Dispersing;
                _coins.Add(coin);
                if (coin.IsBonus)
                {
                    SpawnBonusCoinBurst(coin.Position);
                }
            }
            _coinColorLayoutDirty = true;
        }

        private void SpawnBonusCoinBurst(Vector2 position)
        {
            for (int i = 0; i < 4; i++)
            {
                ImpactParticle particle = _impactParticlePool.Count > 0
                    ? _impactParticlePool.Pop()
                    : new ImpactParticle();
                float angle = RandomRange(0f, Mathf.PI * 2f);
                float speed = RandomRange(0.35f, 0.9f);
                particle.Position = position;
                particle.Velocity =
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                particle.Color =
                    Color.Lerp(coinGoldColor, Color.white, RandomRange(0.45f, 0.9f));
                particle.Elapsed = 0f;
                particle.Lifetime = RandomRange(0.22f, 0.42f);
                particle.Diameter = coinDiameter * RandomRange(0.16f, 0.28f);
                _impactParticles.Add(particle);
            }
        }

        private void SpawnFinalBallCoinBurst(
            Vector2 origin,
            int coinCount,
            int standardCoinCount)
        {
            float minimumDistance = Mathf.Max(
                0.05f,
                Mathf.Min(
                    finalBallCoinBurstDistance.x,
                    finalBallCoinBurstDistance.y));
            float maximumDistance = Mathf.Max(
                minimumDistance,
                Mathf.Max(
                    finalBallCoinBurstDistance.x,
                    finalBallCoinBurstDistance.y));
            float clumpAngle = RandomRange(0f, Mathf.PI * 2f);
            int clumpCount = Mathf.Clamp(
                Mathf.CeilToInt(coinCount / 5f),
                2,
                5);

            for (int coinIndex = 0; coinIndex < coinCount; coinIndex++)
            {
                if (_coinPool.Count == 0)
                {
                    if (_coins.Count == 0)
                    {
                        break;
                    }
                    AwardAndReleaseCoin(0);
                }

                int clumpIndex = coinIndex % clumpCount;
                float clumpCenter =
                    clumpAngle +
                    clumpIndex * (Mathf.PI * 2f / clumpCount) +
                    RandomRange(-0.28f, 0.28f);
                float angle =
                    clumpCenter +
                    (RandomRange(-1f, 1f) + RandomRange(-1f, 1f)) * 0.24f;
                Vector2 direction =
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = RandomRange(minimumDistance, maximumDistance);
                Vector2 startPosition =
                    origin +
                    new Vector2(
                        RandomRange(-0.035f, 0.035f),
                        RandomRange(-0.035f, 0.035f));
                Vector2 travel = direction * distance;
                Vector2 perpendicular =
                    new Vector2(-direction.y, direction.x);

                CollectibleCoin coin = _coinPool.Pop();
                coin.Position = startPosition;
                coin.StartPosition = startPosition;
                coin.TargetPosition =
                    startPosition +
                    travel +
                    Vector2.down * RandomRange(0.04f, 0.2f);
                coin.DisperseArcOffset =
                    Vector2.up * RandomRange(0.32f, 0.88f) +
                    perpendicular * RandomRange(-0.28f, 0.28f);
                coin.DisperseDurationScale = RandomRange(0.52f, 0.86f);
                coin.IsBonus = coinIndex >= standardCoinCount;
                coin.Scale = 0f;
                coin.Elapsed = 0f;
                coin.PulseOffset = RandomRange(0f, Mathf.PI * 2f);
                coin.CollectionDuration = coinCollectionDuration * 0.82f;
                coin.CollectionArcHeight = 1.5f;
                coin.State = CoinState.Dispersing;
                _coins.Add(coin);
            }

            _coinColorLayoutDirty = true;
            int sparkBursts = Mathf.Clamp(coinCount, 5, 14);
            for (int burstIndex = 0; burstIndex < sparkBursts; burstIndex++)
            {
                SpawnBonusCoinBurst(
                    origin +
                    new Vector2(
                        RandomRange(-0.06f, 0.06f),
                        RandomRange(-0.06f, 0.06f)));
            }
        }

        private void UpdateCollectibleCoins(float deltaTime)
        {
            for (int i = _coins.Count - 1; i >= 0; i--)
            {
                CollectibleCoin coin = _coins[i];
                coin.Elapsed += deltaTime;

                if (coin.State == CoinState.Dispersing)
                {
                    float duration =
                        coinDisperseDuration * Mathf.Max(0.1f, coin.DisperseDurationScale);
                    float disperseT = Mathf.Clamp01(coin.Elapsed / duration);
                    Vector2 position = Vector2.Lerp(
                        coin.StartPosition,
                        coin.TargetPosition,
                        disperseT);
                    position +=
                        coin.DisperseArcOffset * (4f * disperseT * (1f - disperseT));
                    SetCoinPosition(coin, position);
                    float bonusPop = coin.IsBonus
                        ? 1.28f + Mathf.Sin(disperseT * Mathf.PI * 3f) *
                            (1f - disperseT) * 0.16f
                        : 1f;
                    coin.Scale = coinDiameter * EaseOutBack(disperseT) * bonusPop;

                    if (disperseT >= 1f)
                    {
                        coin.State = CoinState.Available;
                        coin.Elapsed = 0f;
                        SetCoinPosition(coin, coin.TargetPosition);
                    }
                }
                else if (coin.State == CoinState.Available)
                {
                    float pulseStrength = coin.IsBonus ? 0.13f : 0.055f;
                    float pulseSpeed = coin.IsBonus ? 6.4f : 3.2f;
                    float baseScale = coin.IsBonus ? 1.22f : 1f;
                    float pulse =
                        baseScale +
                        Mathf.Sin(Time.time * pulseSpeed + coin.PulseOffset) * pulseStrength;
                    coin.Scale = coinDiameter * pulse;
                }
                else
                {
                    if (coin.Elapsed < 0f)
                    {
                        float waitingPulse =
                            (coin.IsBonus ? 1.22f : 1f) +
                            Mathf.Sin(
                                Time.time * (coin.IsBonus ? 6.4f : 3.2f) +
                                coin.PulseOffset) *
                            (coin.IsBonus ? 0.13f : 0.055f);
                        coin.Scale = coinDiameter * waitingPulse;
                        continue;
                    }

                    float collectionT = Mathf.Clamp01(
                        coin.Elapsed / Mathf.Max(0.05f, coin.CollectionDuration));
                    float easedCollection = Smooth01(collectionT);
                    Vector2 hudTarget = GetHudCoinLocalPosition();
                    Vector2 control =
                        Vector2.Lerp(coin.CollectionStartPosition, hudTarget, 0.5f) +
                        Vector2.up * coin.CollectionArcHeight;
                    if (coin.IsBonus)
                    {
                        control += new Vector2(
                            Mathf.Sin(coin.PulseOffset) * 0.55f,
                            0.45f);
                    }
                    float inverseT = 1f - easedCollection;
                    Vector2 position =
                        inverseT * inverseT * coin.CollectionStartPosition +
                        2f * inverseT * easedCollection * control +
                        easedCollection * easedCollection * hudTarget;
                    SetCoinPosition(coin, position);
                    coin.Scale =
                        coinDiameter *
                        Mathf.Lerp(coin.IsBonus ? 1.48f : 1.15f, 0.32f, easedCollection);

                    if (collectionT >= 1f)
                    {
                        AwardAndReleaseCoin(i);
                    }
                }
            }

            UpdateCoinBatchMesh();
            TryCollectHoveredCoin();
        }

        private void UpdateExperienceParticles(float deltaTime)
        {
            for (int index = _experienceParticles.Count - 1; index >= 0; index--)
            {
                ExperienceParticle particle = _experienceParticles[index];
                particle.Elapsed += deltaTime;
                if (particle.Elapsed < 0f)
                {
                    particle.Position = particle.StartPosition;
                    particle.Scale = 0f;
                    continue;
                }

                if (particle.Elapsed < particle.BurstDuration)
                {
                    float burstT = Mathf.Clamp01(
                        particle.Elapsed / Mathf.Max(0.01f, particle.BurstDuration));
                    float easedBurst = 1f - Mathf.Pow(1f - burstT, 3f);
                    particle.Position = Vector2.Lerp(
                        particle.StartPosition,
                        particle.BurstPosition,
                        easedBurst);
                    particle.Scale = 0.12f * EaseOutBack(burstT);
                    continue;
                }

                float flightT = Mathf.Clamp01(
                    (particle.Elapsed - particle.BurstDuration) /
                    Mathf.Max(0.05f, particle.FlightDuration));
                float easedFlight = Smooth01(flightT);
                Vector2 target = GetHudExperienceLocalPosition();
                Vector2 control = particle.BurstPosition + particle.ControlOffset;
                float inverseT = 1f - easedFlight;
                particle.Position =
                    inverseT * inverseT * particle.BurstPosition +
                    2f * inverseT * easedFlight * control +
                    easedFlight * easedFlight * target;
                float pulse =
                    1f + Mathf.Sin(Time.time * 9f + particle.PulseOffset) * 0.12f;
                particle.Scale =
                    0.12f * Mathf.Lerp(1f, 0.34f, easedFlight) * pulse;

                if (flightT >= 1f)
                {
                    _experienceParticles.RemoveAt(index);
                    particle.Elapsed = 0f;
                    particle.Scale = 0f;
                    _experienceParticlePool.Push(particle);
                    AwardExperience(1);
                }
            }

            UpdateExperienceBatchMesh();
        }

        private void UpdateExperienceBatchMesh()
        {
            if (_experienceBatchMesh == null || _experienceBatchRenderer == null)
            {
                return;
            }

            int activeCount = Mathf.Min(
                _experienceParticles.Count,
                experienceParticlePoolCapacity);
            _experienceBatchRenderer.enabled = activeCount > 0;
            if (activeCount == 0)
            {
                return;
            }

            int verticesPerParticle = DotMeshSegments + 1;
            for (int particleIndex = 0; particleIndex < activeCount; particleIndex++)
            {
                ExperienceParticle particle = _experienceParticles[particleIndex];
                int vertexStart = particleIndex * verticesPerParticle;
                float rotation =
                    Time.time * 2.4f + particle.PulseOffset;
                Color edgeColor = Color.Lerp(
                    experienceColor,
                    Color.white,
                    0.2f + Mathf.Clamp01(particle.Scale / 0.12f) * 0.16f);
                _experienceBatchVertices[vertexStart] =
                    new Vector3(particle.Position.x, particle.Position.y, -0.22f);
                _experienceBatchColors[vertexStart] = Color.white;
                for (int segment = 0; segment < DotMeshSegments; segment++)
                {
                    float angle =
                        segment / (float)DotMeshSegments * Mathf.PI * 2f +
                        rotation;
                    int vertexIndex = vertexStart + segment + 1;
                    float alternatingScale = (segment & 1) == 0 ? 1f : 0.62f;
                    float radius = particle.Scale * alternatingScale;
                    _experienceBatchVertices[vertexIndex] = new Vector3(
                        particle.Position.x + Mathf.Cos(angle) * radius,
                        particle.Position.y + Mathf.Sin(angle) * radius,
                        -0.22f);
                    _experienceBatchColors[vertexIndex] = edgeColor;
                }
            }

            int usedVertexCount = activeCount * verticesPerParticle;
            _experienceBatchMesh.SetVertexBufferData(
                _experienceBatchVertices,
                0,
                0,
                usedVertexCount,
                0,
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices);
            _experienceBatchMesh.SetVertexBufferData(
                _experienceBatchColors,
                0,
                0,
                usedVertexCount,
                1,
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices);
            _experienceBatchMesh.SetSubMesh(
                0,
                new SubMeshDescriptor(
                    0,
                    activeCount * DotMeshSegments * 3,
                    MeshTopology.Triangles),
                MeshUpdateFlags.DontRecalculateBounds |
                MeshUpdateFlags.DontValidateIndices);
        }

        private void AwardExperience(int amount)
        {
            RingEscapeSimulation host = _gridOwner != null ? _gridOwner : this;
            host.ReceiveExperience(amount);
        }

        private void ReceiveExperience(int amount)
        {
            _currentExperience += Mathf.Max(0, amount);
            _experienceFillPulseRemaining =
                Mathf.Max(
                    _experienceFillPulseRemaining,
                    experienceFillPulseDuration);
            UpdateProgressionHud();
            TryStartLevelUp();
        }

        private int ExperienceRequirementForLevel(int level)
        {
            return Mathf.Max(
                1,
                Mathf.RoundToInt(
                    baseExperienceRequirement *
                    Mathf.Pow(experienceRequirementGrowth, Mathf.Max(0, level - 1))));
        }

        private Vector2 GetHudExperienceLocalPosition()
        {
            if (_camera == null)
            {
                return Vector2.zero;
            }

            Vector3 screenPosition = new Vector3(
                Screen.width * 0.5f,
                Mathf.Max(
                    experienceBarSize.y * 50f,
                    Screen.height -
                    experienceBarTopOffset *
                    Mathf.Max(
                        0.01f,
                        _hudCanvas != null
                            ? _hudCanvas.scaleFactor
                            : 1f)),
                Mathf.Abs(_camera.transform.position.z));
            Vector3 worldPosition = _camera.ScreenToWorldPoint(screenPosition);
            return transform.InverseTransformPoint(worldPosition);
        }

        private void AwardAndReleaseCoin(int coinIndex)
        {
            if (coinIndex < 0 || coinIndex >= _coins.Count)
            {
                return;
            }

            if (_gridOwner != null)
            {
                _gridOwner._coinCount++;
            }
            else
            {
                _coinCount++;
            }

            CollectibleCoin coin = _coins[coinIndex];
            _coins.RemoveAt(coinIndex);
            _coinColorLayoutDirty = true;
            coin.Scale = 0f;
            coin.Elapsed = 0f;
            _coinPool.Push(coin);
        }

        private void UpdateCoinBatchMesh()
        {
            if (_coinBatchMesh == null || _coinBatchRenderer == null)
            {
                return;
            }

            int activeCoinCount = _coins.Count;
            _coinBatchRenderer.enabled = activeCoinCount > 0;
            if (activeCoinCount == 0)
            {
                return;
            }

            int verticesPerCoin = _coinBaseVertices.Length;
            for (int coinIndex = 0; coinIndex < activeCoinCount; coinIndex++)
            {
                CollectibleCoin coin = _coins[coinIndex];
                int vertexStart = coinIndex * verticesPerCoin;
                for (int vertexIndex = 0; vertexIndex < verticesPerCoin; vertexIndex++)
                {
                    Vector3 source = _coinBaseVertices[vertexIndex];
                    _coinBatchVertices[vertexStart + vertexIndex] = new Vector3(
                        coin.Position.x + source.x * coin.Scale,
                        coin.Position.y + source.y * coin.Scale,
                        -0.16f + source.z * coin.Scale);
                    if (_coinColorLayoutDirty)
                    {
                        Color color = _coinBaseColors[vertexIndex];
                        _coinBatchColors[vertexStart + vertexIndex] = coin.IsBonus
                            ? Color.Lerp(color, Color.white, 0.36f)
                            : color;
                    }
                }
            }

            _coinBatchMesh.SetVertexBufferData(
                _coinBatchVertices,
                0,
                0,
                activeCoinCount * verticesPerCoin,
                0,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            if (_coinColorLayoutDirty)
            {
                _coinBatchMesh.SetVertexBufferData(
                    _coinBatchColors,
                    0,
                    0,
                    activeCoinCount * verticesPerCoin,
                    1,
                    MeshUpdateFlags.DontRecalculateBounds |
                    MeshUpdateFlags.DontValidateIndices);
                _coinColorLayoutDirty = false;
            }
            _coinBatchMesh.SetSubMesh(
                0,
                new SubMeshDescriptor(
                    0,
                    activeCoinCount * _coinBaseTriangles.Length,
                    MeshTopology.Triangles),
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        }

        private void TryCollectHoveredCoin()
        {
            if (!TryGetMouseWorldPosition(out Vector2 mouseWorldPosition))
            {
                return;
            }

            float pickupRadiusWorld =
                coinPickupRadius * Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            float pickupRadiusSquared = pickupRadiusWorld * pickupRadiusWorld;

            foreach (CollectibleCoin coin in _coins)
            {
                if (coin.State != CoinState.Available)
                {
                    continue;
                }

                float distanceSquared =
                    ((Vector2)transform.TransformPoint(coin.Position) - mouseWorldPosition).sqrMagnitude;
                if (distanceSquared <= pickupRadiusSquared)
                {
                    coin.State = CoinState.Collecting;
                    coin.Elapsed = 0f;
                    coin.CollectionStartPosition = coin.Position;
                    coin.CollectionDuration = coinCollectionDuration;
                    coin.CollectionArcHeight = coin.IsBonus ? 1.35f : 0.85f;
                }
            }
        }

        private bool TryGetMouseWorldPosition(out Vector2 mouseWorldPosition)
        {
            mouseWorldPosition = Vector2.zero;
            if (!Input.mousePresent || _camera == null)
            {
                return false;
            }

            Vector3 mouseScreenPosition = Input.mousePosition;
            mouseScreenPosition.z = Mathf.Abs(_camera.transform.position.z);
            mouseWorldPosition = _camera.ScreenToWorldPoint(mouseScreenPosition);
            return true;
        }

        private Vector2 GetHudCoinLocalPosition()
        {
            Vector3 hudScreenPosition = new Vector3(
                Mathf.Max(24f, Screen.width - 260f),
                Mathf.Max(24f, Screen.height - 72f),
                Mathf.Abs(_camera.transform.position.z));
            Vector3 hudWorldPosition = _camera.ScreenToWorldPoint(hudScreenPosition);
            return transform.InverseTransformPoint(hudWorldPosition);
        }

        private static void SetCoinPosition(CollectibleCoin coin, Vector2 position)
        {
            coin.Position = position;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float EaseOutBack(float value)
        {
            value = Mathf.Clamp01(value);
            const float overshoot = 1.70158f;
            float shifted = value - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted +
                   overshoot * shifted * shifted;
        }

        private void UpdateVisuals()
        {
            UpdateCombinedRingMesh();

            Vector2 shakenBallPosition =
                _ballPosition + GetFinalBallShakeOffset();
            Vector3 ballPosition = new Vector3(
                shakenBallPosition.x,
                shakenBallPosition.y,
                -0.08f);
            _ballTransform.localPosition = ballPosition;
            _ballGlowTransform.localPosition = ballPosition + Vector3.forward * 0.01f;
            _ballTrailTransform.localPosition = ballPosition + Vector3.forward * 0.02f;
            float ballRevealScale = 1f;
            if (_resetRingRevealActive)
            {
                float revealProgress = Mathf.Clamp01(
                    _resetRingRevealElapsed / Mathf.Max(0.0001f, resetRingRevealDuration));
                ballRevealScale = EaseOutBack(Mathf.Clamp01(revealProgress / 0.32f));
            }
            ballRevealScale *= GetFinalEscapeBallScale();

            float squashStrength =
                !_finalEscapeActive &&
                ballImpactSquashDuration > 0.0001f
                ? Smooth01(_ballImpactSquashRemaining / ballImpactSquashDuration)
                : 0f;
            float baseDiameter = BallRadius * 2f * ballVisualScale * ballRevealScale;
            _ballTransform.localScale = new Vector3(
                baseDiameter * (1f - ballImpactSquash * squashStrength),
                baseDiameter * (1f + ballImpactSquash * 0.72f * squashStrength),
                1f);
            float glowDiameter = BallRadius * 3.45f * ballVisualScale * ballRevealScale;
            glowDiameter *= Mathf.Lerp(
                1f,
                1.52f,
                GetFinalEscapeChargeProgress());
            _ballGlowTransform.localScale = new Vector3(
                glowDiameter * (1f - ballImpactSquash * 0.45f * squashStrength),
                glowDiameter * (1f + ballImpactSquash * 0.5f * squashStrength),
                1f);
            float impactAngle =
                Mathf.Atan2(_ballImpactNormal.y, _ballImpactNormal.x) * Mathf.Rad2Deg;
            Quaternion impactRotation = Quaternion.Euler(0f, 0f, impactAngle);
            _ballTransform.localRotation = impactRotation;
            _ballGlowTransform.localRotation = impactRotation;
            if (_ballTrailRenderer != null)
            {
                float cellWorldScale = Mathf.Max(
                    0.0001f,
                    Mathf.Abs(transform.lossyScale.x));
                _ballTrailRenderer.time = ballTrailDuration;
                _ballTrailRenderer.widthMultiplier =
                    BallRadius * 2f * ballVisualScale * ballTrailWidth * cellWorldScale;
                _ballTrailRenderer.minVertexDistance =
                    BallRadius * 0.35f * cellWorldScale;
                _ballTrailRenderer.emitting =
                    !_resetRingRevealActive &&
                    !_finalEscapeActive;
            }

            if (_cloneBallRenderer != null &&
                _cloneBallGlowRenderer != null &&
                _cloneBallTransform != null &&
                _cloneBallGlowTransform != null)
            {
                bool showClone =
                    _cloneBallActive &&
                    !_finalEscapeActive;
                _cloneBallRenderer.enabled = showClone;
                _cloneBallGlowRenderer.enabled = showClone;
                if (_cloneBallRimRenderer != null)
                {
                    _cloneBallRimRenderer.enabled = showClone;
                }
                if (_cloneBallHighlightRenderer != null)
                {
                    _cloneBallHighlightRenderer.enabled = showClone;
                }
                if (showClone)
                {
                    Vector3 clonePosition = new Vector3(
                        _cloneBallPosition.x,
                        _cloneBallPosition.y,
                        -0.075f);
                    _cloneBallTransform.localPosition = clonePosition;
                    _cloneBallGlowTransform.localPosition =
                        clonePosition + Vector3.forward * 0.01f;
                    if (_cloneBallTrailTransform != null)
                    {
                        _cloneBallTrailTransform.localPosition =
                            clonePosition + Vector3.forward * 0.02f;
                    }

                    float clonePulse =
                        1f +
                        Mathf.Sin(
                            Time.time * 7.5f +
                            (_simulationSeed & 31)) *
                        0.035f;
                    float cloneDiameter =
                        BallRadius *
                        2f *
                        ballVisualScale *
                        ballRevealScale *
                        clonePulse;
                    _cloneBallTransform.localScale =
                        Vector3.one * cloneDiameter;
                    _cloneBallGlowTransform.localScale =
                        Vector3.one *
                        (BallRadius *
                         3.45f *
                         ballVisualScale *
                         ballRevealScale *
                         clonePulse);
                }

                if (_cloneBallTrailRenderer != null)
                {
                    float cellWorldScale = Mathf.Max(
                        0.0001f,
                        Mathf.Abs(transform.lossyScale.x));
                    _cloneBallTrailRenderer.time =
                        ballTrailDuration;
                    _cloneBallTrailRenderer.widthMultiplier =
                        BallRadius *
                        2f *
                        ballVisualScale *
                        ballTrailWidth *
                        cellWorldScale;
                    _cloneBallTrailRenderer.minVertexDistance =
                        BallRadius * 0.35f * cellWorldScale;
                    _cloneBallTrailRenderer.emitting =
                        showClone &&
                        !_resetRingRevealActive;
                }
            }

            if (_pickupRadiusTransform != null && _pickupRadiusRenderer != null)
            {
                bool showPickupRadius = TryGetMouseWorldPosition(out Vector2 mousePosition);
                _pickupRadiusRenderer.enabled = showPickupRadius;
                if (showPickupRadius)
                {
                    _pickupRadiusTransform.position =
                        new Vector3(mousePosition.x, mousePosition.y, -0.2f);
                    _pickupRadiusTransform.localScale = Vector3.one * coinPickupRadius;
                    SetRendererTint(
                        _pickupRadiusRenderer,
                        _pickupRadiusColor);
                }
            }
        }

        private float GetFinalEscapeBallScale()
        {
            if (!_finalEscapeActive)
            {
                return 1f;
            }

            if (_finalEscapeElapsed < finalBallGrowDuration)
            {
                float growT = Smooth01(
                    _finalEscapeElapsed /
                    Mathf.Max(0.0001f, finalBallGrowDuration));
                return Mathf.Lerp(1f, finalBallGrowthScale, growT);
            }

            float shrinkT = Smooth01(
                (_finalEscapeElapsed - finalBallGrowDuration) /
                Mathf.Max(0.0001f, finalBallShrinkDuration));
            return Mathf.Lerp(finalBallGrowthScale, 0f, shrinkT);
        }

        private float GetFinalEscapeChargeProgress()
        {
            if (!_finalEscapeActive || _finalBallExploded)
            {
                return 0f;
            }

            return Smooth01(
                Mathf.Clamp01(
                    _finalEscapeElapsed /
                    Mathf.Max(0.0001f, finalBallGrowDuration)));
        }

        private Vector2 GetFinalBallShakeOffset()
        {
            if (!_finalEscapeActive || _finalBallExploded)
            {
                return Vector2.zero;
            }

            float charge = GetFinalEscapeChargeProgress();
            float shrinkFade = 1f;
            if (_finalEscapeElapsed > finalBallGrowDuration)
            {
                shrinkFade = 1f - Smooth01(
                    (_finalEscapeElapsed - finalBallGrowDuration) /
                    Mathf.Max(
                        0.0001f,
                        finalBallShrinkDuration));
            }

            float amplitude =
                finalBallShakeStrength *
                charge *
                charge *
                shrinkFade;
            float phase =
                (_simulationSeed & 1023) * 0.0137f;
            float time =
                _finalEscapeElapsed *
                finalBallShakeFrequency;
            return new Vector2(
                Mathf.Sin(time + phase) +
                Mathf.Sin(time * 1.91f + phase * 0.7f) * 0.38f,
                Mathf.Cos(time * 1.37f + phase * 1.3f) +
                Mathf.Sin(time * 2.27f + phase) * 0.34f) *
                (amplitude / 1.38f);
        }

        private float GetFinalFragmentAttractionProgress()
        {
            if (!_finalEscapeActive || _finalBallExploded)
            {
                return 0f;
            }

            return Smooth01(
                Mathf.Clamp01(
                    (_finalEscapeElapsed -
                     finalFragmentAttractionDelay) /
                    Mathf.Max(
                        0.0001f,
                        finalFragmentAttractionDuration)));
        }

        private void UpdateCombinedRingMesh()
        {
            if (_combinedRingMesh == null ||
                _combinedRingBaseVertices == null ||
                _combinedRingVertices == null)
            {
                return;
            }

            int verticesPerRing = (_ringMeshSegmentCount + 1) * 2;
            foreach (Ring ring in _rings)
            {
                int vertexEnd = ring.VertexStart + verticesPerRing;
                if (!ring.IsAlive)
                {
                    for (int vertexIndex = ring.VertexStart; vertexIndex < vertexEnd; vertexIndex++)
                    {
                        _combinedRingVertices[vertexIndex] = Vector3.zero;
                    }
                    continue;
                }

                float angle = ring.RotationDegrees * Mathf.Deg2Rad;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);
                float revealScale = RingRevealScale(ring);
                for (int vertexIndex = ring.VertexStart; vertexIndex < vertexEnd; vertexIndex++)
                {
                    Vector3 source = _combinedRingBaseVertices[vertexIndex];
                    _combinedRingVertices[vertexIndex] = new Vector3(
                        (source.x * cosine - source.y * sine) * revealScale,
                        (source.x * sine + source.y * cosine) * revealScale,
                        source.z);
                }
            }

            _combinedRingMesh.SetVertices(
                _combinedRingVertices,
                0,
                _combinedRingVertices.Length,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        }

        private float RingRevealScale(Ring ring)
        {
            if (!_resetRingRevealActive || resetRingRevealDuration <= 0.0001f)
            {
                return 1f;
            }

            float staggerWindow = resetRingRevealDuration * resetRingRevealStagger;
            float ringDuration = Mathf.Max(
                0.0001f,
                resetRingRevealDuration - staggerWindow);
            float ringStart = ring.NormalizedRadius * staggerWindow;
            float ringProgress = Mathf.Clamp01(
                (_resetRingRevealElapsed - ringStart) / ringDuration);
            return Smooth01(ringProgress);
        }

        private void ApplyPalette()
        {
            for (int i = 0; i < _rings.Count; i++)
            {
                float t = _rings.Count > 1 ? i / (float)(_rings.Count - 1) : 0f;
                Color color = _activeColorProfile != null
                    ? _activeColorProfile.RingGradient.Evaluate(t)
                    : t < 0.5f
                        ? Color.Lerp(
                            new Color(1.02f, 0.94f, 0.6f, 1f),
                            new Color(0.5f, 0.86f, 0.38f, 1f),
                            Mathf.SmoothStep(0f, 1f, t * 2f))
                        : Color.Lerp(
                            new Color(0.5f, 0.86f, 0.38f, 1f),
                            new Color(0.08f, 0.7f, 0.82f, 1f),
                            Mathf.SmoothStep(0f, 1f, (t - 0.5f) * 2f));
                Ring ring = _rings[i];
                if (ring == _tintedInnermostRing)
                {
                    color = _ballColor;
                }

                ring.Color = color;
                int verticesPerRing = (_ringMeshSegmentCount + 1) * 2;
                int vertexEnd = ring.VertexStart + verticesPerRing;
                for (int vertexIndex = ring.VertexStart; vertexIndex < vertexEnd; vertexIndex++)
                {
                    _combinedRingColors[vertexIndex] = color;
                }
            }

            if (_combinedRingMesh != null)
            {
                _combinedRingMesh.colors = _combinedRingColors;
            }

            SetRendererTint(_ballRenderer, _ballColor);
            SetRendererTint(_ballGlowRenderer, _ballGlowColor);
            SetRendererTint(
                _ballRimRenderer,
                Color.Lerp(_ballColor, SanctuaryInk, 0.68f));
            SetRendererTint(_ballHighlightRenderer, SanctuaryCream);
            SetRendererTint(_cloneBallRenderer, _ballColor);
            SetRendererTint(_cloneBallGlowRenderer, _ballGlowColor);
            SetRendererTint(
                _cloneBallRimRenderer,
                Color.Lerp(_ballColor, SanctuaryInk, 0.68f));
            SetRendererTint(_cloneBallHighlightRenderer, SanctuaryCream);
            SetRendererTint(
                _combinedRingShadowRenderer,
                new Color(
                    SanctuaryInk.r,
                    SanctuaryInk.g,
                    SanctuaryInk.b,
                    0.34f));
            SetRendererTint(
                _combinedRingHighlightRenderer,
                new Color(1.12f, 1.08f, 0.82f, 0.22f));
            if (_ballTrailRenderer != null)
            {
                _ballTrailRenderer.colorGradient = _activeColorProfile != null
                    ? _activeColorProfile.BallTrailGradient
                    : CreateDefaultBallTrailGradient(_ballColor);
            }
            if (_cloneBallTrailRenderer != null)
            {
                _cloneBallTrailRenderer.colorGradient =
                    _activeColorProfile != null
                        ? _activeColorProfile.BallTrailGradient
                        : CreateDefaultBallTrailGradient(_ballColor);
            }
        }

        private void ApplySelectedColorProfile()
        {
            // The visual overhaul uses one authored world palette so every
            // generated cell reads as part of the same painted sanctuary.
            _activeColorProfile = null;
            _ballColor = SanctuaryCoral;
            _ballGlowColor =
                new Color(1f, 0.75f, 0.32f, 0.34f);
            coinGoldColor = SanctuaryGold;
            _pickupRadiusColor =
                new Color(0.98f, 0.84f, 0.35f, 0.38f);
        }

        public static void RandomizeAllColorProfiles()
        {
            int seed = unchecked(Environment.TickCount + ++_colorRerollSequence * 104729);
            var random = new System.Random(seed);
            RingEscapeSimulation[] simulations =
                FindObjectsByType<RingEscapeSimulation>(FindObjectsSortMode.None);
            foreach (RingEscapeSimulation simulation in simulations)
            {
                if (simulation._isGridRoot)
                {
                    foreach (RingEscapeSimulation cell in simulation._gridCells)
                    {
                        cell.RandomizeColorProfile(random, true);
                    }
                }
                else if (simulation._gridOwner == null)
                {
                    simulation.RandomizeColorProfile(random, true);
                }
            }
        }

        private void RandomizeColorProfile(System.Random random, bool forceDifferent)
        {
            if (colorDatabase == null || colorDatabase.ProfileCount == 0)
            {
                return;
            }

            int nextIndex = random.Next(colorDatabase.ProfileCount);
            if (forceDifferent &&
                colorDatabase.ProfileCount > 1 &&
                nextIndex == colorProfileIndex)
            {
                nextIndex =
                    (nextIndex + 1 + random.Next(colorDatabase.ProfileCount - 1)) %
                    colorDatabase.ProfileCount;
            }

            colorProfileIndex = nextIndex;
            ApplySelectedColorProfile();
            RefreshCoinMeshColors();
            ApplyPalette();
        }

        private void RefreshCoinMeshColors()
        {
            if (_coinMesh == null || _coinBaseColors == null)
            {
                return;
            }

            int verticesPerLayer = CoinMeshSegments + 1;
            Color rimColor = Color.Lerp(coinGoldColor, Color.black, 0.36f);
            Color faceColor = coinGoldColor;
            Color stampColor = Color.Lerp(coinGoldColor, Color.black, 0.13f);
            Color highlightColor = Color.Lerp(coinGoldColor, Color.white, 0.78f);
            for (int vertexIndex = 0; vertexIndex < _coinBaseColors.Length; vertexIndex++)
            {
                _coinBaseColors[vertexIndex] = vertexIndex < verticesPerLayer
                    ? rimColor
                    : vertexIndex < verticesPerLayer * 2
                        ? faceColor
                        : vertexIndex < verticesPerLayer * 3
                            ? stampColor
                            : highlightColor;
            }
            _coinMesh.colors = _coinBaseColors;

            int verticesPerCoin = _coinBaseColors.Length;
            for (int coinIndex = 0; coinIndex < coinPoolCapacity; coinIndex++)
            {
                Array.Copy(
                    _coinBaseColors,
                    0,
                    _coinBatchColors,
                    coinIndex * verticesPerCoin,
                    verticesPerCoin);
            }
            _coinBatchMesh.SetVertexBufferData(
                _coinBatchColors,
                0,
                0,
                _coinBatchColors.Length,
                1,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            _coinColorLayoutDirty = true;
        }

        private static Gradient CreateDefaultBallTrailGradient(Color ballColor)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(
                        Color.Lerp(ballColor, Color.white, 0.2f),
                        0f),
                    new GradientColorKey(ballColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.72f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private void SetRendererTint(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(TintId, color);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private Color GetDisplayedRingColor(Ring ring)
        {
            return ring.Color;
        }

        private static void WriteRingBaseGeometry(
            float apothem,
            float thickness,
            float gapDegrees,
            float gapCenterDegrees,
            int shapeSideCount,
            int segmentCount,
            int vertexStart,
            int triangleStart,
            Vector3[] vertices,
            int[] triangles)
        {
            float start =
                (gapCenterDegrees + gapDegrees * 0.5f) * Mathf.Deg2Rad;
            float arc = (360f - gapDegrees) * Mathf.Deg2Rad;
            float innerApothem = apothem - thickness * 0.5f;
            float outerApothem = apothem + thickness * 0.5f;
            var sampleAngles = new float[segmentCount + 1];
            for (int segment = 0; segment <= segmentCount; segment++)
            {
                sampleAngles[segment] =
                    start + arc * (segment / (float)segmentCount);
            }

            if (shapeSideCount >= 3)
            {
                float sector = Mathf.PI * 2f / shapeSideCount;
                float firstCorner = sector * 0.5f;
                int firstCornerIndex = Mathf.CeilToInt(
                    (start - firstCorner) / sector);
                int lastCornerIndex = Mathf.FloorToInt(
                    (start + arc - firstCorner) / sector);
                for (int cornerIndex = firstCornerIndex;
                     cornerIndex <= lastCornerIndex;
                     cornerIndex++)
                {
                    float cornerAngle = firstCorner + cornerIndex * sector;
                    int sampleIndex = Mathf.Clamp(
                        Mathf.RoundToInt(
                            (cornerAngle - start) / arc * segmentCount),
                        1,
                        segmentCount - 1);
                    sampleAngles[sampleIndex] = cornerAngle;
                }
            }

            for (int segment = 0; segment <= segmentCount; segment++)
            {
                float angle = sampleAngles[segment];
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);
                float innerRadius = ShapeRadiusAtLocalAngle(
                    innerApothem,
                    angle,
                    shapeSideCount);
                float outerRadius = ShapeRadiusAtLocalAngle(
                    outerApothem,
                    angle,
                    shapeSideCount);
                int vertex = vertexStart + segment * 2;
                vertices[vertex] = new Vector3(
                    cosine * innerRadius,
                    sine * innerRadius,
                    0f);
                vertices[vertex + 1] = new Vector3(
                    cosine * outerRadius,
                    sine * outerRadius,
                    0f);

                if (segment == segmentCount)
                {
                    continue;
                }

                int triangle = triangleStart + segment * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 2;
            }
        }

        private static float ShapeRadiusAtLocalAngle(
            float apothem,
            float angleRadians,
            int shapeSideCount)
        {
            if (shapeSideCount < 3)
            {
                return apothem;
            }

            float sector = Mathf.PI * 2f / shapeSideCount;
            float localAngle =
                Mathf.Repeat(angleRadians + sector * 0.5f, sector) -
                sector * 0.5f;
            return apothem /
                Mathf.Max(0.001f, Mathf.Cos(localAngle));
        }

        private static float ShapePerimeter(
            float apothem,
            int shapeSideCount)
        {
            return shapeSideCount >= 3
                ? shapeSideCount * 2f * apothem *
                  Mathf.Tan(Mathf.PI / shapeSideCount)
                : Mathf.PI * 2f * apothem;
        }

        private static Mesh CreateArcMesh(float radius, float thickness, float gapDegrees, int segmentCount)
        {
            float start = gapDegrees * 0.5f * Mathf.Deg2Rad;
            float arc = (360f - gapDegrees) * Mathf.Deg2Rad;
            float inner = radius - thickness * 0.5f;
            float outer = radius + thickness * 0.5f;

            var vertices = new Vector3[(segmentCount + 1) * 2];
            var colors = new Color[vertices.Length];
            var triangles = new int[segmentCount * 6];

            for (int i = 0; i <= segmentCount; i++)
            {
                float angle = start + arc * (i / (float)segmentCount);
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);
                vertices[i * 2] = new Vector3(cosine * inner, sine * inner, 0f);
                vertices[i * 2 + 1] = new Vector3(cosine * outer, sine * outer, 0f);
                colors[i * 2] = Color.white;
                colors[i * 2 + 1] = Color.white;

                if (i == segmentCount)
                {
                    continue;
                }

                int triangle = i * 6;
                int vertex = i * 2;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 2;
            }

            var mesh = new Mesh { name = $"Ring Arc {radius:0.000}" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateCircleMesh(int segmentCount)
        {
            var vertices = new Vector3[segmentCount + 1];
            var colors = new Color[vertices.Length];
            var triangles = new int[segmentCount * 3];
            vertices[0] = Vector3.zero;
            colors[0] = Color.white;

            for (int i = 0; i < segmentCount; i++)
            {
                float angle = i / (float)segmentCount * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f);
                colors[i + 1] = Color.white;

                int triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i + 1;
                triangles[triangle + 2] = (i + 1) % segmentCount + 1;
            }

            var mesh = new Mesh { name = $"Circle {segmentCount}" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateLayeredCoinMesh(Color goldColor)
        {
            const int segmentCount = CoinMeshSegments;
            const int layerCount = 4;
            int verticesPerLayer = segmentCount + 1;
            var vertices = new Vector3[layerCount * verticesPerLayer];
            var colors = new Color[vertices.Length];
            var triangles = new int[layerCount * segmentCount * 3];

            WriteCoinDisk(
                0,
                Vector2.zero,
                0.5f,
                0f,
                Color.Lerp(goldColor, Color.black, 0.36f),
                segmentCount,
                vertices,
                colors,
                triangles);
            WriteCoinDisk(
                1,
                Vector2.zero,
                0.43f,
                -0.01f,
                goldColor,
                segmentCount,
                vertices,
                colors,
                triangles);
            WriteCoinDisk(
                2,
                Vector2.zero,
                0.24f,
                -0.02f,
                Color.Lerp(goldColor, Color.black, 0.13f),
                segmentCount,
                vertices,
                colors,
                triangles);
            WriteCoinDisk(
                3,
                new Vector2(-0.1f, 0.1f),
                0.075f,
                -0.03f,
                Color.Lerp(goldColor, Color.white, 0.78f),
                segmentCount,
                vertices,
                colors,
                triangles);

            var mesh = new Mesh { name = "Layered Gold Coin" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void WriteCoinDisk(
            int layerIndex,
            Vector2 center,
            float radius,
            float z,
            Color color,
            int segmentCount,
            Vector3[] vertices,
            Color[] colors,
            int[] triangles)
        {
            int verticesPerLayer = segmentCount + 1;
            int vertexStart = layerIndex * verticesPerLayer;
            int triangleStart = layerIndex * segmentCount * 3;
            vertices[vertexStart] = new Vector3(center.x, center.y, z);
            colors[vertexStart] = color;

            for (int segment = 0; segment < segmentCount; segment++)
            {
                float angle = segment / (float)segmentCount * Mathf.PI * 2f;
                vertices[vertexStart + segment + 1] = new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius,
                    z);
                colors[vertexStart + segment + 1] = color;

                int triangle = triangleStart + segment * 3;
                triangles[triangle] = vertexStart;
                triangles[triangle + 1] = vertexStart + segment + 1;
                triangles[triangle + 2] =
                    vertexStart + ((segment + 1) % segmentCount) + 1;
            }
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in _runtimeMeshes)
            {
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }

            foreach (Material material in _runtimeMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            foreach (Sprite sprite in _runtimeSprites)
            {
                if (sprite != null)
                {
                    Destroy(sprite);
                }
            }

            foreach (Texture2D texture in _runtimeTextures)
            {
                if (texture != null)
                {
                    Destroy(texture);
                }
            }
        }
    }
}
