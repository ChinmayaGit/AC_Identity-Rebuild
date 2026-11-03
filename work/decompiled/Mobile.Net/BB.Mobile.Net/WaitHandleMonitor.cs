using System;
using System.Threading;

namespace BB.Mobile.Net;

public sealed class WaitHandleMonitor
{
	private volatile bool mCompleted;

	private readonly Delegate mMethod;

	private readonly object[] mArgs;

	private InterlockedObject<RegisteredWaitHandle> mRegisteredWaitHandle = new InterlockedObject<RegisteredWaitHandle>();

	public bool Completed => mCompleted;

	public WaitHandleMonitor(IAsyncResult asyncResult, Delegate method, params object[] args)
		: this(asyncResult?.AsyncWaitHandle, method, args)
	{
	}

	public WaitHandleMonitor(WaitHandle waitHandle, Delegate method, params object[] args)
	{
		if (waitHandle == null || (object)method == null)
		{
			throw new ArgumentNullException();
		}
		mMethod = method;
		mArgs = args;
		RegisteredWaitHandle value = ThreadPool.RegisterWaitForSingleObject(waitHandle, WaitHandleCompleted, null, -1, executeOnlyOnce: true);
		mRegisteredWaitHandle.Exchange(value);
		if (mCompleted)
		{
			UnregisterWaitHandle();
		}
	}

	private void WaitHandleCompleted(object state, bool timedOut)
	{
		mCompleted = true;
		UnregisterWaitHandle();
		mMethod.DynamicInvoke(mArgs);
	}

	private void UnregisterWaitHandle()
	{
		mRegisteredWaitHandle.Exchange(null)?.Unregister(null);
	}
}
