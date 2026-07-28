using System.Linq;
using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.Input;
using SSG_Core.Scripts.Localization;
using SSG_Core.Scripts.UI;
using SSG_Core.Scripts.Util;
using SSG.Util;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SSG_Core.Scripts.Menu
{
	public class OptionsPopup : MenuOptionHandler
	{
		[SerializeField] private BaseMenuOption _toggleSfx;
		[SerializeField] private BaseMenuOption _toggleBgm;
		[SerializeField] private BaseMenuOption _toggleLanguage;
		[SerializeField] private BaseMenuOption _toggleClickToMove;
		[SerializeField] private BaseMenuOption _toggleScreenShake;
		[SerializeField] private BaseMenuOption _resolutions;
		[SerializeField] private BaseMenuOption _toggleControllerRumble;
		[SerializeField] private BaseMenuOption _toggleCheats;
		[SerializeField] private BaseMenuOption _toggleFps;
		[SerializeField] private BaseMenuOption _exitOption;
		[SerializeField] private GameObject _languageLeftArrow;
		[SerializeField] private GameObject _languageRightArrow;
		[SerializeField] private GameObject _screenShakeLeftArrow;
		[SerializeField] private GameObject _screenShakeRightArrow;
		[SerializeField] private bool _addSoundValueArrows = true;
		[SerializeField] private Vector2 _soundArrowButtonSize = new Vector2(48f, 56f);
		[SerializeField] private float _soundArrowCenterX = 230f;
		[SerializeField] private float _soundArrowSpacing = 210f;
		[SerializeField] private Sprite _soundArrowSprite;
		[SerializeField] private Color _soundArrowColor = Color.white;
		[SerializeField] private Color _arrowHoverColor = new Color(1f, 0.85f, 0.25f, 1f);

		private bool _soundValueArrowsCreated;
		private GameObject _sfxLeftArrow;
		private GameObject _sfxRightArrow;
		private GameObject _bgmLeftArrow;
		private GameObject _bgmRightArrow;
		private bool _languageArrowsBound;

		private void Awake()
		{
			CreateSoundValueArrows();
			BindLanguageArrows();
		}

		private void OnEnable()
		{
			CreateSoundValueArrows();
			BindLanguageArrows();
			RefreshValues();
		}

		private void CreateSoundValueArrows()
		{
			if (!_addSoundValueArrows || _soundValueArrowsCreated)
				return;

			AddValueArrows(_toggleSfx, out _sfxLeftArrow, out _sfxRightArrow);
			AddValueArrows(_toggleBgm, out _bgmLeftArrow, out _bgmRightArrow);
			_toggleSfx?.SetMainButtonClickable(false);
			_toggleBgm?.SetMainButtonClickable(false);
			_toggleLanguage?.SetMainButtonClickable(false);
			_toggleScreenShake?.SetMainButtonClickable(false);
			_soundValueArrowsCreated = true;
		}

		private void BindLanguageArrows()
		{
			if (_languageArrowsBound)
				return;

			BindValueArrow(_languageLeftArrow, false);
			BindValueArrow(_languageRightArrow, true);
			BindValueArrow(_screenShakeLeftArrow, _toggleScreenShake, false);
			BindValueArrow(_screenShakeRightArrow, _toggleScreenShake, true);
			if (_toggleLanguage != null)
				CenterValueTextBetweenArrows(_toggleLanguage.ValueTextRectTransform);
			if (_toggleScreenShake != null)
				CenterValueTextBetweenArrows(_toggleScreenShake.ValueTextRectTransform);
			_languageArrowsBound = true;
		}

		private void BindValueArrow(GameObject arrowObject, bool shouldGoRight)
		{
			BindValueArrow(arrowObject, _toggleLanguage, shouldGoRight);
		}

		private void BindValueArrow(GameObject arrowObject, BaseMenuOption option, bool shouldGoRight)
		{
			if (arrowObject == null || option == null)
				return;

			var button = arrowObject.GetComponent<Button>();
			if (button == null)
				return;

			arrowObject.transform.SetAsLastSibling();
			ConfigureArrowButton(button, arrowObject.GetComponent<Image>());
			AddArrowHoverHighlight(arrowObject, option);
			button.onClick.AddListener(() =>
			{
				option.HandleDirectionalClick(shouldGoRight);
				RefreshValues();
			});
		}

		private void AddValueArrows(BaseMenuOption option, out GameObject leftArrow, out GameObject rightArrow)
		{
			leftArrow = null;
			rightArrow = null;

			var valueTextRect = option != null ? option.ValueTextRectTransform : null;
			if (valueTextRect == null || valueTextRect.parent == null)
				return;

			CenterValueTextBetweenArrows(valueTextRect);
			leftArrow = CreateValueArrow(option, valueTextRect.parent, false);
			rightArrow = CreateValueArrow(option, valueTextRect.parent, true);
		}

		private void CenterValueTextBetweenArrows(RectTransform valueTextRect)
		{
			valueTextRect.anchorMin = new Vector2(0.5f, 0.5f);
			valueTextRect.anchorMax = new Vector2(0.5f, 0.5f);
			valueTextRect.pivot = new Vector2(0.5f, 0.5f);
			valueTextRect.anchoredPosition = new Vector2(_soundArrowCenterX, 0f);
			valueTextRect.sizeDelta = new Vector2(
				Mathf.Max(1f, _soundArrowSpacing - _soundArrowButtonSize.x),
				valueTextRect.sizeDelta.y);

			var valueText = valueTextRect.GetComponent<TextMeshProUGUI>();
			if (valueText == null)
				return;

			valueText.alignment = TextAlignmentOptions.Center;
			valueText.margin = Vector4.zero;
		}

		private GameObject CreateValueArrow(
			BaseMenuOption option,
			Transform parent,
			bool shouldGoRight)
		{
			var arrowObject = new GameObject(shouldGoRight ? "Value Arrow Right" : "Value Arrow Left", typeof(RectTransform));
			arrowObject.layer = option.gameObject.layer;
			arrowObject.transform.SetParent(parent, false);
			arrowObject.transform.SetAsLastSibling();

			var rectTransform = (RectTransform)arrowObject.transform;
			rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
			rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
			rectTransform.pivot = new Vector2(0.5f, 0.5f);
			rectTransform.sizeDelta = _soundArrowButtonSize;
			rectTransform.anchoredPosition = new Vector2(
				_soundArrowCenterX + (shouldGoRight ? 1f : -1f) * _soundArrowSpacing * 0.5f,
				0f);
			rectTransform.localRotation = Quaternion.Euler(0f, 0f, shouldGoRight ? 90f : -90f);

			var image = arrowObject.AddComponent<Image>();
			image.sprite = _soundArrowSprite;
			image.color = _soundArrowColor;
			image.preserveAspect = true;
			image.raycastTarget = true;

			var button = arrowObject.AddComponent<Button>();
			button.targetGraphic = image;
			ConfigureArrowButton(button, image);
			AddArrowHoverHighlight(arrowObject, option);
			button.onClick.AddListener(() => option.HandleDirectionalClick(shouldGoRight));

			return arrowObject;
		}

		private void ConfigureArrowButton(Button button, Image image)
		{
			if (button == null || image == null)
				return;

			var colors = button.colors;
			colors.normalColor = image.color;
			colors.highlightedColor = _arrowHoverColor;
			colors.selectedColor = _arrowHoverColor;
			colors.pressedColor = _arrowHoverColor;
			button.colors = colors;
			button.transition = Selectable.Transition.ColorTint;
			button.targetGraphic = image;
		}

		private static void AddArrowHoverHighlight(GameObject arrowObject, BaseMenuOption option)
		{
			var eventTrigger = arrowObject.GetComponent<EventTrigger>() ?? arrowObject.AddComponent<EventTrigger>();
			eventTrigger.triggers.Add(CreateArrowHoverEntry(EventTriggerType.PointerEnter, data => option.OnPointerEnter((PointerEventData)data)));
			eventTrigger.triggers.Add(CreateArrowHoverEntry(EventTriggerType.PointerExit, data => option.OnPointerExit((PointerEventData)data)));
		}

		private static EventTrigger.Entry CreateArrowHoverEntry(EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> callback)
		{
			var entry = new EventTrigger.Entry { eventID = eventType };
			entry.callback.AddListener(callback);
			return entry;
		}

		private void RefreshValues()
		{
			if (_toggleSfx != null)
				_toggleSfx.SetValue(SaveUtil.GetSfxVolume(), true);

			if (_toggleBgm != null)
				_toggleBgm.SetValue(SaveUtil.GetBgmVolume(), true);

			if (_toggleLanguage != null)
				_toggleLanguage.SetValue(Localizer.GetLanguageCode(Localizer.GetCurrentLanguageIndex()));

			if (_toggleClickToMove != null)
				_toggleClickToMove.SetValue(SaveUtil.GetClickToMoveEnabled());

			if (_toggleScreenShake != null)
				_toggleScreenShake.SetValue(SaveUtil.GetScreenShakeEnabled());

			if (_resolutions != null)
			{
				var resolutionWidth = SaveUtil.GetResolutionWidth();
				_resolutions.SetValue(string.Format(
					Localizer.GetText("ui_resolution_format"),
					resolutionWidth,
					resolutionWidth * SaveUtil.RESOLUTION_RATIO_16_9));
			}

			if (_toggleControllerRumble != null)
				_toggleControllerRumble.SetValue(ControllerHelper.Instance.IsRumbleEnabled);

			var isCheatsOn = CheatManager.Instance != null && CheatManager.Instance.IsEnabled;
			if (_toggleCheats != null)
				_toggleCheats.SetValue(isCheatsOn);

			if (_toggleFps != null && CheatManager.Instance != null)
			{
				var isFpsOn = CheatManager.Instance.IsDebugUIEnabled;
				_toggleFps.SetValue(isCheatsOn && isFpsOn);
				_toggleFps.gameObject.SetActive(isCheatsOn);
			}

			if (_exitOption != null)
				_exitOption.SetValue(string.Empty);

			RefreshSoundArrowVisibility();
			RefreshLanguageArrowVisibility();
		}

		private void RefreshSoundArrowVisibility()
		{
			SetArrowVisibility(_sfxLeftArrow, _sfxRightArrow, SaveUtil.GetSfxVolume());
			SetArrowVisibility(_bgmLeftArrow, _bgmRightArrow, SaveUtil.GetBgmVolume());
		}

		private void RefreshLanguageArrowVisibility()
		{
			var languageCount = Localizer.GetLanguageCount();
			var languageIndex = Localizer.GetCurrentLanguageIndex();
			SetArrowVisibility(_languageLeftArrow, _languageRightArrow, languageIndex, languageCount);
			SetArrowVisibility(_screenShakeLeftArrow, _screenShakeRightArrow, SaveUtil.GetScreenShakeEnabled());
		}

		private static void SetArrowVisibility(GameObject leftArrow, GameObject rightArrow, float value)
		{
			if (leftArrow != null)
				leftArrow.SetActive(value > 0.001f);
			if (rightArrow != null)
				rightArrow.SetActive(value < 0.999f);
		}

		private static void SetArrowVisibility(GameObject leftArrow, GameObject rightArrow, bool value)
		{
			if (leftArrow != null)
				leftArrow.SetActive(value);
			if (rightArrow != null)
				rightArrow.SetActive(!value);
		}

		private static void SetArrowVisibility(GameObject leftArrow, GameObject rightArrow, int index, int count)
		{
			if (leftArrow != null)
				leftArrow.SetActive(count > 1 && index > 0);
			if (rightArrow != null)
				rightArrow.SetActive(count > 1);
		}

		public override void ChooseMenuOption(BaseMenuOption baseMenuOption, bool shouldGoRight)
		{
			if (baseMenuOption == _toggleSfx)
				SelectToggleSfx(shouldGoRight);
			else if (baseMenuOption == _toggleBgm)
				SelectToggleBgm(shouldGoRight);
			else if (baseMenuOption == _toggleLanguage)
				SelectToggleLanguage(shouldGoRight);
			else if (baseMenuOption == _toggleClickToMove)
				SelectToggleClickToMove();
			else if (baseMenuOption == _toggleScreenShake)
				SelectToggleScreenShake(shouldGoRight);
			else if (baseMenuOption == _resolutions)
				SelectToggleResolution(shouldGoRight);
			else if (baseMenuOption == _toggleControllerRumble)
				SelectToggleRumble();
			else if (baseMenuOption == _toggleCheats)
				SelectToggleCheats();
			else if (baseMenuOption == _toggleFps)
				SelectToggleFps();
			else if (baseMenuOption == _exitOption)
				SelectClose();

			RefreshValues();
		}

		private void SelectToggleSfx(bool shouldGoRight)
		{
			var sfxVol = SaveUtil.GetSfxVolume();
			if (sfxVol > 0)
				MusicManager.Instance.PlayStinger(StingerEvent.UISelect);

			var newSfxVol = sfxVol + (shouldGoRight ? 0.1f : -0.1f);
			newSfxVol = Mathf.Clamp01(newSfxVol);
			MusicManager.Instance.SetSfxVolume(newSfxVol);

			if (sfxVol < 0.05f)
				MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
		}

		private void SelectToggleLanguage(bool shouldGoRight)
		{
			var languageCount = Localizer.GetLanguageCount();
			if (languageCount < 1)
				return;

			var languageIndex = Localizer.GetCurrentLanguageIndex();
			var nextLanguageIndex = languageIndex + (shouldGoRight ? 1 : -1);
			if (nextLanguageIndex >= languageCount)
				nextLanguageIndex = 0;
			if (nextLanguageIndex < 0)
				nextLanguageIndex = languageCount - 1;

			if (nextLanguageIndex == languageIndex)
				return;

			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			Localizer.SetLanguageByIndex(nextLanguageIndex);
		}

		private void SelectToggleBgm(bool shouldGoRight)
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);

			var bgmVol = SaveUtil.GetBgmVolume();

			var newBgmVol = bgmVol + (shouldGoRight ? 0.1f : -0.1f);
			newBgmVol = Mathf.Clamp01(newBgmVol);
			MusicManager.Instance.SetBgmVolume(newBgmVol);
		}

		private void SelectToggleClickToMove()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			SaveUtil.SetClickToMoveEnabled(!SaveUtil.GetClickToMoveEnabled());
		}

		private void SelectToggleScreenShake(bool shouldEnable)
		{
			if (SaveUtil.GetScreenShakeEnabled() == shouldEnable)
				return;

			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			SaveUtil.SetScreenShakeEnabled(shouldEnable);
		}

		private void SelectToggleResolution(bool shouldGoRight)
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);

			var resolutionWidth = SaveUtil.GetResolutionWidth();

			var index = -1;

			var resolutionWidths = Screen.resolutions.Select(r => r.width).Distinct().ToList();

			for (var i = 0; i < resolutionWidths.Count; i++)
			{
				if (resolutionWidths[i] == resolutionWidth)
				{
					index = i;
					break;
				}
			}

			index += shouldGoRight ? 1 : -1;
			if (index >= resolutionWidths.Count)
				index = 0;
			if (index < 0)
				index = resolutionWidths.Count - 1;

			SaveUtil.SetResolution(resolutionWidths[index]);
		}

		private void SelectToggleRumble()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			ControllerHelper.Instance.ToggleRumbleEnabled(!ControllerHelper.Instance.IsRumbleEnabled);
		}

		private void SelectToggleCheats()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			var shouldTurnOnCheats = !CheatManager.Instance.IsEnabled;
			CheatManager.Instance.SetCheatsEnabled(shouldTurnOnCheats);

			_toggleFps.gameObject.SetActive(shouldTurnOnCheats);
		}

		private void SelectToggleFps()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			CheatManager.Instance.ToggleDebugUI();
		}

		private void SelectClose()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UICancel);
			PopupManager.Instance.ClosePopup();
		}
	}
}
