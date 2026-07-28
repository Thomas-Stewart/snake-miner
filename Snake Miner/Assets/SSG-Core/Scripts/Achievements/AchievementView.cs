using System.Linq;
using SSG_Core.Scripts.Localization;
using TMPro;
using UnityEngine;

namespace SSG_Core.Scripts.Achievements
{
	public class AchievementView : MonoBehaviour
	{
		[SerializeField] private TMP_Text _titleText;
		[SerializeField] private TMP_Text _valueText;

		public void Initialize(string key, bool isStat)
		{
			_titleText.text = key;

			if (isStat)
			{
				var data = StatMissions.MissionDatas.FirstOrDefault(d => d.StatKey == key);
				var progress = PlayerPrefs.GetString(key);
				if (string.IsNullOrEmpty(progress))
					progress = PlayerPrefs.GetInt(key).ToString();
				_valueText.text = string.Format(Localizer.GetText("ui_progress_format"), progress, data.ProgressionValue);
			}
			else
			{
				_valueText.text = Localizer.GetText(StatsManager.HasMissionBeenCompleted(key) ? "ui_completed" : "ui_incomplete");
			}
		}
	}
}
