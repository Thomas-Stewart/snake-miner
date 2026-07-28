using UnityEngine;

public class ProgressBarView : MonoBehaviour
{
	private const string ProgressBarMaterialResourcePath = "RuntimeMaterials/ProgressBar";

	[SerializeField] private Vector2 _size = new Vector2(1.1f, 0.12f);
	[SerializeField, Range(0f, 0.5f)] private float _roundness = 0.5f;
	[SerializeField, Range(0f, 1f)] private float _progress = 1f;
	[SerializeField] private Color _fillColor = new Color(1f, 0.78f, 0.18f, 1f);
	[SerializeField] private Color _backgroundColor = new Color(0.08f, 0.06f, 0.13f, 0.8f);
	[SerializeField] private int _sortingOrder = 80;

	private MeshRenderer _renderer;
	private Material _material;
	private float _visibility = 1f;

	private static Mesh _quadMesh;
	private static readonly int _progressProperty = Shader.PropertyToID("_Progress");
	private static readonly int _fillColorProperty = Shader.PropertyToID("_FillColor");
	private static readonly int _backgroundColorProperty = Shader.PropertyToID("_BackgroundColor");
	private static readonly int _widthProperty = Shader.PropertyToID("_Width");
	private static readonly int _heightProperty = Shader.PropertyToID("_Height");
	private static readonly int _roundnessProperty = Shader.PropertyToID("_Roundness");
	private static readonly int _pulseProperty = Shader.PropertyToID("_Pulse");
	private static readonly int _shineProperty = Shader.PropertyToID("_Shine");

	public Vector2 Size => _size;

	private void Awake()
	{
		EnsureRenderer();
		ApplyProperties();
	}

	private void OnDestroy()
	{
		if (Application.isPlaying && _material != null)
			Destroy(_material);
	}

	public void SetProgress(float progress)
	{
		_progress = Mathf.Clamp01(progress);
		EnsureRenderer();
		if (_material != null)
			_material.SetFloat(_progressProperty, _progress);
	}

	public void SetVisibility(float visibility)
	{
		_visibility = Mathf.Clamp01(visibility);
		EnsureRenderer();
		ApplyVisibility();
		ApplyProperties();
	}

	public void SetFillColor(Color fillColor)
	{
		_fillColor = fillColor;
		EnsureRenderer();
		ApplyProperties();
	}

	public void SetBackgroundColor(Color backgroundColor)
	{
		_backgroundColor = backgroundColor;
		EnsureRenderer();
		ApplyProperties();
	}

	public void SetSize(Vector2 size)
	{
		_size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
		EnsureRenderer();
		if (_material != null)
		{
			_material.SetFloat(_widthProperty, _size.x);
			_material.SetFloat(_heightProperty, _size.y);
		}
	}

	public void SetRoundness(float roundness)
	{
		_roundness = Mathf.Clamp01(roundness);
		EnsureRenderer();
		if (_material != null)
			_material.SetFloat(_roundnessProperty, _roundness);
	}

	public void SetPulse(float pulse)
	{
		EnsureRenderer();
		if (_material != null)
			_material.SetFloat(_pulseProperty, Mathf.Clamp01(pulse));
	}

	public void SetShine(float shine)
	{
		EnsureRenderer();
		if (_material != null)
			_material.SetFloat(_shineProperty, Mathf.Clamp01(shine));
	}

	public void SetProperties(Color fillColor, Color backgroundColor, Vector2 size, float roundness)
	{
		_fillColor = fillColor;
		_backgroundColor = backgroundColor;
		_size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
		_roundness = Mathf.Clamp01(roundness);
		EnsureRenderer();
		ApplyProperties();
	}

	private void EnsureRenderer()
	{
		if (_renderer != null && _material != null)
			return;

		_renderer = GetComponentInChildren<MeshRenderer>(true);
		if (_renderer == null)
		{
			var quad = new GameObject("Bar");
			quad.name = "Bar";
			quad.layer = gameObject.layer;
			quad.transform.SetParent(transform, false);
			var meshFilter = quad.AddComponent<MeshFilter>();
			meshFilter.sharedMesh = GetQuadMesh();
			_renderer = quad.AddComponent<MeshRenderer>();
		}

		if (_material == null)
			_material = CreateMaterial();

		_renderer.material = _material;
		_renderer.sortingOrder = _sortingOrder;
		ApplyVisibility();
	}

	private void ApplyVisibility()
	{
		if (_renderer != null)
			_renderer.enabled = _visibility > 0.01f;
	}

	private void ApplyProperties()
	{
		if (_material == null)
			return;

		_material.SetFloat(_progressProperty, Mathf.Clamp01(_progress));
		_material.SetColor(_fillColorProperty, WithAlphaMultiplier(_fillColor, _visibility));
		_material.SetColor(_backgroundColorProperty, WithAlphaMultiplier(_backgroundColor, _visibility));
		_material.SetFloat(_widthProperty, Mathf.Max(0.01f, _size.x));
		_material.SetFloat(_heightProperty, Mathf.Max(0.01f, _size.y));
		_material.SetFloat(_roundnessProperty, Mathf.Clamp01(_roundness));
		_material.SetFloat(_pulseProperty, 0f);
		_material.SetFloat(_shineProperty, 0f);
	}

	private static Material CreateMaterial()
	{
		var materialTemplate = Resources.Load<Material>(ProgressBarMaterialResourcePath);
		if (materialTemplate != null)
			return new Material(materialTemplate) { name = "Progress Bar Runtime" };

		Debug.LogError($"ProgressBarView: Missing Resources material '{ProgressBarMaterialResourcePath}'.");
		var shader = Shader.Find("Custom/ProgressBar");
		if (shader == null)
		{
			Debug.LogError("ProgressBarView: Missing Custom/ProgressBar shader.");
			shader = Shader.Find("Sprites/Default");
		}

		return new Material(shader) { name = "Progress Bar Runtime" };
	}

	private static Mesh GetQuadMesh()
	{
		if (_quadMesh != null)
			return _quadMesh;

		_quadMesh = new Mesh
		{
			name = "Progress Bar Quad"
		};
		_quadMesh.vertices = new []
		{
			new Vector3(-0.5f, -0.5f, 0f),
			new Vector3(0.5f, -0.5f, 0f),
			new Vector3(-0.5f, 0.5f, 0f),
			new Vector3(0.5f, 0.5f, 0f)
		};
		_quadMesh.uv = new []
		{
			new Vector2(0f, 0f),
			new Vector2(1f, 0f),
			new Vector2(0f, 1f),
			new Vector2(1f, 1f)
		};
		_quadMesh.triangles = new [] { 0, 2, 1, 2, 3, 1 };
		_quadMesh.RecalculateBounds();
		return _quadMesh;
	}

	private static Color WithAlphaMultiplier(Color color, float alphaMultiplier)
	{
		color.a *= Mathf.Clamp01(alphaMultiplier);
		return color;
	}
}
