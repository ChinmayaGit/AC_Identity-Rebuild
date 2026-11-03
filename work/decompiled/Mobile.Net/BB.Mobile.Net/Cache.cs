using System;

namespace BB.Mobile.Net;

public sealed class Cache<T> : ICache
{
	public delegate IAsyncResult BeginFetch(AsyncCallback _asyncCallback, object _state, IAsyncCallbackScheduler _scheduler);

	public delegate ContentTimeToLive<T> EndFetch(IAsyncResult _asyncResult);

	private delegate void AsyncResultMonitor(AsyncValueResult<T, Cache<T>> _observedAsyncResult, AsyncValueResult<T, Cache<T>> _observingAsyncResult);

	private static readonly Snapshot<T> ExpiredSnapshot = new Snapshot<T>(default, TimeSpan.Zero);

	private readonly BeginFetch mBeginFetch;

	private readonly EndFetch mEndFetch;

	private readonly TimeSpan mTimeToLive;

	private volatile Snapshot<T> mContentSnapshot = ExpiredSnapshot;

	private InterlockedObject<AsyncValueResult<T, Cache<T>>> mContentAsyncResult = new InterlockedObject<AsyncValueResult<T, Cache<T>>>();

	private static readonly AsyncResultMonitor mFetchedObserved = FetchedObserved;

	public TimeSpan TimeToLive => mTimeToLive;

	public DateTime Expiry => mContentSnapshot.Expiry;

	public Cache(TimeSpan _maxLifespan, BeginFetch _beginFetch, EndFetch _endFetch)
	{
		if (_beginFetch == null || _endFetch == null)
		{
			throw new ArgumentNullException();
		}
		if (_maxLifespan < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException();
		}
		mBeginFetch = _beginFetch;
		mEndFetch = _endFetch;
		mTimeToLive = _maxLifespan;
	}

	public void Expire()
	{
		mContentSnapshot = ExpiredSnapshot;
	}

	public void Prefetch()
	{
		BeginRead(Prefetched, null, ThreadPoolScheduler.DefaultScheduler);
	}

	private void Prefetched(IAsyncResult _asyncResult)
	{
		try
		{
			EndRead(_asyncResult);
		}
		catch (Exception)
		{
		}
	}

	public bool TryUpdate(T _content)
	{
		return TryUpdate(_content, DateTime.UtcNow);
	}

	public bool TryUpdate(T _content, DateTime _generated)
	{
		return TryUpdate(new Snapshot<T>(_content, _generated, mTimeToLive));
	}

	private bool TryUpdate(Snapshot<T> _snapshot)
	{
		if (!_snapshot.Live)
		{
			return false;
		}
		AsyncValueResult<T, Cache<T>> asyncValueResult = new AsyncValueResult<T, Cache<T>>(this);
		if (mContentAsyncResult.CompareExchange(asyncValueResult, null) != null)
		{
			return false;
		}
		try
		{
			Snapshot<T> snapshot = mContentSnapshot;
			if (!snapshot.Live || _snapshot.Generated > snapshot.Generated)
			{
				mContentSnapshot = _snapshot;
				asyncValueResult.Complete(_snapshot.Content, synchronous: true);
				return true;
			}
			asyncValueResult.Complete(snapshot.Content, synchronous: true);
			return false;
		}
		finally
		{
			mContentAsyncResult.Exchange(null);
		}
	}

	public bool TryRead(out T _content)
	{
		Snapshot<T> snapshot = mContentSnapshot;
		if (snapshot.Live)
		{
			_content = snapshot.Content;
			return true;
		}
		_content = default;
		return false;
	}

	public IAsyncResult BeginRead(AsyncCallback _callback, object _state, IAsyncCallbackScheduler _scheduler)
	{
		AsyncValueResult<T, Cache<T>> asyncValueResult = new AsyncValueResult<T, Cache<T>>(this, _callback, _state, _scheduler);
		AsyncValueResult<T, Cache<T>> asyncValueResult2 = mContentAsyncResult.CompareExchange(asyncValueResult, null);
		bool flag = asyncValueResult2 == null;
		Snapshot<T> snapshot = mContentSnapshot;
		if (snapshot.Live)
		{
			asyncValueResult.Complete(snapshot.Content, synchronous: true);
			if (flag)
			{
				mContentAsyncResult.Exchange(null);
			}
		}
		else if (flag)
		{
			mBeginFetch(Fetched, asyncValueResult, ThreadPoolScheduler.DefaultScheduler);
		}
		else
		{
			new WaitHandleMonitor(asyncValueResult2, mFetchedObserved, asyncValueResult2, asyncValueResult);
		}
		return asyncValueResult;
	}

	public T EndRead(IAsyncResult _asyncResult)
	{
		AsyncValueResult<T, Cache<T>> asyncValueResult = AsyncValueResult<T, Cache<T>>.Verify(_asyncResult, (Cache<T> context) => context == this);
		asyncValueResult.End();
		return asyncValueResult.Result;
	}

	private void Fetched(IAsyncResult _asyncResult)
	{
		AsyncValueResult<T, Cache<T>> asyncValueResult = (AsyncValueResult<T, Cache<T>>)_asyncResult.AsyncState;
		try
		{
			ContentTimeToLive<T> contentTimeToLive = mEndFetch(_asyncResult);
			mContentSnapshot = new Snapshot<T>(contentTimeToLive.Content, (contentTimeToLive.MaximumTimeToLive < mTimeToLive) ? contentTimeToLive.MaximumTimeToLive : mTimeToLive);
			asyncValueResult.Complete(contentTimeToLive.Content, synchronous: false);
		}
		catch (Exception exception)
		{
			asyncValueResult.CompleteWithError(exception, synchronous: false);
		}
		finally
		{
			mContentAsyncResult.Exchange(null);
		}
	}

	private static void FetchedObserved(AsyncValueResult<T, Cache<T>> _fetchAsyncResult, AsyncValueResult<T, Cache<T>> _overallAsyncResult)
	{
		if (_fetchAsyncResult.Exception == null)
		{
			_overallAsyncResult.Complete(_fetchAsyncResult.Result, synchronous: false);
		}
		else
		{
			_overallAsyncResult.CompleteWithError(_fetchAsyncResult.Exception, synchronous: false);
		}
	}
}
