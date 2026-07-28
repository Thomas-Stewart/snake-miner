using System;
using SSG_Core.Scripts.UI;
using TMPro;
using UnityEngine;

namespace UI
{
    public class GameHUD : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TMP_Text _woodPointValueText;
        [SerializeField] private ButtonWithAction _fightBtn;

        public event Action OnFightBtnClicked;

        private void Start()
        {
            _fightBtn.OnClicked += HandleFightButtonClicked;
            ToggleFightBtn(false);
            if (_woodPointValueText != null)
                _woodPointValueText.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_fightBtn != null)
                _fightBtn.OnClicked -= HandleFightButtonClicked;
        }

        private void HandleFightButtonClicked(BaseButton _)
        {
            OnFightBtnClicked?.Invoke();
        }

        public void SetWoodPoints(int woodPoints)
        {
            _woodPointValueText.text = woodPoints.ToString();
        }

        public void ToggleFightBtn(bool isOn)
        {
            _fightBtn.gameObject.SetActive(isOn);
        }
    }
}
