using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameConfigParser
{
    public static GameConfig Parse(string json)
    {
        // Standard format uses "Base Values", which JsonUtility cannot map directly.
        var normalizedJson = NormalizeGameConfigJson(json);

        // 1. Extract vars blocks manually
        var varsByIndex = ExtractVars(normalizedJson);
        var connectedCellIsNullByIndex = ExtractConnectedCellIsNull(normalizedJson);

        // 2. Deserialize known fields
        var wrapper = JsonUtility.FromJson<GridPos.GameConfigWrapper>(normalizedJson);
        if (wrapper?.BaseValues != null)
        {
            for (var i = 0; i < wrapper.BaseValues.Length; i++)
                wrapper.BaseValues[i].type = wrapper.BaseValues[i].type?.Trim();
        }
        if (wrapper?.Upgrades == null)
        {
            return new GameConfig
            {
                SkillTreeData = new SkillTreeData
                {
                    cell_px = 0,
                    cols = 0,
                    rows = 0,
                    start_x = 0,
                    start_y = 0,
                    Upgrades = Array.Empty<UpgradeNode>(),
                    indexByPos = new Dictionary<GridPos, int>()
                },
                Mods = wrapper?.BaseValues ?? Array.Empty<Mod>()
            };
        }

        var upgrades = new UpgradeNode[wrapper.Upgrades.Length];

        for (var i = 0; i < upgrades.Length; i++)
        {
            var u = wrapper.Upgrades[i];
            var cell = new GridPos(Mathf.RoundToInt(u.cell.x), -Mathf.RoundToInt(u.cell.y));
            var hasExplicitConnection = connectedCellIsNullByIndex.TryGetValue(i, out var isNull) && !isNull;
            var connectedCell = hasExplicitConnection
                ? new GridPos(Mathf.RoundToInt(u.connected_cell.x), -Mathf.RoundToInt(u.connected_cell.y))
                : cell;
            var varsJson = varsByIndex.TryGetValue(i, out var v) ? v : "{}";

            upgrades[i] = new UpgradeNode
            {
	            gridPos = cell,
                connect_x = connectedCell.x,
                connect_y = connectedCell.y,
                hasExplicitConnection = hasExplicitConnection,
                type = u.type,
                varsJson = varsJson,
                value = TryGetFloat(varsJson, "value", out var value) ? value : 0f,
                connections = Array.Empty<GridPos>()
            };
        }

        var data = new SkillTreeData
        {
            cell_px = wrapper.cell_px,
            cols = wrapper.cols,
            rows = wrapper.rows,
            start_x = wrapper.start_x,
            start_y = wrapper.start_y,
            Upgrades = upgrades,
            indexByPos = new Dictionary<GridPos, int>(upgrades.Length)
        };

        for (var i = 0; i < upgrades.Length; i++)
            data.indexByPos[upgrades[i].gridPos] = i;

        ResolveConnections(ref data);
        return new GameConfig
        {
            SkillTreeData = data,
            Mods = wrapper.BaseValues ?? Array.Empty<Mod>()
        };
    }

    private static string NormalizeGameConfigJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return json;

        return json.Replace("\"Base Values\"", "\"BaseValues\"");
    }

    // ----------------------------
    // Connection rules
    // ----------------------------

    private static void ResolveConnections(ref SkillTreeData data)
    {
        var dirs = new[]
        {
            new GridPos( 1, 0),
            new GridPos(-1, 0),
            new GridPos( 0, 1),
            new GridPos( 0,-1)
        };

        var connSets = new HashSet<GridPos>[data.Upgrades.Length];
        for (var i = 0; i < connSets.Length; i++)
            connSets[i] = new HashSet<GridPos>();

        // Implicit adjacency
        for (var i = 0; i < data.Upgrades.Length; i++)
        {
	        var p = data.Upgrades[i].gridPos;

            foreach (var d in dirs)
            {
                var n = new GridPos(p.x + d.x, p.y + d.y);
                if (!data.indexByPos.TryGetValue(n, out var j)) continue;

                connSets[i].Add(n);
                connSets[j].Add(p);
            }
        }

        // Explicit connections
        for (var i = 0; i < data.Upgrades.Length; i++)
        {
            var u = data.Upgrades[i];
            if (!u.hasExplicitConnection) continue;

            var from = u.gridPos;
            var to = new GridPos(u.connect_x, u.connect_y);

            if (!data.indexByPos.TryGetValue(to, out var j)) continue;

            connSets[i].Add(to);
            connSets[j].Add(from);
        }

        for (var i = 0; i < data.Upgrades.Length; i++)
        {
            var u = data.Upgrades[i];
            u.connections = new List<GridPos>(connSets[i]).ToArray();
            data.Upgrades[i] = u;
        }
    }

    // ----------------------------
    // VERY small vars extractor
    // ----------------------------

    private static Dictionary<int, string> ExtractVars(string json)
    {
        var dict = new Dictionary<int, string>();
        var idx = 0;
        var cursor = 0;

        while (true)
        {
            var varsIndex = json.IndexOf("\"vars\"", cursor, StringComparison.Ordinal);
            if (varsIndex == -1) break;

            var braceStart = json.IndexOf('{', varsIndex);
            if (braceStart == -1) break;

            var depth = 0;
            for (var i = braceStart; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') depth--;

                if (depth == 0)
                {
                    dict[idx++] = json.Substring(braceStart, i - braceStart + 1);
                    cursor = i + 1;
                    break;
                }
            }
        }

        return dict;
    }

    private static Dictionary<int, bool> ExtractConnectedCellIsNull(string json)
    {
        var dict = new Dictionary<int, bool>();
        var idx = 0;
        var cursor = 0;

        while (true)
        {
            var keyIndex = json.IndexOf("\"connected_cell\"", cursor, StringComparison.Ordinal);
            if (keyIndex == -1) break;

            var colonIndex = json.IndexOf(':', keyIndex);
            if (colonIndex == -1) break;

            var i = colonIndex + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

            var isNull = i + 3 < json.Length &&
                         json[i] == 'n' &&
                         json[i + 1] == 'u' &&
                         json[i + 2] == 'l' &&
                         json[i + 3] == 'l';

            dict[idx++] = isNull;
            cursor = i + 1;
        }

        return dict;
    }
    
    public static bool TryGetFloat(string json, string key, out float value)
    {
	    value = default;
	    if (string.IsNullOrEmpty(json)) return false;

	    var k = $"\"{key}\"";
	    var i = json.IndexOf(k, StringComparison.Ordinal);
	    if (i < 0) return false;

	    i = json.IndexOf(':', i);
	    if (i < 0) return false;
	    i++;

	    // Skip whitespace
	    while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

	    var start = i;
	    while (i < json.Length && 
	           (char.IsDigit(json[i]) || json[i] == '.' || json[i] == '-'))
	    {
		    i++;
	    }

	    return float.TryParse(
		    json.Substring(start, i - start),
		    System.Globalization.NumberStyles.Float,
		    System.Globalization.CultureInfo.InvariantCulture,
		    out value
	    );
    }

    public static bool TryGetInt(string json, string key, out int value)
    {
        value = default;
        if (!TryGetFloat(json, key, out var floatValue))
            return false;

        value = Mathf.RoundToInt(floatValue);
        return true;
    }

}

