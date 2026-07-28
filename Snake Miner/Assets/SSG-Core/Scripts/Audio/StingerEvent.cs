using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SSG_Core.Scripts.Audio
{
	/// <summary>
	/// List of stinger events for sound fx
	/// </summary>
	public enum StingerEvent
	{
		GameStart,
		UISelect,
		BlockCrack,
		BlockBreak,
		LoadingScreenShow,
		LoadingScreenHide,
		UICancel,
		UISecondary,
		UINavigate,
		PopupAppear,
		TileSpawn,
		PopupHide,
		HudShow,
		HudHide,
		SpawnStructure,
		SpawnBridge,
		SpawnAnimal,
		TileErase,
		ObjectiveComplete,
		ToastInOut,
		LoadingCogMove,
		RotateObj, // todo: implement
		Screenshot,
		RodCast,
		BobberLand,
		RodReel,
		FishGain,
		FishPickedUp,
		MoneyTick,
		BobberAlert,
		BobberBounce,
		BerryPluck,
		BackpackThrow,
		BackpackStore,
		EndOfDayChime,
		DenyCastAttempt,
		SkillTreeNodeUnlock,
		CoinPickedUp,
		CoinDispense,
		CoinBanked,
		GoldFishSurface,
		PenguinPunt,
		FroggerMG_Jump,
		FroggerMG_Die,
		FroggerMG_Win,
		BoatCollectionMG_Start,
		BoatCollectionMG_Cast,
		BoatCollectionMG_Hook,
		BoatCollectionMG_Catch,
		BoatCollectionMG_PowerupPickup,
		BoatCollectionMG_RodPowerup,
		BoatCollectionMG_SpeedPowerup,
		BoatCollectionMG_RangePowerup,
		BoatCollectionMG_MoreFishPowerup,
		BoatCollectionMG_ResultsReveal,
		BoatCollectionMG_MoneyConvert,
		WoodPlankSurface,
		TurtlePet,
		BoatInteract,
		TitleGameStart,
		LevelVictoryBoatExit,
		BarrelComplete,
		BarrelCompletePrepare,
		PenguinPlayerKick,
		PenguinKicked,
		PenguinFly,
		DroneApproach,
		DroneHonk,
		QuotaProgressFlyToArrive,
		LavaPlatformBubbling,
		LavaPlatformBurst,
		FireballSpawn
	}

	[Serializable]
	public class StingerData
	{
		public StingerEvent StingerEvent;
		public List<AudioClipData> AudioClipDatas;
		[FoldoutGroup("Pitch Config")]
		[Tooltip("Randomly adds or subtracts pitch up to Pitch Variance each time this stinger plays.")]
		public bool UsePitchVariance;
		[FoldoutGroup("Pitch Config")]
		[Min(0f)]
		public float PitchVariance = 0.05f;
		[FoldoutGroup("Pitch Config")]
		[Tooltip("Raises this stinger's pitch each time it is played, then decays back toward normal over time.")]
		public bool UsePitchUp;
		[FoldoutGroup("Pitch Config")]
		[Min(0f)]
		public float PitchUpAmount = 0.05f;
		[FoldoutGroup("Pitch Config")]
		[Min(0f)]
		public float PitchUpMax = 0.5f;
		[FoldoutGroup("Pitch Config")]
		[Min(0f)]
		public float PitchUpDecayPerSecond = 0.25f;
	}
}
