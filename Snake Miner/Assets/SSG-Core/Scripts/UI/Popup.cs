using System;
using System.Collections;
using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SSG_Core.Scripts.UI
{
	public class Popup : MonoBehaviour
	{
		[SerializeField] private PopupType _popupType;
		[SerializeField] private Canvas _canvas;
		[SerializeField] private Image _bgImage;
		[SerializeField] private Image _bgRadiusImage;
		[SerializeField] private Image _blockerImage;
		[SerializeField] protected Animation _animation;
		[SerializeField] private AnimationClip _showAnim;
		[SerializeField] private AnimationClip _closeAnim;
		[SerializeField] private bool _shouldPauseTimeWhenOpen = true;
		private Coroutine _openRtn;

		public bool IsOpen => _canvas.enabled;

		public PopupType PopupType => _popupType;

		public event Action<Popup> OnOpened;
		public event Action<Popup> OnClosed;

		public virtual void Open(bool showBlocker = true)
		{
			if (_openRtn != null)
				StopCoroutine(_openRtn);
			EnsureRuntimeRefs();
			SetBlockerVisible(showBlocker);
			_openRtn = StartCoroutine(OpenRtn());
		}

		public void SetBlockerVisible(bool isVisible)
		{
			EnsureRuntimeRefs();
			if (_blockerImage != null)
				_blockerImage.enabled = isVisible;
		}

		public void ConfigureRuntime(PopupType popupType, Canvas canvas, bool shouldPauseTimeWhenOpen)
		{
			_popupType = popupType;
			_canvas = canvas;
			_shouldPauseTimeWhenOpen = shouldPauseTimeWhenOpen;
		}

		private IEnumerator OpenRtn()
		{
			EnsureRuntimeRefs();

			// clear selected top level btn selection
			if (ControllerHelper.Instance.IsMostRecentControlTypeAController)
				EventSystem.current.SetSelectedGameObject(null);
			
			_canvas.enabled = true;
			if (_animation != null && _showAnim != null)
				_animation.Play(_showAnim.name);
			MusicManager.Instance.PlayStinger(StingerEvent.PopupAppear);
			if (_animation != null && _showAnim != null)
				yield return new WaitWhile(() => _animation.isPlaying);
			if (_shouldPauseTimeWhenOpen)
				Time.timeScale = 0f;
			OnOpened?.Invoke(this);
		}

		/// <summary>
		/// use popupmanager.closepopup
		/// </summary>
		public void Close_ManagerOnly()
		{
			StartCoroutine(CloseRoutine());
		}

		private IEnumerator CloseRoutine()
		{
			EnsureRuntimeRefs();
			yield return null; // eat other inputs - so popups aren't immediately reopened
			if (_shouldPauseTimeWhenOpen)
				Time.timeScale = 1f;
			if (_animation != null && _closeAnim != null)
				_animation.Play(_closeAnim.name);
			MusicManager.Instance.PlayStinger(StingerEvent.PopupHide);
			if (_animation != null && _closeAnim != null)
				yield return new WaitWhile(() => _animation.isPlaying);
			if (_canvas != null)
				_canvas.enabled = false;
			OnClosed?.Invoke(this);
			gameObject.SetActive(false);
		}

		private void EnsureRuntimeRefs()
		{
			if (_canvas == null)
				_canvas = GetComponentInChildren<Canvas>(true);
			if (_blockerImage == null)
			{
				foreach (var image in GetComponentsInChildren<Image>(true))
				{
					if (image.name != "blockerimg")
						continue;

					_blockerImage = image;
					break;
				}
			}
		}
	}
}
