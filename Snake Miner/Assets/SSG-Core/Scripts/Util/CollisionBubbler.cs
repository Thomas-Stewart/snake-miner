using System;
using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	public class CollisionBubbler2D : MonoBehaviour
	{
		public event Action<Collision2D> OnCollisionEnter2DEvent;
		public event Action<Collision2D> OnCollisionExit2DEvent;
		public event Action<Collider2D> OnTriggerEnter2DEvent;
		public event Action<Collider2D> OnTriggerExit2DEvent;

		private void OnCollisionEnter2D(Collision2D other)
		{
			OnCollisionEnter2DEvent?.Invoke(other);
		}

		private void OnCollisionExit2D(Collision2D other)
		{
			OnCollisionExit2DEvent?.Invoke(other);
		}
		
		private void OnTriggerEnter2D(Collider2D other)
		{
			OnTriggerEnter2DEvent?.Invoke(other);
		}

		private void OnTriggerExit2D(Collider2D other)
		{
			OnTriggerExit2DEvent?.Invoke(other);
		}
	}
}