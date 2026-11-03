using System.Net;

namespace BB.Mobile.Net.Http;

public interface IHttpRequestModifier
{
	void ModifyHttpRequest(HttpWebRequest httpRequest);
}