[Serializable]
public struct SkillTreeData
{
	public int cell_px;
	public int cols;
	public int rows;
	public int start_x;
	public int start_y;

	public UpgradeNode[] Upgrades;

	[NonSerialized] public Dictionary<GridPos, int> indexByPos;
}

[Serializable]
public struct UpgradeNode
{
	public GridPos gridPos;

	public int connect_x;
	public int connect_y;

	public bool hasExplicitConnection;

	public string type;
	public float value;

	// Raw JSON blob - tool does not care
	public string varsJson;

	[NonSerialized] public GridPos[] connections;
}

[Serializable]
public struct GridPos : IEquatable<GridPos>
{
	public int x;
	public int y;

	public GridPos(int x, int y)
	{
		this.x = x;
		this.y = y;
	}

	public bool Equals(GridPos other) => x == other.x && y == other.y;
	public override bool Equals(object obj) => obj is GridPos other && Equals(other);
	public override int GetHashCode() => (x * 397) ^ y;
	public override string ToString() => $"({x},{y})";
	
	[Serializable]
	internal class GameConfigWrapper
	{
		public UpgradeWrapper[] Upgrades;
		public Mod[] BaseValues;
		public int cell_px;
		public int cols;
		public int rows;
		public int start_x;
		public int start_y;
	}

	[Serializable]
	internal class UpgradeWrapper
	{
		public CellWrapper cell;
		public CellWrapper connected_cell;

		public string type;

		// JsonUtility ignores this, so we capture it manually
	}

	[Serializable]
	internal class CellWrapper
	{
		public float x;
		public float y;
	}

}

[Serializable]
public struct GameConfig
{
	public SkillTreeData SkillTreeData;
	public Mod[] Mods;
}

[Serializable]
public struct Mod
{
	public string type;
	public float value;
}

[Serializable]
public struct IslandMetadataConfig
{
	public int island_id;
	public FishSpawnConfigByType[] fish_data;
	public bool disable_water_objects;
	public int money_quota;
	public int penguin_count;
}

[Serializable]
public struct FishSpawnConfigByType
{
	public string type;
	public FishSizeSpawnRange[] size_spawn_ranges;
}

[Serializable]
public struct FishSizeSpawnRange
{
	public float size;
	public float spawn_ratio;
	public float min_distance_from_land;
	public float max_distance_from_land;
}

[Serializable]
public struct SpawnDistanceRange
{
	public float spawn_ratio;
	public float min_distance_from_land;
	public float max_distance_from_land;
}

public static class IslandMetadataConfigParser
{
	public static IslandMetadataConfig[] Parse(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return Array.Empty<IslandMetadataConfig>();

		var wrapper = JsonUtility.FromJson<IslandMetadataConfigWrapper>(json);
		return wrapper?.islands ?? Array.Empty<IslandMetadataConfig>();
	}

	[Serializable]
	private class IslandMetadataConfigWrapper
	{
		public IslandMetadataConfig[] islands;
	}
}
