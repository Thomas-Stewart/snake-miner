using System.Collections;
using SSG_Core.Scripts.Audio;
using UnityEngine;

namespace SSG_Core.Scripts.Scene
{
	public class LoadingScreen : MonoBehaviour
	{
		[SerializeField] private Canvas _canvas;
		[SerializeField] private Animation _animation;
		[SerializeField] private AnimationClip _showAnimClip;
		[SerializeField] private AnimationClip _hideAnimClip;
		[SerializeField] private float _minShowTime = 1f;

		public bool IsShowing { get; private set; }
		public bool IsInTransition => _animation.isPlaying;

		private float _timeLastShown;

		private void Awake()
		{
			_canvas.gameObject.SetActive(false);
		}

		public void Show()
		{
			IsShowing = true;
			_timeLastShown = Time.time;
			MusicManager.Instance.PlayStinger(StingerEvent.LoadingScreenShow);
			_animation.Play(_showAnimClip.name);
			MusicManager.Instance.PlayStinger(StingerEvent.LoadingCogMove);
		}

		public void Hide()
		{
			StartCoroutine(HideRoutine());
		}

		private IEnumerator HideRoutine()
		{
			if (Time.time - _timeLastShown < _minShowTime)
				yield return new WaitForSeconds(_minShowTime - (Time.time - _timeLastShown));
			MusicManager.Instance.StopStinger(StingerEvent.LoadingCogMove);
			MusicManager.Instance.PlayStinger(StingerEvent.LoadingScreenHide);
			_animation.Play(_hideAnimClip.name);
			yield return new WaitWhile(() => _animation.isPlaying);
			IsShowing = false;
		}
	}
}