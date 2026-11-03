using System;
using System.Collections;
using System.Collections.Generic;
using AcierProtocol;
using Assets.Scripts.Audio;
using Assets.Scripts.Mission.Definitions;
using Assets.Scripts.Mission.Logic;
using Assets.Scripts.Mission.Logic.CharacterPooling;
using Assets.Scripts.Mission.Mission;
using Assets.Scripts.Mission.Mission.Interfaces;
using Assets.Scripts.Mission.UI;
using Assets.Scripts.Mission.Utilities;
using Assets.Scripts.Mission.Utilities.AutoUpdating;
using Assets.Scripts.Network.Missions.Mission.Data;
using Assets.Scripts.UI.State;
using Assets.Scripts.UI.Tutorial;
using Database;
using Mission;
using SharedBaseLib.Core;
using UbiCore.Debug;
using UbiCore.Utils;
using UnityEngine;

namespace Assets.Scripts.Mission;

public class MissionManager : UbiCore.Utils.Singleton<MissionManager>
{
	private enum EMissionJob
	{
		JobEmpty,
		JobInitMission,
		JobInitMissionPhase2
	}

	private enum EEndMissionEffectType
	{
		Success,
		Fail,
		RevivePending,
		RevivePending2,
		Revive,
		Revive2,
		ReviveFadeout
	}

	public delegate void MissionStateChangeHandler(EMissionState _state);

	public delegate void OnMissionStateChangedDelegate(EMissionState _missionState);

	private const float c_MissionPhase2Progress = 1f;

	private const float c_MissionEndDuration = 2f;

	private bool m_isMissionCompleted;

	private bool m_isMissionEnded;

	private bool m_isMissionLoaded;

	private bool m_ignoreObjectiveStateChange;

	private MissionLoader m_missionLoader;

	private MissionTracker m_missionTracker = new MissionTracker();

	private EMissionState m_missionState;

	private EMissionType m_missionType;

	private bool m_waitForRevive;

	private BBList<IObjectiveBase> m_objectives = new BBList<IObjectiveBase>();

	private float m_missionEndTime;

	private GameObject m_LastEndEffect;

	private bool m_Muted;

	private bool m_OfferingReviveReady;

	private EMissionJob m_nextJob;

	public bool DoIntroduction { get; private set; }

	public bool DoOutro { get; private set; }

	public MissionTracker MissionTracker => m_missionTracker;

	public MissionRoot MissionMain => m_missionLoader.MissionMain;

	public float MissionLoadProgress { get; private set; }

	public MissionPhase Phase { get; private set; }

	public EMissionState MissionState
	{
		get
		{
			return m_missionState;
		}
		set
		{
			if (m_missionState != value)
			{
				EndMission();
				m_missionState = value;
				OnMissionStateChanged(value);
			}
		}
	}

	public bool OfferingRevive => WaitForRevive && m_OfferingReviveReady;

	public bool WaitForRevive
	{
		get
		{
			return m_waitForRevive;
		}
		set
		{
			if (m_waitForRevive && !value)
			{
				for (int i = 0; i < m_objectives.Count; i++)
				{
					ObjectiveStayAlive objectiveStayAlive = m_objectives[i] as ObjectiveStayAlive;
					if (!(objectiveStayAlive == null))
					{
						OnStateChanged(objectiveStayAlive);
						if (objectiveStayAlive.Result == global::Mission.EObjectiveResult.Failed)
						{
							MissionState = EMissionState.Failed;
							ShowEndMissionEffect(EEndMissionEffectType.Fail);
						}
						else
						{
							ShowEndMissionEffect(EEndMissionEffectType.Revive);
						}
					}
				}
				m_OfferingReviveReady = false;
			}
			else if (!m_waitForRevive && value)
			{
				ShowEndMissionEffect(EEndMissionEffectType.RevivePending);
				UbiCore.Utils.Singleton<UiMain>.Instance.SetIngameUiVisibility(_visible: false);
				AcierInputHandler.Instance.isEnabled = false;
			}
			m_waitForRevive = value;
		}
	}

	public ICharacterManager CharacterManager { get; private set; }

