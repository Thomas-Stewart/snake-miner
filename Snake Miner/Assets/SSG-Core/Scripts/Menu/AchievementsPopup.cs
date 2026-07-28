using System;
using SSG_Core.Scripts.Achievements;
using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.UI;
using SSG_Core.Scripts.Util;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SSG_Core.Scripts.Menu
{
	[Obsolete]
	public class AchievementsPopup : Popup
	{
		[SerializeField] private AchievementView _achievementViewPrefab;
		[SerializeField] private Transform _achievementViewParent1;
		[SerializeField] private Transform _achievementViewParent2;
		[SerializeField] private BaseMenuOption _clearValuesOption;
		[SerializeField] private BaseMenuOption _exitOption;

		protected void OnEnable()
		{
			RefreshValues();
		}

		private void RefreshValues()
		{
			_achievementViewParent1.DestroyAllChildren();
			_achievementViewParent2.DestroyAllChildren();
			foreach (var statKey in AchievementKeys.AllStats)
			{
				var view = Instantiate(_achievementViewPrefab, _achievementViewParent1);
				view.Initialize(statKey, true);
			}
			foreach (var statKey in AchievementKeys.AllMissions)
			{
				var view = Instantiate(_achievementViewPrefab, _achievementViewParent2);
				view.Initialize(statKey, false);
			}
		}

		protected void ChooseMenuOption(BaseMenuOption menuOption, bool shouldGoRight)
		{
			if (!IsOpen) return;
			if (_animation.isPlaying) return;
			
			if (menuOption == _clearValuesOption)
				SelectClearValuesOption();
			else if (menuOption == _exitOption)
				SelectClose();

			RefreshValues();
		}

		private void SelectClearValuesOption()
		{
			StatsManager.DebugClear();
			RefreshValues();
		}

		protected void Cancel(InputAction.CallbackContext ctx)
		{
			SelectClose();
		}

		protected void HandlePausePressed()
		{
			SelectClose();
		}

		private void SelectClose()
		{
			if (!IsOpen) return;

			MusicManager.Instance.PlayStinger(StingerEvent.UICancel);
			PopupManager.Instance.ClosePopup();
		}
	}
}
