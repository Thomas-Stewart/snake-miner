using System.Reflection;
using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	public static class Loggy
	{
		public static void Log(string s)
		{
			Debug.Log(s);
		}

		public static void LogError(string s)
		{
			Debug.LogError(s);
		}

		public static void Log(object o)
		{
			var type = o.GetType();
			var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

			foreach (var field in fields)
			{
				var value = field.GetValue(o);
				Debug.Log($"Variable Name: {field.Name}, Value: {value}");
			}
		}
	}
}