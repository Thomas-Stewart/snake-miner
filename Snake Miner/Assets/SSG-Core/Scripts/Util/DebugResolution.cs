using TMPro;
using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	[RequireComponent(typeof(TMP_Text))]
	public class DebugResolution : DebugUI
	{
		protected override void UpdateText()
		{
			_text.text = $"{Screen.width} x {Screen.height}";
		}
	}
}