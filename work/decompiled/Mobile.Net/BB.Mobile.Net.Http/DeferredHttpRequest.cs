using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BB.Mobile.Net.Http;

public sealed class DeferredHttpRequest : IDisposable
{
	private static readonly string pkB64 = "PFJTQUtleVZhbHVlPjxNb2R1bHVzPjVicUoyMFdiRlh0VDV4MEpiVW5FM0Z3eVkwQmdlVlBuYjFpMHZTWWlDTGxmb3NqbjBYRFpHOStOVkxLdUtLNGZLQ01aMmt3VDJxMVduck1UZHVEMUl3PT08L01vZHVsdXM+PEV4cG9uZW50PkFRQUI8L0V4cG9uZW50PjwvUlNBS2V5VmFsdWU+";

	private readonly Uri mResource;

	private readonly HttpVerb mVerb;

	private readonly string mContentType;

	private readonly IEnumerable<IHttpRequestModifier> mModifiers;

	private readonly int mTimeout;

	private WebRequest mRequest;

	private readonly MemoryStream mRequestPayloadStream;

	private AsyncValueResult<HttpResult, DeferredHttpRequest> mOverallAsyncResult;

	private readonly InterlockedObject<IPayload> mResponsePayload = new InterlockedObject<IPayload>();

	private readonly InterlockedObject<RegisteredWaitHandle> mRequestStreamTimeout = new InterlockedObject<RegisteredWaitHandle>();

	private readonly InterlockedObject<RegisteredWaitHandle> mResponseTimeout = new InterlockedObject<RegisteredWaitHandle>();

	private readonly EnumSentinel<MobileNetException.EStatus> mStatus = new EnumSentinel<MobileNetException.EStatus>(MobileNetException.EStatus.Success);

	private bool mUseSignatureVerification;

	public HttpVerb Method => mVerb;

	public int Timeout => mTimeout;

	public TimeSpan Duration => mOverallAsyncResult?.Duration ?? TimeSpan.Zero;

	public DeferredHttpRequest(Uri resource, HttpVerb verb, IEnumerable<IHttpRequestModifier> modifiers, IPayload requestPayload, IPayload responsePayload, bool useSignatureVerification, int timeout)
	{
		bool flag = false;
		try
		{
			if ((object)resource == null)
			{
				throw new ArgumentNullException();
			}
			if (verb == HttpVerb.GET && responsePayload == null)
			{
				throw new ArgumentNullException();
			}
			mResource = resource;
			mVerb = verb;
			mTimeout = timeout;
			mResponsePayload.Exchange(responsePayload);
			mModifiers = modifiers;
			mUseSignatureVerification = useSignatureVerification;
			if (requestPayload != null && (verb == HttpVerb.POST || verb == HttpVerb.PUT))
			{
				mContentType = requestPayload.ContentType;
				mRequestPayloadStream = new MemoryStream();
				requestPayload.Serialize(mRequestPayloadStream);
				flag = true;
				mRequestPayloadStream.Seek(0L, SeekOrigin.Begin);
			}
		}
		catch
		{
			responsePayload?.Generate(null);
			throw;
		}
		finally
		{
			if (!((requestPayload == null) | flag))
			{
				requestPayload.Serialize(null);
			}
		}
	}

	public IAsyncResult BeginExecute(AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler)
	{
		if (!mStatus.TryTransition(MobileNetException.EStatus.Pending, MobileNetException.EStatus.Success))
		{
			MobileNetException.EStatus state2 = mStatus.State;
			if (state2 == MobileNetException.EStatus.RequestCanceled)
			{
				throw new MobileNetException(null, null, MobileNetException.EStatus.RequestCanceled);
			}
			throw new MobileNetException(null, null, MobileNetException.EStatus.Pending);
		}
		mRequest = WebRequest.Create(mResource);
		HttpWebRequest httpWebRequest = mRequest as HttpWebRequest;
		mRequest.Timeout = mTimeout;
		mRequest.Method = mVerb.ToString();
		if (httpWebRequest != null)
		{
			httpWebRequest.KeepAlive = false;
			if (mModifiers != null)
			{
				foreach (IHttpRequestModifier mModifier in mModifiers)
				{
					mModifier.ModifyHttpRequest(httpWebRequest);
				}
			}
		}
		mOverallAsyncResult = new AsyncValueResult<HttpResult, DeferredHttpRequest>(this, callback, state, scheduler);
		if (mRequestPayloadStream != null)
		{
			mRequest.ContentType = mContentType;
			EnregisterTimeout(mRequestStreamTimeout, mRequest.BeginGetRequestStream(AcquiredRequestStream, null));
		}
		else
		{
			AcquireResponse();
		}
		return mOverallAsyncResult;
	}

