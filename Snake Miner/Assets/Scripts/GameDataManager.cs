using System.IO;
using System.Collections.Generic;
using System.Linq;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.UI;
using SSG.Util;
using UnityEngine;

public class GameDataManager : Singleton<GameDataManager>
{
	private const string GameConfigFileName = "game_config.json";
	private const string IslandMetadataConfigFileName = "island_metadata_config.json";
	private const string RuntimeBuffSuffix = "_BUFF";

	[SerializeField] private TextAsset _skillTreeJson;
	[SerializeField] private TextAsset _islandMetadataJson;
	[SerializeField] private bool _showConfigErrorsAsToasts = true;
	[SerializeField] private int _maxConfigErrorToasts = 5;
	[SerializeField] private float _configErrorToastDuration = 4f;
	public event System.Action OnRuntimeModsChanged;
	public GameConfig GameConfig { get; private set; }
	public SkillTreeData SkillTreeData { get; private set; }
	public Mod[] Mods { get; private set; }
	public IslandMetadataConfig[] IslandMetadataConfigs { get; private set; }
	private readonly List<string> _configErrorsThisRead = new List<string>();
	private readonly Dictionary<string, float> _runtimeModValues = new Dictionary<string, float>();
	private readonly Dictionary<string, ConfiguredModLookup> _configuredModLookupsByKey = new Dictionary<string, ConfiguredModLookup>();
	private readonly Dictionary<string, ResolvedModValue> _resolvedModValuesByKey = new Dictionary<string, ResolvedModValue>();
	private readonly Dictionary<string, float> _unlockedUpgradeValueSumsByType = new Dictionary<string, float>();
	private readonly Dictionary<string, int> _unlockedUpgradeCountsByType = new Dictionary<string, int>();
	private int _cachedUpgradeSaveDataVersion = int.MinValue;
	private int _runtimeModRevision;
	private Coroutine _configErrorToastRoutine;

	private readonly struct ConfiguredModLookup
	{
		public ConfiguredModLookup(float configuredValue, string runtimeBuffKey, bool isScaleKey)
		{
			ConfiguredValue = configuredValue;
			RuntimeBuffKey = runtimeBuffKey;
			IsScaleKey = isScaleKey;
		}

		public float ConfiguredValue { get; }
		public string RuntimeBuffKey { get; }
		public bool IsScaleKey { get; }
	}

	private readonly struct ResolvedModValue
	{
		public ResolvedModValue(int saveDataVersion, int runtimeModRevision, float value)
		{
			SaveDataVersion = saveDataVersion;
			RuntimeModRevision = runtimeModRevision;
			Value = value;
		}

		public int SaveDataVersion { get; }
		public int RuntimeModRevision { get; }
		public float Value { get; }
	}

	protected override void Awake()
	{
		base.Awake();
		ReadInGameData();
	}
	
	public void ReadInGameData()
	{
		_configErrorsThisRead.Clear();

		try
		{
			GameConfig = GameConfigParser.Parse(ReadJsonWithBuildOverride(GameConfigFileName, GetFallbackConfigAsset(GameConfigFileName, _skillTreeJson)));
			IslandMetadataConfigs = IslandMetadataConfigParser.Parse(ReadJsonWithBuildOverride(IslandMetadataConfigFileName, GetFallbackConfigAsset(IslandMetadataConfigFileName, _islandMetadataJson)));
			SkillTreeData = GameConfig.SkillTreeData;
			Mods = GameConfig.Mods;
		}
		catch (System.Exception e)
		{
			ReportConfigError($"Config parse failed: {e.Message}");
			GameConfig = new GameConfig
			{
				SkillTreeData = new SkillTreeData
				{
					cell_px = 0,
					cols = 0,
					rows = 0,
					start_x = 0,
					start_y = 0,
					Upgrades = System.Array.Empty<UpgradeNode>(),
					indexByPos = new Dictionary<GridPos, int>()
				},
				Mods = System.Array.Empty<Mod>()
			};
			IslandMetadataConfigs = System.Array.Empty<IslandMetadataConfig>();
			SkillTreeData = GameConfig.SkillTreeData;
			Mods = GameConfig.Mods;
		}

		RebuildConfiguredModCache();
		InvalidateUnlockedUpgradeCache();
		ValidateGameConfig();
		SkillTreeIconResolver.ReloadIcons(SkillTreeData.Upgrades);
		TryShowConfigErrorsAsToasts();
	}

