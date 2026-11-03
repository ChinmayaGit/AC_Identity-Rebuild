using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AcierProtocol;
using Assets.Scripts.Mission;
using Assets.Scripts.Mission.HUD;
using Assets.Scripts.Mission.Mission;
using Assets.Scripts.Mission.Mission.Interfaces;
using Assets.Scripts.Mission.UI;
using Assets.Scripts.Network.Missions.Mission.Data;
using Assets.Scripts.UI.LoadoutScreen;
using Assets.Scripts.UI.Localization;
using Mission;
using SharedBaseLib.Core;
using UbiCore.Utils;
using UnityEngine;

public abstract class ObjectiveBase<TData> : MissionBase<TData>, IMissionBase, IObjectiveBase where TData : ObjectiveBaseData, new()
{
	private Mission.EObjectiveResult m_result;

	private int m_phaseEndMessage = -1;

	private StringMapper m_mapper;

	public MissionMonoBehaviour OnSuccess;

	public MissionMonoBehaviour OnFail;

	private readonly BBList<Vector3> m_targetPositions = new BBList<Vector3>();

	private readonly Regex m_patternRegex;

	private string m_pattern;

	private bool m_visualSummaryState;

	private EProgressState m_visualTrackerState;

	public string Description { get; protected set; }

	public ObjectiveBaseData ObjectiveData => (ObjectiveBaseData)MissionData;

	public EProgressState VisualTrackerState
	{
		get
		{
			return m_visualTrackerState;
		}
		protected set
		{
			if (value != m_visualTrackerState)
			{
				m_visualTrackerState = value;
				UpdateDescription();
			}
		}
	}

	public bool VisualSummaryState
	{
		get
		{
			return m_visualSummaryState;
		}
		protected set
		{
			if (m_visualSummaryState != value)
			{
				m_visualSummaryState = value;
				UpdateChallengeSuccess(_isSilentMode: false);
			}
		}
	}

	public override bool IsActive
	{
		get
		{
			return base.IsActive;
		}
		protected set
		{
			if (base.IsActive != value)
			{
				if (!value && Result == Mission.EObjectiveResult.Fulfilled)
				{
					ShowPhaseCompletionMessage();
				}
				base.IsActive = value;
			}
		}
	}

	public string RawDescription { get; private set; }

	public Mission.EObjectiveResult Result
	{
		get
		{
			return m_result;
		}
		protected set
		{
			if (m_result != value)
			{
				bool flag = true;
				if (Data.IsPrimaryObjective && UbiCore.Utils.Singleton<MissionManager>.Instance.MissionState == EMissionState.Failed)
				{
					flag = false;
					value = Mission.EObjectiveResult.Failed;
				}
				m_result = value;
				if (flag && m_result == Mission.EObjectiveResult.Failed && (!Data.HideDescription || Data.ShowFailMessageEvenIfHidden))
				{
					ShowFailMessage();
				}
				InvokeOnStateChanged();
			}
		}
	}

	public string ShortDescription { get; private set; }

	public string SecondaryDescription { get; private set; }

	public override MissionBaseData MissionData
	{
		get
		{
			return Data;
		}
		protected set
		{
			base.MissionData = value;
		}
	}

	protected abstract EProgressState InitialVisualTrackerState { get; }

	protected abstract bool InitialVisualSummaryState { get; }

	protected abstract Mission.EObjectiveResult InitialResult { get; }

	public string RawShortDescription { get; private set; }

	public string RawSecondaryDescription { get; private set; }

	public BBList<Vector3> TargetPositions => m_targetPositions;

	protected bool ShowNotificationsOnShutdown { get; private set; }

	public event DescriptionChangedDelegate OnDescriptionChanged;

	public event StateChanged OnStateChanged;

	public ObjectiveBase()
	{
		ShowNotificationsOnShutdown = false;
		RawShortDescription = string.Empty;
		RawSecondaryDescription = string.Empty;
		RawDescription = string.Empty;
		VisualTrackerState = EProgressState.NotStarted;
		string[] array = Enum.GetNames(typeof(EProgressState));
		Array.Resize(ref array, array.Length + 1);
		array[array.Length - 1] = "Multi";
		string text = string.Join("|", array);
		m_pattern = "\\[(?<Inverse>!?)(?<Status>" + text + ")(?<Caption>[^\\]]+)\\]";
		m_patternRegex = new Regex(m_pattern);
	}

	protected virtual void ShowFailMessage()
	{
		UbiCore.Utils.Singleton<UiMain>.Instance.GetSubSystemByType<UIOnScreenMessage>().ClearQueue();
	}

	public override void OnActivate()
	{
		base.OnActivate();
		LogicMain.MissionResult.RegisterObjective(this);
	}

	public virtual void FrameUpdateImpl()
	{
	}

	public virtual void TurnUpdateImpl()
	{
	}

	public override void OnPreActivate()
	{
		StartMission(_isRevive: false);
		base.OnPreActivate();
	}

