using System.Collections.Specialized;
using Sirenix.OdinInspector;
using SSG_Core.Scripts.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SSG_Core.Scripts.Util.Platform
{
	public class ControlBindingDisplay : MonoBehaviour
	{
		[HideIf(nameof(_isSpriteRenderer))]
		[SerializeField] private Image _symbolImage;
		[ShowIf(nameof(_isSpriteRenderer))]
		[SerializeField] private SpriteRenderer _symbolSprite;
		[SerializeField] private bool _isSpriteRenderer;
		[SerializeField] private InputActionReference _actionReference;
		[SerializeField] private bool _forceDeviceDisplay;
		[ShowIf(nameof(_forceDeviceDisplay))]
		[SerializeField] private ControllerHelper.ControlType _forcedControlType;
		[SerializeField] private bool _shouldRememberOwnerPlayerId;
		[SerializeField] private bool _shouldOnlyDisplayWithController;

		private int _cachedPlayerId = -1;

		private void Start()
		{
			//todo:
			// GameManager.Instance.MultiplayerManager.ActivePlayerTankInputs.CollectionChanged += HandleActivePlayersChanged;
			Refresh();

			_cachedValueIsController = !ControllerHelper.Instance.IsMostRecentControlTypeAController;

			InputSystem.onDeviceChange += HandleDeviceChange;
		}

		private void OnDestroy()
		{
			InputSystem.onDeviceChange -= HandleDeviceChange;
		}

		private void HandleDeviceChange(InputDevice arg1, InputDeviceChange arg2)
		{
			// Refresh();
		}

		private bool _cachedValueIsController;
		private void Update()
		{
			if (ControllerHelper.Instance == null)
				return;

			var isController = ControllerHelper.Instance.IsMostRecentControlTypeAController;
			if (_cachedValueIsController == isController)
				return;

			_cachedValueIsController = isController;
			if (_shouldOnlyDisplayWithController)
				SetSymbolActive(isController);

			Refresh();
		}

		private void HandleActivePlayersChanged(
			object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
		{
			Refresh();
		}

		[Button]
		public void Refresh(int playerId = -1)
		{
			if (_actionReference == null)
				return;

			if (_shouldRememberOwnerPlayerId && playerId >= 0)
				_cachedPlayerId = playerId;

			if (_cachedPlayerId >= 0)
				playerId = _cachedPlayerId;

			var forcedControlType = _forceDeviceDisplay ? _forcedControlType : (ControllerHelper.ControlType?) null;
			var sprite = ControlBindingDatabase.Instance.GetControlBindingSymbol(_actionReference, forcedControlType, playerId);

			if (_isSpriteRenderer)
			{
				if (_symbolSprite)
					_symbolSprite.sprite = sprite;
			}
			else
			{
				if (_symbolImage)
					_symbolImage.sprite = sprite;
			}
		}

		public void BindAction(InputActionReference actionReference)
		{
			_actionReference = actionReference;
			Refresh();
		}

		public void ConfigureImageTarget(Image symbolImage)
		{
			_symbolImage = symbolImage;
			_symbolSprite = null;
			_isSpriteRenderer = false;
		}

		private void SetSymbolActive(bool isActive)
		{
			if (_isSpriteRenderer)
			{
				if (_symbolSprite != null)
					_symbolSprite.gameObject.SetActive(isActive);
			}
			else if (_symbolImage != null)
			{
				_symbolImage.gameObject.SetActive(isActive);
			}
		}
	}
}
