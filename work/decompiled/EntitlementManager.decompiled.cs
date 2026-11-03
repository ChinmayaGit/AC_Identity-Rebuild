using System.Collections.Generic;
using AcierProtocol;
using Database;
using SharedBaseLib.Core;

namespace Assets.Scripts.UI.State.ProfileManager;

public class EntitlementManager
{
	private readonly BBHashSet<string> m_grantedEntitlementsGuidString = new BBHashSet<string>();

	private readonly BBHashSet<int> m_grantedEntitlementsGuidHash = new BBHashSet<int>();

	public void UpdatGrantedeEntitlements(EntitlementInventory _entitlements)
	{
		m_grantedEntitlementsGuidString.Clear();
		m_grantedEntitlementsGuidHash.Clear();
		for (int i = 0; i < _entitlements.entries.Length; i++)
		{
			string _guid = _entitlements.entries[i].gameIdGuid;
			m_grantedEntitlementsGuidString.Add(_guid.ToLower());
			m_grantedEntitlementsGuidHash.Add(GameID.ComputeGuidHash(ref _guid));
		}
	}

	public bool HasEntitlements(string[] _entitlements)
	{
		for (int i = 0; i < _entitlements.Length; i++)
		{
			if (!HasEntitlement(_entitlements[i]))
			{
				return false;
			}
		}
		return true;
	}

	public bool HasEntitlements(List<GameID> _entitlements)
	{
		for (int i = 0; i < _entitlements.Count; i++)
		{
			if (!HasEntitlement(_entitlements[i].Guid))
			{
				return false;
			}
		}
		return true;
	}

	public bool HasEntitlement(string _entitlementGuidStr)
	{
		return m_grantedEntitlementsGuidString.Contains(_entitlementGuidStr.ToLower());
	}

	public bool HasEntitlement(int _entitlementGuidHash)
	{
		return m_grantedEntitlementsGuidHash.Contains(_entitlementGuidHash);
	}
}
