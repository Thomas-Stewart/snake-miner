using System.Collections.Generic;
using System.Linq;
using SSG_Core.Scripts.SteamUtil;
using SSG_Core.Scripts.Util;
using SSG.Util;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif
#if UNITY_PS5
using Unity.PSN.PS5.UDS;
using Unity.PSN.PS5.Aysnc;
using UnityEngine.PS5;
using PSNSample;
using UnityEngine.InputSystem.PS5;
#endif
#if UNITY_EDITOR
#endif

namespace SSG_Core.Scripts.Achievements
{
	public class StatsManager : MonoBehaviour
	{
		public static StatsManager Instance { get; private set; }

		[SerializeField] private bool _debugLogStatAdditions;
		private List<string> _completedMissionKeysToSave = new List<string>();
		private readonly HashSet<string> _pendingSteamMissionKeys = new();

		public const string ANIMAL_TYPES_SPAWNED = "animal_types_spawned";

		private void Initialize()
		{
			if (Instance == null)
			{
				Instance = this;
				// if (Application.isPlaying)
				// 	DontDestroyOnLoad(Instance);
			}
		}

		private void Awake()
		{
			Initialize();

			SceneManager.activeSceneChanged += HandleSceneChanged;
			Application.wantsToQuit += HandleWantsToQuit;
		}

		private void Update()
		{
			ProcessPendingSteamMissionUnlocks();

			for (var i = _completedMissionKeysToSave.Count - 1; i >= 0; i--)
			{
				var key = _completedMissionKeysToSave[i];

				SaveUtil.SetValue(key, true);
				_completedMissionKeysToSave.RemoveAt(i);
			}
		}

		public static int AddToStat(string statKey, int val)
		{
			var existingValue = SaveUtil.GetIntValue(statKey, 0);
			var newValue = (int)System.Math.Clamp((long)existingValue + val, int.MinValue, int.MaxValue);
			SaveUtil.SetValue(statKey, newValue);
			if (Instance != null && Instance._debugLogStatAdditions)
			{
				Debug.Log($"[StatsManager] AddToStat key={statKey} delta={val} oldValue={existingValue} newValue={newValue}");
			}

			var statMissions = StatMissions.MissionDatas.Where(d => d.StatKey == statKey);

			foreach (var statMission in statMissions)
			{
				if (!HasMissionBeenCompleted(statMission.MissionKey))
				{

					// DEBUG
					// data.ProgressionValue = 5;

					if (statMission.StatKey != string.Empty && newValue >= statMission.ProgressionValue)
						CompleteMission(statMission.MissionKey);
				}
			}

			return newValue;
		}

		public static List<string> AddToList(string key, string val)
		{
			var existingList = SaveUtil.GetListFromPrefs(key);
			if (existingList.Contains(val)) return existingList;

			existingList.Add(val);

			SaveUtil.SaveListToPrefs(key, existingList);
			return existingList;
		}

		public static bool HasMissionBeenCompleted(string key)
		{
			var existingValue = SaveUtil.GetBoolValue(key);
			return existingValue;
		}

		public static void CompleteMission(string key)
		{
			Instance.CompleteMissionInternal(key);
		}
		private void CompleteMissionInternal(string key)
		{
			if (HasMissionBeenCompleted(key)) return;

			// Debug.Log("MISSION COMPLETED: " + key);

			var wasUnlockSuccessful = false;


#if !DISABLESTEAMWORKS
			if (SteamManager.Initialized)
			{
				if (!SteamManager.StatsReady)
				{
					_pendingSteamMissionKeys.Add(key);
					return;
				}

				var isRecognizedAchievement = SteamUserStats.GetAchievement(key, out var wasAlreadyUnlocked);
				var didSetAchievement = SteamUserStats.SetAchievement(key);
				var didStoreStats = SteamUserStats.StoreStats();

				if (!isRecognizedAchievement || !didSetAchievement || !didStoreStats)
				{
					Debug.LogError(
						$"Error setting Steam achievement! {key} " +
						$"(recognized={isRecognizedAchievement}, alreadyUnlocked={wasAlreadyUnlocked}, setAchievement={didSetAchievement}, storeStats={didStoreStats})");
				}

				wasUnlockSuccessful = isRecognizedAchievement && didSetAchievement && didStoreStats;
			}
#endif

#if UNITY_PS5
			DoPSNTrophyCall(key);
#endif

			if (wasUnlockSuccessful)
			{
				_pendingSteamMissionKeys.Remove(key);
				SaveUtil.SetValue(key, true);
			}
		}

