using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace SSG_Core.Scripts.Util
{
	public static class Extensions
	{
		public static void DestroyAllChildren(this Transform parentTransform)
		{
			if (parentTransform == null)
				return;
			foreach (Transform child in parentTransform)
			{
				if (child == null || child.gameObject == null) continue;
				Object.Destroy(child.gameObject);
			}
		}

		public static bool Equals(this int3 a, int3 b)
		{
			return a.x == b.x &&
			       a.y == b.y &&
			       a.z == b.z;
		}

		public static Vector3 Multiply(this Vector3 v1, Vector3 v2)
		{
			return new Vector3(v1.x * v2.x, v1.y * v2.y, v1.z * v2.z);
		}

		public static void DestroyChildrenAndClear<T>(this List<T> list) where T : Component
		{
			for (var i = list.Count - 1; i >= 0; i--)
				Object.Destroy(list[i].gameObject);
			list.Clear();
		}

		public static bool IsSurrounded(this int3 v1, int3[] vs)
		{
			int x = v1.x;
			int y = v1.y;
			int z = v1.z;

			// Check if coordinates exist on all sides
			var left = Array.Exists(vs, v => v.x == x - 1 && v.y == y && v.z == z);
			var right = Array.Exists(vs, v => v.x == x + 1 && v.y == y && v.z == z);
			var forward = Array.Exists(vs, v => v.x == x && v.y == y && v.z == z + 1);
			var backward = Array.Exists(vs, v => v.x == x && v.y == y && v.z == z - 1);
			var top = Array.Exists(vs, v => v.x == x && v.y == y + 1 && v.z == z);

			// Check if surrounded by coordinates on all sides
			return left && right && forward && backward && top;
		}

		public static bool IsOneAway(this int3 v1, int3 v2)
		{
			if (Mathf.Abs(v1.x - v2.x) == 1 &&
			    Mathf.Abs(v1.y - v2.y) == 0 ||
			    Mathf.Abs(v1.z - v2.z) == 0)
				return true;
			if (Mathf.Abs(v1.x - v2.x) == 0 &&
			    Mathf.Abs(v1.y - v2.y) == 1 ||
			    Mathf.Abs(v1.z - v2.z) == 0)
				return true;
			if (Mathf.Abs(v1.x - v2.x) == 0 &&
			    Mathf.Abs(v1.y - v2.y) == 0 ||
			    Mathf.Abs(v1.z - v2.z) == 1)
				return true;
			return false;
		}

		public static Sprite ToSquareSprite(this Texture2D texture)
		{
			var size = Mathf.Min(texture.width, texture.height);

			var offsetX = (texture.width - size) / 2;
			var offsetY = (texture.height - size) / 2;

			var squareTexture = new Texture2D(size, size);
			squareTexture.SetPixels(texture.GetPixels(offsetX, offsetY, size, size));
			squareTexture.Apply();

			var sprite = Sprite.Create(squareTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));

			return sprite;
		}
		
		public static Vector3 DrawArcToClampedGround(
			this LineRenderer line,
			Vector3 startPos,
			Vector3 endPos,
			float maxDistance,
			LayerMask groundMask,
			float arcHeight,
			int resolution = 20)
		{
			// 1. Clamp horizontal (XZ) distance without assuming ground Y
			var startXZ = new Vector2(startPos.x, startPos.z);
			var endXZ   = new Vector2(endPos.x, endPos.z);

			var offsetXZ = endXZ - startXZ;
			if (offsetXZ.magnitude > maxDistance)
			{
				offsetXZ = offsetXZ.normalized * maxDistance;
				endXZ = startXZ + offsetXZ;
			}

			// Reconstruct clamped end with original Y as a placeholder
			var clampedEnd = new Vector3(endXZ.x, endPos.y, endXZ.y);

			// 2. Raycast downward to find actual ground Y
			var rayOrigin = clampedEnd + Vector3.up * 100f;
			if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, 200f, groundMask))
			{
				clampedEnd.y = hit.point.y;
			}
			else
			{
				line.positionCount = 0;
				return startPos;
			}

			// 3. Draw the arc
			DrawArc(line, clampedEnd, startPos, arcHeight, resolution);
			return clampedEnd;
		}
		
		/// <summary>
		/// Draws a quadratic Bezier arc between two points.
		/// </summary>
		private static void DrawArc(LineRenderer line, Vector3 start, Vector3 end, float height, int resolution)
		{
			line.positionCount = resolution + 1;

			var mid = (start + end) * 0.5f;
			mid.y += height;

			for (var i = 0; i <= resolution; i++)
			{
				var t = i / (float)resolution;

				// Quadratic Bezier curve
				var point = Mathf.Pow(1 - t, 2) * start +
				            2 * (1 - t) * t * mid +
				            Mathf.Pow(t, 2) * end;

				line.SetPosition(i, point);
			}
		}
		
		public static Coroutine MoveInArc(
			this Transform transform,
			Vector3 target,
			float duration,
			float arcHeight)
		{
			return CoroutineHelper.Instance.StartCoroutine(
				MoveInArcRoutine(transform, target, duration, arcHeight));
		}

		private static IEnumerator MoveInArcRoutine(
			Transform transform,
			Vector3 target,
			float duration,
			float arcHeight)
		{
			var start = transform.position;
			var time = 0f;

			while (time < duration)
			{
				if (!transform) yield break;
				time += Time.deltaTime;
				var t = Mathf.Clamp01(time / duration);

				var pos = Vector3.Lerp(start, target, t);

				var arc = -4f * (t - 0.5f) * (t - 0.5f) + 1f;
				pos.y += arc * arcHeight;

				transform.position = pos;

				yield return null;
			}

			if (transform)
				transform.position = target;
		}

		public static Sprite GetSprite(this RenderTexture renderTexture)
		{
			var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
			RenderTexture.active = renderTexture;
			texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
			texture.Apply();
			RenderTexture.active = null;
			var capturedSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f, 0, SpriteMeshType.FullRect);
			return capturedSprite;
		}

		public static Color BrightestAndMostSaturated(this Color originalColor)
		{
			// Convert the color to HSB
			Color.RGBToHSV(originalColor, out float h, out float s, out _);

			// Set saturation and brightness to their maximum values
			s = 1f;
			var v = 1f;

			// Convert back to RGB
			return Color.HSVToRGB(h, s, v);
		}

		public static string GetHierarchyPath(this Transform transform)
		{
			if (transform == null)
				return string.Empty;

			var path = transform.name;
			var current = transform.parent;
			while (current != null)
			{
				path = $"{current.name}/{path}";
				current = current.parent;
			}

			return path;
		}

		public static string GetHierarchyPath(this GameObject gameObject)
		{
			return gameObject == null ? string.Empty : gameObject.transform.GetHierarchyPath();
		}
		
		private static Random _random = new Random();
		public static TKey GetRandomKey<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
		{
			if (dictionary == null || dictionary.Count == 0)
			{
				throw new ArgumentException("Dictionary is null or empty.");
			}

			var keys = new List<TKey>(dictionary.Keys);
			var randomIndex = _random.Next(keys.Count);
			return keys[randomIndex];
		}
		
		public static T GetRandomValue<T>(this List<T> list)
		{
			if (list == null || list.Count == 0)
			{
				Debug.LogError("The list cannot be null or empty.");
				return default;
			}

			var index = _random.Next(list.Count);
			return list[index];
		}

		public static T GetRandomValue<T>(this Array list, List<T> exceptList)
		{
			if (list == null || list.Length == 0)
			{
				Debug.LogError("The list cannot be null or empty.");
				return default;
			}

			var safety = 0;
			T value;
			do
			{
				var index = _random.Next(list.Length);
				value = (T)list.GetValue(index);
				safety++;
			} while (safety < 1000 && exceptList != null && exceptList.Contains(value));

			if (safety >= 1000)
				Debug.LogError("safety triggered in while loop!");
			return value;
		}
		
		public static T GetRandomValue<T>(this Array list)
		{
			return list.GetRandomValue<T>(null);
		}
		
		public static T CopyComponent<T>(this T original, GameObject destination) where T : Component
		{
			var copy = destination.AddComponent<T>();
			foreach (var field in typeof(T).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
			{
				field.SetValue(copy, field.GetValue(original));
			}
			foreach (var prop in typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
			{
				if (prop.CanWrite)
				{
					prop.SetValue(copy, prop.GetValue(original));
				}
			}
			return copy;
		}

		public static int3 ToInt3(this Vector3 v)
		{
			return new int3(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
		}
		
		public static Vector3 ToVector3(this int3 i)
		{
			return new Vector3(i.x, i.y, i.z);
		}
		
		public static Vector2 ToVector2(this int2 i)
		{
			return new Vector3(i.x, i.y);
		}
		
		public static int3 MoveTowards(this int3 current, int3 target, int maxDelta)
		{
			var result = current;

			// Calculate step in each direction while clamping by maxDelta
			result.x = MoveTowardsSingleAxis(current.x, target.x, maxDelta);
			result.y = MoveTowardsSingleAxis(current.y, target.y, maxDelta);
			result.z = MoveTowardsSingleAxis(current.z, target.z, maxDelta);

			return result;
		}

		private static int MoveTowardsSingleAxis(int current, int target, int maxDelta)
		{
			var delta = target - current;
        
			if (Mathf.Abs(delta) <= maxDelta)
			{
				return target; // If we're close enough, snap to target
			}

			return current + Mathf.Clamp(delta, -maxDelta, maxDelta); // Move by maxDelta in the right direction
		}

		public static T SelectRandom<T>(this IEnumerable<T> source)
		{
			if (source == null)
				return default;

			var list = source as IList<T> ?? source.ToList();
			if (list.Count == 0)
				return default;

			return list[UnityEngine.Random.Range(0, list.Count)];
		}

		public static IEnumerable<T> SelectRandom<T>(this IEnumerable<T> source, int count)
		{
			if (source == null)
			{
				Debug.LogError("select random source should not be null!");
				return null;
			}

			var sourceList = source.ToList();
			int n = sourceList.Count;

			if (count < 0 || count > n)
			{
				Debug.LogError("Count must be between 0 and the number of elements in the collection.");
				return null;
			}
			
			for (var i = 0; i < count; i++)
			{
				// Pick a random index from the remaining elements
				var randomIndex = _random.Next(i, n);
            
				// Swap the current element with the randomly selected element
				(sourceList[i], sourceList[randomIndex]) = (sourceList[randomIndex], sourceList[i]);
			}

			// Return the first 'count' elements
			return sourceList.Take(count);
		}
		
		public enum Axis { X, Y, Z }
		public static T FindExtremeByAxis<T>(this IEnumerable<T> objects, Axis axis, bool findMax = true) where T : Component
		{
			Func<T, float> selector = axis switch
			{
				Axis.X => obj => obj.transform.position.x,
				Axis.Y => obj => obj.transform.position.y,
				Axis.Z => obj => obj.transform.position.z,
				_ => throw new ArgumentOutOfRangeException(nameof(axis), "Invalid axis specified.")
			};

			return findMax ? objects.OrderByDescending(selector).First() : objects.OrderBy(selector).First();
		}

		public static void DestroyItemsAndClear<T>(this List<T> list) where T : Component
		{
			for (var i = list.Count - 1; i >= 0; i--)
				Object.Destroy(list[i].gameObject);
			list.Clear();
		}
		
		public static string GetFullHierarchyPath(this Transform transform)
		{
			return transform.parent == null ? transform.name : GetFullHierarchyPath(transform.parent) + "/" + transform.name;
		}
		
	
		/// <summary>
		/// Moves a transform to a target position in an arc over time.
		/// </summary>
		/// <param name="transform">The transform to move.</param>
		/// <param name="targetPosition">The target world position.</param>
		/// <param name="duration">Time in seconds to complete the arc.</param>
		/// <param name="arcHeight">Maximum height of the arc above the linear path.</param>
		public static IEnumerator MoveInArcRoutine(
			this Transform transform, Vector3 targetPosition, float duration, float arcHeight,
			Action callback = null)
		{
			Vector3 startPos = transform.position;
			float time = 0f;

			while (time < duration)
			{
				float t = time / duration;
				Vector3 flatPos = Vector3.Lerp(startPos, targetPosition, t);
				float arc = 4 * arcHeight * t * (1 - t); // simple parabola
				flatPos.y += arc;

				transform.position = flatPos;
				time += Time.deltaTime;
				yield return null;
			}

			transform.position = targetPosition;
			callback?.Invoke();
		}

	}
}
