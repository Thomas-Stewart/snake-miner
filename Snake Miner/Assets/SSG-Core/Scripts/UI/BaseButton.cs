using System.Collections;
using Sirenix.OdinInspector;
using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.Input;
using SSG_Core.Scripts.Localization;
using SSG_Core.Scripts.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SSG_Core.Scripts.UI
{
	public abstract class BaseButton : MonoBehaviour, ITooltippable
	{
		[SerializeField] private Button _button;
		[SerializeField] private bool _trackToggleState;
		[ShowIf(nameof(_trackToggleState)), SerializeField] private GameObject _toggleOnObject;
		[ShowIf(nameof(_trackToggleState)), SerializeField] private bool _shouldStartOn;
		[ShowIf(nameof(_trackToggleState)), SerializeField] private bool _shouldDisableDeselect;
		[SerializeField] private Image _highlightImage;
		[SerializeField] private Image _bgImage;
		[SerializeField] private Image _iconBgImage;
		[SerializeField] private Image _iconBorderImage;
		[SerializeField] protected Image _iconImage;
		[SerializeField] protected TMP_Text _text;

		[Header("Tooltip")]
		[SerializeField] protected Tooltip _tooltipPrefab;
		[SerializeField] protected Transform _tooltipParent;
		[ValueDropdown(nameof(GetAllLocIds))]
		[SerializeField] protected string _tooltipLocKey;
		[SerializeField] protected Vector2 _tooltipOffset;

		private Tooltip _tooltipInstance;

		private bool _isToggledOnBacking;
		public bool IsToggledOn => _isToggledOnBacking;
		public Button Button => _button;

		protected virtual void OnEnable()
		{
			if (_button) _button.onClick.AddListener(OnButtonClicked);
		}

		protected virtual void OnDisable()
		{
			if (_button) _button.onClick.RemoveListener(OnButtonClicked);
		}

		protected virtual void Start()
		{
			if (_shouldStartOn)
				OnButtonClicked();
		}

		public void SetToggleVis(bool isToggled)
		{
			_isToggledOnBacking = isToggled;
			Highlight(isToggled);
			Refresh();
		}

		private void OnButtonClicked()
		{
			if (_shouldDisableDeselect && _isToggledOnBacking) return;
			if (!gameObject.activeInHierarchy) return;
			if (ControllerHelper.Instance.IsMostRecentControlTypeAController && !InputManager.InputActions.UI.enabled) return;

			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			if (_trackToggleState)
				_isToggledOnBacking = !_isToggledOnBacking;
			Refresh();
			InvokeEvent();
		}

		private void Refresh()
		{
			if (_trackToggleState && _toggleOnObject)
				_toggleOnObject.SetActive(_isToggledOnBacking);

			if (_isToggledOnBacking)
				HideTooltip();
			
			Highlight(_isToggledOnBacking);
		}

		public void Highlight(bool isHighlighted)
		{
			if (_toggleOnObject)
				_toggleOnObject.SetActive(isHighlighted);
			if (_highlightImage)
				_highlightImage.gameObject.SetActive(isHighlighted);
		}

		protected virtual void InvokeEvent()
		{ }

		public virtual void OnPointerEnter(PointerEventData eventData)
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UINavigate);
			Highlight(true);
			ShowTooltip();
		}

		public virtual void OnPointerExit(PointerEventData eventData)
		{
			HideTooltip();
			Refresh();
		}

		public void ShowTooltip()
		{
			if (_isToggledOnBacking) return;

			if (_tooltipPrefab && _tooltipLocKey != string.Empty)
			{
				_tooltipInstance = Instantiate(_tooltipPrefab, _tooltipParent);
				_tooltipInstance.Open(_tooltipLocKey, _tooltipOffset);
			}
		}

		public void HideTooltip()
		{
			if (_tooltipInstance)
				_tooltipInstance.CloseAndDestroy();
		}

		public static IEnumerable GetAllLocIds()
		{
			return Localizer.GetAllLocIds();
		}
	}
}