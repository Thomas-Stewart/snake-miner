using System.Linq;
using SSG_Core.Scripts.Input;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Util
{
	public class InputHelper : MonoBehaviour
	{
		// [SerializeField] private GameObject _selectorPillar;
		[SerializeField] private float _timeBetweenMousePosChecks = 1000;
		[SerializeField] private float _mousePosIgnoreDistance = 5f;
		[SerializeField] private float _durationToRegisterLongPress = 0.25f;

		private float _timeLastCheckedMousePos;
		private Vector2 _previousMousePos;
		private float _timeStartedHoldingClick = float.PositiveInfinity;
		private float _timeStoppedClick = float.PositiveInfinity;
		private bool IsLongPressing => Time.time - _timeStartedHoldingClick > _durationToRegisterLongPress;

		// public bool ShouldContinueCheckingMousePos()
		// {
		// 	var pressedThisFrame = InputManager.InputActions.Player.UseInputTool.WasPressedThisFrame();
		//
		// 	// Vector2 mousePos = ControllerHelper.Instance.IsMostRecentControlTypeAController ? _selectorPillar.transform.position : Mouse.current.position.ReadValue();
		// 	var mousePos = Mouse.current.position.ReadValue();
		// 	var anyKeyboardKeyDown = Keyboard.current?.anyKey?.IsPressed();
		// 	var anyKeyDown = anyKeyboardKeyDown ?? false;
		// 	anyKeyDown |=
		// 		ControllerHelper.Instance.IsMostRecentControlTypeAController && Gamepad.current != null && Gamepad.current.allControls.Any(x => x is ButtonControl button && x.IsPressed(0.1f) && !x.synthetic);
		// 	anyKeyDown |=
		// 		Gamepad.all.Any(x => (x.leftStick.magnitude > 0.1f || x.rightStick.magnitude > 0.1f) && !x.synthetic);
		//
		// 	var shouldCheckMousePos = Time.time - _timeLastCheckedMousePos > _timeBetweenMousePosChecks;
		// 	var mouseMovedFarEnough = Vector2.Distance(mousePos, _previousMousePos) > _mousePosIgnoreDistance;
		// 	if (!shouldCheckMousePos && !pressedThisFrame && !mouseMovedFarEnough && !anyKeyDown && !IsLongPressing)
		// 		return false;
		//
		// 	_previousMousePos = mousePos;
		// 	_timeLastCheckedMousePos = Time.time;
		//
		// 	return true;
		// }
	}
}