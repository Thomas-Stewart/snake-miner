using System.Collections.Generic;
using UnityEngine;

namespace DrillSnake
{
    /// <summary>
    /// Pure grid gameplay. No transforms, input, coroutines, or scene references
    /// are involved, so movement and collisions remain deterministic and testable.
    /// </summary>
    public sealed class DrillSnakeSimulation
    {
        public const int PermanentChassisCount = 4;
        public const int MinimumSegmentCount = PermanentChassisCount + 1;

        private readonly List<Vector2Int> _segments = new();
        private readonly List<DrillSnakeCargo> _cargo = new();
        private readonly Queue<Vector2Int> _directionBuffer = new();
        private readonly Dictionary<Vector2Int, int> _turretDamageByCell = new();
        private readonly Dictionary<Vector2Int, DrillSnakeOrePickup> _orePickups = new();
        private readonly HashSet<Vector2Int> _drillPowerups = new();
        private bool _cargoBanked;

        public DrillSnakeSimulation(DrillSnakeMap map)
        {
            Map = map;
            foreach (var cell in map.DrillPowerupCells)
            {
                _drillPowerups.Add(cell);
            }

            ResetExpedition();
        }

        public DrillSnakeMap Map { get; }

        public IReadOnlyList<Vector2Int> Segments => _segments;

        public IReadOnlyList<DrillSnakeCargo> Cargo => _cargo;

        public IReadOnlyDictionary<Vector2Int, DrillSnakeOrePickup> OrePickups =>
            _orePickups;

        public IReadOnlyCollection<Vector2Int> DrillPowerups =>
            _drillPowerups;

        public Vector2Int Direction { get; private set; }

        public float Heat { get; private set; }

        public float DrillPowerRemaining { get; private set; }

        public bool DrillActive => DrillPowerRemaining > 0f;

        public int CargoCount => _cargo.Count;

        public int QueuedDirectionCount => _directionBuffer.Count;

        public int CargoValue
        {
            get
            {
                var value = 0;
                foreach (var cargo in _cargo)
                {
                    value += cargo.Value;
                }

                return value;
            }
        }

        public Vector2Int Head => _segments[0];

        public bool IsAtRefinery => Map.IsRefinery(Head);

        public void ResetExpedition()
        {
            _segments.Clear();
            _cargo.Clear();
            _directionBuffer.Clear();
            _cargoBanked = false;
            Heat = 0f;
            DrillPowerRemaining = 0f;
            Direction = Vector2Int.right;

            var center = Map.Center;
            _segments.Add(center);
            for (var i = 1; i <= PermanentChassisCount; i++)
            {
                _segments.Add(center + Vector2Int.left * i);
            }
        }

        public bool TrySetDirection(Vector2Int direction)
        {
            if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1 ||
                direction == -Direction)
            {
                return false;
            }

            Direction = direction;
            _directionBuffer.Clear();
            return true;
        }

        public bool QueueDirection(Vector2Int direction)
        {
            var comparisonDirection = Direction;
            foreach (var bufferedDirection in _directionBuffer)
            {
                comparisonDirection = bufferedDirection;
            }

            if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1 ||
                direction == comparisonDirection ||
                direction == -comparisonDirection ||
                _directionBuffer.Count >= 2)
            {
                return false;
            }

