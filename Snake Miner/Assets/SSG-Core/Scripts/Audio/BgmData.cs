using System;
using System.Collections.Generic;
using SSG_Core.Scripts.Core;

namespace SSG_Core.Scripts.Audio
{
	public enum MusicTrackMatchType
	{
		SceneName,
		LevelIndex
	}

	/// <summary>
	/// Background Music Data
	/// </summary>
	[Serializable]
	public class BgmData
	{
		public List<AudioClipData> AudioClips;
		public MusicTrackMatchType MatchType;
		public string SceneName;
		public int LevelIndex;
	}

	[Serializable]
	public class AmbientData
	{
		public List<AudioClipData> AudioClips;
		public GamePhase GamePhase;
	}
}
