using System;
using System.Collections.Generic;

namespace BB.Mobile.Net.Http;

public sealed class HttpClient
{
	private readonly Uri mBoundEndpoint;

	private readonly AsyncTaskScheduler<AsyncValueResult<HttpResult, OwnerContext<HttpClient, DeferredHttpRequest>>> mRequestScheduler;

	private readonly IAsyncCallbackScheduler mScheduler;

	private readonly bool mUseSignatureVerification;

	private EventHandler mFaultedEventHandler;

	private int mRequestTimeout;

	public Uri BoundEndpoint => mBoundEndpoint;

	public int RequestTimeout
	{
		get
		{
			return mRequestTimeout;
		}
		set
		{
			if (value == -1)
			{
				throw new ArgumentOutOfRangeException();
			}
			mRequestTimeout = value;
		}
	}

	public event EventHandler Faulted
	{
		add
		{
			mFaultedEventHandler = (EventHandler)Delegate.Combine(mFaultedEventHandler, value);
		}
		remove
		{
			mFaultedEventHandler = (EventHandler)Delegate.Remove(mFaultedEventHandler, value);
		}
	}

	public HttpClient(Uri boundEndpoint = null, IAsyncCallbackScheduler scheduler = null, bool useSignatureVerification = false, int requestTimeout = 60000, ushort maximumConcurrentRequests = 1)
	{
		if (requestTimeout == -1 || maximumConcurrentRequests <= 0)
		{
			throw new ArgumentOutOfRangeException();
		}
		if ((object)boundEndpoint != null)
		{
			if (!boundEndpoint.IsAbsoluteUri)
			{
				throw new ArgumentOutOfRangeException("boundEndpoint is not an absolute URI");
			}
			mBoundEndpoint = boundEndpoint;
		}
		mRequestScheduler = new AsyncTaskScheduler<AsyncValueResult<HttpResult, OwnerContext<HttpClient, DeferredHttpRequest>>>(BeginTransmission, EndTransmission, HandleTransmissionException, maximumConcurrentRequests);
		mUseSignatureVerification = useSignatureVerification;
		mRequestTimeout = requestTimeout;
		IAsyncCallbackScheduler asyncCallbackScheduler;
		if (scheduler != null)
		{
			asyncCallbackScheduler = scheduler;
		}
		else
		{
			IAsyncCallbackScheduler defaultScheduler = ThreadPoolScheduler.DefaultScheduler;
			asyncCallbackScheduler = defaultScheduler;
		}
		mScheduler = asyncCallbackScheduler;
		mScheduler.InvocationException += HandleCallbackException;
	}

	public IAsyncResult BeginCreateRequest(Uri resource, IEnumerable<IHttpRequestModifier> modifiers, IPayload requestPayload, IPayload responsePayload, AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler)
	{
		return BeginRequest(resource, HttpVerb.POST, modifiers, requestPayload, responsePayload, callback, state, scheduler);
	}

	public IAsyncResult BeginReadRequest(Uri resource, IEnumerable<IHttpRequestModifier> modifiers, IPayload responsePayload, AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler)
	{
		return BeginRequest(resource, HttpVerb.GET, modifiers, null, responsePayload, callback, state, scheduler);
	}

	public IAsyncResult BeginUpdateRequest(Uri resource, IEnumerable<IHttpRequestModifier> modifiers, IPayload requestPayload, IPayload responsePayload, AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler)
	{
		return BeginRequest(resource, HttpVerb.PUT, modifiers, requestPayload, responsePayload, callback, state, scheduler);
	}

	public IAsyncResult BeginDeleteRequest(Uri resource, IEnumerable<IHttpRequestModifier> modifiers, IPayload responsePayload, AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler)
	{
		return BeginRequest(resource, HttpVerb.DELETE, modifiers, null, responsePayload, callback, state, scheduler);
	}

	public IAsyncResult BeginReadHeadRequest(Uri resource, IEnumerable<IHttpRequestModifier> modifiers, AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler)
	{
		return BeginRequest(resource, HttpVerb.HEAD, modifiers, null, null, callback, state, scheduler);
	}

	public IAsyncResult BeginReadRangeRequest(Uri resource, int start, int end, IEnumerable<IHttpRequestModifier> modifiers, IPayload responsePayload, AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler)
	{
		List<IHttpRequestModifier> list = ((modifiers == null) ? new List<IHttpRequestModifier>() : new List<IHttpRequestModifier>(modifiers));
		list.Add(new RangeModifier(start, end));
		return BeginRequest(resource, HttpVerb.GET, list, null, responsePayload, callback, state, scheduler);
	}

