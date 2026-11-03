using UnityEngine;

namespace BB.Mobile.Net;

internal class AsyncReachabilityResultContext
{
	public NetworkReachability InternetReachability { get; set; }

	public bool AttemptRepair { get; set; }
}
