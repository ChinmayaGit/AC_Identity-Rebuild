using Assets.Scripts.Network.Missions.Mission.Data;
using Mission;

public class ObjectiveVoid : ObjectiveBase<ObjectiveVoidData>
{
	protected override EObjectiveResult InitialResult => EObjectiveResult.Undefined;

	protected override bool InitialVisualSummaryState => true;

	protected override EProgressState InitialVisualTrackerState => EProgressState.Fulfilled;

	private ObjectiveVoid()
	{
	}

	public override void FrameUpdateImpl()
	{
		base.FrameUpdateImpl();
		Result = EObjectiveResult.Fulfilled;
	}

	public override string ToString()
	{
		return "Empty objective";
	}
}
