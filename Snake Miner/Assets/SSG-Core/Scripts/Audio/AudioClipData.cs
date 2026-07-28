using System;
using UnityEngine;

namespace SSG_Core.Scripts.Audio
{
	[Serializable]
	public class AudioClipData
	{
		public AudioClip AudioClip;
		[Range(0f,1f)]
		public float Volume = 0.5f;
	}
}