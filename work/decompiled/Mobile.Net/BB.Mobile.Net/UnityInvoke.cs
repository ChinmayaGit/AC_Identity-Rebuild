using UnityEngine;

namespace BB.Mobile.Net;

public sealed class UnityInvoke : MonoBehaviour
{
	private static readonly SynchronizeInvokeDispatcher mApplicationDispatcher = new SynchronizeInvokeDispatcher(takeOwnership: false);

	private static readonly AsyncCallbackDispatcher mApplicationCallbackDispatcher = new AsyncCallbackDispatcher();

	public static IExtendedSynchronizeInvoke Application => mApplicationDispatcher;

	public static AsyncCallbackDispatcher ApplicationCallbackDispatcher => mApplicationCallbackDispatcher;

	private void Awake()
	{
		mApplicationDispatcher.TakeOwnership();
	}

	private void Update()
	{
		mApplicationDispatcher.Dispatch();
		mApplicationCallbackDispatcher.Dispatch();
	}
}
