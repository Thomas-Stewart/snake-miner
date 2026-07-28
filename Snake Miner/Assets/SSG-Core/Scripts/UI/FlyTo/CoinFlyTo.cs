using System;
using UnityEngine;

namespace SSG_Core.Scripts.UI.FlyTo
{
	[RequireComponent(typeof(RectTransform))]
	public class CoinFlyTo : MonoBehaviour
	{
		[SerializeField] private float _flyTime = 1.0f;
		[SerializeField] private AnimationCurve _path = AnimationCurve.Linear(0, 0, 1, 1);
		[SerializeField] private Vector2 _targetSize = Vector2.one;

		private float _timer;
		private Vector3 _targetScreenPosition;
		private Vector3 _startScreenPosition;
		private Vector2 _startSize;
		private RectTransform _rectTransform;

		public event Action OnArrive;

		private bool _isFlying;

		public void Initialize(Vector2 startScreenPos, Vector2 targetScreenPosition)
		{
			_rectTransform = GetComponent<RectTransform>();
			_startSize = Vector2.one * 0.5f;
			_startScreenPosition = startScreenPos;
			_targetScreenPosition = targetScreenPosition;

			_rectTransform.anchoredPosition = startScreenPos;
			_isFlying = true;
		}

		private void Update()
		{
			if (!_isFlying) return;

			_timer += Time.deltaTime;
			var step = _path.Evaluate(_timer / _flyTime);

			var currentScreenPosition = Vector2.Lerp(_startScreenPosition, _targetScreenPosition, step);
			_rectTransform.anchoredPosition = currentScreenPosition;
			_rectTransform.localScale = Vector2.Lerp(_startSize, _targetSize, step);

			if (step >= 1.0f)
			{
				_timer = 0.0f;
				_startScreenPosition = _targetScreenPosition;
				_rectTransform.localScale = _targetSize;
				_isFlying = false;
				OnArrive?.Invoke();
				Destroy(gameObject);
			}
		}
	}
}
