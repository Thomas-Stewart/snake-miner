using System.Globalization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	[RequireComponent(typeof(TMP_Text))]
	public class DebugFPS : DebugUI
	{
		private float _deltaTime;

		[Button]
		public void ResetTimer()
		{
			_deltaTime = 0;
		}

		protected override void UpdateText()
		{
			_deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
			_text.text = $"{1f / _deltaTime:0}".ToString(CultureInfo.InvariantCulture);
		}
	}
}