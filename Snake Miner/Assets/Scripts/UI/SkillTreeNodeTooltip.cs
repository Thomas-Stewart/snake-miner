using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SkillTreeNodeTooltip : MonoBehaviour
{
	public struct TooltipContent
	{
		public string Title;
		public string CurrentToNext;
		public string Delta;
		public string Cost;
		public float DeltaOpacity;
	}

	public static SkillTreeNodeTooltip Instance { get; private set; }

	[SerializeField] private RectTransform _rect;
	[SerializeField] private TMP_Text _titleText;
	[SerializeField] private TMP_Text _currentToNextText;
	[SerializeField] private TMP_Text _deltaText;
	[SerializeField] private GameObject _costBackground;
	[SerializeField] private TMP_Text _costText;
	[SerializeField] private Vector2 _cursorOffset = new Vector2(20f, -20f);
	[SerializeField] private Vector2 _nodeAnchorOffset = new Vector2(140f, 0f);
	[SerializeField] private float _screenEdgePadding = 12f;
	[SerializeField] private float _topEdgeExtraPadding = 36f;
	[SerializeField] private Vector2 _controllerStaticScreenOffset = new Vector2(280f, 0f);
	[SerializeField] private float _costTextLockedMinFontSize = 12f;
	[SerializeField] private Color _costTextLockedColor = new Color(0.95f, 0.45f, 0.45f);

	private Canvas _canvas;
	private bool _isVisible;
	private Object _owner;
	private Transform _ownerAnchor;
	private float _defaultCostTextFontSize;
	private bool _defaultCostTextAutoSizing;
	private float _defaultCostTextFontSizeMin;
	private float _defaultCostTextFontSizeMax;
	private Color _defaultCostTextColor;
	private Color _defaultDeltaTextColor;
	private bool _useControllerStaticPosition;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Debug.LogWarning("SkillTreeNodeTooltip: Multiple instances found. Keeping the first one.");
			gameObject.SetActive(false);
			return;
		}

		Instance = this;
		_canvas = GetComponentInParent<Canvas>();
		if (_costText != null)
		{
			_defaultCostTextFontSize = _costText.fontSize;
			_defaultCostTextAutoSizing = _costText.enableAutoSizing;
			_defaultCostTextFontSizeMin = _costText.fontSizeMin;
			_defaultCostTextFontSizeMax = _costText.fontSizeMax;
			_defaultCostTextColor = _costText.color;
		}
		if (_deltaText != null)
			_defaultDeltaTextColor = _deltaText.color;
		Hide();
	}

	private void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
	}

	public void ShowFor(Object owner, TooltipContent content)
	{
		_owner = owner;
		_ownerAnchor = GetOwnerAnchor(owner);
		_useControllerStaticPosition = false;
		ApplyContent(content);
		_isVisible = true;
		gameObject.SetActive(true);
		UpdatePosition();
	}

	public void ShowForController(Object owner, TooltipContent content)
	{
		_owner = owner;
		_ownerAnchor = GetOwnerAnchor(owner);
		_useControllerStaticPosition = true;
		ApplyContent(content);
		_isVisible = true;
		gameObject.SetActive(true);
		UpdatePosition();
	}

	private void ApplyContent(TooltipContent content)
	{
		if (_titleText != null)
			_titleText.text = content.Title;
		if (_currentToNextText != null)
		{
			_currentToNextText.text = content.CurrentToNext;
			_currentToNextText.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.CurrentToNext));
		}
		if (_deltaText != null)
		{
			_deltaText.text = content.Delta;
			_deltaText.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.Delta));
			var deltaColor = _defaultDeltaTextColor;
			deltaColor.a *= Mathf.Clamp01(content.DeltaOpacity <= 0f ? 1f : content.DeltaOpacity);
			_deltaText.color = deltaColor;
		}
		if (_costText != null)
		{
			var shouldShowCost = !string.IsNullOrWhiteSpace(content.Cost);
			_costText.text = content.Cost;
			_costText.gameObject.SetActive(shouldShowCost);
			if (_costBackground != null)
				_costBackground.SetActive(shouldShowCost);
			var isIslandRequirementText = !string.IsNullOrEmpty(content.Cost) && !content.Cost.StartsWith("$");
			_costText.enableAutoSizing = isIslandRequirementText || _defaultCostTextAutoSizing;
			_costText.fontSizeMin = isIslandRequirementText
				? Mathf.Min(_costTextLockedMinFontSize, _defaultCostTextFontSize)
				: _defaultCostTextFontSizeMin;
			_costText.fontSizeMax = isIslandRequirementText
				? _defaultCostTextFontSize
				: _defaultCostTextFontSizeMax;
			_costText.color = isIslandRequirementText ? _costTextLockedColor : _defaultCostTextColor;
			if (!isIslandRequirementText)
				_costText.fontSize = _defaultCostTextFontSize;
		}
	}

	private void Update()
	{
		if (!_isVisible)
			return;

		UpdatePosition();
	}

	public void Hide()
	{
		_isVisible = false;
		_owner = null;
		_ownerAnchor = null;
		_useControllerStaticPosition = false;
		gameObject.SetActive(false);
	}

	public void HideFor(Object owner)
	{
		if (_owner != owner)
			return;

		Hide();
	}

	private void UpdatePosition()
	{
		var rect = _rect != null ? _rect : transform as RectTransform;
		if (rect == null)
			return;

		if (_ownerAnchor != null && TrySetPositionFromOwnerAnchor(rect))
			return;

		var cursorScreenPos = GetDesiredScreenPosition();
		if (_canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
		{
			rect.position = cursorScreenPos;
			return;
		}

		var parentRect = rect.parent as RectTransform;
		if (parentRect == null)
		{
			rect.position = cursorScreenPos;
			return;
		}

		if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
			    parentRect,
			    cursorScreenPos,
			    _canvas.worldCamera,
			    out var worldPoint))
		{
			rect.position = worldPoint;
			return;
		}

		rect.position = cursorScreenPos;
	}

	private Vector2 GetDesiredScreenPosition()
	{
		if (_useControllerStaticPosition)
		{
			var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
			return center + GetScaledControllerOffset();
		}

		var mousePosition = Mouse.current != null
			? Mouse.current.position.ReadValue()
			: new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
		return mousePosition + GetScaledCursorOffset();
	}

	private bool TrySetPositionFromOwnerAnchor(RectTransform rect)
	{
		var parentRect = rect.parent as RectTransform;
		if (parentRect == null)
			return false;

		var screenPoint = GetOwnerAnchorScreenPoint();
		var tooltipCamera = GetCanvasCamera(_canvas);
		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, tooltipCamera, out var localPoint))
			return false;

		rect.anchoredPosition = GetVisibleAnchoredPosition(rect, parentRect, localPoint, _nodeAnchorOffset);
		return true;
	}

	private Vector2 GetVisibleAnchoredPosition(RectTransform rect, RectTransform parentRect, Vector2 anchorLocalPoint, Vector2 preferredOffset)
	{
		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

		var candidates = BuildOffsetCandidates(preferredOffset);
		var bestPosition = anchorLocalPoint + candidates[0];
		var bestOverflow = float.PositiveInfinity;
		for (var i = 0; i < candidates.Length; i++)
		{
			var candidate = anchorLocalPoint + candidates[i];
			var overflow = GetOverflowAmount(rect, parentRect, candidate);
			if (overflow <= 0f)
				return candidate;

			if (overflow < bestOverflow)
			{
				bestOverflow = overflow;
				bestPosition = candidate;
			}
		}

		return ClampAnchoredPositionToParent(rect, parentRect, bestPosition);
	}

	private static Vector2[] BuildOffsetCandidates(Vector2 preferredOffset)
	{
		var x = preferredOffset.x;
		var y = preferredOffset.y;
		var flippedX = Mathf.Abs(x) > 0.001f ? -x : x;
		var flippedY = Mathf.Abs(y) > 0.001f ? -y : y;
		return new[]
		{
			new Vector2(x, y),
			new Vector2(flippedX, y),
			new Vector2(x, flippedY),
			new Vector2(flippedX, flippedY),
			Vector2.zero
		};
	}

	private float GetOverflowAmount(RectTransform rect, RectTransform parentRect, Vector2 anchoredPosition)
	{
		var bounds = GetAnchoredBounds(rect, anchoredPosition);
		var parentBounds = GetPaddedParentBounds(parentRect);
		var overflow = 0f;
		overflow += Mathf.Max(0f, parentBounds.xMin - bounds.xMin);
		overflow += Mathf.Max(0f, bounds.xMax - parentBounds.xMax);
		overflow += Mathf.Max(0f, parentBounds.yMin - bounds.yMin);
		overflow += Mathf.Max(0f, bounds.yMax - parentBounds.yMax);
		return overflow;
	}

	private Vector2 ClampAnchoredPositionToParent(RectTransform rect, RectTransform parentRect, Vector2 anchoredPosition)
	{
		var bounds = GetAnchoredBounds(rect, anchoredPosition);
		var parentBounds = GetPaddedParentBounds(parentRect);
		var correction = Vector2.zero;

		if (bounds.xMin < parentBounds.xMin)
			correction.x += parentBounds.xMin - bounds.xMin;
		if (bounds.xMax > parentBounds.xMax)
			correction.x -= bounds.xMax - parentBounds.xMax;
		if (bounds.yMin < parentBounds.yMin)
			correction.y += parentBounds.yMin - bounds.yMin;
		if (bounds.yMax > parentBounds.yMax)
			correction.y -= bounds.yMax - parentBounds.yMax + Mathf.Max(0f, _topEdgeExtraPadding);

		return anchoredPosition + correction;
	}

	private Rect GetAnchoredBounds(RectTransform rect, Vector2 anchoredPosition)
	{
		var size = rect.rect.size;
		var min = anchoredPosition - Vector2.Scale(size, rect.pivot);
		return new Rect(min, size);
	}

	private Rect GetPaddedParentBounds(RectTransform parentRect)
	{
		var rect = parentRect.rect;
		var padding = Mathf.Max(0f, _screenEdgePadding);
		rect.xMin += padding;
		rect.xMax -= padding;
		rect.yMin += padding;
		rect.yMax -= padding + Mathf.Max(0f, _topEdgeExtraPadding);
		return rect;
	}

	private Vector2 GetOwnerAnchorScreenPoint()
	{
		var ownerCanvas = _ownerAnchor.GetComponentInParent<Canvas>();
		var ownerCamera = GetCanvasCamera(ownerCanvas);
		if (ownerCamera == null && ownerCanvas == null)
			ownerCamera = FindCameraForOwnerAnchor();

		return RectTransformUtility.WorldToScreenPoint(ownerCamera, _ownerAnchor.position);
	}

	private static Transform GetOwnerAnchor(Object owner)
	{
		return owner switch
		{
			Component component => component.transform,
			GameObject gameObject => gameObject.transform,
			_ => null
		};
	}

	private Vector2 GetScaledCursorOffset()
	{
		if (_canvas == null)
			return _cursorOffset;

		// Keep offset visually consistent when CanvasScaler changes UI scale across resolutions.
		return _cursorOffset * Mathf.Max(0.0001f, _canvas.scaleFactor);
	}

	private static Camera GetCanvasCamera(Canvas canvas)
	{
		return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
			? canvas.worldCamera
			: null;
	}

	private Camera FindCameraForOwnerAnchor()
	{
		var ownerLayerMask = 1 << _ownerAnchor.gameObject.layer;
		var cameras = Camera.allCameras;
		for (var i = 0; i < cameras.Length; i++)
		{
			var camera = cameras[i];
			if (camera != null && camera.enabled && (camera.cullingMask & ownerLayerMask) != 0)
				return camera;
		}

		return Camera.main;
	}

	private Vector2 GetScaledControllerOffset()
	{
		if (_canvas == null)
			return _controllerStaticScreenOffset;

		return _controllerStaticScreenOffset * Mathf.Max(0.0001f, _canvas.scaleFactor);
	}
}
