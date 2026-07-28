using UnityEngine;

namespace UI
{
	public interface IIconCacheable
	{
		public void CacheIcon();
		public RenderTexture GetCachedIcon();
	}
}