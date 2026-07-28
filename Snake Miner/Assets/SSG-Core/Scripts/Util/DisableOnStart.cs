using UnityEngine;

namespace SSG_Core.Scripts.Util
{
	/// <summary>
	/// Throws this on an object if you want to make sure it gets disabled once the game starts
	/// </summary>
	public class DisableOnStart : MonoBehaviour
	{
		private void Start()
		{
			gameObject.SetActive(false);
		}
	}
}