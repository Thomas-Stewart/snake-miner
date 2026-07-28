using System.Collections;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.Util;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;
#if UNITY_PS5
using UnityEngine.InputSystem.PS5;
using UnityEngine.PS5;
#endif

namespace SSG_Core.Scripts.Input
{
	public class ControllerHelper : MonoBehaviour
	{
		public static ControllerHelper Instance { get; private set; }

		public bool IsRumbleEnabled { get; private set; } = true;

		private void Initialize()
		{
			if (Instance == null)
			{
				Instance = this;
				if (Application.isPlaying)
					DontDestroyOnLoad(Instance);
			}
		}

		private void Awake()
		{
			Initialize();
		}

		private void OnEnable()
		{
			InputSystem.onDeviceChange += OnDeviceChange;
		}

		private void OnDisable()
		{
			InputSystem.onDeviceChange -= OnDeviceChange;
		}

		public bool IsMostRecentControlTypeAController = false;
		private void Update()
		{
			if (HasMouseInputThisFrame() || HasKeyboardInputThisFrame())
			{
				IsMostRecentControlTypeAController = false;
				return;
			}

			if (HasGamepadInputThisFrame())
				IsMostRecentControlTypeAController = true;
		}

		private static bool HasMouseInputThisFrame()
		{
			var mouse = Mouse.current;
			var hasInputSystemMouseInput = mouse != null &&
			                               (mouse.delta.IsActuated() ||
			                                mouse.scroll.IsActuated() ||
			                                HasPressedMouseButton(mouse));
			return hasInputSystemMouseInput;
		}

		private static bool HasPressedMouseButton(Mouse mouse)
		{
			var controls = mouse.allControls;
			for (var i = 0; i < controls.Count; i++)
			{
				if (controls[i] is ButtonControl button && button.isPressed && !button.synthetic)
					return true;
			}

			return false;
		}

		private static bool HasKeyboardInputThisFrame()
		{
			var keyboard = Keyboard.current;
			return keyboard != null &&
			       keyboard.anyKey.isPressed;
		}

		private static bool HasGamepadInputThisFrame()
		{
			var gamepad = Gamepad.current;
			if (gamepad == null)
				return false;

			var controls = gamepad.allControls;
			for (var i = 0; i < controls.Count; i++)
			{
				var control = controls[i];
				if (!control.synthetic && control.IsActuated())
					return true;
			}

			return false;
		}

		private void OnDeviceChange(InputDevice device, InputDeviceChange change)
		{
			SetupRumbleOnExistingControllers();
		}

		public void SetupRumbleOnExistingControllers()
		{
#if UNITY_PS5
			foreach (var ps5Controller in DualSenseGamepad.all)
			{
				if (ps5Controller != null)
				{
					PS5Input.PadSetVibrationMode(ps5Controller.slotIndex, PS5Input.VibrationMode.Compatible2);
				}
			}
#endif
		}

		public void ToggleRumbleEnabled(bool isRumbleEnabled)
		{
			IsRumbleEnabled = isRumbleEnabled;
		}

		public int GetDeviceIdFromPlayerId(int playerId)
		{
			if (Gamepad.current == null) return -1;
			
			return Gamepad.current.deviceId;
		}

        public void ChangePS5Color(int playerId, Color color)
		{
			var deviceId = GetDeviceIdFromPlayerId(playerId);
			var device = FindGamepadByDeviceId(deviceId);
			var controlType = GetActiveControlType(deviceId);
			switch (controlType)
			{
				case ControlType.PlayStation:
#if UNITY_STANDALONE || UNITY_EDITOR
                    if (device is DualSenseGamepadHID psPcController)
					{
						if (color != Color.white && color != Color.black)
							color = color.BrightestAndMostSaturated();
						psPcController.SetLightBarColor(color);
					}
#endif
#if UNITY_PS5
					if (device is DualSenseGamepad ps5Controller)
					{
						if (color != Color.white && color != Color.black)
							color = color.BrightestAndMostSaturated();
						ps5Controller.SetLightBarColor(color);
					}
#endif
					break;
			}
		}

        // public static void IsDeviceLastInput()
        // {
	       //  var lastDevice = InputManager.InputActions.Player.Jump.activeControl?.device;
        //     
	       //  if (lastDevice != null && lastDevice.deviceId == ctx.control.device.deviceId)
	       //  {
		      //   Debug.Log($"Move pressed from device {ctx.control.device.deviceId}");
		      //   DoMovement(ctx.ReadValue<float>());
	       //  }
        // }

