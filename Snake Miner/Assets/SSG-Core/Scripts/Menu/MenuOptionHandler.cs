using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace SSG_Core.Scripts.Menu
{
	public class MenuOptionHandler : MonoBehaviour
	{
		[SerializeField] protected OptionEventPair[] _optionEventPairs;

		public virtual void ChooseMenuOption(BaseMenuOption menuOption, bool shouldGoRight)
		{
			var pair = _optionEventPairs.FirstOrDefault(p => p.MenuOption == menuOption);
			pair.Event?.Invoke();
		}

		[Serializable]
		protected struct OptionEventPair
		{
			public BaseMenuOption MenuOption;
			public UnityEvent Event;
		}
	}
}
