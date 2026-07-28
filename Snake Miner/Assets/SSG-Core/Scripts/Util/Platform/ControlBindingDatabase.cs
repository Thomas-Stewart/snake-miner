using System;
using System.Collections.Generic;
using System.Linq;
using SSG_Core.Scripts.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SSG_Core.Scripts.Util.Platform
{
	[CreateAssetMenu(fileName = nameof(ControlBindingDatabase), menuName = "SSG/ControlBindingDatabase")]
	public class ControlBindingDatabase : ScriptableObject
	{
		[SerializeField] private List<ControlBindingData> _controlBindingDatas;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void RuntimeInitialize()
		{
			CreateInstance();
		}

		private static ControlBindingDatabase CreateInstance()
		{
			if (_instanceBacking != null) return _instanceBacking;

			_instanceBacking = Resources.Load<ControlBindingDatabase>(nameof(ControlBindingDatabase));
			if (_instanceBacking == null)
			{
				Debug.LogError($"Cannot find ControlBindingDatabase");
				return null;
			}
			return _instanceBacking;
		}

		public Sprite GetControlBindingSymbol(InputActionReference inputAction, ControllerHelper.ControlType? forcedControlType = null, int playerId = -1)
		{
			if (inputAction == null || inputAction.action == null)
				return null;

			var deviceId = ControllerHelper.Instance?.GetDeviceIdFromPlayerId(playerId) ?? -1;
			var defaultController = ControllerHelper.Instance?.GetActiveControlType(deviceId) ??
			                        ControllerHelper.ControlType.Xbox;
			var defaultControlType = ControllerHelper.Instance.IsMostRecentControlTypeAController
				? defaultController
				: ControllerHelper.ControlType.Keyboard;
			var controlType = forcedControlType ?? defaultControlType;
			var data = _controlBindingDatas.FirstOrDefault(d =>
				d.ControlType == controlType &&
				d.Action != null &&
				d.Action.action != null &&
				d.Action.action.id == inputAction.action.id);

			return data.Symbol;
		}

		[Serializable]
		private struct ControlBindingData
		{
			public ControllerHelper.ControlType ControlType;
			public InputActionReference Action;
			public Sprite Symbol;
		}

		public static ControlBindingDatabase Instance => _instanceBacking != null ? _instanceBacking : CreateInstance();
		private static ControlBindingDatabase _instanceBacking;
	}
}
