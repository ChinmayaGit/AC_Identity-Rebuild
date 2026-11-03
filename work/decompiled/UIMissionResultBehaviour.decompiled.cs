using System;
using System.Collections.Generic;
using AcierProtocol;
using Assets.Scripts.Database;
using Assets.Scripts.Mission;
using Assets.Scripts.Mission.Logic;
using Assets.Scripts.Mission.UI;
using Assets.Scripts.Network.Missions.Mission.Data;
using Assets.Scripts.UI.CameraManager;
using Assets.Scripts.UI.InfoPopup;
using Assets.Scripts.UI.Ingame.Behaviours.Misc;
using Assets.Scripts.UI.Ingame.Behaviours.MissionResult;
using Assets.Scripts.UI.LoadoutScreen;
using Assets.Scripts.UI.Localization;
using Assets.Scripts.UI.NGUIExtensions;
using Assets.Scripts.UI.NGUIExtensions.Tweens;
using Assets.Scripts.UI.State;
using Assets.Scripts.UI.State.MissionManager;
using Assets.Scripts.UI.Tutorial;
using Assets.Scripts.UI.Utils;
using Assets.Scripts.UI.Utils.Extensions;
using Mission;
using SharedBaseLib.System.Extensions;
using UbiCore.Utils;
using UnityEngine;

namespace Assets.Scripts.UI.Ingame.Behaviours;

public class UIMissionResultBehaviour : UIBaseResultBehaviour
{
	[SerializeField]
	private UIMissionResultXpProgressBehaviour m_progressBehaviour;

	[SerializeField]
	private BBTweenAnimation m_objectiveTweenAnimation;

	[SerializeField]
	private BBTweenAnimation m_rewardTweenAnimation;

	[SerializeField]
	private UIPortraitTexture m_assassinPortrait;

	[SerializeField]
	private UIExtendedLabel m_missionResultLabel;

	[SerializeField]
	private UIExtendedLabel m_assassinClassNames;

	[SerializeField]
	private UIExtendedLabel m_assassinName;

	[SerializeField]
	private UIExtendedLabel m_xpFromMainGoalReceived;

	[SerializeField]
	private UIExtendedLabel m_xpFromChallengesReceived;

	[SerializeField]
	private UISprite m_missionIcon;

	[SerializeField]
	private UISprite m_missionIconBackground;

	[SerializeField]
	private UIMissionRewardItemBehaviour[] m_mainRewardItems;

	[SerializeField]
	private UIMissionRewardItemBehaviour[] m_challengeRewardItems;

	[SerializeField]
	private UIMissionRewardItemBehaviour[] m_extraRewardItems;

	[SerializeField]
	private UIAlignmentBox m_extraRewardsRow;

	[SerializeField]
	private UIAlignmentBox m_mainRewardsRow;

	[SerializeField]
	private UIAlignmentBox m_challengeRewardsRow;

	[SerializeField]
	private UITable m_rewardsContainerTable;

	[SerializeField]
	private BBTweenPosition m_leftPositionTween;

	[SerializeField]
	private BBTweenPosition m_rightPositionTween;

	[SerializeField]
	private UIWidget m_leftContainer;

	[SerializeField]
	private UIWidget m_rightContainer;

	[SerializeField]
	private UIExtendedButton m_returnToAnimusButton;

	[SerializeField]
	private UIObjectiveBehaviour m_challenges;

	[SerializeField]
	private UIObjectiveBehaviour m_modifier;

	[SerializeField]
	private UICheckboxBehaviour m_mainGoal;

	[SerializeField]
	private UITable m_missionGoalsTable;

	[SerializeField]
	private UIScrollView m_missionGoalsScrollView;

	[SerializeField]
	private UIInfoPopup m_infoPopup;

	[SerializeField]
	private BBTweenAlpha m_missionDetailsTweenAlpha;

	private UIMissionRewardItemBehaviour m_currentSelectedItem;

	private int m_missionRepositionCallbackWaitCounter;

	private int m_objectiveLayoutWaitCounter;

	private int m_numMainRewards;

	private int m_numChallengeRewards;

	private int m_numExtraREwards;

