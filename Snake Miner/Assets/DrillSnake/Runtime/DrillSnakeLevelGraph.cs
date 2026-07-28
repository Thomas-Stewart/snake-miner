using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrillSnake
{
    public sealed class DrillSnakeRoom
    {
        internal DrillSnakeRoom(
            int id,
            string name,
            RectInt bounds,
            DrillSnakeRoomKind kind,
            bool majorOuterRegion,
            bool orePocket,
            int minimumTurningSize)
        {
            Id = id;
            Name = name;
            Bounds = bounds;
            Kind = kind;
            MajorOuterRegion = majorOuterRegion;
            IsOrePocket = orePocket;
            MinimumTurningSize = minimumTurningSize;
            GraphDistance = int.MaxValue;
        }

        public int Id { get; }

        public string Name { get; }

        public RectInt Bounds { get; }

        public DrillSnakeRoomKind Kind { get; }

        public bool MajorOuterRegion { get; }

        public bool IsOrePocket { get; }

        public int MinimumTurningSize { get; }

        public int GraphDistance { get; internal set; }

        public DrillSnakeDistanceTier DistanceTier { get; internal set; }

        public Vector2Int Center => new(
            Bounds.xMin + Bounds.width / 2,
            Bounds.yMin + Bounds.height / 2);
    }

    public sealed class DrillSnakeRoute
    {
        private readonly List<Vector2Int> _waypoints;
        private readonly List<Vector2Int> _rasterCells = new();

        internal DrillSnakeRoute(
            int id,
            int roomAId,
            int roomBId,
            int width,
            DrillSnakeRouteKind kind,
            bool required,
            IReadOnlyList<Vector2Int> waypoints)
        {
            Id = id;
            RoomAId = roomAId;
            RoomBId = roomBId;
            Width = width;
            Kind = kind;
            Required = required;
            _waypoints = new List<Vector2Int>(waypoints);
        }

        public int Id { get; }

        public int RoomAId { get; }

        public int RoomBId { get; }

        public int Width { get; }

        public DrillSnakeRouteKind Kind { get; }

        public bool Required { get; }

        public IReadOnlyList<Vector2Int> Waypoints => _waypoints;

        public IReadOnlyList<Vector2Int> RasterCells => _rasterCells;

        public int Length
        {
            get
            {
                var length = 0;
                for (var i = 1; i < _waypoints.Count; i++)
                {
                    length += Mathf.Abs(_waypoints[i].x - _waypoints[i - 1].x);
                    length += Mathf.Abs(_waypoints[i].y - _waypoints[i - 1].y);
                }

                return Mathf.Max(1, length);
            }
        }

        internal void AddRasterCell(Vector2Int cell)
        {
            if (!_rasterCells.Contains(cell))
            {
                _rasterCells.Add(cell);
            }
        }
    }

    public sealed class DrillSnakeLevelGraph
    {
        private readonly List<DrillSnakeRoom> _rooms = new();
        private readonly List<DrillSnakeRoute> _routes = new();

        public IReadOnlyList<DrillSnakeRoom> Rooms => _rooms;

        public IReadOnlyList<DrillSnakeRoute> Routes => _routes;

        public DrillSnakeRoom Refinery => _rooms[0];

        internal void AddRoom(DrillSnakeRoom room)
        {
            _rooms.Add(room);
        }

        internal void AddRoute(DrillSnakeRoute route)
        {
            _routes.Add(route);
        }

        public DrillSnakeRoom GetRoom(int id)
        {
            return _rooms[id];
        }

        public int GetConnectionCount(int roomId, bool includeRiskyShortcuts = true)
        {
            var count = 0;
            foreach (var route in _routes)
            {
                if (!includeRiskyShortcuts &&
                    route.Kind == DrillSnakeRouteKind.RiskySoftRockShortcut)
                {
                    continue;
                }

                if (route.RoomAId == roomId || route.RoomBId == roomId)
                {
                    count++;
                }
            }

            return count;
        }

        public IEnumerable<DrillSnakeRoute> GetRoutesForRoom(int roomId)
        {
            foreach (var route in _routes)
            {
                if (route.RoomAId == roomId || route.RoomBId == roomId)
                {
                    yield return route;
                }
            }
        }

        public int GetOtherRoomId(DrillSnakeRoute route, int roomId)
        {
            return route.RoomAId == roomId ? route.RoomBId : route.RoomAId;
        }
    }

    public sealed class DrillSnakePresetSettings
    {
        private DrillSnakePresetSettings(
            DrillSnakeLayoutPreset preset,
            string displayName,
            int innerRoomMinimum,
            int innerRoomMaximum,
            int outerRoomMinimum,
            int outerRoomMaximum,
            int spokeWidth,
            int outerRouteWidth,
            int secondaryRouteWidth,
            bool includeInnerLoop,
            float minimumDiggableRatio,
            float maximumDiggableRatio,
            int commonOrePerRoom,
            int rareOrePerRoom,
            int veryRareOrePerRoom)
        {
            Preset = preset;
            DisplayName = displayName;
            InnerRoomMinimum = innerRoomMinimum;
            InnerRoomMaximum = innerRoomMaximum;
            OuterRoomMinimum = outerRoomMinimum;
            OuterRoomMaximum = outerRoomMaximum;
            SpokeWidth = spokeWidth;
            OuterRouteWidth = outerRouteWidth;
            SecondaryRouteWidth = secondaryRouteWidth;
            IncludeInnerLoop = includeInnerLoop;
            MinimumDiggableRatio = minimumDiggableRatio;
            MaximumDiggableRatio = maximumDiggableRatio;
            CommonOrePerRoom = commonOrePerRoom;
            RareOrePerRoom = rareOrePerRoom;
            VeryRareOrePerRoom = veryRareOrePerRoom;
        }

        public DrillSnakeLayoutPreset Preset { get; }

        public string DisplayName { get; }

        public int InnerRoomMinimum { get; }

        public int InnerRoomMaximum { get; }

        public int OuterRoomMinimum { get; }

        public int OuterRoomMaximum { get; }

        public int SpokeWidth { get; }

        public int OuterRouteWidth { get; }

        public int SecondaryRouteWidth { get; }

        public bool IncludeInnerLoop { get; }

        public float MinimumDiggableRatio { get; }

        public float MaximumDiggableRatio { get; }

        public int CommonOrePerRoom { get; }

        public int RareOrePerRoom { get; }

        public int VeryRareOrePerRoom { get; }

        public int MinimumCommonOre => CommonOrePerRoom * 3;

        public int MinimumRareOre => RareOrePerRoom * 3;

        public int MinimumVeryRareOre => VeryRareOrePerRoom * 3;

        public static DrillSnakePresetSettings For(DrillSnakeLayoutPreset preset)
        {
            return preset switch
            {
                DrillSnakeLayoutPreset.EasyOpenQuarry => new DrillSnakePresetSettings(
                    preset,
                    "EASY — OPEN QUARRY",
                    7,
                    9,
                    7,
                    9,
                    3,
                    3,
                    2,
                    true,
                    0.62f,
                    0.69f,
                    6,
                    7,
                    5),
                DrillSnakeLayoutPreset.HardMagmaFissures => new DrillSnakePresetSettings(
                    preset,
                    "HARD — MAGMA FISSURES",
                    5,
                    6,
                    5,
                    6,
                    1,
                    1,
                    1,
                    false,
                    0.4f,
                    0.46f,
                    4,
                    5,
                    6),
                _ => new DrillSnakePresetSettings(
                    DrillSnakeLayoutPreset.MediumCrystalCaverns,
                    "MEDIUM — CRYSTAL CAVERNS",
                    6,
                    7,
                    6,
                    8,
                    2,
                    2,
                    1,
                    true,
                    0.5f,
                    0.56f,
                    5,
                    6,
                    5)
            };
        }
    }
}
