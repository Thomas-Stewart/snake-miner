using System.Collections.Generic;
using System.Linq;
using SSG.Util;
using SSG_Core.Scripts.Core;
using SSG_Core.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameplayInputReminderSystem : MonoBehaviour
{
	private sealed class ReminderRuntime
	{
		public GameplayInputReminderDefinition Definition;
		public float ElapsedSeconds;
		public bool IsCompleted;
		public bool HasBegunEvaluation;
	}

	[SerializeField] private GameplayInputReminderPanelView _panelTemplate;
	[SerializeField] private int _sortingOrder = 9500;
	[SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
	[SerializeField] private bool _onlyEvaluateOnFirstLevel = true;

	private readonly List<ReminderRuntime> _runtimeReminders = new();
	private GameplayInputReminderView _view;

	private void Awake()
	{
		BuildRuntimeState();
		EnsureView();
	}

	private void Update()
	{
		if (_view == null)
			return;

		if (!ShouldEvaluateReminders())
		{
			_view.Hide();
			return;
		}

		for (var i = 0; i < _runtimeReminders.Count; i++)
		{
			var reminder = _runtimeReminders[i];
			if (reminder == null || reminder.Definition == null || reminder.IsCompleted)
				continue;

			if (!reminder.Definition.ShouldShowOnCurrentIsland())
				continue;

			if (!reminder.HasBegunEvaluation)
			{
				reminder.Definition.BeginEvaluation();
				reminder.HasBegunEvaluation = true;
			}

			if (reminder.Definition.IsSatisfied())
			{
				MarkCompleted(reminder);
				continue;
			}

			reminder.ElapsedSeconds += Time.unscaledDeltaTime;
		}

		var activeReminder = GetHighestPriorityOverdueReminder();
		if (activeReminder == null)
		{
			_view.Hide();
			return;
		}

		_view.Show(activeReminder.Definition.PromptText, activeReminder.Definition.ActionReference);
	}

	private void OnDestroy()
	{
		if (_view != null)
			_view.DestroySelf();
	}

	private void BuildRuntimeState()
	{
		_runtimeReminders.Clear();
		var definitions = GetComponentsInChildren<GameplayInputReminderDefinition>(includeInactive: true)
			.Where(definition => definition != null)
			.OrderBy(definition => definition.DisplayOrder)
			.ToArray();

		for (var i = 0; i < definitions.Length; i++)
		{
			var definition = definitions[i];
			if (string.IsNullOrWhiteSpace(definition.Id) || definition.ActionReference == null)
				continue;

			definition.ResetEvaluation();
			_runtimeReminders.Add(new ReminderRuntime
			{
				Definition = definition,
				ElapsedSeconds = 0f,
				IsCompleted = definition.PersistCompletion && SaveUtil.HasCompletedGameplayReminder(definition.Id),
				HasBegunEvaluation = false
			});
		}
	}

	private void EnsureView()
	{
		if (_view != null)
			return;

		_view = GameplayInputReminderView.Create(
			"Gameplay Input Reminder",
			ResolvePanelTemplate(),
			_sortingOrder,
			_referenceResolution);
	}

	private GameplayInputReminderPanelView ResolvePanelTemplate()
	{
		if (_panelTemplate != null)
			return _panelTemplate;

		_panelTemplate = GetComponentInChildren<GameplayInputReminderPanelView>(includeInactive: true);
		return _panelTemplate;
	}

	public void MarkReminderCompleted(string reminderId)
	{
		if (string.IsNullOrWhiteSpace(reminderId))
			return;

		for (var i = 0; i < _runtimeReminders.Count; i++)
		{
			var reminder = _runtimeReminders[i];
			if (reminder == null || reminder.Definition == null || reminder.Definition.Id != reminderId)
				continue;

			MarkCompleted(reminder);
			if (_view != null)
				_view.Hide();
			return;
		}
	}

	private bool ShouldEvaluateReminders()
	{
		if (_onlyEvaluateOnFirstLevel && SaveUtil.SaveData.CurrentLevelIndex != 0)
			return false;

		if (PopupManager.Instance != null && PopupManager.Instance.AreAnyPopupsShowing)
			return false;

		if (SkillTreePopupController.IsPopupOpenOrTransitioning)
			return false;

		if (CoreGameManager.Instance != null && CoreGameManager.Instance.CurrentGamePhase == GamePhase.SkillTree)
			return false;

		return true;
	}

	private void MarkCompleted(ReminderRuntime reminder)
	{
		if (reminder == null || reminder.IsCompleted)
			return;

		reminder.IsCompleted = true;
		if (reminder.Definition != null && reminder.Definition.PersistCompletion)
			SaveUtil.MarkGameplayReminderCompleted(reminder.Definition.Id);
	}

	private ReminderRuntime GetHighestPriorityOverdueReminder()
	{
		for (var i = 0; i < _runtimeReminders.Count; i++)
		{
			var reminder = _runtimeReminders[i];
			if (reminder == null || reminder.IsCompleted || reminder.Definition == null)
				continue;

			if (!reminder.Definition.ShouldShowOnCurrentIsland())
				continue;

			if (reminder.ElapsedSeconds >= Mathf.Max(0f, reminder.Definition.DelaySeconds))
				return reminder;
		}

		return null;
	}

	private sealed class GameplayInputReminderView
	{
		private readonly GameObject _root;
		private readonly GameplayInputReminderPanelView _panel;
		private readonly Canvas _canvas;

		private GameplayInputReminderView(GameObject root, Canvas canvas, GameplayInputReminderPanelView panel)
		{
			_root = root;
			_canvas = canvas;
			_panel = panel;
		}

		public static GameplayInputReminderView Create(string name, GameplayInputReminderPanelView panelTemplate, int sortingOrder, Vector2 referenceResolution)
		{
			if (panelTemplate == null)
				return null;

			var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			var canvas = canvasObject.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = sortingOrder;

			var scaler = canvasObject.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = referenceResolution;
			scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
			scaler.matchWidthOrHeight = 0.5f;

			var panelInstance = UnityEngine.Object.Instantiate(panelTemplate, canvasObject.transform);
			panelInstance.gameObject.SetActive(false);
			return new GameplayInputReminderView(canvasObject, canvas, panelInstance);
		}

		public void Show(string promptText, UnityEngine.InputSystem.InputActionReference actionReference)
		{
			if (_root == null)
				return;

			if (!_root.activeSelf)
				_root.SetActive(true);

			if (_panel != null && !_panel.gameObject.activeSelf)
				_panel.gameObject.SetActive(true);

			_panel?.Configure(promptText, actionReference);

			if (_canvas != null && !_canvas.enabled)
				_canvas.enabled = true;
		}

		public void Hide()
		{
			if (_panel != null && _panel.gameObject.activeSelf)
				_panel.gameObject.SetActive(false);

			if (_root != null && _root.activeSelf)
				_root.SetActive(false);
		}

		public void DestroySelf()
		{
			if (_root != null)
				UnityEngine.Object.Destroy(_root);
		}
	}
}