	public AreaManager AreaManager { get; private set; }

	public List<IObjectiveBase> Objectives
	{
		get
		{
			return m_objectives;
		}
		set
		{
			DisconnectObjectives();
			if (value != null)
			{
				m_objectives = new BBList<IObjectiveBase>(value);
				ConnectObjectives();
			}
			else
			{
				m_objectives = null;
			}
		}
	}

	public GameObject Root => m_missionLoader.Root;

	public int MissionTargetLevel
	{
		get
		{
			if (Singleton<StateManager>.Instance.MissionSelectionManager.IsSelectedMissionCampaign)
			{
				return MissionMain.Data.DifficultyLevel;
			}
			HeroicRankMapping.Entry[] heroicRankMapping = Singleton<StateManager>.Instance.TemplateManager.HeroicRankMapping;
			return Mathf.Clamp(Singleton<StateManager>.Instance.ProfileManager.CurrentAssassin.level, heroicRankMapping[MissionTargetRank].randomMissionsScalingLevelLower, heroicRankMapping[MissionTargetRank].randomMissionsScalingLevelUpper);
		}
	}

	public int MissionTargetRank
	{
		get
		{
			if (Singleton<StateManager>.Instance.MissionSelectionManager.IsSelectedMissionCampaign)
			{
				return MissionMain.Data.Rank;
			}
			return Singleton<StateManager>.Instance.MissionSelectionManager.ContractMission.rank;
		}
	}

	public float MissionTargetRankMultiplier
	{
		get
		{
			HeroicRankMapping.Entry[] heroicRankMapping = Singleton<StateManager>.Instance.TemplateManager.HeroicRankMapping;
			if (heroicRankMapping == null)
			{
				return 1f;
			}
			return heroicRankMapping[MissionTargetRank].missionsScalingMultiplier;
		}
	}

	public bool ShowHidingSpots => ObjectivesRelyingOnHidingSpotsCount > 0;

	public int ObjectivesRelyingOnHidingSpotsCount { get; set; }

	public event MissionStateChangeHandler OnMissionStateChanged = delegate
	{
	};

	public MissionManager()
	{
		ObjectivesRelyingOnHidingSpotsCount = 0;
		m_missionLoader = new MissionLoader();
		m_missionLoader.OnMissionLoaded += OnMissionLoaded;
		OnMissionStateChanged = (MissionStateChangeHandler)Delegate.Combine(OnMissionStateChanged, new MissionStateChangeHandler(HandleMissionStateChanged));
		MissionLoadProgress = 0f;
	}

	public void Mute()
	{
		m_Muted = true;
	}

	public void Go()
	{
		m_Muted = false;
		if (DoIntroduction)
		{
			DoIntroduction = false;
			MissionState = EMissionState.ShowIntroduction;
			UbiCore.Utils.Singleton<Timer>.Instance.Pause(Timer.EPauseState.Running);
			return;
		}
		MissionState = EMissionState.InProgress;
		AudioManager.Instance.EventManager.OnRegionIntroEnded();
		Timer.EPauseState pauseState = UbiCore.Utils.Singleton<Timer>.Instance.PauseState;
		UbiCore.Utils.Singleton<Timer>.Instance.Reset();
		UbiCore.Utils.Singleton<Timer>.Instance.Pause(pauseState);
		UbiCore.Utils.Singleton<Timer>.Instance.AddCallback(CharacterManager.Update, 0.25f, _unscaled: true);
	}

	public bool IsEntryActive(IMissionBase _missionBase)
	{
		return _missionBase.IsDynamic || m_missionLoader.State.MissionEntries.Contains(_missionBase);
	}

	public void PrepareMissionLoad(bool _isQuickRestart)
	{
		m_isMissionCompleted = false;
		m_isMissionEnded = false;
		WaitForRevive = false;
		DoIntroduction = !_isQuickRestart;
		MissionState = EMissionState.Loading;
		Main.Instance.SceneLoader.OnStep += m_missionLoader.OnSceneLoaderStep;
	}

