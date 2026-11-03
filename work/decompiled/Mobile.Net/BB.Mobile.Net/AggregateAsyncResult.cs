using System;
using System.Collections.Generic;
using System.Threading;
using BB.Mobile.Net.Collections;

namespace BB.Mobile.Net;

public sealed class AggregateAsyncResult<C> : AsyncResult<C>
{
	private readonly EnumSentinel<AggregateAsyncResultState> mSentinel = new EnumSentinel<AggregateAsyncResultState>(AggregateAsyncResultState.Pending);

	private readonly ConcurrentQueue<RegisteredWaitHandle> mWaitHandles = new ConcurrentQueue<RegisteredWaitHandle>();

	private readonly List<IAsyncResult> mAsyncResults = new List<IAsyncResult>();

	private IList<IAsyncResult> mCompletedAsyncResults;

	private volatile int mPendingEvents = 1;

	public bool Observing => mSentinel.State >= AggregateAsyncResultState.Observing;

	public IList<IAsyncResult> Results => mCompletedAsyncResults;

	public AggregateAsyncResult(C context)
		: this(context, (AsyncCallback)null, (object)null, (IAsyncCallbackScheduler)null)
	{
	}

	public AggregateAsyncResult(C context, AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler)
		: base(context, callback, state, scheduler)
	{
	}

	public int Add(IAsyncResult asyncResult)
	{
		if (asyncResult == null)
		{
			throw new ArgumentNullException();
		}
		mSentinel.Transition(AggregateAsyncResultState.Adding, AggregateAsyncResultState.Pending);
		try
		{
			mAsyncResults.Add(asyncResult);
			int result = mAsyncResults.Count - 1;
			if (!asyncResult.IsCompleted)
			{
				Interlocked.Increment(ref mPendingEvents);
				mWaitHandles.Enqueue(ThreadPool.RegisterWaitForSingleObject(asyncResult.AsyncWaitHandle, HandleAsyncResultCompleted, null, -1, executeOnlyOnce: true));
			}
			return result;
		}
		finally
		{
			mSentinel.Transition(AggregateAsyncResultState.Pending);
		}
	}

	public void Observe()
	{
		mSentinel.Transition(AggregateAsyncResultState.Observing, AggregateAsyncResultState.Pending);
		CompleteEvent(synchronous: true);
	}

	private void HandleAsyncResultCompleted(object state, bool timedOut)
	{
		CompleteEvent(synchronous: false);
	}

	private void CompleteEvent(bool synchronous)
	{
		if (Interlocked.Decrement(ref mPendingEvents) == 0)
		{
			mCompletedAsyncResults = mAsyncResults.AsReadOnly();
			RegisteredWaitHandle value;
			while (mWaitHandles.TryDequeue(out value))
			{
				value.Unregister(null);
			}
			mSentinel.Transition(AggregateAsyncResultState.Completed);
			Complete(synchronous);
		}
	}
}
