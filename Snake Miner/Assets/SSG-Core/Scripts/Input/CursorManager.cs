using System.Collections;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SSG_Core.Scripts.Input
{
	public class CursorManager : MonoBehaviour
	{
		private IEnumerator Start()
		{
			yield return new WaitUntil(() => CoreGameManager.Instance != null && PopupManager.Instance != null);
			SceneManager.activeSceneChanged += HandleSceneChanged;
			// InputActionMapHelper.OnActionMapChanged += HandleActionMapChanged;
		}

// 		private void HandleActionMapChanged(string newActionMap)
// 		{
// 			var isUsingKeyboard = true;//check if anybody is using a keyboard
// 			var shouldShowCursor = true;
//
// 			if (newActionMap == InputActionMapHelper.UI && isUsingKeyboard)
// 				shouldShowCursor = true;
//
// #if UNITY_PS5
// 			shouldShowCursor = false;
// #endif
//
// 			Cursor.visible = shouldShowCursor;
// 		}

		private void HandleSceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
		{
			if (CoreGameManager.Instance == null) return;

			var shouldShowCursor = false;
			switch (CoreGameManager.Instance.CurrentGamePhase)
			{
				case GamePhase.Title:
					break;
				case GamePhase.Gameplay:
					shouldShowCursor = true;//check if anybody is using a keyboard
					break;
			}

#if UNITY_PS5
			shouldShowCursor = false;
#endif

			Cursor.visible = shouldShowCursor;
		}

		private void OnDestroy()
		{
			SceneManager.activeSceneChanged -= HandleSceneChanged;
		}
	}
}
