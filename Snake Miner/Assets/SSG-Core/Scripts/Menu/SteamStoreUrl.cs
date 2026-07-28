using SSG_Core.Scripts.SteamUtil;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace SSG_Core.Scripts.Menu
{
	public static class SteamStoreUrl
	{
		public static void Open(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				return;

#if !DISABLESTEAMWORKS
			if (SteamManager.Initialized && url.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
			{
				SteamFriends.ActivateGameOverlayToWebPage(url);
				return;
			}
#endif

			Application.OpenURL(url);
		}
	}
}
