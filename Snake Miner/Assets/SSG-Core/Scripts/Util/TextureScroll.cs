using UnityEngine;
using UnityEngine.UI;

namespace SSG_Core.Scripts.Util
{
	public class TextureScroll : MonoBehaviour
	{
		[SerializeField] private Vector2 _scrollSpeed;
		[SerializeField] private Image _image;

		private Renderer _rend;
		private float _offsetX;
		private float _offsetY;

		private void Start()
		{
			_image.material = new Material(_image.material);
		}

		private void Update()
		{
			_offsetX = Time.time * _scrollSpeed.x;
			_offsetY = Time.time * _scrollSpeed.y;

			_image.material.SetTextureOffset("_MainTex", new Vector2(_offsetX, _offsetY));
		}
	}
}