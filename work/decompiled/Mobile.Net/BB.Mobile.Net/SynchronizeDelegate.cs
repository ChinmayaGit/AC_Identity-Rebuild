using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading;

namespace BB.Mobile.Net;

public class SynchronizeDelegate<T> : SynchronizeDelegate where T : class
{
	public SynchronizeDelegate(T method, ISynchronizeInvoke synchronizeInvoke)
		: base(method as Delegate, synchronizeInvoke)
	{
	}
}
public class SynchronizeDelegate
{
	private readonly ISynchronizeInvoke mSynchronizeInvoke;

	private readonly Delegate mMethod;

	public ISynchronizeInvoke SynchronizeInvoke => mSynchronizeInvoke;

	public bool InvokeRequired
	{
		get
		{
			if (mSynchronizeInvoke == null)
			{
				return false;
			}
			return mSynchronizeInvoke.InvokeRequired;
		}
	}

	public SynchronizeDelegate(Delegate method, ISynchronizeInvoke synchronizeInvoke)
	{
		if ((object)method == null)
		{
			throw new ArgumentNullException();
		}
		mMethod = method;
		mSynchronizeInvoke = synchronizeInvoke;
	}

	public IAsyncResult BeginInvoke(params object[] args)
	{
		if (mSynchronizeInvoke == null)
		{
			AsyncValueResult<object, SynchronizeDelegate> asyncResult = new AsyncValueResult<object, SynchronizeDelegate>(this);
			try
			{
				ThreadPool.QueueUserWorkItem((object argArray) =>
				{
					try
					{
						asyncResult.Complete(mMethod.DynamicInvoke((object[])argArray), synchronous: false);
					}
					catch (Exception exception2)
					{
						asyncResult.CompleteWithError(exception2, synchronous: false);
					}
				}, args);
			}
			catch (Exception exception)
			{
				asyncResult.CompleteWithError(exception, synchronous: true);
			}
			return asyncResult;
		}
		return mSynchronizeInvoke.BeginInvoke(mMethod, args);
	}

	public object EndInvoke(IAsyncResult asyncResult)
	{
		if (mSynchronizeInvoke == null)
		{
			AsyncValueResult<object, SynchronizeDelegate> asyncValueResult = AsyncValueResult<object, SynchronizeDelegate>.Verify(asyncResult, (SynchronizeDelegate context) => context == this);
			if (asyncValueResult.WaitForCompletion())
			{
				return asyncValueResult.Result;
			}
			Exception exception = asyncValueResult.Exception;
			if (exception is MemberAccessException)
			{
				throw new MemberAccessException(null, exception);
			}
			if (exception is TargetException)
			{
				throw new TargetException(null, exception);
			}
			throw new TargetInvocationException(exception);
		}
		return mSynchronizeInvoke.EndInvoke(asyncResult);
	}

	public object Invoke(params object[] args)
	{
		if (mSynchronizeInvoke == null)
		{
			return mMethod.DynamicInvoke(args);
		}
		return mSynchronizeInvoke.Invoke(mMethod, args);
	}
}