	private UIMissionResultData m_missionResultData;

	private UnityEngine.Animation[] m_rewardAnimations;

	public override UIScreenWidgetManager.EUiScreenWidget ScreenWidgetType => UIScreenWidgetManager.EUiScreenWidget.MissionResult;

	protected override string GetAnimationName()
	{
		return "MissionResultMain";
	}

	protected override void ValidateFields()
	{
		base.ValidateFields();
	}

	public override void SetMissionRewards(MissionReward _missionRewardData)
	{
		m_missionResultData.SetMissionRewardData(_missionRewardData);
	}

	public override void Init()
	{
		base.Init();
		ValidateFields();
		ResetInfoPoup();
		Singleton<UICameraManager>.Instance.RegisterCameraReference(gameObject.layer);
		UpdateTweenPosition(m_leftContainer, m_leftPositionTween, UIUtil.ESide.Left);
		UpdateTweenPosition(m_rightContainer, m_rightPositionTween, UIUtil.ESide.Right);
		Singleton<UICameraManager>.Instance.UnRegisterCameraReference(gameObject.layer);
		m_missionResultData = new UIMissionResultData();
		EventDelegate.Add(m_returnToAnimusButton.onClick, OnTapReturnToAnimus);
	}

	public void UpdateTweenPosition(UIWidget _widget, BBTweenPosition _tweenPos, UIUtil.ESide _side)
	{
		_widget.ForceUpdate(_forceUpdate: true);
		_tweenPos.SetEndToCurrentValue();
		Vector3 localPosition = _widget.transform.localPosition;
		localPosition.x = UIUtil.GetOffscreenPositionX(_widget, _widget.width, _widget.pivotOffset.x, _side);
		_tweenPos.from = localPosition;
	}

	public override void SetMissionData(bool _missionCompleted, MissionRootData _missionDescription)
	{
		base.SetMissionData(_missionCompleted, _missionDescription);
		MissionSelectionManager missionSelectionManager = Singleton<StateManager>.Instance.MissionSelectionManager;
		m_mainAnimation.SampleAnimation(1f, string.Empty);
		m_missionCompleted = _missionCompleted;
		m_missionDescription = _missionDescription;
		m_hasAnimationStarted = false;
		gameObject.GetComponent<UIPanel>().alpha = 0f;
		m_missionRepositionCallbackWaitCounter = 0;
		bool isSelectedMissionCampaign = missionSelectionManager.IsSelectedMissionCampaign;
		m_returnToAnimusButton.gameObject.SetActive(UbiCore.Utils.Singleton<UiMain>.Instance.TutorialManager.IsDebriefingConfirmationVisible(_missionCompleted));
		m_nextMissionButton.gameObject.SetActive(!isSelectedMissionCampaign && EntitlementExtensions.IsHeroicRankGranted(1) && UbiCore.Utils.Singleton<UiMain>.Instance.TutorialManager.IsDebriefingConfirmationVisible(_missionCompleted));
		m_missionResultLabel.text = LocalizationManager.Instance.Get((!_missionCompleted) ? 1196 : 1195);
		UpdateStaticData(_missionCompleted);
		UpdateItems();
		UpdateMissionRelatedData();
		bool isEnabled = missionSelectionManager.IsCurrentMissionValid();
		if (_missionCompleted && TutorialHelper.JustFinishedFirstStoryMissionInTutorial(_success: true, _missionDescription))
		{
			isEnabled = false;
		}
		m_retryButton.isEnabled = isEnabled;
		bool haveMissionAfterTutorial = missionSelectionManager.HaveMissionAfterTutorial;
		m_nextMissionButton.AllowDisabledClick = !haveMissionAfterTutorial;
		m_nextMissionButton.isEnabled = haveMissionAfterTutorial || _missionDescription.ObjectiveType != EObjectiveType.Story_EObjectiveType;
	}

	public override void UpdateScreen()
	{
		m_progressBehaviour.UpdateScreen();
		base.UpdateScreen();
	}

	public override void UpdatePlayerPortrait(EntityId _entityId)
	{
		UIPortraitUtil.UpdateInMissionPortrait(_entityId, m_assassinPortrait);
	}

