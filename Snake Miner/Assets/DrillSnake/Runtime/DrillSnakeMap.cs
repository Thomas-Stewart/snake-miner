using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrillSnake
{
    /// <summary>
    /// Mutable authoritative tiles produced from an immutable room-and-route
    /// graph. Seed variation changes bounded room sizes, corridor bends, lane
    /// widths, and structured ore ordering; it never scatters arbitrary tiles.
    /// </summary>
    public sealed class DrillSnakeMap
    {
        public const int PrototypeSize = 45;
        public const int RefinerySize = 9;
        public const int SafeGenerationAttemptLimit = 12;

        private const int RefineryRoomId = 0;
        private const int InnerNorthRoomId = 1;
        private const int InnerEastRoomId = 2;
        private const int InnerSouthRoomId = 3;
        private const int InnerWestRoomId = 4;
        private const int OuterNorthRoomId = 5;
        private const int OuterNorthEastRoomId = 6;
        private const int OuterEastRoomId = 7;
        private const int OuterSouthEastRoomId = 8;
        private const int OuterSouthRoomId = 9;
        private const int OuterSouthWestRoomId = 10;
        private const int OuterWestRoomId = 11;
        private const int OuterNorthWestRoomId = 12;

        private readonly DrillSnakeCellType[,] _cells;
        private readonly int[,] _graphDistances;
        private readonly List<Vector2Int> _docks = new();
        private readonly HashSet<Vector2Int> _routeCells = new();
        private readonly List<DrillSnakeValidationFailure> _rejectedFailures = new();

        private DrillSnakeMap(
            int requestedSeed,
            int actualSeed,
            int generationAttempt,
            DrillSnakeLayoutPreset preset)
        {
            RequestedSeed = requestedSeed;
            Seed = actualSeed;
            GenerationAttempt = generationAttempt;
            Preset = preset;
            Settings = DrillSnakePresetSettings.For(preset);
            Width = PrototypeSize;
            Height = PrototypeSize;
            Center = new Vector2Int(Width / 2, Height / 2);
            _cells = new DrillSnakeCellType[Width, Height];
            _graphDistances = new int[Width, Height];
            Graph = new DrillSnakeLevelGraph();
        }

        public int RequestedSeed { get; }

        public int Seed { get; }

        public int GenerationAttempt { get; }

        public DrillSnakeLayoutPreset Preset { get; }

        public DrillSnakePresetSettings Settings { get; }

        public int Width { get; }

        public int Height { get; }

        public Vector2Int Center { get; }

        public IReadOnlyList<Vector2Int> Docks => _docks;

        public DrillSnakeLevelGraph Graph { get; }

        public DrillSnakeValidationReport ValidationReport { get; private set; }

        public IReadOnlyList<DrillSnakeValidationFailure> RejectedFailures =>
            _rejectedFailures;

        public float TraversableOrDiggableRatio { get; private set; }

        public static DrillSnakeMap Generate(int seed)
        {
            return Generate(seed, DrillSnakeLayoutPreset.MediumCrystalCaverns);
        }

        public static DrillSnakeMap Generate(int seed, DrillSnakeLayoutPreset preset)
        {
            var rejectedFailures = new List<DrillSnakeValidationFailure>();
            for (var attempt = 0; attempt < SafeGenerationAttemptLimit; attempt++)
            {
                var actualSeed = unchecked(seed + attempt * 104729);
                var map = new DrillSnakeMap(seed, actualSeed, attempt + 1, preset);
                map.BuildGraphFirstLayout();

                var structuralReport = DrillSnakeLevelValidator.Validate(map, false);
                if (!structuralReport.IsValid)
                {
                    rejectedFailures.AddRange(structuralReport.Failures);
                    continue;
                }

                map.PlaceOreByGraphDistance();
                map.ValidationReport = DrillSnakeLevelValidator.Validate(map, true);
                if (!map.ValidationReport.IsValid)
                {
                    rejectedFailures.AddRange(map.ValidationReport.Failures);
                    continue;
                }

                map._rejectedFailures.AddRange(rejectedFailures);
                return map;
            }

            var reason = rejectedFailures.Count > 0
                ? rejectedFailures[rejectedFailures.Count - 1].Message
                : "No candidate layout was produced.";
            throw new InvalidOperationException(
                $"Could not generate a valid {preset} layout after " +
                $"{SafeGenerationAttemptLimit} attempts. Last failure: {reason}");
        }

        public bool IsInBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }

        public DrillSnakeCellType GetCell(Vector2Int cell)
        {
            return IsInBounds(cell) ? _cells[cell.x, cell.y] : DrillSnakeCellType.Bedrock;
        }

        public void SetCell(Vector2Int cell, DrillSnakeCellType type)
        {
            if (IsInBounds(cell))
            {
                _cells[cell.x, cell.y] = type;
            }
        }

        public int CountCells(DrillSnakeCellType type)
        {
            var count = 0;
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (_cells[x, y] == type)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public bool IsRefinery(Vector2Int cell)
        {
            var type = GetCell(cell);
            return type == DrillSnakeCellType.RefineryFloor ||
                   type == DrillSnakeCellType.RefineryDock;
        }

        public int DistanceFromRefinery(Vector2Int cell)
        {
            return GetGraphDistance(cell);
        }

        public int GetGraphDistance(Vector2Int cell)
        {
            if (!IsInBounds(cell))
            {
                return int.MaxValue;
            }

            return _graphDistances[cell.x, cell.y];
        }

        public bool IsRequiredRouteCell(Vector2Int cell)
        {
            return _routeCells.Contains(cell);
        }

        public static bool IsNavigableOrDiggable(DrillSnakeCellType type)
        {
            return type != DrillSnakeCellType.Bedrock;
        }

        public static bool IsInitiallyNavigable(DrillSnakeCellType type)
        {
            return type == DrillSnakeCellType.OpenFloor ||
                   type == DrillSnakeCellType.RefineryFloor ||
                   type == DrillSnakeCellType.RefineryDock ||
                   type == DrillSnakeCellType.CommonOre ||
                   type == DrillSnakeCellType.RareOre ||
                   type == DrillSnakeCellType.VeryRareOre;
        }

        private void BuildGraphFirstLayout()
        {
            Fill(DrillSnakeCellType.Bedrock);
            BuildRoomGraph();
            RasterizeRooms();
            RasterizeRoutes();
            BuildRefineryDocks();
            ComputeRoomGraphDistances();
            FillUnusedSpaceFromGraph();
            PopulateCellGraphDistances();
            TraversableOrDiggableRatio = CalculateDiggableRatio();
        }

        private void BuildRoomGraph()
        {
            AddRoom(
                "Central Refinery",
                Center,
                RefinerySize,
                DrillSnakeRoomKind.Refinery,
                false,
                false,
                RefinerySize);

            var innerCenters = new[]
            {
                new Vector2Int(22, 31),
                new Vector2Int(31, 22),
                new Vector2Int(22, 13),
                new Vector2Int(13, 22)
            };
            var innerNames = new[]
            {
                "North Transfer Chamber",
                "East Transfer Chamber",
                "South Transfer Chamber",
                "West Transfer Chamber"
            };
            for (var i = 0; i < innerCenters.Length; i++)
            {
                var id = Graph.Rooms.Count;
                var size = StructuredRange(
                    Settings.InnerRoomMinimum,
                    Settings.InnerRoomMaximum,
                    id * 31 + 7);
                AddRoom(
                    innerNames[i],
                    innerCenters[i],
                    size,
                    DrillSnakeRoomKind.OreChamber,
                    false,
                    false,
                    Settings.InnerRoomMinimum);
            }

            var outerCenters = new[]
            {
                new Vector2Int(22, 39),
                new Vector2Int(37, 37),
                new Vector2Int(39, 22),
                new Vector2Int(37, 7),
                new Vector2Int(22, 5),
                new Vector2Int(7, 7),
                new Vector2Int(5, 22),
                new Vector2Int(7, 37)
            };
            var outerNames = new[]
            {
                "North Mining Chamber",
                "North-East Mining Chamber",
                "East Mining Chamber",
                "South-East Mining Chamber",
                "South Mining Chamber",
                "South-West Mining Chamber",
                "West Mining Chamber",
                "North-West Mining Chamber"
            };
            for (var i = 0; i < outerCenters.Length; i++)
            {
                var id = Graph.Rooms.Count;
                var size = StructuredRange(
                    Settings.OuterRoomMinimum,
                    Settings.OuterRoomMaximum,
                    id * 47 + 19);
                AddRoom(
                    outerNames[i],
                    outerCenters[i],
                    size,
                    DrillSnakeRoomKind.OreChamber,
                    true,
                    i % 2 == 1,
                    Settings.OuterRoomMinimum);
            }

            AddRoute(RefineryRoomId, InnerNorthRoomId, Settings.SpokeWidth);
            AddRoute(RefineryRoomId, InnerEastRoomId, Settings.SpokeWidth);
            AddRoute(RefineryRoomId, InnerSouthRoomId, Settings.SpokeWidth);
            AddRoute(RefineryRoomId, InnerWestRoomId, Settings.SpokeWidth);

            AddRoute(InnerNorthRoomId, OuterNorthRoomId, Settings.SpokeWidth);
            AddRoute(InnerEastRoomId, OuterEastRoomId, Settings.SpokeWidth);
            AddRoute(InnerSouthRoomId, OuterSouthRoomId, Settings.SpokeWidth);
            AddRoute(InnerWestRoomId, OuterWestRoomId, Settings.SpokeWidth);

            var outerCycle = new[]
            {
                OuterNorthRoomId,
                OuterNorthEastRoomId,
                OuterEastRoomId,
                OuterSouthEastRoomId,
                OuterSouthRoomId,
                OuterSouthWestRoomId,
                OuterWestRoomId,
                OuterNorthWestRoomId
            };
            var widthPhase = PositiveModulo(Seed, outerCycle.Length);
            for (var i = 0; i < outerCycle.Length; i++)
            {
                var width = (i + widthPhase) % 3 == 0
                    ? Settings.SecondaryRouteWidth
                    : Settings.OuterRouteWidth;
                AddRoute(
                    outerCycle[i],
                    outerCycle[(i + 1) % outerCycle.Length],
                    width,
                    DrillSnakeRouteKind.SafeLongRoute);
            }

            if (Settings.IncludeInnerLoop)
            {
                AddInnerLoopRoute(InnerNorthRoomId, InnerEastRoomId, new Vector2Int(31, 31));
                AddInnerLoopRoute(InnerEastRoomId, InnerSouthRoomId, new Vector2Int(31, 13));
                AddInnerLoopRoute(InnerSouthRoomId, InnerWestRoomId, new Vector2Int(13, 13));
                AddInnerLoopRoute(InnerWestRoomId, InnerNorthRoomId, new Vector2Int(13, 31));
            }

            AddRoute(
                InnerNorthRoomId,
                OuterNorthEastRoomId,
                1,
                DrillSnakeRouteKind.RiskySoftRockShortcut);
            AddRoute(
                InnerEastRoomId,
                OuterSouthEastRoomId,
                1,
                DrillSnakeRouteKind.RiskySoftRockShortcut);
            AddRoute(
                InnerSouthRoomId,
                OuterSouthWestRoomId,
                1,
                DrillSnakeRouteKind.RiskySoftRockShortcut);
            AddRoute(
                InnerWestRoomId,
                OuterNorthWestRoomId,
                1,
                DrillSnakeRouteKind.RiskySoftRockShortcut);
        }

        private void AddRoom(
            string name,
            Vector2Int center,
            int size,
            DrillSnakeRoomKind kind,
            bool majorOuterRegion,
            bool orePocket,
            int minimumTurningSize)
        {
            var bounds = new RectInt(
                center.x - size / 2,
                center.y - size / 2,
                size,
                size);
            Graph.AddRoom(new DrillSnakeRoom(
                Graph.Rooms.Count,
                name,
                bounds,
                kind,
                majorOuterRegion,
                orePocket,
                minimumTurningSize));
        }

        private void AddRoute(
            int roomAId,
            int roomBId,
            int width,
            DrillSnakeRouteKind kind = DrillSnakeRouteKind.Standard)
        {
            var roomA = Graph.GetRoom(roomAId);
            var roomB = Graph.GetRoom(roomBId);
            var waypoints = BuildOrthogonalWaypoints(
                roomA.Center,
                roomB.Center,
                Graph.Routes.Count);
            Graph.AddRoute(new DrillSnakeRoute(
                Graph.Routes.Count,
                roomAId,
                roomBId,
                width,
                kind,
                kind != DrillSnakeRouteKind.RiskySoftRockShortcut,
                waypoints));
        }

        private void AddInnerLoopRoute(int roomAId, int roomBId, Vector2Int bend)
        {
            Graph.AddRoute(new DrillSnakeRoute(
                Graph.Routes.Count,
                roomAId,
                roomBId,
                Settings.SecondaryRouteWidth,
                DrillSnakeRouteKind.SafeLongRoute,
                true,
                new[]
                {
                    Graph.GetRoom(roomAId).Center,
                    bend,
                    Graph.GetRoom(roomBId).Center
                }));
        }

        private IReadOnlyList<Vector2Int> BuildOrthogonalWaypoints(
            Vector2Int start,
            Vector2Int end,
            int routeId)
        {
            if (start.x == end.x || start.y == end.y)
            {
                return new[] { start, end };
            }

            var horizontalFirst = StructuredHash(routeId * 97 + 53) % 2 == 0;
            var bend = horizontalFirst
                ? new Vector2Int(end.x, start.y)
                : new Vector2Int(start.x, end.y);
            return new[] { start, bend, end };
        }

        private void RasterizeRooms()
        {
            foreach (var room in Graph.Rooms)
            {
                for (var y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
                {
                    for (var x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (!IsInterior(cell))
                        {
                            continue;
                        }

                        _cells[x, y] = room.Kind == DrillSnakeRoomKind.Refinery
                            ? DrillSnakeCellType.RefineryFloor
                            : DrillSnakeCellType.OpenFloor;
                    }
                }
            }
        }

        private void RasterizeRoutes()
        {
            foreach (var route in Graph.Routes)
            {
                for (var waypointIndex = 1;
                     waypointIndex < route.Waypoints.Count;
                     waypointIndex++)
                {
                    RasterizeSegment(
                        route,
                        route.Waypoints[waypointIndex - 1],
                        route.Waypoints[waypointIndex]);
                }
            }
        }

        private void RasterizeSegment(
            DrillSnakeRoute route,
            Vector2Int start,
            Vector2Int end)
        {
            var direction = new Vector2Int(
                Math.Sign(end.x - start.x),
                Math.Sign(end.y - start.y));
            var length = Mathf.Abs(end.x - start.x) + Mathf.Abs(end.y - start.y);
            var perpendicular = direction.x != 0 ? Vector2Int.up : Vector2Int.right;
            var firstOffset = -(route.Width / 2);

            for (var step = 0; step <= length; step++)
            {
                var centerCell = start + direction * step;
                for (var lane = 0; lane < route.Width; lane++)
                {
                    var cell = centerCell + perpendicular * (firstOffset + lane);
                    if (!IsInterior(cell))
                    {
                        continue;
                    }

                    route.AddRasterCell(cell);
                    _routeCells.Add(cell);
                    var current = _cells[cell.x, cell.y];
                    if (current == DrillSnakeCellType.RefineryFloor)
                    {
                        continue;
                    }

                    if (route.Kind == DrillSnakeRouteKind.RiskySoftRockShortcut)
                    {
                        if (current == DrillSnakeCellType.Bedrock)
                        {
                            _cells[cell.x, cell.y] = DrillSnakeCellType.SoftRock;
                        }
                    }
                    else
                    {
                        _cells[cell.x, cell.y] = DrillSnakeCellType.OpenFloor;
                    }
                }
            }
        }

        private void BuildRefineryDocks()
        {
            var half = RefinerySize / 2;
            _docks.Add(new Vector2Int(Center.x, Center.y + half + 1));
            _docks.Add(new Vector2Int(Center.x + half + 1, Center.y));
            _docks.Add(new Vector2Int(Center.x, Center.y - half - 1));
            _docks.Add(new Vector2Int(Center.x - half - 1, Center.y));
            foreach (var dock in _docks)
            {
                _cells[dock.x, dock.y] = DrillSnakeCellType.RefineryDock;
                _routeCells.Add(dock);
            }
        }

        private void ComputeRoomGraphDistances()
        {
            var unvisited = new HashSet<int>();
            foreach (var room in Graph.Rooms)
            {
                room.GraphDistance = room.Id == RefineryRoomId ? 0 : int.MaxValue;
                unvisited.Add(room.Id);
            }

            while (unvisited.Count > 0)
            {
                var currentId = -1;
                var currentDistance = int.MaxValue;
                foreach (var roomId in unvisited)
                {
                    var distance = Graph.GetRoom(roomId).GraphDistance;
                    if (distance < currentDistance)
                    {
                        currentDistance = distance;
                        currentId = roomId;
                    }
                }

                if (currentId < 0)
                {
                    break;
                }

                unvisited.Remove(currentId);
                foreach (var route in Graph.GetRoutesForRoom(currentId))
                {
                    if (route.Kind == DrillSnakeRouteKind.RiskySoftRockShortcut)
                    {
                        continue;
                    }

                    var otherId = Graph.GetOtherRoomId(route, currentId);
                    var candidateDistance = currentDistance + route.Length;
                    if (candidateDistance < Graph.GetRoom(otherId).GraphDistance)
                    {
                        Graph.GetRoom(otherId).GraphDistance = candidateDistance;
                    }
                }
            }

            var maximumDistance = 1;
            foreach (var room in Graph.Rooms)
            {
                if (room.GraphDistance < int.MaxValue)
                {
                    maximumDistance = Mathf.Max(maximumDistance, room.GraphDistance);
                }
            }

            foreach (var room in Graph.Rooms)
            {
                if (room.Kind == DrillSnakeRoomKind.Refinery)
                {
                    room.DistanceTier = DrillSnakeDistanceTier.Refinery;
                    continue;
                }

                var normalized = room.GraphDistance / (float)maximumDistance;
                room.DistanceTier = normalized <= 0.4f
                    ? DrillSnakeDistanceTier.Common
                    : normalized <= 0.72f
                        ? DrillSnakeDistanceTier.Rare
                        : DrillSnakeDistanceTier.VeryRare;
            }
        }

        private void FillUnusedSpaceFromGraph()
        {
            var distanceToGraph = BuildDistanceToGraph();
            var interiorCellCount = (Width - 2) * (Height - 2);
            var targetRatio =
                (Settings.MinimumDiggableRatio + Settings.MaximumDiggableRatio) * 0.5f;
            var targetCount = Mathf.RoundToInt(interiorCellCount * targetRatio);
            var currentCount = CountNavigableOrDiggableInteriorCells();

            var candidateOrbits = new Dictionary<int, List<Vector2Int>>();
            for (var y = 1; y < Height - 1; y++)
            {
                for (var x = 1; x < Width - 1; x++)
                {
                    if (_cells[x, y] == DrillSnakeCellType.Bedrock)
                    {
                        var cell = new Vector2Int(x, y);
                        var orbitKey = GetRotationalOrbitKey(cell);
                        if (!candidateOrbits.TryGetValue(orbitKey, out var orbit))
                        {
                            orbit = new List<Vector2Int>(4);
                            candidateOrbits.Add(orbitKey, orbit);
                        }

                        orbit.Add(cell);
                    }
                }
            }

            var orderedOrbits = new List<KeyValuePair<int, List<Vector2Int>>>(
                candidateOrbits);
            orderedOrbits.Sort((left, right) =>
            {
                var leftDistance = 0;
                foreach (var cell in left.Value)
                {
                    leftDistance = Mathf.Max(
                        leftDistance,
                        distanceToGraph[cell.x, cell.y]);
                }

                var rightDistance = 0;
                foreach (var cell in right.Value)
                {
                    rightDistance = Mathf.Max(
                        rightDistance,
                        distanceToGraph[cell.x, cell.y]);
                }

                var distanceComparison = leftDistance.CompareTo(rightDistance);
                if (distanceComparison != 0)
                {
                    return distanceComparison;
                }

                var leftDelta = left.Value[0] - Center;
                var rightDelta = right.Value[0] - Center;
                var leftRadius = Mathf.Max(
                    Mathf.Abs(leftDelta.x),
                    Mathf.Abs(leftDelta.y));
                var rightRadius = Mathf.Max(
                    Mathf.Abs(rightDelta.x),
                    Mathf.Abs(rightDelta.y));
                var radiusComparison = leftRadius.CompareTo(rightRadius);
                if (radiusComparison != 0)
                {
                    return radiusComparison;
                }

                // Within a complete proximity band, grow coherent diagonal
                // wedges before the cardinal edges. The key is only a final
                // spatial ordering; there is no random per-tile decision.
                var leftDiagonalOffset = Mathf.Abs(
                    Mathf.Abs(leftDelta.x) - Mathf.Abs(leftDelta.y));
                var rightDiagonalOffset = Mathf.Abs(
                    Mathf.Abs(rightDelta.x) - Mathf.Abs(rightDelta.y));
                var diagonalComparison = leftDiagonalOffset.CompareTo(
                    rightDiagonalOffset);
                return diagonalComparison != 0
                    ? diagonalComparison
                    : left.Key.CompareTo(right.Key);
            });

            foreach (var orbit in orderedOrbits)
            {
                if (currentCount >= targetCount)
                {
                    break;
                }

                // Fill complete 90-degree rotational orbits together. This
                // avoids the visible row sweep of a partial distance layer
                // while keeping all filler derived from graph proximity.
                if (currentCount + orbit.Value.Count > targetCount + 2)
                {
                    continue;
                }

                foreach (var cell in orbit.Value)
                {
                    _cells[cell.x, cell.y] = DrillSnakeCellType.SoftRock;
                    currentCount++;
                }
            }
        }

        private int GetRotationalOrbitKey(Vector2Int cell)
        {
            var maximumIndex = Width - 1;
            var rotated90 = new Vector2Int(maximumIndex - cell.y, cell.x);
            var rotated180 = new Vector2Int(
                maximumIndex - cell.x,
                maximumIndex - cell.y);
            var rotated270 = new Vector2Int(cell.y, maximumIndex - cell.x);

            return Mathf.Min(
                cell.y * Width + cell.x,
                rotated90.y * Width + rotated90.x,
                rotated180.y * Width + rotated180.x,
                rotated270.y * Width + rotated270.x);
        }

        private int[,] BuildDistanceToGraph()
        {
            var distances = new int[Width, Height];
            var queue = new Queue<Vector2Int>();
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (_cells[x, y] != DrillSnakeCellType.Bedrock)
                    {
                        distances[x, y] = 0;
                        queue.Enqueue(new Vector2Int(x, y));
                    }
                    else
                    {
                        distances[x, y] = int.MaxValue;
                    }
                }
            }

            var directions = CardinalDirections;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                var nextDistance = distances[cell.x, cell.y] + 1;
                foreach (var direction in directions)
                {
                    var next = cell + direction;
                    if (!IsInBounds(next) ||
                        nextDistance >= distances[next.x, next.y])
                    {
                        continue;
                    }

                    distances[next.x, next.y] = nextDistance;
                    queue.Enqueue(next);
                }
            }

            return distances;
        }

        private void PopulateCellGraphDistances()
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var bestDistance = int.MaxValue;
                    foreach (var room in Graph.Rooms)
                    {
                        if (room.GraphDistance == int.MaxValue)
                        {
                            continue;
                        }

                        var candidate =
                            room.GraphDistance +
                            Mathf.Abs(cell.x - room.Center.x) +
                            Mathf.Abs(cell.y - room.Center.y);
                        bestDistance = Mathf.Min(bestDistance, candidate);
                    }

                    _graphDistances[x, y] = bestDistance;
                }
            }

            foreach (var room in Graph.Rooms)
            {
                for (var y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
                {
                    for (var x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                    {
                        if (x >= 0 && x < Width && y >= 0 && y < Height)
                        {
                            _graphDistances[x, y] = room.GraphDistance;
                        }
                    }
                }
            }

            foreach (var route in Graph.Routes)
            {
                var roomA = Graph.GetRoom(route.RoomAId);
                var roomB = Graph.GetRoom(route.RoomBId);
                foreach (var cell in route.RasterCells)
                {
                    var fromA =
                        roomA.GraphDistance +
                        Mathf.Abs(cell.x - roomA.Center.x) +
                        Mathf.Abs(cell.y - roomA.Center.y);
                    var fromB =
                        roomB.GraphDistance +
                        Mathf.Abs(cell.x - roomB.Center.x) +
                        Mathf.Abs(cell.y - roomB.Center.y);
                    _graphDistances[cell.x, cell.y] = Mathf.Min(fromA, fromB);
                }
            }
        }

        private void PlaceOreByGraphDistance()
        {
            foreach (var room in Graph.Rooms)
            {
                if (room.DistanceTier == DrillSnakeDistanceTier.Refinery)
                {
                    continue;
                }

                var oreType = room.DistanceTier switch
                {
                    DrillSnakeDistanceTier.Common => DrillSnakeCellType.CommonOre,
                    DrillSnakeDistanceTier.Rare => DrillSnakeCellType.RareOre,
                    DrillSnakeDistanceTier.VeryRare => DrillSnakeCellType.VeryRareOre,
                    _ => DrillSnakeCellType.CommonOre
                };
                var targetCount = room.DistanceTier switch
                {
                    DrillSnakeDistanceTier.Common => Settings.CommonOrePerRoom,
                    DrillSnakeDistanceTier.Rare => Settings.RareOrePerRoom,
                    DrillSnakeDistanceTier.VeryRare => Settings.VeryRareOrePerRoom,
                    _ => 0
                };
                PlaceStructuredOreRing(room, oreType, targetCount);
            }
        }

        private void PlaceStructuredOreRing(
            DrillSnakeRoom room,
            DrillSnakeCellType oreType,
            int targetCount)
        {
            var candidates = new List<Vector2Int>();
            for (var y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
            {
                for (var x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var edgeDistance = Mathf.Min(
                        Mathf.Min(x - room.Bounds.xMin, room.Bounds.xMax - 1 - x),
                        Mathf.Min(y - room.Bounds.yMin, room.Bounds.yMax - 1 - y));
                    if (_cells[x, y] == DrillSnakeCellType.OpenFloor &&
                        !_routeCells.Contains(cell) &&
                        edgeDistance <= 1 &&
                        Mathf.Max(
                            Mathf.Abs(x - room.Center.x),
                            Mathf.Abs(y - room.Center.y)) >= 2)
                    {
                        candidates.Add(cell);
                    }
                }
            }

            candidates.Sort((left, right) =>
            {
                var leftAngle = PerimeterOrder(room, left);
                var rightAngle = PerimeterOrder(room, right);
                return leftAngle.CompareTo(rightAngle);
            });

            if (candidates.Count == 0)
            {
                return;
            }

            var start = PositiveModulo(
                StructuredHash(room.Id * 131 + 29),
                candidates.Count);
            var stride = candidates.Count > 5 ? 3 : 1;
            var placed = 0;
            var visited = new HashSet<int>();
            for (var index = start;
                 placed < targetCount && visited.Count < candidates.Count;
                 index = PositiveModulo(index + stride, candidates.Count))
            {
                if (!visited.Add(index))
                {
                    stride = 1;
                    continue;
                }

                var cell = candidates[index];
                _cells[cell.x, cell.y] = oreType;
                placed++;
            }
        }

        private float CalculateDiggableRatio()
        {
            var interiorCellCount = (Width - 2) * (Height - 2);
            return CountNavigableOrDiggableInteriorCells() / (float)interiorCellCount;
        }

        private int CountNavigableOrDiggableInteriorCells()
        {
            var count = 0;
            for (var y = 1; y < Height - 1; y++)
            {
                for (var x = 1; x < Width - 1; x++)
                {
                    if (IsNavigableOrDiggable(_cells[x, y]))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private void Fill(DrillSnakeCellType type)
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    _cells[x, y] = type;
                    _graphDistances[x, y] = int.MaxValue;
                }
            }
        }

        private bool IsInterior(Vector2Int cell)
        {
            return cell.x > 0 &&
                   cell.x < Width - 1 &&
                   cell.y > 0 &&
                   cell.y < Height - 1;
        }

        private int StructuredRange(int minimum, int maximum, int salt)
        {
            return minimum + PositiveModulo(
                StructuredHash(salt),
                maximum - minimum + 1);
        }

        private int StructuredHash(int salt)
        {
            unchecked
            {
                var value = Seed ^ (salt * 16777619);
                value ^= value >> 16;
                value *= 0x7feb352d;
                value ^= value >> 15;
                value *= 0x6a09e667;
                value ^= value >> 16;
                return value;
            }
        }

        private static int PerimeterOrder(DrillSnakeRoom room, Vector2Int cell)
        {
            var relativeX = cell.x - room.Bounds.xMin;
            var relativeY = cell.y - room.Bounds.yMin;
            if (relativeY == room.Bounds.height - 1)
            {
                return relativeX;
            }

            if (relativeX == room.Bounds.width - 1)
            {
                return room.Bounds.width + (room.Bounds.height - 1 - relativeY);
            }

            if (relativeY == 0)
            {
                return room.Bounds.width + room.Bounds.height +
                       (room.Bounds.width - 1 - relativeX);
            }

            return room.Bounds.width * 2 + room.Bounds.height +
                   relativeY;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            if (modulus <= 0)
            {
                return 0;
            }

            var result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };
    }
}
