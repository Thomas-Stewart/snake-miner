using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrillSnake
{
    /// <summary>
    /// Runtime-built industrial mine presentation with interchangeable PNG and
    /// texture-free procedural cel art passes.
    /// </summary>
    public sealed class DrillSnakeWorldView : MonoBehaviour
    {
        private const int MovementPathSampleCount = 16;

        private sealed class SegmentView
        {
            public GameObject Root;
            public Renderer[] HeatRenderers;
            public Vector3 PathStart;
            public Vector3 PathControlA;
            public Vector3 PathControlB;
            public Vector3 TargetPosition;
            public float[] PathDistances;
            public float PathLength;
            public float MovementStart;
            public float MovementDuration;
            public Vector3 PreviousDirection;
            public bool HasPreviousDirection;
            public bool PathInitialized;
            public Transform Turret;
        }

        private readonly Dictionary<Vector2Int, GameObject> _solidCells = new();
        private readonly Dictionary<Vector2Int, GameObject> _orePickupViews = new();
        private readonly Dictionary<Vector2Int, GameObject> _drillPowerupViews = new();
        private readonly HashSet<Vector2Int> _animatingPickupCells = new();
        private readonly List<SegmentView> _segmentViews = new();

        private DrillSnakeMap _map;
        private DrillSnakeArtMode _artMode;
        private Transform _worldRoot;
        private GameObject _gridOverlay;
        private GameObject _levelDesignOverlay;
        private bool _gridVisible;
        private bool _levelDesignOverlayVisible;
        private Material _floorMaterial;
        private Material _softRockMaterial;
        private Material _bedrockMaterial;
        private Material _refineryMaterial;
        private Material _dockMaterial;
        private Material _refineryDarkMaterial;
        private Material _outlineMaterial;
        private Material _steelMaterial;
        private Material _steelLightMaterial;
        private Material _machineAccentMaterial;
        private Material _rubberMaterial;
        private Material _commonOreMaterial;
        private Material _rareOreMaterial;
        private Material _veryRareOreMaterial;
        private Material _lampMaterial;
        private Material _gridMaterial;
        private Material _standardRouteMaterial;
        private Material _safeRouteMaterial;
        private Material _riskyRouteMaterial;
        private Material _refineryNodeMaterial;
        private Material _commonZoneMaterial;
        private Material _rareZoneMaterial;
        private Material _veryRareZoneMaterial;
        private Material _validationFailureMaterial;
        private Material _heatParticleMaterial;
        private Sprite _headSprite;
        private Sprite _chassisSprite;
        private Sprite _cargoSprite;
        private Sprite _refinerySprite;
        private Sprite _commonOreSprite;
        private Sprite _rareOreSprite;
        private Sprite _veryRareOreSprite;
        private Sprite _lampSprite;
        private Mesh _beveledCubeMesh;
        private Mesh _crystalMesh;
        private Mesh _drillConeMesh;
        private GameObject _drillAura;
        private bool _drillAuraActive;
        private float _heatTint;
        private float _targetHeatTint;
        private float _appliedHeatTint = -1f;
        private ParticleSystem _steamParticles;
        private ParticleSystem _smokeParticles;
        private MaterialPropertyBlock _heatTintProperties;
        private static readonly int HeatTintColorId =
            Shader.PropertyToID("_HeatTintColor");
        private static readonly int HeatTintStrengthId =
            Shader.PropertyToID("_HeatTintStrength");
        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");
        private static readonly Color HotSnakeColor =
            new(1f, 0.08f, 0.025f, 1f);

        public bool GridVisible => _gridVisible;

        public bool LevelDesignOverlayVisible => _levelDesignOverlayVisible;

        public DrillSnakeArtMode ArtMode => _artMode;

        public bool TryGetHeadVisualPosition(out Vector3 position)
        {
            if (_segmentViews.Count > 0 && _segmentViews[0].Root != null)
            {
                position = _segmentViews[0].Root.transform.position;
                return true;
            }

            position = default;
            return false;
        }

        public void BuildWorld(
            DrillSnakeMap map,
            DrillSnakeArtMode artMode = DrillSnakeArtMode.IllustratedPng)
        {
            _map = map;
            var materialsNeedRefresh =
                _floorMaterial == null ||
                _artMode != artMode;
            _artMode = artMode;
            if (_worldRoot != null)
            {
                _worldRoot.gameObject.SetActive(false);
                Destroy(_worldRoot.gameObject);
            }

            _solidCells.Clear();
            _orePickupViews.Clear();
            _drillPowerupViews.Clear();
            _animatingPickupCells.Clear();
            _segmentViews.Clear();
            _drillAura = null;
            _drillAuraActive = false;
            _steamParticles = null;
            _smokeParticles = null;
            if (materialsNeedRefresh)
            {
                ReleaseMaterials();
            }

            CreateMaterials();

            _worldRoot = new GameObject("Generated Drill Snake World").transform;
            _worldRoot.SetParent(transform, false);

            CreateBaseFloor();
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    CreateCellVisual(new Vector2Int(x, y), map.GetCell(new Vector2Int(x, y)));
                }
            }

            if (_artMode == DrillSnakeArtMode.ProceduralCel)
            {
                CreateProceduralFloorDebris();
            }

            CreateRefinerySetDressing();
            CreateMineLamps();
            CreateGridOverlay();
            CreateLevelDesignOverlay();
        }

        public void SetGridVisible(bool visible)
        {
            _gridVisible = visible;
            if (_gridOverlay != null)
            {
                _gridOverlay.SetActive(visible);
            }
        }

        public void ToggleGrid()
        {
            SetGridVisible(!GridVisible);
        }

        public void SetLevelDesignOverlayVisible(bool visible)
        {
            _levelDesignOverlayVisible = visible;
            if (_levelDesignOverlay != null)
            {
                _levelDesignOverlay.SetActive(visible);
            }
        }

        public void ToggleLevelDesignOverlay()
        {
            SetLevelDesignOverlayVisible(!LevelDesignOverlayVisible);
        }

        public void SetDrillPowerActive(bool active)
        {
            _drillAuraActive = active;
            if (_drillAura != null)
            {
                _drillAura.SetActive(active);
            }
        }

        public void SetHeatTint(float normalizedHeat)
        {
            _targetHeatTint = Mathf.Clamp01(normalizedHeat);
        }

        public void SyncCollectibles(DrillSnakeSimulation simulation)
        {
            var activeOreCells = new HashSet<Vector2Int>();
            foreach (var pair in simulation.OrePickups)
            {
                activeOreCells.Add(pair.Key);
                if (!_orePickupViews.ContainsKey(pair.Key))
                {
                    _orePickupViews[pair.Key] = CreateOrePickupView(pair.Value);
                }
            }

            RemoveMissingCollectibleViews(_orePickupViews, activeOreCells);

            var activePowerupCells = new HashSet<Vector2Int>();
            foreach (var cell in simulation.DrillPowerups)
            {
                activePowerupCells.Add(cell);
                if (!_drillPowerupViews.ContainsKey(cell))
                {
                    _drillPowerupViews[cell] = CreateDrillPowerupView(cell);
                }
            }

            RemoveMissingCollectibleViews(
                _drillPowerupViews,
                activePowerupCells);
        }

        public void PlayTurretShot(
            DrillSnakeTurretResult result,
            float travelSeconds,
            float projectileSize)
        {
            if (!result.Fired || _segmentViews.Count == 0)
            {
                return;
            }

            var turret = _segmentViews[0].Turret;
            if (turret != null)
            {
                var direction = GridToWorld(result.Target) -
                                GridToWorld(result.Origin);
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.001f)
                {
                    turret.rotation = Quaternion.LookRotation(
                        direction,
                        Vector3.up);
                }
            }

            StartCoroutine(AnimateTurretShot(
                result,
                travelSeconds,
                projectileSize));
        }

        public void PlayOreScatter(
            Vector2Int source,
            IReadOnlyList<DrillSnakeOrePickup> pickups)
        {
            foreach (var pickup in pickups)
            {
                if (_orePickupViews.ContainsKey(pickup.Cell))
                {
                    StartCoroutine(AnimateOreScatter(source, pickup.Cell));
                }
            }
        }

        public void PlayOreCollection(
            Vector2Int source,
            DrillSnakeOreType oreType)
        {
            if (!_orePickupViews.Remove(source, out var pickupView) ||
                pickupView == null)
            {
                pickupView = CreateOrePickupView(
                    new DrillSnakeOrePickup(source, oreType, 0));
            }

            _animatingPickupCells.Remove(source);
            StartCoroutine(AnimateOreCollection(pickupView, oreType));
        }

        public void RemoveDrilledCell(Vector2Int cell)
        {
            if (!_solidCells.Remove(cell, out var cellObject) || cellObject == null)
            {
                return;
            }

            StartCoroutine(ShrinkAndDestroy(cellObject, 0.09f));
        }

        public void SyncSnake(DrillSnakeSimulation simulation, float movementDuration)
        {
            while (_segmentViews.Count > simulation.Segments.Count)
            {
                var lastIndex = _segmentViews.Count - 1;
                if (_segmentViews[lastIndex].Root != null)
                {
                    Destroy(_segmentViews[lastIndex].Root);
                }

                _segmentViews.RemoveAt(lastIndex);
                _appliedHeatTint = -1f;
            }

            while (_segmentViews.Count < simulation.Segments.Count)
            {
                var index = _segmentViews.Count;
                var view = CreateSegmentView(index, simulation);
                var position = GridToWorld(simulation.Segments[index], SegmentHeight(index));
                view.Root.transform.position = position;
                view.PathStart = position;
                view.PathControlA = position;
                view.PathControlB = position;
                view.TargetPosition = position;
                if (index == 0)
                {
                    CreateHeatVentEffects(view.Root.transform);
                }

                _segmentViews.Add(view);
                _appliedHeatTint = -1f;
            }

            var now = Time.time;
            for (var i = 0; i < _segmentViews.Count; i++)
            {
                var view = _segmentViews[i];
                var target = GridToWorld(simulation.Segments[i], SegmentHeight(i));
                if (movementDuration <= 0f)
                {
                    var initialDirection = i == 0
                        ? new Vector3(
                            simulation.Direction.x,
                            0f,
                            simulation.Direction.y)
                        : SegmentDirection(simulation, i);
                    SnapMovementPath(view, target, initialDirection, now);
                    continue;
                }

                BeginMovementPath(view, target, movementDuration, now);
            }

        }

        public IEnumerator AnimateTailConsumption(float duration)
        {
            if (_segmentViews.Count <= DrillSnakeSimulation.MinimumSegmentCount)
            {
                yield break;
            }

            var tailIndex = _segmentViews.Count - 1;
            var tail = _segmentViews[tailIndex];
            var root = tail.Root;
            var startScale = root.transform.localScale;
            var startPosition = root.transform.position;
            var targetPosition = GridToWorld(_map.Center, 0.65f);
            var elapsed = 0f;

            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                var eased = t * t * (3f - 2f * t);
                root.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
                root.transform.position = Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    eased * 0.18f);
                root.transform.Rotate(Vector3.up, 540f * Time.deltaTime, Space.World);
                yield return null;
            }

            if (root != null)
            {
                Destroy(root);
            }

            _segmentViews.RemoveAt(tailIndex);
        }

        public static Vector3 GridToWorld(Vector2Int cell, float height = 0f)
        {
            var center = DrillSnakeMap.PrototypeSize / 2;
            return new Vector3(cell.x - center, height, cell.y - center);
        }

        private void Update()
        {
            var now = Time.time;
            _heatTint = Mathf.MoveTowards(
                _heatTint,
                _targetHeatTint,
                Time.deltaTime * 1.7f);
            if (Mathf.Abs(_heatTint - _appliedHeatTint) > 0.001f)
            {
                ApplyHeatTintToSnake(_heatTint);
                UpdateHeatVentEffects(_heatTint);
                _appliedHeatTint = _heatTint;
            }

            foreach (var view in _segmentViews)
            {
                if (view.Root == null || view.MovementDuration <= 0f)
                {
                    continue;
                }

                ApplyMovementPath(view, now);
            }

            if (_drillAuraActive && _drillAura != null)
            {
                var pulse = 0.92f + Mathf.Sin(now * 12f) * 0.1f;
                _drillAura.transform.localScale =
                    new Vector3(pulse, 0.05f, pulse);
                _drillAura.transform.Rotate(
                    Vector3.up,
                    110f * Time.deltaTime,
                    Space.Self);
            }

            foreach (var pair in _orePickupViews)
            {
                if (pair.Value == null ||
                    _animatingPickupCells.Contains(pair.Key))
                {
                    continue;
                }

                pair.Value.transform.position = GridToWorld(
                    pair.Key,
                    0.23f + Mathf.Sin(now * 5f + pair.Key.x) * 0.05f);
                pair.Value.transform.Rotate(
                    Vector3.up,
                    80f * Time.deltaTime,
                    Space.World);
            }

            foreach (var pair in _drillPowerupViews)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                pair.Value.transform.position = GridToWorld(
                    pair.Key,
                    0.32f + Mathf.Sin(now * 4f + pair.Key.y) * 0.08f);
                pair.Value.transform.Rotate(
                    Vector3.up,
                    55f * Time.deltaTime,
                    Space.World);
            }
        }

        private void ApplyHeatTintToSnake(float strength)
        {
            // Unity can preserve an existing MonoBehaviour instance across a
            // script hot reload. Newly added reference fields are null on that
            // preserved instance even when they have a field initializer.
            _heatTintProperties ??= new MaterialPropertyBlock();

            foreach (var view in _segmentViews)
            {
                if (view.Root == null)
                {
                    continue;
                }

                view.HeatRenderers ??=
                    view.Root.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in view.HeatRenderers)
                {
                    if (renderer == null)
                    {
                        continue;
                    }

                    if (renderer is ParticleSystemRenderer)
                    {
                        continue;
                    }

                    if (renderer is SpriteRenderer spriteRenderer)
                    {
                        spriteRenderer.color = Color.Lerp(
                            Color.white,
                            new Color(1f, 0.28f, 0.2f, 1f),
                            strength * 0.82f);
                        continue;
                    }

                    var material = renderer.sharedMaterial;
                    if (material == null)
                    {
                        continue;
                    }

                    _heatTintProperties.Clear();
                    renderer.GetPropertyBlock(_heatTintProperties);
                    if (material.HasProperty(HeatTintStrengthId))
                    {
                        _heatTintProperties.SetColor(
                            HeatTintColorId,
                            HotSnakeColor);
                        _heatTintProperties.SetFloat(
                            HeatTintStrengthId,
                            strength);
                    }
                    else if (material.HasProperty(BaseColorId))
                    {
                        var baseColor = material.GetColor(BaseColorId);
                        var heatedColor = new Color(
                            Mathf.Max(0.78f, baseColor.r),
                            baseColor.g * 0.22f,
                            baseColor.b * 0.16f,
                            baseColor.a);
                        _heatTintProperties.SetColor(
                            BaseColorId,
                            Color.Lerp(
                                baseColor,
                                heatedColor,
                                strength * 0.78f));
                    }

                    renderer.SetPropertyBlock(_heatTintProperties);
                }
            }
        }

        private void CreateHeatVentEffects(Transform head)
        {
            if (head == null)
            {
                return;
            }

            _steamParticles = CreateHeatVentParticleSystem(
                head,
                "High Heat Steam",
                new Vector3(-0.18f, 0.48f, -0.12f),
                new Color(0.72f, 0.88f, 1f, 0.72f),
                0.95f,
                0.72f,
                0.2f,
                0.28f);
            _smokeParticles = CreateHeatVentParticleSystem(
                head,
                "Critical Heat Smoke",
                new Vector3(0.18f, 0.46f, -0.16f),
                new Color(0.13f, 0.105f, 0.095f, 0.78f),
                1.55f,
                0.48f,
                0.3f,
                0.42f);
            UpdateHeatVentEffects(_heatTint);
        }

        private ParticleSystem CreateHeatVentParticleSystem(
            Transform parent,
            string name,
            Vector3 localPosition,
            Color startColor,
            float lifetime,
            float speed,
            float minimumSize,
            float maximumSize)
        {
            var effect = new GameObject(name);
            // ParticleSystem begins playing immediately when added to an
            // active object. Configure it while inactive so duration and the
            // other startup-only properties can be changed without Unity
            // issuing "system is still playing" warnings.
            effect.SetActive(false);
            effect.transform.SetParent(parent, false);
            effect.transform.localPosition = localPosition;
            effect.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var particles = effect.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                lifetime * 0.78f,
                lifetime * 1.22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                speed * 0.72f,
                speed * 1.18f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                minimumSize,
                maximumSize);
            main.startColor = startColor;
            main.maxParticles = 48;

            var emission = particles.emission;
            emission.rateOverTime = 0f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16f;
            shape.radius = 0.09f;
            shape.radiusThickness = 1f;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.88f, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color =
                new ParticleSystem.MinMaxGradient(fade);

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.45f),
                    new Keyframe(0.18f, 0.8f),
                    new Keyframe(1f, 1.75f)));

            var noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = 0.18f;
            noise.frequency = 0.42f;
            noise.scrollSpeed = 0.3f;

            var particleRenderer =
                effect.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = 30;
            particleRenderer.sharedMaterial = GetHeatParticleMaterial();
            effect.SetActive(true);
            return particles;
        }

        private void UpdateHeatVentEffects(float heatRatio)
        {
            SetHeatVentEmission(
                _steamParticles,
                Mathf.InverseLerp(0.7f, 1f, heatRatio) * 17f);
            SetHeatVentEmission(
                _smokeParticles,
                Mathf.InverseLerp(0.86f, 1f, heatRatio) * 10f);
        }

        private static void SetHeatVentEmission(
            ParticleSystem particles,
            float rate)
        {
            if (particles == null)
            {
                return;
            }

            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(0f, rate);
            if (rate > 0.01f)
            {
                if (!particles.isPlaying)
                {
                    particles.Play();
                }
            }
            else if (particles.isPlaying)
            {
                particles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private Material GetHeatParticleMaterial()
        {
            if (_heatParticleMaterial != null)
            {
                return _heatParticleMaterial;
            }

            var shader = Shader.Find(
                "Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            _heatParticleMaterial = new Material(shader)
            {
                name = "Heat Steam and Smoke Particles"
            };
            if (_heatParticleMaterial.HasProperty(BaseColorId))
            {
                _heatParticleMaterial.SetColor(BaseColorId, Color.white);
            }

            return _heatParticleMaterial;
        }

        private static Vector3 SegmentDirection(
            DrillSnakeSimulation simulation,
            int index)
        {
            if (index <= 0 || index >= simulation.Segments.Count)
            {
                return Vector3.forward;
            }

            var delta =
                simulation.Segments[index - 1] -
                simulation.Segments[index];
            return new Vector3(delta.x, 0f, delta.y).normalized;
        }

        private static void SnapMovementPath(
            SegmentView view,
            Vector3 position,
            Vector3 direction,
            float now)
        {
            view.PathStart = position;
            view.PathControlA = position;
            view.PathControlB = position;
            view.TargetPosition = position;
            view.PathLength = 0f;
            view.MovementStart = now;
            view.MovementDuration = 0f;
            view.PathInitialized = true;
            if (direction.sqrMagnitude > 0.001f)
            {
                view.PreviousDirection = direction.normalized;
                view.HasPreviousDirection = true;
                view.Root.transform.SetPositionAndRotation(
                    position,
                    Quaternion.LookRotation(direction, Vector3.up));
            }
            else
            {
                view.Root.transform.position = position;
            }
        }

        private static void BeginMovementPath(
            SegmentView view,
            Vector3 target,
            float duration,
            float now)
        {
            ApplyMovementPath(view, now);

            var start = view.Root.transform.position;
            var delta = target - start;
            delta.y = 0f;
            var distance = delta.magnitude;
            view.PathStart = start;
            view.TargetPosition = target;
            view.MovementStart = now;
            view.MovementDuration = Mathf.Max(0.001f, duration);
            view.PathInitialized = true;
            if (distance <= 0.0001f)
            {
                view.PathControlA = start;
                view.PathControlB = target;
                view.PathLength = 0f;
                return;
            }

            var direction = delta / distance;
            var isTurn =
                view.HasPreviousDirection &&
                Vector3.Dot(view.PreviousDirection, direction) < 0.999f;
            if (isTurn)
            {
                var cornerRadius = Mathf.Min(0.2f, distance * 0.2f);
                view.PathControlA =
                    start + view.PreviousDirection * cornerRadius;
                view.PathControlB =
                    target - direction * cornerRadius;
            }
            else
            {
                view.PathControlA =
                    Vector3.LerpUnclamped(start, target, 1f / 3f);
                view.PathControlB =
                    Vector3.LerpUnclamped(start, target, 2f / 3f);
            }

            view.PreviousDirection = direction;
            view.HasPreviousDirection = true;
            BuildMovementDistanceTable(view);
        }

        private static void BuildMovementDistanceTable(SegmentView view)
        {
            view.PathDistances ??= new float[MovementPathSampleCount + 1];
            view.PathDistances[0] = 0f;
            var previous = view.PathStart;
            var total = 0f;
            for (var sample = 1;
                 sample <= MovementPathSampleCount;
                 sample++)
            {
                var curveTime = sample / (float)MovementPathSampleCount;
                var point = EvaluateMovementCurve(view, curveTime);
                total += Vector3.Distance(previous, point);
                view.PathDistances[sample] = total;
                previous = point;
            }

            view.PathLength = total;
        }

        private static void ApplyMovementPath(
            SegmentView view,
            float now)
        {
            if (view.Root == null)
            {
                return;
            }

            if (!view.PathInitialized)
            {
                SnapMovementPath(
                    view,
                    view.Root.transform.position,
                    view.Root.transform.forward,
                    now);
                return;
            }

            if (view.MovementDuration <= 0f)
            {
                view.Root.transform.position = view.TargetPosition;
                return;
            }

            var distanceProgress = Mathf.Clamp01(
                (now - view.MovementStart) / view.MovementDuration);
            var curveTime = DistanceProgressToCurveTime(
                view,
                distanceProgress);
            var position = EvaluateMovementCurve(view, curveTime);
            var tangent = EvaluateMovementTangent(view, curveTime);
            tangent.y = 0f;
            var rotation = tangent.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(tangent, Vector3.up)
                : view.Root.transform.rotation;
            view.Root.transform.SetPositionAndRotation(position, rotation);
        }

        private static float DistanceProgressToCurveTime(
            SegmentView view,
            float distanceProgress)
        {
            if (view.PathLength <= 0.0001f ||
                view.PathDistances == null)
            {
                return distanceProgress;
            }

            var targetDistance = distanceProgress * view.PathLength;
            for (var sample = 1;
                 sample <= MovementPathSampleCount;
                 sample++)
            {
                if (view.PathDistances[sample] < targetDistance)
                {
                    continue;
                }

                var previousDistance = view.PathDistances[sample - 1];
                var sampleLength =
                    view.PathDistances[sample] - previousDistance;
                var sampleProgress = sampleLength > 0.0001f
                    ? (targetDistance - previousDistance) / sampleLength
                    : 0f;
                return (sample - 1f + sampleProgress) /
                       MovementPathSampleCount;
            }

            return 1f;
        }

        private static Vector3 EvaluateMovementCurve(
            SegmentView view,
            float curveTime)
        {
            var inverse = 1f - curveTime;
            return inverse * inverse * inverse * view.PathStart +
                   3f * inverse * inverse * curveTime *
                   view.PathControlA +
                   3f * inverse * curveTime * curveTime *
                   view.PathControlB +
                   curveTime * curveTime * curveTime *
                   view.TargetPosition;
        }

        private static Vector3 EvaluateMovementTangent(
            SegmentView view,
            float curveTime)
        {
            var inverse = 1f - curveTime;
            return 3f * inverse * inverse *
                   (view.PathControlA - view.PathStart) +
                   6f * inverse * curveTime *
                   (view.PathControlB - view.PathControlA) +
                   3f * curveTime * curveTime *
                   (view.TargetPosition - view.PathControlB);
        }

        private void CreateMaterials()
        {
            if (_artMode == DrillSnakeArtMode.IllustratedPng)
            {
                CreateSpriteAssets();
            }

            if (_floorMaterial != null)
            {
                return;
            }

            if (_artMode == DrillSnakeArtMode.ProceduralCel)
            {
                _floorMaterial = CreateCelMaterial(
                    "Cel Slate Floor",
                    new Color(0.135f, 0.145f, 0.165f),
                    new Color(0.026f, 0.03f, 0.04f),
                    new Color(0.24f, 0.245f, 0.255f),
                    1.35f,
                    0.18f,
                    stoneSurface: 0.3f);
                _softRockMaterial = CreateCelMaterial(
                    "Cel Drillable Sandstone",
                    new Color(0.49f, 0.31f, 0.18f),
                    new Color(0.105f, 0.055f, 0.033f),
                    new Color(0.72f, 0.48f, 0.27f),
                    3.8f,
                    0.3f,
                    stoneSurface: 1f);
                _bedrockMaterial = CreateCelMaterial(
                    "Cel Basalt Bedrock",
                    new Color(0.285f, 0.385f, 0.59f),
                    new Color(0.035f, 0.055f, 0.105f),
                    new Color(0.48f, 0.61f, 0.84f),
                    3.2f,
                    0.32f,
                    stoneSurface: 1f);
                _refineryMaterial = CreateCelMaterial(
                    "Cel Refinery Deck",
                    new Color(0.29f, 0.32f, 0.33f),
                    new Color(0.075f, 0.085f, 0.09f),
                    new Color(0.5f, 0.37f, 0.14f),
                    2.25f,
                    0.18f);
                _dockMaterial = CreateCelMaterial(
                    "Cel Safety Yellow",
                    new Color(0.92f, 0.54f, 0.08f),
                    new Color(0.31f, 0.13f, 0.025f),
                    new Color(1f, 0.77f, 0.18f),
                    5.8f,
                    0.34f,
                    new Color(0.2f, 0.07f, 0.005f));
                _refineryDarkMaterial = CreateCelMaterial(
                    "Cel Dark Steel",
                    new Color(0.105f, 0.125f, 0.14f),
                    new Color(0.018f, 0.025f, 0.032f),
                    new Color(0.24f, 0.29f, 0.31f),
                    7f,
                    0.12f);
                _outlineMaterial = CreateCelMaterial(
                    "Ink Outline",
                    new Color(0.018f, 0.022f, 0.026f),
                    new Color(0.005f, 0.007f, 0.009f),
                    new Color(0.025f, 0.03f, 0.034f),
                    1f,
                    0f);
                _steelMaterial = CreateCelMaterial(
                    "Cel Gunmetal",
                    new Color(0.3f, 0.34f, 0.37f),
                    new Color(0.04f, 0.05f, 0.06f),
                    new Color(0.52f, 0.57f, 0.59f),
                    6f,
                    0.08f);
                _steelLightMaterial = CreateCelMaterial(
                    "Cel Silver",
                    new Color(0.56f, 0.62f, 0.63f),
                    new Color(0.14f, 0.18f, 0.19f),
                    new Color(0.78f, 0.82f, 0.79f),
                    8f,
                    0.12f);
                _machineAccentMaterial = CreateCelMaterial(
                    "Cel Machine Orange",
                    new Color(1f, 0.34f, 0.035f),
                    new Color(0.23f, 0.035f, 0.008f),
                    new Color(1f, 0.7f, 0.09f),
                    4f,
                    0.08f,
                    new Color(0.34f, 0.055f, 0.002f));
                _rubberMaterial = CreateCelMaterial(
                    "Cel Track Rubber",
                    new Color(0.055f, 0.065f, 0.07f),
                    new Color(0.008f, 0.011f, 0.014f),
                    new Color(0.14f, 0.16f, 0.17f),
                    9f,
                    0.08f);
                _commonOreMaterial = CreateCelMaterial(
                    "Cel Copper Ore",
                    new Color(1f, 0.34f, 0.035f),
                    new Color(0.28f, 0.045f, 0.005f),
                    new Color(1f, 0.78f, 0.12f),
                    7f,
                    0.24f,
                    new Color(0.5f, 0.08f, 0.003f));
                _rareOreMaterial = CreateCelMaterial(
                    "Cel Cobalt Ore",
                    new Color(0.08f, 0.74f, 1f),
                    new Color(0.018f, 0.14f, 0.34f),
                    new Color(0.46f, 1f, 1f),
                    7f,
                    0.24f,
                    new Color(0.025f, 0.38f, 0.95f));
                _veryRareOreMaterial = CreateCelMaterial(
                    "Cel Plasma Ore",
                    new Color(0.95f, 0.12f, 0.72f),
                    new Color(0.25f, 0.012f, 0.21f),
                    new Color(1f, 0.48f, 0.95f),
                    7f,
                    0.25f,
                    new Color(0.72f, 0.025f, 0.56f));
                _lampMaterial = CreateCelMaterial(
                    "Cel Lamp Glow",
                    new Color(1f, 0.62f, 0.08f),
                    new Color(0.4f, 0.12f, 0.005f),
                    new Color(1f, 0.9f, 0.36f),
                    4f,
                    0.16f,
                    new Color(1.1f, 0.24f, 0.01f));
            }
            else
            {
                _floorMaterial = CreateMaterial(
                    "Painted Mine Floor",
                    new Color(0.94f, 0.92f, 0.9f),
                    0.16f,
                    0.03f,
                    null,
                    Resources.Load<Texture2D>("Art/DrillSnakeMineFloor"),
                    new Vector2(15f, 10f));
                _softRockMaterial = CreateMaterial(
                    "Drillable Sedimentary Rock",
                    new Color(0.82f, 0.72f, 0.62f),
                    0.12f,
                    0f,
                    null,
                    Resources.Load<Texture2D>("Art/DrillSnakeSoftRock"),
                    new Vector2(0.18f, 0.18f));
                _bedrockMaterial = CreateMaterial(
                    "Blue Black Bedrock",
                    new Color(0.68f, 0.75f, 0.86f),
                    0.22f,
                    0.02f,
                    null,
                    Resources.Load<Texture2D>("Art/DrillSnakeBedrock"),
                    new Vector2(0.18f, 0.18f));
                _refineryMaterial = CreateMaterial(
                    "Refinery Floor",
                    new Color(0.34f, 0.35f, 0.35f),
                    0.52f,
                    0.72f);
                _dockMaterial = CreateMaterial(
                    "Refinery Dock",
                    new Color(0.9f, 0.5f, 0.06f),
                    0.75f,
                    0.45f,
                    new Color(0.72f, 0.22f, 0.015f));
                _refineryDarkMaterial = CreateMaterial(
                    "Refinery Dark Steel",
                    new Color(0.09f, 0.1f, 0.105f),
                    0.42f,
                    0.78f);
            }
            _gridMaterial = CreateMaterial(
                "Grid",
                new Color(0.1f, 0.7f, 0.72f, 0.36f),
                0f,
                0f,
                new Color(0.025f, 0.18f, 0.2f));
            _standardRouteMaterial = CreateMaterial(
                "Standard Graph Route",
                new Color(0.82f, 0.9f, 0.94f),
                0f,
                0f,
                new Color(0.25f, 0.32f, 0.35f));
            _safeRouteMaterial = CreateMaterial(
                "Safe Long Route",
                new Color(0.1f, 0.88f, 1f),
                0f,
                0f,
                new Color(0.02f, 0.48f, 0.7f));
            _riskyRouteMaterial = CreateMaterial(
                "Risky Short Route",
                new Color(1f, 0.42f, 0.08f),
                0f,
                0f,
                new Color(0.65f, 0.12f, 0.01f));
            _refineryNodeMaterial = CreateMaterial(
                "Refinery Graph Node",
                new Color(0.2f, 1f, 0.9f),
                0.2f,
                0.1f,
                new Color(0.05f, 0.55f, 0.5f));
            _commonZoneMaterial = CreateMaterial(
                "Common Ore Zone",
                new Color(0.3f, 1f, 0.32f),
                0f,
                0f,
                new Color(0.06f, 0.38f, 0.04f));
            _rareZoneMaterial = CreateMaterial(
                "Rare Ore Zone",
                new Color(0.18f, 0.6f, 1f),
                0f,
                0f,
                new Color(0.02f, 0.2f, 0.65f));
            _veryRareZoneMaterial = CreateMaterial(
                "Very Rare Ore Zone",
                new Color(1f, 0.22f, 0.9f),
                0f,
                0f,
                new Color(0.5f, 0.03f, 0.42f));
            _validationFailureMaterial = CreateMaterial(
                "Validation Failure",
                new Color(1f, 0.05f, 0.04f),
                0f,
                0f,
                new Color(0.8f, 0.01f, 0.01f));
        }

        private void CreateSpriteAssets()
        {
            if (_headSprite != null)
            {
                return;
            }

            var machineAtlas = Resources.Load<Texture2D>("Art/DrillSnakeMachineAtlas");
            _headSprite = CreateAtlasSprite(machineAtlas, 0, 1, "Illustrated Drill Head");
            _chassisSprite = CreateAtlasSprite(machineAtlas, 1, 1, "Illustrated Chassis");
            _cargoSprite = CreateAtlasSprite(machineAtlas, 0, 0, "Illustrated Cargo Wagon");
            _refinerySprite = CreateAtlasSprite(machineAtlas, 1, 0, "Illustrated Refinery");

            var oreAtlas = Resources.Load<Texture2D>("Art/DrillSnakeOreAtlas");
            _commonOreSprite = CreateAtlasSprite(oreAtlas, 0, 1, "Illustrated Common Ore");
            _rareOreSprite = CreateAtlasSprite(oreAtlas, 1, 1, "Illustrated Rare Ore");
            _veryRareOreSprite = CreateAtlasSprite(oreAtlas, 0, 0, "Illustrated Very Rare Ore");
            _lampSprite = CreateAtlasSprite(oreAtlas, 1, 0, "Illustrated Mine Lamp");
        }

        private void CreateBaseFloor()
        {
            var floor = CreatePrimitive(
                PrimitiveType.Plane,
                _artMode == DrillSnakeArtMode.ProceduralCel
                    ? "Procedural Cel Excavation Floor"
                    : "Painted Excavation Floor",
                _worldRoot,
                _floorMaterial);
            floor.transform.position = new Vector3(0f, -0.11f, 2f);
            floor.transform.localScale = new Vector3(12f, 1f, 10f);
        }

        private void CreateProceduralFloorDebris()
        {
            var debrisRoot = new GameObject("Procedural Floor Debris");
            debrisRoot.transform.SetParent(_worldRoot, false);
            for (var y = 1; y < _map.Height - 1; y++)
            {
                for (var x = 1; x < _map.Width - 1; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var type = _map.GetCell(cell);
                    if (type != DrillSnakeCellType.OpenFloor ||
                        Hash01(cell, 211) < 0.88f)
                    {
                        continue;
                    }

                    var size = 0.07f + Hash01(cell, 223) * 0.13f;
                    var pebble = CreateMeshObject(
                        $"Loose Stone {x},{y}",
                        debrisRoot.transform,
                        GetBeveledCubeMesh(),
                        Hash01(cell, 227) > 0.72f
                            ? _softRockMaterial
                            : _bedrockMaterial);
                    pebble.transform.position = GridToWorld(
                        cell,
                        -0.045f + size * 0.18f);
                    pebble.transform.position += new Vector3(
                        (Hash01(cell, 229) - 0.5f) * 0.56f,
                        0f,
                        (Hash01(cell, 233) - 0.5f) * 0.56f);
                    pebble.transform.rotation = Quaternion.Euler(
                        Hash01(cell, 239) * 28f,
                        HashAngle(cell) + Hash01(cell, 241) * 45f,
                        Hash01(cell, 251) * 28f);
                    pebble.transform.localScale = new Vector3(
                        size,
                        size * (0.38f + Hash01(cell, 257) * 0.34f),
                        size * (0.72f + Hash01(cell, 263) * 0.5f));
                }
            }
        }

        private void CreateCellVisual(Vector2Int cell, DrillSnakeCellType type)
        {
            switch (type)
            {
                case DrillSnakeCellType.SoftRock:
                    CreateRock(cell, "Soft Rock", _softRockMaterial, 0.98f, 0.96f);
                    break;
                case DrillSnakeCellType.Bedrock:
                    CreateRock(cell, "Bedrock", _bedrockMaterial, 1.2f, 0.97f);
                    break;
                case DrillSnakeCellType.CommonOre:
                    CreateOre(cell, DrillSnakeOreType.Common);
                    break;
                case DrillSnakeCellType.RareOre:
                    CreateOre(cell, DrillSnakeOreType.Rare);
                    break;
                case DrillSnakeCellType.VeryRareOre:
                    CreateOre(cell, DrillSnakeOreType.VeryRare);
                    break;
                case DrillSnakeCellType.RefineryFloor:
                    CreateFloorTile(cell, "Refinery Floor", _refineryMaterial, 0.05f);
                    break;
                case DrillSnakeCellType.RefineryDock:
                    CreateFloorTile(cell, "Refinery Dock", _dockMaterial, 0.08f);
                    CreateDockBeacon(cell);
                    break;
            }
        }

        private void CreateRock(
            Vector2Int cell,
            string name,
            Material material,
            float height,
            float footprint)
        {
            var root = new GameObject($"{name} {cell.x},{cell.y}");
            root.transform.SetParent(_worldRoot, false);
            root.transform.position = GridToWorld(cell);
            root.transform.rotation = Quaternion.Euler(0f, HashAngle(cell), 0f);

            if (_artMode == DrillSnakeArtMode.ProceduralCel)
            {
                var blockHeight =
                    height * (0.76f + Hash01(cell, 17) * 0.22f);
                if (Hash01(cell, 19) > 0.88f)
                {
                    blockHeight *= 1.16f;
                }
                var width = footprint *
                    (0.965f + Hash01(cell, 23) * 0.035f);
                var depth = footprint *
                    (0.965f + Hash01(cell, 29) * 0.035f);
                CreateOutlinedBlock(
                    "Faceted Rock",
                    root.transform,
                    material,
                    new Vector3(0f, blockHeight * 0.5f - 0.025f, 0f),
                    new Vector3(
                        width,
                        blockHeight,
                        depth),
                    0f);
                var chipCount = Hash01(cell, 41) > 0.82f
                    ? 2
                    : Hash01(cell, 41) > 0.52f
                        ? 1
                        : 0;
                for (var chipIndex = 0; chipIndex < chipCount; chipIndex++)
                {
                    var cap = CreateMeshObject(
                        $"Angular Rock Cap {chipIndex + 1}",
                        root.transform,
                        GetBeveledCubeMesh(),
                        material);
                    var salt = chipIndex * 97;
                    var capSize =
                        0.1f + Hash01(cell, 67 + salt) * 0.13f;
                    cap.transform.localPosition = new Vector3(
                        (Hash01(cell, 53 + salt) - 0.5f) * 0.54f,
                        blockHeight + 0.018f + chipIndex * 0.012f,
                        (Hash01(cell, 59 + salt) - 0.5f) * 0.54f);
                    cap.transform.localRotation = Quaternion.Euler(
                        (Hash01(cell, 73 + salt) - 0.5f) * 14f,
                        HashAngle(cell) + 45f,
                        (Hash01(cell, 79 + salt) - 0.5f) * 14f);
                    cap.transform.localScale = new Vector3(
                        capSize,
                        0.07f + capSize * 0.22f,
                        capSize *
                        (0.72f + Hash01(cell, 83 + salt) * 0.3f));
                }

                _solidCells[cell] = root;
                return;
            }

            var baseRock = CreatePrimitive(
                PrimitiveType.Cube,
                "Packed Rock Base",
                root.transform,
                material);
            var heightVariation = 0.88f + Hash01(cell, 17) * 0.2f;
            baseRock.transform.localPosition = new Vector3(0f, height * 0.39f, 0f);
            baseRock.transform.localScale = new Vector3(
                footprint + 0.045f,
                height * 0.78f * heightVariation,
                footprint + 0.045f);

            _solidCells[cell] = root;
        }

        private void CreateOre(Vector2Int cell, DrillSnakeOreType oreType)
        {
            var root = new GameObject($"{oreType} Ore {cell.x},{cell.y}");
            root.transform.SetParent(_worldRoot, false);
            root.transform.position = GridToWorld(cell, 0.34f);
            root.transform.rotation = Quaternion.Euler(0f, HashAngle(cell), 0f);

            if (_artMode == DrillSnakeArtMode.ProceduralCel)
            {
                CreateProceduralOreCluster(root.transform, oreType, cell);
                _solidCells[cell] = root;
                return;
            }

            var sprite = oreType switch
            {
                DrillSnakeOreType.Common => _commonOreSprite,
                DrillSnakeOreType.Rare => _rareOreSprite,
                _ => _veryRareOreSprite
            };
            CreateWorldSprite(
                "Painted Ore Cluster",
                root.transform,
                sprite,
                1.22f,
                8);

            _solidCells[cell] = root;
        }

        private void CreateFloorTile(
            Vector2Int cell,
            string name,
            Material material,
            float height)
        {
            var tile = _artMode == DrillSnakeArtMode.ProceduralCel
                ? CreateMeshObject(
                    $"{name} {cell.x},{cell.y}",
                    _worldRoot,
                    GetBeveledCubeMesh(),
                    material)
                : CreatePrimitive(
                    PrimitiveType.Cube,
                    $"{name} {cell.x},{cell.y}",
                    _worldRoot,
                    material);
            tile.transform.position = GridToWorld(cell, height * 0.5f);
            tile.transform.localScale = new Vector3(0.96f, height, 0.96f);
        }

        private void CreateDockBeacon(Vector2Int cell)
        {
            var beacon = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Dock Hazard Ring",
                _worldRoot,
                _dockMaterial);
            beacon.transform.position = GridToWorld(cell, 0.14f);
            beacon.transform.localScale = new Vector3(0.43f, 0.07f, 0.43f);

            var center = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Dock Recess",
                _worldRoot,
                _refineryDarkMaterial);
            center.transform.position = GridToWorld(cell, 0.2f);
            center.transform.localScale = new Vector3(0.25f, 0.075f, 0.25f);
        }

        private void CreateRefinerySetDressing()
        {
            var refineryRoot = new GameObject("Industrial Refinery Dressing");
            refineryRoot.transform.SetParent(_worldRoot, false);
            refineryRoot.transform.position = GridToWorld(
                _map.Center,
                _artMode == DrillSnakeArtMode.ProceduralCel ? 0.12f : 0.42f);
            if (_artMode == DrillSnakeArtMode.ProceduralCel)
            {
                CreateProceduralRefinery(refineryRoot.transform);
                return;
            }

            CreateWorldSprite(
                "Painted Refinery Platform",
                refineryRoot.transform,
                _refinerySprite,
                3.65f,
                3);
        }

        private void CreateMineLamps()
        {
            var lampRoot = new GameObject("Warm Mine Lamps");
            lampRoot.transform.SetParent(_worldRoot, false);
            var created = 0;
            foreach (var room in _map.Graph.Rooms)
            {
                if (room.Kind == DrillSnakeRoomKind.Refinery ||
                    room.Id % 2 == 0 ||
                    created >= 6)
                {
                    continue;
                }

                var lampCell = FindLampCell(room);
                CreateMineLamp(lampRoot.transform, lampCell);
                created++;
            }
        }

        private Vector2Int FindLampCell(DrillSnakeRoom room)
        {
            for (var x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
            {
                var above = new Vector2Int(x, room.Bounds.yMax);
                if (_map.GetCell(above) == DrillSnakeCellType.Bedrock)
                {
                    return above;
                }
            }

            return room.Center;
        }

        private void CreateMineLamp(Transform parent, Vector2Int cell)
        {
            var root = new GameObject($"Mine Lamp {cell.x},{cell.y}");
            root.transform.SetParent(parent, false);
            root.transform.position = GridToWorld(
                cell,
                _artMode == DrillSnakeArtMode.ProceduralCel ? 0.2f : 0.74f);
            if (_artMode == DrillSnakeArtMode.ProceduralCel)
            {
                var bracket = CreateMeshObject(
                    "Cel Lamp Bracket",
                    root.transform,
                    GetBeveledCubeMesh(),
                    _refineryDarkMaterial);
                bracket.transform.localPosition = new Vector3(0f, 0.26f, 0f);
                bracket.transform.localScale = new Vector3(0.3f, 0.46f, 0.18f);
                var glow = CreatePrimitive(
                    PrimitiveType.Sphere,
                    "Cel Lamp Bulb",
                    root.transform,
                    _lampMaterial);
                glow.transform.localPosition = new Vector3(0f, 0.48f, -0.1f);
                glow.transform.localScale = Vector3.one * 0.22f;
            }
            else
            {
                CreateWorldSprite(
                    "Painted Wall Lantern",
                    root.transform,
                    _lampSprite,
                    1.35f,
                    12);
            }

            var lightObject = new GameObject("Warm Point Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var pointLight = lightObject.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = new Color(1f, 0.47f, 0.12f);
            pointLight.intensity = 2.4f;
            pointLight.range = 5.5f;
            pointLight.shadows = LightShadows.None;
        }

        private void CreateGridOverlay()
        {
            _gridOverlay = new GameObject("Debug Grid Overlay");
            _gridOverlay.transform.SetParent(_worldRoot, false);

            var minimum = -_map.Width * 0.5f;
            var maximum = _map.Width * 0.5f;
            var lineHeight = 1.08f;
            for (var i = 0; i <= _map.Width; i++)
            {
                var offset = minimum + i;
                CreateGridLine(
                    new Vector3(offset, lineHeight, minimum),
                    new Vector3(offset, lineHeight, maximum));
                CreateGridLine(
                    new Vector3(minimum, lineHeight, offset),
                    new Vector3(maximum, lineHeight, offset));
            }

            _gridOverlay.SetActive(_gridVisible);
        }

        private void CreateGridLine(Vector3 start, Vector3 end)
        {
            var lineObject = new GameObject("Grid Line");
            lineObject.transform.SetParent(_gridOverlay.transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = _gridMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.022f;
            line.endWidth = 0.022f;
            line.numCapVertices = 0;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private void CreateLevelDesignOverlay()
        {
            _levelDesignOverlay = new GameObject("Level Design Validation Overlay");
            _levelDesignOverlay.transform.SetParent(_worldRoot, false);

            foreach (var route in _map.Graph.Routes)
            {
                CreateRouteOverlay(route);
            }

            foreach (var room in _map.Graph.Rooms)
            {
                CreateRoomOverlay(room);
            }

            if (_map.ValidationReport != null)
            {
                foreach (var failure in _map.ValidationReport.Failures)
                {
                    foreach (var cell in failure.Cells)
                    {
                        CreateValidationFailureMarker(cell, failure.Code);
                    }
                }
            }

            _levelDesignOverlay.SetActive(_levelDesignOverlayVisible);
        }

        private void CreateRouteOverlay(DrillSnakeRoute route)
        {
            var routeObject = new GameObject(
                route.Kind == DrillSnakeRouteKind.SafeLongRoute
                    ? $"Safe Long Route {route.Id}"
                    : route.Kind == DrillSnakeRouteKind.RiskySoftRockShortcut
                        ? $"Risky Short Route {route.Id}"
                        : $"Required Route {route.Id}");
            routeObject.transform.SetParent(_levelDesignOverlay.transform, false);

            var line = routeObject.AddComponent<LineRenderer>();
            line.sharedMaterial = route.Kind switch
            {
                DrillSnakeRouteKind.SafeLongRoute => _safeRouteMaterial,
                DrillSnakeRouteKind.RiskySoftRockShortcut => _riskyRouteMaterial,
                _ => _standardRouteMaterial
            };
            line.useWorldSpace = true;
            line.positionCount = route.Waypoints.Count;
            line.startWidth = route.Kind == DrillSnakeRouteKind.RiskySoftRockShortcut
                ? 0.22f
                : 0.14f;
            line.endWidth = line.startWidth;
            line.numCapVertices = 3;
            for (var i = 0; i < route.Waypoints.Count; i++)
            {
                line.SetPosition(i, GridToWorld(route.Waypoints[i], 1.28f));
            }
        }

        private void CreateRoomOverlay(DrillSnakeRoom room)
        {
            var material = GetRoomOverlayMaterial(room);
            var boundsObject = new GameObject(
                $"Turning Chamber {room.Id} — {room.Name}");
            boundsObject.transform.SetParent(_levelDesignOverlay.transform, false);
            var bounds = room.Bounds;
            var minimum = GridToWorld(
                new Vector2Int(bounds.xMin, bounds.yMin),
                1.31f);
            var maximum = GridToWorld(
                new Vector2Int(bounds.xMax - 1, bounds.yMax - 1),
                1.31f);
            minimum -= new Vector3(0.47f, 0f, 0.47f);
            maximum += new Vector3(0.47f, 0f, 0.47f);

            var outline = boundsObject.AddComponent<LineRenderer>();
            outline.sharedMaterial = material;
            outline.useWorldSpace = true;
            outline.positionCount = 5;
            outline.startWidth = 0.12f;
            outline.endWidth = 0.12f;
            outline.numCornerVertices = 2;
            outline.SetPosition(0, new Vector3(minimum.x, minimum.y, minimum.z));
            outline.SetPosition(1, new Vector3(maximum.x, minimum.y, minimum.z));
            outline.SetPosition(2, new Vector3(maximum.x, minimum.y, maximum.z));
            outline.SetPosition(3, new Vector3(minimum.x, minimum.y, maximum.z));
            outline.SetPosition(4, new Vector3(minimum.x, minimum.y, minimum.z));

            var node = CreatePrimitive(
                PrimitiveType.Sphere,
                $"Room Node {room.Id}",
                _levelDesignOverlay.transform,
                material);
            node.transform.position = GridToWorld(room.Center, 1.38f);
            node.transform.localScale = room.Kind == DrillSnakeRoomKind.Refinery
                ? Vector3.one * 0.72f
                : Vector3.one * 0.48f;

            var labelObject = new GameObject($"Room Label {room.Id}");
            labelObject.transform.SetParent(_levelDesignOverlay.transform, false);
            labelObject.transform.position = GridToWorld(room.Center, 1.52f);
            labelObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 30;
            label.characterSize = 0.075f;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.color = material.color;
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.text =
                $"R{room.Id}  {room.Bounds.width}x{room.Bounds.height}\n" +
                $"D={room.GraphDistance}  {DistanceTierLabel(room.DistanceTier)}";
        }

        private void CreateValidationFailureMarker(Vector2Int cell, string code)
        {
            if (!_map.IsInBounds(cell))
            {
                return;
            }

            var marker = CreatePrimitive(
                PrimitiveType.Cube,
                $"Validation Failure {code} at {cell.x},{cell.y}",
                _levelDesignOverlay.transform,
                _validationFailureMaterial);
            marker.transform.position = GridToWorld(cell, 1.56f);
            marker.transform.localScale = new Vector3(0.72f, 0.18f, 0.72f);
            marker.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        }

        private Material GetRoomOverlayMaterial(DrillSnakeRoom room)
        {
            return room.DistanceTier switch
            {
                DrillSnakeDistanceTier.Refinery => _refineryNodeMaterial,
                DrillSnakeDistanceTier.Common => _commonZoneMaterial,
                DrillSnakeDistanceTier.Rare => _rareZoneMaterial,
                _ => _veryRareZoneMaterial
            };
        }

        private static string DistanceTierLabel(DrillSnakeDistanceTier tier)
        {
            return tier switch
            {
                DrillSnakeDistanceTier.Refinery => "REFINERY",
                DrillSnakeDistanceTier.Common => "COMMON ZONE",
                DrillSnakeDistanceTier.Rare => "RARE ZONE",
                _ => "VERY RARE ZONE"
            };
        }

        private SegmentView CreateSegmentView(
            int index,
            DrillSnakeSimulation simulation)
        {
            var root = new GameObject(index == 0 ? "Drill Head" : $"Snake Segment {index}");
            root.transform.SetParent(_worldRoot, false);
            if (_artMode == DrillSnakeArtMode.ProceduralCel)
            {
                var artwork = new GameObject(
                    index == 0 ? "Procedural Cel Drill" : "Procedural Cel Module");
                artwork.transform.SetParent(root.transform, false);
                artwork.transform.localScale = Vector3.one * 1.08f;
                if (index == 0)
                {
                    CreateProceduralDrillHead(artwork.transform);
                    var turret = CreateTurret(artwork.transform, 0.33f, 1f);
                    CreateDrillAura(artwork.transform, -0.02f);
                    return new SegmentView
                    {
                        Root = root,
                        Turret = turret
                    };
                }

                if (index < DrillSnakeSimulation.MinimumSegmentCount)
                {
                    CreateProceduralChassis(artwork.transform, false, DrillSnakeOreType.None);
                }
                else
                {
                    var cargoIndex = index - DrillSnakeSimulation.MinimumSegmentCount;
                    var oreType = cargoIndex >= 0 &&
                                  cargoIndex < simulation.Cargo.Count
                        ? simulation.Cargo[cargoIndex].OreType
                        : DrillSnakeOreType.Common;
                    CreateProceduralChassis(artwork.transform, true, oreType);
                }

                return new SegmentView
                {
                    Root = root
                };
            }

            var sprite = index == 0
                ? _headSprite
                : index < DrillSnakeSimulation.MinimumSegmentCount
                    ? _chassisSprite
                    : _cargoSprite;
            var scale = index == 0 ? 1.5f : 1.22f;
            CreateWorldSprite(
                index == 0 ? "Painted Drill Vehicle" : "Painted Snake Module",
                root.transform,
                sprite,
                scale,
                24 - Mathf.Min(index, 12));
            Transform paintedTurret = null;
            if (index == 0)
            {
                paintedTurret = CreateTurret(root.transform, 0.2f, 0.86f);
                CreateDrillAura(root.transform, -0.65f);
            }

            return new SegmentView
            {
                Root = root,
                Turret = paintedTurret
            };
        }

        private Transform CreateTurret(
            Transform parent,
            float localHeight,
            float scale)
        {
            var turretRoot = new GameObject("Auto Turret").transform;
            turretRoot.SetParent(parent, false);
            turretRoot.localPosition = new Vector3(0f, localHeight, -0.03f);
            turretRoot.localScale = Vector3.one * scale;

            var baseObject = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Turret Base",
                turretRoot,
                _refineryDarkMaterial);
            baseObject.transform.localScale = new Vector3(0.25f, 0.08f, 0.25f);

            var housing = CreateMeshObject(
                "Turret Housing",
                turretRoot,
                GetBeveledCubeMesh(),
                _dockMaterial);
            housing.transform.localPosition = new Vector3(0f, 0.13f, 0.04f);
            housing.transform.localScale = new Vector3(0.3f, 0.2f, 0.34f);

            var barrel = CreateMeshObject(
                "Turret Barrel",
                turretRoot,
                GetBeveledCubeMesh(),
                _refineryMaterial);
            barrel.transform.localPosition = new Vector3(0f, 0.14f, 0.34f);
            barrel.transform.localScale = new Vector3(0.1f, 0.1f, 0.48f);

            var muzzle = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Turret Muzzle",
                turretRoot,
                _dockMaterial);
            muzzle.transform.localPosition = new Vector3(0f, 0.14f, 0.59f);
            muzzle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            muzzle.transform.localScale = new Vector3(0.11f, 0.08f, 0.11f);
            return turretRoot;
        }

        private void CreateDrillAura(Transform parent, float localHeight)
        {
            _drillAura = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Active Drill Aura",
                parent,
                _dockMaterial);
            _drillAura.transform.localPosition =
                new Vector3(0f, localHeight, 0.05f);
            _drillAura.transform.localScale = new Vector3(0.94f, 0.05f, 0.94f);
            _drillAura.SetActive(_drillAuraActive);
        }

        private GameObject CreateOrePickupView(DrillSnakeOrePickup pickup)
        {
            var root = new GameObject(
                $"{pickup.OreType} Ore Fragment {pickup.Cell.x},{pickup.Cell.y}");
            root.transform.SetParent(_worldRoot, false);
            root.transform.position = GridToWorld(pickup.Cell, 0.23f);

            if (_artMode == DrillSnakeArtMode.IllustratedPng)
            {
                var sprite = pickup.OreType switch
                {
                    DrillSnakeOreType.Rare => _rareOreSprite,
                    DrillSnakeOreType.VeryRare => _veryRareOreSprite,
                    _ => _commonOreSprite
                };
                CreateWorldSprite(
                    "Painted Ore Fragment",
                    root.transform,
                    sprite,
                    0.48f,
                    28);
                return root;
            }

            var crystal = CreateMeshObject(
                "Cel Ore Fragment",
                root.transform,
                GetCrystalMesh(),
                GetOreMaterial(pickup.OreType));
            crystal.transform.localRotation = Quaternion.Euler(18f, 45f, 12f);
            crystal.transform.localScale = new Vector3(0.48f, 0.46f, 0.48f);

            var shadow = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Fragment Shadow",
                root.transform,
                _outlineMaterial);
            shadow.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            shadow.transform.localScale = new Vector3(0.22f, 0.025f, 0.22f);
            return root;
        }

        private GameObject CreateDrillPowerupView(Vector2Int cell)
        {
            var root = new GameObject($"Drill Charge {cell.x},{cell.y}");
            root.transform.SetParent(_worldRoot, false);
            root.transform.position = GridToWorld(cell, 0.32f);

            var ring = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Powerup Ring",
                root.transform,
                _dockMaterial);
            ring.transform.localScale = new Vector3(0.42f, 0.08f, 0.42f);

            var core = CreateMeshObject(
                "Powerup Drill Bit",
                root.transform,
                GetDrillConeMesh(),
                _artMode == DrillSnakeArtMode.ProceduralCel
                    ? _steelLightMaterial
                    : _refineryMaterial);
            core.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            core.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            core.transform.localScale = new Vector3(0.3f, 0.3f, 0.48f);

            var glowObject = new GameObject("Powerup Light");
            glowObject.transform.SetParent(root.transform, false);
            glowObject.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            var glow = glowObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.38f, 0.04f);
            glow.intensity = 1.8f;
            glow.range = 3f;
            glow.shadows = LightShadows.None;
            return root;
        }

        private IEnumerator AnimateTurretShot(
            DrillSnakeTurretResult result,
            float travelSeconds,
            float projectileSize)
        {
            var projectile = CreatePrimitive(
                PrimitiveType.Sphere,
                "Turret Projectile",
                _worldRoot,
                _artMode == DrillSnakeArtMode.ProceduralCel &&
                _lampMaterial != null
                    ? _lampMaterial
                    : _dockMaterial);
            var start = _segmentViews.Count > 0 &&
                        _segmentViews[0].Root != null
                ? _segmentViews[0].Root.transform.position +
                  Vector3.up * 0.52f
                : GridToWorld(result.Origin, 0.7f);
            var end = GridToWorld(result.Target, 0.72f);
            projectile.transform.position = start;
            projectile.transform.localScale =
                Vector3.one * Mathf.Max(0.1f, projectileSize);

            var duration = Mathf.Max(0.05f, travelSeconds);
            var elapsed = 0f;
            while (elapsed < duration && projectile != null)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                projectile.transform.position = Vector3.Lerp(
                    start,
                    end,
                    progress);
                yield return null;
            }

            if (projectile != null)
            {
                projectile.transform.position = end;
                projectile.transform.localScale =
                    Vector3.one * Mathf.Max(0.35f, projectileSize * 1.65f);
                yield return null;
                Destroy(projectile);
            }
        }

        private IEnumerator AnimateOreScatter(
            Vector2Int source,
            Vector2Int target)
        {
            if (!_orePickupViews.TryGetValue(target, out var pickupView) ||
                pickupView == null)
            {
                yield break;
            }

            _animatingPickupCells.Add(target);
            var start = GridToWorld(source, 0.72f);
            var end = GridToWorld(target, 0.23f);
            pickupView.transform.position = start;
            const float duration = 0.34f;
            var elapsed = 0f;
            while (elapsed < duration && pickupView != null)
            {
                if (!_orePickupViews.TryGetValue(target, out var activeView) ||
                    activeView != pickupView)
                {
                    _animatingPickupCells.Remove(target);
                    yield break;
                }

                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var position = Vector3.Lerp(start, end, progress);
                position.y += Mathf.Sin(progress * Mathf.PI) * 0.65f;
                pickupView.transform.position = position;
                pickupView.transform.Rotate(
                    new Vector3(180f, 260f, 120f) * Time.deltaTime,
                    Space.World);
                yield return null;
            }

            _animatingPickupCells.Remove(target);
            if (pickupView != null &&
                _orePickupViews.TryGetValue(target, out var finalView) &&
                finalView == pickupView)
            {
                pickupView.transform.position = end;
            }
        }

        private IEnumerator AnimateOreCollection(
            GameObject pickupView,
            DrillSnakeOreType oreType)
        {
            if (pickupView == null)
            {
                yield break;
            }

            var start = pickupView.transform.position;
            var startScale = pickupView.transform.localScale;
            var target = start;
            const float duration = 0.4f;
            var elapsed = 0f;
            while (elapsed < duration && pickupView != null)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var eased = progress * progress * (3f - 2f * progress);
                if (TryGetHeadVisualPosition(out var headPosition))
                {
                    target = headPosition + Vector3.up * 0.42f;
                }

                var position = Vector3.Lerp(start, target, eased);
                position.y += Mathf.Sin(progress * Mathf.PI) * 0.55f;
                pickupView.transform.position = position;
                pickupView.transform.localScale = Vector3.Lerp(
                    startScale,
                    startScale * 0.42f,
                    eased);
                pickupView.transform.Rotate(
                    new Vector3(420f, 720f, 300f) * Time.deltaTime,
                    Space.World);
                yield return null;
            }

            if (TryGetHeadVisualPosition(out var finalHeadPosition))
            {
                target = finalHeadPosition + Vector3.up * 0.42f;
            }

            if (pickupView != null)
            {
                pickupView.transform.position = target;
                Destroy(pickupView);
            }

            StartCoroutine(AnimateOreCollectionFanfare(target, oreType));
        }

        private IEnumerator AnimateOreCollectionFanfare(
            Vector3 position,
            DrillSnakeOreType oreType)
        {
            if (_worldRoot == null)
            {
                yield break;
            }

            var effectRoot = new GameObject($"{oreType} Collection Fanfare");
            effectRoot.transform.SetParent(_worldRoot, false);
            effectRoot.transform.position = position;
            var oreMaterial = GetOreMaterial(oreType);

            var flash = CreatePrimitive(
                PrimitiveType.Sphere,
                "Collection Flash",
                effectRoot.transform,
                oreMaterial);
            flash.transform.localScale = Vector3.one * 0.12f;

            var ring = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Collection Ring",
                effectRoot.transform,
                oreMaterial);
            ring.transform.localPosition = Vector3.down * 0.18f;
            ring.transform.localScale = new Vector3(0.12f, 0.025f, 0.12f);

            const int particleCount = 12;
            var particles = new GameObject[particleCount];
            var directions = new Vector3[particleCount];
            for (var index = 0; index < particleCount; index++)
            {
                var angle = index * Mathf.PI * 2f / particleCount;
                directions[index] = new Vector3(
                    Mathf.Cos(angle),
                    0.45f + (index % 3) * 0.16f,
                    Mathf.Sin(angle));
                particles[index] = CreatePrimitive(
                    index % 2 == 0
                        ? PrimitiveType.Cube
                        : PrimitiveType.Sphere,
                    $"Collection Spark {index + 1}",
                    effectRoot.transform,
                    oreMaterial);
                particles[index].transform.localScale =
                    Vector3.one * (0.07f + (index % 3) * 0.012f);
            }

            var lightObject = new GameObject("Collection Flash Light");
            lightObject.transform.SetParent(effectRoot.transform, false);
            var flashLight = lightObject.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = OreEffectColor(oreType);
            flashLight.intensity = 3.4f;
            flashLight.range = 3.2f;
            flashLight.shadows = LightShadows.None;

            const float duration = 0.48f;
            var elapsed = 0f;
            while (elapsed < duration && effectRoot != null)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var fade = 1f - progress;
                flash.transform.localScale =
                    Vector3.one * Mathf.Lerp(0.12f, 0.72f, progress) * fade;
                ring.transform.localScale = new Vector3(
                    Mathf.Lerp(0.12f, 1.15f, progress),
                    0.025f * fade,
                    Mathf.Lerp(0.12f, 1.15f, progress));
                ring.transform.Rotate(Vector3.up, 360f * Time.deltaTime);
                flashLight.intensity = 3.4f * fade * fade;

                for (var index = 0; index < particleCount; index++)
                {
                    var spark = particles[index];
                    if (spark == null)
                    {
                        continue;
                    }

                    var direction = directions[index];
                    spark.transform.localPosition =
                        direction * (progress * 0.82f) +
                        Vector3.down * (progress * progress * 0.52f);
                    spark.transform.localScale =
                        Vector3.one * (0.085f * fade);
                    spark.transform.Rotate(
                        new Vector3(420f, 680f, 260f) * Time.deltaTime,
                        Space.Self);
                }

                yield return null;
            }

            if (effectRoot != null)
            {
                Destroy(effectRoot);
            }
        }

        private static Color OreEffectColor(DrillSnakeOreType oreType)
        {
            return oreType switch
            {
                DrillSnakeOreType.Rare => new Color(0.08f, 0.68f, 1f),
                DrillSnakeOreType.VeryRare => new Color(1f, 0.16f, 0.78f),
                _ => new Color(1f, 0.42f, 0.04f)
            };
        }

        private static void RemoveMissingCollectibleViews(
            Dictionary<Vector2Int, GameObject> views,
            HashSet<Vector2Int> activeCells)
        {
            var removed = new List<Vector2Int>();
            foreach (var pair in views)
            {
                if (!activeCells.Contains(pair.Key))
                {
                    if (pair.Value != null)
                    {
                        Destroy(pair.Value);
                    }

                    removed.Add(pair.Key);
                }
            }

            foreach (var cell in removed)
            {
                views.Remove(cell);
            }
        }

        private void CreateProceduralDrillHead(Transform parent)
        {
            CreateOutlinedBlock(
                "Drill Chassis",
                parent,
                _machineAccentMaterial,
                new Vector3(0f, 0f, -0.05f),
                new Vector3(0.72f, 0.32f, 0.8f),
                0.055f);

            CreateTrack(parent, -0.43f);
            CreateTrack(parent, 0.43f);

            CreateOutlinedBlock(
                "Drill Top Plate",
                parent,
                _refineryDarkMaterial,
                new Vector3(0f, 0.19f, -0.08f),
                new Vector3(0.48f, 0.09f, 0.42f),
                0.02f);

            var collar = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Drill Collar",
                parent,
                _machineAccentMaterial);
            collar.transform.localPosition = new Vector3(0f, 0f, 0.43f);
            collar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            collar.transform.localScale = new Vector3(0.31f, 0.16f, 0.31f);

            var drill = CreateMeshObject(
                "Faceted Drill Bit",
                parent,
                GetDrillConeMesh(),
                _steelLightMaterial);
            drill.transform.localPosition = new Vector3(0f, 0f, 0.78f);
            drill.transform.localScale = new Vector3(0.42f, 0.42f, 0.62f);

            var stripe = CreateMeshObject(
                "Safety Stripe",
                parent,
                GetBeveledCubeMesh(),
                _dockMaterial);
            stripe.transform.localPosition = new Vector3(0f, 0.255f, -0.05f);
            stripe.transform.localScale = new Vector3(0.34f, 0.045f, 0.14f);

            var core = CreatePrimitive(
                PrimitiveType.Sphere,
                "Glowing Drill Core",
                parent,
                _lampMaterial);
            core.transform.localPosition = new Vector3(0f, 0.25f, -0.24f);
            core.transform.localScale = new Vector3(0.22f, 0.12f, 0.22f);
        }

        private void CreateProceduralChassis(
            Transform parent,
            bool carriesOre,
            DrillSnakeOreType oreType)
        {
            CreateOutlinedBlock(
                carriesOre ? "Cargo Car" : "Drive Chassis",
                parent,
                _machineAccentMaterial,
                Vector3.zero,
                new Vector3(0.72f, 0.3f, 0.72f),
                0.05f);
            CreateTrack(parent, -0.43f);
            CreateTrack(parent, 0.43f);

            CreateOutlinedBlock(
                carriesOre ? "Cargo Recess" : "Drive Top Plate",
                parent,
                _refineryDarkMaterial,
                new Vector3(0f, 0.19f, 0f),
                new Vector3(0.48f, 0.1f, 0.48f),
                0.022f);

            var frontCoupler = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Front Coupler",
                parent,
                _machineAccentMaterial);
            frontCoupler.transform.localPosition = new Vector3(0f, -0.02f, 0.42f);
            frontCoupler.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            frontCoupler.transform.localScale = new Vector3(0.105f, 0.12f, 0.105f);

            var rearCoupler = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Rear Coupler",
                parent,
                _machineAccentMaterial);
            rearCoupler.transform.localPosition = new Vector3(0f, -0.02f, -0.42f);
            rearCoupler.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rearCoupler.transform.localScale = new Vector3(0.105f, 0.12f, 0.105f);

            if (carriesOre)
            {
                var oreMaterial = GetOreMaterial(oreType);
                for (var i = 0; i < 3; i++)
                {
                    var crystal = CreateMeshObject(
                        $"Cargo Crystal {i + 1}",
                        parent,
                        GetCrystalMesh(),
                        oreMaterial);
                    crystal.transform.localPosition = new Vector3(
                        (i - 1) * 0.19f,
                        0.3f + (i == 1 ? 0.07f : 0f),
                        i % 2 == 0 ? -0.04f : 0.05f);
                    crystal.transform.localRotation = Quaternion.Euler(
                        i == 1 ? 8f : -8f,
                        45f + i * 24f,
                        i == 1 ? -5f : 8f);
                    crystal.transform.localScale = new Vector3(
                        0.3f,
                        i == 1 ? 0.32f : 0.23f,
                        0.3f);
                }
            }
            else
            {
                var gear = CreatePrimitive(
                    PrimitiveType.Cylinder,
                    "Drive Gear",
                    parent,
                    _steelLightMaterial);
                gear.transform.localPosition = new Vector3(0f, 0.285f, 0f);
                gear.transform.localScale = new Vector3(0.2f, 0.05f, 0.2f);
            }
        }

        private void CreateTrack(Transform parent, float x)
        {
            var track = CreateMeshObject(
                x < 0f ? "Left Track" : "Right Track",
                parent,
                GetBeveledCubeMesh(),
                _rubberMaterial);
            track.transform.localPosition = new Vector3(x, -0.08f, -0.02f);
            track.transform.localScale = new Vector3(0.18f, 0.22f, 0.76f);

            for (var i = -1; i <= 1; i++)
            {
                var tread = CreateMeshObject(
                    "Track Tread",
                    parent,
                    GetBeveledCubeMesh(),
                    _steelLightMaterial);
                tread.transform.localPosition = new Vector3(
                    x,
                    0.045f,
                    i * 0.24f - 0.02f);
                tread.transform.localScale = new Vector3(0.2f, 0.045f, 0.07f);
            }
        }

        private void CreateProceduralOreCluster(
            Transform parent,
            DrillSnakeOreType oreType,
            Vector2Int cell)
        {
            var pedestal = CreatePrimitive(
                PrimitiveType.Sphere,
                "Dark Ore Matrix",
                parent,
                _refineryDarkMaterial);
            pedestal.transform.localPosition = new Vector3(0f, -0.28f, 0f);
            pedestal.transform.localScale = new Vector3(0.82f, 0.34f, 0.82f);

            var oreMaterial = GetOreMaterial(oreType);
            for (var i = 0; i < 4; i++)
            {
                var angle = (Hash01(cell, 71 + i) * 0.6f + i) *
                            Mathf.PI * 0.5f;
                var radius = i == 0 ? 0f : 0.17f + Hash01(cell, 83 + i) * 0.1f;
                var shard = CreateMeshObject(
                    $"Cel Ore Shard {i + 1}",
                    parent,
                    GetCrystalMesh(),
                    oreMaterial);
                shard.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    -0.05f + (i == 0 ? 0.22f : 0.08f),
                    Mathf.Sin(angle) * radius);
                shard.transform.localRotation = Quaternion.Euler(
                    (Hash01(cell, 97 + i) - 0.5f) * 24f,
                    angle * Mathf.Rad2Deg + 45f,
                    (Hash01(cell, 107 + i) - 0.5f) * 20f);
                var height = i == 0
                    ? 0.58f
                    : 0.3f + Hash01(cell, 113 + i) * 0.2f;
                shard.transform.localScale = new Vector3(
                    i == 0 ? 0.59f : 0.43f,
                    height * 1.1f,
                    i == 0 ? 0.59f : 0.43f);
            }
        }

        private void CreateProceduralRefinery(Transform parent)
        {
            CreateOutlinedBlock(
                "Refinery Platform",
                parent,
                _refineryMaterial,
                Vector3.zero,
                new Vector3(3.45f, 0.16f, 3.45f),
                0.07f);

            var hub = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Refinery Turntable",
                parent,
                _refineryDarkMaterial);
            hub.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            hub.transform.localScale = new Vector3(0.72f, 0.08f, 0.72f);

            var hubRing = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Refinery Energy Ring",
                parent,
                _machineAccentMaterial);
            hubRing.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            hubRing.transform.localScale = new Vector3(0.5f, 0.045f, 0.5f);

            var darkCenter = CreatePrimitive(
                PrimitiveType.Cylinder,
                "Refinery Loading Recess",
                parent,
                _outlineMaterial);
            darkCenter.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            darkCenter.transform.localScale = new Vector3(0.3f, 0.035f, 0.3f);

            var corners = new[]
            {
                new Vector3(-1.38f, 0f, -1.38f),
                new Vector3(1.38f, 0f, -1.38f),
                new Vector3(-1.38f, 0f, 1.38f),
                new Vector3(1.38f, 0f, 1.38f)
            };
            foreach (var corner in corners)
            {
                CreateOutlinedBlock(
                    "Refinery Pylon",
                    parent,
                    _steelMaterial,
                    corner + Vector3.up * 0.25f,
                    new Vector3(0.42f, 0.52f, 0.42f),
                    0.045f);
                var cap = CreatePrimitive(
                    PrimitiveType.Cylinder,
                    "Pylon Warning Light",
                    parent,
                    _lampMaterial);
                cap.transform.localPosition = corner + Vector3.up * 0.57f;
                cap.transform.localScale = new Vector3(0.13f, 0.08f, 0.13f);
            }

            for (var direction = -1; direction <= 1; direction += 2)
            {
                for (var stripeIndex = -1; stripeIndex <= 1; stripeIndex++)
                {
                    var stripe = CreatePrimitive(
                        PrimitiveType.Cube,
                        "Dock Safety Mark",
                        parent,
                        _dockMaterial);
                    stripe.transform.localPosition = new Vector3(
                        stripeIndex * 0.42f,
                        0.13f,
                        direction * 1.55f);
                    stripe.transform.localRotation = Quaternion.Euler(
                        0f,
                        direction * 25f,
                        0f);
                    stripe.transform.localScale = new Vector3(0.22f, 0.035f, 0.5f);
                }
            }
        }

        private GameObject CreateOutlinedBlock(
            string name,
            Transform parent,
            Material material,
            Vector3 localPosition,
            Vector3 localScale,
            float outlineWidth)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            if (outlineWidth > 0.001f)
            {
                var outline = CreateMeshObject(
                    "Ink Silhouette",
                    root.transform,
                    GetBeveledCubeMesh(),
                    _outlineMaterial ?? _refineryDarkMaterial);
                outline.transform.localPosition = new Vector3(0f, -outlineWidth, 0f);
                outline.transform.localScale = localScale +
                                               new Vector3(
                                                   outlineWidth * 2f,
                                                   outlineWidth,
                                                   outlineWidth * 2f);
            }

            var block = CreateMeshObject(
                "Cel Surface",
                root.transform,
                GetBeveledCubeMesh(),
                material);
            block.transform.localScale = localScale;
            return root;
        }

        private Material GetOreMaterial(DrillSnakeOreType oreType)
        {
            return oreType switch
            {
                DrillSnakeOreType.Rare => _rareOreMaterial,
                DrillSnakeOreType.VeryRare => _veryRareOreMaterial,
                _ => _commonOreMaterial
            };
        }

        private Mesh GetBeveledCubeMesh()
        {
            if (_beveledCubeMesh != null)
            {
                return _beveledCubeMesh;
            }

            const float half = 0.5f;
            const float bevel = 0.085f;
            const float inner = half - bevel;
            var vertices = new List<Vector3>(132);
            var normals = new List<Vector3>(132);
            var triangles = new List<int>(198);
            var faceNormals = new[]
            {
                Vector3.right,
                Vector3.left,
                Vector3.up,
                Vector3.down,
                Vector3.forward,
                Vector3.back
            };

            foreach (var normal in faceNormals)
            {
                var tangent = Mathf.Abs(normal.y) > 0.9f
                    ? Vector3.right
                    : Vector3.Cross(Vector3.up, normal).normalized;
                var bitangent = Vector3.Cross(normal, tangent).normalized;
                var center = normal * half;
                AddOrientedQuad(
                    vertices,
                    normals,
                    triangles,
                    center - tangent * inner - bitangent * inner,
                    center + tangent * inner - bitangent * inner,
                    center + tangent * inner + bitangent * inner,
                    center - tangent * inner + bitangent * inner,
                    normal);
            }

            var axes = new[]
            {
                Vector3.right,
                Vector3.up,
                Vector3.forward
            };
            for (var firstAxis = 0; firstAxis < 3; firstAxis++)
            {
                for (var secondAxis = firstAxis + 1;
                     secondAxis < 3;
                     secondAxis++)
                {
                    var remainingAxis = 3 - firstAxis - secondAxis;
                    var edgeDirection = axes[remainingAxis];
                    for (var firstSign = -1;
                         firstSign <= 1;
                         firstSign += 2)
                    {
                        for (var secondSign = -1;
                             secondSign <= 1;
                             secondSign += 2)
                        {
                            var firstNormal =
                                axes[firstAxis] * firstSign;
                            var secondNormal =
                                axes[secondAxis] * secondSign;
                            var edgeNormal =
                                (firstNormal + secondNormal).normalized;
                            var firstPlane =
                                firstNormal * half +
                                secondNormal * inner;
                            var secondPlane =
                                firstNormal * inner +
                                secondNormal * half;
                            AddOrientedQuad(
                                vertices,
                                normals,
                                triangles,
                                firstPlane - edgeDirection * inner,
                                secondPlane - edgeDirection * inner,
                                secondPlane + edgeDirection * inner,
                                firstPlane + edgeDirection * inner,
                                edgeNormal);
                        }
                    }
                }
            }

            for (var xSign = -1; xSign <= 1; xSign += 2)
            {
                for (var ySign = -1; ySign <= 1; ySign += 2)
                {
                    for (var zSign = -1; zSign <= 1; zSign += 2)
                    {
                        var normal = new Vector3(
                            xSign,
                            ySign,
                            zSign).normalized;
                        AddOrientedTriangle(
                            vertices,
                            normals,
                            triangles,
                            new Vector3(
                                xSign * half,
                                ySign * inner,
                                zSign * inner),
                            new Vector3(
                                xSign * inner,
                                ySign * half,
                                zSign * inner),
                            new Vector3(
                                xSign * inner,
                                ySign * inner,
                                zSign * half),
                            normal);
                    }
                }
            }

            _beveledCubeMesh = new Mesh
            {
                name = "Procedural Beveled Block",
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                triangles = triangles.ToArray()
            };
            _beveledCubeMesh.RecalculateBounds();
            return _beveledCubeMesh;
        }

        private Mesh GetCrystalMesh()
        {
            if (_crystalMesh != null)
            {
                return _crystalMesh;
            }

            const int sides = 6;
            const float lowerRadius = 0.31f;
            const float upperRadius = 0.25f;
            var vertices = new List<Vector3>(sides * 14);
            var normals = new List<Vector3>(sides * 14);
            var triangles = new List<int>(sides * 15);
            var bottomTip = new Vector3(0f, -0.5f, 0f);
            var topTip = new Vector3(0f, 0.5f, 0f);
            for (var side = 0; side < sides; side++)
            {
                var next = (side + 1) % sides;
                var angle = Mathf.PI * 2f * side / sides;
                var nextAngle = Mathf.PI * 2f * next / sides;
                var middleAngle = (angle + nextAngle) * 0.5f;
                if (next == 0)
                {
                    middleAngle = angle + Mathf.PI / sides;
                }

                var lower = new Vector3(
                    Mathf.Cos(angle) * lowerRadius,
                    -0.24f,
                    Mathf.Sin(angle) * lowerRadius);
                var nextLower = new Vector3(
                    Mathf.Cos(nextAngle) * lowerRadius,
                    -0.24f,
                    Mathf.Sin(nextAngle) * lowerRadius);
                var upper = new Vector3(
                    Mathf.Cos(angle) * upperRadius,
                    0.19f,
                    Mathf.Sin(angle) * upperRadius);
                var nextUpper = new Vector3(
                    Mathf.Cos(nextAngle) * upperRadius,
                    0.19f,
                    Mathf.Sin(nextAngle) * upperRadius);
                var radial = new Vector3(
                    Mathf.Cos(middleAngle),
                    0f,
                    Mathf.Sin(middleAngle));

                AddOrientedTriangle(
                    vertices,
                    normals,
                    triangles,
                    bottomTip,
                    nextLower,
                    lower,
                    (radial + Vector3.down * 0.55f).normalized);
                AddOrientedQuad(
                    vertices,
                    normals,
                    triangles,
                    lower,
                    nextLower,
                    nextUpper,
                    upper,
                    radial);
                AddOrientedTriangle(
                    vertices,
                    normals,
                    triangles,
                    upper,
                    nextUpper,
                    topTip,
                    (radial + Vector3.up * 0.72f).normalized);
            }

            _crystalMesh = new Mesh
            {
                name = "Procedural Faceted Crystal",
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                triangles = triangles.ToArray()
            };
            _crystalMesh.RecalculateBounds();
            return _crystalMesh;
        }

        private static void AddOrientedQuad(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f)
            {
                (b, d) = (d, b);
            }

            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void AddOrientedTriangle(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f)
            {
                (b, c) = (c, b);
            }

            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private Mesh GetDrillConeMesh()
        {
            if (_drillConeMesh != null)
            {
                return _drillConeMesh;
            }

            const int sides = 10;
            var vertices = new Vector3[sides * 2 + 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[sides * 6];
            vertices[0] = new Vector3(0f, 0f, 0.5f);
            vertices[1] = new Vector3(0f, 0f, -0.5f);
            normals[0] = Vector3.forward;
            normals[1] = Vector3.back;
            for (var i = 0; i < sides; i++)
            {
                var angle = Mathf.PI * 2f * i / sides;
                var ring = new Vector3(
                    Mathf.Cos(angle) * 0.5f,
                    Mathf.Sin(angle) * 0.5f,
                    -0.5f);
                vertices[2 + i] = ring;
                vertices[2 + sides + i] = ring;
                normals[2 + i] = new Vector3(ring.x, ring.y, 0.5f).normalized;
                normals[2 + sides + i] = Vector3.back;

                var next = (i + 1) % sides;
                var triangle = i * 6;
                triangles[triangle] = 0;
                triangles[triangle + 1] = 2 + i;
                triangles[triangle + 2] = 2 + next;
                triangles[triangle + 3] = 1;
                triangles[triangle + 4] = 2 + sides + next;
                triangles[triangle + 5] = 2 + sides + i;
            }

            _drillConeMesh = new Mesh
            {
                name = "Procedural Low-Poly Drill Cone",
                vertices = vertices,
                normals = normals,
                triangles = triangles
            };
            _drillConeMesh.RecalculateBounds();
            return _drillConeMesh;
        }

        private static GameObject CreateMeshObject(
            string name,
            Transform parent,
            Mesh mesh,
            Material material)
        {
            var meshObject = new GameObject(name);
            meshObject.transform.SetParent(parent, false);
            meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return meshObject;
        }

        private IEnumerator ShrinkAndDestroy(GameObject target, float duration)
        {
            var startScale = target.transform.localScale;
            var elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                target.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            if (target != null)
            {
                Destroy(target);
            }
        }

        private static SpriteRenderer CreateWorldSprite(
            string name,
            Transform parent,
            Sprite sprite,
            float scale,
            int sortingOrder)
        {
            var spriteObject = new GameObject(name);
            spriteObject.transform.SetParent(parent, false);
            spriteObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            spriteObject.transform.localScale = Vector3.one * scale;
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static Sprite CreateAtlasSprite(
            Texture2D atlas,
            int column,
            int row,
            string name)
        {
            if (atlas == null)
            {
                return null;
            }

            atlas.wrapMode = TextureWrapMode.Clamp;
            atlas.filterMode = FilterMode.Bilinear;
            var width = atlas.width / 2;
            var height = atlas.height / 2;
            var sprite = Sprite.Create(
                atlas,
                new Rect(column * width, row * height, width, height),
                new Vector2(0.5f, 0.5f),
                500f,
                2u,
                SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Material material)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            var collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            return primitive;
        }

        private void ReleaseMaterials()
        {
            var materials = new[]
            {
                _floorMaterial,
                _softRockMaterial,
                _bedrockMaterial,
                _refineryMaterial,
                _dockMaterial,
                _refineryDarkMaterial,
                _outlineMaterial,
                _steelMaterial,
                _steelLightMaterial,
                _machineAccentMaterial,
                _rubberMaterial,
                _commonOreMaterial,
                _rareOreMaterial,
                _veryRareOreMaterial,
                _lampMaterial,
                _gridMaterial,
                _standardRouteMaterial,
                _safeRouteMaterial,
                _riskyRouteMaterial,
                _refineryNodeMaterial,
                _commonZoneMaterial,
                _rareZoneMaterial,
                _veryRareZoneMaterial,
                _validationFailureMaterial,
                _heatParticleMaterial
            };
            foreach (var material in materials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            _floorMaterial = null;
            _softRockMaterial = null;
            _bedrockMaterial = null;
            _refineryMaterial = null;
            _dockMaterial = null;
            _refineryDarkMaterial = null;
            _outlineMaterial = null;
            _steelMaterial = null;
            _steelLightMaterial = null;
            _machineAccentMaterial = null;
            _rubberMaterial = null;
            _commonOreMaterial = null;
            _rareOreMaterial = null;
            _veryRareOreMaterial = null;
            _lampMaterial = null;
            _gridMaterial = null;
            _standardRouteMaterial = null;
            _safeRouteMaterial = null;
            _riskyRouteMaterial = null;
            _refineryNodeMaterial = null;
            _commonZoneMaterial = null;
            _rareZoneMaterial = null;
            _veryRareZoneMaterial = null;
            _validationFailureMaterial = null;
            _heatParticleMaterial = null;
        }

        private static Material CreateCelMaterial(
            string name,
            Color baseColor,
            Color shadowColor,
            Color accentColor,
            float patternScale,
            float patternStrength,
            Color? emission = null,
            float stoneSurface = 0f)
        {
            var shader = Resources.Load<Shader>(
                "Shaders/DrillSnakeProceduralCel");
            if (shader == null)
            {
                shader = Shader.Find("DrillSnake/Procedural Cel");
            }

            if (shader == null)
            {
                return CreateMaterial(
                    name,
                    baseColor,
                    0f,
                    0f,
                    emission);
            }

            var material = new Material(shader)
            {
                name = name,
                enableInstancing = true
            };
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_ShadowColor", shadowColor);
            material.SetColor("_AccentColor", accentColor);
            material.SetFloat("_PatternScale", patternScale);
            material.SetFloat("_PatternStrength", patternStrength);
            material.SetFloat("_StoneSurface", stoneSurface);
            material.SetColor(
                "_EmissionColor",
                emission ?? Color.black);
            return material;
        }

        private static Material CreateMaterial(
            string name,
            Color color,
            float smoothness,
            float metallic,
            Color? emission = null,
            Texture2D texture = null,
            Vector2? textureScale = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                name = name,
                color = color
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Repeat;
                material.mainTexture = texture;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", texture);
                    material.SetTextureScale(
                        "_BaseMap",
                        textureScale ?? Vector2.one);
                }

                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", texture);
                    material.SetTextureScale(
                        "_MainTex",
                        textureScale ?? Vector2.one);
                }
            }

            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }

            return material;
        }

        private float SegmentHeight(int index)
        {
            if (_artMode == DrillSnakeArtMode.ProceduralCel)
            {
                return 0.1f;
            }

            return index == 0 ? 0.82f : 0.78f;
        }

        private void OnDestroy()
        {
            ReleaseMaterials();
            if (_beveledCubeMesh != null)
            {
                Destroy(_beveledCubeMesh);
                _beveledCubeMesh = null;
            }

            if (_crystalMesh != null)
            {
                Destroy(_crystalMesh);
                _crystalMesh = null;
            }

            if (_drillConeMesh != null)
            {
                Destroy(_drillConeMesh);
                _drillConeMesh = null;
            }
        }

        private static float HashAngle(Vector2Int cell)
        {
            var hash = cell.x * 73856093 ^ cell.y * 19349663;
            return Mathf.Abs(hash % 4) * 90f;
        }

        private static float Hash01(Vector2Int cell, int salt)
        {
            var hash = cell.x * 73856093 ^
                       cell.y * 19349663 ^
                       salt * 83492791;
            return Mathf.Abs(hash % 1000) / 999f;
        }
    }
}
