public class InputActionPressedGameplayReminder : GameplayInputReminderDefinition
{
	internal override bool IsSatisfied()
	{
		var action = Action;
		return action != null && (action.WasPressedThisFrame() || action.IsPressed());
	}
}
