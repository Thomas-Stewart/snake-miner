using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SSG_Core.Scripts.Localization;
using SSG_Core.Scripts.UI;

[RequireComponent(typeof(Popup))]
public class EndIslandMapPopupView : MonoBehaviour
{
	private const string TitleLocId = "ui_popup_title_end_island_map";

	private static Sprite s_whiteSprite;

	[SerializeField] private List<Sprite> _islandSprites = new();
	[SerializeField] private Sprite _playerIconSprite;
	[SerializeField] private Color _playerIconColor = new(0.22f, 0.73f, 0.45f, 1f);
	[SerializeField] private Vector2 _islandDotSize = new(64f, 64f);
	[SerializeField] private float _finalIslandScale = 1.35f;
	[SerializeField, Range(0f, 1f)] private float _horizontalSpacingVariance = 0.75f;
	[SerializeField, Range(0f, 1f)] private float _verticalSpacingVariance = 0.75f;
	[SerializeField] private Vector2 _islandXBounds = new(-240f, 240f);
	[SerializeField] private Vector2 _islandYBounds = new(-90f, 90f);
	[SerializeField] private Vector2 _playerIconSize = new(26f, 58f);
	[SerializeField] private float _routeLineThickness = 10f;
	[SerializeField] private Color _routeLineColor = new(1f, 1f, 1f, 0.75f);
	[SerializeField] private Image _scrimImage;
	[SerializeField] private Color _scrimTargetColor = new(0f, 0f, 0f, 0.55f);
	[SerializeField] private float _scrimFadeDuration = 0.25f;

	private Popup _popup;
	private TMP_Text _titleText;
	private RectTransform _mapRoot;
	[SerializeField] private RectTransform[] _islandDots;
	[SerializeField] private RectTransform _playerIcon;
	private Vector3[] _islandPositions;
	private Coroutine _scrimFadeRoutine;
	private bool _wasPopupOpen;

	public Popup Popup => _popup;

	private void Awake()
	{
		_popup = GetComponent<Popup>();
		EnsureScrimImage();
		EnsureView();
	}

	private void OnEnable()
	{
		EnsureScrimImage();
		SetScrimColor(new Color(_scrimTargetColor.r, _scrimTargetColor.g, _scrimTargetColor.b, 0f));
		_wasPopupOpen = false;
	}

	private void OnDisable()
	{
		if (_scrimFadeRoutine == null)
		{
			_wasPopupOpen = false;
			return;
		}

		StopCoroutine(_scrimFadeRoutine);
		_scrimFadeRoutine = null;
		_wasPopupOpen = false;
	}

	private void Update()
	{
		var isPopupOpen = _popup != null && _popup.IsOpen;
		if (isPopupOpen && !_wasPopupOpen)
		{
			EnsureScrimImage();
			SetScrimColor(new Color(_scrimTargetColor.r, _scrimTargetColor.g, _scrimTargetColor.b, 0f));
			StartScrimFade(_scrimTargetColor);
		}

		_wasPopupOpen = isPopupOpen;
	}

	[Button]
	public void PrepareRoute(int currentIndex, int targetIndex, int islandCount)
	{
		EnsureView();
		if (_mapRoot == null)
			return;

		if (_titleText != null)
			_titleText.text = Localizer.GetText(TitleLocId);

		if (!Application.isPlaying && islandCount < 3 && _islandSprites != null && _islandSprites.Count > 0)
			islandCount = _islandSprites.Count;

		if (!Application.isPlaying)
			BuildDots(Mathf.Max(2, islandCount));
		if (_islandDots == null || _islandDots.Length == 0 || _playerIcon == null)
			return;

		var clampedStart = Mathf.Clamp(currentIndex, 0, _islandDots.Length - 1);
		_playerIcon.localPosition = GetDotCenterInMapSpace(_islandDots[clampedStart]);
	}

