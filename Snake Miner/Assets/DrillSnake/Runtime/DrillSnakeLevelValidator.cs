using System.Collections.Generic;
using UnityEngine;

namespace DrillSnake
{
    public sealed class DrillSnakeValidationFailure
    {
        public DrillSnakeValidationFailure(
            string code,
            string message,
            IReadOnlyList<Vector2Int> cells = null)
        {
            Code = code;
            Message = message;
            Cells = cells ?? EmptyCells;
        }

        public string Code { get; }

        public string Message { get; }

        public IReadOnlyList<Vector2Int> Cells { get; }

        private static readonly IReadOnlyList<Vector2Int> EmptyCells =
            new List<Vector2Int>();
    }

    public sealed class DrillSnakeValidationReport
    {
        private readonly List<DrillSnakeValidationFailure> _failures = new();

        public bool IsValid => _failures.Count == 0;

        public IReadOnlyList<DrillSnakeValidationFailure> Failures => _failures;

        public float DiggableRatio { get; internal set; }

        public float DeadEndRatio { get; internal set; }

        public int SafeGraphCycleCount { get; internal set; }

        public int LargestEnclosedBedrockMass { get; internal set; }

        public string Summary => IsValid
            ? $"VALID  •  {DiggableRatio:P0} DIGGABLE  •  " +
              $"{SafeGraphCycleCount} SAFE LOOPS"
            : $"INVALID  •  {_failures.Count} FAILURE(S)";

        internal void Add(
            string code,
            string message,
            IReadOnlyList<Vector2Int> cells = null)
        {
            _failures.Add(new DrillSnakeValidationFailure(code, message, cells));
        }
    }

    /// <summary>
    /// Validates both graph topology and its rasterized tile representation.
    /// Structural validation runs before ore placement; full validation runs
    /// afterward and is the gate used by bounded seed rejection.
    /// </summary>
    public static class DrillSnakeLevelValidator
    {
        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public static DrillSnakeValidationReport Validate(
            DrillSnakeMap map,
            bool includeOreChecks = true)
        {
            var report = new DrillSnakeValidationReport
            {
                DiggableRatio = map.TraversableOrDiggableRatio
            };

            ValidatePresetRatio(map, report);
            ValidateDocks(map, report);
            ValidateGraphConnections(map, report);
            ValidateMajorChamberRoutes(map, report);
            ValidateRequiredCorridors(map, report);
            ValidateTurningChambers(map, report);
            ValidateMandatoryDeadEnds(map, report);
            ValidateLoopsAndBedrock(map, report);
            ValidateShortcut(map, report);
            ValidateRoomShape(map, report);
            ValidateDeadEnds(map, report);

            if (includeOreChecks)
            {
                ValidateOreTiers(map, report);
            }

            return report;
        }

        private static void ValidatePresetRatio(
            DrillSnakeMap map,
            DrillSnakeValidationReport report)
        {
            var ratio = map.TraversableOrDiggableRatio;
            if (ratio < map.Settings.MinimumDiggableRatio ||
                ratio > map.Settings.MaximumDiggableRatio)
            {
                report.Add(
                    "PRESET_RATIO",
                    $"{map.Settings.DisplayName} generated {ratio:P1} traversable or " +
                    $"diggable space; expected {map.Settings.MinimumDiggableRatio:P0}–" +
                    $"{map.Settings.MaximumDiggableRatio:P0}.");
            }
        }

        private static void ValidateDocks(
            DrillSnakeMap map,
            DrillSnakeValidationReport report)
        {
            if (map.Docks.Count != 4)
            {
                report.Add(
                    "DOCK_COUNT",
                    $"Expected four refinery docks, found {map.Docks.Count}.");
                return;
            }

            foreach (var dock in map.Docks)
            {
                var outward = new Vector2Int(
                    System.Math.Sign(dock.x - map.Center.x),
                    System.Math.Sign(dock.y - map.Center.y));
                var exterior = dock + outward;
                if (map.GetCell(dock) != DrillSnakeCellType.RefineryDock ||
                    !DrillSnakeMap.IsInitiallyNavigable(map.GetCell(exterior)))
                {
                    report.Add(
                        "DOCK_BLOCKED",
                        $"Refinery dock {dock} does not connect to an open graph route.",
                        new[] { dock, exterior });
                }
            }
        }