	private void HandleMissionStateChanged(EMissionState state)
	{
		if ((state == EMissionState.Success || state == EMissionState.Failed) && !DoOutro && !OfferingRevive)
		{
			ShowEndMissionEffect((state != EMissionState.Success) ? EEndMissionEffectType.Fail : EEndMissionEffectType.Success);
			if (state == EMissionState.Failed)
			{
				LogicMain.MissionResult.ClearSucceededChallenges();
			}
			AcierInputHandler.Instance.DisableInput();
			PlayerHelper.StopMoving();
		}
		else
		{
			ClearEndMissionEffect();
		}
		if (state == EMissionState.Loading || state == EMissionState.Reloading)
		{
			AcierInputHandler.Instance.DisableInput();
		}
		if (state == EMissionState.WaitingForGo || state == EMissionState.ShowIntroduction || state == EMissionState.InProgress)
		{
			AcierInputHandler.Instance.isEnabled = true;
		}
	}

	private void ConnectObjectives()
	{
		if (Objectives == null)
		{
			return;
		}
		for (int i = 0; i < Objectives.Count; i++)
		{
			IObjectiveBase objectiveBase = Objectives[i];
			objectiveBase.OnStateChanged += OnStateChanged;
			if (objectiveBase is IObjectiveValue objectiveValue)
			{
				objectiveValue.ValueChanged += ObjectiveWithValueChanged;
			}
		}
	}

	private static void ObjectiveWithValueChanged()
	{
		AudioManager.Instance.Play(UbiCore.Utils.Singleton<AudioGameIds>.Instance.ac_sfx_ui_mission_update);
	}

	private void DeInitMissionSystem()
	{
		CharacterManager.OnMissionUnloaded();
		AutoUpdateManager.Reset();
		UbiCore.Utils.Singleton<UiMain>.Instance.OnMissionUnloaded();
		LogicMain.OnMissionUnloaded();
		m_missionTracker.OnUnload();
		CharacterManager = null;
		AreaManager = null;
	}

	private void DisconnectObjectives()
	{
		if (Objectives == null)
		{
			return;
		}
		for (int i = 0; i < Objectives.Count; i++)
		{
			IObjectiveBase objectiveBase = Objectives[i];
			objectiveBase.OnStateChanged -= OnStateChanged;
			if (objectiveBase is IObjectiveValue objectiveValue)
			{
				objectiveValue.ValueChanged -= ObjectiveWithValueChanged;
			}
		}
	}

	private void EndMission()
	{
		if (!m_isMissionEnded && m_isMissionCompleted)
		{
			m_isMissionCompleted = false;
			m_isMissionEnded = true;
			m_missionEndTime = UbiCore.Utils.Singleton<Timer>.Instance.realTime;
		}
	}

	private IEnumerator JobInitMission()
	{
		Profiling.Start("MissionManager::JobInitMission", 0f);
		m_missionType = Main.Instance.SceneLoader.CurrentMissionDescription.MissionType;
		SetOverrideAvatar();
		PreInitMissionSystem();
		m_nextJob = EMissionJob.JobInitMissionPhase2;
		Profiling.Stop();
		yield break;
	}

	private IEnumerator JobInitMissionPhase2()
	{
		if (!Profiler.enabled)
		{
			Profiling.Start("MissionManager::JobInitMissionPhase2", 0f);
		}
		yield return Main.Instance.StartCoroutine(m_missionLoader.State.Load((float _progress) =>
		{
			MissionLoadProgress = 1f + _progress * 1f;
			return true;
		}));
		Phase = m_missionLoader.State;
		Objectives = Phase.MissionObjectives;
		InitMissionSystem();
		yield return UnityEngine.Resources.UnloadUnusedAssets();
		MissionLoadProgress = 1f;
		MissionState = EMissionState.WaitingForGo;
		m_nextJob = EMissionJob.JobEmpty;
		if (!Profiler.enabled)
		{
			Profiling.Stop();
		}
	}

