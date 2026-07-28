using NUnit.Framework;
using UnityEngine;

namespace DrillSnake.Tests
{
    public sealed class DrillSnakeSimulationTests
    {
        [Test]
        public void GeneratedMapIsDeterministicAndContainsRequiredContent()
        {
            var first = DrillSnakeMap.Generate(240628);
            var second = DrillSnakeMap.Generate(240628);

            Assert.That(first.Width, Is.EqualTo(45));
            Assert.That(first.Height, Is.EqualTo(45));
            Assert.That(first.Docks, Has.Count.EqualTo(4));
            foreach (var dock in first.Docks)
            {
                Assert.That(
                    first.GetCell(dock),
                    Is.EqualTo(DrillSnakeCellType.RefineryDock));
            }

            Assert.That(
                first.CountCells(DrillSnakeCellType.RefineryFloor),
                Is.EqualTo(DrillSnakeMap.RefinerySize * DrillSnakeMap.RefinerySize));
            Assert.That(first.CountCells(DrillSnakeCellType.CommonOre), Is.GreaterThanOrEqualTo(20));
            Assert.That(first.CountCells(DrillSnakeCellType.RareOre), Is.GreaterThanOrEqualTo(20));
            Assert.That(first.CountCells(DrillSnakeCellType.VeryRareOre), Is.GreaterThanOrEqualTo(12));
            Assert.That(first.CountCells(DrillSnakeCellType.Bedrock), Is.GreaterThan(200));
            Assert.That(first.CountCells(DrillSnakeCellType.OpenFloor), Is.GreaterThan(500));

            for (var y = 0; y < first.Height; y++)
            {
                for (var x = 0; x < first.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Assert.That(second.GetCell(cell), Is.EqualTo(first.GetCell(cell)));
                }
            }
        }

        [Test]
        public void OreTiersGenerallyGetMoreDistantFromRefinery()
        {
            var map = DrillSnakeMap.Generate(240628);

            var commonDistance = AverageDistance(map, DrillSnakeCellType.CommonOre);
            var rareDistance = AverageDistance(map, DrillSnakeCellType.RareOre);
            var veryRareDistance = AverageDistance(map, DrillSnakeCellType.VeryRareOre);

            Assert.That(rareDistance, Is.GreaterThan(commonDistance));
            Assert.That(veryRareDistance, Is.GreaterThan(rareDistance));
        }

        [Test]
        public void OreCollectionGrowsSnakeAndResetPreservesMinedTerrain()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            var oreCell = simulation.Head + Vector2Int.right;
            map.SetCell(oreCell, DrillSnakeCellType.CommonOre);

            var result = simulation.Step(tuning, 0, 0, false, true);

            Assert.That(result.Outcome, Is.EqualTo(DrillSnakeStepOutcome.CollectedOre));
            Assert.That(simulation.CargoCount, Is.EqualTo(1));
            Assert.That(
                simulation.Segments.Count,
                Is.EqualTo(DrillSnakeSimulation.MinimumSegmentCount + 1));
            Assert.That(map.GetCell(oreCell), Is.EqualTo(DrillSnakeCellType.OpenFloor));

            simulation.ResetExpedition();

            Assert.That(simulation.CargoCount, Is.Zero);
            Assert.That(
                simulation.Segments.Count,
                Is.EqualTo(DrillSnakeSimulation.MinimumSegmentCount));
            Assert.That(map.GetCell(oreCell), Is.EqualTo(DrillSnakeCellType.OpenFloor));
        }

        [Test]
        public void ImmediateReverseIsRejectedAndTightLoopHitsBody()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();

            Assert.That(simulation.TrySetDirection(Vector2Int.left), Is.False);
            Assert.That(simulation.Direction, Is.EqualTo(Vector2Int.right));

            Assert.That(
                simulation.Step(tuning, 0, 0, false, true).Failed,
                Is.False);
            Assert.That(simulation.TrySetDirection(Vector2Int.up), Is.True);
            Assert.That(
                simulation.Step(tuning, 0, 0, false, true).Failed,
                Is.False);
            Assert.That(simulation.TrySetDirection(Vector2Int.left), Is.True);
            Assert.That(
                simulation.Step(tuning, 0, 0, false, true).Failed,
                Is.False);
            Assert.That(simulation.TrySetDirection(Vector2Int.down), Is.True);

