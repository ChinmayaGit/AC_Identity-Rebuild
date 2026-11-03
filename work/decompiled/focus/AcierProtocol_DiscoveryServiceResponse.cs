using System;
using System.ComponentModel;
using ProtoBuf;

namespace AcierProtocol;

[Serializable]
[ProtoContract(Name = "DiscoveryServiceResponse")]
public class DiscoveryServiceResponse : IExtensible
{
	private string _cdnUrl = "";

	private string _serverUrl = "";

	private bool _showLowEndDeviceWarning;

	private bool _everplayEnabled;

	private string _userGeoIPCountry = "";

	private QualitySettings _qualitySettings;

	private bool _fbAchievementsEnabled;

	private bool _fbEnabled;

	private bool _supportEnabled;

	private bool _adColonyEnabled;

	private string _qualitySettingsName = "";

	private bool _cp2Enabled;

	private bool _uplayEnabled;

	private bool _weiboEnabled;

	private IExtension extensionObject;

	[ProtoMember(1, IsRequired = false, Name = "cdnUrl", DataFormat = DataFormat.Default)]
	[DefaultValue("")]
	public string cdnUrl
	{
		get
		{
			return _cdnUrl;
		}
		set
		{
			_cdnUrl = value;
		}
	}

	[ProtoMember(2, IsRequired = false, Name = "serverUrl", DataFormat = DataFormat.Default)]
	[DefaultValue("")]
	public string serverUrl
	{
		get
		{
			return _serverUrl;
		}
		set
		{
			_serverUrl = value;
		}
	}

	[ProtoMember(3, IsRequired = false, Name = "showLowEndDeviceWarning", DataFormat = DataFormat.Default)]
	[DefaultValue(false)]
	public bool showLowEndDeviceWarning
	{
		get
		{
			return _showLowEndDeviceWarning;
		}
		set
		{
			_showLowEndDeviceWarning = value;
		}
	}

	[ProtoMember(4, IsRequired = false, Name = "everplayEnabled", DataFormat = DataFormat.Default)]
	[DefaultValue(false)]
	public bool everplayEnabled
	{
		get
		{
			return _everplayEnabled;
		}
		set
		{
			_everplayEnabled = value;
		}
	}

	[ProtoMember(5, IsRequired = false, Name = "userGeoIPCountry", DataFormat = DataFormat.Default)]
	[DefaultValue("")]
	public string userGeoIPCountry
	{
		get
		{
			return _userGeoIPCountry;
		}
		set
		{
			_userGeoIPCountry = value;
		}
	}

	[ProtoMember(6, IsRequired = false, Name = "qualitySettings", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public QualitySettings qualitySettings
	{
		get
		{
			return _qualitySettings;
		}
		set
		{
			_qualitySettings = value;
		}
	}

	[ProtoMember(7, IsRequired = false, Name = "fbAchievementsEnabled", DataFormat = DataFormat.Default)]
	[DefaultValue(false)]
	public bool fbAchievementsEnabled
	{
		get
		{
			return _fbAchievementsEnabled;
		}
		set
		{
			_fbAchievementsEnabled = value;
		}
	}

	[ProtoMember(8, IsRequired = false, Name = "fbEnabled", DataFormat = DataFormat.Default)]
	[DefaultValue(false)]
	public bool fbEnabled
	{
		get
		{
			return _fbEnabled;
		}
		set
		{
			_fbEnabled = value;
		}
	}

	[ProtoMember(9, IsRequired = false, Name = "supportEnabled", DataFormat = DataFormat.Default)]
	[DefaultValue(false)]
	public bool supportEnabled
	{
		get
		{
			return _supportEnabled;
		}
		set
		{
			_supportEnabled = value;
		}
	}

	[ProtoMember(10, IsRequired = false, Name = "adColonyEnabled", DataFormat = DataFormat.Default)]
	[DefaultValue(false)]
	public bool adColonyEnabled
	{
		get
		{
			return _adColonyEnabled;
		}
		set
		{
			_adColonyEnabled = value;
		}
	}

	[ProtoMember(11, IsRequired = false, Name = "qualitySettingsName", DataFormat = DataFormat.Default)]
	[DefaultValue("")]
	public string qualitySettingsName
	{
		get
		{
			return _qualitySettingsName;
		}
		set
		{
			_qualitySettingsName = value;
		}
	}

	[ProtoMember(12, IsRequired = false, Name = "cp2Enabled", DataFormat = DataFormat.Default)]
	[DefaultValue(false)]
	public bool cp2Enabled
	{
		get
		{
			return _cp2Enabled;
		}
		set
		{
			_cp2Enabled = value;
		}
	}

	[ProtoMember(13, IsRequired = false, Name = "uplayEnabled", DataFormat = DataFormat.Default)]
	[DefaultValue(false)]
	public bool uplayEnabled
	{
		get
		{
			return _uplayEnabled;
		}
		set
		{
			_uplayEnabled = value;
		}
	}

	[ProtoMember(14, IsRequired = false, Name = "weiboEnabled", DataFormat = DataFormat.Default)]
	[DefaultValue(false)]
	public bool weiboEnabled
	{
		get
		{
			return _weiboEnabled;
		}
		set
		{
			_weiboEnabled = value;
		}
	}

	IExtension IExtensible.GetExtensionObject(bool createIfMissing)
	{
		return Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
	}
}