	public override void OnDeactivate()
	{
		bool isActive = IsActive;
		Complete();
		if (isActive)
		{
			UpdateChallengeSuccess(_isSilentMode: false);
			ShowNotificationsOnShutdown = false;
		}
		LogicMain.MissionResult.UnregisterObjective(this);
		base.OnDeactivate();
	}

	public virtual void PrepareRevive()
	{
		StartMission(_isRevive: true);
	}

	public void UpdateFrame()
	{
		if (IsActive && UbiCore.Utils.Singleton<MissionManager>.Instance.MissionState == EMissionState.InProgress)
		{
			FrameUpdateImpl();
		}
	}

	public void UpdateTurn()
	{
		if (IsActive && UbiCore.Utils.Singleton<MissionManager>.Instance.MissionState == EMissionState.InProgress)
		{
			TurnUpdateImpl();
		}
	}

	public void UpdateDescription()
	{
		Description = ChooseDescription();
		ShortDescription = ChooseShortDescription();
		SecondaryDescription = ChooseSecondaryShortDescription();
	}

	protected string ChooseDescription()
	{
		string text = LocalizationManager.Instance.Get(RawDescription, GetStringMapper());
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		return SubstituteConditionalDescription(text);
	}

	protected string ChooseShortDescription()
	{
		string text = LocalizationManager.Instance.Get(RawShortDescription, GetStringMapper());
		if (text == null)
		{
			return string.Empty;
		}
		return SubstituteConditionalDescription(text);
	}

	protected string ChooseSecondaryShortDescription()
	{
		if (RawSecondaryDescription == null)
		{
			return null;
		}
		string text = LocalizationManager.Instance.Get(RawSecondaryDescription, GetStringMapper());
		if (text == null)
		{
			return string.Empty;
		}
		return SubstituteConditionalDescription(text);
	}

	private string SubstituteConditionalDescription(string _description)
	{
		MatchCollection matchCollection = m_patternRegex.Matches(_description);
		if (matchCollection.Count == 0)
		{
			return _description;
		}
		for (int i = 0; i < matchCollection.Count; i++)
		{
			Match match = matchCollection[i];
			bool inverse = match.Groups["Inverse"].Value == "!";
			string value = match.Groups["Status"].Value;
			bool flag = IsConditionToBeRemoved(value, inverse);
			_description = m_patternRegex.Replace(_description, (!flag) ? "${Caption}" : string.Empty, 1);
		}
		return _description;
	}

	protected virtual bool IsConditionToBeRemoved(string _statusText, bool _inverse)
	{
		if (_statusText != "Multi")
		{
			EProgressState eProgressState = (EProgressState)(int)Enum.Parse(typeof(EProgressState), _statusText, ignoreCase: true);
			return VisualTrackerState == EProgressState.NotStarted || (!_inverse && VisualTrackerState != eProgressState) || (_inverse && VisualTrackerState == eProgressState);
		}
		return true;
	}

	protected virtual void Complete()
	{
		UpdateDescription();
		if (IsActive)
		{
			IsActive = false;
			if (Result == Mission.EObjectiveResult.Undefined)
			{
				Result = Data.TerminationResult.ToResult();
				m_visualSummaryState = Result == Mission.EObjectiveResult.Fulfilled;
			}
		}
	}

	protected virtual void ChallengeSuccessChanged(bool _success)
	{
	}

	private void UpdateChallengeSuccess(bool _isSilentMode)
	{
		if (!Data.IsOptionalObjective || !Data.HideDescription)
		{
			return;
		}
		UIMissionGoals.GoalData challengeDescription = MissionMetaDataManager.Instance.GetChallengeDescription(Data.ID);
		if (challengeDescription == null)
		{
			return;
		}
		bool flag = UbiCore.Utils.Singleton<MissionManager>.Instance.MissionState == EMissionState.Failed;
		int textId;
		if (VisualSummaryState && !flag)
		{
			LogicMain.MissionResult.AddSucceededChallenge(challengeDescription.Id);
			textId = 5269;
		}
		else
		{
			LogicMain.MissionResult.RemoveSucceededChallenge(challengeDescription.Id);
			textId = 5270;
		}
		if (!_isSilentMode)
		{
			ChallengeSuccessChanged(VisualSummaryState);
			if (!challengeDescription.Hidden && !ShowNotificationsOnShutdown)
			{
				ShowNotificationsOnShutdown = true;
				StringMapper stringMapper = new StringMapper();
				stringMapper.Add("challengeText", challengeDescription.Name);
				StringMapper stringMapper2 = stringMapper;
				LogicMain.LogicInterface.OnSendInfo(EInfoEventType.TextAnnotation, textId, InfoEvent.ETextAnnotationMode.MessageOpen, -1, stringMapper2);
			}
		}
	}

