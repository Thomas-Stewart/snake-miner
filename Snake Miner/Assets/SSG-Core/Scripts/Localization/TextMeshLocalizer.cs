using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SSG_Core.Scripts.Localization
{
	[RequireComponent(typeof(TextMesh))]
	public class TextMeshLocalizer : MonoBehaviour
	{
		[ValueDropdown("GetAllLocIds")]
		[SerializeField] private string _locId;

		private TextMesh _text;

		private void OnEnable()
		{
			Localizer.OnLanguageChanged += RefreshText;
			RefreshText();
		}

		private void OnDisable()
		{
			Localizer.OnLanguageChanged -= RefreshText;
		}

		private void Awake()
		{
			RefreshText();
		}

		[Button]
		private void LoadFileAndRefresh()
		{
			Localizer.LoadFile();
			RefreshText();
		}

		public void RefreshText()
		{
			if (string.IsNullOrEmpty(_locId))
				return;

			_text = GetComponent<TextMesh>();
			_text.text = Localizer.GetText(_locId);
		}

		private static IEnumerable GetAllLocIds()
		{
			return Localizer.GetAllLocIds();
		}
	}
}
