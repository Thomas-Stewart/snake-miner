using UnityEngine;

namespace SSG_Core.Scripts.PlayStationUtil
{
    public class PSPauseChecker : MonoBehaviour
    {
#if UNITY_PS5
    private void Update()
    {
        var isUIOverlaid = UnityEngine.PS5.Utility.isSystemUiOverlaid;
        var isBackgrounded = UnityEngine.PS5.Utility.isInBackgroundExecution;

        if (isUIOverlaid || isBackgrounded)
        {
            Debug.Log("PS Overlay has been activated");
        }
    }
#endif
    }
}
