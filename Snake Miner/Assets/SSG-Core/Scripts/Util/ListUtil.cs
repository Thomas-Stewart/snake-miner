namespace SSG_Core.Scripts.Util
{
	public static class ListUtil
	{
		public static int NavigateBoundedIndex(int curValue, bool shouldMovePositive, int listSize)
		{
			curValue += shouldMovePositive ? 1 : -1;
			if (curValue < 0)
				curValue = listSize - 1;
			if (curValue >= listSize)
				curValue = 0;

			return curValue;
		}
	}
}