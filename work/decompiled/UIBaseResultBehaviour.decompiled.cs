using System.Collections;
using AcierProtocol;
using Assets.Scripts.Mission.HUD;
using Assets.Scripts.Mission.Logic;
using Assets.Scripts.Mission.UI;
using Assets.Scripts.Network.Missions.Mission.Data;
using Assets.Scripts.UI.State;
using Assets.Scripts.UI.Utils.Extensions;
using UbiCore.Utils;
using UnityEngine;

namespace Assets.Scripts.UI.Ingame.Behaviours;

public abstract class UIBaseResultBehaviour : UIBaseBehaviour
{
	public delegate void TapHandler();

	[SerializeField]
	protected UIExtendedButton m_retryButton;

	[SerializeField]
	protected UIExtendedButton m_nextMissionButton;

	[SerializeField]
	protected UIExtendedLabel m_missionName;

	[SerializeField]
	protected UnityEngine.Animation m_mainAnimation;

	[SerializeField]
	protected GameObject m_cancelAnimationOverlay;

	[SerializeField]
	protected UIEventTrigger m_cancelAnimationEventTrigger;

	[SerializeField]
	protected UIPanel m_screenFlash;

	[SerializeField]
	protected UnityEngine.Animation m_flashAnimation;

	[SerializeField]
	protected UIOnScreenMessageBehaviour m_onScreenMessage;

	[HideInInspector]
	public bool IsAnimationPlaying;

	protected bool m_hasAnimationStarted;

	protected bool m_missionCompleted;

	protected MissionRootData m_missionDescription;

	private bool m_cancelAnimationTriggered;

	private bool m_everyplayRecordingWasStopped;

	public TapHandler OnRetryTapped;

	public TapHandler OnNextMissionTapped;

	public TapHandler OnReturnToAnimusTapped;

	protected Assassin AssassinDataBeforeMissionStart { get; set; }

	protected abstract string GetAnimationName();

	public virtual void UpdateScreen()
	{
		m_mainAnimation.UpdateAnimationSpeed();
		bool flag = (m_hasAnimationStarted && m_mainAnimation.isPlaying) || IsAnimationPlaying;
		if (flag && !m_everyplayRecordingWasStopped)
		{
			m_everyplayRecordingWasStopped = true;
			if (EveryplayManager.IsRecording)
			{
				EveryplayManager.StopRecording(_showSharingDialog: true);
			}
		}
		bool active = !m_hasAnimationStarted || flag;
		m_cancelAnimationOverlay.SetActive(active);
		GamepadWrapper gamepad = AcierInputHandler.Instance.Gamepad;
		if (flag && !m_cancelAnimationTriggered && gamepad.AcceptDown)
		{
			Input.ResetInputAxes();
			m_cancelAnimationTriggered = true;
			OnTapInstantFinishAnimation();
		}
	}

	public virtual void SetMissionData(bool _missionCompleted, MissionRootData _missionDescription)
	{
		UbiCore.Utils.Singleton<UiMain>.Instance.GetSubSystemByType<UIOnScreenMessage>().ScreenMessageBehaviour = m_onScreenMessage;
		m_everyplayRecordingWasStopped = false;
	}

	protected abstract void UpdateMissionRelatedData();

	protected virtual void ValidateFields()
	{
	}

	protected IEnumerator PlayMainAnimation()
	{
		m_mainAnimation.SampleAnimation(0f, string.Empty);
		yield return new WaitForEndOfFrame();
		gameObject.GetComponent<UIPanel>().alpha = 1f;
		m_mainAnimation.Play(GetAnimationName());
		m_mainAnimation.Sample();
		m_hasAnimationStarted = true;
	}

	protected void OnTapRetry()
	{
		if (OnRetryTapped != null)
		{
			OnRetryTapped();
		}
	}

	protected void OnTapNextMission()
	{
		if (OnNextMissionTapped != null)
		{
			OnNextMissionTapped();
		}
	}

	public virtual void Init()
	{
		m_cancelAnimationTriggered = false;
		m_onScreenMessage.gameObject.SetActive(value: false);
		EventDelegate.Add(m_retryButton.onClick, OnTapRetry);
		EventDelegate.Add(m_nextMissionButton.onClick, OnTapNextMission);
		m_screenFlash.gameObject.SetActive(value: false);
		m_hasAnimationStarted = false;
		EventDelegate.Add(m_cancelAnimationEventTrigger.onClick, OnTapInstantFinishAnimation);
	}

	public virtual void SetMissionRewards(MissionReward _missionReward)
	{
	}

	public virtual void UpdatePlayerPortrait(EntityId _entityId)
	{
	}

	public void SetOlduserProfile(UserProfile _oldUserProfile)
	{
		AssassinDataBeforeMissionStart = null;
		Assassin currentAssassin = Singleton<StateManager>.Instance.ProfileManager.CurrentAssassin;
		if (currentAssassin != null)
		{
			AssassinDataBeforeMissionStart = _oldUserProfile.GetAssassin(currentAssassin.id);
		}
	}

	public override void SetVisibility(bool _visible)
	{
		m_visible = _visible;
		gameObject.SetActive(_visible);
	}

	public void OnTapInstantFinishAnimation()
	{
		StartCoroutine(PlayInstantFinishAnimation());
	}

	private IEnumerator PlayInstantFinishAnimation()
	{
		WaitForEndOfFrame wait = new WaitForEndOfFrame();
		m_screenFlash.gameObject.SetActive(value: true);
		m_flashAnimation.SampleAnimation(0f, string.Empty);
		m_flashAnimation.Play();
		while (m_flashAnimation.isPlaying)
		{
			m_flashAnimation.UpdateAnimationSpeed();
			yield return wait;
		}
		m_screenFlash.gameObject.SetActive(value: false);
	}

	public void OnInstantFinishAnimation()
	{
		m_mainAnimation.Stop(GetAnimationName());
		m_mainAnimation.SampleAnimation(1f, string.Empty);
	}

	protected void OnDestroy()
	{
		EventDelegate.Remove(m_cancelAnimationEventTrigger.onClick, OnTapInstantFinishAnimation);
		EventDelegate.Remove(m_retryButton.onClick, OnTapRetry);
		EventDelegate.Remove(m_nextMissionButton.onClick, OnTapNextMission);
	}
}
