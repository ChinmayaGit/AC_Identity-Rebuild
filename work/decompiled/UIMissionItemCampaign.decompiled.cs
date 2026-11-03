using System.Collections;
using AcierProtocol;
using Assets.Scripts.Database;
using Assets.Scripts.UI.Loading;
using Assets.Scripts.UI.Localization;
using Assets.Scripts.UI.NGUIExtensions;
using Assets.Scripts.UI.NGUIExtensions.Tweens;
using Assets.Scripts.UI.ScreenManager;
using Assets.Scripts.UI.State;
using Assets.Scripts.UI.Utils;
using Assets.Scripts.UI.Utils.Development;
using Assets.Scripts.UI.Utils.Extensions;
using Database;
using UbiCore.Utils;
using UnityEngine;

namespace Assets.Scripts.UI.Screens.MissionScreen;

public class UIMissionItemCampaign : UIMissionItemBase
{
	[SerializeField]
	[CheckAssigned]
	private UIExtendedLabel m_level;

	[SerializeField]
	[CheckAssigned]
	private BBTweenColor m_levelColorTween;

	[SerializeField]
	[CheckAssigned]
	private BBTweenColor m_mainColorTween;

	[CheckAssigned]
	[SerializeField]
	private UISprite m_checkMarkIcon;

	[CheckAssigned]
	[SerializeField]
	private BBTweenColor m_lockedIconColorTween;

	[SerializeField]
	[CheckAssigned]
	private UIWidget m_lootableContainer;

	[SerializeField]
	[CheckAssigned]
	private UIAlignmentBox m_lootableItemsBox;

	[SerializeField]
	[CheckAssigned]
	private UIPortraitTexture[] m_lootableItems;

	[SerializeField]
	[CheckAssigned]
	private UnityEngine.Animation m_unlockMissionAnimation;

	[CheckAssigned]
	[SerializeField]
	private UnityEngine.Animation m_completeMissionAnimation;

	private bool m_updateTexture;

	private RegionDefinition.AppearanceOption m_appearanceOption;

	private bool m_updateEntryDate;

	public bool HasCompletedMission { get; private set; }

	public bool HasCompletedMissionChallenge { get; private set; }

	public bool HasFinishedCompletedMissionAnimation { get; private set; }

	public MissionInfo MissionInfo { get; private set; }

	public StoryMissionRewardMapping.Entry RewardEntry { get; private set; }

	public UnityEngine.Animation UnlockMissionAnimation => m_unlockMissionAnimation;

	public UnityEngine.Animation CompleteMissionAnimation => m_completeMissionAnimation;

	protected override void OnAwake()
	{
		Clear();
	}

	public override void Clear()
	{
		base.Clear();
		m_level.text = string.Empty;
		m_checkMarkIcon.gameObject.SetActive(value: false);
		m_lootableContainer.gameObject.SetActive(value: false);
	}

	public void UpdateCampaignMission(MissionInfo _missionInfo, LocationSet _location, int _index)
	{
		Index = _index;
		ItemId = _missionInfo.baseName;
		MissionInfo = _missionInfo;
		RegionData = _location.GetRegionDefinition();
		m_appearanceOption = RegionData.GetRegionAppearanceOption(_missionInfo.regionAppearanceOption);
		UpdateGrantedStatus();
		long missionLevelRequirement = MissionInfo.GetMissionLevelRequirement();
		m_level.gameObject.SetActive(missionLevelRequirement > 0);
		m_updateEntryDate = true;
		m_updateTexture = true;
	}

	public void UpdateMissionStatus()
	{
		if (MissionInfo != null)
		{
			UpdateGrantedStatus();
			ItemId = MissionInfo.baseName;
			long missionLevelRequirement = MissionInfo.GetMissionLevelRequirement();
			m_level.gameObject.SetActive(missionLevelRequirement > 0);
			m_updateEntryDate = true;
			m_updateTexture = false;
		}
	}

