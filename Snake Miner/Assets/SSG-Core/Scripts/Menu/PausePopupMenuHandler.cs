using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.Scene;
using SSG_Core.Scripts.UI;
using SSG.Util;
using UnityEngine;

namespace SSG_Core.Scripts.Menu
{
	/// <summary>
	/// UI element that appears when the game is paused
	/// </summary>
	public class PausePopupMenuHandler : MenuOptionHandler
	{
		[SerializeField] private float _musicOnVolume = 0.5f;

		public void SelectUnpause()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			PopupManager.Instance.CloseAllPopups();
		}

		public void SelectToggleMusic()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			var isMusicOn = SaveUtil.GetBgmVolume() > 0.001f;
			var nextVolume = isMusicOn ? 0f : Mathf.Clamp01(_musicOnVolume);
			MusicManager.Instance.SetBgmVolume(nextVolume);
		}

		public void SelectOptions()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			PopupManager.Instance.OpenPopup(PopupType.OPTIONS);
		}
		
		public static void WishlistClicked()
		{
			SteamStoreUrl.Open("https://store.steampowered.com/");
		}

		public void SelectExit()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			PopupManager.Instance.CloseAllPopups();
			CoreGameManager.Instance.GoToScene(SceneNames.Title);
		}

		public void SelectResetSave()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			PopupManager.Instance.CloseAllPopups();
			SaveUtil.ResetSave();
			CoreGameManager.Instance.GoToScene(SceneNames.Game);
		}
	}
}
