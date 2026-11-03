using System;
using System.ComponentModel;
using ProtoBuf;

namespace AcierProtocol;

[Serializable]
[ProtoContract(Name = "AuthAccountResponse")]
public class AuthAccountResponse : IExtensible
{
	private string _identity = "";

	private IExtension extensionObject;

	[ProtoMember(1, IsRequired = false, Name = "identity", DataFormat = DataFormat.Default)]
	[DefaultValue("")]
	public string identity
	{
		get
		{
			return _identity;
		}
		set
		{
			_identity = value;
		}
	}

	IExtension IExtensible.GetExtensionObject(bool createIfMissing)
	{
		return Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
	}
}
