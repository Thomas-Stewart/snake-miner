using System.Threading.Tasks;
using SSG_Core.Scripts.Localization;
using SSG_Core.Scripts.Scene;
using SSG_Core.Scripts.Util;
using SSG.Util;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SSG_Core.Scripts.Core
{
	public static class Bootstrapper
	{
		private const int TargetFrameRate = 60;
		private const int MaxShadowmapResolution = 2048;
		private const int MaxShadowCascadeCount = 2;
		private const int MaxAdditionalLightsPerObject = 4;
		private const float MaxShadowDistance = 200f;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static async void Start()
		{
			ConfigureRuntimePerformance();
			SceneManager.sceneLoaded -= HandleSceneLoaded;
			SceneManager.sceneLoaded += HandleSceneLoaded;
			EnsureSingleAudioListener();

			Physics.gravity = new Vector3(0, -30, 0);

			Localizer.LoadFile();
			// SaveUtil.SetResolution(SaveUtil.GetResolutionWidth());
			
			var saveFileString = PlayerPrefs.GetString(SaveUtil.SAVE_KEY, "");
			var saveData = new SaveData();
			if (saveFileString != string.Empty)
			{
				saveData = JsonUtility.FromJson<SaveData>(saveFileString);
			}
			SaveUtil.SetSaveDataVariable(saveData, false);

#if UNITY_PS5
			Cursor.visible = false;
#endif

			var initialScene = SceneManager.GetActiveScene();
			if (initialScene.name != "_Base")
			{
				var currentScene = initialScene.name;
				var canUseDebugStartScene =
					!string.IsNullOrEmpty(currentScene) &&
					!string.IsNullOrEmpty(initialScene.path) &&
					initialScene.path.StartsWith("Assets/", System.StringComparison.Ordinal) &&
					Application.CanStreamedLevelBeLoaded(currentScene);
				var asyncLoad = SceneManager.LoadSceneAsync("_Base");

				while (!asyncLoad.isDone || CoreGameManager.Instance == null)
				{
					await Task.Yield();
				}

				if (canUseDebugStartScene)
				{
					var gamePhase = GetGamePhaseFromSceneName(currentScene);
					CoreGameManager.Instance.GoToSceneDebugStartScene(currentScene, gamePhase);
				}
				else
				{
					CoreGameManager.Instance.GoToScene(SceneNames.Title);
				}
			}
			else
			{
				while (CoreGameManager.Instance == null)
				{
					await Task.Yield();
				}
				CoreGameManager.Instance.GoToScene(SceneNames.Title);
			}
		}

		private static GamePhase GetGamePhaseFromSceneName(string sceneName)
		{
			switch (sceneName)
			{
				case SceneNames.Title:
					return GamePhase.Title;
				case SceneNames.SkillTree:
					return GamePhase.SkillTree;
				default:
					return GamePhase.Gameplay;
			}
		}

		private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
		{
			EnsureSingleAudioListener();
		}

		private static void EnsureSingleAudioListener()
		{
			var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			AudioListener keeper = null;

			for (var i = 0; i < listeners.Length; i++)
			{
				var listener = listeners[i];
				if (listener == null || !listener.enabled)
					continue;

				if (keeper == null || (listener.GetComponent<Camera>() != null && keeper.GetComponent<Camera>() == null))
					keeper = listener;
			}

			if (keeper == null)
				return;

			for (var i = 0; i < listeners.Length; i++)
			{
				var listener = listeners[i];
				if (listener != null && listener != keeper)
					listener.enabled = false;
			}
		}

		private static void ConfigureRuntimePerformance()
		{
			Application.targetFrameRate = TargetFrameRate;
			QualitySettings.vSyncCount = 0;
			QualitySettings.shadowDistance = Mathf.Min(QualitySettings.shadowDistance, MaxShadowDistance);

			var qualityPipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
			ConfigureUniversalRenderPipelineAsset(qualityPipeline);

			var graphicsPipeline = GetDefaultRenderPipelineAsset() as UniversalRenderPipelineAsset;
			if (graphicsPipeline != null && graphicsPipeline != qualityPipeline)
				ConfigureUniversalRenderPipelineAsset(graphicsPipeline);
		}

		private static void ConfigureUniversalRenderPipelineAsset(UniversalRenderPipelineAsset pipelineAsset)
		{
			if (pipelineAsset == null)
				return;

			pipelineAsset.renderScale = 1f;
			pipelineAsset.useSRPBatcher = true;
			pipelineAsset.mainLightShadowmapResolution = Mathf.Min(pipelineAsset.mainLightShadowmapResolution, MaxShadowmapResolution);
			pipelineAsset.additionalLightsShadowmapResolution = Mathf.Min(pipelineAsset.additionalLightsShadowmapResolution, MaxShadowmapResolution);
			pipelineAsset.shadowDistance = Mathf.Min(pipelineAsset.shadowDistance, MaxShadowDistance);
			pipelineAsset.shadowCascadeCount = Mathf.Clamp(pipelineAsset.shadowCascadeCount, 1, MaxShadowCascadeCount);
			pipelineAsset.maxAdditionalLightsCount = Mathf.Min(pipelineAsset.maxAdditionalLightsCount, MaxAdditionalLightsPerObject);
		}

		private static RenderPipelineAsset GetDefaultRenderPipelineAsset()
		{
#if UNITY_6000_0_OR_NEWER
			return GraphicsSettings.defaultRenderPipeline;
#else
			return GraphicsSettings.renderPipelineAsset;
#endif
		}

	}
}
