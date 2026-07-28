using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SSG_Core.Scripts.UI
{
	public class ToastManager : MonoBehaviour
	{
		[SerializeField] private Toast _toastPrefab;
		[SerializeField] private RectTransform _toastParent;
		[SerializeField] private float _defaultDuration = 2f;
		[SerializeField] private int _maxQueueSize = 10;
		[SerializeField] private bool _dontDestroyOnLoad = true;

		private readonly Queue<ToastRequest> _queue = new Queue<ToastRequest>();
		private Coroutine _showRoutine;

		public static ToastManager Instance { get; private set; }

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;
			if (_dontDestroyOnLoad)
				DontDestroyOnLoad(gameObject);
		}

		public void Show(string message, float duration = -1f)
		{
			if (string.IsNullOrWhiteSpace(message))
				return;

			if (_toastPrefab == null)
			{
				Debug.LogError("ToastManager: Toast prefab is not assigned.");
				return;
			}

			if (_showRoutine == null)
			{
				_showRoutine = StartCoroutine(ShowRoutine(new ToastRequest(message, duration)));
				return;
			}

			if (_queue.Count >= Mathf.Max(0, _maxQueueSize))
			{
				Debug.LogWarning("ToastManager: Queue is full; dropping toast.");
				return;
			}

			_queue.Enqueue(new ToastRequest(message, duration));
		}

		public static void ShowToast(string message, float duration = -1f)
		{
			if (Instance == null)
			{
				Debug.LogError("ToastManager: No instance in scene.");
				return;
			}

			Instance.Show(message, duration);
		}

		private IEnumerator ShowRoutine(ToastRequest request)
		{
			while (true)
			{
				var parent = GetToastParent();
				if (parent == null)
				{
					Debug.LogError("ToastManager: Could not find toast parent canvas.");
					_showRoutine = null;
					yield break;
				}

				var toast = Instantiate(_toastPrefab, parent);
				var durationToUse = request.Duration > 0f ? request.Duration : _defaultDuration;
				toast.Open(request.Message, durationToUse);

				yield return new WaitUntil(() => toast == null);

				if (_queue.Count == 0)
					break;

				request = _queue.Dequeue();
			}

			_showRoutine = null;
		}

		private RectTransform GetToastParent()
		{
			if (_toastParent != null)
				return _toastParent;

			var canvas = FindFirstObjectByType<Canvas>();
			return canvas != null ? canvas.transform as RectTransform : null;
		}

		private readonly struct ToastRequest
		{
			public readonly string Message;
			public readonly float Duration;

			public ToastRequest(string message, float duration)
			{
				Message = message;
				Duration = duration;
			}
		}
	}
}
