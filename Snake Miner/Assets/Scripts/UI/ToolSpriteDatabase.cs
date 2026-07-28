using System;
using System.Collections.Generic;
using System.Linq;
using SSG_Core.Scripts.Util;
using UnityEngine;

namespace UI
{
	[CreateAssetMenu(fileName = nameof(ToolSpriteDatabase), menuName = "SSG/ToolSpriteDatabase")]
	public class ToolSpriteDatabase : DatabaseSingleton<ToolSpriteDatabase>
	{
		[SerializeField] private List<ToolSpriteData> _toolSpriteDatas;

		public static Sprite GetSprite(CursorToolMode toolType)
		{
			return Instance._toolSpriteDatas.FirstOrDefault(t => t.ToolType == toolType).Sprite;
		}

		[Serializable]
		private struct ToolSpriteData
		{
			public CursorToolMode ToolType;
			public Sprite Sprite;
		}
	}
}