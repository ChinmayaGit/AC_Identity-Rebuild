using System;
using AcierProtocol;
using Assets.Scripts.Audio;
using Assets.Scripts.Mission.Logic;
using Assets.Scripts.Mission.UI;
using Assets.Scripts.UI;
using Assets.Scripts.UI.Helpers;
using Assets.Scripts.UI.Ingame.Behaviours;
using Assets.Scripts.UI.Screens.Popup.AppRatingPopup;
using Assets.Scripts.UI.Screens.Popup.CallToActionPopup;
using Assets.Scripts.UI.Screens.Popup.FriendHirelingPopup;
using Mission;
using UbiCore.Messenger;
using UbiCore.Utils;
using UnityEngine;

namespace Assets.Scripts.Mission.HUD;

public class UIMissionResult : UIBase
{
	private UIBaseResultBehaviour m_missionResultBehaviour;

	private bool m_isWaitingForEndMission;

	private float m_showScreenDelayTimer;

	public override UIScreenWidgetManager.EUiScreenWidget ScreenWidgetType
	{
		get
		{
			if (Main.Instance.SceneLoader.CurrentMissionDescription.ObjectiveType == EObjectiveType.Tutorial_EObjectiveType)
			{
				return UIScreenWidgetManager.EUiScreenWidget.TutorialResult;
			}
			return UIScreenWidgetManager.EUiScreenWidget.MissionResult;
		}
	}

	public override UIBaseBehaviour Details => m_missionResultBehaviour;

	public override bool InitiallyVisible => false;

	public override bool Tap(TapEvent _gesture)
	{
		return m_missionResultBehaviour != null && m_missionResultBehaviour.gameObject.activeInHierarchy;
	}

	public override bool IsOnUi(Vector2 _position)
	{
		return m_missionResultBehaviour != null && m_missionResultBehaviour.gameObject.activeInHierarchy;
	}

	public override void OnSceneLoaded()
	{
		base.OnSceneLoaded();
		if (IsActiveOnSceneLoaded)
		{
			Init();
		}
	}

	public override void OnSceneUnload()
	{
		IsInitialized = false;
		if (m_missionResultBehaviour != null)
		{
			UIBaseResultBehaviour missionResultBehaviour = m_missionResultBehaviour;
			missionResultBehaviour.OnNextMissionTapped = (UIBaseResultBehaviour.TapHandler)Delegate.Remove(missionResultBehaviour.OnNextMissionTapped, new UIBaseResultBehaviour.TapHandler(OnTapNextMission));
			UIBaseResultBehaviour missionResultBehaviour2 = m_missionResultBehaviour;
			missionResultBehaviour2.OnReturnToAnimusTapped = (UIBaseResultBehaviour.TapHandler)Delegate.Remove(missionResultBehaviour2.OnReturnToAnimusTapped, new UIBaseResultBehaviour.TapHandler(OnTapReturnToAnimus));
			UIBaseResultBehaviour missionResultBehaviour3 = m_missionResultBehaviour;
			missionResultBehaviour3.OnRetryTapped = (UIBaseResultBehaviour.TapHandler)Delegate.Remove(missionResultBehaviour3.OnRetryTapped, new UIBaseResultBehaviour.TapHandler(OnTapRetry));
		}
		m_missionResultBehaviour = null;
	}

	public override void OnMissionUnloaded()
	{
		if (IsActive)
		{
			base.OnMissionUnloaded();
			m_isWaitingForEndMission = false;
			m_showScreenDelayTimer = 0f;
		}
	}

	public override void OnMissionEnded()
	{
		base.OnMissionEnded();
		if (IsActive && m_missionResultBehaviour != null)
		{
			m_showScreenDelayTimer = 0.1f;
			m_isWaitingForEndMission = true;
		}
	}

