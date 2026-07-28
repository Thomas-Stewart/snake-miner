using System;
using SSG_Core.Scripts.Input;
using SSG_Core.Scripts.Scene;
using SSG_Core.Scripts.UI;
using UnityEngine;
using Random = System.Random;

namespace SSG_Core.Scripts.Core
{
	/// <summary>
	/// Does something important probably
	/// </summary>
	public class CoreGameManager : MonoBehaviour
	{
		[Header("Managers")]
		[SerializeField] private SceneLoader _sceneLoader;

		public SceneLoader SceneLoader => _sceneLoader;

		public Camera MainCamera { get; private set; }

		private Random _rand;

		public static CoreGameManager Instance { get; private set; }
		public GamePhase CurrentGamePhase { get; private set; }
		public bool IsLoadingScreenShowing => _sceneLoader.LoadingScreen.IsShowing || _sceneLoader.LoadingScreen.IsInTransition;
		public bool IsDebugLevel { get; private set; }

		private void Initialize()
		{
			if (Instance == null)
			{
				Instance = this;
				_sceneLoader.OnSceneLoaded += HandleSceneLoaded;
			}
		}

		private void Awake()
		{
			Initialize();
			CurrentGamePhase = GamePhase.None;
			_rand = new Random(DateTime.Now.Ticks.GetHashCode());
		}

		private void Update()
		{
			if ((InputManager.InputActions.Player.Pause.WasPressedThisFrame() || InputManager.InputActions.UI.Pause.WasPressedThisFrame())
			    && !PopupManager.Instance.AreAnyPopupsShowing)
			{
				PopupManager.Instance.OpenPopup(PopupType.PAUSE);
			}
			else if (InputManager.InputActions.UI.UnPause.WasPressedThisFrame())
			{
				var openPopupType = PopupManager.Instance.GetOpenPopupType();
				if (openPopupType != PopupType.END_ISLAND_STATS && openPopupType != PopupType.END_ISLAND_MAP && openPopupType != PopupType.WISHLIST)
					PopupManager.Instance.ClosePopup();
			}
		}

		public void GoToScene(string sceneName)
		{
			_sceneLoader.LoadingScreen.Show();

			PopupManager.Instance.CloseAllPopups();
			
			switch (sceneName)
			{
				case SceneNames.Title:
					InputActionMapHelper.ChangeAllInputActionMap(InputActionMapHelper.UI);
					SetGamePhase(GamePhase.Title);
					break;
				case SceneNames.SkillTree:
					InputActionMapHelper.ChangeAllInputActionMap(InputActionMapHelper.SkillTree);
					SetGamePhase(GamePhase.SkillTree);
					break;
				default:
					InputActionMapHelper.ChangeAllInputActionMap(InputActionMapHelper.Player);
					SetGamePhase(GamePhase.Gameplay);
					break;
			}
			
			_sceneLoader.GoToScene(sceneName);
		}

		/// <summary>
		/// Don't use this unless it's for debug
		/// </summary>
		/// <param name="sceneName"></param>
		/// <param name="currentGamePhase"></param>
		public void GoToSceneDebugStartScene(string sceneName, GamePhase currentGamePhase)
		{
			CurrentGamePhase = currentGamePhase;
			IsDebugLevel = currentGamePhase == GamePhase.Gameplay;
			_sceneLoader.GoToScene(sceneName);
		}

		private void HandleSceneLoaded()
		{
			MainCamera = Camera.main;
		}

		public void SetGamePhase(GamePhase gamePhase)
		{
			CurrentGamePhase = gamePhase;
		}

		private void OnDisable()
		{
			InputManager.InputActions.UI.Disable();
			InputManager.InputActions.Player.Disable();
			InputManager.InputActions.SkillTree.Disable();
		}

		private void OnDestroy()
		{
			if (_sceneLoader != null)
				_sceneLoader.OnSceneLoaded -= HandleSceneLoaded;

			if (Instance == this)
				Instance = null;

			InputManager.InputActions.Dispose();
		}
	}
}
