namespace SSG_Core.Scripts.PlayStationUtil
{
	public static class PlayStationTrophyHelper
	{
		public static readonly TrophyData[] TrophyDatas =
		{
			new TrophyData
			{
				MissionKey = SSG_Core.Scripts.Achievements.AchievementKeys.Missions.EXAMPLE_MISSION,
				TrophyId = 0
			}
		};

		public struct TrophyData
		{
			public string MissionKey;
			public int TrophyId;
		}

		public static bool TryGetTrophyData(string missionKey, out TrophyData trophyData)
		{
			for (var i = 0; i < TrophyDatas.Length; i++)
			{
				if (TrophyDatas[i].MissionKey != missionKey)
					continue;

				trophyData = TrophyDatas[i];
				return true;
			}

			trophyData = default;
			return false;
		}
	}
}
