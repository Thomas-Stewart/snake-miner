using System;
using System.Collections;
using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.Localization;
using SSG_Core.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SSG_Core.Scripts.Menu
{
	public class BaseMenuOption : MonoBehaviour, ITooltippable
	{
		[SerializeField] protected TextMeshProUGUI _text;
		[SerializeField] protected TextMeshProUGUI _valueText;
		[SerializeField] protected GameObject _highlightedObj;
		[SerializeField] protected ButtonWithAction _button;
		[SerializeField] protected bool _transparentWhenUnhighlighted;
		[SerializeField] private Image _backgroundImage;
		[SerializeField] private float _highlightedObjScale = 1.1f;
		[SerializeField] private float _scaleDuration = 0.08f;

		[Header("Tooltip")]
		[SerializeField] protected Tooltip _tooltipPrefab;
		[SerializeField] protected Transform _tooltipParent;
		[SerializeField] protected string _tooltipLocKey;
		[SerializeField] protected Vector2 _tooltipOffset;

		private Tooltip _tooltipInstance;
		private Vector3 _highlightedObjBaseScale = Vector3.one;
		private Vector3 _bgImageBaseScale = Vector3.one;
		private TextMeshProUGUI _labelTextToScale;
		private Vector3 _labelTextBaseScale = Vector3.one;
		private Vector3 _valueTextBaseScale = Vector3.one;
		private bool _shouldScaleValueText;
		private float _currentScaleMultiplier = 1f;
		private bool _hasBaseScales;
		private Coroutine _scaleRoutine;

		public event Action<BaseMenuOption, bool> OnClicked; // bool = shouldGoRight
		public event Action<BaseMenuOption> OnHighlighted;

		// so parent can provide text color
		public event Action<BaseMenuOption> RequestHighlight;

		public ButtonWithAction Button => _button;
		public RectTransform ValueTextRectTransform => _valueText != null ? _valueText.rectTransform : null;

		protected virtual void OnEnable()
		{
			if (_button != null)
			{
				_button.OnClicked += HandleClicked;
			}
		}

		protected virtual void OnDisable()
		{
			if (_button != null)
			{
				_button.OnClicked -= HandleClicked;
			}
		}

		protected virtual void Awake()
		{
			CacheBaseScales();
		}

		public void HandleClicked(BaseButton baseButton)
		{
			OnClicked?.Invoke(this, true);
		}

		public void HandleDirectionalClick(bool shouldGoRight)
		{
			OnClicked?.Invoke(this, shouldGoRight);
		}

		public void SetMainButtonClickable(bool isClickable)
		{
			if (_button != null && _button.Button != null)
				_button.Button.interactable = isClickable;
		}

		public virtual void SetHighlighted(bool isHighlighted, Color color)
		{
			CacheBaseScales();
			if (_text)
			{
				color.a = _transparentWhenUnhighlighted && !isHighlighted ? 0.5f : 1f;
				_text.color = color;
			}

			if (_highlightedObj)
			{
				_highlightedObj.SetActive(isHighlighted);
				if (!isHighlighted)
				{
					_highlightedObj.transform.localScale = _highlightedObjBaseScale;
				}
			}

			if (_scaleRoutine != null)
				StopCoroutine(_scaleRoutine);

			if (isActiveAndEnabled)
				_scaleRoutine = StartCoroutine(ScaleRoutine(isHighlighted));
			else
				ApplyScale(isHighlighted);

			if (isHighlighted)
				RaiseHighlightEvent();
		}

		private void CacheBaseScales()
		{
			if (_hasBaseScales)
				return;

			if (_highlightedObj)
				_highlightedObjBaseScale = _highlightedObj.transform.localScale;
			if (_backgroundImage)
				_bgImageBaseScale = _backgroundImage.transform.localScale;
			_labelTextToScale = ShouldScaleText(_text) ? _text : GetFallbackTextForScaling();
			if (_labelTextToScale)
				_labelTextBaseScale = _labelTextToScale.transform.localScale;
			_shouldScaleValueText = _valueText != null
			                        && _valueText != _labelTextToScale
			                        && ShouldScaleText(_valueText);
			if (_shouldScaleValueText)
				_valueTextBaseScale = _valueText.transform.localScale;
			_hasBaseScales = true;
		}

		private void ApplyScale(bool isHighlighted)
		{
			ApplyScaleMultiplier(isHighlighted ? _highlightedObjScale : 1f, isHighlighted);
		}

		private void ApplyScaleMultiplier(float scale, bool shouldScaleHighlightObj)
		{
			_currentScaleMultiplier = scale;
			if (_backgroundImage)
				_backgroundImage.transform.localScale = _bgImageBaseScale * scale;
			if (shouldScaleHighlightObj && _highlightedObj)
				_highlightedObj.transform.localScale = _highlightedObjBaseScale * scale;
			ApplyTextScale(scale);
		}

		private IEnumerator ScaleRoutine(bool isHighlighted)
		{
			var startScaleMultiplier = _currentScaleMultiplier;
			var targetTextScaleMultiplier = isHighlighted ? _highlightedObjScale : 1f;

			var elapsed = 0f;
			while (elapsed < _scaleDuration)
			{
				elapsed += Time.unscaledDeltaTime;
				var t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, _scaleDuration));
				ApplyScaleMultiplier(Mathf.Lerp(startScaleMultiplier, targetTextScaleMultiplier, t), isHighlighted);
				yield return null;
			}

			ApplyScaleMultiplier(targetTextScaleMultiplier, isHighlighted);
			_scaleRoutine = null;
		}

		private void ApplyTextScale(float scaleMultiplier)
		{
			if (_labelTextToScale)
				_labelTextToScale.transform.localScale = _labelTextBaseScale * scaleMultiplier;
			if (_shouldScaleValueText)
				_valueText.transform.localScale = _valueTextBaseScale * scaleMultiplier;
		}

		private TextMeshProUGUI GetFallbackTextForScaling()
		{
			var childTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
			foreach (var childText in childTexts)
			{
				if (childText != _valueText && ShouldScaleText(childText))
					return childText;
			}

			return null;
		}

		private bool ShouldScaleText(TextMeshProUGUI text)
		{
			if (text == null)
				return false;
			if (_highlightedObj != null && text.transform.IsChildOf(_highlightedObj.transform))
				return false;
			if (_backgroundImage != null && text.transform.IsChildOf(_backgroundImage.transform))
				return false;

			return true;
		}

		protected void RaiseHighlightEvent()
		{
			OnHighlighted?.Invoke(this);
		}

		public void SetValue(bool value)
		{
			var locKey = value ? "On" : "Off";
			SetValue(Localizer.GetText(locKey));
		}

		public void SetValue(float value, bool isPercent)
		{
			var text = string.Format(Localizer.GetText("ui_percent_format"), (value * 100f).ToString("0"));
			SetValue(text);
		}

		public void SetValue(int value)
		{
			SetValue(value.ToString());
		}

		protected virtual void RefreshUI()
		{
//todo: was there anything here?
		}

		public void SetValue(string value)
		{
			_valueText.text = value;
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UINavigate);
			ShowTooltip();
			RequestHighlight?.Invoke(this);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			HideTooltip();
			OnHighlighted?.Invoke(null);
		}

		public void ShowTooltip()
		{
			if (_tooltipPrefab && _tooltipLocKey != string.Empty)
			{
				_tooltipInstance = Instantiate(_tooltipPrefab, _tooltipParent);
				_tooltipInstance.Open(_tooltipLocKey, _tooltipOffset);
			}
		}

		public void HideTooltip()
		{
			if (_tooltipInstance != null)
				_tooltipInstance.CloseAndDestroy();
		}

		protected virtual void SetText(string text)
		{
			_text.text = text;
		}

		public void ResetEventSubscriptions()
		{
			OnHighlighted = null;
			OnClicked = null;
			RequestHighlight = null;
		}
	}
}
