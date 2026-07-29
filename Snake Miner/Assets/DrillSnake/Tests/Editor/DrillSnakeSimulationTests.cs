using NUnit.Framework;
using UnityEngine;

namespace DrillSnake.Tests
{
    public sealed class DrillSnakeSimulationTests
    {
        [Test]
        public void LogicalTickAdvancesExactlyOneCellAndBodyFollowsHeadHistory()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            var before = CopySegments(simulation);
            map.SetCell(before[0] + Vector2Int.right, DrillSnakeCellType.OpenFloor);

            var result = simulation.Step(tuning, 0, 0, false, true);

            Assert.That(result.Failed, Is.False);
            Assert.That(simulation.Head, Is.EqualTo(before[0] + Vector2Int.right));
            Assert.That(
                ManhattanDistance(before[0], simulation.Head),
                Is.EqualTo(1));
            Assert.That(simulation.Segments.Count, Is.EqualTo(before.Length));
            for (var index = 1; index < simulation.Segments.Count; index++)
            {
                Assert.That(
                    simulation.Segments[index],
                    Is.EqualTo(before[index - 1]),
                    $"segment {index} did not follow its predecessor");
            }
        }

        [Test]
        public void BufferedNinetyDegreeTurnsExecuteOnFollowingLogicalTicks()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            var start = simulation.Head;
            map.SetCell(start + Vector2Int.up, DrillSnakeCellType.OpenFloor);
            map.SetCell(
                start + Vector2Int.up + Vector2Int.left,
                DrillSnakeCellType.OpenFloor);

            Assert.That(simulation.QueueDirection(Vector2Int.up), Is.True);
            Assert.That(simulation.QueueDirection(Vector2Int.left), Is.True);
            Assert.That(simulation.Direction, Is.EqualTo(Vector2Int.right));
            Assert.That(simulation.QueuedDirectionCount, Is.EqualTo(2));

            simulation.Step(tuning, 0, 0, false, true);

            Assert.That(simulation.Head, Is.EqualTo(start + Vector2Int.up));
            Assert.That(simulation.Direction, Is.EqualTo(Vector2Int.up));
            Assert.That(simulation.QueuedDirectionCount, Is.EqualTo(1));

            simulation.Step(tuning, 0, 0, false, true);

