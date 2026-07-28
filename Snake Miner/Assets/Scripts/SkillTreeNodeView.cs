using System;
using System.Collections;
using System.Collections.Generic;
using SSG_Core.Scripts.Localization;
using SSG_Core.Scripts.Input;
using SSG_Core.Scripts.UI;
using SSG.Util;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillTreeNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
	private const string CurrentlyFormatLocId = "ui_skill_tree_currently_format";
	private const string CurrentToNextFormatLocId = "ui_skill_tree_current_to_next_format";
	private const string NodeTitleFormatLocId = "ui_skill_tree_node_title_format";
	private const string UnlockedLocId = "ui_unlocked";
	private const string LockedLocId = "ui_locked";
	private const string UnlockLocId = "ui_unlock";

	public enum SkillTreeNodeState
	{
		Locked,
		DemoLocked,
		Unlockable,
		Unlocked
	}

	[SerializeField] private LineRenderer _linePrefab;
	[SerializeField] private float _lineZOffset = 0.05f;
	[SerializeField] private Color _lineFillColor = Color.white;
	[SerializeField] private float _lineFillWidthMultiplier = 0.7f;
	[SerializeField] private float _lineFillSpeed = 8f;
	[SerializeField] private TMP_Text _titleText;
	[SerializeField] private ButtonWithAction _button;
	[SerializeField] private Image[] _bgImages;
	[SerializeField] private Image[] _secondaryUnlockableImages;
	[SerializeField] private Color _lockedColor = Color.gray;
	[SerializeField] private Color _unlockedColor;
	[SerializeField] private Color _previouslyUnlockedColor = Color.gray;
	[SerializeField] private Color _unlockableColor;
	[SerializeField] private Color _secondaryUnlockableColor = Color.gray;
	[SerializeField] private Image _iconImg;
	[SerializeField] private GameObject _lockedScrim;
	[SerializeField] private GameObject _islandLockedIcon;
	[SerializeField] private GameObject[] _hoverOrFocusedHighlightObjs;
	[SerializeField] private GameObject _controlBindingSprite;
	[SerializeField] private Animation _animation;
	[SerializeField] private AnimationClip _appearAnimClip;
	[SerializeField] private AnimationClip _unlockAnimClip;
	[SerializeField] private AnimationClip _hoverAnimClip;
	
	private UpgradeNode _data;
	private int _index;
	private readonly List<ConnectionVisual> _connections = new List<ConnectionVisual>();
	private static readonly Dictionary<ConnectionKey, SharedConnectionState> ConnectionStates = new Dictionary<ConnectionKey, SharedConnectionState>();
	private bool _isHovered;
	private bool _isControllerSelected;
	private bool _wasUnlockedBeforeSession;
	private bool _justUnlockedThisRefresh;
	private bool _hasPendingIncomingFill;
	private bool _hasInitializedUnlockState;
	private Vector3 _baseScale;
	private EventTrigger _hoverEventTrigger;

	private enum ConnectionFillState
	{
		Unfilled,
		Filling,
		Filled
	}

	private struct ConnectionKey : IEquatable<ConnectionKey>
	{
		public GridPos A;
		public GridPos B;

		public ConnectionKey(GridPos a, GridPos b)
		{
			if (a.x < b.x || (a.x == b.x && a.y <= b.y))
			{
				A = a;
				B = b;
			}
			else
			{
				A = b;
				B = a;
			}
		}

		public bool Equals(ConnectionKey other) => A.Equals(other.A) && B.Equals(other.B);
		public override bool Equals(object obj) => obj is ConnectionKey other && Equals(other);
		public override int GetHashCode() => (A.GetHashCode() * 397) ^ B.GetHashCode();
	}

	private class SharedConnectionState
	{
		public ConnectionFillState FillState;
		public GridPos Source;
		public GridPos Target;
		public float Progress;
	}

	private class ConnectionVisual
	{
		public SkillTreeNodeView Other;
		public ConnectionKey Key;
		public LineRenderer BaseLine;
		public LineRenderer FillLine;
		public Vector3 LastBaseFrom;
		public Vector3 LastBaseTo;
		public Vector3 LastFillFrom;
		public Vector3 LastFillTo;
		public bool HasBasePositions;
		public bool HasFillPositions;
	}

	public SkillTreeNodeState State { get; private set; }
	public bool IsUnlocked { get; private set; }
	public bool IsUnlockableNode { get; private set; }
	public bool CanAfford { get; private set; }

	public UpgradeNode Data => _data;
	public int Index => _index;

	public event Action<SkillTreeNodeView> OnRequestPurchase;

	public void SetWasUnlockedBeforeSession(bool wasUnlockedBeforeSession)
	{
		_wasUnlockedBeforeSession = wasUnlockedBeforeSession;
	}

	public static void ResetSharedConnectionStates()
	{
		ConnectionStates.Clear();
	}

	private void Awake()
	{
		_baseScale = transform.localScale;
	}

	private void Start()
	{
		_button.OnClicked += HandleClicked;
		EnsureHoverEventBridge();
		RefreshHoverOrFocusHighlight();
	}

	private void OnDestroy()
	{
		if (_button != null)
			_button.OnClicked -= HandleClicked;

		HideHoverTooltip();
	}

	private void OnEnable()
	{
		_animation.Play(_appearAnimClip.name);
		transform.localScale = _baseScale;
	}

	private void Update()
	{
		UpdateConnectionFills();
	}

	private void OnDisable()
	{
		HideHoverTooltip();
		RefreshHoverOrFocusHighlight();
	}

	private void HandleClicked(BaseButton obj)
	{
		TryPurchase();
	}

	public bool TryPurchase()
	{
		if (State != SkillTreeNodeState.Unlockable)
			return false;
		if (!SkillTreeNodeRules.IsLevelRequirementMet(_data))
			return false;

		if (!GameConfigParser.TryGetFloat(_data.varsJson, "cost", out var cost))
		{
			Debug.LogError("Unable to find cost for upgrade " + _data.type);
			return false;
		}

		if (SaveUtil.SaveData.CashMoney < cost)
			return false;

		OnRequestPurchase?.Invoke(this);
		RefreshVisuals();
		return true;
	}

	public void PlayUnlockAnim()
	{
		StartCoroutine(PlayUnlockAnimRoutine());
	}

	private IEnumerator PlayUnlockAnimRoutine()
	{
		yield return new WaitWhile(() => _animation.isPlaying);
		_animation.Play(_unlockAnimClip.name);
	}

	public void RefreshVisuals()
	{
		var wasUnlocked = IsUnlocked;
		IsUnlocked = SaveUtil.IsUpgradeUnlocked(_data.gridPos);
		_justUnlockedThisRefresh = _hasInitializedUnlockState && !wasUnlocked && IsUnlocked;
		if (_justUnlockedThisRefresh)
			_hasPendingIncomingFill = true;
		_hasInitializedUnlockState = true;
		var isLockedByLevel = !SkillTreeNodeRules.IsLevelRequirementMet(_data);
		var isLockedForDemo = !IsUnlocked && SkillTreeNodeRules.IsDemoLocked(_data);
		IsUnlockableNode = !isLockedForDemo && ComputeIsUnlockable();
		CanAfford = false;
		if (IsUnlockableNode && GameConfigParser.TryGetFloat(_data.varsJson, "cost", out var cost))
			CanAfford = SaveUtil.SaveData.CashMoney >= cost;

		State = IsUnlocked
			? SkillTreeNodeState.Unlocked
			: isLockedForDemo
				? SkillTreeNodeState.DemoLocked
				: IsUnlockableNode
				? SkillTreeNodeState.Unlockable
				: SkillTreeNodeState.Locked;

		foreach (var bgImage in _bgImages)
		{
			if (State == SkillTreeNodeState.Unlocked)
				bgImage.color = _wasUnlockedBeforeSession ? _previouslyUnlockedColor : _unlockedColor;
			else if (State == SkillTreeNodeState.Unlockable)
				bgImage.color = _unlockableColor;
			else
				bgImage.color = _lockedColor;
		}

		if (_secondaryUnlockableImages != null && _secondaryUnlockableImages.Length > 0)
		{
			var secondaryColor = State == SkillTreeNodeState.Unlockable ? _secondaryUnlockableColor : Color.white;
			foreach (var image in _secondaryUnlockableImages)
			{
				if (image != null)
					image.color = secondaryColor;
			}
		}

		if (_lockedScrim != null)
			_lockedScrim.SetActive(State == SkillTreeNodeState.Locked || State == SkillTreeNodeState.DemoLocked || (State == SkillTreeNodeState.Unlockable && !CanAfford));
		if (_islandLockedIcon != null)
			_islandLockedIcon.SetActive(State == SkillTreeNodeState.DemoLocked || (State == SkillTreeNodeState.Locked && isLockedByLevel));
		
		if (_iconImg)
			_iconImg.gameObject.SetActive(_iconImg.sprite != null && (_islandLockedIcon == null || !_islandLockedIcon.activeInHierarchy));

		if (_button != null)
			_button.Button.interactable = State == SkillTreeNodeState.Unlockable;

		RefreshConnectionTargets();
		RefreshTooltipIfVisible();
	}

	private bool ComputeIsUnlockable() // via neighbor rules
	{
		return SkillTreeNodeRules.IsUnlockable(_data);
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		HandleHoverChanged(true);
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		HandleHoverChanged(false);
	}

	private void OnMouseEnter()
	{
		HandleHoverChanged(true);
	}

	private void OnMouseExit()
	{
		HandleHoverChanged(false);
	}

	private void HandleHoverChanged(bool hovered)
	{
		if (hovered && IsUsingController())
			return;
		if (!hovered && IsUsingController() && !_isHovered)
			return;

		_isHovered = hovered;
		RefreshHoverOrFocusHighlight();

		if (hovered)
		{
			ShowHoverTooltip();
			if (State == SkillTreeNodeState.Unlockable)
				_animation.Play(_hoverAnimClip.name);
		}
		else
		{
			if (!_isControllerSelected)
				HideHoverTooltip();
		}
	}

	public void SetControllerSelected(bool isSelected)
	{
		if (_isControllerSelected == isSelected)
			return;

		_isControllerSelected = isSelected;
		if (isSelected)
			_isHovered = false;
		RefreshHoverOrFocusHighlight();
		if (isSelected)
		{
			ShowControllerTooltip();
			if (_button != null)
				_button.Highlight(true);

			if (State == SkillTreeNodeState.Unlockable && _animation != null && _hoverAnimClip != null)
				_animation.Play(_hoverAnimClip.name);

			return;
		}

		if (_isHovered)
			return;

		HideHoverTooltip();
		if (_button != null)
			_button.Highlight(false);
		transform.localScale = _baseScale;
	}

	public void ClearMouseHoverState()
	{
		if (!_isHovered)
			return;

		_isHovered = false;
		RefreshHoverOrFocusHighlight();
		if (!_isControllerSelected)
			HideHoverTooltip();
	}

	private void RefreshHoverOrFocusHighlight()
	{
		if (_hoverOrFocusedHighlightObjs != null && _hoverOrFocusedHighlightObjs.Length > 0)
		{
			foreach (var hoverOrFocusedHighlightObj in _hoverOrFocusedHighlightObjs)
			{
				hoverOrFocusedHighlightObj.SetActive(_isHovered || _isControllerSelected);
			}
		}
		if (_controlBindingSprite != null)
			_controlBindingSprite.SetActive(ControllerHelper.Instance.IsMostRecentControlTypeAController && _isControllerSelected && !IsUnlocked);
	}

	private void EnsureHoverEventBridge()
	{
		if (_button == null || _button.Button == null)
			return;

		var target = _button.Button.gameObject;
		_hoverEventTrigger = target.GetComponent<EventTrigger>();
		if (_hoverEventTrigger == null)
			_hoverEventTrigger = target.AddComponent<EventTrigger>();

		AddEventTriggerEntry(EventTriggerType.PointerEnter, _ => HandleHoverChanged(true));
		AddEventTriggerEntry(EventTriggerType.PointerExit, _ => HandleHoverChanged(false));
	}

	private void AddEventTriggerEntry(EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> action)
	{
		if (_hoverEventTrigger == null)
			return;

		var entry = new EventTrigger.Entry { eventID = eventType };
		entry.callback.AddListener(action);
		_hoverEventTrigger.triggers.Add(entry);
	}

	private void ShowHoverTooltip()
	{
		var tooltip = SkillTreeNodeTooltip.Instance;
		if (tooltip == null)
			return;
		var content = BuildTooltipContent();
		tooltip.ShowFor(this, content);
	}

	private void ShowControllerTooltip()
	{
		var tooltip = SkillTreeNodeTooltip.Instance;
		if (tooltip == null)
			return;

		var content = BuildTooltipContent();
		tooltip.ShowForController(this, content);
	}

	private SkillTreeNodeTooltip.TooltipContent BuildTooltipContent()
	{
		var title = GameMods.GetTitleForType(_data.type);
		var current = GameDataManager.Instance != null ? GameDataManager.Instance.GetModValue(_data.type) : 0f;
		var next = current + _data.value;
		var currentToNextText = GetTooltipCurrentToNextText(_data.type, current, next);
		var deltaText = GetTooltipDeltaText(_data.type, _data.value);
		if (!IsUnlockUpgradeType(_data.type) && !string.IsNullOrEmpty(deltaText) && deltaText[0] != '+' && deltaText[0] != '-')
			deltaText = FormatLoc("ui_positive_value_format", deltaText);

		var costText = FormatCurrency(0d);
		if (GameConfigParser.TryGetFloat(_data.varsJson, "cost", out var cost))
			costText = FormatCurrency(cost);
		if (State == SkillTreeNodeState.DemoLocked)
			costText = SkillTreeNodeRules.GetDemoLockedText();
		else if (State == SkillTreeNodeState.Locked && !SkillTreeNodeRules.IsLevelRequirementMet(_data))
			costText = SkillTreeNodeRules.GetLevelRequirementText(_data);

		if (State == SkillTreeNodeState.Unlocked)
		{
			var currentText = GetTooltipCurrentText(_data.type, current);
			var purchaseSummary = !string.IsNullOrEmpty(costText)
				? $"{costText} : {deltaText}"
				: deltaText;

			return new SkillTreeNodeTooltip.TooltipContent
			{
				Title = title,
				CurrentToNext = FormatLoc(CurrentlyFormatLocId, currentText),
				Delta = purchaseSummary,
				Cost = string.Empty,
				DeltaOpacity = 0.5f
			};
		}

		return new SkillTreeNodeTooltip.TooltipContent
		{
			Title = title,
			CurrentToNext = currentToNextText,
			Delta = deltaText,
			Cost = costText,
			DeltaOpacity = 1f
		};
	}

	private void RefreshTooltipIfVisible()
	{
		var tooltip = SkillTreeNodeTooltip.Instance;
		if (tooltip == null)
			return;

		if (_isControllerSelected)
		{
			tooltip.ShowForController(this, BuildTooltipContent());
			return;
		}

		if (_isHovered && !IsUsingController())
			tooltip.ShowFor(this, BuildTooltipContent());
	}

	private void HideHoverTooltip()
	{
		var tooltip = SkillTreeNodeTooltip.Instance;
		if (tooltip == null)
			return;

		tooltip.HideFor(this);
	}

	private static bool IsUsingController()
	{
		return ControllerHelper.Instance != null && ControllerHelper.Instance.IsMostRecentControlTypeAController;
	}

	private static string TrimLeadingPlus(string text)
	{
		if (string.IsNullOrEmpty(text))
			return text;

		return text[0] == '+' ? text.Substring(1) : text;
	}

	private static string GetTooltipCurrentToNextText(string upgradeType, float current, float next)
	{
		if (IsUnlockUpgradeType(upgradeType) && current <= 0f && next > 0f)
			return string.Empty;

		var currentText = GameMods.GetDisplayValueForType(upgradeType, current);
		var nextText = GameMods.GetDisplayValueForType(upgradeType, next);
		currentText = TrimLeadingPlus(currentText);
		nextText = TrimLeadingPlus(nextText);

		return FormatLoc(CurrentToNextFormatLocId, currentText, nextText);
	}

	private static string GetTooltipDeltaText(string upgradeType, float value)
	{
		if (IsUnlockUpgradeType(upgradeType) && value > 0f)
			return Localizer.GetText(UnlockLocId);

		return GameMods.GetDisplayValueForType(upgradeType, value);
	}

	private static string GetTooltipCurrentText(string upgradeType, float current)
	{
		return TrimLeadingPlus(GameMods.GetDisplayValueForType(upgradeType, current));
	}

	private static bool IsUnlockUpgradeType(string upgradeType)
	{
		return GameMods.IsUnlockKey(upgradeType);
	}

	public void Initialize(UpgradeNode data, int index)
	{
		_data = data;
		_index = index;

		if (GameConfigParser.TryGetFloat(_data.varsJson, "cost", out var cost))
		{
			var valueText = GameMods.GetDisplayValueForType(data.type, data.value);
			_titleText.text = FormatLoc(NodeTitleFormatLocId, GameMods.GetTitleForType(data.type), valueText, FormatCurrency(cost));
		}
		
		name = $"{_data.type} ({_data.gridPos.x},{_data.gridPos.y})";
		RefreshIcon();
		RefreshVisuals();
	}

	private void RefreshIcon()
	{
		if (_iconImg == null)
			return;

		var iconSprite = SkillTreeIconResolver.GetIcon(_data.type);
		_iconImg.sprite = iconSprite;
		_iconImg.enabled = iconSprite != null;
	}

	private static string FormatLoc(string locId, params object[] args)
	{
		return string.Format(Localizer.GetText(locId), args);
	}

	private static string FormatCurrency(double amount)
	{
		return FormatLoc("ui_currency_amount_format", CurrencyFormatter.GetNumberShortText(amount));
	}

	public void AddConnection(SkillTreeNodeView other)
	{
		if (other == null) return;
		if (!gameObject.activeInHierarchy) return;
		if (!other.gameObject.activeInHierarchy) return;

		// Prevent duplicate lines (A->B and B->A)
		// if (_index > other._index) return;

		if (IsAlreadyConnected(other))
			return;

		if (_linePrefab == null)
		{
			Debug.LogWarning("SkillTreeNodeView: No line prefab assigned.");
			return;
		}

		var baseLine = Instantiate(_linePrefab, transform);
		baseLine.gameObject.layer = gameObject.layer;
		baseLine.sortingOrder = 100;
		baseLine.positionCount = 2;
		baseLine.useWorldSpace = true;

		var fillLine = Instantiate(_linePrefab, transform);
		fillLine.gameObject.layer = gameObject.layer;
		fillLine.sortingOrder = 101;
		fillLine.positionCount = 2;
		fillLine.useWorldSpace = true;
		fillLine.widthMultiplier *= Mathf.Max(0.01f, _lineFillWidthMultiplier);
		ApplyFlatFillLineColor(fillLine);

		var key = new ConnectionKey(_data.gridPos, other.Data.gridPos);
		var connectionState = GetOrCreateConnectionState(other);
		if (_hasPendingIncomingFill &&
		    other.IsUnlocked &&
		    connectionState.FillState == ConnectionFillState.Unfilled)
		{
			connectionState.FillState = ConnectionFillState.Filling;
			connectionState.Source = other.Data.gridPos;
			connectionState.Target = _data.gridPos;
			connectionState.Progress = 0f;
		}

		var connection = new ConnectionVisual
		{
			Other = other,
			Key = key,
			BaseLine = baseLine,
			FillLine = fillLine
		};

		_connections.Add(connection);
		ApplyConnectionPositions(connection);
	}

	public void ClearConnections()
	{
		for (var i = 0; i < _connections.Count; i++)
		{
			if (_connections[i].BaseLine != null)
				Destroy(_connections[i].BaseLine.gameObject);
			if (_connections[i].FillLine != null)
				Destroy(_connections[i].FillLine.gameObject);
		}

		_connections.Clear();
	}
	
	public bool IsAlreadyConnected(SkillTreeNodeView other)
	{
		for (var i = 0; i < _connections.Count; i++)
		{
			var connection = _connections[i];
			if (connection == null || connection.Other == null)
				continue;

			if (connection.Other == other)
				return true;
		}

		return false;
	}

	private void RefreshConnectionTargets()
	{
		for (var i = 0; i < _connections.Count; i++)
		{
			var connection = _connections[i];
			if (connection == null || connection.Other == null)
				continue;

			ApplyConnectionPositions(connection);
		}
	}

	private void UpdateConnectionFills()
	{
		if (_connections.Count == 0)
			return;

		var fillStep = Mathf.Max(0.01f, _lineFillSpeed) * Time.deltaTime;
		var hasActiveIncomingFill = false;
		for (var i = 0; i < _connections.Count; i++)
		{
			var connection = _connections[i];
			if (connection == null || connection.Other == null)
				continue;

			if (ConnectionStates.TryGetValue(connection.Key, out var connectionState) &&
			    connectionState.FillState == ConnectionFillState.Filling &&
			    connectionState.Target.Equals(_data.gridPos))
			{
				hasActiveIncomingFill = true;
				connectionState.Progress = Mathf.MoveTowards(connectionState.Progress, 1f, fillStep);
				if (connectionState.Progress >= 0.999f)
				{
					connectionState.Progress = 1f;
					connectionState.FillState = ConnectionFillState.Filled;
				}
			}

			ApplyConnectionPositions(connection);
		}

		if (_hasPendingIncomingFill && !hasActiveIncomingFill)
			_hasPendingIncomingFill = false;
	}

	private void ApplyConnectionPositions(ConnectionVisual connection)
	{
		if (connection == null || connection.Other == null)
			return;

		var from = GetConnectionPosition(transform.position);
		var to = GetConnectionPosition(connection.Other.transform.position);

		if (connection.BaseLine != null)
		{
			if (!connection.HasBasePositions || connection.LastBaseFrom != from || connection.LastBaseTo != to)
			{
				connection.BaseLine.SetPosition(0, from);
				connection.BaseLine.SetPosition(1, to);
				connection.LastBaseFrom = from;
				connection.LastBaseTo = to;
				connection.HasBasePositions = true;
			}
		}

		if (connection.FillLine != null)
		{
			if (!ConnectionStates.TryGetValue(connection.Key, out var connectionState) ||
			    connectionState.FillState == ConnectionFillState.Unfilled ||
			    !connectionState.Source.Equals(_data.gridPos))
			{
				if (connection.FillLine.enabled)
					connection.FillLine.enabled = false;
				return;
			}

			if (!connection.FillLine.enabled)
				connection.FillLine.enabled = true;
			var progress = connectionState.FillState == ConnectionFillState.Filled ? 1f : connectionState.Progress;
			var filledTo = Vector3.Lerp(from, to, Mathf.Clamp01(progress));
			if (!connection.HasFillPositions || connection.LastFillFrom != from || connection.LastFillTo != filledTo)
			{
				connection.FillLine.SetPosition(0, from);
				connection.FillLine.SetPosition(1, filledTo);
				connection.LastFillFrom = from;
				connection.LastFillTo = filledTo;
				connection.HasFillPositions = true;
			}
		}
	}

	private Vector3 GetConnectionPosition(Vector3 worldPosition)
	{
		worldPosition.z += -Mathf.Abs(_lineZOffset);
		return worldPosition;
	}

	private void ApplyFlatFillLineColor(LineRenderer lineRenderer)
	{
		if (lineRenderer == null)
			return;

		var gradient = new Gradient();
		gradient.SetKeys(
			new[]
			{
				new GradientColorKey(_lineFillColor, 0f),
				new GradientColorKey(_lineFillColor, 1f)
			},
			new[]
			{
				new GradientAlphaKey(_lineFillColor.a, 0f),
				new GradientAlphaKey(_lineFillColor.a, 1f)
			});
		lineRenderer.colorGradient = gradient;
	}

	private SharedConnectionState GetOrCreateConnectionState(SkillTreeNodeView other)
	{
		var key = new ConnectionKey(_data.gridPos, other.Data.gridPos);
		if (ConnectionStates.TryGetValue(key, out var existingState))
			return existingState;

		var source = _index <= other._index ? _data.gridPos : other.Data.gridPos;
		var target = source.Equals(_data.gridPos) ? other.Data.gridPos : _data.gridPos;
		var isFilled = IsUnlocked && other.IsUnlocked;
		var newState = new SharedConnectionState
		{
			FillState = isFilled ? ConnectionFillState.Filled : ConnectionFillState.Unfilled,
			Source = source,
			Target = target,
			Progress = isFilled ? 1f : 0f
		};
		ConnectionStates[key] = newState;
		return newState;
	}

}
