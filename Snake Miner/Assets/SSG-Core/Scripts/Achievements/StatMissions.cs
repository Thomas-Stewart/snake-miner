namespace SSG_Core.Scripts.Achievements
{
	public static class StatMissions
	{
		public static readonly StatMissionData[] MissionDatas =
		{
			new StatMissionData
			{
				StatKey = AchievementKeys.Stats.EXAMPLE_STAT,
				MissionKey = AchievementKeys.Missions.EXAMPLE_MISSION,
				ProgressionValue = 1
			},
			new StatMissionData
			{
				StatKey = AchievementKeys.Stats.FISH_CAUGHT,
				MissionKey = AchievementKeys.Missions.FIRST_FISH_MISSION,
				ProgressionValue = 1
			}
		};

		public struct StatMissionData
		{
			public string StatKey;
			public string MissionKey;
			public int ProgressionValue;
		}
	}
}
