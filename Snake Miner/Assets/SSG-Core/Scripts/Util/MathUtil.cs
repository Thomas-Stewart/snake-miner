using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	public static class MathUtil
	{
		public static float ExponentialRandom(float min, float max)
		{
			var exponent = 0.5f;
			var random01 = Random.value;
			var randomExponential = Mathf.Pow(random01, exponent);
			var result = Mathf.Lerp(min, max, randomExponential);

			return result;
		}
		
		public static Vector3 AngleBetween(Transform t1, Transform t2)
		{
			var direction1 = t1.position - t2.position;
			direction1.Normalize();
			return direction1;
		}
		
		public static Vector3[] OffsetXPositions(Vector3 startPosition, float offset, int numberOfPositions)
		{
			var positions = new Vector3[numberOfPositions];

			for (int i = 0; i < numberOfPositions; i++)
			{
				var angle = i * Mathf.PI * 2 / numberOfPositions;
				var x = startPosition.x + Mathf.Cos(angle) * offset;
				var y = startPosition.y; // Keeping the y position same as start position
				var z = startPosition.z + Mathf.Sin(angle) * offset;
        
				positions[i] = new Vector3(x, y, z);
			}

			return positions;
		}
	}
}