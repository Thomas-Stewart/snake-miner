using System;
using SSG_Core.Scripts.Audio;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.Menu;
using SSG_Core.Scripts.Scene;
using SSG_Core.Scripts.UI;

namespace UI
{
	/// <summary>
	/// UI element that appears when the game is paused
	/// </summary>
	public class StorePopupMenuHandler : MenuOptionHandler
	{
		public void SelectUpgradeShoes()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
		}

		public void SelectBuyItem()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
		}
		
		public void SelectUpgradePrimary()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
		}
		
		public void SelectUpgradeSecondary()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
		}
		
		public void SelectUpgradeLand()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
		}
		
		public void SelectBuyHelper()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
		}

		public void SelectBuyClownFish() => SelectBuyItem();
		public void SelectUpgradeRod() => SelectUpgradePrimary();
		public void SelectUpgradeBobber() => SelectUpgradeSecondary();
		public void SelecBuyFisherman() => SelectBuyHelper();

		public void SelectExit()
		{
			MusicManager.Instance.PlayStinger(StingerEvent.UISelect);
			PopupManager.Instance.CloseAllPopups();
		}
	}
}
