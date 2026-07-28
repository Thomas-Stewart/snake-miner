using System;
using System.Linq;
using SSG_Core.Scripts.Util;
using UnityEngine;

namespace UI
{
	public class CursorManager : MonoBehaviour
	{
		[SerializeField] private CursorData[] _cursorDatas;

		private CursorToolMode _currentCursorToolMode = CursorToolMode.None;
		public CursorToolMode CursorToolMode => _currentCursorToolMode;

		public void ChangeCursorTool(CursorToolMode newCursorToolMode)
		{
			if (_currentCursorToolMode == newCursorToolMode) return;

			Loggy.Log($"Cursor tool changed to {newCursorToolMode}");

			_currentCursorToolMode = newCursorToolMode;

			var data = _cursorDatas.FirstOrDefault(d => d.ToolMode == newCursorToolMode);

			if (data.CursorTexture == null)
				data.CursorTexture = new Texture2D(0, 0);

			//TEMP disabled
			// Cursor.SetCursor(data.CursorTexture,
			// 	new Vector2(data.CursorTexture.width / 2f, data.CursorTexture.width / 2f), CursorMode.ForceSoftware);
		}

		[Serializable]
		private struct CursorData
		{
			public Texture2D CursorTexture;
			public CursorToolMode ToolMode;
		}
	}
}