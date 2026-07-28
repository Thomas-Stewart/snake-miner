using SSG_Core.Scripts.Input;
using SSG.Util;
using TMPro;
using UnityEngine;

namespace SSG_Core.Scripts.Menu
{
	public class TitleScreenManager : MonoBehaviour
	{
		[SerializeField] private BaseMenuOption _continueOption;
		[SerializeField] private TMP_Text _buildVersionText;

		private void Start()
		{
			ControllerHelper.Instance.SetupRumbleOnExistingControllers();

			InputActionMapHelper.ChangeAllInputActionMap(InputActionMapHelper.UI);

			if (_continueOption)
				_continueOption.gameObject.SetActive(SaveUtil.HasMeaningfulSaveData());

			if (_buildVersionText)
				_buildVersionText.text = $"v{Application.version}";
		}
	}
}