        private static void ValidateGraphConnections(
            DrillSnakeMap map,
            DrillSnakeValidationReport report)
        {
            foreach (var room in map.Graph.Rooms)
            {
                if (!room.MajorOuterRegion)
                {
                    continue;
                }

                var connections = map.Graph.GetConnectionCount(room.Id);
                if (connections < 2 && !(room.IsOrePocket && IsValidTurningRoom(room)))
                {
                    report.Add(
                        "OUTER_CONNECTIONS",
                        $"{room.Name} has {connections} graph connection(s); outer " +
                        "regions require at least two.");
                }
            }
        }

        private static void ValidateMajorChamberRoutes(
            DrillSnakeMap map,
            DrillSnakeValidationReport report)
        {
            var reachableRooms = GetGraphRoomsReachableFromRefinery(map.Graph);
            var reachableTiles = GetTilesReachableFromDocks(map);

            foreach (var room in map.Graph.Rooms)
            {
                if (!room.MajorOuterRegion)
                {
                    continue;
                }

                if (!reachableRooms.Contains(room.Id))
                {
                    report.Add(
                        "NO_GRAPH_RETURN",
                        $"{room.Name} has no safe graph route to the refinery.");
                }

                var independentReturns = CountIndependentSafeReturns(
                    map.Graph,
                    room.Id);
                if (independentReturns < 2 &&
                    !(room.IsOrePocket && IsValidTurningRoom(room)))
                {
                    report.Add(
                        "RETURN_REDUNDANCY",
                        $"{room.Name} has {independentReturns} independent safe " +
                        "return route(s); major chambers require two.");
                }

                var tileReachable = false;
                for (var y = room.Bounds.yMin; y < room.Bounds.yMax && !tileReachable; y++)
                {
                    for (var x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                    {
                        if (reachableTiles.Contains(new Vector2Int(x, y)))
                        {
                            tileReachable = true;
                            break;
                        }
                    }
                }

                if (!tileReachable)
                {
                    report.Add(
                        "NO_TILE_RETURN",
                        $"{room.Name} was disconnected while rasterizing the graph.",
                        new[] { room.Center });
                }
            }
        }

        private static void ValidateRequiredCorridors(
            DrillSnakeMap map,
            DrillSnakeValidationReport report)
        {
            foreach (var route in map.Graph.Routes)
            {
                if (!route.Required)
                {
                    continue;
                }

                foreach (var cell in route.RasterCells)
                {
                    var type = map.GetCell(cell);
                    if (type == DrillSnakeCellType.Bedrock ||
                        type == DrillSnakeCellType.SoftRock)
                    {
                        report.Add(
                            "CORRIDOR_BLOCKED",
                            $"Required route {route.Id} contains blocking {type} at {cell}.",
                            new[] { cell });
                        break;
                    }
                }
            }
        }

        private static void ValidateTurningChambers(
            DrillSnakeMap map,
            DrillSnakeValidationReport report)
        {
            foreach (var room in map.Graph.Rooms)
            {
                if (room.Kind == DrillSnakeRoomKind.Refinery)
                {
                    continue;
                }

                if (!IsValidTurningRoom(room))
                {
                    report.Add(
                        "CHAMBER_SIZE",
                        $"{room.Name} is {room.Bounds.width}x{room.Bounds.height}; " +
                        $"minimum is {room.MinimumTurningSize}x{room.MinimumTurningSize}.",
                        new[] { room.Center });
                    continue;
                }

                var blockedCell = FindBlockedTurningCoreCell(map, room);
                if (blockedCell.HasValue)
                {
                    report.Add(
                        "TURNING_CORE_BLOCKED",
                        $"{room.Name} does not retain a clear 3x3 turning core.",
                        new[] { blockedCell.Value });
                }
            }
        }

        private static void ValidateMandatoryDeadEnds(
            DrillSnakeMap map,
            DrillSnakeValidationReport report)
        {
            foreach (var room in map.Graph.Rooms)
            {
                if (room.Kind == DrillSnakeRoomKind.Refinery ||
                    CountOpenSafeConnections(map, room) > 1)
                {
                    continue;
                }

                var blockedCell = FindBlockedTurningCoreCell(map, room);
                if (blockedCell.HasValue)
                {
                    report.Add(
                        "MANDATORY_DEAD_END",
                        $"{room.Name} is a mandatory raster dead end without a " +
                        "clear turning core.",
                        new[] { blockedCell.Value });
                }
            }
        }

        private static void ValidateLoopsAndBedrock(
            DrillSnakeMap map,
            DrillSnakeValidationReport report)
        {
            var safeEdges = 0;
            foreach (var route in map.Graph.Routes)
            {
                if (route.Kind != DrillSnakeRouteKind.RiskySoftRockShortcut)
                {
                    safeEdges++;
                }
            }

            var safeCycles = Mathf.Max(
                0,
                safeEdges - map.Graph.Rooms.Count + 1);
            report.SafeGraphCycleCount = safeCycles;
            if (safeCycles < 1)
            {
                report.Add(
                    "NO_SAFE_LOOP",
                    "The safe route graph contains no loop.");
            }

            report.LargestEnclosedBedrockMass = FindLargestEnclosedBedrockMass(map);
            if (report.LargestEnclosedBedrockMass < 12)
            {
                report.Add(
                    "NO_BEDROCK_LOOP",
                    "No safe graph loop encloses a substantial bedrock island.");
            }
        }

        private static void ValidateShortcut(
            DrillSnakeMap map,
            DrillSnakeValidationReport report)
        {
            var shortcutFound = false;
            foreach (var route in map.Graph.Routes)
            {
                if (route.Kind != DrillSnakeRouteKind.RiskySoftRockShortcut)
                {
                    continue;
                }

                foreach (var cell in route.RasterCells)
                {
                    if (map.GetCell(cell) == DrillSnakeCellType.SoftRock)
                    {
                        shortcutFound = true;
                        break;
                    }
                }
            }

            if (!shortcutFound)
            {
                report.Add(
                    "NO_SHORTCUT",
                    "The level contains no optional soft-rock shortcut.");
            }
        }

        private static void ValidateRoomShape(
            DrillSnakeMap map,
            DrillSnakeValidationReport report)
        {
            var interiorCells = (map.Width - 2) * (map.Height - 2);
            var openCells =
                map.CountCells(DrillSnakeCellType.OpenFloor) +
                map.CountCells(DrillSnakeCellType.RefineryFloor) +
                map.CountCells(DrillSnakeCellType.RefineryDock);
            if (map.Graph.Rooms.Count < 9 || openCells / (float)interiorCells > 0.62f)
            {
                report.Add(
                    "GIANT_OPEN_ROOM",
                    "The rasterized level is too open to read as rooms and corridors.");
            }

            if (map.CountCells(DrillSnakeCellType.Bedrock) < interiorCells * 0.2f)
            {
                report.Add(
                    "BEDROCK_MASS",
                    "The level does not retain enough bedrock to separate routes.");
            }
        }

        private static void ValidateDeadEnds(
            DrillSnakeMap map,
            DrillSnakeValidationReport report)
        {
            var nonRefineryRooms = 0;
            var deadEnds = 0;
            foreach (var room in map.Graph.Rooms)
            {
                if (room.Kind == DrillSnakeRoomKind.Refinery)
                {
                    continue;
                }

                nonRefineryRooms++;
                if (map.Graph.GetConnectionCount(room.Id) <= 1)
                {
                    deadEnds++;
                }
            }

            report.DeadEndRatio = nonRefineryRooms == 0
                ? 1f
                : deadEnds / (float)nonRefineryRooms;
            if (report.DeadEndRatio > 0.2f)
            {
                report.Add(
                    "TOO_MANY_DEAD_ENDS",
                    $"{report.DeadEndRatio:P0} of rooms are dead ends.");
            }
        }

        private static void ValidateOreTiers(
            DrillSnakeMap map,
            DrillSnakeValidationReport report)
        {
            var commonCount = map.CountCells(DrillSnakeCellType.CommonOre);
            var rareCount = map.CountCells(DrillSnakeCellType.RareOre);
            var veryRareCount = map.CountCells(DrillSnakeCellType.VeryRareOre);
            if (commonCount < map.Settings.MinimumCommonOre)
            {
                report.Add(
                    "COMMON_ORE",
                    $"Common tier has {commonCount} ore; minimum is " +
                    $"{map.Settings.MinimumCommonOre}.");
            }

            if (rareCount < map.Settings.MinimumRareOre)
            {
                report.Add(
                    "RARE_ORE",
                    $"Rare tier has {rareCount} ore; minimum is " +
                    $"{map.Settings.MinimumRareOre}.");
            }

            if (veryRareCount < map.Settings.MinimumVeryRareOre)
            {
                report.Add(
                    "VERY_RARE_ORE",
                    $"Very-rare tier has {veryRareCount} ore; minimum is " +
                    $"{map.Settings.MinimumVeryRareOre}.");
            }

            var commonDistance = AverageOreDistance(map, DrillSnakeCellType.CommonOre);
            var rareDistance = AverageOreDistance(map, DrillSnakeCellType.RareOre);
            var veryRareDistance = AverageOreDistance(
                map,
                DrillSnakeCellType.VeryRareOre);
            if (!(commonDistance < rareDistance && rareDistance < veryRareDistance))
            {
                report.Add(
                    "ORE_DISTANCE",
                    "Ore values do not increase with graph distance from the refinery.");
            }
        }

        private static HashSet<int> GetGraphRoomsReachableFromRefinery(
            DrillSnakeLevelGraph graph)
        {
            var reachable = new HashSet<int> { graph.Refinery.Id };
            var queue = new Queue<int>();
            queue.Enqueue(graph.Refinery.Id);

            while (queue.Count > 0)
            {
                var roomId = queue.Dequeue();
                foreach (var route in graph.GetRoutesForRoom(roomId))
                {
                    if (route.Kind == DrillSnakeRouteKind.RiskySoftRockShortcut)
                    {
                        continue;
                    }

                    var otherId = graph.GetOtherRoomId(route, roomId);
                    if (reachable.Add(otherId))
                    {
                        queue.Enqueue(otherId);
                    }
                }
            }

            return reachable;
        }

        private static HashSet<Vector2Int> GetTilesReachableFromDocks(DrillSnakeMap map)
        {
            var reachable = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            foreach (var dock in map.Docks)
            {
                if (reachable.Add(dock))
                {
                    queue.Enqueue(dock);
                }
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var direction in CardinalDirections)
                {
                    var next = cell + direction;
                    if (!map.IsInBounds(next) ||
                        reachable.Contains(next) ||
                        !DrillSnakeMap.IsInitiallyNavigable(map.GetCell(next)))
                    {
                        continue;
                    }

                    reachable.Add(next);
                    queue.Enqueue(next);
                }
            }

            return reachable;
        }