	private void Start()
	{
		ConnectRewardItemCallbacks(m_mainRewardItems);
		ConnectRewardItemCallbacks(m_challengeRewardItems);
		ConnectRewardItemCallbacks(m_extraRewardItems);
		m_infoPopup.OnBeforePopupCloseCallback += OnBeforePopupCloseCallback;
		m_infoPopup.OnPopupClosedCallback += OnPopupClosedCallback;
	}

	private void ConnectRewardItemCallbacks(UIMissionRewardItemBehaviour[] _rewardItems)
	{
		for (int i = 0; i < _rewardItems.Length; i++)
		{
			_rewardItems[i].OnClickCallback += OnRewardItemClicked;
		}
	}

	private void DisconnectRewardItemCallbacks(UIMissionRewardItemBehaviour[] _rewardItems)
	{
		for (int i = 0; i < _rewardItems.Length; i++)
		{
			_rewardItems[i].OnClickCallback -= OnRewardItemClicked;
		}
	}

	private void Destroy()
	{
		OnDestroy();
		m_currentSelectedItem = null;
		m_infoPopup.OnBeforePopupCloseCallback -= OnBeforePopupCloseCallback;
		m_infoPopup.OnPopupClosedCallback -= OnPopupClosedCallback;
		DisconnectRewardItemCallbacks(m_mainRewardItems);
		DisconnectRewardItemCallbacks(m_challengeRewardItems);
		DisconnectRewardItemCallbacks(m_extraRewardItems);
		EventDelegate.Remove(m_returnToAnimusButton.onClick, OnTapReturnToAnimus);
	}

	private void ResetInfoPoup()
	{
		m_infoPopup.IsEnabled = false;
		if (m_currentSelectedItem != null)
		{
			m_currentSelectedItem.IsSelected = false;
		}
		m_currentSelectedItem = null;
		m_missionDetailsTweenAlpha.SetTweenToEnd();
	}

	private void OnPopupClosedCallback()
	{
		m_missionDetailsTweenAlpha.StartTweenPlayForward();
	}

	private void OnBeforePopupCloseCallback()
	{
		if (m_currentSelectedItem != null)
		{
			m_currentSelectedItem.IsSelected = false;
		}
		m_currentSelectedItem = null;
	}

	private void OnRewardItemClicked(UIMissionRewardItemBehaviour _rewardItem)
	{
		if (_rewardItem != null && m_currentSelectedItem == null)
		{
			m_missionDetailsTweenAlpha.StartTweenPlayReverse();
		}
		m_currentSelectedItem = m_infoPopup.UpdateInfoPopupByRewardItem(m_currentSelectedItem, _rewardItem, _showIndicator: false);
	}

	private void OnTapReturnToAnimus()
	{
		if (OnReturnToAnimusTapped != null)
		{
			OnReturnToAnimusTapped();
		}
	}

	private void UpdateMissionChildrenReposition()
	{
		m_missionRepositionCallbackWaitCounter--;
		if (m_missionRepositionCallbackWaitCounter <= 0)
		{
			UITable rewardsContainerTable = m_rewardsContainerTable;
			rewardsContainerTable.onReposition = (UITable.OnReposition)Delegate.Combine(rewardsContainerTable.onReposition, new UITable.OnReposition(OnContainerTableReposition));
			m_rewardsContainerTable.repositionNow = true;
		}
	}

	private void OnContainerTableReposition()
	{
		UITable rewardsContainerTable = m_rewardsContainerTable;
		rewardsContainerTable.onReposition = (UITable.OnReposition)Delegate.Remove(rewardsContainerTable.onReposition, new UITable.OnReposition(OnContainerTableReposition));
		StartCoroutine(PlayMainAnimation());
	}

