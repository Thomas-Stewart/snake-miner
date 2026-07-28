using System;
using UnityEngine;

namespace DrillSnake
{
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
            int oreValue = 0)
        {
            Outcome = outcome;
            Cell = cell;
            OreType = oreType;
            OreValue = oreValue;
        }

        public DrillSnakeStepOutcome Outcome { get; }

        public Vector2Int Cell { get; }

        public DrillSnakeOreType OreType { get; }

        public int OreValue { get; }

        public bool ChangedTerrain =>
            Outcome == DrillSnakeStepOutcome.Drilled ||
            Outcome == DrillSnakeStepOutcome.CollectedOre;

        public bool Failed =>
            Outcome == DrillSnakeStepOutcome.BodyCollision ||
            Outcome == DrillSnakeStepOutcome.BedrockCollision ||
            Outcome == DrillSnakeStepOutcome.Overheated;
    }
}
