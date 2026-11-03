using System;
using System.Collections.Generic;
using AcierProtocol;
using Assets.Scripts.Database;
using Assets.Scripts.UI.Localization;
using Assets.Scripts.UI.State;
using Database;
using SharedBaseLib.Core;
using SharedBaseLib.System.Extensions;
using UbiCore.Utils;

namespace Assets.Scripts.UI.Utils.Extensions;

public static class MissionInfoExtensions
{
	public static string LocaName(this MissionInfo _missionInfo)
	{
		return LocalizationManager.Instance.Get(_missionInfo.objectiveLangKey.neutral);
	}

	public static string LocaObjectiveLang(this MissionInfo _missionInfo, RegionDefinition _def)
	{
		return LocalizationHelper.ParseStringToLocaKey(_missionInfo.objective);
	}

	public static string LocaMissionDescription(this MissionInfo _missionInfo, bool _removePlaceHolders = false)
	{
		string text = LocalizationManager.Instance.Get(_missionInfo.missionDescription);
		if (text == null || !_removePlaceHolders)
		{
			return text;
		}
		return MissionExtensions.s_placeholderRemoverRegex.Replace(text, string.Empty);
	}

	public static string LocaMissionFlavorText(this MissionInfo _missionInfo)
	{
		return LocalizationManager.Instance.Get(_missionInfo.missionFlavorTextLoca);
	}

	public static string LocaMissionLockedStatus(this MissionInfo _missionInfo)
	{
		bool flag = true;
		bool flag2 = true;
		bool flag3 = true;
		for (int i = 0; i < _missionInfo.reqEntitlements.Length; i++)
		{
			if (EntitlementExtensions.GetEntitlementByGUID(_missionInfo.reqEntitlements[i]).RequiredProfileLevel == 0 && flag3)
			{
				flag = Singleton<StateManager>.Instance.ProfileManager.EntitlementManager.HasEntitlement(_missionInfo.reqEntitlements[i]);
				flag3 = flag;
			}
			else
			{
				flag2 = Singleton<StateManager>.Instance.ProfileManager.EntitlementManager.HasEntitlement(_missionInfo.reqEntitlements[i]);
			}
		}
		if (flag && !flag2)
		{
			return LocalizationManager.Instance.Get(7205, new StringMapper { 
			{
				"player_lvl_required",
				_missionInfo.GetMissionLevelRequirement()
			} });
		}
		if (_missionInfo.IsTutorialMission())
		{
			return LocalizationManager.Instance.Get(7860);
		}
		return LocalizationManager.Instance.Get(6786);
	}

	public static LocationSet GetLocationSetFromMissionInfo(this MissionInfo _missionInfo)
	{
		CampaignCatalog.Campaign[] campaigns = Singleton<StateManager>.Instance.TemplateManager.CampaignCatalog.campaigns;
		foreach (CampaignCatalog.Campaign campaign in campaigns)
		{
			LocationSet locationSetFromMissionInfoId = campaign.missions.GetLocationSetFromMissionInfoId(_missionInfo);
			if (locationSetFromMissionInfoId != null)
			{
				return locationSetFromMissionInfoId;
			}
		}
		return null;
	}

	public static RegionDefinition GetRegionDefinition(this MissionInfo _missionInfo)
	{
		LocationSet locationSetFromMissionInfo = _missionInfo.GetLocationSetFromMissionInfo();
		return locationSetFromMissionInfo.GetRegionDefinition();
	}

	public static long GetMissionLevelRequirement(this MissionInfo _missionInfo)
	{
		long num = 0L;
		for (int i = 0; i < _missionInfo.reqEntitlements.Length; i++)
		{
			EntitlementDefinition entitlementByGUID = EntitlementExtensions.GetEntitlementByGUID(_missionInfo.reqEntitlements[i]);
			if (entitlementByGUID != null && num < (int)entitlementByGUID.RequiredProfileLevel)
			{
				num = (int)entitlementByGUID.RequiredProfileLevel;
			}
		}
		return num;
	}