	private void SetOverrideAvatar()
	{
		if (!Main.Instance.BuildDefinition.HasUsedDebugAvatarSelection)
		{
			Main.Instance.BuildDefinition.AvatarFixed = false;
			MissionRootData data = m_missionLoader.MissionMain.Data;
			bool avatarFixed = false;
			string playerAssassinGUID = data.PlayerAssassinGUID;
			if (!string.IsNullOrEmpty(playerAssassinGUID))
			{
				GameID gameID = Main.Instance.GameDB.FindGameIdFromGuid(playerAssassinGUID);
				AvatarData.AvatarDefinition item = Main.Instance.GameDB.GetItem<AvatarData.AvatarDefinition>(gameID);
				Main.Instance.BuildDefinition.Avatar = item.Name;
				avatarFixed = true;
			}
			string playerHirelingGUID = data.PlayerHirelingGUID;
			if (!string.IsNullOrEmpty(playerHirelingGUID))
			{
				GameID gameID2 = Main.Instance.GameDB.FindGameIdFromGuid(playerHirelingGUID);
				AvatarData.AvatarDefinition item2 = Main.Instance.GameDB.GetItem<AvatarData.AvatarDefinition>(gameID2);
				Main.Instance.BuildDefinition.Hireling = item2.Name;
				avatarFixed = true;
			}
			Main.Instance.BuildDefinition.AvatarFixed = avatarFixed;
		}
	}

	public void Update()
	{
		Profiling.Start("MissionManager::Update", float.MaxValue);
		if (m_nextJob != EMissionJob.JobEmpty)
		{
			EMissionJob nextJob = m_nextJob;
			m_nextJob = EMissionJob.JobEmpty;
			switch (nextJob)
			{
			case EMissionJob.JobInitMission:
				Main.Instance.StartCoroutine(JobInitMission());
				break;
			case EMissionJob.JobInitMissionPhase2:
				MissionLoadProgress = 1f;
				Main.Instance.StartCoroutine(JobInitMissionPhase2());
				break;
			}
		}
		bool flag = UbiCore.Utils.Singleton<MissionManager>.Instance.MissionState == EMissionState.InProgress;
		if (flag || (m_isMissionEnded && UbiCore.Utils.Singleton<Timer>.Instance.realTime - m_missionEndTime < 2f) || MissionState == EMissionState.ShowOutro)
		{
			LogicMain.UpdateFrame(UbiCore.Utils.Singleton<Timer>.Instance.deltaTime);
			if (!m_Muted)
			{
				Phase.Update();
			}
		}
		AcierCameraController instance = AcierCameraController.Instance;
		if (instance != null && (flag || MissionState == EMissionState.ShowIntroduction || MissionState == EMissionState.ShowOutro || MissionState == EMissionState.Failed))
		{
			AcierCameraController.Instance.UpdateFrame();
		}
		if (MissionState == EMissionState.ShowOutro && (Outro.Instance == null || Outro.Instance.IsFinished))
		{
			EndOutro();
		}
		Profiling.Stop();
	}

	private void InitMissionSystem()
	{
		LogicMain.LogicInterface.Tutorial_SetActive(m_missionLoader.State.NeedsTutorialSystem());
		m_missionTracker.OnLoaded();
		LogicMain.OnMissionLoaded();
		UbiCore.Utils.Singleton<UiMain>.Instance.OnMissionLoaded();
		m_isMissionLoaded = true;
	}

	private void OnMissionLoaded(MissionLoader _sender)
	{
		m_nextJob = EMissionJob.JobInitMission;
		Main.Instance.SceneLoader.OnStep -= m_missionLoader.OnSceneLoaderStep;
		MissionLoadProgress = 0f;
		DoIntroduction &= CameraStateIntroduction.HasSceneIntroAnimation();
		TutorialCategory category = UbiCore.Utils.Singleton<UiMain>.Instance.TutorialManager.GetCategory(ETutorialCategory.TutCatIntroMission);
		DoOutro = category != null && !category.Finished;
		if (DoOutro)
		{
			GameObject original = UnityEngine.Resources.Load("Effects/Outro/TutorialOutro") as GameObject;
			GameObject gameObject = UnityEngine.Object.Instantiate(original);
			Outro component = gameObject.GetComponent<Outro>();
			if (component != null)
			{
				component.DisableColorCorrectionDuringFadeout = true;
			}
		}
	}