        private static int CountIndependentSafeReturns(
            DrillSnakeLevelGraph graph,
            int roomId)
        {
            var count = 0;
            foreach (var route in graph.GetRoutesForRoom(roomId))
            {
                if (route.Kind == DrillSnakeRouteKind.RiskySoftRockShortcut)
                {
                    continue;
                }

                var neighborId = graph.GetOtherRoomId(route, roomId);
                if (CanReachRefineryWithoutRoom(graph, neighborId, roomId))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool CanReachRefineryWithoutRoom(
            DrillSnakeLevelGraph graph,
            int startingRoomId,
            int blockedRoomId)
        {
            var visited = new HashSet<int> { blockedRoomId, startingRoomId };
            var queue = new Queue<int>();
            queue.Enqueue(startingRoomId);
            while (queue.Count > 0)
            {
                var roomId = queue.Dequeue();
                if (roomId == graph.Refinery.Id)
                {
                    return true;
                }

                foreach (var route in graph.GetRoutesForRoom(roomId))
                {
                    if (route.Kind == DrillSnakeRouteKind.RiskySoftRockShortcut)
                    {
                        continue;
                    }

                    var otherId = graph.GetOtherRoomId(route, roomId);
                    if (visited.Add(otherId))
                    {
                        queue.Enqueue(otherId);
                    }
                }
            }

            return false;
        }

        private static int FindLargestEnclosedBedrockMass(DrillSnakeMap map)
        {
            var visited = new HashSet<Vector2Int>();
            var largest = 0;
            for (var y = 1; y < map.Height - 1; y++)
            {
                for (var x = 1; x < map.Width - 1; x++)
                {
                    var start = new Vector2Int(x, y);
                    if (visited.Contains(start) ||
                        map.GetCell(start) != DrillSnakeCellType.Bedrock)
                    {
                        continue;
                    }

                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(start);
                    visited.Add(start);
                    var size = 0;
                    var touchesBoundary = false;
                    while (queue.Count > 0)
                    {
                        var cell = queue.Dequeue();
                        size++;
                        if (cell.x <= 1 ||
                            cell.x >= map.Width - 2 ||
                            cell.y <= 1 ||
                            cell.y >= map.Height - 2)
                        {
                            touchesBoundary = true;
                        }

                        foreach (var direction in CardinalDirections)
                        {
                            var next = cell + direction;
                            if (!map.IsInBounds(next) ||
                                visited.Contains(next) ||
                                map.GetCell(next) != DrillSnakeCellType.Bedrock)
                            {
                                continue;
                            }

                            visited.Add(next);
                            queue.Enqueue(next);
                        }
                    }

                    if (!touchesBoundary)
                    {
                        largest = Mathf.Max(largest, size);
                    }
                }
            }

            return largest;
        }

        private static float AverageOreDistance(
            DrillSnakeMap map,
            DrillSnakeCellType oreType)
        {
            var total = 0f;
            var count = 0;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (map.GetCell(cell) == oreType)
                    {
                        total += map.GetGraphDistance(cell);
                        count++;
                    }
                }
            }

            return count == 0 ? float.MaxValue : total / count;
        }

