using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	public class MoveObject : MonoBehaviour
	{
		[SerializeField] private Vector3 _speed;

		public Vector3 Speed => _speed;

		public void SetSpeed(Vector3 speed)
		{
			_speed = speed;
		}

		private void Update()
		{
			transform.Translate(_speed * Time.deltaTime);
		}
	}
}