	public bool TryApplyPlaytestConfigOverrides(
		string gameConfigJson,
		string islandMetadataJson,
		out string error)
	{
		error = null;
		if (string.IsNullOrWhiteSpace(gameConfigJson) && string.IsNullOrWhiteSpace(islandMetadataJson))
			return true;

		GameConfig parsedGameConfig;
		IslandMetadataConfig[] parsedIslandMetadata;
		try
		{
			parsedGameConfig = string.IsNullOrWhiteSpace(gameConfigJson)
				? GameConfig
				: GameConfigParser.Parse(gameConfigJson);
			parsedIslandMetadata = string.IsNullOrWhiteSpace(islandMetadataJson)
				? IslandMetadataConfigs
				: IslandMetadataConfigParser.Parse(islandMetadataJson);
		}
		catch (System.Exception exception)
		{
			error = $"Config parse failed: {exception.Message}";
			return false;
		}

		_configErrorsThisRead.Clear();
		GameConfig = parsedGameConfig;
		IslandMetadataConfigs = parsedIslandMetadata ?? System.Array.Empty<IslandMetadataConfig>();
		SkillTreeData = GameConfig.SkillTreeData;
		Mods = GameConfig.Mods;
		RebuildConfiguredModCache();
		InvalidateUnlockedUpgradeCache();
		ValidateGameConfig();
		SkillTreeIconResolver.ReloadIcons(SkillTreeData.Upgrades);
		TryShowConfigErrorsAsToasts();

		if (_configErrorsThisRead.Count > 0)
		{
			error = string.Join(" | ", _configErrorsThisRead);
			return false;
		}

		OnRuntimeModsChanged?.Invoke();
		return true;
	}

	private static string ReadJsonWithBuildOverride(string fileName, TextAsset fallbackAsset)
	{
		var candidatePaths = GetBuildLocalPaths(fileName);
		foreach (var candidatePath in candidatePaths)
		{
			if (!File.Exists(candidatePath))
				continue;

			var json = File.ReadAllText(candidatePath);
			if (string.IsNullOrWhiteSpace(json))
			{
				Debug.LogWarning($"GameDataManager: Found local override at '{candidatePath}' but it was empty. Falling back.");
				continue;
			}

			Debug.Log($"GameDataManager: Loaded '{fileName}' from local build file '{candidatePath}'.");
			return json;
		}

		Debug.LogWarning($"GameDataManager: No local override found for '{fileName}'. Checked paths: {string.Join(", ", candidatePaths)}");

		if (fallbackAsset == null)
			throw new FileNotFoundException($"GameDataManager: Could not find local override or fallback TextAsset for '{fileName}'.");

		return fallbackAsset.text;
	}

	private static string[] GetBuildLocalPaths(string fileName)
	{
		var dataPath = Application.dataPath;
		var dataFolderParent = Path.GetDirectoryName(dataPath);
		var appBundleOrExeDirParent = string.IsNullOrEmpty(dataFolderParent) ? null : Path.GetDirectoryName(dataFolderParent);

		return new[]
		{
			Path.Combine(dataPath, fileName),
			Path.Combine(Application.streamingAssetsPath, fileName),
			string.IsNullOrEmpty(dataFolderParent) ? null : Path.Combine(dataFolderParent, fileName),
			string.IsNullOrEmpty(appBundleOrExeDirParent) ? null : Path.Combine(appBundleOrExeDirParent, fileName),
			Path.Combine(Application.persistentDataPath, fileName)
		}
		.Where(p => !string.IsNullOrEmpty(p))
		.Distinct()
		.ToArray();
	}

	private static TextAsset GetFallbackConfigAsset(string fileName, TextAsset serializedFallback)
	{
		if (serializedFallback != null)
			return serializedFallback;

		var resourceName = Path.GetFileNameWithoutExtension(fileName);
		return Resources.Load<TextAsset>(resourceName);
	}

