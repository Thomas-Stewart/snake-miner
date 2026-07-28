using System.Collections.Generic;
using Sirenix.OdinInspector;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.Scene;
using SSG_Core.Scripts.Util;
using SSG.Util;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace SSG_Core.Scripts.Audio
{
	/// <summary>
	/// Controls all sounds in the game - music and sfx
	/// </summary>
	public class MusicManager : MonoBehaviour
	{
		[SerializeField] private AudioSource[] _currentAudioSources;
		[SerializeField] private bool _disableAmbient;
		[SerializeField] private AudioSource _ambientAudioSource;
		[SerializeField] private float _minTimeBetweenSameStingers = 0.1f;
		[SerializeField] private Transform _stingerAudioSourceParent;
		[SerializeField] private List<StingerEvent> _loadingOkStingers;
		[SerializeField] private List<BgmData> _musicData;
		[SerializeField] private List<AmbientData> _ambientData;
		[SerializeField] private List<StingerData> _stingerData;

		public static MusicManager Instance { get; private set; }

		private BgmData _currentBgm;
		private AmbientData _currentAmbient;
		private static Random rnd = new Random();
		private const float DEFAULT_BGM_VOLUME = 0.4f;
		private const float DEFAULT_SFX_VOLUME = 1f;

		private float _bgmMasterVolume;
		private float _sfxMasterVolume;
		private bool _isMusicOverrideActive;
		private int _cachedActiveSceneHandle = int.MinValue;
		private string _cachedActiveSceneName;

		private Dictionary<StingerEvent, float> _lastPlayTimesByStinger = new ();
		private readonly Dictionary<StingerEvent, PitchUpState> _pitchUpStatesByStinger = new();

		private void Initialize()
		{
			if (Instance == null)
			{
				Instance = this;
				// if (Application.isPlaying)
				// 	DontDestroyOnLoad(Instance);
				SceneManager.activeSceneChanged += HandleSceneChange;
			}
		}

		private void HandleSceneChange(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
		{
			_isMusicOverrideActive = false;
			_currentBgm = null;
			foreach (var currentAudioSource in _currentAudioSources)
			{
				if (currentAudioSource)
				{
					currentAudioSource.Stop();
					currentAudioSource.loop = false;
				}
			}
		}

		public void RefreshVolumes()
		{
			var oldBgmMasterVolume = _bgmMasterVolume;
			_bgmMasterVolume = SaveUtil.GetBgmVolume() * DEFAULT_BGM_VOLUME;
			_sfxMasterVolume = SaveUtil.GetSfxVolume() * DEFAULT_SFX_VOLUME;

			foreach (var currentAudioSource in _currentAudioSources)
			{
				if (currentAudioSource != null)
					currentAudioSource.volume = oldBgmMasterVolume > 0.001f
						? currentAudioSource.volume / oldBgmMasterVolume * _bgmMasterVolume
						: _bgmMasterVolume;
			}

			if (_ambientAudioSource != null)
				_ambientAudioSource.volume = oldBgmMasterVolume > 0.001f
					? _ambientAudioSource.volume / oldBgmMasterVolume * _bgmMasterVolume
					: _bgmMasterVolume;
		}

		/// <summary>
		/// 0f - 1f
		/// </summary>
		public void SetBgmVolume(float newVol)
		{
			SaveUtil.SetBgmVolume(newVol);
			RefreshVolumes();
		}

		/// <summary>
		/// 0f - 1f
		/// </summary>
		public void SetSfxVolume(float newVol)
		{
			SaveUtil.SetSfxVolume(newVol);
			RefreshVolumes();
		}

		private void Awake()
		{
			if (Instance != null)
			{
				Destroy(gameObject);
				return;
			}
			Initialize();
			RefreshVolumes();
			foreach (var stingerData in _stingerData)
				_lastPlayTimesByStinger.TryAdd(stingerData.StingerEvent, float.NegativeInfinity);
		}

		public void PauseMusic()
		{
			foreach (var currentAudioSource in _currentAudioSources)
			{
				if (currentAudioSource != null)
					currentAudioSource.Pause();
			}
			if (_ambientAudioSource != null)
				_ambientAudioSource.Pause();
		}

		public void PlayNewAmbient(GamePhase gamePhase)
		{
			if (gamePhase == GamePhase.None)
				return;

			if (_ambientData.Count <= 0) return;

			if (Instance == null)
				Initialize();

			if (_currentAmbient != null && gamePhase == _currentAmbient.GamePhase && _ambientAudioSource.isPlaying) return;

			AmbientData newAmbient = null;
			for (var i = 0; i < _ambientData.Count; i++)
			{
				var ambient = _ambientData[i];
				if (ambient.GamePhase != gamePhase)
					continue;

				newAmbient = ambient;
				break;
			}

			if (newAmbient == null)
				return;

			// stop music
			if (_currentAmbient != null)
				_ambientAudioSource.Stop();

			// play music
			_currentAmbient = newAmbient;
			var audioClipData = _currentAmbient.AudioClips[rnd.Next(_currentAmbient.AudioClips.Count)];
			_ambientAudioSource.clip = audioClipData.AudioClip;
			_ambientAudioSource.volume = audioClipData.Volume * _bgmMasterVolume;
			_ambientAudioSource.Play();
			Debug.Log($"Playing ambient: {audioClipData.AudioClip.name}");
		}

		public void PlayMusicOverride(AudioClip clip, float volumeMultiplier = 1f)
		{
			if (clip == null || _currentAudioSources == null || _currentAudioSources.Length == 0 || _currentAudioSources[0] == null)
				return;

			if (Instance == null)
				Initialize();

			_isMusicOverrideActive = true;
			_currentBgm = null;

			for (var i = 0; i < _currentAudioSources.Length; i++)
			{
				var source = _currentAudioSources[i];
				if (source == null)
					continue;

				source.Stop();
				source.loop = i == 0;
				source.clip = i == 0 ? clip : null;
				source.volume = Mathf.Max(0f, volumeMultiplier) * _bgmMasterVolume;
			}

			_currentAudioSources[0].Play();
			Debug.Log($"Playing music override: {clip.name}");
		}

		public void StopMusicOverride()
		{
			if (!_isMusicOverrideActive)
				return;

			_isMusicOverrideActive = false;
			_currentBgm = null;

			if (_currentAudioSources == null)
				return;

			foreach (var source in _currentAudioSources)
			{
				if (source == null)
					continue;

				source.Stop();
				source.loop = false;
				source.clip = null;
			}
		}

		private void Update()
		{
			if (CoreGameManager.Instance && CoreGameManager.Instance.IsLoadingScreenShowing) return;
			if (_isMusicOverrideActive) return;

			if (!_disableAmbient && !_ambientAudioSource.isPlaying)
				PlayNewAmbient(CoreGameManager.Instance.CurrentGamePhase);

			PlayCurrentMusic();
		}

		private void PlayCurrentMusic()
		{
			if (_musicData.Count <= 0 || _currentAudioSources == null || _currentAudioSources.Length == 0 || _currentAudioSources[0] == null)
				return;

			if (Instance == null)
				Initialize();

			var newBgm = ResolveCurrentMusicData();
			if (newBgm == null || newBgm.AudioClips == null || newBgm.AudioClips.Count == 0)
				return;

			var source = _currentAudioSources[0];
			if (_currentBgm == newBgm && source.isPlaying)
				return;

			var audioClipData = newBgm.AudioClips[rnd.Next(newBgm.AudioClips.Count)];
			if (audioClipData.AudioClip == null)
				return;

			for (var i = 1; i < _currentAudioSources.Length; i++)
			{
				if (_currentAudioSources[i] != null)
					_currentAudioSources[i].Stop();
			}

			_currentBgm = newBgm;
			source.Stop();
			source.loop = true;
			source.clip = audioClipData.AudioClip;
			source.volume = audioClipData.Volume * _bgmMasterVolume;
			source.Play();
			Debug.Log($"Playing music: {audioClipData.AudioClip.name}");
		}

		private BgmData ResolveCurrentMusicData()
		{
			var sceneName = GetActiveSceneName();

			if (string.Equals(sceneName, SceneNames.Game, System.StringComparison.Ordinal))
			{
				BgmData levelTrack = null;
				for (var i = 0; i < _musicData.Count; i++)
				{
					var bgm = _musicData[i];
					if (bgm.MatchType != MusicTrackMatchType.LevelIndex || bgm.LevelIndex != SaveUtil.SaveData.CurrentLevelIndex)
						continue;

					levelTrack = bgm;
					break;
				}
				if (levelTrack != null)
					return levelTrack;
			}

			BgmData sceneTrack = null;
			for (var i = 0; i < _musicData.Count; i++)
			{
				var bgm = _musicData[i];
				if (bgm.MatchType != MusicTrackMatchType.SceneName ||
				    !string.Equals(bgm.SceneName, sceneName, System.StringComparison.Ordinal))
					continue;

				sceneTrack = bgm;
				break;
			}
			if (sceneTrack != null)
				return sceneTrack;

			return null;
		}

		private string GetActiveSceneName()
		{
			var activeScene = SceneManager.GetActiveScene();
			if (_cachedActiveSceneHandle == activeScene.handle)
				return _cachedActiveSceneName;

			_cachedActiveSceneHandle = activeScene.handle;
			_cachedActiveSceneName = activeScene.name;
			return _cachedActiveSceneName;
		}

		[Button]
		private void LogDspTime()
		{
			Debug.Log("AudioSettings.dspTime = " + AudioSettings.dspTime);
		}

		private Dictionary<StingerEvent, List<GameObject>> _stingerObjsByEvent =
			new Dictionary<StingerEvent, List<GameObject>>();
		public void StopStinger(StingerEvent stingerEvent)
		{
			if (_stingerObjsByEvent.TryGetValue(stingerEvent, out var objs))
			{
				for (var i = objs.Count - 1; i >= 0; i--)
				{
					Destroy(objs[i]);
				}
			}
		}
		public void PlayStinger(StingerEvent stingerEvent)
		{
			PlayStinger(stingerEvent, 1f, false);
		}

		public void PlayStinger(StingerEvent stingerEvent, float pitch, bool ignoreRepeatCooldown = false)
		{
			if (_stingerData.Count <= 0) return;
			var now = Time.unscaledTime;
			if (!ignoreRepeatCooldown &&
			    (!_lastPlayTimesByStinger.ContainsKey(stingerEvent) || now - _lastPlayTimesByStinger[stingerEvent] < _minTimeBetweenSameStingers))
				return;
			if (CoreGameManager.Instance.IsLoadingScreenShowing && !_loadingOkStingers.Contains(stingerEvent))
				return;

			StingerData stingerData = null;
			for (var i = 0; i < _stingerData.Count; i++)
			{
				var data = _stingerData[i];
				if (data.StingerEvent != stingerEvent)
					continue;

				stingerData = data;
				break;
			}
			if (stingerData == null)
			{
				Debug.LogError("Stinger data should not be null!");
				return;
			}
			
			if (stingerData.AudioClipDatas == null || stingerData.AudioClipDatas.Count == 0)
				return;

			var audioClipData = stingerData.AudioClipDatas[rnd.Next(stingerData.AudioClipDatas.Count)];
			if (!audioClipData.AudioClip) return;

			var stingerGo = new GameObject("Stinger_"+audioClipData.AudioClip.name);
			stingerGo.transform.SetParent(_stingerAudioSourceParent);
			var stingerAudioSource = stingerGo.AddComponent<AudioSource>();
			stingerAudioSource.clip = audioClipData.AudioClip;
			stingerAudioSource.volume = audioClipData.Volume * _sfxMasterVolume;
			stingerAudioSource.pitch = GetStingerPitch(stingerData, pitch);
			stingerAudioSource.Play();

			if (_stingerObjsByEvent.ContainsKey(stingerEvent))
				_stingerObjsByEvent[stingerEvent].Add(stingerGo);
			else
				_stingerObjsByEvent.Add(stingerEvent, new List<GameObject>{stingerGo});

			_lastPlayTimesByStinger[stingerEvent] = now;

			Destroy(stingerGo, audioClipData.AudioClip.length / stingerAudioSource.pitch + 2f);
		}

		private float GetStingerPitch(StingerData stingerData, float basePitch)
		{
			var pitch = basePitch;
			if (stingerData.UsePitchVariance && stingerData.PitchVariance > 0f)
				pitch += UnityEngine.Random.Range(-stingerData.PitchVariance, stingerData.PitchVariance);

			if (stingerData.UsePitchUp && stingerData.PitchUpAmount > 0f)
				pitch += GetPitchUpAmount(stingerData);

			return Mathf.Max(0.01f, pitch);
		}

		private float GetPitchUpAmount(StingerData stingerData)
		{
			var now = Time.unscaledTime;
			if (!_pitchUpStatesByStinger.TryGetValue(stingerData.StingerEvent, out var state))
				state = new PitchUpState { LastPlayTime = now };

			var elapsed = Mathf.Max(0f, now - state.LastPlayTime);
			var decayedPitch = Mathf.Max(0f, state.PitchOffset - stingerData.PitchUpDecayPerSecond * elapsed);
			state.PitchOffset = Mathf.Min(stingerData.PitchUpMax, decayedPitch + stingerData.PitchUpAmount);
			state.LastPlayTime = now;
			_pitchUpStatesByStinger[stingerData.StingerEvent] = state;
			return decayedPitch;
		}

		private struct PitchUpState
		{
			public float PitchOffset;
			public float LastPlayTime;
		}

		private void OnDestroy()
		{
			SceneManager.activeSceneChanged -= HandleSceneChange;
			if (Instance == this)
				Instance = null;
		}
	}
}
