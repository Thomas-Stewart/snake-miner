using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.Scene;
using SSG_Core.Scripts.UI;
using SSG.Util;
using UnityEngine;

namespace SSG_Core.Scripts.Menu
{
	public class WishlistPopupMenuHandler : MenuOptionHandler
	{
		private const string _steamPageUrl = "https://store.steampowered.com/";

		public void OpenSteamPage()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);

			if (string.IsNullOrWhiteSpace(_steamPageUrl))
			{
				Debug.LogWarning("WishlistPopupMenuHandler has no Steam page URL assigned.");
				return;
			}

			SteamStoreUrl.Open(_steamPageUrl);
		}

		public void Continue()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			var currentLevelIndex = SaveUtil.SaveData.CurrentLevelIndex;
			var previousLevelIndex = Mathf.Max(0, currentLevelIndex - 1);
			var saveData = SaveUtil.SaveData;
			saveData.CurrentLevelIndex = previousLevelIndex;
			SaveUtil.SetSaveDataVariable(saveData, true);
			CoreGameManager.Instance.GoToScene(SceneNames.Game);
		}

		public void KeepFishing()
		{
			Continue();
		}

		public void QuitToTitle()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			CoreGameManager.Instance.GoToScene(SceneNames.Title);
		}
	}
}
