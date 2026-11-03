using System;
using AcierProtocol;
using Assets.Scripts.Mission.UI;
using Assets.Scripts.Network.Missions.Mission.Data;
using Assets.Scripts.UI.Localization;
using Mission;

public class ObjectiveTimer : ObjectiveBase<ObjectiveTimerData>, IObjectiveTimer
{
	private float m_currentTime;

	public bool HasTimer => true;

	protected override Mission.EObjectiveResult InitialResult => ToResult(Data.InitialResultWrapper.Result);

	protected override bool InitialVisualSummaryState => ToSummaryState(Data.InitialResultWrapper.Tracker);

	protected override EProgressState InitialVisualTrackerState => ToTrackerState(Data.InitialResultWrapper.Tracker);

	private float CurrentTime
	{
		get
		{
			return m_currentTime;
		}
		set
		{
			if (!(Math.Abs(m_currentTime - value) < float.Epsilon))
			{
				m_currentTime = value;
				UpdateDescription();
			}
		}
	}

	private ObjectiveTimer()
	{
	}

	protected override void ShowFailMessage()
	{
		base.ShowFailMessage();
		LogicMain.LogicInterface.OnSendInfo(EInfoEventType.TextAnnotation, 7742, InfoEvent.ETextAnnotationMode.MessageOpen, -1, null, _immediate: true);
	}

	public override void OnPreActivate()
	{
		base.OnPreActivate();
		CurrentTime = 0f;
	}

	public override void FrameUpdateImpl()
	{
		float currentTime = CurrentTime + LogicMain.FrameTime;
		CurrentTime = currentTime;
		if (!(CurrentTime < (float)Data.TimeInSeconds))
		{
			ObjectiveResultWrapper completionResultWrapper = Data.CompletionResultWrapper;
			VisualTrackerState = ToTrackerState(completionResultWrapper.Tracker);
			VisualSummaryState = ToSummaryState(completionResultWrapper.Tracker);
			Result = ToResult(completionResultWrapper.Result);
		}
	}

	protected override void Complete()
	{
		if (CurrentTime < (float)Data.TimeInSeconds)
		{
			ObjectiveResultWrapper terminationResultWrapper = Data.TerminationResultWrapper;
			VisualTrackerState = ToTrackerState(terminationResultWrapper.Tracker);
			VisualSummaryState = ToSummaryState(terminationResultWrapper.Tracker);
			Result = ToResult(terminationResultWrapper.Result);
		}
		base.Complete();
	}

	public override void OnActivate()
	{
		base.OnActivate();
		m_currentTime = 0f;
	}

	public int GetTime()
	{
		return Data.TimeInSeconds - (int)CurrentTime;
	}

	public override string ToString()
	{
		return $"Timer: {Data.TimeInSeconds}s";
	}

	public override StringMapper GetStringMapper()
	{
		StringMapper stringMapper = base.GetStringMapper();
		TimeSpan timeSpan = TimeSpan.FromSeconds(GetTime());
		stringMapper.Add("minutes", timeSpan.Minutes);
		stringMapper.Add("seconds", timeSpan.Seconds);
		stringMapper.Add("total_seconds", timeSpan.TotalSeconds);
		stringMapper.Add("TotalSeconds", timeSpan.TotalSeconds);
		return stringMapper;
	}

	private static bool ToSummaryState(EObjectiveProgress _tracker)
	{
		switch (_tracker)
		{
		case EObjectiveProgress.Succeeded_EObjectiveProgress:
		case EObjectiveProgress.InRange_EObjectiveProgress:
		case EObjectiveProgress.InProgress_EObjectiveProgress:
			return true;
		default:
			return false;
		}
	}

	private static EProgressState ToTrackerState(EObjectiveProgress _tracker)
	{
		return _tracker switch
		{
			EObjectiveProgress.NotStarted_EObjectiveProgress => EProgressState.NotStarted, 
			EObjectiveProgress.Succeeded_EObjectiveProgress => EProgressState.Fulfilled, 
			EObjectiveProgress.Failed_EObjectiveProgress => EProgressState.Failed, 
			EObjectiveProgress.InRange_EObjectiveProgress => EProgressState.InRange, 
			EObjectiveProgress.InProgress_EObjectiveProgress => EProgressState.InProgress, 
			_ => throw new ArgumentOutOfRangeException("_tracker"), 
		};
	}

	private Mission.EObjectiveResult ToResult(AcierProtocol.EObjectiveResult _initialResult)
	{
		return _initialResult switch
		{
			AcierProtocol.EObjectiveResult.Undefined_EObjectiveResult => Mission.EObjectiveResult.Undefined, 
			AcierProtocol.EObjectiveResult.Succeeded_EObjectiveResult => Mission.EObjectiveResult.Fulfilled, 
			AcierProtocol.EObjectiveResult.Failed_EObjectiveResult => Mission.EObjectiveResult.Failed, 
			_ => throw new ArgumentOutOfRangeException("_initialResult"), 
		};
	}
}
