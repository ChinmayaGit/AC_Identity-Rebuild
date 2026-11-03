using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using BB.Mobile.Net.Collections;

namespace BB.Mobile.Net;

public sealed class SynchronizeInvokeDispatcher : IExtendedSynchronizeInvoke, ISynchronizeInvoke
{
	private readonly ConcurrentQueue<AsyncValueResult<object, KeyValuePair<Delegate, object[]>>> mPendingDelegates = new ConcurrentQueue<AsyncValueResult<object, KeyValuePair<Delegate, object[]>>>();

	private volatile int mOwningThreadId = -1;

	public bool InvokeRequired => Thread.CurrentThread.ManagedThreadId != mOwningThreadId;

	public SynchronizeInvokeDispatcher(bool takeOwnership = true)
	{
		if (takeOwnership)
		{
			TakeOwnership();
		}
	}

	public IAsyncResult BeginInvoke(Delegate method, object[] args)
	{
		return BeginInvoke(method, args, null, null, null);
	}

	public IAsyncResult BeginInvoke(Delegate method, object[] args, AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler)
	{
		AsyncValueResult<object, KeyValuePair<Delegate, object[]>> asyncValueResult = new AsyncValueResult<object, KeyValuePair<Delegate, object[]>>(new KeyValuePair<Delegate, object[]>(method, args), callback, state, scheduler);
		if ((object)method == null)
		{
			asyncValueResult.Complete(null, synchronous: true);
		}
		else
		{
			mPendingDelegates.Enqueue(asyncValueResult);
		}
		return asyncValueResult;
	}

	public object EndInvoke(IAsyncResult asyncResult)
	{
		AsyncValueResult<object, KeyValuePair<Delegate, object[]>> asyncValueResult = AsyncValueResult<object, KeyValuePair<Delegate, object[]>>.Verify(asyncResult);
		asyncValueResult.End();
		return asyncValueResult.Result;
	}

	public object Invoke(Delegate method, object[] args)
	{
		if (InvokeRequired)
		{
			return EndInvoke(BeginInvoke(method, args));
		}
		return method.DynamicInvoke(args);
	}

	public void TakeOwnership()
	{
		mOwningThreadId = Thread.CurrentThread.ManagedThreadId;
	}

	public uint Dispatch()
	{
		if (InvokeRequired)
		{
			throw new InvalidOperationException();
		}
		uint num = 0u;
		AsyncValueResult<object, KeyValuePair<Delegate, object[]>> value;
		while (mPendingDelegates.TryDequeue(out value))
		{
			try
			{
				AsyncValueResult<object, KeyValuePair<Delegate, object[]>> asyncValueResult = value;
				asyncValueResult.Complete(asyncValueResult.Context.Key.DynamicInvoke(value.Context.Value), synchronous: false);
			}
			catch (Exception exception)
			{
				value.CompleteWithError(exception, synchronous: false);
			}
			num++;
		}
		return num;
	}
}