	public IAsyncResult BeginRequest(Uri resource, HttpVerb verb, IEnumerable<IHttpRequestModifier> modifiers, IPayload requestPayload, IPayload responsePayload, AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler)
	{
		AsyncValueResult<HttpResult, OwnerContext<HttpClient, DeferredHttpRequest>> asyncValueResult = new AsyncValueResult<HttpResult, OwnerContext<HttpClient, DeferredHttpRequest>>(new OwnerContext<HttpClient, DeferredHttpRequest>(this, null), callback, state, (scheduler == null) ? mScheduler : scheduler);
		try
		{
			Uri uri;
			try
			{
				if ((object)resource == null)
				{
					throw new ArgumentNullException();
				}
				if (BoundEndpoint != null)
				{
					if (resource.IsAbsoluteUri)
					{
						throw new ArgumentOutOfRangeException(string.Concat("Client is already bound to an Uri endpoint: ", BoundEndpoint, ". Uri: ", resource, " must be relative to the bound endpoint."));
					}
					uri = new Uri(mBoundEndpoint, resource);
				}
				else
				{
					if (!resource.IsAbsoluteUri)
					{
						throw new ArgumentOutOfRangeException(string.Concat("Uri: ", resource, " is not absolute."));
					}
					uri = resource;
				}
				if (uri.IsFile && (verb == HttpVerb.DELETE || verb == HttpVerb.PUT))
				{
					throw new ArgumentOutOfRangeException("Only Read(GET) and Create(POST) allowed on file resource: " + uri);
				}
			}
			catch
			{
				try
				{
					requestPayload?.Serialize(null);
				}
				finally
				{
					responsePayload?.Generate(null);
				}
				throw;
			}
			DeferredHttpRequest context = new DeferredHttpRequest(uri, verb, modifiers, requestPayload, responsePayload, mUseSignatureVerification, mRequestTimeout);
			asyncValueResult.Context.Context = context;
			mRequestScheduler.Schedule(asyncValueResult);
		}
		catch (Exception exception)
		{
			asyncValueResult.CompleteWithError(exception, synchronous: true);
		}
		return asyncValueResult;
	}

	public HttpResult EndRequest(IAsyncResult asyncResult)
	{
		AsyncValueResult<HttpResult, OwnerContext<HttpClient, DeferredHttpRequest>> asyncValueResult = AsyncValueResult<HttpResult, OwnerContext<HttpClient, DeferredHttpRequest>>.Verify(asyncResult, (OwnerContext<HttpClient, DeferredHttpRequest> context) => context.Owner == this);
		bool flag = asyncValueResult.WaitForCompletion();
		_ = asyncValueResult.Context.Context;
		if (flag)
		{
			return asyncValueResult.Result;
		}
		if (asyncValueResult.Exception is ArgumentException)
		{
			throw asyncValueResult.Exception;
		}
		if (asyncValueResult.Exception is UriFormatException)
		{
			throw asyncValueResult.Exception;
		}
		if (asyncValueResult.Exception is MobileNetException mobileNetException)
		{
			throw new MobileNetException(mobileNetException);
		}
		throw new MobileNetException(null, asyncValueResult.Exception, MobileNetException.EStatus.UnknownError);
	}

	public void AbortRequest(IAsyncResult asyncResult)
	{
		AsyncValueResult<HttpResult, OwnerContext<HttpClient, DeferredHttpRequest>>.Verify(asyncResult, (OwnerContext<HttpClient, DeferredHttpRequest> context) => context.Owner == this).Context.Context?.Dispose();
	}

	private void BeginTransmission(AsyncValueResult<HttpResult, OwnerContext<HttpClient, DeferredHttpRequest>> overallAsyncResult, AsyncCallback callback, object state)
	{
		overallAsyncResult.Context.Context.BeginExecute(callback, state, ThreadPoolScheduler.DefaultScheduler);
	}

	private void EndTransmission(AsyncValueResult<HttpResult, OwnerContext<HttpClient, DeferredHttpRequest>> overallAsyncResult, IAsyncResult taskAsyncResult)
	{
		HttpResult result = overallAsyncResult.Context.Context.EndExecute(taskAsyncResult);
		overallAsyncResult.Complete(result, synchronous: false);
	}

	private void HandleTransmissionException(AsyncValueResult<HttpResult, OwnerContext<HttpClient, DeferredHttpRequest>> overallAsyncResult, Exception requestException)
	{
		try
		{
			overallAsyncResult.CompleteWithError(requestException, synchronous: false);
		}
		catch (Exception)
		{
		}
		mFaultedEventHandler?.Invoke(this, EventArgs.Empty);
	}

	private void HandleCallbackException(object sender, AsyncCallbackExceptionEventArgs eventArgs)
	{
		if (eventArgs.AsyncResult is AsyncValueResult<HttpResult, OwnerContext<HttpClient, DeferredHttpRequest>> asyncValueResult)
		{
			_ = asyncValueResult.Context.Owner;
		}
	}
}
