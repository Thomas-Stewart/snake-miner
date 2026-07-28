using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.Scene;
using UnityEngine;
using UnityEngine.UI;

namespace SSG_Core.Scripts.Menu
{
	/// <summary>
	/// UI that displays when the player has died 1 too many times.
	/// </summary>
	public class GameOverScreen : MonoBehaviour
	{
		[SerializeField] private Button _newGameButton;
		[SerializeField] private Button _wishlistButton;
		[SerializeField] private Button _optionsButton;
		[SerializeField] private Button _menuButton;

		private void OnEnable()
		{
			if (_newGameButton)
				_newGameButton.onClick.AddListener(NewGameOnClick);
			if (_wishlistButton)
				_wishlistButton.onClick.AddListener(WishlistClicked);
			if (_optionsButton)
				_optionsButton.onClick.AddListener(OptionsOnClick);
			if (_menuButton)
				_menuButton.onClick.AddListener(MenuOnClick);
		}

		private static void NewGameOnClick()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			CoreGameManager.Instance.GoToScene(SceneNames.Title);
		}

		private static void WishlistClicked()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			// TODO: Put this URL into a global variable
			SteamStoreUrl.Open("https://store.steampowered.com/app/4601160/Just_Keep_Fishing/");
		}

		private static void OptionsOnClick()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);

		}

		private static void MenuOnClick()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			CoreGameManager.Instance.GoToScene(SceneNames.Title);
		}
	}
}
