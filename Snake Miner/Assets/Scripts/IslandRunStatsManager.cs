using SSG.Util;
using UnityEngine;

public class IslandRunStatsManager : MonoBehaviour
{
	public struct Snapshot
	{
		public int FishCaught;
		public long CoinsPickedUp;
		public long MoneyEarned;
		public int RodCasts;
		public int TurtlePets;
		public int PenguinsKicked;
		public float TotalIslandTimeSeconds;
		public float TotalSessionTimeSeconds;
		public float TimeToLastUpgradePurchaseSeconds;
		public bool PurchasedUpgradeThisIsland;
		public bool AllAvailableUpgradesPurchased;
		public int AvailableUnpurchasedUpgradeCount;
	}

	private static IslandRunStatsManager _instance;

	private int _fishCaught;
	private long _coinsPickedUp;
	private long _moneyEarned;
	private int _rodCasts;
	private int _turtlePets;
	private int _penguinsKicked;
	private float _sessionStartTime;
	private float _islandStartTime;
	private float _lastUpgradePurchaseTime = -1f;

	public static IslandRunStatsManager Instance
	{
		get
		{
			if (_instance != null)
				return _instance;

			_instance = FindAnyObjectByType<IslandRunStatsManager>();
			if (_instance != null)
				return _instance;

			var go = new GameObject(nameof(IslandRunStatsManager));
			_instance = go.AddComponent<IslandRunStatsManager>();
			return _instance;
		}
	}

	private void Awake()
	{
		if (_instance != null && _instance != this)
		{
			Destroy(gameObject);
			return;
		}

		_instance = this;
		_sessionStartTime = Time.time;
		ResetStats();
	}

	private void OnDestroy()
	{
		if (_instance == this)
			_instance = null;
	}

	public void ResetStats()
	{
		_islandStartTime = Time.time;
		_lastUpgradePurchaseTime = -1f;
		_fishCaught = 0;
		_coinsPickedUp = 0;
		_moneyEarned = 0;
		_rodCasts = 0;
		_turtlePets = 0;
		_penguinsKicked = 0;
	}

	public Snapshot GetSnapshot()
	{
		var availableUnpurchasedUpgradeCount = GetAvailableUnpurchasedUpgradeCount();
		return new Snapshot
		{
			FishCaught = _fishCaught,
			CoinsPickedUp = _coinsPickedUp,
			MoneyEarned = _moneyEarned,
			RodCasts = _rodCasts,
			TurtlePets = _turtlePets,
			PenguinsKicked = _penguinsKicked,
			TotalIslandTimeSeconds = Mathf.Max(0f, Time.time - _islandStartTime),
			TotalSessionTimeSeconds = Mathf.Max(0f, Time.time - _sessionStartTime),
			TimeToLastUpgradePurchaseSeconds = _lastUpgradePurchaseTime >= 0f ? Mathf.Max(0f, _lastUpgradePurchaseTime - _islandStartTime) : -1f,
			PurchasedUpgradeThisIsland = _lastUpgradePurchaseTime >= 0f,
			AllAvailableUpgradesPurchased = availableUnpurchasedUpgradeCount == 0,
			AvailableUnpurchasedUpgradeCount = availableUnpurchasedUpgradeCount
		};
	}

	public static void RecordFishCaught(int amount)
	{
		if (amount <= 0)
			return;

		Instance._fishCaught += amount;
	}

	public static void RecordCoinsPickedUp(long amount)
	{
		if (amount <= 0)
			return;

		Instance._coinsPickedUp = AddSaturating(Instance._coinsPickedUp, amount);
	}

	public static void RecordMoneyEarned(long amount)
	{
		if (amount <= 0)
			return;

		Instance._moneyEarned = AddSaturating(Instance._moneyEarned, amount);
	}

	private static long AddSaturating(long current, long amount)
	{
		current = System.Math.Max(0L, current);
		amount = System.Math.Max(0L, amount);
		return amount > long.MaxValue - current ? long.MaxValue : current + amount;
	}

	public static void RecordRodCast(int amount = 1)
	{
		if (amount <= 0)
			return;

		Instance._rodCasts += amount;
	}

	public static void RecordTurtlePet(int amount = 1)
	{
		if (amount <= 0)
			return;

		Instance._turtlePets += amount;
	}

	public static void RecordPenguinKick(int amount = 1)
	{
		if (amount <= 0)
			return;

		Instance._penguinsKicked += amount;
	}

	public static void RecordUpgradePurchased()
	{
		Instance._lastUpgradePurchaseTime = Time.time;
	}

	private static int GetAvailableUnpurchasedUpgradeCount()
	{
		if (!SaveUtil.IsSaveDataReady || GameDataManager.Instance == null || GameDataManager.Instance.SkillTreeData.Upgrades == null)
			return 0;

		var count = 0;
		var upgrades = GameDataManager.Instance.SkillTreeData.Upgrades;
		for (var i = 0; i < upgrades.Length; i++)
		{
			var node = upgrades[i];
			if (SaveUtil.IsUpgradeUnlocked(node.gridPos))
				continue;
			if (!SkillTreeNodeRules.IsUnlockable(node))
				continue;

			count++;
		}

		return count;
	}
}