	protected override void SetActivated(bool _completeInstantly)
	{
		base.SetActivated(_completeInstantly);
		ActivateTweens(_completeInstantly);
	}

	protected override void SetDeActivated(bool _completeInstantly)
	{
		base.SetDeActivated(_completeInstantly);
		ActivateTweens(_completeInstantly);
	}

	private void ActivateTweens(bool _completeInstantly)
	{
		if (_completeInstantly)
		{
			if (m_level.gameObject.activeSelf)
			{
				m_levelColorTween.SetTweenToEnd();
			}
			m_mainColorTween.SetTweenToEnd();
			m_lockedIconColorTween.SetTweenToEnd();
		}
		else
		{
			if (m_level.gameObject.activeSelf)
			{
				m_levelColorTween.StartTweenPlayForward();
			}
			m_mainColorTween.StartTweenPlayForward();
			m_lockedIconColorTween.StartTweenPlayForward();
		}
	}

	private void UpdateGrantedStatus()
	{
		LocalStorage.IMissionProgressData missionProgressData = UbiCore.Utils.Singleton<LocalStorage>.Instance.GetMissionProgressData(MissionInfo.baseName);
		IsUnlocked = missionProgressData != null && missionProgressData.WasUnlockShown && Singleton<StateManager>.Instance.ProfileManager.EntitlementManager.HasEntitlements(MissionInfo.reqEntitlements);
		HasCompletedMission = false;
		HasCompletedMissionChallenge = false;
		if (IsUnlocked)
		{
			RewardEntry = MissionInfo.GetMissionReward();
			StoryMissionRewardMapping.Entry.RankEntry currentRankNormalRewards = RewardEntry.GetCurrentRankNormalRewards();
			if (currentRankNormalRewards != null)
			{
				HasCompletedMission = currentRankNormalRewards.lootTemplates.HasEntitlementsFomLootTemplates();
			}
			StoryMissionRewardMapping.Entry.RankEntry currentRankChallengesRewards = RewardEntry.GetCurrentRankChallengesRewards();
			if (currentRankChallengesRewards != null)
			{
				HasCompletedMissionChallenge = currentRankChallengesRewards.lootTemplates.HasEntitlementsFomLootTemplates();
			}
		}
		HasFinishedCompletedMissionAnimation = missionProgressData?.WasCompletedShown ?? false;
		m_checkMarkIcon.gameObject.SetActive(HasCompletedMission && HasFinishedCompletedMissionAnimation);
		m_completeMissionAnimation.SampleAnimation((HasCompletedMission && HasFinishedCompletedMissionAnimation) ? 1 : 0, string.Empty);
		m_lockedIconColorTween.gameObject.SetActive(!IsUnlocked);
		m_unlockMissionAnimation.SampleAnimation(IsUnlocked ? 1 : 0, string.Empty);
	}

	protected override void OnDestroyItem()
	{
		MissionInfo = null;
		m_appearanceOption = null;
	}

	private void Update()
	{
		if (m_updateEntryDate)
		{
			NextFrameUpdateCampaignMission();
			m_updateEntryDate = false;
		}
	}

	private void NextFrameUpdateCampaignMission()
	{
		m_title.text = MissionInfo.LocaName();
		long missionLevelRequirement = MissionInfo.GetMissionLevelRequirement();
		if (missionLevelRequirement > 0)
		{
			m_level.SetLocalizationKey(2336, new StringMapper { { "Unlock_Level", missionLevelRequirement } });
		}
		bool flag = false;
		if (IsUnlocked)
		{
			flag = UpdateRewardItems();
		}
		m_lootableContainer.gameObject.SetActive(IsUnlocked && flag);
		if (m_updateTexture)
		{
			StartCoroutine(UpdateTexture());
		}
	}

