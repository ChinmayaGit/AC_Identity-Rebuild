using System;

namespace BB.Mobile.Net;

public sealed class InternetReachabilityEventArgs : EventArgs
{
	private readonly ReachabilityLevel mReachability;

	public ReachabilityLevel Level => mReachability;

	public InternetReachabilityEventArgs(ReachabilityLevel reachability)
	{
		mReachability = reachability;
	}
}