            _directionBuffer.Enqueue(direction);
            return true;
        }

        public void ClearDirectionBuffer()
        {
            _directionBuffer.Clear();
        }

        public void AdvanceTime(float seconds)
        {
            DrillPowerRemaining = Mathf.Max(
                0f,
                DrillPowerRemaining - Mathf.Max(0f, seconds));
        }

        public void ActivateDrillPowerup(float duration)
        {
            DrillPowerRemaining = Mathf.Max(
                DrillPowerRemaining,
                Mathf.Max(0f, duration));
        }

        public DrillSnakeStepResult Step(
            DrillSnakeTuning tuning,
            int scannerLevel,
            int coolingLevel,
            bool boosting,
            bool heatFree)
        {
            if (_directionBuffer.Count > 0)
            {
                Direction = _directionBuffer.Dequeue();
            }

            var nextCell = Head + Direction;
            if (!Map.IsInBounds(nextCell))
            {
                return new DrillSnakeStepResult(
                    DrillSnakeStepOutcome.Blocked,
                    nextCell);
            }

            var cellType = Map.GetCell(nextCell);
            var cellOreType = ToOreType(cellType);
            var destroyedBlock = false;
            IReadOnlyList<DrillSnakeOrePickup> spawnedPickups = null;
            if (IsSolid(cellType))
            {
                if (!DrillActive)
                {
                    return new DrillSnakeStepResult(
                        DrillSnakeStepOutcome.Blocked,
                        nextCell,
                        cellOreType);
                }

                Map.SetCell(nextCell, DrillSnakeCellType.OpenFloor);
                _turretDamageByCell.Remove(nextCell);
                destroyedBlock = true;
                if (cellOreType != DrillSnakeOreType.None)
                {
                    spawnedPickups = ScatterOre(
                        nextCell,
                        cellOreType,
                        tuning,
                        scannerLevel);
                }
            }

            var willGrow = TryFindNearestOrePickup(
                nextCell,
                tuning.OrePickupRadius,
                out var nearbyOrePickup);
            if (CollidesWithBody(nextCell, willGrow))
            {
                return new DrillSnakeStepResult(
                    DrillSnakeStepOutcome.BodyCollision,
                    nextCell);
            }

            _segments.Insert(0, nextCell);
            DrillSnakeOrePickup collectedOre = default;
            var collectedPowerup = false;
            if (willGrow &&
                _orePickups.Remove(nearbyOrePickup.Cell, out collectedOre))
            {
                _cargo.Add(new DrillSnakeCargo(
                    collectedOre.OreType,
                    collectedOre.Value));
                _cargoBanked = false;
            }
            else
            {
                _segments.RemoveAt(_segments.Count - 1);
                if (_drillPowerups.Remove(nextCell))
                {
                    ActivateDrillPowerup(tuning.DrillPowerupDuration);
                    collectedPowerup = true;
                }
            }

            if (!heatFree)
            {
                Heat += tuning.GetMoveHeat(CargoCount, boosting);
            }

            if (Map.GetCell(nextCell) == DrillSnakeCellType.RefineryDock)
            {
                return new DrillSnakeStepResult(DrillSnakeStepOutcome.Docked, nextCell);
            }

            if (willGrow)
            {
                return new DrillSnakeStepResult(
                    DrillSnakeStepOutcome.CollectedOre,
                    nextCell,
                    collectedOre.OreType,
                    collectedOre.Value,
                    0,
                    0,
                    spawnedPickups,
                    collectedOre.Cell);
            }

            if (collectedPowerup)
            {
                return new DrillSnakeStepResult(
                    DrillSnakeStepOutcome.CollectedDrillPowerup,
                    nextCell);
            }

            return new DrillSnakeStepResult(
                destroyedBlock
                    ? DrillSnakeStepOutcome.Drilled
                    : DrillSnakeStepOutcome.Moved,
                nextCell,
                cellOreType,
                0,
                0,
                destroyedBlock ? 1 : 0,
                spawnedPickups);
        }

        public DrillSnakeTurretResult TryFireTurret(
            DrillSnakeTuning tuning,
            int scannerLevel = 0)
        {
            var rangeSquared = tuning.TurretRange * tuning.TurretRange;
            var target = default(Vector2Int);
            var targetType = DrillSnakeOreType.None;
            var bestDistanceSquared = float.MaxValue;
            for (var y = 0; y < Map.Height; y++)
            {
                for (var x = 0; x < Map.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var oreType = ToOreType(Map.GetCell(cell));
                    if (oreType == DrillSnakeOreType.None)
                    {
                        continue;
                    }

                    var delta = cell - Head;
                    var distanceSquared = delta.sqrMagnitude;
                    if (distanceSquared > rangeSquared ||
                        distanceSquared >= bestDistanceSquared ||
                        !HasTurretLineOfSight(Head, cell))
                    {
                        continue;
                    }

                    target = cell;
                    targetType = oreType;
                    bestDistanceSquared = distanceSquared;
                }
            }

            if (targetType == DrillSnakeOreType.None)
            {
                return default;
            }

            var durability = tuning.GetCellDurability(Map.GetCell(target));
            var previousDamage = _turretDamageByCell.TryGetValue(
                target,
                out var damage)
                ? damage
                : 0;
            var totalDamage = previousDamage + tuning.TurretDamage;
            var remaining = Mathf.Max(0, durability - totalDamage);
            IReadOnlyList<DrillSnakeOrePickup> spawnedPickups = null;
            if (remaining > 0)
            {
                _turretDamageByCell[target] = totalDamage;
            }
            else
            {
                _turretDamageByCell.Remove(target);
                Map.SetCell(target, DrillSnakeCellType.OpenFloor);
                spawnedPickups = ScatterOre(
                    target,
                    targetType,
                    tuning,
                    scannerLevel);
            }

            return new DrillSnakeTurretResult(
                Head,
                target,
                targetType,
                remaining,
                spawnedPickups);
        }

        public int GetRemainingDurability(
            Vector2Int cell,
            DrillSnakeTuning tuning)
        {
            var durability = tuning.GetCellDurability(Map.GetCell(cell));
            if (durability <= 0)
            {
                return 0;
            }

            return Mathf.Max(
                0,
                durability - (_turretDamageByCell.TryGetValue(
                    cell,
                    out var damage)
                    ? damage
                    : 0));
        }

        public bool ConsumeTailCargo()
        {
            if (_cargo.Count == 0 || _segments.Count <= MinimumSegmentCount)
            {
                return false;
            }

            _cargo.RemoveAt(_cargo.Count - 1);
            _segments.RemoveAt(_segments.Count - 1);
            if (_cargo.Count == 0)
            {
                _cargoBanked = false;
            }

            return true;
        }

        public bool TryMarkCargoBanked(out int payoff)
        {
            payoff = 0;
            if (!IsAtRefinery || _cargo.Count == 0 || _cargoBanked)
            {
                return false;
            }

            payoff = CargoValue;
            _cargoBanked = true;
            return true;
        }

        public void ResetHeat()
        {
            Heat = 0f;
        }

        public DrillSnakeOreType GetSegmentOreType(int segmentIndex)
        {
            var cargoIndex = segmentIndex - MinimumSegmentCount;
            return cargoIndex >= 0 && cargoIndex < _cargo.Count
                ? _cargo[cargoIndex].OreType
                : DrillSnakeOreType.None;
        }

        private IReadOnlyList<DrillSnakeOrePickup> ScatterOre(
            Vector2Int source,
            DrillSnakeOreType oreType,
            DrillSnakeTuning tuning,
            int scannerLevel)
        {
            var candidates = new List<Vector2Int>();
            var start = Mathf.Abs(
                source.x * 73856093 ^
                source.y * 19349663) % ScatterOffsets.Length;
            for (var offsetIndex = 0;
                 offsetIndex < ScatterOffsets.Length;
                 offsetIndex++)
            {
                var offset = ScatterOffsets[
                    (start + offsetIndex) % ScatterOffsets.Length];
                var candidate = source + offset;
                if (!Map.IsInBounds(candidate) ||
                    IsSolid(Map.GetCell(candidate)) ||
                    _orePickups.ContainsKey(candidate) ||
                    _drillPowerups.Contains(candidate) ||
                    ContainsSegment(candidate))
                {
                    continue;
                }

                candidates.Add(candidate);
                if (candidates.Count >= tuning.OreFragmentCount)
                {
                    break;
                }
            }

            if (candidates.Count == 0)
            {
                candidates.Add(source);
            }

            var totalValue = tuning.GetOreValue(oreType, scannerLevel);
            var baseValue = totalValue / candidates.Count;
            var remainder = totalValue % candidates.Count;
            var spawned = new List<DrillSnakeOrePickup>(candidates.Count);
            for (var index = 0; index < candidates.Count; index++)
            {
                var pickup = new DrillSnakeOrePickup(
                    candidates[index],
                    oreType,
                    baseValue + (index < remainder ? 1 : 0));
                _orePickups[pickup.Cell] = pickup;
                spawned.Add(pickup);
            }

            return spawned;
        }

        private bool ContainsSegment(Vector2Int cell)
        {
            foreach (var segment in _segments)
            {
                if (segment == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryFindNearestOrePickup(
            Vector2Int center,
            float radius,
            out DrillSnakeOrePickup pickup)
        {
            pickup = default;
            var found = false;
            var maximumDistanceSquared = radius * radius;
            var bestDistanceSquared = float.MaxValue;
            foreach (var pair in _orePickups)
            {
                var distanceSquared = (pair.Key - center).sqrMagnitude;
                if (distanceSquared > maximumDistanceSquared)
                {
                    continue;
                }

                if (found &&
                    (distanceSquared > bestDistanceSquared ||
                     (Mathf.Approximately(
                          distanceSquared,
                          bestDistanceSquared) &&
                      (pair.Key.y > pickup.Cell.y ||
                       (pair.Key.y == pickup.Cell.y &&
                        pair.Key.x >= pickup.Cell.x)))))
                {
                    continue;
                }

                found = true;
                bestDistanceSquared = distanceSquared;
                pickup = pair.Value;
            }

            return found;
        }

        public bool HasTurretLineOfSight(
            Vector2Int origin,
            Vector2Int target)
        {
            var x = origin.x;
            var y = origin.y;
            var deltaX = Mathf.Abs(target.x - origin.x);
            var deltaY = Mathf.Abs(target.y - origin.y);
            var stepX = origin.x < target.x ? 1 : -1;
            var stepY = origin.y < target.y ? 1 : -1;
            var error = deltaX - deltaY;

            while (x != target.x || y != target.y)
            {
                var previousX = x;
                var previousY = y;
                var doubledError = error * 2;
                var movedX = doubledError > -deltaY;
                var movedY = doubledError < deltaX;

                if (movedX)
                {
                    error -= deltaY;
                    x += stepX;
                }

                if (movedY)
                {
                    error += deltaX;
                    y += stepY;
                }

                // When crossing a grid corner, both adjacent cells count as
                // part of the shot's supercover. This prevents bullets from
                // squeezing diagonally between touching rock blocks.
                if (movedX && movedY)
                {
                    var horizontalSide = new Vector2Int(x, previousY);
                    var verticalSide = new Vector2Int(previousX, y);
                    if (IsLineOfSightBlocker(horizontalSide, target) ||
                        IsLineOfSightBlocker(verticalSide, target))
                    {
                        return false;
                    }
                }

                var traversed = new Vector2Int(x, y);
                if (IsLineOfSightBlocker(traversed, target))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsLineOfSightBlocker(
            Vector2Int cell,
            Vector2Int target)
        {
            return cell != target &&
                   (!Map.IsInBounds(cell) ||
                    IsSolid(Map.GetCell(cell)));
        }

        private static bool IsSolid(DrillSnakeCellType cellType)
        {
            return cellType == DrillSnakeCellType.SoftRock ||
                   cellType == DrillSnakeCellType.Bedrock ||
                   ToOreType(cellType) != DrillSnakeOreType.None;
        }

        private bool CollidesWithBody(Vector2Int cell, bool willGrow)
        {
            var collisionCount = _segments.Count;
            if (!willGrow)
            {
                collisionCount--;
            }

            for (var i = 0; i < collisionCount; i++)
            {
                if (_segments[i] == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private static DrillSnakeOreType ToOreType(DrillSnakeCellType cellType)
        {
            return cellType switch
            {
                DrillSnakeCellType.CommonOre => DrillSnakeOreType.Common,
                DrillSnakeCellType.RareOre => DrillSnakeOreType.Rare,
                DrillSnakeCellType.VeryRareOre => DrillSnakeOreType.VeryRare,
                _ => DrillSnakeOreType.None
            };
        }

        private static readonly Vector2Int[] ScatterOffsets =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left,
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 2),
            new Vector2Int(2, 0),
            new Vector2Int(0, -2),
            new Vector2Int(-2, 0),
            Vector2Int.zero
        };
    }
}
