using UnityEngine;

namespace SSG_Core.Scripts.Menu
{
	public class BoolMenuOption : BaseMenuOption
	{
		[SerializeField] private GameObject _showCheckedObj;

		private bool _value;

		protected override void RefreshUI()
		{
			base.RefreshUI();
			_showCheckedObj.SetActive(_value);
		}

		public void SetValue(bool value)
		{
			_value = value;
			RefreshUI();
		}

		public bool GetValue()
		{
			return _value;
		}
	}
}