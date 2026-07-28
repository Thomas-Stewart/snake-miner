using UnityEngine;

namespace SSG_Core.Scripts.UI
{
	public class MoveWithResolution_16x10 : MonoBehaviour
	{
		[SerializeField] private Vector3 _axis = Vector3.right;
		[SerializeField] private float _offsetAt16x10;
		[SerializeField] private bool _useLocalPosition = true;

		private const float ReferenceAspect = 16f / 9f;
		private const float TargetAspect = 16f / 10f;
		private Vector3 _initialPosition;

		private void Awake()
		{
			_initialPosition = _useLocalPosition ? transform.localPosition : transform.position;
			Apply();
		}

		private void OnEnable()
		{
			Apply();
		}

		private void Apply()
		{
			var currentAspect = Screen.height > 0 ? (float)Screen.width / Screen.height : ReferenceAspect;
			var t = Mathf.InverseLerp(ReferenceAspect, TargetAspect, currentAspect);
			var offset = _axis.normalized * (_offsetAt16x10 * Mathf.Clamp01(t));

			if (_useLocalPosition)
				transform.localPosition = _initialPosition + offset;
			else
				transform.position = _initialPosition + offset;
		}
	}
}
