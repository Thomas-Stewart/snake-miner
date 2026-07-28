using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	/// <summary>
	/// Applies a given material to a Mesh Renderer
	/// </summary>
	public class MaterialApplier : MonoBehaviour
	{
		[SerializeField] private MeshRenderer _meshRenderer;

		public void Initialize(Material material)
		{
			if (_meshRenderer)
				_meshRenderer.sharedMaterial = material;
		}
	}
}