	private void OnStateChanged(IObjectiveBase _objective)
	{
		if (MissionState != EMissionState.InProgress || m_ignoreObjectiveStateChange || _objective.Result == global::Mission.EObjectiveResult.Undefined)
		{
			return;
		}
		if (_objective is ObjectiveStayAlive && !WaitForRevive)
		{
			if (Main.Instance.IsNetworkEnabled && Singleton<StateManager>.Instance.TemplateManager.Globals.reviveFeatureEnabled && Singleton<StateManager>.Instance.ProfileManager.EntitlementManager.HasEntitlement(StaticEntitlementIds.ReviveEntitlementGUID))
			{
				WaitForRevive = true;
				return;
			}
			m_waitForRevive = true;
			WaitForRevive = false;
		}
		else if (StillProgressing(_objective))
		{
			m_ignoreObjectiveStateChange = true;
			Main.Instance.StartCoroutine(ProgressAsync(_objective));
			m_ignoreObjectiveStateChange = false;
		}
	}

	private bool StillProgressing(IObjectiveBase _objective)
	{
		if (m_waitForRevive)
		{
			return false;
		}
		if (MissionState != EMissionState.InProgress)
		{
			return false;
		}
		global::Mission.EObjectiveResult eObjectiveResult = Phase.IsFullFilled(_ignoreProgress: true);
		global::Mission.EObjectiveResult eObjectiveResult2 = Phase.IsFullFilled(_ignoreProgress: false);
		if (eObjectiveResult != global::Mission.EObjectiveResult.Undefined && eObjectiveResult2 != global::Mission.EObjectiveResult.Undefined)
		{
			m_isMissionCompleted = true;
			if (DoOutro && Outro.Instance != null)
			{
				StartOutro();
			}
			else
			{
				EMissionState eMissionState = ((eObjectiveResult2 != global::Mission.EObjectiveResult.Failed) ? EMissionState.Success : EMissionState.Failed);
				if (eMissionState == EMissionState.Failed)
				{
					ObjectiveBaseData objectiveData = _objective.ObjectiveData;
					LogicMain.MissionResult.FailMissionID = objectiveData.ID;
					if (int.TryParse(objectiveData.ShortDescription, out var result))
					{
						LogicMain.MissionResult.FailMissionLocaID = result;
					}
				}
				MissionState = eMissionState;
			}
			Phase.Deactivate();
		}
		return MissionState == EMissionState.InProgress;
	}

	private void StartOutro()
	{
		AcierInputHandler.Instance.StartOutro();
		UbiCore.Utils.Singleton<UiMain>.Instance.StartOutro();
		Outro.Instance.StartOutro();
		MissionState = EMissionState.ShowOutro;
	}

	private void EndOutro()
	{
		global::Mission.EObjectiveResult eObjectiveResult = Phase.IsFullFilled(_ignoreProgress: false);
		MissionState = ((eObjectiveResult != global::Mission.EObjectiveResult.Failed) ? EMissionState.Success : EMissionState.Failed);
	}

	private void ClearEndMissionEffect()
	{
		if (m_LastEndEffect != null)
		{
			m_LastEndEffect.GetComponent<MissionEndEffect>().EndEffect();
			m_LastEndEffect = null;
		}
	}

