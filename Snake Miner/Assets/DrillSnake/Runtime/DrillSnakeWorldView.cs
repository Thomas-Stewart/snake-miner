using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrillSnake
{
    /// <summary>
    /// Runtime-built industrial mine presentation. Generated materials provide
    /// the hand-painted surface language while primitives keep the prototype
    /// dependency-light and deterministic.
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
        private Vector3 _recoilDirection;
        private float _recoilStartTime;
        private float _recoilDuration;
        private float _recoilDistance;

        public bool GridVisible => _gridVisible;

        public bool LevelDesignOverlayVisible => _levelDesignOverlayVisible;

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

        public void BuildWorld(DrillSnakeMap map)
        {
            _map = map;
            if (_worldRoot != null)
            {
                _worldRoot.gameObject.SetActive(false);
                Destroy(_worldRoot.gameObject);
            }

            _solidCells.Clear();
            _segmentViews.Clear();
            _recoilDuration = 0f;
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
                var view = CreateSegmentView(index);
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
            CreateSpriteAssets();
            if (_floorMaterial != null)
            {
                return;
            }

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
                "Painted Excavation Floor",
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
            refineryRoot.transform.position = GridToWorld(_map.Center, 0.42f);
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
            root.transform.position = GridToWorld(cell, 0.74f);
            CreateWorldSprite(
                "Painted Wall Lantern",
                root.transform,
                _lampSprite,
                1.35f,
                12);

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

        private SegmentView CreateSegmentView(int index)
        {
            var root = new GameObject(index == 0 ? "Drill Head" : $"Snake Segment {index}");
            root.transform.SetParent(_worldRoot, false);
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

        private static float SegmentHeight(int index)
        {
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