	private void ShowPhaseCompletionMessage()
	{
		if (m_phaseEndMessage != -1)
		{
			LogicMain.LogicInterface.OnSendInfo(EInfoEventType.TextAnnotation, m_phaseEndMessage, InfoEvent.ETextAnnotationMode.MessageOpen, -1, GetStringMapper());
		}
	}

	public virtual StringMapper GetStringMapper()
	{
		if (m_mapper == null)
		{
			m_mapper = new StringMapper();
		}
		return m_mapper;
	}

	protected virtual void InvokeOnStateChanged()
	{
		OnStateChanged?.Invoke(this);
	}

	protected virtual void StartMission(bool _isRevive)
	{
		if (!Data.IsOptionalObjective || !Data.HideDescription || !_isRevive)
		{
			m_result = InitialResult;
			VisualTrackerState = InitialVisualTrackerState;
			m_visualSummaryState = InitialVisualSummaryState;
			if (_isRevive)
			{
				IsActive = true;
			}
		}
		if (!_isRevive)
		{
			UpdateChallengeSuccess(_isSilentMode: true);
		}
		UpdateDescription();
	}

	public override void OnPostRead(MissionBaseData _nodeData)
	{
		base.OnPostRead(_nodeData);
		Register(this);
		PrepareRawDescriptions();
		m_phaseEndMessage = Data.PhaseCompletedMessage;
	}

	public void PrepareRawDescriptions()
	{
		if (int.TryParse(Data.Description, out var result))
		{
			string raw = LocalizationManager.Instance.GetRaw(result);
			RawDescription = raw;
		}
		else
		{
			RawDescription = "NO LOCA: " + Data.Description;
		}
		if (int.TryParse(Data.ShortDescription, out var result2))
		{
			string raw2 = LocalizationManager.Instance.GetRaw(result2);
			RawShortDescription = raw2;
		}
		else
		{
			RawShortDescription = "NO LOCA: " + Data.ShortDescription;
		}
		RawSecondaryDescription = string.Empty;
		if (int.TryParse(Data.SecondaryDescription, out var result3))
		{
			if (result3 > 0)
			{
				string raw3 = LocalizationManager.Instance.GetRaw(result3);
				RawSecondaryDescription = raw3;
			}
		}
		else if (!string.IsNullOrEmpty(Data.SecondaryDescription))
		{
			RawSecondaryDescription = "NO LOCA: " + Data.SecondaryDescription;
		}
	}

	public override void OnReadCompleted()
	{
		base.OnReadCompleted();
		OnSuccess = (MissionMonoBehaviour)GetMissionObject(ObjectiveData.OnSuccessId);
		OnFail = (MissionMonoBehaviour)GetMissionObject(ObjectiveData.OnFailId);
	}

	protected static string FormatEntityList(List<IMissionEntity> _missionEntities)
	{
		string text = string.Empty;
		if (_missionEntities == null)
		{
			return text;
		}
		for (int i = 0; i < _missionEntities.Count; i++)
		{
			if (i > 0)
			{
				text += "\n";
			}
			string text2 = FormatEntity(_missionEntities[i] as MissionMonoBehaviour, i);
			text += text2;
		}
		return text;
	}

	protected static string FormatEntity(MissionMonoBehaviour _missionEntity, int _index)
	{
		if (_missionEntity == null)
		{
			return "- null -";
		}
		return $"[{_index}] {_missionEntity.name}";
	}

	protected List<T> ReadMissionEntities<T>(IList<long> _targets, bool _allowEmpty = false) where T : class, IMissionBase
	{
		BBList<T> bBList = new BBList<T>(_targets.Count);
		for (int i = 0; i < _targets.Count; i++)
		{
			long num = _targets[i];
			bool flag = num != -2;
			if (!_allowEmpty && !flag)
			{
				continue;
			}
			T val = (T)null;
			if (flag)
			{
				val = ReadMissionEntity<T>(num);
				if (val == null)
				{
					continue;
				}
			}
			bBList.Add(val);
		}
		return bBList;
	}

	protected T ReadMissionEntity<T>(long _id) where T : IMissionBase
	{
		if (_id == -2)
		{
			return default;
		}
		if (!(GetMissionObject(_id) is T val))
		{
			return default;
		}
		if (val is MissionNpcSpawn)
		{
			MissionNpcSpawn missionNpcSpawn = val as MissionNpcSpawn;
			string typeGuid = missionNpcSpawn.Data.TypeGuid;
			if (MissionMetaDataManager.IsAvailable && MissionMetaDataManager.Instance.MissionGoals.RolledNames.ContainsKey(typeGuid))
			{
				int rolledNameLocaId = MissionMetaDataManager.Instance.MissionGoals.RolledNames[typeGuid];
				missionNpcSpawn.RolledNameLocaId = rolledNameLocaId;
				int rolledPortraitNameLocaId = MissionMetaDataManager.Instance.MissionGoals.RolledPortraitNames[typeGuid];
				missionNpcSpawn.RolledPortraitNameLocaId = rolledPortraitNameLocaId;
			}
		}
		return val;
	}
}