            Assert.That(
                simulation.Head,
                Is.EqualTo(start + Vector2Int.up + Vector2Int.left));
            Assert.That(simulation.Direction, Is.EqualTo(Vector2Int.left));
            Assert.That(simulation.QueuedDirectionCount, Is.Zero);
        }

        [Test]
        public void ImmediateReverseInputIsRejectedByDirectAndBufferedApis()
        {
            var simulation = new DrillSnakeSimulation(
                DrillSnakeMap.Generate(240628));

            Assert.That(
                simulation.TrySetDirection(Vector2Int.right),
                Is.True,
                "The current forward direction must remain a valid departure.");
            Assert.That(simulation.TrySetDirection(Vector2Int.left), Is.False);
            Assert.That(simulation.QueueDirection(Vector2Int.left), Is.False);
            Assert.That(simulation.Direction, Is.EqualTo(Vector2Int.right));
            Assert.That(simulation.QueuedDirectionCount, Is.Zero);
        }

        [Test]
        public void GeneratedMapIsDeterministicAndContainsRequiredContent()
        {
            var first = DrillSnakeMap.Generate(
                240628,
                DrillSnakeLayoutPreset.MediumCrystalCaverns);
            var second = DrillSnakeMap.Generate(
                240628,
                DrillSnakeLayoutPreset.MediumCrystalCaverns);

            Assert.That(first.Width, Is.EqualTo(45));
            Assert.That(first.Height, Is.EqualTo(45));
            Assert.That(first.Graph.Rooms, Has.Count.EqualTo(13));
            Assert.That(first.ValidationReport.IsValid, Is.True);
            Assert.That(
                first.Preset,
                Is.EqualTo(DrillSnakeLayoutPreset.MediumCrystalCaverns));
            Assert.That(first.Docks, Has.Count.EqualTo(4));
            Assert.That(first.DrillPowerupCells, Has.Count.EqualTo(4));
            Assert.That(second.DrillPowerupCells, Is.EqualTo(first.DrillPowerupCells));
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
        public void EveryPresetProducesValidatedGraphFirstLayoutsAcrossSeeds()
        {
            var presets = new[]
            {
                DrillSnakeLayoutPreset.EasyOpenQuarry,
                DrillSnakeLayoutPreset.MediumCrystalCaverns,
                DrillSnakeLayoutPreset.HardMagmaFissures
            };
            foreach (var preset in presets)
            {
                var settings = DrillSnakePresetSettings.For(preset);
                for (var seedIndex = 0; seedIndex < 50; seedIndex++)
                {
                    var seed = 10000 + seedIndex * 7919;
                    var map = DrillSnakeMap.Generate(seed, preset);

                    Assert.That(
                        map.ValidationReport.IsValid,
                        Is.True,
                        $"{settings.DisplayName}, seed {seed}: " +
                        map.ValidationReport.Summary);
                    Assert.That(
                        map.TraversableOrDiggableRatio,
                        Is.InRange(
                            settings.MinimumDiggableRatio,
                            settings.MaximumDiggableRatio));
                    Assert.That(
                        map.GenerationAttempt,
                        Is.InRange(1, DrillSnakeMap.SafeGenerationAttemptLimit));
                    Assert.That(map.Graph.Rooms, Has.Count.EqualTo(13));
                    Assert.That(
                        map.ValidationReport.SafeGraphCycleCount,
                        Is.GreaterThanOrEqualTo(1));
                    Assert.That(
                        map.ValidationReport.LargestEnclosedBedrockMass,
                        Is.GreaterThanOrEqualTo(12));

                    foreach (var room in map.Graph.Rooms)
                    {
                        if (room.MajorOuterRegion)
                        {
                            Assert.That(
                                map.Graph.GetConnectionCount(room.Id),
                                Is.GreaterThanOrEqualTo(2),
                                room.Name);
                        }
                    }
                }
            }
        }

        [Test]
        public void RoomsRoutesAndShortcutsSurviveRasterization()
        {
            var map = DrillSnakeMap.Generate(
                240628,
                DrillSnakeLayoutPreset.HardMagmaFissures);
            var foundSoftRockShortcut = false;

            foreach (var room in map.Graph.Rooms)
            {
                Assert.That(
                    room.Bounds.width,
                    Is.GreaterThanOrEqualTo(room.MinimumTurningSize),
                    room.Name);
                Assert.That(
                    room.Bounds.height,
                    Is.GreaterThanOrEqualTo(room.MinimumTurningSize),
                    room.Name);
                if (room.Kind != DrillSnakeRoomKind.Refinery)
                {
                    for (var y = room.Center.y - 1; y <= room.Center.y + 1; y++)
                    {
                        for (var x = room.Center.x - 1; x <= room.Center.x + 1; x++)
                        {
                            Assert.That(
                                DrillSnakeMap.IsInitiallyNavigable(
                                    map.GetCell(new Vector2Int(x, y))),
                                Is.True,
                                $"{room.Name} turning core");
                        }
                    }
                }
            }

            foreach (var route in map.Graph.Routes)
            {
                Assert.That(route.RasterCells, Is.Not.Empty);
                foreach (var cell in route.RasterCells)
                {
                    if (route.Required)
                    {
                        Assert.That(
                            DrillSnakeMap.IsInitiallyNavigable(map.GetCell(cell)),
                            Is.True,
                            $"required route {route.Id} at {cell}");
                    }
                    else if (map.GetCell(cell) == DrillSnakeCellType.SoftRock)
                    {
                        foundSoftRockShortcut = true;
                    }
                }
            }

            Assert.That(foundSoftRockShortcut, Is.True);
        }

        [Test]
        public void ValidatorReportsBlockedDockAndCorridorCells()
        {
            var blockedDockMap = DrillSnakeMap.Generate(240628);
            blockedDockMap.SetCell(
                blockedDockMap.Docks[0],
                DrillSnakeCellType.Bedrock);
            var dockReport = DrillSnakeLevelValidator.Validate(blockedDockMap);

            Assert.That(dockReport.IsValid, Is.False);
            Assert.That(HasFailure(dockReport, "DOCK_BLOCKED"), Is.True);

            var blockedRouteMap = DrillSnakeMap.Generate(240628);
            DrillSnakeRoute requiredRoute = null;
            foreach (var route in blockedRouteMap.Graph.Routes)
            {
                if (route.Required)
                {
                    requiredRoute = route;
                    break;
                }
            }

            Assert.That(requiredRoute, Is.Not.Null);
            var blockedCell = requiredRoute.RasterCells[
                requiredRoute.RasterCells.Count / 2];
            blockedRouteMap.SetCell(blockedCell, DrillSnakeCellType.Bedrock);
            var routeReport = DrillSnakeLevelValidator.Validate(blockedRouteMap);

            Assert.That(routeReport.IsValid, Is.False);
            Assert.That(HasFailure(routeReport, "CORRIDOR_BLOCKED"), Is.True);
        }

        [Test]
        public void ValidatorRejectsDisconnectedOreChamber()
        {
            var map = DrillSnakeMap.Generate(
                240628,
                DrillSnakeLayoutPreset.HardMagmaFissures);
            var chamber = GetFirstOuterChamber(map);
            BlockIncidentSafeRoutes(map, chamber, null);

            var report = DrillSnakeLevelValidator.Validate(map);

            Assert.That(report.IsValid, Is.False);
            Assert.That(HasFailure(report, "NO_TILE_RETURN"), Is.True);
        }

        [Test]
        public void ValidatorRejectsMandatoryDeadEndWithoutTurningSpace()
        {
            var map = DrillSnakeMap.Generate(
                240628,
                DrillSnakeLayoutPreset.HardMagmaFissures);
            var chamber = GetFirstOuterChamber(map);
            DrillSnakeRoute retainedRoute = null;
            foreach (var route in map.Graph.GetRoutesForRoom(chamber.Id))
            {
                if (route.Kind != DrillSnakeRouteKind.RiskySoftRockShortcut)
                {
                    retainedRoute = route;
                    break;
                }
            }

            Assert.That(retainedRoute, Is.Not.Null);
            BlockIncidentSafeRoutes(map, chamber, retainedRoute);
            map.SetCell(chamber.Center, DrillSnakeCellType.Bedrock);

            var report = DrillSnakeLevelValidator.Validate(map);

            Assert.That(report.IsValid, Is.False);
            Assert.That(HasFailure(report, "MANDATORY_DEAD_END"), Is.True);
            Assert.That(HasFailure(report, "TURNING_CORE_BLOCKED"), Is.True);
        }

        [Test]
        public void LongSnakeDiagnosticReportsEveryRequiredLength()
        {
            var map = DrillSnakeMap.Generate(
                240628,
                DrillSnakeLayoutPreset.MediumCrystalCaverns);
            var report = DrillSnakeDesignDiagnostics.Analyze(map);
            var expectedLengths = new[] { 5, 15, 30, 60 };

            Assert.That(report.LengthReports, Has.Count.EqualTo(expectedLengths.Length));
            for (var index = 0; index < expectedLengths.Length; index++)
            {
                var lengthReport = report.LengthReports[index];
                Assert.That(
                    lengthReport.SnakeLength,
                    Is.EqualTo(expectedLengths[index]));
                Assert.That(
                    lengthReport.AccessibleFloorPercentage,
                    Is.InRange(0f, 100f));
                Assert.That(lengthReport.ReachableOreChambers, Is.GreaterThan(0));
                Assert.That(lengthReport.ViableReturnRoutes, Is.GreaterThan(0));
                Assert.That(lengthReport.MinimumRouteWidth, Is.GreaterThan(0));
                Assert.That(lengthReport.TurningChambers, Is.GreaterThanOrEqualTo(0));
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
        public void DestroyedOreFragmentsGrowSnakeAndResetPreservesTerrain()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            var oreCell = PrepareAndCollectOreFragments(
                simulation,
                tuning,
                DrillSnakeCellType.CommonOre);

            Assert.That(simulation.CargoCount, Is.EqualTo(3));
            Assert.That(simulation.CargoValue, Is.EqualTo(15));
            Assert.That(
                simulation.Segments.Count,
                Is.EqualTo(DrillSnakeSimulation.MinimumSegmentCount + 3));
            Assert.That(map.GetCell(oreCell), Is.EqualTo(DrillSnakeCellType.OpenFloor));
            Assert.That(simulation.OrePickups, Is.Empty);

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
            PrepareAndCollectOreFragments(
                simulation,
                tuning,
                DrillSnakeCellType.VeryRareOre);

            Assert.That(simulation.ConsumeTailCargo(), Is.True);
            Assert.That(simulation.ConsumeTailCargo(), Is.True);
            Assert.That(simulation.ConsumeTailCargo(), Is.True);
            Assert.That(simulation.ConsumeTailCargo(), Is.False);
            Assert.That(simulation.CargoCount, Is.Zero);
            Assert.That(
                simulation.Segments.Count,
                Is.EqualTo(DrillSnakeSimulation.MinimumSegmentCount));
        }

        [Test]
        public void BankingMultipleOreRemovesOnlyCargoAndRetainsPermanentChassis()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var session = new DrillSnakeSession();
            var tuning = new DrillSnakeTuning();
            PrepareAndCollectOreFragments(
                simulation,
                tuning,
                DrillSnakeCellType.CommonOre);

            Assert.That(simulation.CargoCount, Is.EqualTo(3));
            Assert.That(
                simulation.Segments.Count,
                Is.EqualTo(DrillSnakeSimulation.MinimumSegmentCount + 3));
            var payoff = session.BankCargo(simulation);
            Assert.That(payoff, Is.EqualTo(15));
            Assert.That(session.BankCargo(simulation), Is.Zero);
            Assert.That(session.BankedCredits, Is.EqualTo(15));

            var consumed = 0;
            while (simulation.ConsumeTailCargo())
            {
                consumed++;
            }

            Assert.That(consumed, Is.EqualTo(3));
            Assert.That(simulation.CargoCount, Is.Zero);
            Assert.That(
                simulation.Segments.Count,
                Is.EqualTo(DrillSnakeSimulation.MinimumSegmentCount));
            for (var index = 0; index < simulation.Segments.Count; index++)
            {
                Assert.That(
                    simulation.GetSegmentOreType(index),
                    Is.EqualTo(DrillSnakeOreType.None));
            }
        }

        [Test]
        public void NormalContactWithRockBlocksWithoutDamagingTerrain()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            var drillCell = simulation.Head + Vector2Int.right;
            var originalHead = simulation.Head;
            map.SetCell(drillCell, DrillSnakeCellType.SoftRock);

            var impact = simulation.Step(tuning, 0, 0, false, true);

            Assert.That(impact.Outcome, Is.EqualTo(DrillSnakeStepOutcome.Blocked));
            Assert.That(impact.DamageDealt, Is.Zero);
            Assert.That(simulation.Head, Is.EqualTo(originalHead));
            Assert.That(map.GetCell(drillCell), Is.EqualTo(DrillSnakeCellType.SoftRock));
        }

        [Test]
        public void TurretDestroysOreAndScattersCollectibleFragments()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            var oreCell = simulation.Head + Vector2Int.right;
            map.SetCell(oreCell, DrillSnakeCellType.RareOre);

            var firstShot = simulation.TryFireTurret(tuning);
            var secondShot = simulation.TryFireTurret(tuning);
            var finalShot = simulation.TryFireTurret(tuning);

            Assert.That(firstShot.Fired, Is.True);
            Assert.That(firstShot.RemainingDurability, Is.EqualTo(2));
            Assert.That(secondShot.RemainingDurability, Is.EqualTo(1));
            Assert.That(finalShot.Destroyed, Is.True);
            Assert.That(map.GetCell(oreCell), Is.EqualTo(DrillSnakeCellType.OpenFloor));
            Assert.That(finalShot.SpawnedPickups, Has.Count.EqualTo(3));
            Assert.That(simulation.OrePickups, Has.Count.EqualTo(3));
            Assert.That(simulation.CargoCount, Is.Zero);

            var scatteredValue = 0;
            foreach (var pickup in finalShot.SpawnedPickups)
            {
                scatteredValue += pickup.Value;
                Assert.That(pickup.OreType, Is.EqualTo(DrillSnakeOreType.Rare));
            }

            Assert.That(scatteredValue, Is.EqualTo(50));

            var radiusCollection =
                simulation.Step(tuning, 0, 0, false, true);
            Assert.That(
                radiusCollection.Outcome,
                Is.EqualTo(DrillSnakeStepOutcome.CollectedOre));
            Assert.That(simulation.Head, Is.EqualTo(oreCell));
            Assert.That(simulation.OrePickups, Has.Count.EqualTo(2));
            var collectionSourceWasScattered = false;
            foreach (var spawnedPickup in finalShot.SpawnedPickups)
            {
                if (spawnedPickup.Cell ==
                    radiusCollection.CollectedPickupCell)
                {
                    collectionSourceWasScattered = true;
                    break;
                }
            }

            Assert.That(collectionSourceWasScattered, Is.True);
        }

        [Test]
        public void TurretRequiresUnblockedLineOfSight()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            var blocker = simulation.Head + Vector2Int.right * 2;
            var target = simulation.Head + Vector2Int.right * 4;

            map.SetCell(blocker, DrillSnakeCellType.SoftRock);
            map.SetCell(target, DrillSnakeCellType.RareOre);

            Assert.That(
                simulation.HasTurretLineOfSight(simulation.Head, target),
                Is.False);
            Assert.That(simulation.TryFireTurret(tuning).Fired, Is.False);

            map.SetCell(blocker, DrillSnakeCellType.OpenFloor);

            Assert.That(
                simulation.HasTurretLineOfSight(simulation.Head, target),
                Is.True);
            var shot = simulation.TryFireTurret(tuning);
            Assert.That(shot.Fired, Is.True);
            Assert.That(shot.Target, Is.EqualTo(target));
        }

        [Test]
        public void OreHealthControlsTurretShotCountAndDamagePersistsThroughReset()
        {
            var cellTypes = new[]
            {
                DrillSnakeCellType.CommonOre,
                DrillSnakeCellType.RareOre,
                DrillSnakeCellType.VeryRareOre
            };
            var expectedShots = new[] { 2, 3, 4 };

            for (var typeIndex = 0; typeIndex < cellTypes.Length; typeIndex++)
            {
                var map = DrillSnakeMap.Generate(240628 + typeIndex);
                var simulation = new DrillSnakeSimulation(map);
                var tuning = new DrillSnakeTuning();
                var target = simulation.Head + Vector2Int.right;
                map.SetCell(target, cellTypes[typeIndex]);

                Assert.That(
                    simulation.GetRemainingDurability(target, tuning),
                    Is.EqualTo(expectedShots[typeIndex]));

                DrillSnakeTurretResult result = default;
                for (var shot = 1; shot <= expectedShots[typeIndex]; shot++)
                {
                    result = simulation.TryFireTurret(tuning);
                    Assert.That(result.Fired, Is.True);
                    Assert.That(
                        result.RemainingDurability,
                        Is.EqualTo(expectedShots[typeIndex] - shot));
                }

                Assert.That(result.Destroyed, Is.True);
                Assert.That(
                    map.GetCell(target),
                    Is.EqualTo(DrillSnakeCellType.OpenFloor));
            }

            var persistenceMap = DrillSnakeMap.Generate(240700);
            var persistenceSimulation = new DrillSnakeSimulation(persistenceMap);
            var persistenceTuning = new DrillSnakeTuning();
            var persistenceCell =
                persistenceSimulation.Head + Vector2Int.right;
            persistenceMap.SetCell(
                persistenceCell,
                DrillSnakeCellType.RareOre);

            persistenceSimulation.TryFireTurret(persistenceTuning);
            Assert.That(
                persistenceSimulation.GetRemainingDurability(
                    persistenceCell,
                    persistenceTuning),
                Is.EqualTo(2));

            persistenceSimulation.ResetExpedition();

            Assert.That(
                persistenceSimulation.GetRemainingDurability(
                    persistenceCell,
                    persistenceTuning),
                Is.EqualTo(2));

            var secondShot =
                persistenceSimulation.TryFireTurret(persistenceTuning);
            Assert.That(secondShot.RemainingDurability, Is.EqualTo(1));
            var finalShot =
                persistenceSimulation.TryFireTurret(persistenceTuning);
            Assert.That(finalShot.Destroyed, Is.True);
        }

        [Test]
        public void DrillPowerDestroysBedrockButNormalContactDoesNot()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            var originalHead = simulation.Head;
            map.SetCell(originalHead + Vector2Int.right, DrillSnakeCellType.Bedrock);

            var result = simulation.Step(tuning, 0, 0, false, true);

            Assert.That(result.Outcome, Is.EqualTo(DrillSnakeStepOutcome.Blocked));
            Assert.That(simulation.Head, Is.EqualTo(originalHead));
            Assert.That(
                map.GetCell(originalHead + Vector2Int.right),
                Is.EqualTo(DrillSnakeCellType.Bedrock));

            simulation.ActivateDrillPowerup(10f);
            var drilled = simulation.Step(tuning, 0, 0, false, true);

            Assert.That(drilled.Outcome, Is.EqualTo(DrillSnakeStepOutcome.Drilled));
            Assert.That(simulation.Head, Is.EqualTo(originalHead + Vector2Int.right));
            Assert.That(
                map.GetCell(originalHead + Vector2Int.right),
                Is.EqualTo(DrillSnakeCellType.OpenFloor));

            simulation.AdvanceTime(10f);
            Assert.That(simulation.DrillActive, Is.False);
        }

        [Test]
        public void FailedExpeditionDoesNotRemovePreviouslyBankedCredits()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var session = new DrillSnakeSession();
            var tuning = new DrillSnakeTuning();

            PrepareAndCollectOreFragments(
                simulation,
                tuning,
                DrillSnakeCellType.CommonOre);
            Assert.That(session.BankCargo(simulation), Is.EqualTo(15));
            while (simulation.ConsumeTailCargo())
            {
            }

            var failureMap = DrillSnakeMap.Generate(240700);
            var failureSimulation = new DrillSnakeSimulation(failureMap);
            PrepareAndCollectOreFragments(
                failureSimulation,
                tuning,
                DrillSnakeCellType.VeryRareOre);
            Assert.That(failureSimulation.CargoValue, Is.EqualTo(140));
            var creditsBeforeFailure = session.BankedCredits;

            session.ResolveFailedExpedition(failureSimulation);

            Assert.That(session.BankedCredits, Is.EqualTo(creditsBeforeFailure));
            Assert.That(failureSimulation.CargoCount, Is.Zero);
            Assert.That(
                failureSimulation.Segments.Count,
                Is.EqualTo(DrillSnakeSimulation.MinimumSegmentCount));
        }

        [Test]
        public void DrillChargePickupActivatesForTenSeconds()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            var powerup = map.DrillPowerupCells[0];
            Assert.That(powerup.x, Is.EqualTo(map.Center.x));
            simulation.QueueDirection(Vector2Int.up);
            DrillSnakeStepResult result = default;
            while (simulation.Head.y < powerup.y)
            {
                result = simulation.Step(tuning, 0, 0, false, true);
            }

            Assert.That(
                result.Outcome,
                Is.EqualTo(DrillSnakeStepOutcome.CollectedDrillPowerup));
            Assert.That(simulation.DrillPowerRemaining, Is.EqualTo(10f));
            foreach (var activePowerup in simulation.DrillPowerups)
            {
                Assert.That(activePowerup, Is.Not.EqualTo(powerup));
            }

            simulation.AdvanceTime(9.25f);
            Assert.That(simulation.DrillActive, Is.True);
            Assert.That(simulation.DrillPowerRemaining, Is.EqualTo(0.75f));
            simulation.AdvanceTime(0.75f);
            Assert.That(simulation.DrillActive, Is.False);
        }

        [Test]
        public void HeatAcceleratesMovementWithoutFailingTheExpedition()
        {
            var map = DrillSnakeMap.Generate(240628);
            var simulation = new DrillSnakeSimulation(map);
            var tuning = new DrillSnakeTuning();
            var coldInterval = tuning.GetMoveInterval(
                0,
                false,
                false,
                simulation.Heat);

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
            for (var move = 0; move < 400; move++)
            {
                var side = move / sideLength % directions.Length;
                simulation.TrySetDirection(directions[side]);
                var result = simulation.Step(tuning, 0, 0, false, false);
                Assert.That(result.Failed, Is.False);
            }

            var hotInterval = tuning.GetMoveInterval(
                0,
                false,
                false,
                simulation.Heat);
            Assert.That(simulation.Heat, Is.GreaterThan(100f));
            Assert.That(
                tuning.GetHeatSpeedBonus(simulation.Heat),
                Is.EqualTo(1.4f).Within(0.001f));
            Assert.That(
                tuning.GetHeatRatio(simulation.Heat),
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(hotInterval, Is.LessThan(coldInterval));
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

        private static Vector2Int PrepareAndCollectOreFragments(
            DrillSnakeSimulation simulation,
            DrillSnakeTuning tuning,
            DrillSnakeCellType oreCellType)
        {
            var oreCell = simulation.Head + Vector2Int.right;
            simulation.Map.SetCell(oreCell, oreCellType);

            DrillSnakeTurretResult turretResult = default;
            for (var guard = 0; guard < 16; guard++)
            {
                turretResult = simulation.TryFireTurret(tuning);
                if (turretResult.Destroyed)
                {
                    break;
                }
            }

            Assert.That(turretResult.Destroyed, Is.True);
            Assert.That(turretResult.SpawnedPickups, Has.Count.EqualTo(3));

            Assert.That(
                simulation.Step(tuning, 0, 0, false, true).Outcome,
                Is.EqualTo(DrillSnakeStepOutcome.CollectedOre));
            Assert.That(
                simulation.Step(tuning, 0, 0, false, true).Outcome,
                Is.EqualTo(DrillSnakeStepOutcome.CollectedOre));
            Assert.That(simulation.TrySetDirection(Vector2Int.up), Is.True);
            Assert.That(
                simulation.Step(tuning, 0, 0, false, true).Outcome,
                Is.EqualTo(DrillSnakeStepOutcome.CollectedOre));
            return oreCell;
        }

        private static bool HasFailure(
            DrillSnakeValidationReport report,
            string code)
        {
            foreach (var failure in report.Failures)
            {
                if (failure.Code == code)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2Int[] CopySegments(
            DrillSnakeSimulation simulation)
        {
            var copy = new Vector2Int[simulation.Segments.Count];
            for (var index = 0; index < simulation.Segments.Count; index++)
            {
                copy[index] = simulation.Segments[index];
            }

            return copy;
        }

        private static int ManhattanDistance(Vector2Int left, Vector2Int right)
        {
            return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
        }

        private static DrillSnakeRoom GetFirstOuterChamber(DrillSnakeMap map)
        {
            foreach (var room in map.Graph.Rooms)
            {
                if (room.MajorOuterRegion)
                {
                    return room;
                }
            }

            Assert.Fail("Generated graph has no outer chamber.");
            return null;
        }

        private static void BlockIncidentSafeRoutes(
            DrillSnakeMap map,
            DrillSnakeRoom chamber,
            DrillSnakeRoute retainedRoute)
        {
            foreach (var route in map.Graph.GetRoutesForRoom(chamber.Id))
            {
                if (route == retainedRoute ||
                    route.Kind == DrillSnakeRouteKind.RiskySoftRockShortcut)
                {
                    continue;
                }

                foreach (var cell in route.RasterCells)
                {
                    if (!chamber.Bounds.Contains(cell))
                    {
                        map.SetCell(cell, DrillSnakeCellType.Bedrock);
                    }
                }
            }
        }

    }
}
