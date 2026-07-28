using System.Collections;
using UnityEngine;

public class SkillTreePowerupInfoPanel : MonoBehaviour
{
	[SerializeField] private Transform _pulseTarget;
	[SerializeField] private float _pulseScale = 1.08f;
	[SerializeField] private float _pulseDuration = 0.22f;

	private Coroutine _pulseRoutine;
	private Vector3 _baseScale = Vector3.one;

	private void Awake()
	{
		if (_pulseTarget == null)
			_pulseTarget = transform;

		_baseScale = _pulseTarget.localScale;
	}

	public void PlayAttentionPulse()
	{
		if (_pulseTarget == null)
			return;

		if (_pulseRoutine != null)
			StopCoroutine(_pulseRoutine);

		_pulseRoutine = StartCoroutine(PulseRoutine());
	}

	public void PlayHighlightSequence()
	{
		PlayAttentionPulse();
	}

	private IEnumerator PulseRoutine()
	{
		var elapsed = 0f;
		while (elapsed < _pulseDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			var t = Mathf.Clamp01(elapsed / _pulseDuration);
			var pulse = Mathf.Sin(t * Mathf.PI);
			_pulseTarget.localScale = _baseScale * Mathf.Lerp(1f, _pulseScale, pulse);
			yield return null;
		}

		_pulseTarget.localScale = _baseScale;
		_pulseRoutine = null;
	}
}
