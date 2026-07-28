using System.Linq;
using UnityEngine;

namespace SSG_Core.Scripts.Util.Platform
{
	public class ConditionalHide : MonoBehaviour
	{
		[SerializeField] private bool _shouldHideInProduction;
		[SerializeField] private RuntimePlatform[] _platformsToHideIn;
		private void Awake()
		{
			if (_shouldHideInProduction)
			{
#if PRODUCTION_BUILD
				gameObject.SetActive(false);
#endif
			}

			if (_platformsToHideIn.Contains(Application.platform))
			{
				gameObject.SetActive(false);
			}
		}
	}
}