	public float GetModValue(string modKey)
	{
		if (IsRuntimeBuffKey(modKey))
			return GetRuntimeModValue(modKey);

		if (!TryGetConfiguredModLookup(modKey, out var lookup))
		{
			Debug.LogError($"GameDataManager: Missing required mod key '{modKey}'. Returning 0.");
			return 0f;
		}

		var saveDataVersion = SaveUtil.SaveDataVersion;
		if (_resolvedModValuesByKey.TryGetValue(modKey, out var cachedValue) &&
		    cachedValue.SaveDataVersion == saveDataVersion &&
		    cachedValue.RuntimeModRevision == _runtimeModRevision)
		{
			return cachedValue.Value;
		}

		float resolvedValue;
		if (lookup.IsScaleKey)
		{
			var scaleValue = lookup.ConfiguredValue;
			if (TryGetUnlockedUpgradeValuesSum(modKey, out var unlockedScaleAdd))
				scaleValue += unlockedScaleAdd;
			resolvedValue = scaleValue + GetRuntimeModValue(lookup.RuntimeBuffKey);
		}
		else
		{
			var value = lookup.ConfiguredValue;
			if (TryGetUnlockedUpgradeValuesSum(modKey, out var unlockedValueAdd))
				value += unlockedValueAdd;
			resolvedValue = value + GetRuntimeModValue(lookup.RuntimeBuffKey);
		}

		_resolvedModValuesByKey[modKey] = new ResolvedModValue(
			saveDataVersion,
			_runtimeModRevision,
			resolvedValue);
		return resolvedValue;
	}

	public void SetRuntimeModValue(string modKey, float value)
	{
		if (Mathf.Approximately(value, 0f))
		{
			if (!_runtimeModValues.Remove(modKey))
				return;
		}
		else
		{
			if (_runtimeModValues.TryGetValue(modKey, out var existingValue) && Mathf.Approximately(existingValue, value))
				return;

			_runtimeModValues[modKey] = value;
		}

		NotifyRuntimeModsChanged();
	}

	public void SetRuntimeModValues(IReadOnlyDictionary<string, float> valuesByModKey)
	{
		if (valuesByModKey == null || valuesByModKey.Count == 0)
			return;

		var changed = false;
		foreach (var kvp in valuesByModKey)
		{
			if (Mathf.Approximately(kvp.Value, 0f))
			{
				changed |= _runtimeModValues.Remove(kvp.Key);
			}
			else
			{
				if (_runtimeModValues.TryGetValue(kvp.Key, out var existingValue) && Mathf.Approximately(existingValue, kvp.Value))
					continue;

				_runtimeModValues[kvp.Key] = kvp.Value;
				changed = true;
			}
		}

		if (!changed)
			return;

		NotifyRuntimeModsChanged();
	}

	public void ClearRuntimeModValue(string modKey)
	{
		if (!_runtimeModValues.Remove(modKey))
			return;

		NotifyRuntimeModsChanged();
	}

	public void ClearRuntimeModValues(IEnumerable<string> modKeys)
	{
		if (modKeys == null)
			return;

		var changed = false;
		foreach (var modKey in modKeys)
			changed |= _runtimeModValues.Remove(modKey);

		if (changed)
			NotifyRuntimeModsChanged();
	}

	private bool TryGetConfiguredModLookup(string key, out ConfiguredModLookup lookup)
	{
		if (Mods == null)
		{
			lookup = default;
			Debug.LogError($"GameDataManager: Mods array is null while looking up '{key}'.");
			return false;
		}

		return _configuredModLookupsByKey.TryGetValue(key, out lookup);
	}

	public bool HasConfiguredMod(string key)
	{
		return Mods != null && _configuredModLookupsByKey.ContainsKey(key);
	}

	private float GetRuntimeModValue(string modKey)
	{
		return _runtimeModValues.TryGetValue(modKey, out var value) ? value : 0f;
	}

	private static bool IsRuntimeBuffKey(string modKey)
	{
		return !string.IsNullOrEmpty(modKey) && modKey.EndsWith(RuntimeBuffSuffix, System.StringComparison.Ordinal);
	}

	private bool TryGetUnlockedUpgradeValuesSum(string upgradeType, out float sum)
	{
		EnsureUnlockedUpgradeCache();
		if (!_unlockedUpgradeValueSumsByType.TryGetValue(upgradeType, out sum))
		{
			sum = -1;
			return false;
		}

		return true;
	}

