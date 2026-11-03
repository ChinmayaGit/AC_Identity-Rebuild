using System;
using System.Net;

namespace BB.Mobile.Net;

public sealed class WebProxyBypassWrapper : IWebProxy
{
	private const string mHackProxyUri = "127.0.0.1:8888";

	private readonly IWebProxy mWebProxy;

	private readonly WebProxy mBypassList;

	public IWebProxy WebProxy => mWebProxy;

	public string[] AdditionalBypassList => mBypassList.BypassList;

	public ICredentials Credentials
	{
		get
		{
			return mWebProxy.Credentials;
		}
		set
		{
			mWebProxy.Credentials = value;
		}
	}

	public WebProxyBypassWrapper(IWebProxy webProxy, string[] bypassList)
	{
		if (webProxy == null)
		{
			throw new ArgumentNullException();
		}
		mWebProxy = webProxy;
		mBypassList = new WebProxy("127.0.0.1:8888");
		if (bypassList != null)
		{
			mBypassList.BypassList = bypassList;
		}
	}

	public Uri GetProxy(Uri destination)
	{
		if (mBypassList.IsBypassed(destination))
		{
			return destination;
		}
		return mWebProxy.GetProxy(destination);
	}

	public bool IsBypassed(Uri host)
	{
		if (mWebProxy.IsBypassed(host))
		{
			return true;
		}
		return mBypassList.IsBypassed(host);
	}
}
