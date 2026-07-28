using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	public class DrawForwardLine : MonoBehaviour
	{
		public float lineLength = 2f;
		public Color lineColor = Color.green;

		void OnDrawGizmos()
		{
			Gizmos.color = lineColor;
			Vector3 start = transform.position;
			Vector3 end = start + transform.forward * lineLength;
			Gizmos.DrawLine(start, end);
		}
	}
}