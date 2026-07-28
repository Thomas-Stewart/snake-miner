using TMPro;
using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	public abstract class DebugUI : MonoBehaviour
	{
		[SerializeField] private bool _shouldUpdateEveryFrame;

		protected TMP_Text _text;
		private bool _isDisabled;

		public bool IsDisabled => _isDisabled;

		private void Awake()
		{
			_text = GetComponent<TMP_Text>();
			_isDisabled = false;
			ToggleUI();
		}

		public void ToggleUI()
		{
			_isDisabled = !_isDisabled;
			_text.enabled = !_isDisabled;

			if (!_isDisabled)
				UpdateText();
		}

		private void Update()
		{
			if (_isDisabled) return;
			if (!_shouldUpdateEveryFrame) return;

			UpdateText();
		}

		protected abstract void UpdateText();
	}
}