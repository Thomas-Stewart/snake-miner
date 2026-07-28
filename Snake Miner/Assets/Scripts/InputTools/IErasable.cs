namespace InputTools
{
	public interface IErasable
	{
		public bool IsLandTile { get; set; }
		public bool IsStructure { get; set; }
		public bool IsBridge { get; set; }
		public bool IsLander { get; set; }
		public bool IsErasing { get; set; }
		public bool IsHumanoid { get; set; }

		public void Highlight();
		public void UnHighlight();
	}
}