	public float GetScaleMultiplier(string scaleKey)
	{
		var multiplier = GetModValue(scaleKey);
		if (multiplier <= 0f)
		{
			Debug.LogError($"GameDataManager: Scale key '{scaleKey}' resolved to non-positive multiplier '{multiplier}'. Clamping to 0.01.");
			return 0.01f;
		}

		return multiplier;
	}

	public bool TryGetIslandMetadataConfig(int islandId, out IslandMetadataConfig islandConfig)
	{
		islandConfig = default;
		var islands = IslandMetadataConfigs;
		if (islands == null)
			return false;

		for (var i = 0; i < islands.Length; i++)
		{
			if (islands[i].island_id != islandId)
				continue;

			islandConfig = islands[i];
			return true;
		}

		return false;
	}

	public bool AreWaterObjectsDisabledForCurrentIsland()
	{
		return false;
	}

	public void NotifyRuntimeModsChanged()
	{
		_runtimeModRevision++;
		OnRuntimeModsChanged?.Invoke();
	}
	
	public int GetUnlockedUpgradeQuantityByType(string upgradeType)
	{
		EnsureUnlockedUpgradeCache();
		return _unlockedUpgradeCountsByType.TryGetValue(upgradeType, out var count) ? count : 0;
	}

	private void RebuildConfiguredModCache()
	{
		_configuredModLookupsByKey.Clear();
		_resolvedModValuesByKey.Clear();
		if (Mods == null)
			return;

		for (var i = 0; i < Mods.Length; i++)
		{
			var mod = Mods[i];
			if (string.IsNullOrEmpty(mod.type))
				continue;

			if (_configuredModLookupsByKey.ContainsKey(mod.type))
				continue;

			_configuredModLookupsByKey[mod.type] = new ConfiguredModLookup(
				mod.value,
				string.Concat(mod.type, RuntimeBuffSuffix),
				GameMods.IsScaleKey(mod.type));
		}
	}

	private void InvalidateUnlockedUpgradeCache()
	{
		_cachedUpgradeSaveDataVersion = int.MinValue;
		_unlockedUpgradeValueSumsByType.Clear();
		_unlockedUpgradeCountsByType.Clear();
	}

	private void EnsureUnlockedUpgradeCache()
	{
		if (_cachedUpgradeSaveDataVersion == SaveUtil.SaveDataVersion)
			return;

		_cachedUpgradeSaveDataVersion = SaveUtil.SaveDataVersion;
		_unlockedUpgradeValueSumsByType.Clear();
		_unlockedUpgradeCountsByType.Clear();

		if (!SaveUtil.IsSaveDataReady || SkillTreeData.Upgrades == null)
			return;

		var saveData = SaveUtil.SaveData;
		if (saveData.SavedUpgrades == null || saveData.SavedUpgrades.Count == 0)
			return;

		var unlockedCoords = new HashSet<GridPos>();
		for (var i = 0; i < saveData.SavedUpgrades.Count; i++)
		{
			var savedUpgrade = saveData.SavedUpgrades[i];
			unlockedCoords.Add(savedUpgrade.Coords);
		}

		if (unlockedCoords.Count == 0)
			return;

		var upgrades = SkillTreeData.Upgrades;
		for (var i = 0; i < upgrades.Length; i++)
		{
			var upgrade = upgrades[i];
			if (!unlockedCoords.Contains(upgrade.gridPos))
				continue;

			if (_unlockedUpgradeValueSumsByType.TryGetValue(upgrade.type, out var sum))
				_unlockedUpgradeValueSumsByType[upgrade.type] = sum + upgrade.value;
			else
				_unlockedUpgradeValueSumsByType[upgrade.type] = upgrade.value;

			if (_unlockedUpgradeCountsByType.TryGetValue(upgrade.type, out var count))
				_unlockedUpgradeCountsByType[upgrade.type] = count + 1;
			else
				_unlockedUpgradeCountsByType[upgrade.type] = 1;
		}
	}

