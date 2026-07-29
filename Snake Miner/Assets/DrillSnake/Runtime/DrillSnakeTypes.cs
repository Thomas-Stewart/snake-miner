using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrillSnake
{
    public enum DrillSnakeLayoutPreset
    {
        EasyOpenQuarry,
        MediumCrystalCaverns,
        HardMagmaFissures
    }

    public enum DrillSnakeArtMode
    {
        IllustratedPng,
        ProceduralCel
    }

    public enum DrillSnakeRoomKind
    {
        Refinery,
        OreChamber
    }

    public enum DrillSnakeRouteKind
    {
        Standard,
        SafeLongRoute,
        RiskySoftRockShortcut
    }

    public enum DrillSnakeDistanceTier
    {
        Refinery,
        Common,
        Rare,
        VeryRare
    }

    public enum DrillSnakeCellType
    {
        OpenFloor,
        SoftRock,
        Bedrock,
        CommonOre,
        RareOre,
        VeryRareOre,
        RefineryFloor,
        RefineryDock
    }

    public enum DrillSnakeOreType
    {
        None,
        Common,
        Rare,
        VeryRare
    }

    public enum DrillSnakeUpgradeType
    {
        Cooling,
        DrillMotor,
        DriveSpeed,
        OreScanner
    }

    public enum DrillSnakeStepOutcome
    {
        Moved,
        Blocked,
        Drilled,
        CollectedOre,
        CollectedDrillPowerup,
        Docked,
        BodyCollision
    }

    public readonly struct DrillSnakeOrePickup
    {
        public DrillSnakeOrePickup(
            Vector2Int cell,
            DrillSnakeOreType oreType,
            int value)
        {
            Cell = cell;
            OreType = oreType;
            Value = value;
        }

        public Vector2Int Cell { get; }

        public DrillSnakeOreType OreType { get; }

        public int Value { get; }
    }

    public readonly struct DrillSnakeTurretResult
    {
        public DrillSnakeTurretResult(
            Vector2Int origin,
            Vector2Int target,
            DrillSnakeOreType oreType,
            int remainingDurability,
            IReadOnlyList<DrillSnakeOrePickup> spawnedPickups)
        {
            Origin = origin;
            Target = target;
            OreType = oreType;
            RemainingDurability = remainingDurability;
            SpawnedPickups = spawnedPickups ??
                             Array.Empty<DrillSnakeOrePickup>();
        }

        public Vector2Int Origin { get; }

        public Vector2Int Target { get; }

        public DrillSnakeOreType OreType { get; }

        public int RemainingDurability { get; }

        public IReadOnlyList<DrillSnakeOrePickup> SpawnedPickups { get; }

        public bool Fired => OreType != DrillSnakeOreType.None;

        public bool Destroyed =>
            Fired &&
            RemainingDurability == 0;
    }

    [Serializable]
    public readonly struct DrillSnakeCargo
    {
        public DrillSnakeCargo(DrillSnakeOreType oreType, int value)
        {
            OreType = oreType;
            Value = value;
        }

        public DrillSnakeOreType OreType { get; }

        public int Value { get; }
    }

    public readonly struct DrillSnakeStepResult
    {
        public DrillSnakeStepResult(
            DrillSnakeStepOutcome outcome,
            Vector2Int cell,
            DrillSnakeOreType oreType = DrillSnakeOreType.None,
            int oreValue = 0,
            int remainingDurability = 0,
            int damageDealt = 0,
            IReadOnlyList<DrillSnakeOrePickup> spawnedPickups = null,
            Vector2Int collectedPickupCell = default)
        {
            Outcome = outcome;
            Cell = cell;
            OreType = oreType;
            OreValue = oreValue;
            RemainingDurability = remainingDurability;
            DamageDealt = damageDealt;
            SpawnedPickups = spawnedPickups ??
                             Array.Empty<DrillSnakeOrePickup>();
            CollectedPickupCell = collectedPickupCell;
        }

        public DrillSnakeStepOutcome Outcome { get; }

        public Vector2Int Cell { get; }

        public DrillSnakeOreType OreType { get; }

        public int OreValue { get; }

        public int RemainingDurability { get; }

        public int DamageDealt { get; }

        public IReadOnlyList<DrillSnakeOrePickup> SpawnedPickups { get; }

        public Vector2Int CollectedPickupCell { get; }

        public bool ChangedTerrain =>
            Outcome == DrillSnakeStepOutcome.Drilled;

        public bool Failed =>
            Outcome == DrillSnakeStepOutcome.BodyCollision;
    }
}
