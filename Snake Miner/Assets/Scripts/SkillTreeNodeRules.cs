using SSG_Core.Scripts.Localization;
using SSG.Util;
using UnityEngine;

public static class SkillTreeNodeRules
{
	private const int DefaultLevelId = -1;
	private const int FirstPlayableLevelId = 1;
	private const string DemoLockedKey = "demo_locked";
	private const string IslandRequiredFormatLocId = "ui_skill_tree_island_required_format";
	private const string LockedForDemoLocId = "ui_skill_tree_locked_for_demo";

	public static int GetRequiredLevelId(UpgradeNode node)
	{
		if (!GameConfigParser.TryGetInt(node.varsJson, "level_id", out var levelId))
			return DefaultLevelId;

		return Mathf.Max(DefaultLevelId, levelId);
	}

	public static bool IsLevelRequirementMet(UpgradeNode node)
	{
		var requiredLevelId = GetRequiredLevelId(node);
		if (requiredLevelId < 0)
			return true;

		return GetCurrentLevelId() >= requiredLevelId;
	}

	public static string GetLevelRequirementText(UpgradeNode node)
	{
		var requiredLevelId = GetRequiredLevelId(node);
		if (requiredLevelId < 0)
			return string.Empty;

		return string.Format(Localizer.GetText(IslandRequiredFormatLocId), requiredLevelId);
	}

	public static bool IsDemoLocked(UpgradeNode node)
	{
		if (!IsDemoLockEnabled())
			return false;

		return GameConfigParser.TryGetFloat(node.varsJson, DemoLockedKey, out var demoLockedValue) && demoLockedValue > 0f;
	}

	public static string GetDemoLockedText()
	{
		return Localizer.GetText(LockedForDemoLocId);
	}

	private static int GetCurrentLevelId()
	{
		return Mathf.Max(FirstPlayableLevelId, SaveUtil.SaveData.CurrentLevelIndex + 1);
	}

	public static bool IsUnlockable(UpgradeNode node)
	{
		if (SaveUtil.IsUpgradeUnlocked(node.gridPos))
			return false;

		if (IsDemoLocked(node))
			return false;

		if (!IsLevelRequirementMet(node))
			return false;

		if (node.gridPos.x == 0 && node.gridPos.y == 0)
			return true;

		var conns = node.connections;
		if (conns == null || conns.Length == 0)
			return false;

		for (var i = 0; i < conns.Length; i++)
		{
			if (SaveUtil.IsUpgradeUnlocked(conns[i]))
				return true;
		}

		return false;
	}

	private static bool IsDemoLockEnabled()
	{
		return GameDataManager.Instance != null && GameDataManager.Instance.GetModValue(GameMods.SKILL_TREE_DEMO_LOCK_ENABLED) > 0f;
	}
}
