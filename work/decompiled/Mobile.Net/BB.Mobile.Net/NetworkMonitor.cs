using System;
using BB.Mobile.Net.Http;
using UnityEngine;

namespace BB.Mobile.Net;

public static class NetworkMonitor
{
	private delegate void NetworkReachabilityCallback(AsyncValueResult<ContentTimeToLive<int>, AsyncReachabilityResultContext> overallAsyncResult);

	private delegate object DynamicInvoke(params object[] args);

	private static readonly TimeSpan mCacheTimeToLive = TimeSpan.FromSeconds(30.0);

	private static EventHandler<InternetReachabilityEventArgs> mInternetReachabilityChanged;

	private static ReachabilityLevel mInternetReachability = ReachabilityLevel.None;

	private static readonly InterlockedObject<Func<bool>> mRepairConnection = new InterlockedObject<Func<bool>>();

	private static readonly Uri mInternetEndpoint = new Uri("https://ac-id.ubi.com/");

	private static readonly Uri mResource = new Uri(".", UriKind.Relative);

	private static readonly HttpClient mPingClient = new HttpClient(mInternetEndpoint, null, useSignatureVerification: false, 60000, 1);

	private static readonly Cache<int> mInternetReachabilityCache = new Cache<int>(mCacheTimeToLive, BeginCacheDetermineInternetReachability, EndCacheDetermineInternetReachability);

	public static event EventHandler<InternetReachabilityEventArgs> InternetReachabilityChanged
	{
		add
		{
			mInternetReachabilityChanged = (EventHandler<InternetReachabilityEventArgs>)Delegate.Combine(mInternetReachabilityChanged, value);
		}
		remove
		{
			mInternetReachabilityChanged = (EventHandler<InternetReachabilityEventArgs>)Delegate.Remove(mInternetReachabilityChanged, value);
		}
	}

	public static void EnregisterRepairConnection(Func<bool> repairConnection)
	{
		if (mRepairConnection.CompareExchange(repairConnection, null) != null)
		{
			throw new InvalidOperationException("A repair connection delegate is already registered");
		}
	}

	public static void DeregisterRepairConnection(Func<bool> repairConnection)
	{
		if ((object)mRepairConnection.CompareExchange(null, repairConnection) != repairConnection)
		{
			throw new InvalidOperationException("The repair connection delegate is not already registered");
		}
	}

	public static IAsyncResult BeginDetermineInternetReachability(AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler)
	{
		return mInternetReachabilityCache.BeginRead(callback, state, scheduler);
	}

	public static ReachabilityLevel EndDetermineInternetReachability(IAsyncResult asyncResult)
	{
		return (ReachabilityLevel)mInternetReachabilityCache.EndRead(asyncResult);
	}

	private static IAsyncResult BeginCacheDetermineInternetReachability(AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler)
	{
		AsyncValueResult<ContentTimeToLive<int>, AsyncReachabilityResultContext> asyncValueResult = new AsyncValueResult<ContentTimeToLive<int>, AsyncReachabilityResultContext>(new AsyncReachabilityResultContext
		{
			AttemptRepair = true
		}, callback, state, scheduler);
		if (UnityInvoke.Application.InvokeRequired)
		{
			UnityInvoke.Application.BeginInvoke(new NetworkReachabilityCallback(HandleNetworkReachability), new object[1] { asyncValueResult }, InternetReachabilityInvoked, asyncValueResult, ThreadPoolScheduler.DefaultScheduler);
		}
		else
		{
			HandleNetworkReachability(asyncValueResult);
		}
		return asyncValueResult;
	}

	private static void InternetReachabilityInvoked(IAsyncResult asyncResult)
	{
		AsyncValueResult<ContentTimeToLive<int>, AsyncReachabilityResultContext> asyncValueResult = (AsyncValueResult<ContentTimeToLive<int>, AsyncReachabilityResultContext>)asyncResult.AsyncState;
		try
		{
			UnityInvoke.Application.EndInvoke(asyncResult);
		}
		catch (Exception exception)
		{
			asyncValueResult.CompleteWithError(exception, synchronous: false);
		}
	}

	private static void HandleNetworkReachability(AsyncValueResult<ContentTimeToLive<int>, AsyncReachabilityResultContext> overallAsyncResult)
	{
		NetworkReachability internetReachability = Application.internetReachability;
		overallAsyncResult.Context.InternetReachability = internetReachability;
		if (internetReachability != NetworkReachability.NotReachable)
		{
			mPingClient.BeginReadHeadRequest(mResource, null, AcquiredInternetReachabilityResponse, overallAsyncResult, null);
		}
		else
		{
			overallAsyncResult.Complete(new ContentTimeToLive<int>(0, TimeSpan.Zero), synchronous: true);
		}
	}

	private static void AcquiredInternetReachabilityResponse(IAsyncResult asyncResult)
	{
		AsyncValueResult<ContentTimeToLive<int>, AsyncReachabilityResultContext> asyncValueResult = (AsyncValueResult<ContentTimeToLive<int>, AsyncReachabilityResultContext>)asyncResult.AsyncState;
		ReachabilityLevel content = ReachabilityLevel.Local;
		try
		{
			mPingClient.EndRequest(asyncResult);
			content = ((asyncValueResult.Context.InternetReachability == NetworkReachability.ReachableViaCarrierDataNetwork) ? ReachabilityLevel.InternetViaDataCarrier : ReachabilityLevel.Internet);
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
			if (asyncValueResult.Context.AttemptRepair)
			{
				asyncValueResult.Context.AttemptRepair = false;
				Func<bool> value = mRepairConnection.Value;
				if (value != null)
				{
					try
					{
						if (value())
						{
							mPingClient.BeginReadHeadRequest(mResource, null, AcquiredInternetReachabilityResponse, asyncValueResult, null);
							return;
						}
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
					}
				}
			}
		}
		asyncValueResult.Complete(new ContentTimeToLive<int>((int)content), synchronous: false);
	}

	private static ContentTimeToLive<int> EndCacheDetermineInternetReachability(IAsyncResult asyncResult)
	{
		AsyncValueResult<ContentTimeToLive<int>, AsyncReachabilityResultContext> asyncValueResult = AsyncValueResult<ContentTimeToLive<int>, AsyncReachabilityResultContext>.Verify(asyncResult);
		asyncValueResult.End();
		ReachabilityLevel content = (ReachabilityLevel)asyncValueResult.Result.Content;
		if (mInternetReachability != content)
		{
			mInternetReachability = content;
			EventHandler<InternetReachabilityEventArgs> eventHandler = mInternetReachabilityChanged;
			if (eventHandler != null)
			{
				InternetReachabilityEventArgs e = new InternetReachabilityEventArgs(content);
				UnityInvoke.Application.BeginInvoke(new DynamicInvoke(eventHandler.DynamicInvoke), new object[2] { null, e }, ReachabilityChangedInvoked, null, ThreadPoolScheduler.DefaultScheduler);
			}
		}
		return asyncValueResult.Result;
	}

	private static void ReachabilityChangedInvoked(IAsyncResult asyncResult)
	{
		try
		{
			UnityInvoke.Application.EndInvoke(asyncResult);
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}
}
