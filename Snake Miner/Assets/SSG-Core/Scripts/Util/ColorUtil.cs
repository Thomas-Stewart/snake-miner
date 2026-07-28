using System.Collections.Generic;
using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	public static class ColorUtil
	{
		public static Color GetDarkenedColor(Color color, float darkenAmount = 0.2f)
		{
			// var darkenAmount = 0.2f;
			var newR = Mathf.Clamp01(color.r - darkenAmount);
			var newG = Mathf.Clamp01(color.g - darkenAmount);
			var newB = Mathf.Clamp01(color.b - darkenAmount);
			return new Color(newR, newG, newB);
		}

		public static Color[] GetColorShades(Color color, float deviation, int numColors)
		{
			var colors = new Color[numColors];
			for (var i = 0; i < numColors; i++)
			{
				var darkenAmount = ((float)i / numColors * deviation) - deviation / 2f;
				var newR = Mathf.Clamp01(color.r + darkenAmount);
				var newG = Mathf.Clamp01(color.g + darkenAmount);
				var newB = Mathf.Clamp01(color.b + darkenAmount);
				colors[i] = new Color(newR, newG, newB);
			}

			return colors;
		}

		public static Color GetRandomDarkOrBrighterColor(Color color, float deviation)
		{
			var darkenAmount = Random.Range(0f, deviation) - deviation / 2f;
			var newR = Mathf.Clamp01(color.r + darkenAmount);
			var newG = Mathf.Clamp01(color.g + darkenAmount);
			var newB = Mathf.Clamp01(color.b + darkenAmount);
			return new Color(newR, newG, newB);
		}

		public static Color GetRandomColor()
		{
			var randomR = Random.Range(0f, 1f);
			var randomG = Random.Range(0f, 1f);
			var randomB = Random.Range(0f, 1f);

			return new Color(randomR, randomG, randomB);
		}

		public static Color GetRandomColor(Color startColor, Color endColor)
		{
			var randomR = Random.Range(Mathf.Min(startColor.r, endColor.r), Mathf.Max(startColor.r, endColor.r));
			var randomG = Random.Range(Mathf.Min(startColor.g, endColor.g), Mathf.Max(startColor.g, endColor.g));
			var randomB = Random.Range(Mathf.Min(startColor.b, endColor.b), Mathf.Max(startColor.b, endColor.b));

			return new Color(randomR, randomG, randomB);
		}

		public static void ApplyMatToRenderers(IEnumerable<Renderer> renderers, Material material)
		{
			foreach (var renderer1 in renderers)
			{
				if (renderer1)
				{
					renderer1.material = material;
				}
			}
		}
		// }
		// public static void ApplyMatPropBlock(IEnumerable<Renderer> renderers, MaterialPropertyBlock materialPropertyBlock)
		// {
		// 	foreach (var renderer1 in renderers)
		// 	{
		// 		if (renderer1)
		// 		{
		// 			renderer1.SetPropertyBlock(materialPropertyBlock);
		// 		}
		// 	}
		// }

		public static void ApplyColorToMaterial(Material material, Color color, bool sameDarkerColor = false)
		{
			material.SetColor(ShaderUtil.BaseColor, color);
			material.SetColor(ShaderUtil.DimColor, sameDarkerColor ? color : GetDarkenedColor(color));
		}

		// public static MaterialPropertyBlock GeneratePropBlock(Color color, bool sameDarkerColor = false)
		// {
		// 	var propBlock = new MaterialPropertyBlock();
		// 	propBlock.SetColor(ShaderUtil.BaseColor, color);
		// 	propBlock.SetColor(ShaderUtil.DimColor, sameDarkerColor ? color : GetDarkenedColor(color));
		// 	return propBlock;
		// }

		// public static void SetBaseAndDimColor(IEnumerable<Renderer> renderers, Color color, bool sameDarkerColor = false)
		// {
		// 	var propBlock = new MaterialPropertyBlock();
		// 	propBlock.SetColor(ShaderUtil.BaseColor, color);
		// 	propBlock.SetColor(ShaderUtil.DimColor, sameDarkerColor ? color : GetDarkenedColor(color));
		// 	foreach (var renderer1 in renderers)
		// 	{
		// 		if (renderer1)
		// 		{
		// 			renderer1.SetPropertyBlock(propBlock);
		// 		}
		// 	}
		// }

		// public static void SetLitColor(Renderer[] renderers, Color color, bool shouldEnableEmission)
		// {
		// 	var propBlock = new MaterialPropertyBlock();
		// 	propBlock.SetColor(ShaderUtil.BaseColor, color);
		//
		// 	if (renderers.Length > 0 && renderers[0])
		// 	{
		// 		if (shouldEnableEmission)
		// 			renderers[0].material.EnableKeyword("_EMISSION");
		// 		else
		// 			renderers[0].material.DisableKeyword("_EMISSION");
		// 	}
		//
		// 	foreach (var renderer1 in renderers)
		// 	{
		// 		if (renderer1)
		// 		{
		// 			renderer1.SetPropertyBlock(propBlock);
		// 		}
		// 	}
		// }
	}
}