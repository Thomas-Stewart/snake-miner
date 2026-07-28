using SSG_Core.Scripts.Localization;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class GameplayInputReminderDefinition : MonoBehaviour
{
	[SerializeField] private string _id;
	[SerializeField] private string _promptText;
	[SerializeField] private InputActionReference _actionReference;
	[SerializeField] private float _delaySeconds = 8f;
	[SerializeField] private bool _persistCompletion = true;
	[SerializeField] private int _displayOrder;
	[SerializeField] private int _onlyShowOnIslandId;

	public string Id => _id;
	public string PromptText => string.IsNullOrEmpty(_promptText) ? string.Empty : Localizer.GetText(_promptText);
	public float DelaySeconds => _delaySeconds;
	public bool PersistCompletion => _persistCompletion;
	public int DisplayOrder => _displayOrder;
	public InputActionReference ActionReference => _actionReference;
	protected InputAction Action => _actionReference != null ? _actionReference.action : null;

	internal bool ShouldShowOnCurrentIsland()
	{
		return _onlyShowOnIslandId <= 0;
	}

	internal virtual void ResetEvaluation()
	{
	}

	internal virtual void BeginEvaluation()
	{
	}

	internal abstract bool IsSatisfied();
}
