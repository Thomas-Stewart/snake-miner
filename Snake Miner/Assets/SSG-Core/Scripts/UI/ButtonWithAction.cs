using System;

namespace SSG_Core.Scripts.UI
{
    public class ButtonWithAction : BaseButton
    {
        public event Action<BaseButton> OnClicked;

        protected override void InvokeEvent()
        {
            base.InvokeEvent();
            OnClicked?.Invoke(this);
        }
    }
}