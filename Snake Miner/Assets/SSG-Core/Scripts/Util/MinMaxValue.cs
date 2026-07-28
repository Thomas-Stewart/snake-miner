using System;
using Random = UnityEngine.Random;

namespace SSG_Core.Scripts.Util
{
	[Serializable]
	public struct MinMaxValue
	{
		public float Min;
		public float Max;

		public float GetRandValue()
		{
			return Random.Range(Min, Max);
		}
	}
}