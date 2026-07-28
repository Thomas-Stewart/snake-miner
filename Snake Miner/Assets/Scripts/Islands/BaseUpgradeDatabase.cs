using System;
using System.Collections.Generic;
using System.Linq;
using SSG_Core.Scripts.Util;
using Unity.Mathematics;
using UnityEngine;

namespace Islands
{
	[CreateAssetMenu(fileName = nameof(BaseUpgradeDatabase), menuName = "SSG/BaseUpgradeDatabase")]
	public class BaseUpgradeDatabase : DatabaseSingleton<BaseUpgradeDatabase>
	{
		[SerializeField] private List<BaseUpgradeData> _baseUpgradeData;

		public BaseUpgradeData GetBaseUpgradeDatas(int upgradeLevel)
		{
			var upgrades = _baseUpgradeData.Where(b => b.UpgradeLevel == upgradeLevel).ToList();
			if (upgrades == null || upgrades.Count == 0)
				Debug.LogError("no base upgrades found for upgrade level: " + upgradeLevel);
			if (upgrades.Count > 1)
				Debug.LogError("multiple upgrades found for level : " + upgradeLevel);
				
			return upgrades[0];
		}
	}

	[Serializable]
	public struct BaseUpgradeData
	{
		public int UpgradeLevel;
		public int2 GridExpansion;
		public float BaseHealthAddition;
		// public HumanoidDefinition[] UnlockedUnits;
	}
}