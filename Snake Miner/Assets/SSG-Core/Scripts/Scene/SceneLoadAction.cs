using UnityEngine;

namespace SSG_Core.Scripts.Scene
{
	public abstract class SceneLoadAction : MonoBehaviour
	{
		[SerializeField] private bool _shouldRunDuringUnload;
		[SerializeField] private bool _shouldRunDuringLoad;

		public bool ShouldRunDuringUnload => _shouldRunDuringUnload;
		public bool ShouldRunDuringLoad => _shouldRunDuringLoad;

		public abstract void DoAction();
		public abstract bool IsActionComplete();
	}
}