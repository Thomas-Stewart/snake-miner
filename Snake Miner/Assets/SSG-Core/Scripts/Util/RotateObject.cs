using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	/// <summary>
	/// Rotates an object according to the given rotation _speed
	/// </summary>
	public class RotateObject : MonoBehaviour
	{
		[SerializeField] private Vector3 _rotateSpeed;

		public Vector3 Speed => _rotateSpeed;

		public void SetSpeed(Vector3 speed)
		{
			_rotateSpeed = speed;
		}

		private void Update()
		{
			transform.Rotate(_rotateSpeed * Time.deltaTime);
		}
	}
}