using System;

namespace BB.Mobile.Net;

internal class Snapshot<T>
{
	private readonly T mContent;

	private readonly DateTime mGenerated;

	private readonly DateTime mExpiry;

	public T Content => mContent;

	public DateTime Generated => mGenerated;

	public DateTime Expiry => mExpiry;

	public bool Live => DateTime.UtcNow < mExpiry;

	public Snapshot(T content, TimeSpan timeToLive)
		: this(content, DateTime.UtcNow, timeToLive)
	{
	}

	public Snapshot(T content, DateTime generated, TimeSpan timeToLive)
		: this(content, generated, generated + timeToLive)
	{
	}

	public Snapshot(T content, DateTime generated, DateTime expiry)
	{
		if (generated > expiry)
		{
			throw new ArgumentOutOfRangeException();
		}
		mContent = content;
		mGenerated = generated;
		mExpiry = expiry;
	}
}