		// public void VibrateController(int deviceId, VibrationType vibrationType)
		public void VibrateController(int deviceId, VibrationType vibrationType)
		{
			if (!IsRumbleEnabled) return;

            StartCoroutine(VibrateControllerRoutine(deviceId, vibrationType));
		}

		private IEnumerator VibrateControllerRoutine(int deviceId, VibrationType vibrationType)
		{
			var device = FindGamepadByDeviceId(deviceId);
			// var device = Gamepad.current;
			if (device == null) yield break;

			var vibrationData = default(VibrationData);
			for (var i = 0; i < _vibrationDatas.Length; i++)
			{
				if (_vibrationDatas[i].VibrationType != vibrationType)
					continue;

				vibrationData = _vibrationDatas[i];
				break;
			}

			var controlType = GetActiveControlType(device.deviceId);
			switch (controlType)
			{
				case ControlType.Xbox:
					break;
				case ControlType.PlayStation:
#if UNITY_STANDALONE || UNITY_EDITOR
					if (device is DualSenseGamepadHID psPcController)
					{
						psPcController.SetMotorSpeeds(vibrationData.Strength, vibrationData.Strength);

						var startTime = Time.unscaledTimeAsDouble;
						var targetTime = startTime + vibrationData.Duration;
						while (Time.unscaledTimeAsDouble < targetTime)
						{
							yield return null;
						}
						psPcController.ResetHaptics();
					}
#endif
#if UNITY_PS5
                    if (device is DualSenseGamepad ps5Controller)
					{
                        ps5Controller.SetMotorSpeeds(vibrationData.Strength, vibrationData.Strength);

						var startTime = Time.unscaledTimeAsDouble;
						var targetTime = startTime + vibrationData.Duration;
						while (Time.unscaledTimeAsDouble < targetTime)
						{
							yield return null;
						}
                        ps5Controller.ResetHaptics();
					}
#endif
					break;
			}
		}

		private static VibrationData[] _vibrationDatas = new[]
		{
			new VibrationData
			{
				VibrationType = VibrationType.VERY_SMALL,
				Strength = 0.1f,
				Duration = 0.1f
			},
			new VibrationData
			{
				VibrationType = VibrationType.BEEP,
				Strength = 0.25f,
				Duration = 0.1f
			},
			new VibrationData
			{
				VibrationType = VibrationType.NORMAL,
				Strength = 0.5f,
				Duration = 0.25f
			},
			new VibrationData
			{
				VibrationType = VibrationType.LONG,
				Strength = 0.5f,
				Duration = 0.5f
			},
			new VibrationData
			{
				VibrationType = VibrationType.REALLY_LONG,
				Strength = 0.5f,
				Duration = 1f
			}
		};

		private struct VibrationData
		{
			public VibrationType VibrationType;
			public float Strength;
			public float Duration;
		}

		public enum VibrationType
		{
			VERY_SMALL,
			BEEP,
			NORMAL,
			LONG,
			REALLY_LONG,
		}

		public int GetActiveDeviceId()
		{
			var deviceId = -1;

			var devices = Gamepad.all;
			if (deviceId < 0 && devices.Count > 0)
			{
				deviceId = devices[0].deviceId;
			}

			return deviceId;
		}

		public ControlType GetActiveControlType(int deviceId)
		{
			if (CoreGameManager.Instance)
			{
				GetActiveDeviceId();
			}

			if (deviceId >= 0)
			{
				var device = FindGamepadByDeviceId(deviceId);

				if (device != null)
				{
					var displayName = device.name;
					if (displayName.Contains("Xbox"))
					{
						return ControlType.Xbox;
					}
					else if (displayName.Contains("DualShock") || displayName.Contains("DualSense"))
					{
						return ControlType.PlayStation;
					}

					return ControlType.Xbox;
				}
			}

			return ControlType.Keyboard;
		}

		private static Gamepad FindGamepadByDeviceId(int deviceId)
		{
			var gamepads = Gamepad.all;
			for (var i = 0; i < gamepads.Count; i++)
			{
				var gamepad = gamepads[i];
				if (gamepad.deviceId == deviceId)
					return gamepad;
			}

			return null;
		}

		public enum ControlType
		{
			Keyboard,
			Xbox,
			PlayStation
		}
	}
}
