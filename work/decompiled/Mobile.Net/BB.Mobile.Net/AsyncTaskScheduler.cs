using System;
using System.Threading;
using BB.Mobile.Net.Collections;

namespace BB.Mobile.Net;

public sealed class AsyncTaskScheduler<T>
{
	public delegate void BeginRequest(T _task, AsyncCallback _asyncCall, object _state);

	public delegate void EndRequest(T _task, IAsyncResult _requestAsyncResult);

	public delegate void HandleRequestException(T _task, Exception _requestException);

	private readonly Semaphore mRequestsSemaphore;

	private readonly ConcurrentQueue<T> mRequests = new ConcurrentQueue<T>();

	private readonly BeginRequest mBeginRequest;

	private readonly EndRequest mEndRequest;

	private readonly HandleRequestException mHandleRequestException;

	private EventHandler mDepletedEventHandler;

	public event EventHandler Depleted
	{
		add
		{
			mDepletedEventHandler = (EventHandler)Delegate.Combine(mDepletedEventHandler, value);
		}
		remove
		{
			mDepletedEventHandler = (EventHandler)Delegate.Remove(mDepletedEventHandler, value);
		}
	}

	public AsyncTaskScheduler(BeginRequest beginRequest, EndRequest endRequest, HandleRequestException handleRequestException, ushort maximumConcurrentResults = 1)
	{
		if (beginRequest == null || endRequest == null || handleRequestException == null)
		{
			throw new ArgumentNullException();
		}
		mRequestsSemaphore = new Semaphore(maximumConcurrentResults, maximumConcurrentResults);
		mBeginRequest = beginRequest;
		mEndRequest = endRequest;
		mHandleRequestException = handleRequestException;
	}

	public void Schedule(T task)
	{
		mRequests.Enqueue(task);
		ProcessConcurrentQueue(holdsSemaphore: false);
	}

	private void ProcessConcurrentQueue(bool holdsSemaphore)
	{
		if (!holdsSemaphore && !mRequestsSemaphore.WaitOne(0))
		{
			return;
		}
		T value;
		while (mRequests.TryDequeue(out value))
		{
			try
			{
				mBeginRequest(value, RequestExecuted, value);
				return;
			}
			catch (Exception requestException)
			{
				mHandleRequestException(value, requestException);
			}
		}
		if (holdsSemaphore)
		{
			mDepletedEventHandler?.Invoke(this, EventArgs.Empty);
		}
		mRequestsSemaphore.Release();
	}

	private void RequestExecuted(IAsyncResult requestAsyncResult)
	{
		try
		{
			T task = (T)requestAsyncResult.AsyncState;
			try
			{
				mEndRequest(task, requestAsyncResult);
			}
			catch (Exception requestException)
			{
				mHandleRequestException(task, requestException);
			}
		}
		finally
		{
			ProcessConcurrentQueue(holdsSemaphore: true);
		}
	}
}
