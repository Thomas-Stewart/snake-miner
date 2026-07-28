using System.Collections.Generic;
using UnityEngine;

namespace SSG_Core.Scripts.Menu
{
	[CreateAssetMenu(fileName = nameof(PopupMessageData), menuName = "SSG/PopupMessageData")]
	public class PopupMessageData : ScriptableObject
	{
		[SerializeField] private string _title;
		[SerializeField] private List<string> _messages;

		public List<string> Messages => _messages;
		public string Title => _title;
	}
}