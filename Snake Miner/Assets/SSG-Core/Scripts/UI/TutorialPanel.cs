using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SSG_Core.Scripts.UI
{
	public class TutorialPanel : MonoBehaviour
	{
		[SerializeField] private Canvas _canvas;
		[SerializeField] private GameObject _continueDisplay;
		[SerializeField] private float _timeToWait;


		public event Action OnClosed;

		private void Awake()
		{
			_canvas.enabled = false;
		}

		public void Show()
		{
			_continueDisplay.SetActive(false);

			StartCoroutine(ShowRoutine());
		}

		private IEnumerator ShowRoutine()
		{
			_canvas.enabled = true;
			yield return new WaitForSeconds(_timeToWait);
			_continueDisplay.SetActive(true);
			yield return WaitForInput();
			_canvas.enabled = false;
			OnClosed?.Invoke();
		}

		private IEnumerator WaitForInput()
		{
			var action = new InputAction(type: InputActionType.PassThrough, binding: "*/<Button>");
			action.Enable();

			yield return new WaitUntil(() => action.triggered);

			action.Dispose();

			// MusicManager.Instance.PlayStinger(StingerEvent.ScoreboardContinue);
			//todo:
			// foreach (var playerId in GameManager.Instance.MultiplayerManager.GetActivePlayerIds())
			// {
			// 	var deviceId = ControllerHelper.Instance.GetDeviceIdFromPlayerId(playerId);
			// 	ControllerHelper.Instance.VibrateController(deviceId, ControllerHelper.VibrationType.BEEP);
			// }
		}
	}
}