	public static StoryMissionRewardMapping.Entry GetMissionReward(string _storyMissionBaseName)
	{
		StoryMissionRewardMapping storyMissionRewardMapping = Singleton<StateManager>.Instance.TemplateManager.StoryMissionRewardMapping;
		for (int i = 0; i < storyMissionRewardMapping.entries.Length; i++)
		{
			StoryMissionRewardMapping.Entry entry = storyMissionRewardMapping.entries[i];
			if (_storyMissionBaseName.Equals(entry.name, StringComparison.InvariantCultureIgnoreCase))
			{
				return entry;
			}
		}
		return null;
	}

	public static StoryMissionRewardMapping.Entry GetMissionReward(this MissionInfo _missionInfo)
	{
		return GetMissionReward(_missionInfo.baseName);
	}

	public static bool HasUnlockAnimationBeenPlayed(this MissionInfo _missionInfo)
	{
		return UbiCore.Utils.Singleton<LocalStorage>.Instance.GetMissionProgressData(_missionInfo.baseName)?.WasUnlockShown ?? false;
	}

	public static bool HasCompletedAnimationBeenPlayed(this MissionInfo _missionInfo)
	{
		return UbiCore.Utils.Singleton<LocalStorage>.Instance.GetMissionProgressData(_missionInfo.baseName)?.WasCompletedShown ?? false;
	}

	public static bool HasCompletedMission(this MissionInfo _missionInfo)
	{
		StoryMissionRewardMapping.Entry missionReward = _missionInfo.GetMissionReward();
		return missionReward.GetCurrentRankNormalRewards()?.lootTemplates.HasEntitlementsFomLootTemplates() ?? false;
	}

	public static bool IsTutorialMission(this MissionInfo _missionInfo)
	{
		return _missionInfo.objectiveBaseType == EObjectiveType.Tutorial_EObjectiveType;
	}

	public static bool IsUnlocked(this MissionInfo _missionInfo)
	{
		return Singleton<StateManager>.Instance.ProfileManager.EntitlementManager.HasEntitlements(_missionInfo.reqEntitlements);
	}

	public static TutorialEntitlementDescription GetTutorialDescription(this MissionInfo _missionInfo)
	{
		for (int i = 0; i < UbiCore.Utils.Singleton<UIDataMapper>.Instance.TutorialEntitlementDescription.Count; i++)
		{
			TutorialEntitlementDescription tutorialEntitlementDescription = UbiCore.Utils.Singleton<UIDataMapper>.Instance.TutorialEntitlementDescription[i];
			EntitlementDefinition entitlementByGUID = EntitlementExtensions.GetEntitlementByGUID(tutorialEntitlementDescription.EntitlementGUID);
			if (entitlementByGUID != null && entitlementByGUID.HasAllRequiredEntitlementInList(_missionInfo.reqEntitlements))
			{
				return tutorialEntitlementDescription;
			}
		}
		return null;
	}

	public static EntitlementHighlightDescription GetEntitlementHighlightDescription(this MissionInfo _missionInfo)
	{
		StoryMissionRewardMapping.Entry missionReward = _missionInfo.GetMissionReward();
		StoryMissionRewardMapping.Entry.RankEntry currentRankNormalRewards = missionReward.GetCurrentRankNormalRewards();
		if (currentRankNormalRewards != null)
		{
			string[] entitlementsFromLootTemplate = currentRankNormalRewards.lootTemplates.GetEntitlementsFromLootTemplate();
			if (entitlementsFromLootTemplate.Length > 0)
			{
				for (int i = 0; i < UbiCore.Utils.Singleton<UIDataMapper>.Instance.EntitlementHighlightDescription.Count; i++)
				{
					EntitlementHighlightDescription entitlementHighlightDescription = UbiCore.Utils.Singleton<UIDataMapper>.Instance.EntitlementHighlightDescription[i];
					EntitlementDefinition entitlementByGUID = EntitlementExtensions.GetEntitlementByGUID(entitlementHighlightDescription.EntitlementGUID);
					if (entitlementByGUID != null && entitlementByGUID.RequiredEntitlements.Count > 0 && entitlementByGUID.HasAllRequiredEntitlementInList(entitlementsFromLootTemplate))
					{
						return entitlementHighlightDescription;
					}
				}
			}
		}
		return null;
	}

