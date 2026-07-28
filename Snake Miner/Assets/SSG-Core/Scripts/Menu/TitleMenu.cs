using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.Localization;
using SSG_Core.Scripts.Scene;
using SSG_Core.Scripts.UI;
using SSG_Core.Scripts.Util;
using SSG.Util;
using UnityEditor;
using UnityEngine;

namespace SSG_Core.Scripts.Menu
{
	public class TitleMenuHandler : MenuOptionHandler
	{
		[SerializeField] private GameObject _camera;
		[SerializeField] private Transform _cameraTarget;
		
		public void ContinueGameOnClick()
		{
			MusicManager.Instance?.PlayStinger(StingerEvent.TitleGameStart);
			foreach (var optionEventPair in _optionEventPairs)
			{
				optionEventPair.MenuOption.gameObject.SetActive(false);
			}

			CoreGameManager.Instance.GoToScene(SceneNames.Game);

			// CoroutineHelper.Engage(CoroutineHelper.MoveToTarget(_camera.transform, _cameraTarget.position, 3f));
			// CoroutineHelper.Engage(CoroutineHelper.MoveToTarget(_playerBoat.transform, _playerBoatTarget.position, 2f));
			// CoroutineHelper.Engage(CoroutineHelper.Wait(1.5f),
			// 	() =>
			// 	{
			// 		CoreGameManager.Instance.GoToScene(SceneNames.Game);
			// 	});		
		}
		
		public void NewGameOnClick()
		{
			MusicManager.Instance?.PlayStinger(StingerEvent.TitleGameStart);
			foreach (var optionEventPair in _optionEventPairs)
			{
				optionEventPair.MenuOption.gameObject.SetActive(false);
			}
			
			SaveUtil.ResetSave();
			CoreGameManager.Instance.GoToScene(SceneNames.Game);

			// CoroutineHelper.Engage(CoroutineHelper.MoveToTarget(_camera.transform, _cameraTarget.position, 3f));
			// CoroutineHelper.Engage(CoroutineHelper.MoveToTarget(_playerBoat.transform, _playerBoatTarget.position, 2f));
			// CoroutineHelper.Engage(CoroutineHelper.Wait(1.5f),
			// 	() =>
			// 	{
			// 		SaveUtil.ResetSave();
			// 		SaveUtil.ResetSave();
			// 		CoreGameManager.Instance.GoToScene(SceneNames.Game);
			// 	});
		}

		public static void WishlistClicked()
		{
			SteamStoreUrl.Open("https://store.steampowered.com/");
		}
		
		public static void PressKitClicked()
		{
			Application.OpenURL("https://drive.google.com/drive/folders/1Xe5Y0bYU2-Zw0jhYblkQz_WZiwhWZyHb?usp=drive_link");
		}

		public void OptionsOnClick()
		{
			PopupManager.Instance.OpenPopup(PopupType.OPTIONS);
		}

		public static void BugFormClicked()
		{
			Application.OpenURL(Localizer.GetText("BugFormURL"));
		}

		public static void DiscordClicked()
		{
			Application.OpenURL(Localizer.GetText("DiscordURL"));
		}

		public static void QuitOnClick()
		{
#if UNITY_EDITOR
			EditorApplication.ExitPlaymode();
#else
			Application.Quit();
#endif

		}
	}
}
