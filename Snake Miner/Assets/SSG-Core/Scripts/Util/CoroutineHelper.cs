using System;
using System.Collections;
using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	public class CoroutineHelper : MonoBehaviour
	{
		private static CoroutineHelper _instance;

		// Singleton pattern to get a single MonoBehaviour instance
		public static CoroutineHelper Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject helperObject = new GameObject("CoroutineHelper");
					_instance = helperObject.AddComponent<CoroutineHelper>();
					DontDestroyOnLoad(helperObject); // Optional: keeps this object alive across scenes
				}
				return _instance;
			}
		}

		// Method to start a coroutine
		public static void Engage(IEnumerator coroutine, Action callback = null)
		{
			Instance.StartCoroutine(CallbackCoroutine(coroutine, callback));
		}
		
		private static IEnumerator CallbackCoroutine(IEnumerator coroutine, Action callback)
		{
			yield return coroutine;
			callback?.Invoke();
		}

		// Method to stop a coroutine
		public static void Disengage(IEnumerator coroutine)
		{
			Instance.StopCoroutine(coroutine);
		}

		public static IEnumerator Wait(float duration)
		{
			yield return new WaitForSeconds(duration);
		}
		
		public static IEnumerator MoveToTarget(Transform obj, Vector3 targetPos, float duration)
		{
			var startPos = obj.transform.position;

			for (var t = 0f; t < 1; t += Time.deltaTime / duration)
			{
				var currentPos = Vector3.Lerp(startPos, targetPos, t);
				if (obj)
					obj.transform.position = currentPos;

				yield return null;
			}

			if (obj)
				obj.transform.position = targetPos;
		}
	}
}