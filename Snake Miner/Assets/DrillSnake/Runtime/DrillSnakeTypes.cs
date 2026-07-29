using System;
using UnityEngine;

namespace DrillSnake
{
    public enum DrillSnakeLayoutPreset
    {
        EasyOpenQuarry,
        MediumCrystalCaverns,
        HardMagmaFissures
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
        RockImpact,
        Drilled,
        CollectedOre,
        Docked,
        BodyCollision,
        BedrockCollision,
        Overheated
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
            int damageDealt = 0)
        {
            Outcome = outcome;
            Cell = cell;
            OreType = oreType;
            OreValue = oreValue;
            RemainingDurability = remainingDurability;
            DamageDealt = damageDealt;
        }

        public DrillSnakeStepOutcome Outcome { get; }

        public Vector2Int Cell { get; }

        public DrillSnakeOreType OreType { get; }

        public int OreValue { get; }

        public int RemainingDurability { get; }

        public int DamageDealt { get; }

        public bool Rebuffed => Outcome == DrillSnakeStepOutcome.RockImpact;

        public bool ChangedTerrain =>
            Outcome == DrillSnakeStepOutcome.Drilled ||
            Outcome == DrillSnakeStepOutcome.CollectedOre ||
            (Outcome == DrillSnakeStepOutcome.RockImpact &&
             RemainingDurability == 0);

        public bool Failed =>
            Outcome == DrillSnakeStepOutcome.BodyCollision ||
            Outcome == DrillSnakeStepOutcome.BedrockCollision ||
            Outcome == DrillSnakeStepOutcome.Overheated;
    }
}
