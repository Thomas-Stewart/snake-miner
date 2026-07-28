using System.Collections;
using System.Collections.Generic;
using System;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.Input;
using SSG_Core.Scripts.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SkillTreePopupController : MonoBehaviour
{
	private static SkillTreePopupController _instance;
	private const float PopupCameraBaseDepth = 100f;
	private const int PopupCanvasSortingOrder = 10000;
	private const int SkillTreeLayer = 5; // UI layer
	private const float ReopenCooldownSeconds = 0.2f;

	private bool _isOpen;
	private bool _isTransitioning;
	private readonly List<Camera> _disabledGameplayCameras = new List<Camera>();
	private readonly List<Canvas> _disabledGameplayCanvases = new List<Canvas>();
	private Vector3? _playerPositionBeforeOpen;
	private Quaternion? _playerRotationBeforeOpen;
	private float _nextAllowedCloseTime;
	private float _nextAllowedOpenTime;
	public static event Action PopupClosed;

	public static bool IsPopupOpen => _instance != null && _instance._isOpen;
	public static bool IsPopupOpenOrTransitioning => _instance != null && (_instance._isOpen || _instance._isTransitioning);

	public static void OpenSkillTreePopup()
	{
		EnsureInstance();
		_instance.TryOpen();
	}

	public static void CloseSkillTreePopup()
	{
		if (_instance == null)
			return;

		_instance.TryClose();
	}

	private static void EnsureInstance()
	{
		if (_instance != null)
			return;

		var go = new GameObject(nameof(SkillTreePopupController));
		_instance = go.AddComponent<SkillTreePopupController>();
		DontDestroyOnLoad(go);
	}

	private void TryOpen()
	{
		if (_isOpen || _isTransitioning)
			return;
		if (Time.unscaledTime < _nextAllowedOpenTime)
			return;

		StartCoroutine(OpenRoutine());
	}

	private void TryClose()
	{
		if (!_isOpen || _isTransitioning)
			return;
		if (Time.unscaledTime < _nextAllowedCloseTime)
			return;

		StartCoroutine(CloseRoutine());
	}

	private IEnumerator OpenRoutine()
	{
		_isTransitioning = true;
		InputActionMapHelper.ChangeAllInputActionMap(InputActionMapHelper.SkillTree);
		if (CoreGameManager.Instance != null)
			CoreGameManager.Instance.SetGamePhase(GamePhase.SkillTree);

		var skillTreeScene = SceneManager.GetSceneByName(SceneNames.SkillTree);
		if (!skillTreeScene.isLoaded)
			yield return SceneManager.LoadSceneAsync(SceneNames.SkillTree, LoadSceneMode.Additive);

		skillTreeScene = SceneManager.GetSceneByName(SceneNames.SkillTree);
		SetSceneLayer(skillTreeScene, SkillTreeLayer);
		ConfigureSkillTreeCameras(skillTreeScene);
		ConfigureSkillTreeCanvases(skillTreeScene);
		DisableGameplayCameras(skillTreeScene);
		DisableGameplayCanvases(skillTreeScene);

		_nextAllowedCloseTime = Time.unscaledTime + ReopenCooldownSeconds;
		_isOpen = true;
		_isTransitioning = false;
	}

	private IEnumerator CloseRoutine()
	{
		_isTransitioning = true;

		var skillTreeScene = SceneManager.GetSceneByName(SceneNames.SkillTree);
		if (skillTreeScene.isLoaded)
			yield return SceneManager.UnloadSceneAsync(skillTreeScene);

		RestoreGameplayCameras();
		RestoreGameplayCanvases();
		_playerPositionBeforeOpen = null;
		_playerRotationBeforeOpen = null;

		InputActionMapHelper.ChangeAllInputActionMap(InputActionMapHelper.Player);
		if (CoreGameManager.Instance != null)
			CoreGameManager.Instance.SetGamePhase(GamePhase.Gameplay);

		_nextAllowedOpenTime = Time.unscaledTime + ReopenCooldownSeconds;
		_isOpen = false;
		_isTransitioning = false;
		PopupClosed?.Invoke();
	}

	private void OnDestroy()
	{
		RestoreGameplayCameras();
		RestoreGameplayCanvases();

		if (_instance == this)
			_instance = null;
	}

	private static void ConfigureSkillTreeCameras(UnityEngine.SceneManagement.Scene skillTreeScene)
	{
		if (!skillTreeScene.IsValid() || !skillTreeScene.isLoaded)
			return;

		var roots = skillTreeScene.GetRootGameObjects();
		var depth = PopupCameraBaseDepth;
		for (var i = 0; i < roots.Length; i++)
		{
			var cameras = roots[i].GetComponentsInChildren<Camera>(true);
			for (var c = 0; c < cameras.Length; c++)
			{
				var camera = cameras[c];
				camera.depth = depth;
				camera.cullingMask = 1 << SkillTreeLayer;
				camera.enabled = true;
				depth += 1f;
			}
		}
	}

	private static void SetSceneLayer(UnityEngine.SceneManagement.Scene scene, int layer)
	{
		if (!scene.IsValid() || !scene.isLoaded)
			return;

		var roots = scene.GetRootGameObjects();
		for (var i = 0; i < roots.Length; i++)
			SetLayerRecursively(roots[i], layer);
	}

	private static void SetLayerRecursively(GameObject gameObject, int layer)
	{
		gameObject.layer = layer;
		var transform = gameObject.transform;
		for (var i = 0; i < transform.childCount; i++)
			SetLayerRecursively(transform.GetChild(i).gameObject, layer);
	}

	private static void ConfigureSkillTreeCanvases(UnityEngine.SceneManagement.Scene skillTreeScene)
	{
		if (!skillTreeScene.IsValid() || !skillTreeScene.isLoaded)
			return;

		var roots = skillTreeScene.GetRootGameObjects();
		for (var i = 0; i < roots.Length; i++)
		{
			var canvases = roots[i].GetComponentsInChildren<Canvas>(true);
			for (var c = 0; c < canvases.Length; c++)
			{
				var canvas = canvases[c];
				canvas.overrideSorting = true;
				canvas.sortingOrder = PopupCanvasSortingOrder + c;
				canvas.enabled = true;
			}
		}
	}

	private void DisableGameplayCameras(UnityEngine.SceneManagement.Scene skillTreeScene)
	{
		_disabledGameplayCameras.Clear();
		var cameras = Camera.allCameras;
		for (var i = 0; i < cameras.Length; i++)
		{
			var camera = cameras[i];
			if (camera == null || !camera.enabled)
				continue;

			if (camera.gameObject.scene == skillTreeScene)
				continue;

			_disabledGameplayCameras.Add(camera);
			camera.enabled = false;
		}
	}

	private void DisableGameplayCanvases(UnityEngine.SceneManagement.Scene skillTreeScene)
	{
		_disabledGameplayCanvases.Clear();
		var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		for (var i = 0; i < canvases.Length; i++)
		{
			var canvas = canvases[i];
			if (canvas == null || !canvas.enabled)
				continue;

			if (canvas.gameObject.scene == skillTreeScene)
				continue;

			_disabledGameplayCanvases.Add(canvas);
			canvas.enabled = false;
		}
	}

	private void RestoreGameplayCameras()
	{
		for (var i = 0; i < _disabledGameplayCameras.Count; i++)
		{
			var camera = _disabledGameplayCameras[i];
			if (camera != null)
				camera.enabled = true;
		}

		_disabledGameplayCameras.Clear();
	}

	private void RestoreGameplayCanvases()
	{
		for (var i = 0; i < _disabledGameplayCanvases.Count; i++)
		{
			var canvas = _disabledGameplayCanvases[i];
			if (canvas != null)
				canvas.enabled = true;
		}

		_disabledGameplayCanvases.Clear();
	}
}
