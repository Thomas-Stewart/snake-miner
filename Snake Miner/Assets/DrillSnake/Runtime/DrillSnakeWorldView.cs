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
        private sealed class SegmentView
        {
            public GameObject Root;
            public Transform Artwork;
            public Vector3 StartPosition;
            public Vector3 TargetPosition;
            public Quaternion StartRotation;
            public Quaternion TargetRotation;
            public float MovementStart;
            public float MovementDuration;
        }

        private readonly Dictionary<Vector2Int, GameObject> _solidCells = new();
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
        private Sprite _headSprite;
        private Sprite _chassisSprite;
        private Sprite _cargoSprite;
        private Sprite _refinerySprite;
        private Sprite _commonOreSprite;
        private Sprite _rareOreSprite;
        private Sprite _veryRareOreSprite;
        private Sprite _lampSprite;
        private Mesh _drillConeMesh;
        private Vector3 _recoilDirection;
        private float _recoilStartTime;
        private float _recoilDuration;
        private float _recoilDistance;

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
            _segmentViews.Clear();
            _recoilDuration = 0f;
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

        public void RemoveDrilledCell(Vector2Int cell)
        {
            if (!_solidCells.Remove(cell, out var cellObject) || cellObject == null)
            {
                return;
            }

            StartCoroutine(ShrinkAndDestroy(cellObject, 0.09f));
        }

        public void PlayDrillRecoil(
            Vector2Int direction,
            float duration,
            float distance)
        {
            _recoilDirection = new Vector3(
                -direction.x,
                0f,
                -direction.y);
            _recoilStartTime = Time.time;
            _recoilDuration = Mathf.Max(0.05f, duration);
            _recoilDistance = Mathf.Max(0.05f, distance);
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
            }

            while (_segmentViews.Count < simulation.Segments.Count)
            {
                var index = _segmentViews.Count;
                var view = CreateSegmentView(index, simulation);
                var position = GridToWorld(simulation.Segments[index], SegmentHeight(index));
                view.Root.transform.position = position;
                view.StartPosition = position;
                view.TargetPosition = position;
                view.StartRotation = view.Root.transform.rotation;
                view.TargetRotation = view.Root.transform.rotation;
                _segmentViews.Add(view);
            }

            var now = Time.time;
            for (var i = 0; i < _segmentViews.Count; i++)
            {
                var view = _segmentViews[i];
                ApplyInterpolatedPose(view, now);

                var target = GridToWorld(simulation.Segments[i], SegmentHeight(i));
                view.StartPosition = view.Root.transform.position;
                view.TargetPosition = target;
                view.StartRotation = view.Root.transform.rotation;
                view.MovementStart = now;
                view.MovementDuration = movementDuration;

                var movement = target - view.StartPosition;
                movement.y = 0f;
                if (i == 0)
                {
                    var forward = new Vector3(
                        simulation.Direction.x,
                        0f,
                        simulation.Direction.y);
                    view.TargetRotation = Quaternion.LookRotation(forward, Vector3.up);
                }
                else if (movement.sqrMagnitude > 0.001f)
                {
                    view.TargetRotation = Quaternion.LookRotation(movement, Vector3.up);
                }

                if (movementDuration <= 0f)
                {
                    view.Root.transform.SetPositionAndRotation(
                        view.TargetPosition,
                        view.TargetRotation);
                    view.StartPosition = view.TargetPosition;
                    view.StartRotation = view.TargetRotation;
                }
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
            foreach (var view in _segmentViews)
            {
                if (view.Root == null || view.MovementDuration <= 0f)
                {
                    continue;
                }

                ApplyInterpolatedPose(view, now);
            }

            ApplyDrillRecoil(now);
        }

        private void ApplyDrillRecoil(float now)
        {
            var strength = 0f;
            if (_recoilDuration > 0f)
            {
                var progress = Mathf.Clamp01(
                    (now - _recoilStartTime) / _recoilDuration);
                if (progress < 0.2f)
                {
                    strength = Mathf.SmoothStep(0f, 1f, progress / 0.2f);
                }
                else
                {
                    strength = 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        (progress - 0.2f) / 0.8f);
                }

                if (progress >= 1f)
                {
                    _recoilDuration = 0f;
                }
            }

            var worldOffset = _recoilDirection * (_recoilDistance * strength);
            for (var index = 0; index < _segmentViews.Count; index++)
            {
                var view = _segmentViews[index];
                if (view.Root == null || view.Artwork == null)
                {
                    continue;
                }

                view.Artwork.localPosition = index == 0
                    ? view.Root.transform.InverseTransformVector(worldOffset)
                    : Vector3.zero;
            }
        }

        private static void ApplyInterpolatedPose(SegmentView view, float now)
        {
            if (view.Root == null)
            {
                return;
            }

            if (view.MovementDuration <= 0f)
            {
                view.Root.transform.SetPositionAndRotation(
                    view.TargetPosition,
                    view.TargetRotation);
                return;
            }

            var progress = Mathf.Clamp01(
                (now - view.MovementStart) /
                Mathf.Max(0.001f, view.MovementDuration));

            // Translation stays linear across logical ticks. Reapplying an
            // ease-in/ease-out curve per cell creates a visible stop-and-burst
            // cadence even at a high frame rate.
            var position = Vector3.LerpUnclamped(
                view.StartPosition,
                view.TargetPosition,
                progress);

            // Rotate early in the cell transition without changing the
            // authoritative grid path or introducing a movement turn radius.
            var rotationProgress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(progress * 3.5f));
            var rotation = Quaternion.Slerp(
                view.StartRotation,
                view.TargetRotation,
                rotationProgress);
            view.Root.transform.SetPositionAndRotation(position, rotation);
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
                    new Color(0.22f, 0.25f, 0.27f),
                    new Color(0.085f, 0.1f, 0.115f),
                    new Color(0.34f, 0.3f, 0.24f),
                    1.02f,
                    0.18f);
                _softRockMaterial = CreateCelMaterial(
                    "Cel Drillable Sandstone",
                    new Color(0.45f, 0.29f, 0.18f),
                    new Color(0.18f, 0.1f, 0.065f),
                    new Color(0.68f, 0.45f, 0.24f),
                    4.7f,
                    0.28f);
                _bedrockMaterial = CreateCelMaterial(
                    "Cel Basalt Bedrock",
                    new Color(0.16f, 0.22f, 0.3f),
                    new Color(0.035f, 0.055f, 0.09f),
                    new Color(0.27f, 0.38f, 0.52f),
                    5.4f,
                    0.24f);
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
                    new Color(0.23f, 0.29f, 0.32f),
                    new Color(0.045f, 0.065f, 0.075f),
                    new Color(0.43f, 0.5f, 0.52f),
                    6f,
                    0.14f);
                _steelLightMaterial = CreateCelMaterial(
                    "Cel Silver",
                    new Color(0.56f, 0.62f, 0.63f),
                    new Color(0.14f, 0.18f, 0.19f),
                    new Color(0.78f, 0.82f, 0.79f),
                    8f,
                    0.12f);
                _machineAccentMaterial = CreateCelMaterial(
                    "Cel Machine Orange",
                    new Color(0.92f, 0.4f, 0.055f),
                    new Color(0.3f, 0.09f, 0.015f),
                    new Color(1f, 0.68f, 0.1f),
                    4f,
                    0.22f,
                    new Color(0.08f, 0.018f, 0f));
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
                    new Color(0.08f, 0.62f, 1f),
                    new Color(0.015f, 0.1f, 0.3f),
                    new Color(0.35f, 0.94f, 1f),
                    7f,
                    0.24f,
                    new Color(0.015f, 0.2f, 0.52f));
                _veryRareOreMaterial = CreateCelMaterial(
                    "Cel Plasma Ore",
                    new Color(0.95f, 0.12f, 0.72f),
                    new Color(0.25f, 0.012f, 0.21f),
                    new Color(1f, 0.48f, 0.95f),
                    7f,
                    0.25f,
                    new Color(0.42f, 0.015f, 0.32f));
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
            floor.transform.localScale = new Vector3(9.2f, 1f, 6.2f);
        }

        private void CreateCellVisual(Vector2Int cell, DrillSnakeCellType type)
        {
            switch (type)
            {
                case DrillSnakeCellType.SoftRock:
                    CreateRock(cell, "Soft Rock", _softRockMaterial, 0.64f, 0.94f);
                    break;
                case DrillSnakeCellType.Bedrock:
                    CreateRock(cell, "Bedrock", _bedrockMaterial, 1f, 0.97f);
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
                CreateOutlinedBlock(
                    "Faceted Rock",
                    root.transform,
                    material,
                    new Vector3(0f, height * 0.39f, 0f),
                    new Vector3(
                        footprint + 0.015f,
                        height * (0.7f + Hash01(cell, 17) * 0.16f),
                        footprint + 0.015f),
                    0.035f);
                if (Hash01(cell, 41) > 0.58f)
                {
                    var cap = CreatePrimitive(
                        PrimitiveType.Cube,
                        "Angular Rock Cap",
                        root.transform,
                        material);
                    cap.transform.localPosition = new Vector3(
                        (Hash01(cell, 53) - 0.5f) * 0.23f,
                        height * 0.79f,
                        (Hash01(cell, 59) - 0.5f) * 0.23f);
                    cap.transform.localRotation = Quaternion.Euler(
                        0f,
                        HashAngle(cell) + 45f,
                        0f);
                    cap.transform.localScale = new Vector3(
                        0.42f,
                        0.1f + height * 0.08f,
                        0.42f);
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
            root.transform.position = GridToWorld(cell, 0.66f);
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
            var tile = CreatePrimitive(
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
                var bracket = CreatePrimitive(
                    PrimitiveType.Cube,
                    "Cel Lamp Bracket",
                    root.transform,
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
                if (index == 0)
                {
                    CreateProceduralDrillHead(artwork.transform);
                }
                else if (index < DrillSnakeSimulation.MinimumSegmentCount)
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
                    Root = root,
                    Artwork = artwork.transform
                };
            }

            var sprite = index == 0
                ? _headSprite
                : index < DrillSnakeSimulation.MinimumSegmentCount
                    ? _chassisSprite
                    : _cargoSprite;
            var scale = index == 0 ? 1.5f : 1.22f;
            var artworkRenderer = CreateWorldSprite(
                index == 0 ? "Painted Drill Vehicle" : "Painted Snake Module",
                root.transform,
                sprite,
                scale,
                24 - Mathf.Min(index, 12));

            return new SegmentView
            {
                Root = root,
                Artwork = artworkRenderer.transform
            };
        }

        private void CreateProceduralDrillHead(Transform parent)
        {
            CreateOutlinedBlock(
                "Drill Chassis",
                parent,
                _steelMaterial,
                new Vector3(0f, 0f, -0.05f),
                new Vector3(0.68f, 0.3f, 0.78f),
                0.055f);

            CreateTrack(parent, -0.43f);
            CreateTrack(parent, 0.43f);

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

            var stripe = CreatePrimitive(
                PrimitiveType.Cube,
                "Safety Stripe",
                parent,
                _machineAccentMaterial);
            stripe.transform.localPosition = new Vector3(0f, 0.19f, -0.04f);
            stripe.transform.localScale = new Vector3(0.48f, 0.055f, 0.22f);

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
                carriesOre ? _refineryDarkMaterial : _steelMaterial,
                Vector3.zero,
                new Vector3(0.68f, 0.27f, 0.7f),
                0.05f);
            CreateTrack(parent, -0.43f);
            CreateTrack(parent, 0.43f);

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
                    var crystal = CreatePrimitive(
                        PrimitiveType.Cube,
                        $"Cargo Crystal {i + 1}",
                        parent,
                        oreMaterial);
                    crystal.transform.localPosition = new Vector3(
                        (i - 1) * 0.19f,
                        0.23f + (i == 1 ? 0.07f : 0f),
                        i % 2 == 0 ? -0.04f : 0.05f);
                    crystal.transform.localRotation = Quaternion.Euler(
                        i == 1 ? 8f : -8f,
                        45f + i * 24f,
                        i == 1 ? -5f : 8f);
                    crystal.transform.localScale = new Vector3(
                        0.16f,
                        i == 1 ? 0.32f : 0.23f,
                        0.16f);
                }
            }
            else
            {
                var gear = CreatePrimitive(
                    PrimitiveType.Cylinder,
                    "Drive Gear",
                    parent,
                    _steelLightMaterial);
                gear.transform.localPosition = new Vector3(0f, 0.19f, 0f);
                gear.transform.localScale = new Vector3(0.22f, 0.055f, 0.22f);
            }
        }

        private void CreateTrack(Transform parent, float x)
        {
            var track = CreatePrimitive(
                PrimitiveType.Cube,
                x < 0f ? "Left Track" : "Right Track",
                parent,
                _rubberMaterial);
            track.transform.localPosition = new Vector3(x, -0.08f, -0.02f);
            track.transform.localScale = new Vector3(0.18f, 0.22f, 0.76f);

            for (var i = -1; i <= 1; i++)
            {
                var tread = CreatePrimitive(
                    PrimitiveType.Cube,
                    "Track Tread",
                    parent,
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
                var shard = CreatePrimitive(
                    PrimitiveType.Cube,
                    $"Cel Ore Shard {i + 1}",
                    parent,
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
                    i == 0 ? 0.24f : 0.18f,
                    height,
                    i == 0 ? 0.24f : 0.18f);
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

            var outline = CreatePrimitive(
                PrimitiveType.Cube,
                "Ink Silhouette",
                root.transform,
                _outlineMaterial ?? _refineryDarkMaterial);
            outline.transform.localPosition = new Vector3(0f, -outlineWidth, 0f);
            outline.transform.localScale = localScale +
                                           new Vector3(
                                               outlineWidth * 2f,
                                               outlineWidth,
                                               outlineWidth * 2f);

            var block = CreatePrimitive(
                PrimitiveType.Cube,
                "Cel Surface",
                root.transform,
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
                Destroy(collider);
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
                _validationFailureMaterial
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
        }

        private static Material CreateCelMaterial(
            string name,
            Color baseColor,
            Color shadowColor,
            Color accentColor,
            float patternScale,
            float patternStrength,
            Color? emission = null)
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
                return index == 0 ? 0.42f : 0.39f;
            }

            return index == 0 ? 0.82f : 0.78f;
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