	private void UpdateStaticData(bool _missionCompleted)
	{
		long totalXpRecived = 0L;
		long num = 0L;
		long num2 = 0L;
		long currentAssassinXp = 0L;
		string text = string.Empty;
		string text2 = string.Empty;
		int assassinsLevel = 0;
		int newAssassinLevels = 0;
		bool hasAssassinMaxLevel = false;
		long maxXpForLevel = 0L;
		if (m_missionResultData.HasValidRewardData)
		{
			num = m_missionResultData.ExpMain;
			num2 = m_missionResultData.ExpChallenge;
			totalXpRecived = num + num2;
			if (AssassinDataBeforeMissionStart != null)
			{
				text = AssassinDataBeforeMissionStart.GetAssassinName();
				text2 = LocalizationManager.Instance.Get(UbiCore.Utils.Singleton<UIDataMapper>.Instance.AssassinClassDescriptions[(int)AssassinDataBeforeMissionStart.classTypeId].NameId);
				if (AssassinDataBeforeMissionStart.extendedClassTypeId != Assassin.EClassTypeId.None)
				{
					string value = LocalizationManager.Instance.Get(UbiCore.Utils.Singleton<UIDataMapper>.Instance.AssassinClassDescriptions[(int)AssassinDataBeforeMissionStart.extendedClassTypeId].NameId);
					text2 = LocalizationManager.Instance.Get(2116, new StringMapper
					{
						{ "Primary_Class_Name", text2 },
						{ "Secondary_Class_Name", value }
					});
				}
				currentAssassinXp = AssassinDataBeforeMissionStart.GetCurrentXPRelativeToCurrentLevel();
				hasAssassinMaxLevel = AssassinDataBeforeMissionStart.IsMaxLevel();
				assassinsLevel = AssassinDataBeforeMissionStart.level;
				maxXpForLevel = AssassinDataBeforeMissionStart.GetCurrentLevelMaxXP();
				newAssassinLevels = Singleton<StateManager>.Instance.ProfileManager.CurrentAssassin.level - AssassinDataBeforeMissionStart.level;
			}
		}
		m_missionResultLabel.text = LocalizationManager.Instance.Get((!_missionCompleted) ? 1196 : 1195);
		m_assassinName.text = text;
		m_assassinClassNames.text = text2;
		bool flag = num > 0;
		m_xpFromMainGoalReceived.transform.parent.gameObject.SetActive(flag);
		if (flag)
		{
			string text3 = ((num <= 0) ? string.Empty : "+");
			text3 += LocalizationManager.Instance.GetInteger(num);
			m_xpFromMainGoalReceived.text = text3;
		}
		bool flag2 = num2 > 0;
		m_xpFromChallengesReceived.transform.parent.gameObject.SetActive(flag2);
		if (flag2)
		{
			string text4 = ((num2 <= 0) ? string.Empty : "+");
			text4 += LocalizationManager.Instance.GetInteger(num2);
			m_xpFromChallengesReceived.text = text4;
		}
		m_progressBehaviour.SetMissionData(currentAssassinXp, assassinsLevel, newAssassinLevels, maxXpForLevel, hasAssassinMaxLevel, totalXpRecived);
	}

	private void UpdateVisibilityAndAnimations(UIMissionRewardItemBehaviour[] _rewardItems, int _numRewards)
	{
		for (int i = _numRewards; i < _rewardItems.Length; i++)
		{
			_rewardItems[i].SetVisibility(_visible: false);
		}
		for (int j = 0; j < _numRewards; j++)
		{
			m_rewardAnimations = m_rewardAnimations.Add(_rewardItems[j].RewardAnimation);
		}
	}

	private void UpdateExtraRewards()
	{
		UpdateRewardResourceFromData(ref m_numExtraREwards, EResourceType.SoftCurrency, m_missionResultData.SoftCurrencyExtra, m_extraRewardItems);
		UpdateRewardResourceFromData(ref m_numExtraREwards, EResourceType.SocialCurrency, m_missionResultData.HeroicCurrencyExtra, m_extraRewardItems);
		UpdateRewardItemsFromData(ref m_numExtraREwards, m_missionResultData.GetExtraItems(), m_extraRewardItems);
		m_extraRewardsRow.transform.parent.gameObject.SetActive(m_numExtraREwards > 0);
		UpdateVisibilityAndAnimations(m_extraRewardItems, m_numExtraREwards);
		if (m_numExtraREwards > 0)
		{
			RepositionRewardItemRow(m_extraRewardsRow);
		}
	}

