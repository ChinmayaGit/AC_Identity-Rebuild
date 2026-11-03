using System;
using System.ComponentModel;
using ProtoBuf;

namespace AcierProtocol;

[Serializable]
[ProtoContract(Name = "UserProfile")]
public class UserProfile : IExtensible
{
	private double _version;

	private string _id;

	private string _userName = "";

	private int _level;

	private Inventory _Inventory;

	private Assassins _assassins;

	private string _lastUserAssassionId = "";

	private Resources _resources;

	private Counters _counters;

	private long _lastActivity;

	private int _dirtyFlags;

	private FriendProfiles _friends;

	private string _userIdHash = "";

	private int _maxFriends;

	private Achievements _achievements;

	private long _nextDailyRewardCheck;

	private PayUnlockedClasses _payUnlockedClasses;

	private Tutorial _tutorial;

	private bool _tutorialSkipped;

	private long _mysteryBoxExpireTime;

	private EntitlementInventory _entitlementInventory;

	private MissionCollection _missionCollection;

	private ProgressionQuests _progressionQuests;

	private UserSettings _userSettings;

	private int _maxRankIdx;

	private EventsProgress _eventsProgress;

	private long _maxItemPower;

	private bool _pirate;

	private IExtension extensionObject;

	[ProtoMember(1, IsRequired = true, Name = "version", DataFormat = DataFormat.TwosComplement)]
	public double version
	{
		get
		{
			return _version;
		}
		set
		{
			_version = value;
		}
	}

	[ProtoMember(2, IsRequired = true, Name = "id", DataFormat = DataFormat.Default)]
	public string id
	{
		get
		{
			return _id;
		}
		set
		{
			_id = value;
		}
	}

	[ProtoMember(3, IsRequired = false, Name = "userName", DataFormat = DataFormat.Default)]
	[DefaultValue("")]
	public string userName
	{
		get
		{
			return _userName;
		}
		set
		{
			_userName = value;
		}
	}

	[ProtoMember(4, IsRequired = false, Name = "level", DataFormat = DataFormat.TwosComplement)]
	[DefaultValue(0)]
	public int level
	{
		get
		{
			return _level;
		}
		set
		{
			_level = value;
		}
	}

