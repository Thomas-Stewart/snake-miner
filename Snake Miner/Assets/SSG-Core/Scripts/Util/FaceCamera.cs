using System;
using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	[ExecuteInEditMode]
	public class FaceCamera : MonoBehaviour
	{
		[SerializeField] private Camera _camera;
		[SerializeField] private bool _shouldUpdateEveryFrame;

		private bool _isInitialized;
		private bool _isCameraNull;

		private void Start()
		{
			_isCameraNull = _camera == null;
		}

		private void Update()
		{
			if (_isInitialized && !_shouldUpdateEveryFrame) return;

			if (_isCameraNull)
			{
				_camera = Camera.main;
				_isCameraNull = _camera == null;
				return;
			}

			if (_camera)
			{
				transform.rotation = Quaternion.LookRotation(_camera.transform.forward, Vector3.up);
				_isInitialized = true;
			}
		}
	}
}