using System.Threading;

namespace BB.Mobile.Net;

public sealed class ThreadPoolScheduler : AsyncCallbackScheduler
{
	private static ThreadPoolScheduler m_defaultScheduler = new ThreadPoolScheduler();

	public static ThreadPoolScheduler DefaultScheduler => m_defaultScheduler;

	protected override void Schedule(DeferredAsyncCallback deferredCallback)
	{
		ThreadPool.QueueUserWorkItem(ThreadPoolCallback, deferredCallback);
	}

	private void ThreadPoolCallback(object state)
	{
		InvokeCallback((DeferredAsyncCallback)state);
	}
}
