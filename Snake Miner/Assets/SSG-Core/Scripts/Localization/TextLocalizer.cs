using System.Collections;
using Sirenix.OdinInspector;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace SSG_Core.Scripts.Localization
{
	[RequireComponent(typeof(TMP_Text))]
	public class TextLocalizer : MonoBehaviour
	{
		[ValueDropdown("GetAllLocIds")]
		[SerializeField] private string _locId;

		private TMP_Text _text;

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

			_text = GetComponent<TMP_Text>();
			_text.text = Localizer.GetText(_locId);
#if UNITY_EDITOR
			AssetDatabase.SaveAssets();			
#endif
		}

		private static IEnumerable GetAllLocIds()
		{
			return Localizer.GetAllLocIds();
		}
	}
}
