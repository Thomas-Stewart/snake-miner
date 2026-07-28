using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	public class DatabaseSingleton<T> : ScriptableObject where T : Object
	{
		private static T CreateInstance()
		{
			if (_instanceBacking != null) return _instanceBacking;

			_instanceBacking = Resources.Load<T>(typeof(T).Name);
			if (_instanceBacking == null)
			{
				Debug.LogError($"Cannot find {typeof(T).Name}");
				return null;
			}
			return _instanceBacking;
		}

		public static T Instance => _instanceBacking != null ? _instanceBacking : CreateInstance();
		private static T _instanceBacking;
	}
}
