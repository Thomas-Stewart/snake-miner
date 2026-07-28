using System;
using UnityEngine;

namespace UI
{
    public class HealthBarView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRendererFill;
        [SerializeField] private SpriteRenderer _spriteRendererShowFill;
        [SerializeField] private SpriteRenderer _spriteRendererBg;
        [SerializeField] private float _showFillDecreaseSpeed = 0.1f;

        private float _shownFillPercentage = 1f;
        private float _fillPercentage = 1f;
        private float _startingScale;

        private void Start()
        {
            _startingScale = _spriteRendererFill.transform.localScale.y;
        }

        public void Hide()
        {
            SetActiveIfChanged(_spriteRendererFill.gameObject, false);
            SetActiveIfChanged(_spriteRendererBg.gameObject, false);
            SetActiveIfChanged(_spriteRendererShowFill.gameObject, false);
        }

        public void Show()
        {
            SetActiveIfChanged(_spriteRendererFill.gameObject, true);
            SetActiveIfChanged(_spriteRendererBg.gameObject, true);
            SetActiveIfChanged(_spriteRendererShowFill.gameObject, true);
        }

        public void SetFillAmount(float fillAmount)
        {
            Show();
            _fillPercentage = fillAmount;
            var scale = _spriteRendererFill.transform.localScale;
            scale.y = _startingScale * _fillPercentage;
            _spriteRendererFill.transform.localScale = float.IsFinite(scale.y) ? scale : Vector3.zero;
        }

        private void Update()
        {
            if (Math.Abs(_shownFillPercentage - _fillPercentage) > 0.01f)
            {
                _shownFillPercentage = Mathf.SmoothStep(_shownFillPercentage, _fillPercentage, _showFillDecreaseSpeed);
                var scale = _spriteRendererShowFill.transform.localScale;
                scale.y = _startingScale * _shownFillPercentage;
                _spriteRendererShowFill.transform.localScale = float.IsFinite(scale.y) ? scale : Vector3.zero;
            }
        }

        public void SetFillColor(Color color)
        {
            _spriteRendererFill.color = color;
        }

        private static void SetActiveIfChanged(GameObject target, bool isActive)
        {
            if (target.activeSelf != isActive)
                target.SetActive(isActive);
        }
    }
}