	private bool UpdateRewardItems()
	{
		int num = 0;
		if (!HasCompletedMission)
		{
			StoryMissionRewardMapping.Entry.RankEntry currentRankNormalRewards = RewardEntry.GetCurrentRankNormalRewards();
			if (currentRankNormalRewards != null)
			{
				num += UpdateRewardItemsByLootTemplate(currentRankNormalRewards.lootTemplates, m_lootableItems, num);
			}
		}
		if (!HasCompletedMissionChallenge)
		{
			StoryMissionRewardMapping.Entry.RankEntry currentRankChallengesRewards = RewardEntry.GetCurrentRankChallengesRewards();
			if (currentRankChallengesRewards != null)
			{
				num += UpdateRewardItemsByLootTemplate(currentRankChallengesRewards.lootTemplates, m_lootableItems, num);
			}
		}
		for (int i = num; i < m_lootableItems.Length; i++)
		{
			m_lootableItems[i].gameObject.SetActive(value: false);
		}
		m_lootableItemsBox.RepositionNow = true;
		return num > 0;
	}

	public static int UpdateRewardItemsByLootTemplate(LootTemplate[] _lootTemplates, UIPortraitTexture[] _items, int _startItemCounter)
	{
		int itmeCounter = _startItemCounter;
		_lootTemplates.DoForItemTemplateWrappers((LootTemplate _template) =>
		{
			if (itmeCounter < _items.Length)
			{
				UIItemUtil.UpdateItemByLootTemplate(_template, _turnBackgroundOff: false, _items[itmeCounter], 0f);
				_items[itmeCounter].gameObject.SetActive(value: true);
				itmeCounter++;
			}
		});
		_lootTemplates.DoForMysteryBoxTemplateWrappers((LootTemplate _template) =>
		{
			if (itmeCounter < _items.Length)
			{
				UIItemUtil.UpdateItemByLootTemplate(_template, _turnBackgroundOff: false, _items[itmeCounter], 0f);
				_items[itmeCounter].gameObject.SetActive(value: true);
				itmeCounter++;
			}
		});
		_lootTemplates.DoForModuleTemplateWrappers((LootTemplate _template) =>
		{
			if (itmeCounter < _items.Length)
			{
				UIItemUtil.UpdateItemByLootTemplate(_template, _turnBackgroundOff: false, _items[itmeCounter], 0f);
				_items[itmeCounter].gameObject.SetActive(value: true);
				itmeCounter++;
			}
		});
		_lootTemplates.DoForResourceTemplates((LootTemplate.ResourceTemplate _resource) =>
		{
			if (itmeCounter < _items.Length)
			{
				Resource maxResource = _resource.maxResource;
				UIItemUtil.UpdateResourceItem(maxResource.type, _turnBackgroundOff: false, _items[itmeCounter], _isShopItem: false, UIItemUtil.ItemUVCoordType.ENone, string.Empty, 0f);
				_items[itmeCounter].gameObject.SetActive(value: true);
				itmeCounter++;
			}
		});
		return itmeCounter - _startItemCounter;
	}

	private IEnumerator UpdateTexture()
	{
		m_texture.mainTexture = null;
		string imagePath = string.Empty;
		if (UbiCore.Utils.Singleton<UIDataMapper>.Instance.TutorialMissionDescription.TryGetValue(MissionInfo.baseName, out imagePath) && !string.IsNullOrEmpty(imagePath))
		{
			Texture2D textureAtlas = (Texture2D)UnityEngine.Resources.Load(imagePath, typeof(Texture2D));
			m_texture.mainTexture = textureAtlas;
			yield break;
		}
		UnityResource resource = RegionData.GetRegionBanner(m_appearanceOption);
		LoadAnimationManager loadManager = Singleton<UIScreenManager>.Instance.SceneData.LoadAnimationManager;
		yield return StartCoroutine(loadManager.BeginLoadUnityResource(resource, new LoadAnimationManager.SLoadObjectInfo(m_texture)));
		UnityResource banner = RegionData.GetRegionBanner(m_appearanceOption);
		if (resource == banner)
		{
			m_texture.mainTexture = banner.AccessObject<Texture>();
		}
	}
}