        private static bool IsValidTurningRoom(DrillSnakeRoom room)
        {
            return room.Bounds.width >= room.MinimumTurningSize &&
                   room.Bounds.height >= room.MinimumTurningSize;
        }

        private static int CountOpenSafeConnections(
            DrillSnakeMap map,
            DrillSnakeRoom room)
        {
            var openConnections = 0;
            foreach (var route in map.Graph.GetRoutesForRoom(room.Id))
            {
                if (route.Kind == DrillSnakeRouteKind.RiskySoftRockShortcut)
                {
                    continue;
                }

                var open = true;
                foreach (var cell in route.RasterCells)
                {
                    if (room.Bounds.Contains(cell))
                    {
                        continue;
                    }

                    if (!DrillSnakeMap.IsInitiallyNavigable(map.GetCell(cell)))
                    {
                        open = false;
                        break;
                    }
                }

                if (open)
                {
                    openConnections++;
                }
            }

            return openConnections;
        }

        private static Vector2Int? FindBlockedTurningCoreCell(
            DrillSnakeMap map,
            DrillSnakeRoom room)
        {
            for (var y = room.Center.y - 1; y <= room.Center.y + 1; y++)
            {
                for (var x = room.Center.x - 1; x <= room.Center.x + 1; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!DrillSnakeMap.IsInitiallyNavigable(map.GetCell(cell)))
                    {
                        return cell;
                    }
                }
            }

            return null;
        }
    }
}
