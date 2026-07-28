using System;
using UnityEngine;

namespace DrillSnake
{
    [Serializable]
    public sealed class DrillSnakeTuning
    {
        [Header("Movement")]
        [SerializeField, Min(0.05f)] private float movementTickSeconds = 0.2f;
        [SerializeField, Min(0.03f)] private float boostTickSeconds = 0.105f;
        [SerializeField, Min(1f)] private float slowTestingMultiplier = 3f;
        [SerializeField, Range(0.01f, 0.2f)] private float speedUpgradeReduction = 0.018f;
        [SerializeField, Min(0f)] private float drillDelaySeconds = 0.15f;
        [SerializeField, Range(0f, 0.75f)] private float drillUpgradeReduction = 0.18f;
        [SerializeField, Min(0.03f)] private float bankSegmentSeconds = 0.09f;

        [Header("Heat")]
        [SerializeField, Min(1f)] private float baseMaximumHeat = 100f;
        [SerializeField, Min(0f)] private float coolingUpgradeCapacity = 18f;
        [SerializeField, Min(0f)] private float movementHeat = 0.55f;
        [SerializeField, Min(0f)] private float drillingHeat = 4.5f;
        [SerializeField, Min(0f)] private float cargoHeatPerSegment = 0.055f;
        [SerializeField, Min(0f)] private float boostHeat = 1.8f;

        [Header("Ore")]
        [SerializeField, Min(1)] private int commonOreValue = 15;
        [SerializeField, Min(1)] private int rareOreValue = 50;
        [SerializeField, Min(1)] private int veryRareOreValue = 140;
        [SerializeField, Range(0f, 1f)] private float scannerValueBonus = 0.15f;

        [Header("Upgrade Costs")]
        [SerializeField, Min(1)] private int coolingBaseCost = 100;
        [SerializeField, Min(1)] private int drillMotorBaseCost = 130;
        [SerializeField, Min(1)] private int driveSpeedBaseCost = 160;
        [SerializeField, Min(1)] private int oreScannerBaseCost = 140;
        [SerializeField, Min(1.01f)] private float upgradeCostGrowth = 1.75f;

        public float BankSegmentSeconds => bankSegmentSeconds;

        public float GetMaximumHeat(int coolingLevel)
        {
            return baseMaximumHeat + coolingLevel * coolingUpgradeCapacity;
        }

        public float GetMoveHeat(int cargoCount, bool boosting)
        {
            var heat = movementHeat + cargoCount * cargoHeatPerSegment;
            return boosting ? heat + boostHeat : heat;
        }

        public float DrillingHeat => drillingHeat;

        public float GetMoveInterval(int driveSpeedLevel, bool boosting, bool slowTesting)
        {
            var normalInterval = Mathf.Max(
                0.07f,
                movementTickSeconds - driveSpeedLevel * speedUpgradeReduction);
            var interval = boosting ? Mathf.Min(normalInterval, boostTickSeconds) : normalInterval;
            return slowTesting ? interval * slowTestingMultiplier : interval;
        }

        public float GetDrillDelay(int drillMotorLevel)
        {
            var multiplier = Mathf.Pow(1f - drillUpgradeReduction, drillMotorLevel);
            return drillDelaySeconds * multiplier;
        }

        public int GetOreValue(DrillSnakeOreType oreType, int scannerLevel)
        {
            var baseValue = oreType switch
            {
                DrillSnakeOreType.Common => commonOreValue,
                DrillSnakeOreType.Rare => rareOreValue,
                DrillSnakeOreType.VeryRare => veryRareOreValue,
                _ => 0
            };
            return Mathf.RoundToInt(baseValue * (1f + scannerLevel * scannerValueBonus));
        }

        public int GetUpgradeCost(DrillSnakeUpgradeType type, int currentLevel)
        {
            var baseCost = type switch
            {
                DrillSnakeUpgradeType.Cooling => coolingBaseCost,
                DrillSnakeUpgradeType.DrillMotor => drillMotorBaseCost,
                DrillSnakeUpgradeType.DriveSpeed => driveSpeedBaseCost,
                DrillSnakeUpgradeType.OreScanner => oreScannerBaseCost,
                _ => coolingBaseCost
            };
            return Mathf.RoundToInt(baseCost * Mathf.Pow(upgradeCostGrowth, currentLevel));
        }
    }
}
