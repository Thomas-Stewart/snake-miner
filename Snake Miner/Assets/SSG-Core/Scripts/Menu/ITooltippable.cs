using UnityEngine.EventSystems;

namespace SSG_Core.Scripts.Menu
{
	public interface ITooltippable : IPointerEnterHandler, IPointerExitHandler
	{
		public void ShowTooltip();
		public void HideTooltip();
	}
}