	public HttpResult EndExecute(IAsyncResult asyncResult)
	{
		AsyncValueResult<HttpResult, DeferredHttpRequest> asyncValueResult = AsyncValueResult<HttpResult, DeferredHttpRequest>.Verify(asyncResult, (DeferredHttpRequest context) => context == this);
		if (asyncValueResult.WaitForCompletion())
		{
			HttpResult result = asyncValueResult.Result;
			if (result != null)
			{
				return result;
			}
			MobileNetException.EStatus eStatus = mStatus.State;
			if (eStatus == MobileNetException.EStatus.Pending)
			{
				eStatus = MobileNetException.EStatus.UnknownError;
			}
			throw new MobileNetException(null, asyncValueResult.Exception, eStatus);
		}
		if (asyncValueResult.Exception is WebException ex)
		{
			throw new MobileNetException(ex.Message, ex, (MobileNetException.EStatus)ex.Status);
		}
		throw new MobileNetException(null, asyncValueResult.Exception, MobileNetException.EStatus.UnknownError);
	}

	private bool VerifyTransmittingState()
	{
		if (mStatus.State == MobileNetException.EStatus.Pending)
		{
			return true;
		}
		mRequest.Abort();
		return false;
	}

	private void AcquiredRequestStream(IAsyncResult asyncResult)
	{
		Stream stream = null;
		try
		{
			stream = mRequest.EndGetRequestStream(asyncResult);
			if (VerifyTransmittingState())
			{
				mRequestPayloadStream.BeginCopyTo(stream, TransmittedRequestPayload, stream, ThreadPoolScheduler.DefaultScheduler);
			}
		}
		catch (Exception exception)
		{
			mOverallAsyncResult.CompleteWithError(exception, synchronous: false);
			stream?.Dispose();
		}
	}

	private void TransmittedRequestPayload(IAsyncResult asyncResult)
	{
		try
		{
			try
			{
				mRequestPayloadStream.EndCopyTo(asyncResult);
			}
			finally
			{
				((Stream)asyncResult.AsyncState).Dispose();
			}
			if (VerifyTransmittingState())
			{
				AcquireResponse();
			}
		}
		catch (Exception exception)
		{
			mOverallAsyncResult.CompleteWithError(exception, synchronous: false);
		}
	}

	private void AcquireResponse()
	{
		EnregisterTimeout(mResponseTimeout, mRequest.BeginGetResponse(AcquiredResponse, null));
	}

