using System.Collections;
using SSG_Core.Scripts.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SSG_Core.Scripts.Menu
{
	public class Tooltip : MonoBehaviour
	{
		[SerializeField] private TMP_Text _text;
		[SerializeField] private float _showWaitTime = 0.25f;
		[SerializeField] private Animation _animation;
		[SerializeField] private AnimationClip _openClip;
		[SerializeField] private AnimationClip _closeClip;
		[SerializeField] private Transform _scale;
		[Space]
		[SerializeField] private Image[] _bgImages;
		[SerializeField] private float _characterSpacing;
		[SerializeField] private float _horizontalEdgePadding;
		[SerializeField] private RectTransform _rect;

		private Coroutine _openRoutine;

		public void Open(string locKey, Vector2 offset)
		{
			_openRoutine = StartCoroutine(OpenRoutine(locKey, offset));
		}

		private IEnumerator OpenRoutine(string locKey, Vector2 offset)
		{
			// var colorHudData = GameManager.Instance.WorldColorManager.CurrentWorldColorData.HudColorData;
			// foreach (var bgImage in _bgImages)
			// {
			// 	bgImage.color = colorHudData.Background;
			// }

			var localizedString = Localizer.GetText(locKey);
			_text.text = localizedString;

			var spacing = _characterSpacing;
			var edgePadding = _horizontalEdgePadding;
			_rect.sizeDelta = new Vector2(
				spacing * localizedString.Length + edgePadding,
				_rect.sizeDelta.y);
			_rect.position += new Vector3(offset.x, offset.y, 0);

			_scale.localScale = Vector3.zero;
			yield return new WaitForSeconds(_showWaitTime);
			_animation.Play(_openClip.name);
			_openRoutine = null;
		}

		private Coroutine _closeRoutine;
		public void CloseAndDestroy()
		{
			_closeRoutine = StartCoroutine(CloseRoutine());
		}

		private IEnumerator CloseRoutine()
		{
			if (_scale.localScale.x > 0.33f)
				_animation.Play(_closeClip.name);
			yield return new WaitWhile(() => _animation.isPlaying);
			// _closeRoutine = null;
			Destroy(gameObject);
		}
	}
}