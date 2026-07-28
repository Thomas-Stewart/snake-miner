using SSG_Core.Scripts.Input;
using UnityEngine;

[DisallowMultipleComponent]
public class ControlBindingSpriteHandler : MonoBehaviour
{
	[SerializeField] private GameObject _controlBindingSprite;
	[SerializeField] private bool _hideWhileInteracting = true;

	private bool _isPlayerInTrigger;
	private IControlBindingSpriteCondition _condition;
	private IInteractable _interactable;

	private void Awake()
	{
		_condition ??= GetComponent<IControlBindingSpriteCondition>();
		_interactable ??= GetComponent<IInteractable>();
		RefreshVisibility();
	}

	private void Update()
	{
		RefreshVisibility();
	}

	public void Bind(GameObject controlBindingSprite, IControlBindingSpriteCondition condition, bool hideWhileInteracting)
	{
		_controlBindingSprite = controlBindingSprite;
		_condition = condition;
		_interactable ??= GetComponent<IInteractable>();
		_hideWhileInteracting = hideWhileInteracting;
		RefreshVisibility();
	}

	public void RefreshVisibility()
	{
		if (_controlBindingSprite == null)
			return;

		var shouldShow = _isPlayerInTrigger;
		if (shouldShow && _interactable != null)
			shouldShow = IsCurrentPlayerInteractable(_interactable);
		if (shouldShow && _hideWhileInteracting)
			shouldShow = !InputManager.InputActions.Player.Interact.IsInProgress();
		if (shouldShow && _condition != null)
			shouldShow = _condition.ShouldShowControlBindingSprite();

		if (_controlBindingSprite.activeSelf != shouldShow)
			_controlBindingSprite.SetActive(shouldShow);
	}

	private void OnTriggerEnter(Collider other)
	{
		SetPlayerTriggerState(other, true);
	}

	private void OnTriggerStay(Collider other)
	{
		SetPlayerTriggerState(other, true);
	}

	private void OnTriggerExit(Collider other)
	{
		SetPlayerTriggerState(other, false);
	}

	private void SetPlayerTriggerState(Collider other, bool isPlayerInTrigger)
	{
		if (!other.CompareTag("Player"))
			return;

		_isPlayerInTrigger = isPlayerInTrigger;
		RefreshVisibility();
	}

	private static bool IsCurrentPlayerInteractable(IInteractable interactable)
	{
		return interactable != null;
	}
}