	private void ValidateGameConfig()
	{
		var errors = 0;
		if (SkillTreeData.Upgrades == null)
		{
			ReportConfigError("GameDataManager Validation: SkillTreeData.Upgrades is null.");
			errors++;
		}

		if (Mods == null)
		{
			ReportConfigError("GameDataManager Validation: GameConfig.Mods is null.");
			errors++;
		}

		if (IslandMetadataConfigs == null)
		{
			ReportConfigError("GameDataManager Validation: Island metadata is null.");
			errors++;
		}

		if (errors > 0)
		{
			ReportConfigError($"GameDataManager Validation: Found {errors} schema errors and cannot continue validation.");
			return;
		}

		var seenModKeys = new HashSet<string>();
		for (var i = 0; i < Mods.Length; i++)
		{
			var mod = Mods[i];
			if (string.IsNullOrEmpty(mod.type))
			{
				ReportConfigError($"GameDataManager Validation: Base Values[{i}] has empty type.");
				errors++;
				continue;
			}

			if (!seenModKeys.Add(mod.type))
			{
				ReportConfigError($"GameDataManager Validation: Duplicate Base Values key '{mod.type}'.");
				errors++;
			}

			if (!GameMods.IsKnownKey(mod.type))
			{
				ReportConfigError($"GameDataManager Validation: Unknown Base Values key '{mod.type}'.");
				errors++;
			}

		}

		foreach (var knownKey in GameMods.GetAllKeys())
		{
			if (seenModKeys.Contains(knownKey))
				continue;

			ReportConfigError($"GameDataManager Validation: Missing Base Values key '{knownKey}'.");
			errors++;
		}

		var seenIslandIds = new HashSet<int>();
		for (var i = 0; i < IslandMetadataConfigs.Length; i++)
		{
			var island = IslandMetadataConfigs[i];
			if (island.island_id < 1)
			{
				ReportConfigError($"GameDataManager Validation: islands[{i}] has invalid island_id '{island.island_id}'. Expected 1 or higher.");
				errors++;
			}

			if (!seenIslandIds.Add(island.island_id))
			{
				ReportConfigError($"GameDataManager Validation: Duplicate island_id '{island.island_id}'.");
				errors++;
			}

			var fishConfigs = island.fish_data ?? System.Array.Empty<FishSpawnConfigByType>();
			for (var fishIndex = 0; fishIndex < fishConfigs.Length; fishIndex++)
			{
				var fishConfig = fishConfigs[fishIndex];
				if (string.IsNullOrWhiteSpace(fishConfig.type))
				{
					ReportConfigError($"GameDataManager Validation: islands[{i}].fish_data[{fishIndex}] has empty type.");
					errors++;
				}

				var ranges = fishConfig.size_spawn_ranges ?? System.Array.Empty<FishSizeSpawnRange>();
				for (var rangeIndex = 0; rangeIndex < ranges.Length; rangeIndex++)
				{
					var range = ranges[rangeIndex];
					if (range.size <= 0f)
					{
						ReportConfigError($"GameDataManager Validation: islands[{i}].fish_data[{fishIndex}].size_spawn_ranges[{rangeIndex}] has non-positive size '{range.size}'.");
						errors++;
					}

					if (range.spawn_ratio < 0f)
					{
						ReportConfigError($"GameDataManager Validation: islands[{i}].fish_data[{fishIndex}].size_spawn_ranges[{rangeIndex}] has negative spawn_ratio '{range.spawn_ratio}'.");
						errors++;
					}

					if (range.min_distance_from_land < 0f)
					{
						ReportConfigError($"GameDataManager Validation: islands[{i}].fish_data[{fishIndex}].size_spawn_ranges[{rangeIndex}] has negative min_distance_from_land '{range.min_distance_from_land}'.");
						errors++;
					}

					if (range.max_distance_from_land < range.min_distance_from_land)
					{
						ReportConfigError($"GameDataManager Validation: islands[{i}].fish_data[{fishIndex}].size_spawn_ranges[{rangeIndex}] has max_distance_from_land smaller than min_distance_from_land.");
						errors++;
					}
				}

				var ratioSum = ranges.Sum(r => r.spawn_ratio);
				if (ranges.Length > 0 && ratioSum > 1.001f)
				{
					ReportConfigError($"GameDataManager Validation: islands[{i}].fish_data[{fishIndex}] spawn_ratio values must not exceed 1. Current sum: {ratioSum:0.###}.");
					errors++;
				}
			}

			if (island.money_quota < 0)
			{
				ReportConfigError($"GameDataManager Validation: islands[{i}].money_quota must be 0 or higher.");
				errors++;
			}

			if (island.penguin_count < 0)
			{
				ReportConfigError($"GameDataManager Validation: islands[{i}].penguin_count must be 0 or higher.");
				errors++;
			}
		}

		var seenUpgradeCells = new Dictionary<GridPos, int>();
		for (var i = 0; i < SkillTreeData.Upgrades.Length; i++)
		{
			var upgrade = SkillTreeData.Upgrades[i];
			if (string.IsNullOrEmpty(upgrade.type))
			{
				ReportConfigError($"GameDataManager Validation: Upgrades[{i}] has empty type at {upgrade.gridPos}.");
				errors++;
				continue;
			}

			if (!GameMods.IsKnownKey(upgrade.type))
			{
				ReportConfigError($"GameDataManager Validation: Unknown upgrade key '{upgrade.type}' at {upgrade.gridPos}.");
				errors++;
			}

			if (seenUpgradeCells.TryGetValue(upgrade.gridPos, out var firstIndex))
			{
				var firstType = SkillTreeData.Upgrades[firstIndex].type;
				ReportConfigError($"GameDataManager Validation: Duplicate upgrade cell at {upgrade.gridPos} between Upgrades[{firstIndex}] '{firstType}' and Upgrades[{i}] '{upgrade.type}'.");
				errors++;
			}
			else
			{
				seenUpgradeCells[upgrade.gridPos] = i;
			}

			if (!GameConfigParser.TryGetFloat(upgrade.varsJson, "cost", out _))
			{
				ReportConfigError($"GameDataManager Validation: Upgrade '{upgrade.type}' at {upgrade.gridPos} is missing vars.cost.");
				errors++;
			}

			if (!GameConfigParser.TryGetFloat(upgrade.varsJson, "value", out _))
			{
				ReportConfigError($"GameDataManager Validation: Upgrade '{upgrade.type}' at {upgrade.gridPos} is missing vars.value.");
				errors++;
			}

			if (GameConfigParser.TryGetInt(upgrade.varsJson, "level_id", out var levelId) && levelId < -1)
			{
				ReportConfigError($"GameDataManager Validation: Upgrade '{upgrade.type}' at {upgrade.gridPos} has invalid vars.level_id '{levelId}'. Expected -1 or higher.");
				errors++;
			}

		}

		if (errors > 0)
		{
			ReportConfigError($"GameDataManager Validation: Found {errors} config/schema issues in game_config.json.");
		}
		else
		{
			Debug.Log("GameDataManager Validation: game_config.json schema validated successfully.");
		}
	}

