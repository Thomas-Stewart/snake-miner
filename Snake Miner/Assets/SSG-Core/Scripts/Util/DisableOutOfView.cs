using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	[RequireComponent(typeof(Renderer))]
	public class DisableOutOfView : MonoBehaviour
	{
		private Renderer _objectRenderer;

		private bool _isVisible = true;
		public bool IsVisible => _isVisible;

		private void Start()
		{
			_objectRenderer = GetComponent<Renderer>();
		}

		private void OnBecameInvisible()
		{
			_isVisible = false;

			_objectRenderer.enabled = false;
		}

		private void OnBecameVisible()
		{
#if UNITY_EDITOR
			if (UnityEngine.Camera.current)
			{
				Debug.Log("Camera.current.name = " + UnityEngine.Camera.current.name);
				if (UnityEngine.Camera.current.name == "SceneCamera")
					return;
			}
#endif
			_isVisible = true;

			_objectRenderer.enabled = true;
		}
	}
}