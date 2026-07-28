using SSG_Core.Scripts.SteamUtil;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace SSG_Core.Scripts.Util.Platform
{
	public static class PlatformUtil
	{
		public static string GetPlayerNameOverride()
		{
#if !DISABLESTEAMWORKS
			if (SteamManager.Initialized)
			{
				return SteamFriends.GetPersonaName();
			}
#endif
			
			return string.Empty;
		}
	}
}
