using SSG_Core.Scripts.UI;

namespace SSG_Core.Scripts.Menu
{
    public class ClosePopupHandler : MenuOptionHandler
    {
        public void ClosePopup()
        {
            PopupManager.Instance.ClosePopup();
        }
    }
}