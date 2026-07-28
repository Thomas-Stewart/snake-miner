using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using SSG_Core.Scripts.Util;

namespace SSG_Core.Scripts.Input
{
	public static class InputManager
	{
		public static readonly InputActions InputActions;

		private static InputSystemUIInputModule _inputSystemUIInputModule;

		static InputManager()
		{
			InputActions = new InputActions();
			InputActions.Enable();

			GetUIInputModule();
		}

		private static InputSystemUIInputModule GetUIInputModule()
		{
			if (_inputSystemUIInputModule != null)
				return _inputSystemUIInputModule;

			_inputSystemUIInputModule = Object.FindAnyObjectByType<InputSystemUIInputModule>();
			return _inputSystemUIInputModule;
		}

		public static bool IsCursorOverUI()
		{
			if (CheatManager.Instance != null && CheatManager.Instance.IsPointerOverCheatOverlay())
				return true;

			var inputModule = GetUIInputModule();
			return inputModule != null
			       && !ControllerHelper.Instance.IsMostRecentControlTypeAController
			       && inputModule.IsPointerOverGameObject(PointerInputModule.kMouseLeftId);
		} 

	}
}
