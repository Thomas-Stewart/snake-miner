using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrillSnake
{
    /// <summary>
    /// Runtime-only graybox presentation. Every object is generated from
    /// primitives so the prototype has no asset assignment requirements.
    /// </summary>
    public sealed class DrillSnakeWorldView : MonoBehaviour
    {
        private sealed class SegmentView
        {
            public GameObject Root;
            public Renderer AccentRenderer;
            public Vector3 StartPosition;
            public Vector3 TargetPosition;
            public Quaternion TargetRotation;
            public float MovementStart;
            public float MovementDuration;
        }

        private readonly Dictionary<Vector2Int, GameObject> _solidCells = new();
        private readonly List<SegmentView> _segmentViews = new();
        private readonly Dictionary<DrillSnakeOreType, Material> _oreMaterials = new();

        private DrillSnakeMap _map;
        private Transform _worldRoot;
        private GameObject _gridOverlay;
        private Material _floorMaterial;
        private Material _softRockMaterial;
        private Material _bedrockMaterial;
        private Material _refineryMaterial;
        private Material _dockMaterial;
        private Material _headMaterial;
        private Material _chassisMaterial;
        private Material _trackMaterial;
        private Material _cargoFrameMaterial;
        private Material _gridMaterial;

        public bool GridVisible => _gridOverlay != null && _gridOverlay.activeSelf;

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

            CreateGridOverlay();
        }

        public void SetGridVisible(bool visible)
        {
            if (_gridOverlay != null)
            {
                _gridOverlay.SetActive(visible);
            }
        }

        public void ToggleGrid()
        {
            SetGridVisible(!GridVisible);
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
            }

            while (_segmentViews.Count < simulation.Segments.Count)
            {
                var index = _segmentViews.Count;
                var oreType = simulation.GetSegmentOreType(index);
                var view = CreateSegmentView(index, oreType);
                var position = GridToWorld(simulation.Segments[index], SegmentHeight(index));
                view.Root.transform.position = position;
                view.StartPosition = position;
                view.TargetPosition = position;
                view.TargetRotation = view.Root.transform.rotation;
                _segmentViews.Add(view);
            }

            var now = Time.time;
            for (var i = 0; i < _segmentViews.Count; i++)
            {
                var view = _segmentViews[i];
                if (view.MovementDuration > 0f &&
                    now >= view.MovementStart + view.MovementDuration)
                {
                    view.Root.transform.SetPositionAndRotation(
                        view.TargetPosition,
                        view.TargetRotation);
                }

                var target = GridToWorld(simulation.Segments[i], SegmentHeight(i));
                view.StartPosition = view.Root.transform.position;
                view.TargetPosition = target;
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

                // Heading changes snap at the authoritative cell center. Only
                // position is interpolated, so there is no visual turn radius.
                view.Root.transform.rotation = view.TargetRotation;

                if (view.AccentRenderer != null && i >= DrillSnakeSimulation.MinimumSegmentCount)
                {
                    view.AccentRenderer.sharedMaterial =
                        _oreMaterials[simulation.GetSegmentOreType(i)];
                }

                if (movementDuration <= 0f)
                {
                    view.Root.transform.SetPositionAndRotation(
                        view.TargetPosition,
                        view.TargetRotation);
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

                var t = Mathf.Clamp01(
                    (now - view.MovementStart) / Mathf.Max(0.01f, view.MovementDuration));
                var eased = t * t * (3f - 2f * t);
                view.Root.transform.SetPositionAndRotation(
                    Vector3.Lerp(view.StartPosition, view.TargetPosition, eased),
                    view.TargetRotation);
            }
        }

        private void CreateMaterials()
        {
            if (_floorMaterial != null)
            {
                return;
            }

            _floorMaterial = CreateMaterial(
                "Basalt Floor",
                new Color(0.055f, 0.07f, 0.085f),
                0.2f,
                0.05f);
            _softRockMaterial = CreateMaterial(
                "Soft Rock",
                new Color(0.28f, 0.22f, 0.18f),
                0.15f,
                0f);
            _bedrockMaterial = CreateMaterial(
                "Bedrock",
                new Color(0.095f, 0.11f, 0.14f),
                0.45f,
                0.25f);
            _refineryMaterial = CreateMaterial(
                "Refinery Floor",
                new Color(0.17f, 0.21f, 0.23f),
                0.65f,
                0.7f);
            _dockMaterial = CreateMaterial(
                "Refinery Dock",
                new Color(0.06f, 0.8f, 0.88f),
                0.75f,
                0.45f,
                new Color(0.02f, 0.35f, 0.45f));
            _headMaterial = CreateMaterial(
                "Drill Head",
                new Color(1f, 0.58f, 0.08f),
                0.55f,
                0.72f);
            _chassisMaterial = CreateMaterial(
                "Chassis",
                new Color(0.18f, 0.52f, 0.62f),
                0.5f,
                0.55f);
            _trackMaterial = CreateMaterial(
                "Tracks",
                new Color(0.035f, 0.045f, 0.055f),
                0.15f,
                0.2f);
            _cargoFrameMaterial = CreateMaterial(
                "Cargo Frame",
                new Color(0.24f, 0.28f, 0.3f),
                0.45f,
                0.65f);
            _gridMaterial = CreateMaterial(
                "Grid",
                new Color(0.1f, 0.7f, 0.72f, 0.36f),
                0f,
                0f,
                new Color(0.025f, 0.18f, 0.2f));

            _oreMaterials[DrillSnakeOreType.Common] = CreateMaterial(
                "Common Ore",
                new Color(0.2f, 0.86f, 0.38f),
                0.6f,
                0.25f,
                new Color(0.02f, 0.18f, 0.04f));
            _oreMaterials[DrillSnakeOreType.Rare] = CreateMaterial(
                "Rare Ore",
                new Color(0.16f, 0.52f, 1f),
                0.7f,
                0.35f,
                new Color(0.015f, 0.12f, 0.38f));
            _oreMaterials[DrillSnakeOreType.VeryRare] = CreateMaterial(
                "Very Rare Ore",
                new Color(0.93f, 0.22f, 1f),
                0.82f,
                0.42f,
                new Color(0.32f, 0.015f, 0.4f));
        }

        private void CreateBaseFloor()
        {
            var floor = CreatePrimitive(
                PrimitiveType.Plane,
                "Excavation Floor",
                _worldRoot,
                _floorMaterial);
            floor.transform.position = new Vector3(0f, -0.08f, 0f);
            floor.transform.localScale = new Vector3(
                _map.Width / 10f,
                1f,
                _map.Height / 10f);
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
            var rock = CreatePrimitive(
                PrimitiveType.Cube,
                $"{name} {cell.x},{cell.y}",
                _worldRoot,
                material);
            rock.transform.position = GridToWorld(cell, height * 0.5f);
            rock.transform.localScale = new Vector3(footprint, height, footprint);
            rock.transform.rotation = Quaternion.Euler(
                0f,
                HashAngle(cell),
                0f);
            _solidCells[cell] = rock;
        }

        private void CreateOre(Vector2Int cell, DrillSnakeOreType oreType)
        {
            var root = new GameObject($"{oreType} Ore {cell.x},{cell.y}");
            root.transform.SetParent(_worldRoot, false);
            root.transform.position = GridToWorld(cell);

            var baseRock = CreatePrimitive(
                PrimitiveType.Cube,
                "Ore Matrix",
                root.transform,
                _softRockMaterial);
            baseRock.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            baseRock.transform.localScale = new Vector3(0.92f, 0.55f, 0.92f);

            var crystal = CreatePrimitive(
                PrimitiveType.Sphere,
                "Exposed Ore",
                root.transform,
                _oreMaterials[oreType]);
            crystal.transform.localPosition = new Vector3(0f, 0.68f, 0f);
            crystal.transform.localScale = new Vector3(0.44f, 0.62f, 0.44f);
            crystal.transform.rotation = Quaternion.Euler(12f, HashAngle(cell), 8f);
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
                "Dock Beacon",
                _worldRoot,
                _dockMaterial);
            beacon.transform.position = GridToWorld(cell, 0.13f);
            beacon.transform.localScale = new Vector3(0.36f, 0.09f, 0.36f);
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

            _gridOverlay.SetActive(false);
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

        private SegmentView CreateSegmentView(int index, DrillSnakeOreType oreType)
        {
            var root = new GameObject(index == 0 ? "Drill Head" : $"Snake Segment {index}");
            root.transform.SetParent(_worldRoot, false);
            Renderer accentRenderer = null;

            if (index == 0)
            {
                var body = CreatePrimitive(
                    PrimitiveType.Cube,
                    "Drill Cab",
                    root.transform,
                    _headMaterial);
                body.transform.localPosition = new Vector3(0f, 0.36f, 0f);
                body.transform.localScale = new Vector3(0.72f, 0.48f, 0.76f);

                var drill = CreatePrimitive(
                    PrimitiveType.Cylinder,
                    "Drill Bit",
                    root.transform,
                    _headMaterial);
                drill.transform.localPosition = new Vector3(0f, 0.34f, 0.52f);
                drill.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                drill.transform.localScale = new Vector3(0.28f, 0.2f, 0.28f);
            }
            else if (index < DrillSnakeSimulation.MinimumSegmentCount)
            {
                var chassis = CreatePrimitive(
                    PrimitiveType.Cube,
                    "Permanent Chassis",
                    root.transform,
                    _chassisMaterial);
                chassis.transform.localPosition = new Vector3(0f, 0.31f, 0f);
                chassis.transform.localScale = new Vector3(0.65f, 0.4f, 0.72f);
                CreateTracks(root.transform);
            }
            else
            {
                var frame = CreatePrimitive(
                    PrimitiveType.Cube,
                    "Cargo Frame",
                    root.transform,
                    _cargoFrameMaterial);
                frame.transform.localPosition = new Vector3(0f, 0.27f, 0f);
                frame.transform.localScale = new Vector3(0.69f, 0.3f, 0.69f);
                CreateTracks(root.transform);

                var cargo = CreatePrimitive(
                    PrimitiveType.Sphere,
                    "Cargo Ore",
                    root.transform,
                    _oreMaterials[oreType]);
                cargo.transform.localPosition = new Vector3(0f, 0.62f, 0f);
                cargo.transform.localScale = new Vector3(0.48f, 0.58f, 0.48f);
                cargo.transform.localRotation = Quaternion.Euler(8f, 45f, 8f);
                accentRenderer = cargo.GetComponent<Renderer>();
            }

            return new SegmentView
            {
                Root = root,
                AccentRenderer = accentRenderer
            };
        }

        private void CreateTracks(Transform parent)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var track = CreatePrimitive(
                    PrimitiveType.Cube,
                    side < 0 ? "Left Track" : "Right Track",
                    parent,
                    _trackMaterial);
                track.transform.localPosition = new Vector3(side * 0.36f, 0.16f, 0f);
                track.transform.localScale = new Vector3(0.13f, 0.2f, 0.78f);
            }
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
            Color? emission = null)
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

            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }

            return material;
        }

        private static float SegmentHeight(int index)
        {
            return index == 0 ? 0.03f : 0.02f;
        }

        private static float HashAngle(Vector2Int cell)
        {
            var hash = cell.x * 73856093 ^ cell.y * 19349663;
            return Mathf.Abs(hash % 4) * 90f;
        }
    }
}
