using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrillSnake
{
    /// <summary>
    /// Mutable, authoritative grid data. Presentation reads this map but never
    /// decides whether a cell may be entered.
    /// </summary>
    public sealed class DrillSnakeMap
    {
        public const int PrototypeSize = 45;
        public const int RefinerySize = 9;

        private readonly DrillSnakeCellType[,] _cells;
        private readonly List<Vector2Int> _docks = new();

        private DrillSnakeMap(int seed)
        {
            Seed = seed;
            Width = PrototypeSize;
            Height = PrototypeSize;
            Center = new Vector2Int(Width / 2, Height / 2);
            _cells = new DrillSnakeCellType[Width, Height];
        }

        public int Seed { get; }

        public int Width { get; }

        public int Height { get; }

        public Vector2Int Center { get; }

        public IReadOnlyList<Vector2Int> Docks => _docks;

        public static DrillSnakeMap Generate(int seed)
        {
            var map = new DrillSnakeMap(seed);
            map.GenerateLayout();
            return map;
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
            if (!IsInBounds(cell))
            {
                return;
            }

            _cells[cell.x, cell.y] = type;
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
            var half = RefinerySize / 2;
            var dx = Mathf.Max(0, Mathf.Abs(cell.x - Center.x) - half);
            var dy = Mathf.Max(0, Mathf.Abs(cell.y - Center.y) - half);
            return dx + dy;
        }

        private void GenerateLayout()
        {
            Fill(DrillSnakeCellType.SoftRock);
            BuildBoundary();
            BuildBedrockIslands();
            CarveRefinery();
            CarveSafeRoutes();
            CarveRiskyShortcuts();
            CarveTurningChambers();
            PlaceOrePockets();
        }

        private void Fill(DrillSnakeCellType type)
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    _cells[x, y] = type;
                }
            }
        }

        private void BuildBoundary()
        {
            for (var x = 0; x < Width; x++)
            {
                _cells[x, 0] = DrillSnakeCellType.Bedrock;
                _cells[x, Height - 1] = DrillSnakeCellType.Bedrock;
            }

            for (var y = 0; y < Height; y++)
            {
                _cells[0, y] = DrillSnakeCellType.Bedrock;
                _cells[Width - 1, y] = DrillSnakeCellType.Bedrock;
            }
        }

        private void BuildBedrockIslands()
        {
            SetRectangle(7, 27, 6, 7, DrillSnakeCellType.Bedrock);
            SetRectangle(14, 29, 5, 6, DrillSnakeCellType.Bedrock);
            SetRectangle(28, 29, 7, 5, DrillSnakeCellType.Bedrock);
            SetRectangle(32, 20, 6, 6, DrillSnakeCellType.Bedrock);
            SetRectangle(27, 10, 7, 6, DrillSnakeCellType.Bedrock);
            SetRectangle(15, 10, 5, 6, DrillSnakeCellType.Bedrock);
            SetRectangle(7, 12, 6, 7, DrillSnakeCellType.Bedrock);
            SetRectangle(11, 36, 6, 5, DrillSnakeCellType.Bedrock);
            SetRectangle(29, 4, 7, 5, DrillSnakeCellType.Bedrock);
        }

        private void CarveRefinery()
        {
            var half = RefinerySize / 2;
            for (var y = Center.y - half; y <= Center.y + half; y++)
            {
                for (var x = Center.x - half; x <= Center.x + half; x++)
                {
                    _cells[x, y] = DrillSnakeCellType.RefineryFloor;
                }
            }

            _docks.Add(new Vector2Int(Center.x, Center.y + half + 1));
            _docks.Add(new Vector2Int(Center.x + half + 1, Center.y));
            _docks.Add(new Vector2Int(Center.x, Center.y - half - 1));
            _docks.Add(new Vector2Int(Center.x - half - 1, Center.y));
            foreach (var dock in _docks)
            {
                _cells[dock.x, dock.y] = DrillSnakeCellType.RefineryDock;
            }
        }

        private void CarveSafeRoutes()
        {
            // A long, forgiving two-cell outer loop.
            CarveHorizontal(4, 40, 4, 2);
            CarveHorizontal(4, 40, 39, 2);
            CarveVertical(4, 4, 40, 2);
            CarveVertical(39, 4, 40, 2);

            // A second loop gives every major region at least two ways home.
            CarveHorizontal(10, 34, 10, 2);
            CarveHorizontal(10, 34, 33, 2);
            CarveVertical(10, 10, 34, 2);
            CarveVertical(33, 10, 34, 2);

            // Wide cardinal routes connect all four refinery docks to both loops.
            CarveVertical(Center.x - 1, Center.y + 5, 40, 2);
            CarveVertical(Center.x, 4, Center.y - 5, 2);
            CarveHorizontal(Center.x + 5, 40, Center.y - 1, 2);
            CarveHorizontal(4, Center.x - 5, Center.y, 2);

            // Additional wide connections prevent the loops from acting as rails.
            CarveVertical(6, 10, 39, 2);
            CarveVertical(37, 10, 39, 2);
            CarveHorizontal(10, 39, 7, 2);
            CarveHorizontal(4, 34, 36, 2);
        }

        private void CarveRiskyShortcuts()
        {
            // Single-cell shortcuts are visibly faster but awkward with cargo.
            CarveHorizontal(11, Center.x - 5, 27, 1);
            CarveVertical(11, 27, 33, 1);

            CarveHorizontal(Center.x + 5, 33, 17, 1);
            CarveVertical(33, 11, 17, 1);

            CarveHorizontal(11, 17, 17, 1);
            CarveVertical(17, 11, 17, 1);

            CarveHorizontal(27, 33, 28, 1);
            CarveVertical(27, 28, 33, 1);
        }

        private void CarveTurningChambers()
        {
            CarveRectangle(3, 3, 8, 8);
            CarveRectangle(34, 3, 8, 8);
            CarveRectangle(3, 34, 8, 8);
            CarveRectangle(34, 34, 8, 8);
            CarveRectangle(19, 34, 7, 8);
            CarveRectangle(19, 3, 7, 8);
            CarveRectangle(3, 19, 8, 7);
            CarveRectangle(34, 19, 8, 7);
            CarveRectangle(12, 20, 6, 6);
            CarveRectangle(27, 20, 6, 6);
        }

        private void PlaceOrePockets()
        {
            var random = new System.Random(Seed);

            var commonCenters = new[]
            {
                new Vector2Int(15, 22),
                new Vector2Int(29, 22),
                new Vector2Int(22, 15),
                new Vector2Int(22, 29),
                new Vector2Int(15, 17),
                new Vector2Int(29, 27)
            };
            PlaceClusters(commonCenters, 2, 7, DrillSnakeCellType.CommonOre, random);

            var rareCenters = new[]
            {
                new Vector2Int(10, 22),
                new Vector2Int(34, 22),
                new Vector2Int(22, 10),
                new Vector2Int(22, 34),
                new Vector2Int(12, 12),
                new Vector2Int(32, 32),
                new Vector2Int(12, 32),
                new Vector2Int(32, 12)
            };
            PlaceClusters(rareCenters, 2, 6, DrillSnakeCellType.RareOre, random);

            var veryRareCenters = new[]
            {
                new Vector2Int(3, 22),
                new Vector2Int(41, 22),
                new Vector2Int(22, 3),
                new Vector2Int(22, 41),
                new Vector2Int(7, 37),
                new Vector2Int(37, 7),
                new Vector2Int(7, 7),
                new Vector2Int(37, 37)
            };
            PlaceClusters(veryRareCenters, 2, 4, DrillSnakeCellType.VeryRareOre, random);
        }

        private void PlaceClusters(
            IReadOnlyList<Vector2Int> centers,
            int radius,
            int targetCount,
            DrillSnakeCellType type,
            System.Random random)
        {
            foreach (var center in centers)
            {
                var candidates = new List<Vector2Int>();
                for (var y = center.y - radius; y <= center.y + radius; y++)
                {
                    for (var x = center.x - radius; x <= center.x + radius; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (IsInBounds(cell) &&
                            _cells[x, y] == DrillSnakeCellType.SoftRock &&
                            Vector2Int.Distance(cell, center) <= radius + 0.35f)
                        {
                            candidates.Add(cell);
                        }
                    }
                }

                // Chambers and routes stay readable, but a pocket centered on a
                // carved space may expose a few ore blocks at its edge.
                if (candidates.Count < targetCount)
                {
                    for (var y = center.y - radius; y <= center.y + radius; y++)
                    {
                        for (var x = center.x - radius; x <= center.x + radius; x++)
                        {
                            var cell = new Vector2Int(x, y);
                            if (IsInBounds(cell) &&
                                _cells[x, y] == DrillSnakeCellType.OpenFloor &&
                                Vector2Int.Distance(cell, center) <= radius + 0.35f &&
                                !candidates.Contains(cell))
                            {
                                candidates.Add(cell);
                            }
                        }
                    }
                }

                for (var i = candidates.Count - 1; i > 0; i--)
                {
                    var swapIndex = random.Next(i + 1);
                    (candidates[i], candidates[swapIndex]) =
                        (candidates[swapIndex], candidates[i]);
                }

                var count = Mathf.Min(targetCount, candidates.Count);
                for (var i = 0; i < count; i++)
                {
                    var cell = candidates[i];
                    _cells[cell.x, cell.y] = type;
                }
            }
        }

        private void SetRectangle(
            int startX,
            int startY,
            int width,
            int height,
            DrillSnakeCellType type)
        {
            for (var y = startY; y < startY + height; y++)
            {
                for (var x = startX; x < startX + width; x++)
                {
                    if (x > 0 && x < Width - 1 && y > 0 && y < Height - 1)
                    {
                        _cells[x, y] = type;
                    }
                }
            }
        }

        private void CarveRectangle(int startX, int startY, int width, int height)
        {
            for (var y = startY; y < startY + height; y++)
            {
                for (var x = startX; x < startX + width; x++)
                {
                    if (x > 0 &&
                        x < Width - 1 &&
                        y > 0 &&
                        y < Height - 1 &&
                        _cells[x, y] != DrillSnakeCellType.RefineryDock)
                    {
                        _cells[x, y] = DrillSnakeCellType.OpenFloor;
                    }
                }
            }
        }

        private void CarveHorizontal(int startX, int endX, int startY, int width)
        {
            for (var y = startY; y < startY + width; y++)
            {
                for (var x = startX; x <= endX; x++)
                {
                    if (_cells[x, y] != DrillSnakeCellType.RefineryDock)
                    {
                        _cells[x, y] = DrillSnakeCellType.OpenFloor;
                    }
                }
            }
        }

        private void CarveVertical(int startX, int startY, int endY, int width)
        {
            for (var x = startX; x < startX + width; x++)
            {
                for (var y = startY; y <= endY; y++)
                {
                    if (_cells[x, y] != DrillSnakeCellType.RefineryDock)
                    {
                        _cells[x, y] = DrillSnakeCellType.OpenFloor;
                    }
                }
            }
        }
    }
}
