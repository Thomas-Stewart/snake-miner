using Sirenix.OdinInspector;
using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	[CreateAssetMenu(fileName = nameof(GuidSO), menuName = "SSG/GuidSO")]
	public class GuidSO : ScriptableObject
	{
		[SerializeField, ReadOnly] private string _guid;

		public string Guid {
			get
			{
				if (string.IsNullOrEmpty(_guid))
					Debug.LogError("GUID not set!");

				return _guid;
			}
		}

		[Button]
		private void GenerateGuid(){
			_guid = System.Guid.NewGuid().ToString();
		}
	}
}