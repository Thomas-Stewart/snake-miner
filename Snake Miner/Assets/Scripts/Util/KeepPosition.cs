using System;
using ExternPropertyAttributes;
using UnityEngine;

namespace Util
{
	public class KeepPosition : MonoBehaviour
	{
		[SerializeField] private XYZ _position;
		[SerializeField] private XYZ _rotation;
		[SerializeField] private XYZ _scale;
		

		private void LateUpdate()
		{
			var transform1 = transform;

			var pos = transform1.position;
			if (_position.ShouldFreezeX)
				pos.x = _position.XVal;
			if (_position.ShouldFreezeY)
				pos.y = _position.YVal;
			if (_position.ShouldFreezeZ)
				pos.z = _position.ZVal;
			transform1.position = pos;
			
			var rot = transform1.eulerAngles;
			if (_rotation.ShouldFreezeX)
				rot.x = _rotation.XVal;
			if (_rotation.ShouldFreezeY)
				rot.y = _rotation.YVal;
			if (_rotation.ShouldFreezeZ)
				rot.z = _rotation.ZVal;
			transform1.eulerAngles = rot;

			var scale = transform1.localScale;
			if (_scale.ShouldFreezeX)
				scale.x = _scale.XVal;
			if (_scale.ShouldFreezeY)
				scale.y = _scale.YVal;
			if (_scale.ShouldFreezeZ)
				scale.z = _scale.ZVal;
			transform1.localScale = scale;
		}

		[Serializable]
		private struct XYZ
		{
			public bool ShouldFreezeX;
			public bool ShouldFreezeY;
			public bool ShouldFreezeZ;
			[ShowIf(nameof(ShouldFreezeX))] public float XVal;
			[ShowIf(nameof(ShouldFreezeY))] public float YVal;
			[ShowIf(nameof(ShouldFreezeZ))] public float ZVal;
		}
	}
}