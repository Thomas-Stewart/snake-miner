using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DrillSnake
{
    public sealed class DrillSnakeLengthDiagnostic
    {
        internal DrillSnakeLengthDiagnostic(int snakeLength)
        {
            SnakeLength = snakeLength;
        }

        public int SnakeLength { get; }

        public float AccessibleFloorPercentage { get; internal set; }

        public int ReachableOreChambers { get; internal set; }

        public int ViableReturnRoutes { get; internal set; }

        public int MinimumRouteWidth { get; internal set; }

        public int TurningChambers { get; internal set; }
    }

    public sealed class DrillSnakeDiagnosticReport
    {
        private readonly List<DrillSnakeLengthDiagnostic> _lengthReports = new();

        internal DrillSnakeDiagnosticReport(DrillSnakeMap map)
        {
            Preset = map.Preset;
            Seed = map.Seed;
        }

        public DrillSnakeLayoutPreset Preset { get; }

        public int Seed { get; }

        public IReadOnlyList<DrillSnakeLengthDiagnostic> LengthReports =>
            _lengthReports;

        internal void Add(DrillSnakeLengthDiagnostic report)
        {
            _lengthReports.Add(report);
        }

        public string ToConsoleString()
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                $"DRILL SNAKE DESIGN DIAGNOSTIC — {Preset} — SEED {Seed}");
            builder.AppendLine(
                "Length | Accessible | Ore chambers | Return routes | " +
                "Min width | Turning chambers");
            foreach (var report in _lengthReports)
            {
                builder.AppendLine(
                    $"{report.SnakeLength,6} | " +
                    $"{report.AccessibleFloorPercentage,9:0.0}% | " +
                    $"{report.ReachableOreChambers,12} | " +
                    $"{report.ViableReturnRoutes,13} | " +
                    $"{report.MinimumRouteWidth,9} | " +
                    $"{report.TurningChambers,16}");
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Design-only diagnostic that pathfinds through the room graph, converts
    /// candidate routes to exact grid walks, and drives a fixed-length virtual
    /// body through each outbound/return pair. It does not affect generation or
    /// runtime game rules.
    /// </summary>
    public static class DrillSnakeDesignDiagnostics
    {
        private const int MaximumPathsPerTarget = 12;
        private const int MaximumPathSearchStates = 5000;
        private const int MaximumBodyTrailSearchStates = 500000;

        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        private sealed class GraphPath
        {
            public readonly List<int> RoomIds = new();
            public readonly List<DrillSnakeRoute> Routes = new();
            public int Cost;

            public GraphPath Clone()
            {
                var clone = new GraphPath
                {
                    Cost = Cost
                };
                clone.RoomIds.AddRange(RoomIds);
                clone.Routes.AddRange(Routes);
                return clone;
            }
        }

        public static DrillSnakeDiagnosticReport Analyze(
            DrillSnakeMap map,
            IReadOnlyList<int> snakeLengths = null)
        {
            var lengths = snakeLengths ?? new[] { 5, 15, 30, 60 };
            var report = new DrillSnakeDiagnosticReport(map);
            foreach (var snakeLength in lengths)
            {
                report.Add(AnalyzeLength(map, Mathf.Max(2, snakeLength)));
            }

            return report;
        }

        private static DrillSnakeLengthDiagnostic AnalyzeLength(
            DrillSnakeMap map,
            int snakeLength)
        {
            var diagnostic = new DrillSnakeLengthDiagnostic(snakeLength);
            var accessibleCells = new HashSet<Vector2Int>();
            var successfulRooms = new HashSet<int>();
            var successfulReturnPaths = new HashSet<string>();
            var successfulRouteIds = new HashSet<int>();

            AddRoomCells(map, map.Graph.Refinery, accessibleCells);
            foreach (var targetRoom in map.Graph.Rooms)
            {
                if (targetRoom.Kind != DrillSnakeRoomKind.OreChamber)
                {
                    continue;
                }

                var outboundPaths = FindGraphPaths(
                    map.Graph,
                    map.Graph.Refinery.Id,
                    targetRoom.Id);
                var returnPaths = FindGraphPaths(
                    map.Graph,
                    targetRoom.Id,
                    map.Graph.Refinery.Id);
                var roomReachable = false;

                foreach (var outbound in outboundPaths)
                {
                    var outboundCells = TrimOutboundToDock(
                        map,
                        BuildGridPath(map.Graph, outbound));
                    if (outboundCells.Count == 0 ||
                        !TryBuildInitialBody(
                            map,
                            outboundCells[0],
                            snakeLength,
                            out var initialBody))
                    {
                        continue;
                    }

                    foreach (var returnPath in returnPaths)
                    {
                        var returnCells = TrimReturnAtDock(
                            map,
                            BuildGridPath(map.Graph, returnPath));
                        var combinedWalk = CombineWalks(outboundCells, returnCells);
                        if (!CanDrive(initialBody, combinedWalk))
                        {
                            continue;
                        }

                        roomReachable = true;
                        var returnKey = BuildReturnKey(targetRoom.Id, returnPath);
                        successfulReturnPaths.Add(returnKey);
                        AddPathAccess(map, outbound, accessibleCells, successfulRouteIds);
                        AddPathAccess(
                            map,
                            returnPath,
                            accessibleCells,
                            successfulRouteIds);
                    }
                }

                if (roomReachable)
                {
                    successfulRooms.Add(targetRoom.Id);
                }
            }

            var accessibleTotal = CountDiggableInteriorCells(map);
            diagnostic.AccessibleFloorPercentage = accessibleTotal == 0
                ? 0f
                : accessibleCells.Count * 100f / accessibleTotal;
            diagnostic.ReachableOreChambers = successfulRooms.Count;
            diagnostic.ViableReturnRoutes = successfulReturnPaths.Count;
            diagnostic.MinimumRouteWidth = GetMinimumRouteWidth(
                map.Graph,
                successfulRouteIds);
            diagnostic.TurningChambers = CountTurningChambers(map, snakeLength);
            return diagnostic;
        }

        private static List<GraphPath> FindGraphPaths(
            DrillSnakeLevelGraph graph,
            int startRoomId,
            int targetRoomId)
        {
            var results = new List<GraphPath>();
            var frontier = new List<GraphPath>();
            var initial = new GraphPath();
            initial.RoomIds.Add(startRoomId);
            frontier.Add(initial);
            var searchedStates = 0;

            while (frontier.Count > 0 &&
                   results.Count < MaximumPathsPerTarget &&
                   searchedStates < MaximumPathSearchStates)
            {
                frontier.Sort(CompareGraphPaths);
                var current = frontier[0];
                frontier.RemoveAt(0);
                searchedStates++;

                var roomId = current.RoomIds[current.RoomIds.Count - 1];
                if (roomId == targetRoomId)
                {
                    results.Add(current);
                    continue;
                }

                foreach (var route in graph.GetRoutesForRoom(roomId))
                {
                    var otherId = graph.GetOtherRoomId(route, roomId);
                    if (current.RoomIds.Contains(otherId))
                    {
                        continue;
                    }

                    var next = current.Clone();
                    next.RoomIds.Add(otherId);
                    next.Routes.Add(route);
                    next.Cost += route.Length +
                                 (route.Kind ==
                                  DrillSnakeRouteKind.RiskySoftRockShortcut
                                     ? 2
                                     : 0);
                    frontier.Add(next);
                }
            }

            return results;
        }

        private static int CompareGraphPaths(GraphPath left, GraphPath right)
        {
            var costComparison = left.Cost.CompareTo(right.Cost);
            if (costComparison != 0)
            {
                return costComparison;
            }

            return left.RoomIds.Count.CompareTo(right.RoomIds.Count);
        }

        private static List<Vector2Int> BuildGridPath(
            DrillSnakeLevelGraph graph,
            GraphPath path)
        {
            var cells = new List<Vector2Int>
            {
                graph.GetRoom(path.RoomIds[0]).Center
            };
            var currentRoomId = path.RoomIds[0];
            foreach (var route in path.Routes)
            {
                if (route.RoomAId == currentRoomId)
                {
                    AppendWaypoints(cells, route.Waypoints, false);
                    currentRoomId = route.RoomBId;
                }
                else
                {
                    AppendWaypoints(cells, route.Waypoints, true);
                    currentRoomId = route.RoomAId;
                }
            }

            return cells;
        }

        private static void AppendWaypoints(
            List<Vector2Int> cells,
            IReadOnlyList<Vector2Int> waypoints,
            bool reverse)
        {
            if (!reverse)
            {
                for (var i = 1; i < waypoints.Count; i++)
                {
                    AppendSegment(cells, waypoints[i - 1], waypoints[i]);
                }

                return;
            }

            for (var i = waypoints.Count - 1; i > 0; i--)
            {
                AppendSegment(cells, waypoints[i], waypoints[i - 1]);
            }
        }

        private static void AppendSegment(
            List<Vector2Int> cells,
            Vector2Int start,
            Vector2Int end)
        {
            var direction = new Vector2Int(
                System.Math.Sign(end.x - start.x),
                System.Math.Sign(end.y - start.y));
            var length = Mathf.Abs(end.x - start.x) + Mathf.Abs(end.y - start.y);
            for (var step = 1; step <= length; step++)
            {
                var cell = start + direction * step;
                if (cells[cells.Count - 1] != cell)
                {
                    cells.Add(cell);
                }
            }
        }

        private static List<Vector2Int> TrimOutboundToDock(
            DrillSnakeMap map,
            List<Vector2Int> cells)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                if (map.GetCell(cells[i]) == DrillSnakeCellType.RefineryDock)
                {
                    return cells.GetRange(i, cells.Count - i);
                }
            }

            return new List<Vector2Int>();
        }

        private static List<Vector2Int> TrimReturnAtDock(
            DrillSnakeMap map,
            List<Vector2Int> cells)
        {
            for (var i = 1; i < cells.Count; i++)
            {
                if (map.GetCell(cells[i]) == DrillSnakeCellType.RefineryDock)
                {
                    return cells.GetRange(0, i + 1);
                }
            }

            return new List<Vector2Int>();
        }

        private static List<Vector2Int> CombineWalks(
            List<Vector2Int> outbound,
            List<Vector2Int> returning)
        {
            var combined = new List<Vector2Int>(outbound);
            for (var i = 1; i < returning.Count; i++)
            {
                combined.Add(returning[i]);
            }

            return combined;
        }

        private static bool TryBuildInitialBody(
            DrillSnakeMap map,
            Vector2Int head,
            int snakeLength,
            out List<Vector2Int> bodyTailToHead)
        {
            var headToTail = new List<Vector2Int> { head };
            var visited = new HashSet<Vector2Int> { head };
            var searchedStates = 0;
            var found = SearchBodyTrail(
                map,
                head,
                snakeLength,
                headToTail,
                visited,
                ref searchedStates);

            bodyTailToHead = new List<Vector2Int>();
            if (!found)
            {
                return false;
            }

            for (var i = headToTail.Count - 1; i >= 0; i--)
            {
                bodyTailToHead.Add(headToTail[i]);
            }

            return true;
        }

        private static bool SearchBodyTrail(
            DrillSnakeMap map,
            Vector2Int current,
            int targetLength,
            List<Vector2Int> path,
            HashSet<Vector2Int> visited,
            ref int searchedStates)
        {
            if (path.Count >= targetLength)
            {
                return true;
            }

            if (++searchedStates > MaximumBodyTrailSearchStates)
            {
                return false;
            }

            var candidates = new List<Vector2Int>();
            foreach (var direction in CardinalDirections)
            {
                var next = current + direction;
                if (!visited.Contains(next) && map.IsRefinery(next))
                {
                    candidates.Add(next);
                }
            }

            candidates.Sort((left, right) =>
            {
                var degreeComparison = CountUnvisitedRefineryNeighbors(
                        map,
                        left,
                        visited)
                    .CompareTo(CountUnvisitedRefineryNeighbors(map, right, visited));
                if (degreeComparison != 0)
                {
                    return degreeComparison;
                }

                var rowComparison = left.y.CompareTo(right.y);
                return rowComparison != 0
                    ? rowComparison
                    : left.x.CompareTo(right.x);
            });

            foreach (var next in candidates)
            {
                visited.Add(next);
                path.Add(next);
                if (SearchBodyTrail(
                        map,
                        next,
                        targetLength,
                        path,
                        visited,
                        ref searchedStates))
                {
                    return true;
                }

                path.RemoveAt(path.Count - 1);
                visited.Remove(next);
            }

            return false;
        }

        private static int CountUnvisitedRefineryNeighbors(
            DrillSnakeMap map,
            Vector2Int cell,
            HashSet<Vector2Int> visited)
        {
            var count = 0;
            foreach (var direction in CardinalDirections)
            {
                var next = cell + direction;
                if (!visited.Contains(next) && map.IsRefinery(next))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool CanDrive(
            List<Vector2Int> initialBodyTailToHead,
            List<Vector2Int> walk)
        {
            if (walk.Count == 0 ||
                initialBodyTailToHead.Count == 0 ||
                walk[0] !=
                initialBodyTailToHead[initialBodyTailToHead.Count - 1])
            {
                return false;
            }

            var body = new Queue<Vector2Int>();
            var occupied = new HashSet<Vector2Int>();
            foreach (var cell in initialBodyTailToHead)
            {
                body.Enqueue(cell);
                occupied.Add(cell);
            }

            for (var i = 1; i < walk.Count; i++)
            {
                var tail = body.Dequeue();
                occupied.Remove(tail);
                var next = walk[i];
                if (!occupied.Add(next))
                {
                    return false;
                }

                body.Enqueue(next);
            }

            return true;
        }

        private static string BuildReturnKey(int roomId, GraphPath path)
        {
            var builder = new StringBuilder();
            builder.Append(roomId);
            foreach (var route in path.Routes)
            {
                builder.Append(':');
                builder.Append(route.Id);
            }

            return builder.ToString();
        }

        private static void AddPathAccess(
            DrillSnakeMap map,
            GraphPath path,
            HashSet<Vector2Int> cells,
            HashSet<int> routeIds)
        {
            foreach (var roomId in path.RoomIds)
            {
                AddRoomCells(map, map.Graph.GetRoom(roomId), cells);
            }

            foreach (var route in path.Routes)
            {
                routeIds.Add(route.Id);
                foreach (var cell in route.RasterCells)
                {
                    if (map.IsInBounds(cell) &&
                        DrillSnakeMap.IsNavigableOrDiggable(map.GetCell(cell)))
                    {
                        cells.Add(cell);
                    }
                }
            }
        }

        private static void AddRoomCells(
            DrillSnakeMap map,
            DrillSnakeRoom room,
            HashSet<Vector2Int> cells)
        {
            for (var y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
            {
                for (var x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (map.IsInBounds(cell) &&
                        DrillSnakeMap.IsNavigableOrDiggable(map.GetCell(cell)))
                    {
                        cells.Add(cell);
                    }
                }
            }
        }

        private static int CountDiggableInteriorCells(DrillSnakeMap map)
        {
            var count = 0;
            for (var y = 1; y < map.Height - 1; y++)
            {
                for (var x = 1; x < map.Width - 1; x++)
                {
                    if (DrillSnakeMap.IsNavigableOrDiggable(
                            map.GetCell(new Vector2Int(x, y))))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int GetMinimumRouteWidth(
            DrillSnakeLevelGraph graph,
            HashSet<int> routeIds)
        {
            var minimum = int.MaxValue;
            foreach (var route in graph.Routes)
            {
                if (routeIds.Contains(route.Id))
                {
                    minimum = Mathf.Min(minimum, route.Width);
                }
            }

            return minimum == int.MaxValue ? 0 : minimum;
        }

        private static int CountTurningChambers(
            DrillSnakeMap map,
            int snakeLength)
        {
            var count = 0;
            foreach (var room in map.Graph.Rooms)
            {
                var usableCells = 0;
                for (var y = room.Bounds.yMin; y < room.Bounds.yMax; y++)
                {
                    for (var x = room.Bounds.xMin; x < room.Bounds.xMax; x++)
                    {
                        if (DrillSnakeMap.IsInitiallyNavigable(
                                map.GetCell(new Vector2Int(x, y))))
                        {
                            usableCells++;
                        }
                    }
                }

                if (room.Bounds.width >= 5 &&
                    room.Bounds.height >= 5 &&
                    usableCells >= snakeLength + 2)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
