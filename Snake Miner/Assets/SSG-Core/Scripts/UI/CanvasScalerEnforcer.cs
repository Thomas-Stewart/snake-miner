using SSG_Core.Scripts.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SSG_Core.Scripts.UI
{
	public class CanvasScalerEnforcer : SceneLoadAction
	{
		[SerializeField] private Vector2 _referenceResolution = new(1920f, 1080f);
		[SerializeField] private CanvasScaler.ScreenMatchMode _screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		[SerializeField, Range(0f, 1f)] private float _matchWidthOrHeight = 0.5f;

		private bool _hasApplied;

		private void OnEnable()
		{
			SceneManager.sceneLoaded += HandleSceneLoaded;
			Apply();
		}

		private void OnDisable()
		{
			SceneManager.sceneLoaded -= HandleSceneLoaded;
		}

		public override void DoAction()
		{
			_hasApplied = false;
			Apply();
		}

		public override bool IsActionComplete()
		{
			return _hasApplied;
		}

		private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
		{
			Apply();
		}

		private void Apply()
		{
			var scalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			for (var i = 0; i < scalers.Length; i++)
			{
				var scaler = scalers[i];
				scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
				scaler.referenceResolution = _referenceResolution;
				scaler.screenMatchMode = _screenMatchMode;
				scaler.matchWidthOrHeight = _matchWidthOrHeight;
			}

			_hasApplied = true;
		}
	}
}
