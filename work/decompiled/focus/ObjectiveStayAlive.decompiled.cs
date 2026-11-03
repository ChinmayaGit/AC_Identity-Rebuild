using AcierProtocol;
using Assets.Scripts.Mission.Logic;
using Assets.Scripts.Mission.UI;
using Assets.Scripts.Network.Missions.Mission.Data;
using Mission;

public class ObjectiveStayAlive : ObjectiveBase<ObjectiveStayAliveData>
{
	protected override bool InitialVisualSummaryState => true;

	protected override Mission.EObjectiveResult InitialResult => Mission.EObjectiveResult.Fulfilled;

	protected override EProgressState InitialVisualTrackerState => EProgressState.InProgress;

	public ObjectiveStayAlive()
	{
		Data.HideDescription = true;
	}

	protected override void ShowFailMessage()
	{
		base.ShowFailMessage();
		LogicMain.LogicInterface.OnSendInfo(EInfoEventType.TextAnnotation, 7747, InfoEvent.ETextAnnotationMode.MessageOpen, -1, null, _immediate: true);
	}

	public override void PrepareRevive()
	{
		IsActive = Result == Mission.EObjectiveResult.Failed;
		base.PrepareRevive();
	}

	public override void TurnUpdateImpl()
	{
		if (LogicMain.PlayerEntity.GetIsDead())
		{
			VisualTrackerState = EProgressState.Failed;
			Result = Mission.EObjectiveResult.Failed;
			VisualSummaryState = false;
			Complete();
		}
	}

	public override string ToString()
	{
		return "Stay alive.";
	}
}