	private void AcquiredResponse(IAsyncResult asyncResult)
	{
		try
		{
			WebResponse webResponse;
			try
			{
				webResponse = mRequest.EndGetResponse(asyncResult);
			}
			catch (WebException ex)
			{
				if (!(ex.Response is HttpWebResponse httpWebResponse))
				{
					throw;
				}
				using (httpWebResponse)
				{
					mOverallAsyncResult.Complete(new HttpResult(httpWebResponse.StatusCode, httpWebResponse.Headers, null), synchronous: false);
					return;
				}
			}
			using (webResponse)
			{
				if (!VerifyTransmittingState())
				{
					return;
				}
				Stream stream = null;
				try
				{
					HttpWebResponse httpWebResponse2 = webResponse as HttpWebResponse;
					long num = httpWebResponse2?.ContentLength ?? (-1);
					if (mUseSignatureVerification && httpWebResponse2 != null)
					{
						stream = new MemoryStream();
						using (StreamProxy streamProxy = new StreamProxy(webResponse.GetResponseStream()))
						{
							streamProxy.CopyTo(stream);
							if (num >= 0 && streamProxy.BytesRead != num)
							{
								throw new MobileNetException(null, null, MobileNetException.EStatus.ConnectionClosed);
							}
						}
						stream.Seek(0L, SeekOrigin.Begin);
						if (!VerifySignature(httpWebResponse2, stream))
						{
							throw new MobileNetException(null, null, MobileNetException.EStatus.SignatureVerificationFailure);
						}
						stream.Seek(0L, SeekOrigin.Begin);
					}
					else
					{
						stream = webResponse.GetResponseStream();
					}
					IPayload payload = mResponsePayload.Exchange(null);
					if (payload != null)
					{
						StreamProxy streamProxy2 = new StreamProxy(stream);
						payload.Generate(streamProxy2);
						if (num >= 0 && streamProxy2.BytesRead != num)
						{
							throw new MobileNetException(null, null, MobileNetException.EStatus.ConnectionClosed);
						}
					}
					HttpResult result = ((httpWebResponse2 != null) ? new HttpResult(httpWebResponse2.StatusCode, httpWebResponse2.Headers, payload) : new HttpResult(HttpStatusCode.OK, webResponse.Headers, payload));
					mOverallAsyncResult.Complete(result, synchronous: false);
				}
				finally
				{
					stream?.Dispose();
				}
			}
		}
		catch (Exception exception)
		{
			mOverallAsyncResult.CompleteWithError(exception, synchronous: false);
		}
	}

	private bool VerifySignature(HttpWebResponse response, Stream readContentStream)
	{
		string responseHeader = response.GetResponseHeader("X-Signature");
		if (responseHeader == null || responseHeader.Length == 0)
		{
			return false;
		}
		byte[] signature = Convert.FromBase64String(responseHeader);
		byte[] array = new byte[readContentStream.Length];
		readContentStream.Read(array, 0, array.Length);
		SHA256Managed halg = new SHA256Managed();
		RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider();
		rSACryptoServiceProvider.FromXmlString(Encoding.UTF8.GetString(Convert.FromBase64String(pkB64)));
		return rSACryptoServiceProvider.VerifyData(array, halg, signature);
	}

	public void Dispose()
	{
		Dispose(MobileNetException.EStatus.RequestCanceled);
	}

	private void Dispose(MobileNetException.EStatus status)
	{
		bool flag = mStatus.TryTransition(status, MobileNetException.EStatus.Pending);
		if (!flag && !mStatus.TryTransition(status, MobileNetException.EStatus.Success))
		{
			return;
		}
		if (flag)
		{
			AsyncValueResult<HttpResult, DeferredHttpRequest> asyncValueResult = mOverallAsyncResult;
			if (asyncValueResult != null)
			{
				try
				{
					asyncValueResult.Complete(null, synchronous: false);
				}
				catch (InvalidOperationException)
				{
				}
				mRequest?.Abort();
			}
		}
		DeregisterTimeout(mRequestStreamTimeout);
		DeregisterTimeout(mResponseTimeout);
		mResponsePayload.Exchange(null)?.Generate(null);
		mRequestPayloadStream?.Dispose();
	}

	private void TimedOutOrSignaled(object state, bool timedOut)
	{
		if (timedOut)
		{
			Dispose(MobileNetException.EStatus.Timeout);
		}
		DeregisterTimeout((InterlockedObject<RegisteredWaitHandle>)state);
	}

	private void EnregisterTimeout(InterlockedObject<RegisteredWaitHandle> timeout, IAsyncResult asyncResult)
	{
		RegisteredWaitHandle registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(asyncResult.AsyncWaitHandle, TimedOutOrSignaled, timeout, mTimeout, executeOnlyOnce: true);
		if (timeout.CompareExchange(registeredWaitHandle, null) != null)
		{
			registeredWaitHandle.Unregister(null);
		}
		else if (mStatus.State != MobileNetException.EStatus.Pending)
		{
			DeregisterTimeout(timeout);
		}
	}

	private static void DeregisterTimeout(InterlockedObject<RegisteredWaitHandle> timeout)
	{
		timeout.Exchange(null)?.Unregister(null);
	}
}