	public static MissionInfo GetMissionInfoFromLocationSetById(this MissionsAtLocation[] _missionsAtLocations, string _missionId)
	{
		foreach (MissionsAtLocation missionsAtLocation in _missionsAtLocations)
		{
			for (int j = 0; j < missionsAtLocation.missions.Length; j++)
			{
				MissionInfo missionInfo = missionsAtLocation.missions[j];
				if (_missionId.Equals(missionInfo.baseName, StringComparison.InvariantCultureIgnoreCase))
				{
					return missionInfo;
				}
			}
		}
		return null;
	}

	public static LocationSet GetLocationSetFromMissionInfoId(this MissionsAtLocation[] _missionsAtLocations, MissionInfo _MissionInfo)
	{
		return _missionsAtLocations.GetLocationSetFromMissionInfoId(_MissionInfo.baseName);
	}

	public static LocationSet GetLocationSetFromMissionInfoId(this MissionsAtLocation[] _missionsAtLocations, string _missionId)
	{
		foreach (MissionsAtLocation missionsAtLocation in _missionsAtLocations)
		{
			for (int j = 0; j < missionsAtLocation.missions.Length; j++)
			{
				MissionInfo missionInfo = missionsAtLocation.missions[j];
				if (_missionId.Equals(missionInfo.baseName, StringComparison.InvariantCultureIgnoreCase))
				{
					return missionsAtLocation.location;
				}
			}
		}
		return null;
	}

	public static RegionDefinition GetRegionDefinition(this LocationSet _location)
	{
		List<KeyValuePair<GameID, DBEntry>> items = Main.Instance.GameDB.GetItems<RegionDefinition>();
		for (int i = 0; i < items.Count; i++)
		{
			RegionDefinition regionDefinition = items[i].Value as RegionDefinition;
			if (regionDefinition.Region == _location.region && regionDefinition.Age == _location.era && regionDefinition.Mood == _location.mood)
			{
				return regionDefinition;
			}
		}
		return null;
	}

	public static StoryMissionRewardMapping.Entry.RankEntry GetRankNormalRewards(this StoryMissionRewardMapping.Entry _entry, int _rank)
	{
		if (_entry.completionRewards.Length > _rank)
		{
			return _entry.completionRewards[_rank];
		}
		return null;
	}

	public static StoryMissionRewardMapping.Entry.RankEntry GetCurrentRankNormalRewards(this StoryMissionRewardMapping.Entry _entry)
	{
		int heroicMissionRank = Singleton<StateManager>.Instance.MissionSelectionManager.HeroicMissionRank;
		return _entry.GetRankNormalRewards(heroicMissionRank);
	}

	public static StoryMissionRewardMapping.Entry.RankEntry GetCurrentRankChallengesRewards(this StoryMissionRewardMapping.Entry _entry)
	{
		int heroicMissionRank = Singleton<StateManager>.Instance.MissionSelectionManager.HeroicMissionRank;
		if (_entry.challengeRewards.Length > heroicMissionRank)
		{
			return _entry.challengeRewards[heroicMissionRank];
		}
		return null;
	}

	public static bool HasEntitlementsFomLootTemplates(this LootTemplate[] _lootTemplates)
	{
		string[] entitlementsFromLootTemplate = _lootTemplates.GetEntitlementsFromLootTemplate();
		return Singleton<StateManager>.Instance.ProfileManager.EntitlementManager.HasEntitlements(entitlementsFromLootTemplate);
	}

	public static string[] GetEntitlementsFromLootTemplate(this LootTemplate[] _lootTemplates)
	{
		BBSimpleList<string> bBSimpleList = new BBSimpleList<string>();
		foreach (LootTemplate lootTemplate in _lootTemplates)
		{
			if (lootTemplate.entitlementTemplate != null)
			{
				bBSimpleList.Add(lootTemplate.entitlementTemplate.gameIdGuid);
			}
		}
		return bBSimpleList.ToArray();
	}
}
