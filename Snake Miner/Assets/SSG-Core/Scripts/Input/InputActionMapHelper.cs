using System;
using UnityEngine;

namespace SSG_Core.Scripts.Input
{
	public static class InputActionMapHelper
	{
		public const string UI = "UI";
		public const string Player = "Player";
		public const string SkillTree = "SkillTree";

		public static event Action<string> OnActionMapChanged;

		public static void ChangeAllInputActionMap(string actionMap)
		{
			Debug.Log("changing input map to " + actionMap);
			InputManager.InputActions.Player.Disable();
			InputManager.InputActions.UI.Disable();
			InputManager.InputActions.SkillTree.Disable();

			switch (actionMap)
			{
				case UI:
					InputManager.InputActions.UI.Enable();
					break;
				case Player:
					InputManager.InputActions.Player.Enable();
					break;
				case SkillTree:
					InputManager.InputActions.SkillTree.Enable();
					break;
				default:
					Debug.LogError("unknown action map!");
					break;
			}

			OnActionMapChanged?.Invoke(actionMap);
		}
	}
}