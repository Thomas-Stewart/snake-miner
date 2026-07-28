using System.Collections.Generic;
using SSG_Core.Scripts.Util;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(LevelDatabase), menuName = "SSG/Level Database")]
public class LevelDatabase : DatabaseSingleton<LevelDatabase>
{
	[SerializeField] private List<GameObject> _levelLandMassPrefabs = new();

	public static IReadOnlyList<GameObject> GetLevelLandMassPrefabs()
	{
		return Instance != null ? Instance._levelLandMassPrefabs : null;
	}

	public static GameObject GetLevelLandMassPrefab(int index)
	{
		if (Instance == null || Instance._levelLandMassPrefabs == null || Instance._levelLandMassPrefabs.Count == 0)
			return null;

		var clampedIndex = Mathf.Clamp(index, 0, Instance._levelLandMassPrefabs.Count - 1);
		return Instance._levelLandMassPrefabs[clampedIndex];
	}

	public static int GetLevelCount()
	{
		return Instance != null && Instance._levelLandMassPrefabs != null
			? Instance._levelLandMassPrefabs.Count
			: 0;
	}
}
