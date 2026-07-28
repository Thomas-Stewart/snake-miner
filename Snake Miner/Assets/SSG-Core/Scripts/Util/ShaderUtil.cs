using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	public abstract class ShaderUtil
	{
		public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
		public static readonly int DimColor = Shader.PropertyToID("_ColorDim");
		public static readonly int EmissionEnabled = Shader.PropertyToID("_EMISSION");
	}
}