	[ProtoMember(7, IsRequired = false, Name = "Inventory", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public Inventory Inventory
	{
		get
		{
			return _Inventory;
		}
		set
		{
			_Inventory = value;
		}
	}

	[ProtoMember(8, IsRequired = false, Name = "assassins", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public Assassins assassins
	{
		get
		{
			return _assassins;
		}
		set
		{
			_assassins = value;
		}
	}

	[ProtoMember(10, IsRequired = false, Name = "lastUserAssassionId", DataFormat = DataFormat.Default)]
	[DefaultValue("")]
	public string lastUserAssassionId
	{
		get
		{
			return _lastUserAssassionId;
		}
		set
		{
			_lastUserAssassionId = value;
		}
	}

	[ProtoMember(14, IsRequired = false, Name = "resources", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public Resources resources
	{
		get
		{
			return _resources;
		}
		set
		{
			_resources = value;
		}
	}

	[ProtoMember(16, IsRequired = false, Name = "counters", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public Counters counters
	{
		get
		{
			return _counters;
		}
		set
		{
			_counters = value;
		}
	}

	[ProtoMember(17, IsRequired = false, Name = "lastActivity", DataFormat = DataFormat.TwosComplement)]
	[DefaultValue(0L)]
	public long lastActivity
	{
		get
		{
			return _lastActivity;
		}
		set
		{
			_lastActivity = value;
		}
	}

	[ProtoMember(18, IsRequired = false, Name = "dirtyFlags", DataFormat = DataFormat.TwosComplement)]
	[DefaultValue(0)]
	public int dirtyFlags
	{
		get
		{
			return _dirtyFlags;
		}
		set
		{
			_dirtyFlags = value;
		}
	}

	[ProtoMember(19, IsRequired = false, Name = "friends", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public FriendProfiles friends
	{
		get
		{
			return _friends;
		}
		set
		{
			_friends = value;
		}
	}

	[ProtoMember(20, IsRequired = false, Name = "userIdHash", DataFormat = DataFormat.Default)]
	[DefaultValue("")]
	public string userIdHash
	{
		get
		{
			return _userIdHash;
		}
		set
		{
			_userIdHash = value;
		}
	}

	[ProtoMember(21, IsRequired = false, Name = "maxFriends", DataFormat = DataFormat.TwosComplement)]
	[DefaultValue(0)]
	public int maxFriends
	{
		get
		{
			return _maxFriends;
		}
		set
		{
			_maxFriends = value;
		}
	}

	[ProtoMember(22, IsRequired = false, Name = "achievements", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public Achievements achievements
	{
		get
		{
			return _achievements;
		}
		set
		{
			_achievements = value;
		}
	}

	[ProtoMember(23, IsRequired = false, Name = "nextDailyRewardCheck", DataFormat = DataFormat.TwosComplement)]
	[DefaultValue(0L)]
	public long nextDailyRewardCheck
	{
		get
		{
			return _nextDailyRewardCheck;
		}
		set
		{
			_nextDailyRewardCheck = value;
		}
	}

	[ProtoMember(27, IsRequired = false, Name = "payUnlockedClasses", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public PayUnlockedClasses payUnlockedClasses
	{
		get
		{
			return _payUnlockedClasses;
		}
		set
		{
			_payUnlockedClasses = value;
		}
	}

	[ProtoMember(29, IsRequired = false, Name = "tutorial", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public Tutorial tutorial
	{
		get
		{
			return _tutorial;
		}
		set
		{
			_tutorial = value;
		}
	}

	[ProtoMember(30, IsRequired = false, Name = "tutorialSkipped", DataFormat = DataFormat.Default)]
	[DefaultValue(false)]
	public bool tutorialSkipped
	{
		get
		{
			return _tutorialSkipped;
		}
		set
		{
			_tutorialSkipped = value;
		}
	}

	[ProtoMember(31, IsRequired = false, Name = "mysteryBoxExpireTime", DataFormat = DataFormat.TwosComplement)]
	[DefaultValue(0L)]
	public long mysteryBoxExpireTime
	{
		get
		{
			return _mysteryBoxExpireTime;
		}
		set
		{
			_mysteryBoxExpireTime = value;
		}
	}

	[ProtoMember(32, IsRequired = false, Name = "entitlementInventory", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public EntitlementInventory entitlementInventory
	{
		get
		{
			return _entitlementInventory;
		}
		set
		{
			_entitlementInventory = value;
		}
	}

	[ProtoMember(33, IsRequired = false, Name = "missionCollection", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public MissionCollection missionCollection
	{
		get
		{
			return _missionCollection;
		}
		set
		{
			_missionCollection = value;
		}
	}

	[ProtoMember(34, IsRequired = false, Name = "progressionQuests", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ProgressionQuests progressionQuests
	{
		get
		{
			return _progressionQuests;
		}
		set
		{
			_progressionQuests = value;
		}
	}

	[ProtoMember(35, IsRequired = false, Name = "userSettings", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public UserSettings userSettings
	{
		get
		{
			return _userSettings;
		}
		set
		{
			_userSettings = value;
		}
	}

	[ProtoMember(36, IsRequired = false, Name = "maxRankIdx", DataFormat = DataFormat.TwosComplement)]
	[DefaultValue(0)]
	public int maxRankIdx
	{
		get
		{
			return _maxRankIdx;
		}
		set
		{
			_maxRankIdx = value;
		}
	}

	[ProtoMember(37, IsRequired = false, Name = "eventsProgress", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public EventsProgress eventsProgress
	{
		get
		{
			return _eventsProgress;
		}
		set
		{
			_eventsProgress = value;
		}
	}

	[ProtoMember(38, IsRequired = false, Name = "maxItemPower", DataFormat = DataFormat.TwosComplement)]
	[DefaultValue(0L)]
	public long maxItemPower
	{
		get
		{
			return _maxItemPower;
		}
		set
		{
			_maxItemPower = value;
		}
	}

	[ProtoMember(39, IsRequired = false, Name = "pirate", DataFormat = DataFormat.Default)]
	[DefaultValue(false)]
	public bool pirate
	{
		get
		{
			return _pirate;
		}
		set
		{
			_pirate = value;
		}
	}

	IExtension IExtensible.GetExtensionObject(bool createIfMissing)
	{
		return Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
	}
}
