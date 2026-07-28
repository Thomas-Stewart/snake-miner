using System;
using System.Collections;
using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace SSG_Core.Scripts.Scene
{
	public class SceneLoader : MonoBehaviour
	{
		[SerializeField] private LoadingScreen _loadingScreen;

		private UnityEngine.SceneManagement.Scene? _currentlyLoadedScene;

		public bool IsLoading { get; private set; }

		public LoadingScreen LoadingScreen => _loadingScreen;

		public event Action OnSceneLoaded;

		private Coroutine _goToSceneCoroutine;
		public void GoToScene(string sceneName)
		{
			// if (sceneName == SceneManager.GetActiveScene().name) return;
			if (_goToSceneCoroutine != null) return;

			Debug.Log("loading scene " + sceneName);
			_goToSceneCoroutine = StartCoroutine(GoToSceneRoutine(sceneName));
		}

		private IEnumerator GoToSceneRoutine(string sceneName)
		{
			IsLoading = true;
			
			if (EventSystem.current)
				EventSystem.current.SetSelectedGameObject(null);

			MusicManager.Instance.PauseMusic();
			yield return new WaitWhile(() => PopupManager.Instance != null && PopupManager.Instance.AreAnyPopupsShowing);

			if (!_loadingScreen.IsShowing)
				_loadingScreen.Show();

			yield return new WaitWhile(() => _loadingScreen.IsInTransition);

			var unloadActions = FindObjectsByType<SceneLoadAction>(FindObjectsInactive.Include, FindObjectsSortMode.None);

			foreach (var unloadAction in unloadActions)
			{
				if (unloadAction.ShouldRunDuringUnload)
				{
					unloadAction.DoAction();
					yield return new WaitUntil(() => unloadAction.IsActionComplete());
				}
			}

			if (_currentlyLoadedScene != null)
				yield return SceneManager.UnloadSceneAsync(_currentlyLoadedScene.Value);

			yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
			_currentlyLoadedScene = SceneManager.GetSceneByName(sceneName);
			SceneManager.SetActiveScene(_currentlyLoadedScene.Value);

			var loadActions = FindObjectsByType<SceneLoadAction>(FindObjectsInactive.Include, FindObjectsSortMode.None);

			foreach (var loadAction in loadActions)
			{
				if (loadAction.ShouldRunDuringLoad)
				{
					loadAction.DoAction();
					yield return new WaitUntil(() => loadAction.IsActionComplete());
				}
			}

			_loadingScreen.Hide();
			yield return new WaitWhile(() => _loadingScreen.IsInTransition);

			IsLoading = false;
			OnSceneLoaded?.Invoke();
			_goToSceneCoroutine = null;
		}
	}
}