            var collision = simulation.Step(tuning, 0, 0, false, true);

            Assert.That(collision.Outcome, Is.EqualTo(DrillSnakeStepOutcome.BodyCollision));
        }

        [Test]
        public void CargoCanBeConsumedBackToPermanentChassis()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            var oreCell = simulation.Head + Vector2Int.right;
            map.SetCell(oreCell, DrillSnakeCellType.VeryRareOre);
            simulation.Step(tuning, 0, 0, false, true);

            Assert.That(simulation.ConsumeTailCargo(), Is.True);
            Assert.That(simulation.ConsumeTailCargo(), Is.False);
            Assert.That(simulation.CargoCount, Is.Zero);
            Assert.That(
                simulation.Segments.Count,
                Is.EqualTo(DrillSnakeSimulation.MinimumSegmentCount));
        }

        [Test]
        public void DrillingSoftRockChangesItToOpenFloor()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            var drillCell = simulation.Head + Vector2Int.right;
            map.SetCell(drillCell, DrillSnakeCellType.SoftRock);

            var result = simulation.Step(tuning, 0, 0, false, true);

            Assert.That(result.Outcome, Is.EqualTo(DrillSnakeStepOutcome.Drilled));
            Assert.That(map.GetCell(drillCell), Is.EqualTo(DrillSnakeCellType.OpenFloor));
            Assert.That(simulation.CargoCount, Is.Zero);
        }

        [Test]
        public void BedrockCollisionFailsWithoutMovingHead()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            var originalHead = simulation.Head;
            map.SetCell(originalHead + Vector2Int.right, DrillSnakeCellType.Bedrock);

            var result = simulation.Step(tuning, 0, 0, false, true);

            Assert.That(result.Outcome, Is.EqualTo(DrillSnakeStepOutcome.BedrockCollision));
            Assert.That(simulation.Head, Is.EqualTo(originalHead));
        }

        [Test]
        public void EastDockRecognizesCargoReturn()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            map.SetCell(simulation.Head + Vector2Int.right, DrillSnakeCellType.CommonOre);

            var collected = simulation.Step(tuning, 0, 0, false, true);
            Assert.That(collected.Outcome, Is.EqualTo(DrillSnakeStepOutcome.CollectedOre));

            DrillSnakeStepResult result = default;
            while (simulation.Head.x < map.Center.x + DrillSnakeMap.RefinerySize / 2 + 1)
            {
                result = simulation.Step(tuning, 0, 0, false, true);
            }

            Assert.That(result.Outcome, Is.EqualTo(DrillSnakeStepOutcome.Docked));
            Assert.That(simulation.CargoCount, Is.EqualTo(1));
        }

        [Test]
        public void SustainedMovementEventuallyOverheats()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();

            for (var y = 13; y <= 31; y++)
            {
                for (var x = 13; x <= 31; x++)
                {
                    map.SetCell(new Vector2Int(x, y), DrillSnakeCellType.OpenFloor);
                }
            }

            var directions = new[]
            {
                Vector2Int.right,
                Vector2Int.up,
                Vector2Int.left,
                Vector2Int.down
            };
            var sideLength = 8;
            var outcome = DrillSnakeStepOutcome.Moved;
            for (var move = 0; move < 400; move++)
            {
                var side = move / sideLength % directions.Length;
                simulation.TrySetDirection(directions[side]);
                var result = simulation.Step(tuning, 0, 0, false, false);
                outcome = result.Outcome;
                if (result.Failed)
                {
                    break;
                }
            }

            Assert.That(outcome, Is.EqualTo(DrillSnakeStepOutcome.Overheated));
        }

        private static float AverageDistance(
            DrillSnakeMap map,
            DrillSnakeCellType type)
        {
            var total = 0f;
            var count = 0;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (map.GetCell(cell) != type)
                    {
                        continue;
                    }

                    total += map.DistanceFromRefinery(cell);
                    count++;
                }
            }

            Assert.That(count, Is.GreaterThan(0));
            return total / count;
        }
    }
}