	private void ReportConfigError(string message)
	{
		Debug.LogError(message);
		_configErrorsThisRead.Add(message);
	}

	private void TryShowConfigErrorsAsToasts()
	{
		if (!_showConfigErrorsAsToasts || _configErrorsThisRead.Count == 0)
			return;

		if (_configErrorToastRoutine != null)
			StopCoroutine(_configErrorToastRoutine);

		_configErrorToastRoutine = StartCoroutine(ShowConfigErrorsAsToastsRoutine());
	}

	private System.Collections.IEnumerator ShowConfigErrorsAsToastsRoutine()
	{
		var timeout = Time.realtimeSinceStartup + 5f;
		while (ToastManager.Instance == null && Time.realtimeSinceStartup < timeout)
			yield return null;

		if (ToastManager.Instance == null)
			yield break;

		var maxToasts = Mathf.Max(1, _maxConfigErrorToasts);
		var shown = 0;
		for (var i = 0; i < _configErrorsThisRead.Count && shown < maxToasts; i++)
		{
			var msg = _configErrorsThisRead[i];
			if (string.IsNullOrEmpty(msg))
				continue;

			ToastManager.ShowToast($"Config Error: {TrimForToast(msg)}", _configErrorToastDuration);
			shown++;
		}

		var remaining = _configErrorsThisRead.Count - shown;
		if (remaining > 0)
			ToastManager.ShowToast($"Config Error: +{remaining} more. Check logs.", _configErrorToastDuration);
	}

	private static string TrimForToast(string text)
	{
		const int maxLen = 120;
		if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
			return text;

		return text.Substring(0, maxLen - 3) + "...";
	}
}
