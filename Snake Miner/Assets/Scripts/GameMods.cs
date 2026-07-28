using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SSG_Core.Scripts.Localization;
using UnityEngine;

public static class GameMods
{
	public const string PLAYER_MOVE_SPEED_BASE = "PLAYER_MOVE_SPEED_BASE";
	public const string PLAYER_MOVE_SPEED_SCALE = "PLAYER_MOVE_SPEED_SCALE";
	public const string PLAYER_MAX_HEALTH_BASE = "PLAYER_MAX_HEALTH_BASE";
	public const string CURRENCY_GAIN_SCALE = "CURRENCY_GAIN_SCALE";
	public const string STARTING_ABILITY_UNLOCKED = "STARTING_ABILITY_UNLOCKED";
	public const string SKILL_TREE_DEMO_LOCK_ENABLED = "SKILL_TREE_DEMO_LOCK_ENABLED";

	private static readonly HashSet<string> _knownModKeys = typeof(GameMods)
		.GetFields(BindingFlags.Public | BindingFlags.Static)
		.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
		.Select(f => (string)f.GetRawConstantValue())
		.ToHashSet();

	private static readonly Dictionary<string, string> _titleByType = new Dictionary<string, string>
	{
		[PLAYER_MOVE_SPEED_BASE] = "Move Speed",
		[PLAYER_MOVE_SPEED_SCALE] = "Move Speed Multiplier",
		[PLAYER_MAX_HEALTH_BASE] = "Max Health",
		[CURRENCY_GAIN_SCALE] = "Currency Gain",
		[STARTING_ABILITY_UNLOCKED] = "Starting Ability",
		[SKILL_TREE_DEMO_LOCK_ENABLED] = "Skill Tree Demo Lock"
	};

	private static readonly Dictionary<string, Func<float, string>> _displayByType = new Dictionary<string, Func<float, string>>
	{
		[PLAYER_MOVE_SPEED_BASE] = PlainNumberText,
		[PLAYER_MOVE_SPEED_SCALE] = SignedPercentText,
		[PLAYER_MAX_HEALTH_BASE] = value => Mathf.Max(1, Mathf.RoundToInt(value)).ToString(),
		[CURRENCY_GAIN_SCALE] = SignedPercentText,
		[STARTING_ABILITY_UNLOCKED] = value => Localizer.GetText(value > 0f ? "ui_unlocked" : "ui_locked"),
		[SKILL_TREE_DEMO_LOCK_ENABLED] = value => Localizer.GetText(value > 0f ? "ui_on" : "ui_off")
	};

	private static readonly string[] _allModKeysSorted = _knownModKeys.OrderBy(k => k).ToArray();

	static GameMods()
	{
		ValidateMappings();
	}

	public static bool IsKnownKey(string key) => !string.IsNullOrEmpty(key) && _knownModKeys.Contains(key);
	public static IEnumerable<string> GetAllKeys() => _knownModKeys;
	public static bool IsScaleKey(string key) => !string.IsNullOrEmpty(key) && key.EndsWith("_SCALE");
	public static IReadOnlyList<string> GetAllModKeysList() => _allModKeysSorted;
	public static bool IsUnlockKey(string key) => !string.IsNullOrEmpty(key) && key.EndsWith("_UNLOCKED");

	public static string GetTitleForType(string upgradeType)
	{
		if (!_titleByType.TryGetValue(upgradeType, out var title))
		{
			Debug.LogError($"GameMods: Missing title mapping for mod key '{upgradeType}'.");
			return "Unknown";
		}

		var localizedTitle = Localizer.GetText(upgradeType);
		return string.IsNullOrWhiteSpace(localizedTitle) || localizedTitle.StartsWith("LOC_", StringComparison.Ordinal)
			? title
			: localizedTitle;
	}

	public static string GetDisplayValueForType(string upgradeType, float value)
	{
		if (!_displayByType.TryGetValue(upgradeType, out var formatter))
		{
			Debug.LogError($"GameMods: Missing display mapping for mod key '{upgradeType}'.");
			return value.ToString("0.##");
		}

		return formatter(value);
	}

	private static void ValidateMappings()
	{
		foreach (var key in _knownModKeys)
		{
			if (!_titleByType.ContainsKey(key))
				Debug.LogError($"GameMods: Missing GetTitleForType entry for key '{key}'.");

			if (!_displayByType.ContainsKey(key))
				Debug.LogError($"GameMods: Missing GetDisplayValueForType entry for key '{key}'.");
		}
	}

	private static string SignedPercentText(float fraction)
	{
		var percent = fraction * 100f;
		var rounded = Mathf.Round(percent * 10f) / 10f;
		if (rounded > 0f)
			return FormatLoc("ui_positive_percent_format", $"{rounded:0.#}");
		if (rounded < 0f)
			return FormatLoc("ui_negative_percent_format", $"{Mathf.Abs(rounded):0.#}");
		return FormatLoc("ui_percent_format", "0");
	}

	private static string PlainNumberText(float value)
	{
		return value.ToString("0.##");
	}

	private static string FormatLoc(string locId, params object[] args)
	{
		return string.Format(Localizer.GetText(locId), args);
	}
}
