using System;

namespace BB.Mobile.Net;

public sealed class ContentTimeToLive<T>
{
	private readonly T mContent;

	private readonly TimeSpan mMaximumTimeToLive = TimeSpan.MaxValue;

	public T Content => mContent;

	public TimeSpan MaximumTimeToLive => mMaximumTimeToLive;

	public ContentTimeToLive(T content)
	{
		mContent = content;
	}

	public ContentTimeToLive(T content, TimeSpan maximumTimeToLive)
	{
		mContent = content;
		mMaximumTimeToLive = maximumTimeToLive;
	}
}
