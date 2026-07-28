using SSG_Core.Scripts.Util.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class GameplayInputReminderPanelView : MonoBehaviour
{
	[SerializeField] private ControlBindingDisplay _bindingDisplay;
	[SerializeField] private TextMeshProUGUI _promptText;
	[SerializeField] private RectTransform _bindingRect;
	[SerializeField] private RectTransform _promptTextRect;
	[SerializeField] private float _spacing = 18f;
	[SerializeField] private float _horizontalPadding = 24f;
	private string _configuredPromptText;
	private InputActionReference _configuredActionReference;
	private float _configuredPanelWidth = float.NaN;
	private float _configuredIconWidth = float.NaN;

	public void Configure(string promptText, InputActionReference actionReference)
	{
		var normalizedPromptText = promptText ?? string.Empty;
		if (_configuredPromptText != normalizedPromptText)
		{
			_configuredPromptText = normalizedPromptText;
			if (_promptText != null)
				_promptText.text = normalizedPromptText;
			_configuredPanelWidth = float.NaN;
		}

		if (_bindingDisplay != null && _configuredActionReference != actionReference)
		{
			_configuredActionReference = actionReference;
			_bindingDisplay.BindAction(actionReference);
		}

		CenterContents();
	}

	private void Awake()
	{
		if (_bindingRect == null && _bindingDisplay != null)
			_bindingRect = _bindingDisplay.transform as RectTransform;

		if (_promptTextRect == null && _promptText != null)
			_promptTextRect = _promptText.transform as RectTransform;
	}

	private void CenterContents()
	{
		var panelRect = transform as RectTransform;
		if (panelRect == null || _bindingRect == null || _promptTextRect == null || _promptText == null)
			return;

		var panelWidth = panelRect.rect.width;
		if (panelWidth <= 0f)
			return;

		var iconWidth = _bindingRect.rect.width > 0f ? _bindingRect.rect.width : _bindingRect.sizeDelta.x;
		if (Mathf.Approximately(_configuredPanelWidth, panelWidth) &&
		    Mathf.Approximately(_configuredIconWidth, iconWidth))
		{
			return;
		}

		_configuredPanelWidth = panelWidth;
		_configuredIconWidth = iconWidth;
		var maxTextWidth = Mathf.Max(0f, panelWidth - _horizontalPadding * 2f - iconWidth - _spacing);
		var preferredTextWidth = _promptText.GetPreferredValues(_promptText.text, maxTextWidth, 0f).x;
		var textWidth = Mathf.Min(maxTextWidth, preferredTextWidth);
		var contentWidth = iconWidth + _spacing + textWidth;
		var leftEdge = -contentWidth * 0.5f;

		_bindingRect.anchorMin = new Vector2(0.5f, 0.5f);
		_bindingRect.anchorMax = new Vector2(0.5f, 0.5f);
		_bindingRect.pivot = new Vector2(0.5f, 0.5f);
		_bindingRect.anchoredPosition = new Vector2(leftEdge + iconWidth * 0.5f, 0f);

		_promptTextRect.anchorMin = new Vector2(0.5f, 0.5f);
		_promptTextRect.anchorMax = new Vector2(0.5f, 0.5f);
		_promptTextRect.pivot = new Vector2(0f, 0.5f);
		_promptTextRect.sizeDelta = new Vector2(textWidth, _promptTextRect.sizeDelta.y);
		_promptTextRect.anchoredPosition = new Vector2(leftEdge + iconWidth + _spacing, 0f);
	}
}
