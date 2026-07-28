namespace SSG_Core.Scripts.Achievements
{
	public static class AchievementKeys
	{
		public class Stats
		{
			public const string EXAMPLE_STAT = "example_stat";
			public const string FISH_CAUGHT = "fish_caught";
			public const string COINS_PICKED_UP = "coins_picked_up";
			public const string PLAYER_STEPS_TAKEN = "player_steps_taken";
			public const string PLAYER_ROD_CASTS = "player_rod_casts";

			public static readonly string[] AllStats =
			{
				EXAMPLE_STAT,
				FISH_CAUGHT,
				COINS_PICKED_UP,
				PLAYER_STEPS_TAKEN,
				PLAYER_ROD_CASTS
			};
		}

		public class Missions
		{
			public const string EXAMPLE_MISSION = "example_mission";
			public const string FIRST_FISH_MISSION = "first_fish_mission";

			public static readonly string[] AllMissions =
			{
				EXAMPLE_MISSION,
				FIRST_FISH_MISSION
			};
		}

		public static readonly string[] AllStats = Stats.AllStats;
		public static readonly string[] AllMissions = Missions.AllMissions;
	}
}
