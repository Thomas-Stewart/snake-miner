using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class SkillTreeIconResolver
{
	private const string IconsFolderName = "skill_tree_icons";
	private static readonly Dictionary<string, Sprite> _cachedIconsByKey = new(System.StringComparer.OrdinalIgnoreCase);
	private static readonly HashSet<string> _missingKeysLogged = new(System.StringComparer.OrdinalIgnoreCase);
	private static Dictionary<string, Sprite> _resourcesIconsByKey;

	public static Sprite GetIcon(string modKey)
	{
		if (string.IsNullOrWhiteSpace(modKey))
			return null;

		if (_cachedIconsByKey.TryGetValue(modKey, out var cachedIcon))
			return cachedIcon;

		var icon = LoadFromLocalPng(modKey);
		if (icon == null)
			icon = LoadFromResources(modKey);
		#if UNITY_EDITOR
		if (icon == null)
			icon = LoadFromProjectAssets(modKey);
		#endif

		_cachedIconsByKey[modKey] = icon;
		if (icon == null && _missingKeysLogged.Add(modKey))
			Debug.LogWarning($"SkillTreeIconResolver: Could not find a PNG/Sprite named '{modKey}' for the skill tree icon.");

		return icon;
	}

	public static void ReloadIcons(IEnumerable<UpgradeNode> upgrades)
	{
		ClearCache();
		if (upgrades == null)
			return;

		var keys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		foreach (var upgrade in upgrades)
		{
			if (string.IsNullOrWhiteSpace(upgrade.type) || !keys.Add(upgrade.type))
				continue;

			GetIcon(upgrade.type);
		}
	}

	public static void ClearCache()
	{
		_cachedIconsByKey.Clear();
		_missingKeysLogged.Clear();
		_resourcesIconsByKey = null;
	}

	private static Sprite LoadFromResources(string modKey)
	{
		var directIcon = Resources.Load<Sprite>(modKey);
		if (directIcon != null)
			return directIcon;

		_resourcesIconsByKey ??= BuildResourcesIconMap();
		return _resourcesIconsByKey.TryGetValue(modKey, out var icon) ? icon : null;
	}

	private static Sprite LoadFromLocalPng(string modKey)
	{
		var paths = GetLocalIconPaths(modKey);
		for (var i = 0; i < paths.Length; i++)
		{
			var path = paths[i];
			if (!File.Exists(path))
				continue;

			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			if (!texture.LoadImage(File.ReadAllBytes(path)))
				continue;

			texture.name = modKey;
			return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
		}

		return null;
	}

	private static string[] GetLocalIconPaths(string modKey)
	{
		var dataPath = Application.dataPath;
		var dataFolderParent = Path.GetDirectoryName(dataPath);
		var appBundleOrExeDirParent = string.IsNullOrEmpty(dataFolderParent) ? null : Path.GetDirectoryName(dataFolderParent);
		var fileName = modKey + ".png";

		return new[]
		{
			Path.Combine(dataPath, IconsFolderName, fileName),
			Path.Combine(Application.streamingAssetsPath, IconsFolderName, fileName),
			string.IsNullOrEmpty(dataFolderParent) ? null : Path.Combine(dataFolderParent, IconsFolderName, fileName),
			string.IsNullOrEmpty(appBundleOrExeDirParent) ? null : Path.Combine(appBundleOrExeDirParent, IconsFolderName, fileName),
			Path.Combine(Application.persistentDataPath, IconsFolderName, fileName)
		};
	}

	private static Dictionary<string, Sprite> BuildResourcesIconMap()
	{
		var iconsByKey = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);
		var sprites = Resources.LoadAll<Sprite>(string.Empty);
		for (var i = 0; i < sprites.Length; i++)
		{
			var sprite = sprites[i];
			if (sprite == null || string.IsNullOrWhiteSpace(sprite.name))
				continue;

			iconsByKey.TryAdd(sprite.name, sprite);
		}

		return iconsByKey;
	}

	#if UNITY_EDITOR
	private static Sprite LoadFromProjectAssets(string modKey)
	{
		var spriteGuids = AssetDatabase.FindAssets($"{modKey} t:Sprite");
		for (var i = 0; i < spriteGuids.Length; i++)
		{
			var path = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
			if (!IsExactFileNameMatch(path, modKey))
				continue;

			var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
			if (sprite != null)
				return sprite;
		}

		var textureGuids = AssetDatabase.FindAssets($"{modKey} t:Texture2D");
		for (var i = 0; i < textureGuids.Length; i++)
		{
			var path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
			if (!IsExactFileNameMatch(path, modKey))
				continue;

			var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
			if (sprite != null)
				return sprite;
		}

		return null;
	}

	private static bool IsExactFileNameMatch(string assetPath, string modKey)
	{
		var fileName = Path.GetFileNameWithoutExtension(assetPath);
		return string.Equals(fileName, modKey, System.StringComparison.OrdinalIgnoreCase);
	}
	#endif
}