	private void ShowEndMissionEffect(EEndMissionEffectType _type)
	{
		bool flag = m_LastEndEffect != null;
		if (!flag)
		{
			m_LastEndEffect = UnityEngine.Object.Instantiate(UnityEngine.Resources.Load<GameObject>("Effects/Prefabs/MissionEndEffect"));
		}
		MissionEndEffect component = m_LastEndEffect.GetComponent<MissionEndEffect>();
		switch (_type)
		{
		case EEndMissionEffectType.Success:
			component.Config = component.SuccessConf;
			component.DoSlowMotion = true;
			component.Reverse = true;
			component.AutoDestroy = false;
			component.EventAction = null;
			break;
		case EEndMissionEffectType.Fail:
			component.Config = component.FailConf;
			component.DoSlowMotion = true;
			component.Reverse = true;
			component.AutoDestroy = false;
			component.EventAction = null;
			break;
		case EEndMissionEffectType.RevivePending:
			component.Config = component.RevivePendingConf;
			component.DoSlowMotion = false;
			component.Reverse = true;
			component.AutoDestroy = false;
			component.EventAction = RevivePendingDelayFinished;
			break;
		case EEndMissionEffectType.RevivePending2:
			component.Config = component.RevivePendingConf;
			component.DoSlowMotion = true;
			component.Reverse = true;
			component.AutoDestroy = false;
			component.EventAction = null;
			break;
		case EEndMissionEffectType.Revive:
			component.Config = component.ReviveConf;
			component.DoSlowMotion = false;
			component.Reverse = true;
			component.AutoDestroy = false;
			component.EventAction = ReviveFinished;
			break;
		case EEndMissionEffectType.Revive2:
			component.Config = component.Revive2Conf;
			component.DoSlowMotion = false;
			component.Reverse = true;
			component.AutoDestroy = false;
			component.EventAction = Revive2Finished;
			break;
		case EEndMissionEffectType.ReviveFadeout:
			component.Config = component.ReviveFadeoutConf;
			component.DoSlowMotion = false;
			component.Reverse = false;
			component.AutoDestroy = true;
			component.EventAction = null;
			break;
		}
		if (flag)
		{
			component.StartEffect();
		}
	}

	private void RevivePendingDelayFinished(MissionEndEffect _missionEndEffect)
	{
		ShowEndMissionEffect(EEndMissionEffectType.RevivePending2);
		m_OfferingReviveReady = true;
	}

	private void ReviveFinished(MissionEndEffect _missionEndEffect)
	{
		ShowEndMissionEffect(EEndMissionEffectType.Revive2);
		LogicMain.PlayerEntity.FinalizeRevive();
		UbiCore.Utils.Singleton<UiMain>.Instance.SetIngameUiVisibility(_visible: true);
		AcierInputHandler.Instance.isEnabled = true;
	}

	private void Revive2Finished(MissionEndEffect _missionEndEffect)
	{
		ShowEndMissionEffect(EEndMissionEffectType.ReviveFadeout);
	}

	private IEnumerator ProgressAsync(IObjectiveBase _objective)
	{
		yield return Main.Instance.StartCoroutine(Phase.Progress(_objective));
		Objectives = Phase.MissionObjectives;
		Phase.FinalizeProgress();
		LogicMain.LogicInterface.Tutorial_SetActive(m_missionLoader.State.NeedsTutorialSystem());
		if (StillProgressing(_objective))
		{
			MissionTracker.UpdateObjectives();
			MissionState = EMissionState.InProgress;
		}
	}

	private void PreInitMissionSystem()
	{
		CharacterManager = new DefaultCharacterManager();
		CharacterManager.OnMissionLoad();
		AreaManager = new AreaManager();
		LogicMain.OnMissionLoad(m_missionType);
		UbiCore.Utils.Singleton<Timer>.Instance.Reset();
		UbiCore.Utils.Singleton<Timer>.Instance.Unpause();
		UbiCore.Utils.Singleton<UiMain>.Instance.OnMissionLoad(m_missionType, LogicMain.LogicInterface);
	}

	protected override void OnDestroy()
	{
		if (m_isMissionLoaded && !SharedMain.IsShuttingDown)
		{
			if (!m_isMissionEnded)
			{
				EndMission();
			}
			MissionState = EMissionState.Unloading;
			Rendering.Instance.OnMissionUnload();
			UbiCore.Utils.Singleton<UiMain>.Instance.OnMissionUnload();
			AcierInputHandler.Instance.isEnabled = false;
			m_missionLoader.Destroy();
			m_missionLoader.OnMissionLoaded -= OnMissionLoaded;
			Objectives = null;
			MissionMonoBehaviour.Reset();
			MissionTarget.Reset();
			DeInitMissionSystem();
			m_isMissionLoaded = false;
			MissionState = EMissionState.NoMission;
			Singleton<EffectManager>.Instance.DestroyActiveEffects();
		}
	}
}
