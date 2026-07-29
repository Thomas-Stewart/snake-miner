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
        private readonly Dictionary<Vector2Int, int> _drillDamageByCell = new();
        private readonly Dictionary<Vector2Int, DrillSnakeOreType> _brokenOreByCell = new();
        private bool _cargoBanked;

        public DrillSnakeSimulation(DrillSnakeMap map)
        {
            Map = map;
            ResetExpedition();
        }

        public DrillSnakeMap Map { get; }

        public IReadOnlyList<Vector2Int> Segments => _segments;

        public IReadOnlyList<DrillSnakeCargo> Cargo => _cargo;

        public Vector2Int Direction { get; private set; }

        public float Heat { get; private set; }

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

        public DrillSnakeStepResult Step(
            DrillSnakeTuning tuning,
            int scannerLevel,
            int coolingLevel,
            bool boosting,
            bool heatFree,
            int drillMotorLevel = 0)
        {
            if (_directionBuffer.Count > 0)
            {
                Direction = _directionBuffer.Dequeue();
            }

            var nextCell = Head + Direction;
            var cellType = Map.GetCell(nextCell);
            var cellOreType = ToOreType(cellType);
            var oreType = _brokenOreByCell.TryGetValue(
                nextCell,
                out var brokenOreType)
                ? brokenOreType
                : cellOreType;
            var willGrow = oreType != DrillSnakeOreType.None;

            if (cellType == DrillSnakeCellType.Bedrock || !Map.IsInBounds(nextCell))
            {
                return new DrillSnakeStepResult(
                    DrillSnakeStepOutcome.BedrockCollision,
                    nextCell);
            }

            if (CollidesWithBody(nextCell, willGrow))
            {
                return new DrillSnakeStepResult(
                    DrillSnakeStepOutcome.BodyCollision,
                    nextCell);
            }

            var drilled =
                cellType == DrillSnakeCellType.SoftRock ||
                cellOreType != DrillSnakeOreType.None;
            var drillDamageDealt = 0;
            if (drilled)
            {
                var durability = tuning.GetCellDurability(cellType);
                var previousDamage = _drillDamageByCell.TryGetValue(
                    nextCell,
                    out var storedDamage)
                    ? storedDamage
                    : 0;
                var damageDealt = Mathf.Min(
                    tuning.GetDrillDamage(drillMotorLevel),
                    durability - previousDamage);
                drillDamageDealt = damageDealt;
                var totalDamage = previousDamage + damageDealt;
                var remainingDurability = Mathf.Max(0, durability - totalDamage);

                if (remainingDurability > 0)
                {
                    _drillDamageByCell[nextCell] = totalDamage;
                    if (!heatFree)
                    {
                        Heat += tuning.DrillingHeat;
                    }

                    if (Heat >= tuning.GetMaximumHeat(coolingLevel))
                    {
                        return new DrillSnakeStepResult(
                            DrillSnakeStepOutcome.Overheated,
                            nextCell,
                            oreType,
                            0,
                            remainingDurability,
                            damageDealt);
                    }

                    return new DrillSnakeStepResult(
                        DrillSnakeStepOutcome.RockImpact,
                        nextCell,
                        oreType,
                        0,
                        remainingDurability,
                        damageDealt);
                }

                _drillDamageByCell.Remove(nextCell);
                Map.SetCell(nextCell, DrillSnakeCellType.OpenFloor);
                if (oreType != DrillSnakeOreType.None)
                {
                    _brokenOreByCell[nextCell] = oreType;
                }

                if (!heatFree)
                {
                    Heat += tuning.DrillingHeat;
                }

                if (Heat >= tuning.GetMaximumHeat(coolingLevel))
                {
                    return new DrillSnakeStepResult(
                        DrillSnakeStepOutcome.Overheated,
                        nextCell,
                        oreType,
                        0,
                        0,
                        damageDealt);
                }

                // Breaking and entering are deliberately separate logical
                // actions. Even the final point of damage produces a full
                // rebuff; the next tick moves into the newly cleared cell.
                return new DrillSnakeStepResult(
                    DrillSnakeStepOutcome.RockImpact,
                    nextCell,
                    oreType,
                    0,
                    0,
                    damageDealt);
            }

            _segments.Insert(0, nextCell);
            if (willGrow)
            {
                var value = tuning.GetOreValue(oreType, scannerLevel);
                _cargo.Add(new DrillSnakeCargo(oreType, value));
                _brokenOreByCell.Remove(nextCell);
                _cargoBanked = false;
            }
            else
            {
                _segments.RemoveAt(_segments.Count - 1);
            }

            if (!heatFree)
            {
                Heat += tuning.GetMoveHeat(CargoCount, boosting);
                if (drilled)
                {
                    Heat += tuning.DrillingHeat;
                }
            }

            if (Heat >= tuning.GetMaximumHeat(coolingLevel))
            {
                return new DrillSnakeStepResult(
                    DrillSnakeStepOutcome.Overheated,
                    nextCell,
                    oreType,
                    willGrow ? _cargo[_cargo.Count - 1].Value : 0,
                    0,
                    drillDamageDealt);
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
                    oreType,
                    _cargo[_cargo.Count - 1].Value,
                    0,
                    drillDamageDealt);
            }

            return new DrillSnakeStepResult(
                drilled ? DrillSnakeStepOutcome.Drilled : DrillSnakeStepOutcome.Moved,
                nextCell,
                DrillSnakeOreType.None,
                0,
                0,
                drillDamageDealt);
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
                durability - (_drillDamageByCell.TryGetValue(
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
    }
}
