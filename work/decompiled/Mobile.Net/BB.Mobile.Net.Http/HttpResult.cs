using System.Net;

namespace BB.Mobile.Net.Http;

public sealed class HttpResult
{
	private readonly HttpStatusCode mStatusCode;

	private readonly IPayload mPayload;

	private readonly WebHeaderCollection mHeaders;

	public HttpStatusCode StatusCode => mStatusCode;

	public WebHeaderCollection Headers => mHeaders;

	public IPayload Payload => mPayload;

	public bool Succeeded
	{
		get
		{
			if (mStatusCode < HttpStatusCode.MultipleChoices)
			{
				return mStatusCode >= HttpStatusCode.OK;
			}
			return false;
		}
	}

	public HttpResult(HttpStatusCode statusCode, WebHeaderCollection headers, IPayload payload)
	{
		mStatusCode = statusCode;
		mHeaders = headers;
		mPayload = payload;
	}
}
