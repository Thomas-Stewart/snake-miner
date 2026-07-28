using UnityEngine;

public interface IInteractable
{
	public bool IsInteractable();
	public bool ShouldBlockSharedInput();
	public void ToggleIsInteractable(bool isInteractable);
	public void Interact();
	public GameObject GetGameObject();
	public bool GetShouldRemoveFromStackWhenUsed();
}
