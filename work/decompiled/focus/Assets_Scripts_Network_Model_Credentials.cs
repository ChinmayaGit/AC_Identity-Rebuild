using System.Collections.Generic;
using MiniJSON;

namespace Assets.Scripts.Network.Model;

public class Credentials : IJsonExternizable
{
	public string Username { get; set; }

	public string Password { get; set; }

	public void ReadExternal(string _data)
	{
		object obj = Json.Deserialize(_data);
		if (obj is Dictionary<string, object> dictionary && dictionary.ContainsKey("u") && dictionary.ContainsKey("p"))
		{
			Username = dictionary["u"] as string;
			Password = dictionary["p"] as string;
		}
	}

	public void WriteExternal(out string _data)
	{
		if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
		{
			Dictionary<string, object> dictionary = new Dictionary<string, object>(2);
			dictionary.Add("u", Username);
			dictionary.Add("p", Password);
			Dictionary<string, object> obj = dictionary;
			_data = Json.Serialize(obj);
		}
		else
		{
			_data = null;
		}
	}
}
