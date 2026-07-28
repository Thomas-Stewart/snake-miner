using System.Linq;
using BallBounce.RingEscape;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.Scene;
using SSG.Util;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SSG_Core.Scripts.Util
{
	/// <summary>
	/// Centralized controller for project-level debug cheats.
	/// </summary>
	public class CheatManager : MonoBehaviour
	{
		[SerializeField] private DebugUI[] _debugUIs;
		[SerializeField] private KeyCode _toggleCheatsHotkey = KeyCode.BackQuote;
		[SerializeField] private KeyCode _toggleCheatOverlayHotkey = KeyCode.F1;

		private const float CheatOverlayWidth = 390f;
		private const float CheatOverlayMaxHeight = 540f;
		private const float CheatOverlayPadding = 16f;
		private const int CheatOverlayTitleFontSize = 24;
		private const int CheatOverlayLabelFontSize = 18;
		private const int CheatOverlaySectionFontSize = 20;
		private const int CheatOverlayButtonFontSize = 20;
		private const float CheatOverlayButtonHeight = 50f;
		private const float CheatOverlayTitleHeight = 32f;
		private const float CheatOverlayStatusHeight = 28f;
		private const float CheatOverlaySectionHeight = 28f;
		private const float CheatOverlayButtonSpacing = 6f;
		private const float CheatOverlaySectionSpacing = 12f;
		private const float CheatOverlayTwoColumnGap = 8f;
		private const float CheatOverlayScrollbarWidth = 18f;

		private Vector2 _cheatOverlayScroll;
		private bool _isCheatOverlayVisible;
		private GUIStyle _cheatOverlayTitleStyle;
		private GUIStyle _cheatOverlayLabelStyle;
		private GUIStyle _cheatOverlaySectionLabelStyle;
		private GUIStyle _cheatOverlayButtonStyle;
		private Texture2D _cheatOverlayWindowBackground;
		private Texture2D _cheatOverlayButtonBackground;
		private Texture2D _cheatOverlayButtonHoverBackground;
		private Texture2D _cheatOverlayButtonActiveBackground;

		public bool IsEnabled { get; private set; }
		public bool IsDebugUIEnabled => _debugUIs != null && _debugUIs.Any(d => d != null && !d.IsDisabled);

		public static CheatManager Instance { get; private set; }

		public bool IsPointerOverCheatOverlay()
		{
			if (!_isCheatOverlayVisible || Mouse.current == null)
				return false;

			var mousePosition = Mouse.current.position.ReadValue();
			var guiPosition = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
			return GetCheatOverlayRect().Contains(guiPosition);
		}

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;
			if (Application.isPlaying)
				DontDestroyOnLoad(gameObject);
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		public void SetCheatsEnabled(bool isEnabled)
		{
			IsEnabled = isEnabled;
		}

		private void Update()
		{
			if (WasKeyPressed(_toggleCheatOverlayHotkey))
			{
				_isCheatOverlayVisible = !_isCheatOverlayVisible;
				if (_isCheatOverlayVisible)
					SetCheatsEnabled(true);
			}

			if (WasKeyPressed(_toggleCheatsHotkey))
			{
				SetCheatsEnabled(!IsEnabled);
				if (!IsEnabled)
					_isCheatOverlayVisible = false;
				Debug.Log($"CheatManager: Cheats {(IsEnabled ? "enabled" : "disabled")}.");
			}

			if (!IsEnabled)
				return;

			if (WasKeyPressed(KeyCode.F))
				AddCurrency(IsKeyPressed(KeyCode.LeftShift) || IsKeyPressed(KeyCode.RightShift) ? 50000 : 100);
			if (WasKeyPressed(KeyCode.R))
				ReloadGameDataAndScene();
			if (WasKeyPressed(KeyCode.C))
				RandomizeSimulationColors();
			if (WasKeyPressed(KeyCode.F2))
				OpenSkillTree();
			if (WasKeyPressed(KeyCode.F7))
				UnlockAllUpgrades();
			if (WasKeyPressed(KeyCode.F12))
				ResetSave();
			if (WasKeyPressed(KeyCode.Alpha2))
				ToggleTimeScale();
		}

		private static bool WasKeyPressed(KeyCode keyCode)
		{
			var key = ToInputSystemKey(keyCode);
			return key != Key.None && Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
		}

		private static bool IsKeyPressed(KeyCode keyCode)
		{
			var key = ToInputSystemKey(keyCode);
			return key != Key.None && Keyboard.current != null && Keyboard.current[key].isPressed;
		}

		private static Key ToInputSystemKey(KeyCode keyCode)
		{
			switch (keyCode)
			{
				case KeyCode.BackQuote: return Key.Backquote;
				case KeyCode.F1: return Key.F1;
				case KeyCode.F2: return Key.F2;
				case KeyCode.F7: return Key.F7;
				case KeyCode.F12: return Key.F12;
				case KeyCode.F: return Key.F;
				case KeyCode.C: return Key.C;
				case KeyCode.R: return Key.R;
				case KeyCode.LeftShift: return Key.LeftShift;
				case KeyCode.RightShift: return Key.RightShift;
				case KeyCode.Alpha2: return Key.Digit2;
				default: return Key.None;
			}
		}

		private void OnGUI()
		{
			if (!_isCheatOverlayVisible)
				return;

			EnsureCheatOverlayStyles();
			var panelRect = GetCheatOverlayRect();
			var contentRect = new Rect(
				panelRect.x + CheatOverlayPadding,
				panelRect.y + CheatOverlayPadding,
				panelRect.width - CheatOverlayPadding * 2f,
				panelRect.height - CheatOverlayPadding * 2f);

			var previousDepth = GUI.depth;
			var previousColor = GUI.color;
			var previousContentColor = GUI.contentColor;
			var previousBackgroundColor = GUI.backgroundColor;
			try
			{
				GUI.depth = -10000;
				GUI.color = Color.white;
				GUI.contentColor = Color.white;
				GUI.backgroundColor = Color.white;
				GUI.DrawTexture(panelRect, _cheatOverlayWindowBackground, ScaleMode.StretchToFill);
				DrawCheatOverlayWindow(contentRect);
			}
			finally
			{
				GUI.depth = previousDepth;
				GUI.color = previousColor;
				GUI.contentColor = previousContentColor;
				GUI.backgroundColor = previousBackgroundColor;
			}
		}

		private static Rect GetCheatOverlayRect()
		{
			var height = Mathf.Min(CheatOverlayMaxHeight, Mathf.Max(220f, Screen.height - 24f));
			var width = Mathf.Min(CheatOverlayWidth, Mathf.Max(260f, Screen.width - 24f));
			return new Rect(12f, Mathf.Max(12f, Screen.height - height - 12f), width, height);
		}

		private void DrawCheatOverlayWindow(Rect contentRect)
		{
			var headerY = contentRect.y;
			GUI.Label(new Rect(contentRect.x, headerY, contentRect.width, CheatOverlayTitleHeight), "Debug Cheats", _cheatOverlayTitleStyle);
			headerY += CheatOverlayTitleHeight;
			GUI.Label(new Rect(contentRect.x, headerY, contentRect.width, CheatOverlayStatusHeight), $"Cheats: {(IsEnabled ? "On" : "Off")} | F1 closes", _cheatOverlayLabelStyle);
			headerY += CheatOverlayStatusHeight + 6f;

			var scrollViewport = new Rect(contentRect.x, headerY, contentRect.width, Mathf.Max(1f, contentRect.yMax - headerY));
			var scrollContentWidth = Mathf.Max(1f, contentRect.width - CheatOverlayScrollbarWidth);
			var scrollContent = new Rect(0f, 0f, scrollContentWidth, GetCheatOverlayContentHeight());
			_cheatOverlayScroll = GUI.BeginScrollView(scrollViewport, _cheatOverlayScroll, scrollContent);
			try
			{
				var y = 0f;
				DrawCheatSection(ref y, scrollContentWidth, "Currency");
				DrawCurrencyButtonRow(ref y, scrollContentWidth, "+100", 100L, "+50K", 50_000L);
				DrawCurrencyButtonRow(ref y, scrollContentWidth, "+1M", 1_000_000L, "+100M", 100_000_000L);
				if (DrawCheatButton(ref y, scrollContentWidth, "Reset Currency"))
					ResetCurrency();

				DrawCheatSection(ref y, scrollContentWidth, "Progression");
				if (DrawCheatButton(ref y, scrollContentWidth, "Open Skill Tree"))
					OpenSkillTree();
				if (DrawCheatButton(ref y, scrollContentWidth, "Unlock All Upgrades"))
					UnlockAllUpgrades();
				if (DrawCheatButton(ref y, scrollContentWidth, "Reset Save"))
					ResetSave();

				DrawCheatSection(ref y, scrollContentWidth, "System");
				if (DrawCheatButton(ref y, scrollContentWidth, Time.timeScale > 1.1f ? "Set Time Scale 1x" : "Set Time Scale 3x"))
					ToggleTimeScale();
				if (DrawCheatButton(ref y, scrollContentWidth, "Reload Game Data / Scene"))
					ReloadGameDataAndScene();
				if (DrawCheatButton(ref y, scrollContentWidth, "Toggle Debug UI"))
					ToggleDebugUI();
				if (DrawCheatButton(ref y, scrollContentWidth, "Randomize Simulation Colors (C)"))
					RandomizeSimulationColors();
				if (DrawCheatButton(ref y, scrollContentWidth, "Close"))
					_isCheatOverlayVisible = false;
			}
			finally
			{
				GUI.EndScrollView();
			}
		}

		private void EnsureCheatOverlayStyles()
		{
			if (_cheatOverlayButtonStyle != null &&
			    _cheatOverlayTitleStyle != null &&
			    _cheatOverlayLabelStyle != null &&
			    _cheatOverlaySectionLabelStyle != null &&
			    _cheatOverlayWindowBackground != null)
				return;

			_cheatOverlayWindowBackground = CreateCheatOverlayTexture(new Color(0.05f, 0.05f, 0.06f, 0.94f));
			_cheatOverlayTitleStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleLeft,
				fontSize = CheatOverlayTitleFontSize,
				fontStyle = FontStyle.Bold,
				wordWrap = false
			};
			SetStyleTextColor(_cheatOverlayTitleStyle, Color.white);

			_cheatOverlayLabelStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = CheatOverlayLabelFontSize,
				fontStyle = FontStyle.Bold,
				wordWrap = true
			};
			SetStyleTextColor(_cheatOverlayLabelStyle, Color.white);

			_cheatOverlaySectionLabelStyle = new GUIStyle(_cheatOverlayLabelStyle)
			{
				fontSize = CheatOverlaySectionFontSize
			};
			SetStyleTextColor(_cheatOverlaySectionLabelStyle, new Color(1f, 0.86f, 0.22f, 1f));

			_cheatOverlayButtonStyle = new GUIStyle(GUI.skin.button)
			{
				alignment = TextAnchor.MiddleCenter,
				fontSize = CheatOverlayButtonFontSize,
				fontStyle = FontStyle.Bold,
				margin = new RectOffset(0, 0, 5, 5),
				padding = new RectOffset(12, 12, 8, 8),
				wordWrap = true
			};
			_cheatOverlayButtonStyle.border = new RectOffset(6, 6, 6, 6);
			_cheatOverlayButtonBackground = CreateCheatOverlayTexture(new Color(0.95f, 0.95f, 0.9f, 1f));
			_cheatOverlayButtonHoverBackground = CreateCheatOverlayTexture(new Color(1f, 0.88f, 0.34f, 1f));
			_cheatOverlayButtonActiveBackground = CreateCheatOverlayTexture(new Color(0.75f, 0.9f, 1f, 1f));
			SetButtonState(_cheatOverlayButtonStyle.normal, _cheatOverlayButtonBackground);
			SetButtonState(_cheatOverlayButtonStyle.hover, _cheatOverlayButtonHoverBackground);
			SetButtonState(_cheatOverlayButtonStyle.active, _cheatOverlayButtonActiveBackground);
			SetButtonState(_cheatOverlayButtonStyle.focused, _cheatOverlayButtonHoverBackground);
			SetButtonState(_cheatOverlayButtonStyle.onNormal, _cheatOverlayButtonBackground);
			SetButtonState(_cheatOverlayButtonStyle.onHover, _cheatOverlayButtonHoverBackground);
			SetButtonState(_cheatOverlayButtonStyle.onActive, _cheatOverlayButtonActiveBackground);
			SetButtonState(_cheatOverlayButtonStyle.onFocused, _cheatOverlayButtonHoverBackground);
		}

		private static Texture2D CreateCheatOverlayTexture(Color color)
		{
			var texture = new Texture2D(1, 1)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = FilterMode.Point;
			texture.SetPixel(0, 0, color);
			texture.Apply();
			return texture;
		}

		private static void SetStyleTextColor(GUIStyle style, Color color)
		{
			style.normal.textColor = color;
			style.hover.textColor = color;
			style.active.textColor = color;
			style.focused.textColor = color;
			style.onNormal.textColor = color;
			style.onHover.textColor = color;
			style.onActive.textColor = color;
			style.onFocused.textColor = color;
		}

		private static void SetButtonState(GUIStyleState state, Texture2D background)
		{
			state.background = background;
			state.textColor = Color.black;
		}

		private static float GetCheatOverlayContentHeight()
		{
			const int sectionCount = 3;
			const int buttonRowCount = 3 + 3 + 6;
			return sectionCount * (CheatOverlaySectionHeight + 4f)
			       + (sectionCount - 1) * CheatOverlaySectionSpacing
			       + buttonRowCount * (CheatOverlayButtonHeight + CheatOverlayButtonSpacing)
			       + 8f;
		}

		private void DrawCheatSection(ref float y, float width, string label)
		{
			if (y > 0f)
				y += CheatOverlaySectionSpacing;

			GUI.Label(new Rect(0f, y, width, CheatOverlaySectionHeight), label, _cheatOverlaySectionLabelStyle);
			y += CheatOverlaySectionHeight + 4f;
		}

		private bool DrawCheatButton(ref float y, float width, string label)
		{
			var clicked = GUI.Button(new Rect(0f, y, width, CheatOverlayButtonHeight), label, _cheatOverlayButtonStyle);
			y += CheatOverlayButtonHeight + CheatOverlayButtonSpacing;
			return clicked;
		}

		private void DrawCurrencyButtonRow(
			ref float y,
			float width,
			string leftLabel,
			long leftAmount,
			string rightLabel,
			long rightAmount)
		{
			var buttonWidth = Mathf.Max(1f, (width - CheatOverlayTwoColumnGap) * 0.5f);
			if (GUI.Button(new Rect(0f, y, buttonWidth, CheatOverlayButtonHeight), leftLabel, _cheatOverlayButtonStyle))
				AddCurrency(leftAmount);
			if (GUI.Button(new Rect(buttonWidth + CheatOverlayTwoColumnGap, y, buttonWidth, CheatOverlayButtonHeight), rightLabel, _cheatOverlayButtonStyle))
				AddCurrency(rightAmount);
			y += CheatOverlayButtonHeight + CheatOverlayButtonSpacing;
		}

		private static void AddCurrency(long amount)
		{
			var clampedAmount = System.Math.Max(0L, amount);
			if (clampedAmount <= 0)
				return;

			var currentMoney = System.Math.Max(0L, SaveUtil.SaveData.CashMoney);
			SaveUtil.SaveData.CashMoney = clampedAmount > long.MaxValue - currentMoney
				? long.MaxValue
				: currentMoney + clampedAmount;
			SaveUtil.SetSaveDataVariable(SaveUtil.SaveData, true);
			RefreshSkillTreeVisuals();
		}

		private static void ResetCurrency()
		{
			SaveUtil.SaveData.CashMoney = 0;
			SaveUtil.SetSaveDataVariable(SaveUtil.SaveData, true);
			RefreshSkillTreeVisuals();
		}

		private static void ReloadGameDataAndScene()
		{
			if (GameDataManager.Instance != null)
				GameDataManager.Instance.ReadInGameData();

			var activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
			var sceneName = string.IsNullOrWhiteSpace(activeSceneName) ? SceneNames.Game : activeSceneName;
			if (CoreGameManager.Instance != null)
				CoreGameManager.Instance.GoToScene(sceneName);
		}

		private static void OpenSkillTree()
		{
			if (CoreGameManager.Instance != null)
				CoreGameManager.Instance.GoToScene(SceneNames.SkillTree);
		}

		private static void ResetSave()
		{
			SaveUtil.ResetSave();
		}

		private static void UnlockAllUpgrades()
		{
			SaveUtil.UnlockAllUpgrades();
		}

		private static void ToggleTimeScale()
		{
			Time.timeScale = Time.timeScale > 1.1f ? 1f : 3f;
		}

		private static void RandomizeSimulationColors()
		{
			RingEscapeSimulation.RandomizeAllColorProfiles();
		}

		private static void RefreshSkillTreeVisuals()
		{
			var skillTreeManager = Object.FindAnyObjectByType<SkillTreeManager>();
			if (skillTreeManager)
				skillTreeManager.RefreshAllVisuals();
		}

		public void ToggleDebugUI()
		{
#if CHEATS_ENABLED
			if (_debugUIs == null)
				return;

			foreach (var debugUI in _debugUIs)
			{
				if (debugUI != null)
					debugUI.ToggleUI();
			}
#endif
		}
	}
}