	private void UpdateChallengeRewards()
	{
		UpdateRewardResourceFromData(ref m_numChallengeRewards, EResourceType.SoftCurrency, m_missionResultData.SoftCurrencyChallenge, m_challengeRewardItems);
		UpdateRewardResourceFromData(ref m_numChallengeRewards, EResourceType.SocialCurrency, m_missionResultData.HeroicCurrencyChallenge, m_challengeRewardItems);
		UpdateRewardItemsFromData(ref m_numChallengeRewards, m_missionResultData.GetChallengeItems(), m_challengeRewardItems);
		m_challengeRewardsRow.transform.parent.gameObject.SetActive(m_numChallengeRewards > 0);
		UpdateVisibilityAndAnimations(m_challengeRewardItems, m_numChallengeRewards);
		if (m_numMainRewards > 0)
		{
			RepositionRewardItemRow(m_challengeRewardsRow);
		}
	}

	private void UpdateMainRewards()
	{
		UpdateRewardResourceFromData(ref m_numMainRewards, EResourceType.SoftCurrency, m_missionResultData.SoftCurrencyMain, m_mainRewardItems);
		UpdateRewardResourceFromData(ref m_numMainRewards, EResourceType.SocialCurrency, m_missionResultData.HeroicCurrencyMain, m_mainRewardItems);
		UpdateRewardItemsFromData(ref m_numMainRewards, m_missionResultData.Elements, m_mainRewardItems);
		m_mainRewardsRow.transform.parent.gameObject.SetActive(m_numMainRewards > 0);
		UpdateVisibilityAndAnimations(m_mainRewardItems, m_numMainRewards);
		if (m_numMainRewards > 0)
		{
			RepositionRewardItemRow(m_mainRewardsRow);
		}
	}

	private void UpdateItems()
	{
		m_rewardAnimations = new UnityEngine.Animation[0];
		m_numMainRewards = 0;
		m_numChallengeRewards = 0;
		m_numExtraREwards = 0;
		UpdateMainRewards();
		UpdateChallengeRewards();
		UpdateExtraRewards();
		m_rewardTweenAnimation.SetAnimationList(m_rewardAnimations, string.Empty);
	}

	private void UpdateRewardItemsFromData(ref int _itemsAdded, InventoryElement[] _elements, UIMissionRewardItemBehaviour[] _rewardItems)
	{
		if (_elements == null)
		{
			return;
		}
		for (int i = 0; i < _elements.Length; i++)
		{
			if (_itemsAdded < _rewardItems.Length)
			{
				InventoryElement element = _elements[i];
				UIMissionRewardItemBehaviour uIMissionRewardItemBehaviour = _rewardItems[_itemsAdded];
				uIMissionRewardItemBehaviour.UpdateRewardByItem(element);
				_itemsAdded++;
			}
		}
	}

	private void UpdateRewardResourceFromData(ref int _numRewards, EResourceType _resType, int _amount, UIMissionRewardItemBehaviour[] _rewardItems)
	{
		if (_amount > 0 && _numRewards < _rewardItems.Length)
		{
			_rewardItems[_numRewards].UpdateRewardByResource(_resType, _amount);
			_numRewards++;
		}
	}

	private void RepositionRewardItemRow(UIAlignmentBox _rowBox)
	{
		m_missionRepositionCallbackWaitCounter++;
		EventDelegate.Add(_rowBox.OnReposition, OnRepositionRewardItemRowFinished, oneShot: true);
		_rowBox.RepositionNow = true;
		_rowBox.gameObject.SetActive(value: true);
	}

	private void OnRepositionRewardItemRowFinished()
	{
		UpdateMissionChildrenReposition();
	}

	protected override void UpdateMissionRelatedData()
	{
		m_missionResultData.UpdateChallengeData();
		RegionDefinition regionDefinition = m_missionDescription.GetRegionDefinition();
		m_missionName.text = m_missionDescription.LocaName(regionDefinition);
		UpdateObjectives();
	}

