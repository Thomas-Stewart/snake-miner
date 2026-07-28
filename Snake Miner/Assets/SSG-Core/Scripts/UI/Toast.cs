using System.Collections;
using SSG_Core.Scripts.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SSG_Core.Scripts.UI
{
	public class Toast : MonoBehaviour
	{
		[SerializeField] private float _defaultShowDuration = 1f;
		[SerializeField] private float _bgSpacingPerCharacter = 1f;
		[SerializeField] private float _edgePadding = 15f;
		[SerializeField] private TMP_Text _text;
		[SerializeField] private Image _bg;
		[SerializeField] private Animation _animation;
		[SerializeField] private AnimationClip _showAnim;
		[SerializeField] private AnimationClip _hideAnim;

		private const float EDGE_REDUCTION = -500f;

		public void Open(string text, float showDuration = -1f)
		{
			StartCoroutine(OpenRoutine(text, showDuration));
		}

		private IEnumerator OpenRoutine(string text, float showDuration)
		{
			if (showDuration <= 0)
				showDuration = _defaultShowDuration;

			_text.text = text;

			ResizeBg(text);

			_animation.Play(_showAnim.name);
			MusicManager.Instance.PlayStinger(StingerEvent.ToastInOut);
			yield return new WaitForSeconds(showDuration);
			_animation.Play(_hideAnim.name);
			MusicManager.Instance.PlayStinger(StingerEvent.ToastInOut);
			yield return new WaitWhile(() => _animation.isPlaying);
			Destroy(gameObject);
		}

		private void ResizeBg(string text)
		{
			_bg.rectTransform.sizeDelta = new Vector2(
				_text.preferredWidth + EDGE_REDUCTION + _edgePadding,
				_bg.rectTransform.sizeDelta.y);
		}
	}
}