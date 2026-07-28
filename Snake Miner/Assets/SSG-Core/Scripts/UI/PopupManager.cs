using System.Collections;
using System.Collections.Generic;
using SSG_Core.Scripts.Input;
using SSG_Core.Scripts.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SSG_Core.Scripts.UI
{
	public class PopupManager : MonoBehaviour
	{
		[SerializeField] private Popup[] _popups;
		private readonly List<Popup> _runtimePopups = new();

		private Stack<Popup> _popupStack = new Stack<Popup>();

		public bool AreAnyPopupsShowing
		{
			get
			{
				if (_popups != null)
				{
					for (var i = 0; i < _popups.Length; i++)
					{
						var popup = _popups[i];
						if (popup != null && popup.IsOpen)
							return true;
					}
				}

				for (var i = 0; i < _runtimePopups.Count; i++)
				{
					var popup = _runtimePopups[i];
					if (popup != null && popup.IsOpen)
						return true;
				}

				return false;
			}
		}

		public bool IsPopupOpen(PopupType popupType)
		{
			foreach (var popup in _popupStack)
			{
				if (popup.PopupType == popupType)
					return true;
			}

			return false;
		}

		public static PopupManager Instance { get; private set; }
		private void Initialize()
		{
			if (Instance != null)
			{
				Destroy(Instance.gameObject);
			}

			Instance = this;
			if (Application.isPlaying)
				DontDestroyOnLoad(Instance);
		}

		private void Awake()
		{
			Initialize();
		}

		public Popup OpenPopup(PopupType popupType)
		{
			Debug.Log($"Opening popup {popupType}");

			var hasPreviousPopup = _popupStack.TryPeek(out var prevPopup);
			if (hasPreviousPopup)
				prevPopup.Close_ManagerOnly();

			var popup = FindPopup(popupType);
			if (popup)
			{
				popup.gameObject.SetActive(true);
				popup.Open(!hasPreviousPopup);
				_popupStack.Push(popup);
				if (hasPreviousPopup)
					StartCoroutine(ShowBlockerAfterPopupClosed(popup, prevPopup));
			}

			CheckInputMode();
			return popup;
		}

		public void RegisterPopup(Popup popup)
		{
			if (popup == null || _runtimePopups.Contains(popup))
				return;

			_runtimePopups.Add(popup);
		}

		public void UnregisterPopup(Popup popup)
		{
			if (popup == null)
				return;

			_runtimePopups.Remove(popup);
		}

		public void ClosePopup()
		{
			StartCoroutine(ClosePopupRoutine());
		}

		private IEnumerator ClosePopupRoutine()
		{
			Popup closedPopup = null;
			if (_popupStack.TryPop(out var popup))
			{
				Debug.Log($"Closing popup {popup.PopupType}");
				closedPopup = popup;
				if (popup.gameObject.activeInHierarchy)
					popup.Close_ManagerOnly();
			}

			// wait for popup to close
			yield return null;
			yield return null;

			if (_popupStack.TryPeek(out var nextPopup))
			{
				nextPopup.gameObject.SetActive(true);
				nextPopup.Open(false);
				StartCoroutine(ShowBlockerAfterPopupClosed(nextPopup, closedPopup));
			}

			CheckInputMode();
		}

		private IEnumerator ShowBlockerAfterPopupClosed(Popup popup, Popup previousPopup)
		{
			yield return new WaitUntil(() => previousPopup == null || !previousPopup.IsOpen);

			if (popup != null && popup.IsOpen)
				popup.SetBlockerVisible(true);
		}

		public void CloseAllPopups()
		{
			while (_popupStack.Count > 0)
			{
				ClosePopup();
			}
		}

		public PopupType GetOpenPopupType()
		{
			if (_popupStack.TryPeek(out var popup))
				return popup.PopupType;

			return PopupType.NONE;
		}

		private void CheckInputMode()
		{
			var actionMap = _popupStack.Count > 0 ? InputActionMapHelper.UI : InputActionMapHelper.Player;

			if (SceneManager.GetActiveScene().name == SceneNames.Title)
				actionMap = InputActionMapHelper.UI;

			InputActionMapHelper.ChangeAllInputActionMap(actionMap);
		}

		private Popup FindPopup(PopupType popupType)
		{
			if (_popups != null)
			{
				for (var i = 0; i < _popups.Length; i++)
				{
					var popup = _popups[i];
					if (popup != null && popup.PopupType == popupType)
						return popup;
				}
			}

			for (var i = 0; i < _runtimePopups.Count; i++)
			{
				var popup = _runtimePopups[i];
				if (popup != null && popup.PopupType == popupType)
					return popup;
			}

			return null;
		}
	}


	public enum PopupType
	{
		NONE,
		PAUSE,
		OPTIONS,
		ACHIEVEMENTS,
		SAVE,
		LOAD,
		CONFIRMDELETE,
		OJBECTIVE_REWARD,
		CONFIRM_TRASH,
		TOOL_CONTEXT,
		CONTROLS,
		PLAY_LEVEL,
		TEXT,
		CHOOSE_TROOP,
		STORE,
		WISHLIST,
		END_ISLAND_STATS,
		END_ISLAND_MAP,
	}
}
