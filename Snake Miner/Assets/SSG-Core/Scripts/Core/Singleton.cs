using UnityEngine;

namespace SSG_Core.Scripts.Core
{
	public class Singleton<T> : MonoBehaviour where T : Object
	{
		[SerializeField] private bool _dontDestroyOnLoad;
		public static T Instance { get; private set; }

		private void Initialize()
		{
			if (Instance == null)
			{
				Instance = FindFirstObjectByType<T>();
				if (Application.isPlaying && _dontDestroyOnLoad)
					DontDestroyOnLoad(Instance);
			}
		}

		protected virtual void Awake()
		{
			Initialize();
		}
	}
}