		private void ProcessPendingSteamMissionUnlocks()
		{
#if !DISABLESTEAMWORKS
			if (!SteamManager.Initialized || !SteamManager.StatsReady || _pendingSteamMissionKeys.Count == 0)
				return;

			var pendingKeys = _pendingSteamMissionKeys.ToArray();
			for (var i = 0; i < pendingKeys.Length; i++)
			{
				var key = pendingKeys[i];
				if (HasMissionBeenCompleted(key))
				{
					_pendingSteamMissionKeys.Remove(key);
					continue;
				}

				CompleteMissionInternal(key);
			}
#endif
		}

		private void DoPSNTrophyCall(string key)
		{
#if UNITY_PS5
			if (!PlayStationTrophyHelper.TryGetTrophyData(key, out var data))
			{
				Debug.LogWarning($"No PS5 trophy mapping found for mission key: {key}");
				return;
			}
			var request = new UniversalDataSystem.UnlockTrophyRequest();
			request.TrophyId = data.TrophyId;
			var userId = PS5Input.GetUsersDetails(0).userId;
			request.UserId = userId;
			if (UniversalDataSystem.IsInitialized)
			{
				var unlockTrophyOp = new AsyncRequest<UniversalDataSystem.UnlockTrophyRequest>(request).ContinueWith((antecedent) =>
				{
					if (SonyNpMain.CheckAysncRequestOK(antecedent))
					{
						if (!_completedMissionKeysToSave.Contains(key))
							_completedMissionKeysToSave.Add(key);
					}
					else
					{
						Debug.LogError("PSN Unlock Trophy Fail");
					}

				});

				UniversalDataSystem.Schedule(unlockTrophyOp);
			}
			else
			{
				Debug.LogError("PSN Unlock Trophy Fail: UniversalDataSystem not initialized");
			}
#endif
		}

		private static bool HandleWantsToQuit()
		{
			SendStatsToCloud();
			return true;
		}
		private static void HandleSceneChanged(UnityEngine.SceneManagement.Scene prevScene, UnityEngine.SceneManagement.Scene newScene)
		{
			SendStatsToCloud();
		}

		private static void SendStatsToCloud()
		{
			// Sending immediately. If this ever changes, send the StoreStats call here
		}

		private void OnDestroy()
		{
			SceneManager.activeSceneChanged -= HandleSceneChanged;
			Application.wantsToQuit -= HandleWantsToQuit;

			if (Instance == this)
				Instance = null;
		}

#if UNITY_EDITOR
		[MenuItem("Tools/Achievements/Reset Steam Achievements (Runtime)")]
#endif
		public static void DebugClear()
		{
			PlayerPrefs.DeleteAll();
			PlayerPrefs.Save();

#if !DISABLESTEAMWORKS
			if (Application.isPlaying && SteamManager.Initialized)
			{
				SteamUserStats.ResetAllStats(true);
			}
#endif
		}

#if UNITY_EDITOR
		[MenuItem("Tools/Achievements/Log Stats")]
#endif
		public static void LogStats()
		{
			foreach (var statKey in AchievementKeys.AllStats)
			{
				Debug.Log(statKey + " : " + SaveUtil.GetIntValue(statKey, 0));
			}
		}
	}
}
