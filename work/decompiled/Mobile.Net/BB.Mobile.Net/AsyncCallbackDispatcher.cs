using BB.Mobile.Net.Collections;

namespace BB.Mobile.Net;

public sealed class AsyncCallbackDispatcher : AsyncCallbackScheduler
{
	private readonly ConcurrentQueue<DeferredAsyncCallback> mPendingCallbacks = new ConcurrentQueue<DeferredAsyncCallback>();

	protected override void Schedule(DeferredAsyncCallback deferredCallback)
	{
		mPendingCallbacks.Enqueue(deferredCallback);
	}

	public int Dispatch()
	{
		int num = 0;
		DeferredAsyncCallback value;
		while (mPendingCallbacks.TryDequeue(out value))
		{
			num++;
			InvokeCallback(value);
		}
		return num;
	}
}