	public void Init()
	{
		if (m_missionResultBehaviour != null)
		{
			return;
		}
		UIBaseBehaviour widgetByType = UbiCore.Utils.Singleton<UIScreenWidgetManager>.Instance.GetWidgetByType(ScreenWidgetType);
		if (widgetByType != null)
		{
			m_missionResultBehaviour = widgetByType as UIBaseResultBehaviour;
			if (m_missionResultBehaviour != null)
			{
				m_missionResultBehaviour.Init();
				UIBaseResultBehaviour missionResultBehaviour = m_missionResultBehaviour;
				missionResultBehaviour.OnNextMissionTapped = (UIBaseResultBehaviour.TapHandler)Delegate.Combine(missionResultBehaviour.OnNextMissionTapped, new UIBaseResultBehaviour.TapHandler(OnTapNextMission));
				UIBaseResultBehaviour missionResultBehaviour2 = m_missionResultBehaviour;
				missionResultBehaviour2.OnReturnToAnimusTapped = (UIBaseResultBehaviour.TapHandler)Delegate.Combine(missionResultBehaviour2.OnReturnToAnimusTapped, new UIBaseResultBehaviour.TapHandler(OnTapReturnToAnimus));
				UIBaseResultBehaviour missionResultBehaviour3 = m_missionResultBehaviour;
				missionResultBehaviour3.OnRetryTapped = (UIBaseResultBehaviour.TapHandler)Delegate.Combine(missionResultBehaviour3.OnRetryTapped, new UIBaseResultBehaviour.TapHandler(OnTapRetry));
				SetVisible(_value: false);
				IsInitialized = true;
			}
		}
	}

	public void OnTapNextMission()
	{
		PlayTapSound();
		UIMissionHelper.PlayNextMission();
	}

	public void OnTapReturnToAnimus()
	{
		PlayTapSound();
		CallToActionPopup.RecordMissionData();
		AppRatingPopup.LastMissionWasSuccess = UbiCore.Utils.Singleton<MissionManager>.Instance.MissionState == EMissionState.Success;
		UIMissionHelper.GoToAnimus();
	}

	public void OnTapReloadeUI()
	{
		OnMissionEnded();
	}

	public void OnTapRetry()
	{
		PlayTapSound();
		FriendHirelingPopup.ResetMissionData();
		CallToActionPopup.RecordMissionData();
		UIMissionHelper.RetryMission();
	}

	public override void UpdateFrame(float _deltaTime)
	{
		base.UpdateFrame(_deltaTime);
		if (m_isWaitingForEndMission && m_showScreenDelayTimer < 0f)
		{
			if (EveryplayManager.IsRecording)
			{
				EveryplayManager.StopRecording(_showSharingDialog: false);
			}
			m_isWaitingForEndMission = false;
			m_showScreenDelayTimer = -1f;
			SetData();
			SetVisible(_value: true);
		}
		else if (m_missionResultBehaviour != null && m_missionResultBehaviour.gameObject.activeSelf)
		{
			m_missionResultBehaviour.UpdateScreen();
		}
		if (m_isWaitingForEndMission)
		{
			m_showScreenDelayTimer -= _deltaTime;
		}
	}

	public void SetMissionReward(MissionReward _missionReward)
	{
		if (m_missionResultBehaviour != null)
		{
			m_missionResultBehaviour.SetMissionRewards(_missionReward);
		}
	}

	public override void SetVisible(bool _value)
	{
		if (m_missionResultBehaviour != null)
		{
			m_missionResultBehaviour.SetVisibility(_value);
		}
	}

	public void SetOldUserProfile(UserProfile profile)
	{
		m_missionResultBehaviour.SetOlduserProfile(profile);
	}

	public void Exclude()
	{
		AudioManager.Instance.EventManager.OnResultScreenExit();
		SetVisible(_value: false);
	}

	private void SetData()
	{
		bool missionCompleted = UbiCore.Utils.Singleton<MissionManager>.Instance.MissionState == EMissionState.Success;
		m_missionResultBehaviour.SetMissionData(missionCompleted, Main.Instance.SceneLoader.CurrentMissionDescription);
		EntityId playerId = UbiCore.Utils.Singleton<UiMain>.Instance.LogicInterface.PlayerId;
		m_missionResultBehaviour.UpdatePlayerPortrait(playerId);
	}

	private void PlayTapSound()
	{
		IAudioHandle audioHandle = AudioManager.Instance.CreateHandle(UbiCore.Utils.Singleton<AudioGameIds>.Instance.ac_sfx_ui_menu_global_accept, ELoadStrategy.LoadAll, EStoppedFinishedStrategy.UnloadAll);
		audioHandle.Load();
		audioHandle.Play();
	}
}