	private void UpdateObjectives()
	{
		bool flag = UbiCore.Utils.Singleton<MissionManager>.Instance.MissionState == EMissionState.Success;
		UICheckboxBehaviour.ECheckedState checkedState = ((!flag) ? UICheckboxBehaviour.ECheckedState.Fail : UICheckboxBehaviour.ECheckedState.Success);
		List<UIMissionGoals.GoalData> list = null;
		List<UIMissionGoals.GoalData> list2 = null;
		if (m_missionResultData.MissionGoals != null)
		{
			list = m_missionResultData.MissionGoals.Challenges.Where((UIMissionGoals.GoalData x) => !x.Hidden);
			list2 = m_missionResultData.MissionGoals.AdditionalObjectives.Where((UIMissionGoals.GoalData x) => !x.Hidden);
			List<UIMissionGoals.GoalData> list3 = m_missionResultData.MissionGoals.Objectives.Where((UIMissionGoals.GoalData x) => !x.Hidden);
			if (list3.Count > 0)
			{
				m_mainGoal.SetData(list3[0].Name, checkedState);
			}
		}
		else
		{
			m_mainGoal.SetData("Error: NO DATA", checkedState);
		}
		m_objectiveLayoutWaitCounter = 0;
		int num = 0;
		bool flag2 = list?.Any((UIMissionGoals.GoalData x) => !x.Hidden) ?? false;
		m_challenges.SetActive(flag2);
		if (flag2)
		{
			num = list.Count;
			m_objectiveLayoutWaitCounter++;
			UICheckboxBehaviour.ECheckedState? overrideState = ((!flag) ? new UICheckboxBehaviour.ECheckedState?(UICheckboxBehaviour.ECheckedState.None) : ((UICheckboxBehaviour.ECheckedState?)null));
			m_challenges.SetData(list.ToArray(), StartRepositionMissionGoalTable, _hasToBeDone: false, _useDots: false, _hasToBeChecked: true, overrideState);
		}
		bool flag3 = list2 != null && list2.Count > 0;
		m_modifier.SetActive(flag3);
		if (flag3)
		{
			m_objectiveLayoutWaitCounter++;
			m_modifier.SetData(list2.ToArray(), StartRepositionMissionGoalTable, _hasToBeDone: false, _useDots: true);
		}
		if (m_objectiveLayoutWaitCounter == 0)
		{
			StartRepositionMissionGoalTable();
		}
		m_missionGoalsScrollView.ResetPosition();
		if (m_missionResultData.ObjectiveType == EObjectiveType.Story_EObjectiveType || m_missionResultData.ObjectiveType == EObjectiveType.Tutorial_EObjectiveType)
		{
			UIMissionUtil.UpdateMissionIconByMissionObjectiveType(m_missionIcon, m_missionResultData.ObjectiveType, _isElite: false, m_missionIconBackground);
		}
		else
		{
			UIMissionUtil.UpdateMissionIconByMissionObjectiveType(m_missionIcon, m_missionResultData.BaseType, m_missionResultData.MissionGoals.IsElite, m_missionIconBackground);
		}
		UnityEngine.Animation[] array = new UnityEngine.Animation[num + 1];
		array[0] = m_mainGoal.CheckboxAnimation;
		for (int num2 = 0; num2 < num; num2++)
		{
			array[num2 + 1] = m_challenges.ChallengeEntries[num2].CheckboxAnimation;
		}
		m_objectiveTweenAnimation.SetAnimationList(array, string.Empty);
	}

	private void StartRepositionMissionGoalTable()
	{
		m_objectiveLayoutWaitCounter--;
		if (m_objectiveLayoutWaitCounter <= 0)
		{
			m_missionGoalsTable.repositionNow = true;
			UITable missionGoalsTable = m_missionGoalsTable;
			missionGoalsTable.onReposition = (UITable.OnReposition)Delegate.Combine(missionGoalsTable.onReposition, new UITable.OnReposition(OnMissionGoalReposition));
		}
	}

	private void OnMissionGoalReposition()
	{
		UITable missionGoalsTable = m_missionGoalsTable;
		missionGoalsTable.onReposition = (UITable.OnReposition)Delegate.Remove(missionGoalsTable.onReposition, new UITable.OnReposition(OnMissionGoalReposition));
		m_missionGoalsScrollView.ResetPosition();
		UpdateMissionChildrenReposition();
	}
}
