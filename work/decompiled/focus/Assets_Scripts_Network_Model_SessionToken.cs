using System;

namespace Assets.Scripts.Network.Model;

public class SessionToken : IJsonExternizable
{
	protected string Token { get; private set; }

	protected int Expires { get; private set; }

	protected double Created { get; private set; }

	public SessionToken()
	{
	}

	public SessionToken(string _token, int _expires)
	{
		Token = _token;
		Expires = _expires;
		Created = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
	}

	public bool IsExpired()
	{
		double num = Created + (double)Expires;
		double totalSeconds = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
		return totalSeconds > num;
	}

	public void ReadExternal(string _data)
	{
		throw new NotImplementedException();
	}

	public void WriteExternal(out string _data)
	{
		throw new NotImplementedException();
	}
}
