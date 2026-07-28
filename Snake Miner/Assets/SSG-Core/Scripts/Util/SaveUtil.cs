using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_GAMECORE
using Microsoft.Xbox;
using Unity.GameCore;
#endif
using UnityEngine;
#if UNITY_PS5
using UnityEngine.PS5;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace SSG.Util
{
	/// <summary>
	/// Singular location for routing all save/load calls for persistent data
	/// Right now we're using PlayerPrefs, but we can swap that out for any implementation here easily.
	/// </summary>
	public static class SaveUtil
	{
		public static SaveData SaveData;

		public static bool IsSaveDataReady => SaveData.SavedUpgrades != null;
		public static int SaveDataVersion { get; private set; }

		public const string SAVE_KEY = "Save_DATA";

		public static bool HasMeaningfulSaveData()
		{
			if (!PlayerPrefs.HasKey(SAVE_KEY))
				return false;

			var saveFileString = PlayerPrefs.GetString(SAVE_KEY, string.Empty);
			if (string.IsNullOrWhiteSpace(saveFileString))
				return false;

			var saveData = JsonUtility.FromJson<SaveData>(saveFileString);
			return saveData.CashMoney > 0
			       || saveData.TotalCoinsCollected > 0
			       || saveData.WoodPlanks > 0
			       || saveData.TotalWoodPlanksCollected > 0
			       || saveData.CurrentLevelIndex > 0
			       || !string.IsNullOrWhiteSpace(saveData.FtueStepGuid)
			       || saveData.HasCaughtTurtlePet
			       || (saveData.SavedUpgrades != null && saveData.SavedUpgrades.Any(x => x.Exists))
			       || (saveData.SavedCoinBanks != null && saveData.SavedCoinBanks.Any(x => x.Coins > 0));
		}
		
		static SaveUtil()
		{
			var shouldAutoSave = true;
#if UNITY_PS5 || UNITY_PS4 || UNITY_GAMECORE
			shouldAutoSave = true;
#endif
			if (shouldAutoSave)
			{
				SceneManager.activeSceneChanged += (arg0, scene) =>
				{
					// HACK: to flush saves
					PlayerPrefs.Save();
					SetSaveDataVariable(SaveData, true);
#if UNITY_GAMECORE
					SDK.XGameSaveSubmitUpdate(null);

					var dataBytes = SerializeToByteArray(SaveData);
					if (dataBytes != null && dataBytes.Length > 0)
						Gdk.Helpers.Save(SerializeToByteArray(SaveData));
#endif
				};
			}
		}

		public static void SetSaveDataVariable(SaveData saveData, bool shouldWriteToDisk)
		{
			SaveData = saveData;
			if (SaveData.SavedUpgrades == null)
				SaveData.SavedUpgrades = new List<SavedUpgrades>();
			if (SaveData.SavedCoinBanks == null)
				SaveData.SavedCoinBanks = new List<SavedCoinBankProgress>();
			if (SaveData.CompletedGameplayReminderIds == null)
				SaveData.CompletedGameplayReminderIds = new List<string>();

			SaveDataVersion++;

			// var objectiveManager = GameObject.FindAnyObjectByType<ObjectiveManager>();
			// if (objectiveManager && objectiveManager.CurrentObjective == null)
			// 	objectiveManager.FindNextObjective();

			if (shouldWriteToDisk)
			{
#if UNITY_GAMECORE
				Debug.Log("saving data - here are objectives" );
				foreach (var completedObjective in SaveData.CompletedObjectives)
				{
					Debug.Log(completedObjective);
				}
				var dataBytes = SerializeToByteArray(SaveData);
				if (dataBytes != null && dataBytes.Length > 0)
					Gdk.Helpers.Save(dataBytes);
#else
				// Debug.LogError("saving levels - count: " + SaveData.LevelSaveDatas.Count);	
				// Debug.LogError("saving levels 2 - count: " + saveData.LevelSaveDatas.Count);	
				
				PlayerPrefs.SetString(SAVE_KEY, JsonUtility.ToJson(SaveData));

				// var saveStartTime = Time.time;
				// Task.Run(async () =>
				// {
				// 	while (Time.time < saveStartTime + 2f)
				// 		await Task.Delay(1000);
					// PlayerPrefs.Save();
				// });
#endif
			}
		}
		
#if UNITY_EDITOR
		[MenuItem("Tools/Reset Save", false, 0)]
#endif
		public static void ResetSave()
		{
			SetSaveDataVariable(new SaveData(), true);
			SkillTreeCamera.ResetCachedState();
		}
		
#if UNITY_EDITOR
		[MenuItem("Tools/Unlock All Upgrades", false, 0)]
#endif
		public static void UnlockAllUpgrades()
		{
			if (GameDataManager.Instance == null || GameDataManager.Instance.SkillTreeData.Upgrades == null)
				return;

			foreach (var upgradeNode in GameDataManager.Instance.SkillTreeData.Upgrades)
			{
				SaveUpgrade(upgradeNode.gridPos);
			}
		}

		public static string GetStringValue(string key, string defaultValue)
		{
			return PlayerPrefs.GetString(key, defaultValue);
		}

		public static int GetIntValue(string key, int defaultValue)
		{
			return PlayerPrefs.GetInt(key, defaultValue);
		}

		public static float GetFloatValue(string key, float defaultValue)
		{
			return PlayerPrefs.GetFloat(key, defaultValue);
		}

		public static bool GetBoolValue(string key)
		{
			var intVal = GetIntValue(key, 0);
			var boolVal = intVal >= 1;
			return boolVal;
		}

		public static void SetValue(string key, string value)
		{
			// PlayerPrefs.SetString(key, value);
			//playerprefs.save();
			
#if UNITY_GAMECORE
			var dataBytes = SerializeToByteArray(SaveData);
			if (dataBytes != null && dataBytes.Length > 0)
				Gdk.Helpers.Save(dataBytes);
#endif
			
			SetSaveDataVariable(SaveData, true);
		}

		public static void SetValue(string key, int value)
		{
			PlayerPrefs.SetInt(key, value);
			//playerprefs.save();
		}

		public static void SetValue(string key, bool value)
		{
			var intVal = value ? 1 : 0;
			PlayerPrefs.SetInt(key, intVal);
			//playerprefs.save();
		}

		public static void SetValue(string key, float value)
		{
			PlayerPrefs.SetFloat(key, value);
			//playerprefs.save();
		}

		public static List<string> GetListFromPrefs(string key)
		{
			var delineatedList = PlayerPrefs.GetString(key);
			if (string.IsNullOrEmpty(delineatedList)) return new List<string>();
		
			var list = delineatedList.Split(',').ToList();
			return list;
		}
		
		public static void SaveListToPrefs(string key, List<string> list)
		{
			list = list.Distinct().ToList();
			var listString = string.Join(",", list.ToArray());
			Debug.Log("saving list: = " + listString);
			PlayerPrefs.SetString(key, listString);
			//playerprefs.save();
		}

		private static List<T> GetEnumListFromStringList<T>(List<String> list)
		{
			return (from e in list select (T)Enum.Parse(typeof(T), e, true)).ToList();
		}

		// ---------------------------------------------------------------------- //
		// ----------------------- Legacy Music Code ------------------------------ //
		// ---------------------------------------------------------------------- //

		private const string BGM_VOLUME_KEY = "bgm_volume";
		private const string SFX_VOLUME_KEY = "sfx_volume";
		private const string CLICK_TO_MOVE_ENABLED_KEY = "click_to_move_enabled";
		private const string SCREEN_SHAKE_ENABLED_KEY = "screen_shake_enabled";
		private const string RESOLUTION_WIDTH_KEY = "resolution_width";
		private const string LANGUAGE_CODE_KEY = "language_code";
		public const float RESOLUTION_RATIO_16_9 = 0.5625F;

		public static void SetBgmVolume(float volume)
		{
			PlayerPrefs.SetFloat(BGM_VOLUME_KEY, volume);
			//playerprefs.save();
		}

		public static float GetBgmVolume()
		{
			return PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 0.5f);
		}

		public static void SetSfxVolume(float volume)
		{
			PlayerPrefs.SetFloat(SFX_VOLUME_KEY, volume);
			//playerprefs.save();
		}

		public static float GetSfxVolume()
		{
			return PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 0.5f);
		}

		public static void SetClickToMoveEnabled(bool isEnabled)
		{
			PlayerPrefs.SetInt(CLICK_TO_MOVE_ENABLED_KEY, isEnabled ? 1 : 0);
		}

		public static bool GetClickToMoveEnabled()
		{
			return PlayerPrefs.GetInt(CLICK_TO_MOVE_ENABLED_KEY, 0) >= 1;
		}

		public static void SetScreenShakeEnabled(bool isEnabled)
		{
			PlayerPrefs.SetInt(SCREEN_SHAKE_ENABLED_KEY, isEnabled ? 1 : 0);
		}

		public static bool GetScreenShakeEnabled()
		{
			return PlayerPrefs.GetInt(SCREEN_SHAKE_ENABLED_KEY, 1) >= 1;
		}

		public static void SetResolution(int width)
		{
			return;
			var height = (int)(width * RESOLUTION_RATIO_16_9);
			Screen.SetResolution(width, height, Screen.fullScreen);
			PlayerPrefs.SetInt(RESOLUTION_WIDTH_KEY, width);
			//playerprefs.save();
		}

		public static int GetResolutionWidth()
		{
			return PlayerPrefs.GetInt(RESOLUTION_WIDTH_KEY, Screen.currentResolution.width);
		}

		public static void SetLanguageCode(string languageCode)
		{
			PlayerPrefs.SetString(LANGUAGE_CODE_KEY, languageCode);
		}

		public static string GetLanguageCode()
		{
			return PlayerPrefs.GetString(LANGUAGE_CODE_KEY, "en");
		}
		
		
		public static byte[] SerializeToByteArray<T>(T obj)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));

			using (MemoryStream ms = new MemoryStream())
			{
				BinaryFormatter formatter = new BinaryFormatter();
				formatter.Serialize(ms, obj);
				return ms.ToArray();
			}
		}

		public static T DeserializeFromByteArray<T>(byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			try
			{
				using (var ms = new MemoryStream(data))
				{
					var formatter = new BinaryFormatter();
					return (T)formatter.Deserialize(ms);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				return default;
			}
		}

		public static void SaveUpgrade(GridPos coords)
		{
			SaveData.SavedUpgrades.Add(new SavedUpgrades
			{
				Exists = true,
				Coords = coords
			});
			
			SetSaveDataVariable(SaveData, true);
		}
		
		public static bool IsUpgradeUnlocked(GridPos coords)
		{
			var savedUpgrades = SaveData.SavedUpgrades;
			for (var i = 0; i < savedUpgrades.Count; i++)
			{
				var savedUpgrade = savedUpgrades[i];
				if (savedUpgrade.Coords.Equals(coords))
					return savedUpgrade.Exists;
			}

			return false;
		}

		public static bool TryGetCoinBankProgress(string bankId, out int coins)
		{
			coins = 0;
			if (string.IsNullOrWhiteSpace(bankId))
				return false;
			if (SaveData.SavedCoinBanks == null)
				return false;

			for (var i = 0; i < SaveData.SavedCoinBanks.Count; i++)
			{
				var entry = SaveData.SavedCoinBanks[i];
				if (entry.BankId != bankId)
					continue;

				coins = Mathf.Max(0, entry.Coins);
				return true;
			}

			return false;
		}

		public static void SaveCoinBankProgress(string bankId, int coins)
		{
			if (string.IsNullOrWhiteSpace(bankId))
				return;

			if (SaveData.SavedCoinBanks == null)
				SaveData.SavedCoinBanks = new List<SavedCoinBankProgress>();

			var clampedCoins = Mathf.Max(0, coins);
			var index = SaveData.SavedCoinBanks.FindIndex(x => x.BankId == bankId);
			if (index >= 0)
			{
				var existing = SaveData.SavedCoinBanks[index];
				existing.Coins = clampedCoins;
				SaveData.SavedCoinBanks[index] = existing;
			}
			else
			{
				SaveData.SavedCoinBanks.Add(new SavedCoinBankProgress
				{
					BankId = bankId,
					Coins = clampedCoins
				});
			}

			SetSaveDataVariable(SaveData, true);
		}

		public static bool HasCompletedGameplayReminder(string reminderId)
		{
			if (string.IsNullOrWhiteSpace(reminderId) || SaveData.CompletedGameplayReminderIds == null)
				return false;

			return SaveData.CompletedGameplayReminderIds.Contains(reminderId);
		}

		public static void MarkGameplayReminderCompleted(string reminderId)
		{
			if (string.IsNullOrWhiteSpace(reminderId))
				return;

			if (SaveData.CompletedGameplayReminderIds == null)
				SaveData.CompletedGameplayReminderIds = new List<string>();

			if (SaveData.CompletedGameplayReminderIds.Contains(reminderId))
				return;

			SaveData.CompletedGameplayReminderIds.Add(reminderId);
			SetSaveDataVariable(SaveData, true);
		}
	}
}

[Serializable]
public struct SaveData
{
	public List<SavedUpgrades> SavedUpgrades;
	public List<SavedCoinBankProgress> SavedCoinBanks;
	public long CashMoney;
	public long TotalCoinsCollected;
	public int WoodPlanks;
	public int TotalWoodPlanksCollected;
	public int CurrentLevelIndex;
	public string FtueStepGuid;
	public List<string> CompletedGameplayReminderIds;
	public bool HasCaughtTurtlePet;
}

[Serializable]
public struct SavedUpgrades
{
	public bool Exists;
	public GridPos Coords;
}

[Serializable]
public struct SavedCoinBankProgress
{
	public string BankId;
	public int Coins;
}