	public IEnumerator AnimateRouteRoutine(int currentIndex, int targetIndex)
	{
		EnsureView();
		if (_islandDots == null || _islandDots.Length == 0 || _playerIcon == null)
			yield break;

		var clampedStart = Mathf.Clamp(currentIndex, 0, _islandDots.Length - 1);
		var clampedEnd = Mathf.Clamp(targetIndex, clampedStart, _islandDots.Length - 1);
		var startPos = GetDotCenterInMapSpace(_islandDots[clampedStart]);
		var endPos = GetDotCenterInMapSpace(_islandDots[clampedEnd]);
		_playerIcon.localPosition = startPos;
		_playerIcon.localScale = Vector3.one;

		var duration = 1.8f + Mathf.Abs(clampedEnd - clampedStart) * 0.25f;
		var elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			var t = Mathf.Clamp01(elapsed / duration);
			var easedT = Mathf.SmoothStep(0f, 1f, t);
			_playerIcon.localPosition = Vector3.Lerp(startPos, endPos, easedT);
			var pulse = 1f + Mathf.Sin(t * Mathf.PI * 8f) * 0.08f;
			_playerIcon.localScale = new Vector3(pulse, pulse, 1f);
			yield return null;
		}

		_playerIcon.localPosition = endPos;
		_playerIcon.localScale = Vector3.one;
		yield return new WaitForSecondsRealtime(0.35f);
	}

	private Vector3 GetDotCenterInMapSpace(RectTransform dot)
	{
		if (dot == null)
			return Vector3.zero;

		return dot.localPosition;
	}

	private void EnsureView()
	{
		_popup ??= GetComponent<Popup>();
		EnsureScrimImage();
		_titleText ??= FindTextByName("Title Text");
		ApplyTitleLayout();
		HideBuiltInMenu();
		_mapRoot ??= FindRectByName("End Island Map Content");
		if (_mapRoot == null)
			CreateMapRoot();
	}

	private void EnsureScrimImage()
	{
		if (_scrimImage == null)
		{
			var scrimRect = FindRectByName("Scrim Image");
			if (scrimRect != null)
				_scrimImage = scrimRect.GetComponent<Image>();
		}

		var canvasRect = FindCanvasRect();
		if (canvasRect == null)
			return;

		if (_scrimImage == null)
		{
			var scrimGo = new GameObject("Scrim Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			scrimGo.transform.SetParent(canvasRect, false);
			_scrimImage = scrimGo.GetComponent<Image>();
			_scrimImage.sprite = GetWhiteSprite();
		}

		var rect = _scrimImage.rectTransform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		rect.localScale = Vector3.one;
		rect.SetAsFirstSibling();

		_scrimImage.raycastTarget = false;
	}

	private void StartScrimFade(Color targetColor)
	{
		if (_scrimImage == null)
			return;

		if (_scrimFadeRoutine != null)
			StopCoroutine(_scrimFadeRoutine);

		_scrimFadeRoutine = StartCoroutine(ScrimFadeRoutine(targetColor));
	}

	private IEnumerator ScrimFadeRoutine(Color targetColor)
	{
		var startColor = _scrimImage.color;
		var duration = Mathf.Max(0f, _scrimFadeDuration);
		if (duration <= 0f)
		{
			SetScrimColor(targetColor);
			_scrimFadeRoutine = null;
			yield break;
		}

		var elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			SetScrimColor(Color.Lerp(startColor, targetColor, Mathf.Clamp01(elapsed / duration)));
			yield return null;
		}

		SetScrimColor(targetColor);
		_scrimFadeRoutine = null;
	}

	private void SetScrimColor(Color color)
	{
		if (_scrimImage != null)
			_scrimImage.color = color;
	}

	private void CreateMapRoot()
	{
		var scaleRoot = FindScaleRoot();
		if (scaleRoot == null)
			return;

		var mapGo = new GameObject("End Island Map Content", typeof(RectTransform));
		mapGo.transform.SetParent(scaleRoot, false);
		mapGo.layer = scaleRoot.gameObject.layer;
		_mapRoot = mapGo.GetComponent<RectTransform>();
		_mapRoot.anchorMin = new Vector2(0.06f, 0.14f);
		_mapRoot.anchorMax = new Vector2(0.94f, 0.66f);
		_mapRoot.offsetMin = Vector2.zero;
		_mapRoot.offsetMax = Vector2.zero;
	}

	private void BuildDots(int islandCount)
	{
		if (_mapRoot == null)
			return;

		ClearMapRootChildren();

		_islandDots = new RectTransform[islandCount];
		_islandPositions = new Vector3[islandCount];
		var islandXs = GetIslandXPositions(islandCount);

		for (var i = 0; i < islandCount; i++)
		{
			var dotGo = new GameObject($"Island Dot {i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			dotGo.transform.SetParent(_mapRoot, false);
			dotGo.layer = _mapRoot.gameObject.layer;

			var rect = dotGo.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = GetIslandDotSize(i, islandCount);
			var position = new Vector3(islandXs[i], GetIslandYPosition(i, islandCount), 0f);
			rect.localPosition = position;

			var image = dotGo.GetComponent<Image>();
			image.sprite = GetIslandSprite(i);
			image.color = new Color(0.91f, 0.96f, 0.93f, 1f);
			image.preserveAspect = true;

			_islandDots[i] = rect;
			_islandPositions[i] = position;
		}

		BuildRouteSegments();

		var iconGo = new GameObject("Player Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		iconGo.transform.SetParent(_mapRoot, false);
		iconGo.layer = _mapRoot.gameObject.layer;
		_playerIcon = iconGo.GetComponent<RectTransform>();
		_playerIcon.anchorMin = new Vector2(0.5f, 0.5f);
		_playerIcon.anchorMax = new Vector2(0.5f, 0.5f);
		_playerIcon.sizeDelta = _playerIconSize;

		var iconImage = iconGo.GetComponent<Image>();
		iconImage.sprite = _playerIconSprite != null ? _playerIconSprite : GetWhiteSprite();
		iconImage.color = _playerIconColor;
		iconImage.preserveAspect = true;
	}

	private void ClearMapRootChildren()
	{
		for (var i = _mapRoot.childCount - 1; i >= 0; i--)
		{
			var child = _mapRoot.GetChild(i).gameObject;
			if (Application.isPlaying)
				Destroy(child);
			else
				DestroyImmediate(child);
		}
	}

	private void BuildRouteSegments()
	{
		if (_mapRoot == null || _islandPositions == null || _islandPositions.Length < 2)
			return;

		for (var i = 0; i < _islandPositions.Length - 1; i++)
		{
			var from = _islandPositions[i];
			var to = _islandPositions[i + 1];
			var delta = to - from;
			var distance = delta.magnitude;
			if (distance <= Mathf.Epsilon)
				continue;

			var lineGo = new GameObject($"Route Segment {i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			lineGo.transform.SetParent(_mapRoot, false);
			lineGo.layer = _mapRoot.gameObject.layer;

			var rect = lineGo.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(distance, _routeLineThickness);
			rect.localPosition = from + delta * 0.5f;
			rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

			var image = lineGo.GetComponent<Image>();
			image.sprite = GetWhiteSprite();
			image.color = _routeLineColor;
			image.raycastTarget = false;

			rect.SetAsFirstSibling();
		}
	}

	private Sprite GetIslandSprite(int index)
	{
		if (_islandSprites == null || _islandSprites.Count == 0)
			return GetWhiteSprite();

		var clampedIndex = Mathf.Clamp(index, 0, _islandSprites.Count - 1);
		return _islandSprites[clampedIndex] != null ? _islandSprites[clampedIndex] : GetWhiteSprite();
	}

	private Vector2 GetIslandDotSize(int index, int islandCount)
	{
		if (islandCount > 0 && index == islandCount - 1)
			return _islandDotSize * Mathf.Max(1f, _finalIslandScale);

		return _islandDotSize;
	}

	private float[] GetIslandXPositions(int islandCount)
	{
		var positions = new float[islandCount];
		if (islandCount <= 0)
			return positions;

		var minX = Mathf.Min(_islandXBounds.x, _islandXBounds.y);
		var maxX = Mathf.Max(_islandXBounds.x, _islandXBounds.y);
		var width = maxX - minX;
		if (width <= Mathf.Epsilon)
		{
			for (var i = 0; i < islandCount; i++)
				positions[i] = minX;
			return positions;
		}

		var variance = Mathf.Clamp01(_horizontalSpacingVariance);
		positions[0] = minX;
		if (islandCount == 1)
			return positions;

		positions[islandCount - 1] = maxX;
		if (islandCount == 2)
			return positions;

		var randomPositions = new float[islandCount - 2];
		for (var i = 0; i < randomPositions.Length; i++)
			randomPositions[i] = Random.Range(minX, maxX);

		System.Array.Sort(randomPositions);
		for (var i = 1; i < islandCount - 1; i++)
		{
			var evenX = Mathf.Lerp(minX, maxX, (float)i / (islandCount - 1));
			positions[i] = Mathf.Lerp(evenX, randomPositions[i - 1], variance);
		}

		return positions;
	}

	private float GetIslandYPosition(int index, int islandCount)
	{
		var minY = Mathf.Min(_islandYBounds.x, _islandYBounds.y);
		var maxY = Mathf.Max(_islandYBounds.x, _islandYBounds.y);
		if (index == 0 || index == islandCount - 1)
			return (minY + maxY) * 0.5f;

		var evenY = 0f;
		var randomY = Random.Range(minY, maxY);
		return Mathf.Lerp(evenY, randomY, Mathf.Clamp01(_verticalSpacingVariance));
	}

	private void ApplyTitleLayout()
	{
		if (_titleText == null)
			return;

		var rect = _titleText.rectTransform;
		rect.anchorMin = new Vector2(0.08f, 0.68f);
		rect.anchorMax = new Vector2(0.92f, 0.9f);
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;

		_titleText.fontSize = 40f;
		_titleText.fontSizeMin = 22f;
		_titleText.fontSizeMax = 40f;
		_titleText.enableAutoSizing = true;
		_titleText.alignment = TextAlignmentOptions.Center;
	}

	private void HideBuiltInMenu()
	{
		var menu = FindRectByName("Menu");
		if (menu != null)
			menu.gameObject.SetActive(false);
	}

	private RectTransform FindScaleRoot()
	{
		var transforms = GetComponentsInChildren<RectTransform>(true);
		for (var i = 0; i < transforms.Length; i++)
		{
			if (transforms[i].name == "Scale")
				return transforms[i];
		}

		return null;
	}

	private RectTransform FindCanvasRect()
	{
		var canvas = GetComponentInChildren<Canvas>(true);
		return canvas != null ? canvas.transform as RectTransform : null;
	}

	private RectTransform FindRectByName(string objectName)
	{
		var transforms = GetComponentsInChildren<RectTransform>(true);
		for (var i = 0; i < transforms.Length; i++)
		{
			if (transforms[i].name == objectName)
				return transforms[i];
		}

		return null;
	}

	private TMP_Text FindTextByName(string objectName)
	{
		var texts = GetComponentsInChildren<TMP_Text>(true);
		for (var i = 0; i < texts.Length; i++)
		{
			if (texts[i].name == objectName)
				return texts[i];
		}

		return null;
	}

	private static Sprite GetWhiteSprite()
	{
		if (s_whiteSprite != null)
			return s_whiteSprite;

		s_whiteSprite = Sprite.Create(
			Texture2D.whiteTexture,
			new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
			new Vector2(0.5f, 0.5f));
		return s_whiteSprite;
	}
}
