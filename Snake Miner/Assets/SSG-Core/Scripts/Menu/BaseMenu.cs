using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.Input;
using SSG_Core.Scripts.UI;
using SSG_Core.Scripts.Util;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SSG_Core.Scripts.Menu
{
	public class BaseMenu : MonoBehaviour
	{
		[SerializeField] private MenuOptionHandler _menuOptionHandler;
		[SerializeField] private bool _isNavigableWithoutPlayers = true;
		[SerializeField] protected Canvas _canvas;
		[SerializeField] private bool _shouldStartActive;
		[SerializeField] private bool _shouldFreezeTimeWhenEnabled;
		[SerializeField] private Color _unselectedTextColor = Color.gray;
		[SerializeField] private Color _selectedTextColor = Color.white;
		[SerializeField] private bool _isHorizontalMenu;
		[SerializeField] private bool _shouldAllowNavSelect = true;
		[SerializeField] private bool _shouldFocusOnFirstOption = true;

		[SerializeField] private bool _isGridMenu;
		[SerializeField, ShowIf(nameof(_isGridMenu))] private bool _isGridNonUniform;
		[SerializeField, ShowIf(nameof(ShouldShowNumInRow))] private int _numInRow;
		[SerializeField, ShowIf(nameof(_isGridNonUniform))] private int[] _specificGridRows;
		private int[] _rowStartIndices;

		private bool ShouldShowNumInRow() {return _isGridMenu && !_isGridNonUniform;}

		protected List<BaseMenuOption> _menuOptions = new();
		private int _menuOptionIndex;
		private Popup _parentPopup;
		private bool _isInputSubscribed;
		// private InputActions _inputActions;

		public event Action OnDisabled;
		public event Action OnOptionChosen;
		public bool ShouldStartActive => _shouldStartActive;
		public bool IsEnabled => IsCanvasActuallyVisible();

		private bool IsCanvasActuallyVisible()
		{
			if (_canvas == null)
				return false;

			Transform current = _canvas.transform;

			while (current != null)
			{
				// Check if GameObject is active
				if (!current.gameObject.activeSelf)
					return false;

				// Check if Canvas component (if any) is enabled
				var parentCanvas = current.GetComponent<Canvas>();
				if (parentCanvas != null && !parentCanvas.enabled)
					return false;

				current = current.parent;
			}

			return true;
		}	

		protected virtual void Awake()
		{
			_parentPopup = _canvas != null
				? _canvas.GetComponentInParent<Popup>()
				: GetComponentInParent<Popup>();

			var menuOptions = _canvas.GetComponentsInChildren<BaseMenuOption>(true).ToList();
			
			foreach (var menuOption in menuOptions)
			{
				AddMenuOption(menuOption);
			}

			if (_menuOptions.Count < 1)
				Debug.LogError("No menu options found!");
			
			if (_menuOptionHandler == null)
				Debug.LogError("No menu handler setup! " + transform.GetFullHierarchyPath());
			
			_rowStartIndices = new int[_specificGridRows.Length];
			var runningIndex = 0;
			for (var i = 0; i < _specificGridRows.Length; i++)
			{
				_rowStartIndices[i] = runningIndex;
				runningIndex += _specificGridRows[i];
			}
		}

		protected void ClearMenuOptions()
		{
			foreach (var menuOption in _menuOptions)
			{
				menuOption.ResetEventSubscriptions();
			}
			_menuOptions.Clear();
		}

		protected void AddMenuOption(BaseMenuOption menuOption)
		{
			if (_menuOptions.Contains(menuOption)) return;

			menuOption.OnClicked += ChooseMenuOption;
			menuOption.OnHighlighted += HandleOptionHighlighted;
			menuOption.RequestHighlight += HandleRequestHighlight;
			_menuOptions.Add(menuOption);
		}

		protected void AddMenuOptionRange(IEnumerable<BaseMenuOption> menuOptions)
		{
			foreach (var menuOption in menuOptions)
			{
				AddMenuOption(menuOption);
			}
		}

		protected virtual void HandleOptionHighlighted(BaseMenuOption highlightedOption)
		{
			for (var i = 0; i < _menuOptions.Count; i++)
			{
				var menuOption = _menuOptions[i];
				if (menuOption == highlightedOption) continue;
				menuOption.SetHighlighted(false, _unselectedTextColor);
			}
		}

		private void HandleRequestHighlight(BaseMenuOption menuOption)
		{
			menuOption.SetHighlighted(true, _selectedTextColor);
		}

		protected virtual void Start()
		{
			_menuOptionIndex = _shouldFocusOnFirstOption ? 0 : -1;
			UpdateUI();
		}

		protected virtual void OnEnable()
		{
			_menuOptionIndex = _shouldFocusOnFirstOption ? 0 : -1;
			if (!_isNavigableWithoutPlayers)
			{
				// if (GameManager.Instance)
				// {
				// 	foreach (var tankPlayerInput in GameManager.Instance.MultiplayerManager.ActivePlayerTankInputs)
				// 	{
				// 		tankPlayerInput.OnUISelect += SelectMenuOption;
				// 		tankPlayerInput.OnUICancel += Cancel;
				// 		tankPlayerInput.OnUIMove += HandleNavigateMenuInput;
				// 		tankPlayerInput.RequestPause += HandlePausePressed;
				// 	}
				// }
			}
			else
			{
				// _inputActions.Enable();
			}
			
			if (_isNavigableWithoutPlayers && !_isInputSubscribed)
			{
				var inputActions = InputManager.InputActions;
				inputActions.UI.UISelect.performed += SelectMenuOption;
				inputActions.UI.UICancel.performed += Cancel;
				inputActions.UI.UIMove.performed += HandleNavigateMenuInput;
				inputActions.Enable();
				_isInputSubscribed = true;
			}

			if (_shouldFreezeTimeWhenEnabled)
				Time.timeScale = 0f;

			UpdateUI();
		}

		private void OnDisable()
		{
			if (!_isNavigableWithoutPlayers)
			{
				// if (GameManager.Instance)
				// {
					// foreach (var tankPlayerInput in GameManager.Instance.MultiplayerManager.ActivePlayerTankInputs)
					// {
					// 	tankPlayerInput.OnUISelect -= SelectMenuOption;
					// 	tankPlayerInput.OnUICancel -= Cancel;
					// 	tankPlayerInput.OnUIMove -= HandleNavigateMenuInput;
					// 	tankPlayerInput.RequestPause -= HandlePausePressed;
					// }
				// }
			}
			else
			{
			}
			
			if (_isInputSubscribed)
			{
				var inputActions = InputManager.InputActions;
				inputActions.UI.UISelect.performed -= SelectMenuOption;
				inputActions.UI.UICancel.performed -= Cancel;
				inputActions.UI.UIMove.performed -= HandleNavigateMenuInput;
				_isInputSubscribed = false;
			}

			if (_shouldFreezeTimeWhenEnabled)
				Time.timeScale = 1f;

			OnDisabled?.Invoke();
		}

		protected void ChooseMenuOption(BaseMenuOption menuOption, bool shouldGoRight)
		{
			if (!CanReceiveMenuInput()) return;
			Debug.Log("selected menu option " + menuOption.name);
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			_menuOptionHandler.ChooseMenuOption(menuOption, shouldGoRight);
			OnOptionChosen?.Invoke();
		}

		private void HandleNavigateMenuInput(InputAction.CallbackContext ctx)
		{
			if (!CanReceiveMenuInput()) return;
			
			ControllerHelper.Instance.VibrateController(ctx.control.device.deviceId, ControllerHelper.VibrationType.VERY_SMALL);

			var movementVector = ctx.ReadValue<Vector2>();

			if (_isHorizontalMenu)
			{
				var tempVec = -movementVector;
				movementVector.x = tempVec.y;
				movementVector.y = tempVec.x;
			}
			var isHorizontalStronger = Mathf.Abs(movementVector.x) > Mathf.Abs(movementVector.y);
			var shouldNavigateRight = isHorizontalStronger ? movementVector.x > 0 : movementVector.y < 0;
			int traverseAmount = 0;

			if (!_isGridMenu)
			{
				traverseAmount= shouldNavigateRight ? 1 : -1;
			}
			else if (_isGridMenu && !_isGridNonUniform)
			{
				if (isHorizontalStronger)
				{
					traverseAmount= shouldNavigateRight ? 1 : -1;
				}
				else
				{
					if (shouldNavigateRight)
					{
						traverseAmount = _numInRow;
						if (_menuOptionIndex + traverseAmount >= _menuOptions.Count)
						{
							var currentColumn = _menuOptionIndex % _numInRow;
							traverseAmount = -_menuOptionIndex + currentColumn;
						}
					}
					else
					{
						traverseAmount = -_numInRow;
						if (_menuOptionIndex + traverseAmount < 0)
						{
							var currentColumn = _menuOptionIndex % _numInRow;
							var numInLastCol = _menuOptions.Count % _numInRow;
							traverseAmount = -_menuOptionIndex;
							if (numInLastCol > currentColumn)
								traverseAmount -= numInLastCol - currentColumn;
							else
								traverseAmount -= numInLastCol + _numInRow - currentColumn;
						}
					}
				}
			}
			else if (_isGridMenu && _isGridNonUniform)
			{
				if (isHorizontalStronger)
				{
					traverseAmount= shouldNavigateRight ? 1 : -1;
				}
				else
				{
					if (shouldNavigateRight) // Navigating down
					{
						for (var i = 0; i < _rowStartIndices.Length; i++)
						{
							if (_menuOptionIndex <= _rowStartIndices[i])
							{
								var startIndex = _rowStartIndices[i];
								traverseAmount = startIndex - _menuOptionIndex + 1;
								break;
							}
						}

						if (traverseAmount == 0)
						{
							traverseAmount = -_menuOptionIndex + 1;
							//HACK
							// make this specific fishing upgrade menu feel better by putting highlight over middle option
							traverseAmount += 1;
						}
					}
					else // Navigating up
					{
						for (var i = _rowStartIndices.Length - 1; i >= 0 ; i--)
						{
							var endIndex = _rowStartIndices[i] + _specificGridRows[i];
							var startIndex = _rowStartIndices[i];
							if (_menuOptionIndex >= endIndex)
							{
								var diff = endIndex - _menuOptionIndex - 1;
								if (diff + _menuOptionIndex <= startIndex)
									traverseAmount = diff;
								break;
							}
						}
						
						if (traverseAmount == 0)
						{
							var lastIndex = _rowStartIndices[^1] + _specificGridRows[^1];
							traverseAmount = lastIndex - _menuOptionIndex;
						}
						
						//HACK
						// make this specific fishing upgrade menu feel better by putting highlight over middle option
						if (_menuOptionIndex + traverseAmount <= 3)
							traverseAmount -= 1;
					}
				}
			}

			
			if (isHorizontalStronger && _shouldAllowNavSelect)
			{
				var currentOption = _menuOptions[_menuOptionIndex];
				ChooseMenuOption(currentOption, shouldNavigateRight);
			}
			else
			{
				NavigateMenu(traverseAmount);
			}
		}

		protected virtual void NavigateMenu(int traverseAmount)
		{
			if (!CanReceiveMenuInput()) return;

			Debug.Log("traverseAmount = " + traverseAmount);
			
			MusicManager.Instance.PlayStinger(StingerEvent.UINavigate);

			_menuOptionIndex = (_menuOptionIndex + traverseAmount) % _menuOptions.Count;
			if (_menuOptionIndex < 0) 
			{
				_menuOptionIndex += _menuOptions.Count;
			}

			var currentOption = _menuOptions[_menuOptionIndex];
			if (currentOption != null
			    && !currentOption.gameObject.activeInHierarchy
			    && _menuOptions.Count > 0
			    && _menuOptions.Any(o => o.gameObject.activeInHierarchy))
			{
				NavigateMenu(traverseAmount > 0 ? 1 : -1);
			}

			Debug.Log("_menuOptionIndex = " + _menuOptionIndex);
			UpdateUI();
		}

		private void SelectMenuOption(InputAction.CallbackContext ctx)
		{
			if (!CanReceiveMenuInput() || _menuOptionIndex < 0 || _menuOptionIndex >= _menuOptions.Count)
				return;
			
			ControllerHelper.Instance.VibrateController(ctx.control.device.deviceId, ControllerHelper.VibrationType.VERY_SMALL);
			ChooseMenuOption(_menuOptions[_menuOptionIndex], true);
		}

		private void UpdateUI()
		{
			for (var i = 0; i < _menuOptions.Count; i++)
			{
				var menuOption = _menuOptions[i];
				var isSelected = i == _menuOptionIndex;
				menuOption.SetHighlighted(isSelected, isSelected ? _selectedTextColor : _unselectedTextColor);
			}
		}

		protected virtual void Cancel(InputAction.CallbackContext ctx)
		{
			if (!CanReceiveMenuInput()) return;
			
			ControllerHelper.Instance.VibrateController(ctx.control.device.deviceId, ControllerHelper.VibrationType.VERY_SMALL);
		}

		protected virtual void HandlePausePressed()
		{
			if (!CanReceiveMenuInput()) return;
		}

		private bool CanReceiveMenuInput()
		{
			if (!IsEnabled)
				return false;

			var popupManager = PopupManager.Instance;
			if (popupManager == null || !popupManager.AreAnyPopupsShowing)
				return true;

			return _parentPopup != null
			       && _parentPopup.IsOpen
			       && popupManager.GetOpenPopupType() == _parentPopup.PopupType;
		}

		private void OnDestroy()
		{
			if (_isInputSubscribed)
			{
				var inputActions = InputManager.InputActions;
				inputActions.UI.UISelect.performed -= SelectMenuOption;
				inputActions.UI.UICancel.performed -= Cancel;
				inputActions.UI.UIMove.performed -= HandleNavigateMenuInput;
				_isInputSubscribed = false;
			}

			// if (_isNavigableWithoutPlayers)
			// 	_inputActions.Dispose();
		}
	}
}
