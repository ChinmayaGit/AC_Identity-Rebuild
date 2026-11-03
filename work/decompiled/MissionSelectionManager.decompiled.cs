using System;
using AcierProtocol;
using Assets.Scripts.UI.Interfaces;
using Assets.Scripts.UI.ScreenManager;
using Assets.Scripts.UI.Utils.Extensions;
using SharedBaseLib.Core;

namespace Assets.Scripts.UI.State.MissionManager;

public class MissionSelectionManager : IValidated
{
	public enum EMissionMenu
	{
		Campaigns,
		CampaignMissions,
		ContractMissions,
		CampaignDetails,
		ContractDetails
	}

	private EMissionMenu m_subMenuType;

	private int m_heroicMissionRank;

	private string m_campaignId = string.Empty;

	private MissionInfo m_campaignMission;

	private AcierProtocol.Mission m_contractMission;

	public SimpleDelegate OnRankChanged;

	public SimpleDelegate OnMissionStateChanged;

	public SimpleDelegate OnSubMenuChanged;

	public int CampaignHirelingReward;

	public int ContractHirelingReward;

	public string FirstContractAtStartup { get; set; }

	public int HeroicMissionRank
	{
		get
		{
			if (IsSelectedMissionCampaign)
			{
				return CampaignHeroicMissionRank;
			}
			return ContractHeroicMissionRank;
		}
		set
		{
			if (value != m_heroicMissionRank)
			{
				m_heroicMissionRank = value;
				if (OnMissionStateChanged != null)
				{
					OnMissionStateChanged.Trigger();
				}
				if (OnRankChanged != null)
				{
					OnRankChanged.Trigger();
				}
			}
		}
	}

	public int CampaignHeroicMissionRank => 0;

	public int ContractHeroicMissionRank => m_heroicMissionRank;

	public bool IsSelectedMissionCampaign => IsCampaignMenuType(m_subMenuType);

	public string CurrentSelectedMissionCampaignId
	{
		get
		{
			if (IsSelectedMissionCampaign)
			{
				CampaignCatalog.Campaign campaignByMissionId = Singleton<StateManager>.Instance.TemplateManager.CampaignCatalog.GetCampaignByMissionId(m_campaignMission.baseName);
				if (campaignByMissionId != null)
				{
					return campaignByMissionId.id;
				}
			}
			return string.Empty;
		}
	}

	public LocationSet CurrentMissionLocationSet
	{
		get
		{
			if (IsCampaignMenuType(m_subMenuType))
			{
				return Singleton<StateManager>.Instance.TemplateManager.CampaignCatalog.GetLocationByMissionInfo(m_campaignMission.baseName);
			}
			return m_contractMission.locationSet;
		}
	}

	public string MissionId
	{
		get
		{
			if (IsCampaignMenuType(m_subMenuType))
			{
				if (m_campaignMission != null)
				{
					return m_campaignMission.baseName;
				}
			}
			else if (m_contractMission != null)
			{
				return m_contractMission.id;
			}
			return string.Empty;
		}
	}

	public bool IsFromCampaignMenu { get; private set; }

	public EMissionMenu SubMenuType
	{
		get
		{
			return m_subMenuType;
		}
		set
		{
			IsFromCampaignMenu = IsCampaignMenuType(value);
			m_subMenuType = value;
			OnMissionStateChanged.Trigger();
			OnSubMenuChanged.Trigger();
		}
	}

	public string CampaignId
	{
		get
		{
			return m_campaignId;
		}
		set
		{
			m_campaignId = value;
			OnMissionStateChanged.Trigger();
		}
	}

	public bool HaveMissionAfterTutorial => CampaignMissionAfterTutorial != null || ContractMissionAfterTutorial != null;

	public MissionInfo CampaignMissionAfterTutorial { get; private set; }

	public AcierProtocol.Mission ContractMissionAfterTutorial { get; private set; }

	public MissionInfo CampaignMission
	{
		get
		{
			return m_campaignMission;
		}
		set
		{
			if (value != null && !IsCampaignMenuType(m_subMenuType))
			{
				m_subMenuType = EMissionMenu.CampaignMissions;
			}
			m_campaignMission = value;
			if (OnMissionStateChanged != null)
			{
				OnMissionStateChanged.Trigger();
			}
		}
	}

	public AcierProtocol.Mission ContractMission
	{
		get
		{
			return m_contractMission;
		}
		set
		{
			if (value != null && IsCampaignMenuType(m_subMenuType))
			{
				m_subMenuType = EMissionMenu.ContractMissions;
			}
			m_contractMission = value;
			OnMissionStateChanged.Trigger();
		}
	}

	public bool Valid
	{
		get
		{
			if (IsCampaignMenuType(m_subMenuType))
			{
				return m_campaignMission == null;
			}
			return m_contractMission == null;
		}
	}

	public MissionSelectionManager()
	{
		ContractMissionAfterTutorial = null;
		CampaignMissionAfterTutorial = null;
		OnRankChanged = new SimpleDelegate(this, new Type[0]);
		OnMissionStateChanged = new SimpleDelegate(this, new Type[0]);
		OnSubMenuChanged = new SimpleDelegate(this, new Type[0]);
	}

	public bool IsCurrentMissionValid()
	{
		if (!IsCampaignMenuType(m_subMenuType))
		{
			MissionCollection.RankMissions currrentRankMissions = Singleton<StateManager>.Instance.ProfileManager.CurrrentRankMissions;
			return ContractMission != null && currrentRankMissions.GetMissionByID(ContractMission.id) != null;
		}
		return true;
	}

	public void Init()
	{
		if (Main.Instance.IsNetworkEnabled)
		{
			HeroicMissionRank = Singleton<StateManager>.Instance.TemplateManager.HeroicRankMapping.GetHighestGrantedRankIndex();
			FirstContractAtStartup = Singleton<StateManager>.Instance.ProfileManager.Profile.missionCollection.GetFirstAvailableMissionFromCollection();
		}
	}

	public static bool IsCampaignMenuType(EMissionMenu _type)
	{
		return _type == EMissionMenu.CampaignMissions || _type == EMissionMenu.Campaigns || _type == EMissionMenu.CampaignDetails;
	}

	public void Clear()
	{
		m_campaignMission = null;
		m_contractMission = null;
		CampaignMissionAfterTutorial = null;
		ContractMissionAfterTutorial = null;
	}

	public void FixMissionScreenOnClear()
	{
		UIScreenHistoryDetails.EUIScreenGroups groupIdByMenuType = UIScreenHistoryDetails.GetGroupIdByMenuType(Singleton<StateManager>.Instance.MissionSelectionManager.SubMenuType);
		UIScreenHistoryDetails.ScreenHistory lastScreenByScreenGroup = Singleton<UIScreenManager>.Instance.LastScreenHistory.GetLastScreenByScreenGroup(groupIdByMenuType);
		if (lastScreenByScreenGroup != null)
		{
			m_subMenuType = lastScreenByScreenGroup.MenuType;
		}
	}

	public void ClearMissionAfterTutorial()
	{
		CampaignMissionAfterTutorial = null;
		ContractMissionAfterTutorial = null;
	}

	public void SetCampaignMissionTutorialMisison(MissionInfo _tutorialMisison)
	{
		CampaignMissionAfterTutorial = CampaignMission;
		m_campaignMission = _tutorialMisison;
	}

	public void SetContractMissionTutorialMisison(MissionInfo _tutorialMisison)
	{
		ContractMissionAfterTutorial = ContractMission;
		m_subMenuType = EMissionMenu.CampaignDetails;
		m_campaignMission = _tutorialMisison;
	}
}
