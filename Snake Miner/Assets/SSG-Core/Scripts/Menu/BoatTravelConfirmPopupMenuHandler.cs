using System;
using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.Localization;
using SSG_Core.Scripts.UI;
using TMPro;
using UnityEngine;

namespace SSG_Core.Scripts.Menu
{
	public class BoatTravelConfirmPopupMenuHandler : MenuOptionHandler
	{
		private const string DefaultTitleLocId = "ui_popup_title_boat_travel_confirm";

		private static string _pendingTitle;
		private static Action _pendingOnYes;
		private static Action _pendingOnNo;

		[SerializeField] private TMP_Text _titleText;
		private bool _resolved;

		public static void Configure(string title, Action onYes, Action onNo)
		{
			_pendingTitle = title;
			_pendingOnYes = onYes;
			_pendingOnNo = onNo;
		}

		public static void ClearPending()
		{
			_pendingTitle = null;
			_pendingOnYes = null;
			_pendingOnNo = null;
		}

		private void OnEnable()
		{
			_resolved = false;
			if (_titleText != null)
				_titleText.text = string.IsNullOrWhiteSpace(_pendingTitle) ? Localizer.GetText(DefaultTitleLocId) : _pendingTitle;
		}

		private void OnDisable()
		{
			if (_resolved)
				return;

			_resolved = true;
			var onNo = _pendingOnNo;
			ClearPending();
			onNo?.Invoke();
		}

		public void Confirm()
		{
			if (_resolved)
				return;

			_resolved = true;
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			var onYes = _pendingOnYes;
			ClearPending();
			PopupManager.Instance.CloseAllPopups();
			onYes?.Invoke();
		}

		public void Deny()
		{
			if (_resolved)
				return;

			_resolved = true;
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			var onNo = _pendingOnNo;
			ClearPending();
			PopupManager.Instance.CloseAllPopups();
			onNo?.Invoke();
		}
	}
}
