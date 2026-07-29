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
        [SerializeField, Min(0.03f)] private float bankSegmentSeconds = 0.09f;

        [Header("Ore Integrity")]
        [SerializeField, Min(1)] private int commonOreHealth = 2;
        [SerializeField, Min(1)] private int rareOreHealth = 3;
        [SerializeField, Min(1)] private int veryRareOreHealth = 4;

        [Header("Turret and Pickups")]
        [SerializeField, Min(1f)] private float turretRange = 5.5f;
        [SerializeField, Min(0.1f)] private float turretFireInterval = 0.62f;
        [SerializeField, Min(1)] private int turretDamage = 1;
        [SerializeField, Min(0.05f)] private float projectileTravelSeconds = 0.26f;
        [SerializeField, Range(0.1f, 0.6f)] private float projectileSize = 0.26f;
        [SerializeField, Range(1, 6)] private int oreFragmentCount = 3;
        [SerializeField, Range(0f, 3f)] private float orePickupRadius = 1.5f;
        [SerializeField, Min(1f)] private float drillPowerupDuration = 10f;

        [Header("Heat")]
        [SerializeField, Min(1f)] private float baseMaximumHeat = 100f;
        [SerializeField, Range(0f, 3f)] private float maximumHeatSpeedBonus = 1.4f;
        [SerializeField, Min(0f)] private float coolingUpgradeCapacity = 18f;
        [SerializeField, Min(0f)] private float movementHeat = 0.55f;
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

        public float GetMoveInterval(
            int driveSpeedLevel,
            bool boosting,
            bool slowTesting,
            float heat = 0f)
        {
            var normalInterval = Mathf.Max(
                0.07f,
                movementTickSeconds - driveSpeedLevel * speedUpgradeReduction);
            var baseInterval = boosting
                ? Mathf.Min(normalInterval, boostTickSeconds)
                : normalInterval;
            var interval = Mathf.Max(
                0.045f,
                baseInterval / (1f + GetHeatSpeedBonus(heat)));
            return slowTesting ? interval * slowTestingMultiplier : interval;
        }

        public float GetHeatSpeedBonus(float heat)
        {
            return GetHeatRatio(heat) * maximumHeatSpeedBonus;
        }

        public float GetHeatRatio(float heat)
        {
            return Mathf.Clamp01(
                Mathf.Max(0f, heat) /
                Mathf.Max(1f, baseMaximumHeat));
        }

        public int GetCellDurability(DrillSnakeCellType cellType)
        {
            return cellType switch
            {
                DrillSnakeCellType.CommonOre => commonOreHealth,
                DrillSnakeCellType.RareOre => rareOreHealth,
                DrillSnakeCellType.VeryRareOre => veryRareOreHealth,
                _ => 0
            };
        }

        public float TurretRange => turretRange;

        public float TurretFireInterval => turretFireInterval;

        public int TurretDamage => turretDamage;

        public float ProjectileTravelSeconds => projectileTravelSeconds;

        public float ProjectileSize => projectileSize;

        public int OreFragmentCount => oreFragmentCount;

        public float OrePickupRadius => orePickupRadius;

        public float DrillPowerupDuration => drillPowerupDuration;

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
