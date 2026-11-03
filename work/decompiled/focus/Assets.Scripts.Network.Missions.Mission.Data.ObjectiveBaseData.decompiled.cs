using AcierProtocol;
using ProtoBuf;

namespace Assets.Scripts.Network.Missions.Mission.Data;

public abstract class ObjectiveBaseData : MissionBaseData
{
	public int PhaseCompletedMessage = -1;

	public bool IsPrimaryObjective;

	public bool IsOptionalObjective;

	public bool HideDescription;

	public bool ShowFailMessageEvenIfHidden;

	public string Description = string.Empty;

	public string ShortDescription = string.Empty;

	public string SecondaryDescription = string.Empty;

	public long OnSuccessId = -2L;

	public long OnFailId = -2L;

	public bool IsVisibleInEagleVision = true;

	public EObjectiveType TrackerIcon = EObjectiveType.Kill_EObjectiveType;

	public EObjectiveResult TerminationResult;

	public bool IsEliteObjective;

	protected ObjectiveBaseData()
	{
	}

	protected ObjectiveBaseData(ObjectiveBaseContent _data)
		: base(_data.MissionBaseContent)
	{
		IsPrimaryObjective = _data.IsPrimaryObjective;
		IsOptionalObjective = _data.IsOptionalObjective;
		HideDescription = _data.HideDescription;
		ShowFailMessageEvenIfHidden = _data.ShowFailMessageEvenIfHidden;
		Description = _data.Description;
		ShortDescription = _data.ShortDescription;
		SecondaryDescription = _data.SecondaryDescription;
		PhaseCompletedMessage = _data.PhaseCompletedMessage;
		OnSuccessId = _data.OnSuccessId;
		OnFailId = _data.OnFailId;
		IsVisibleInEagleVision = _data.IsVisibleInEagleVision;
		TrackerIcon = _data.TrackerIcon;
		TerminationResult = _data.TerminationResult;
		IsEliteObjective = _data.IsEliteObjective;
	}

	public override IExtensible SerializeProto()
	{
		ObjectiveBaseContent objectiveBaseContent = new ObjectiveBaseContent();
		objectiveBaseContent.MissionBaseContent = (MissionBaseContent)base.SerializeProto();
		objectiveBaseContent.Description = Description;
		objectiveBaseContent.HideDescription = HideDescription;
		objectiveBaseContent.ShowFailMessageEvenIfHidden = ShowFailMessageEvenIfHidden;
		objectiveBaseContent.IsOptionalObjective = IsOptionalObjective;
		objectiveBaseContent.IsPrimaryObjective = IsPrimaryObjective;
		objectiveBaseContent.OnFailId = OnFailId;
		objectiveBaseContent.OnSuccessId = OnSuccessId;
		objectiveBaseContent.PhaseCompletedMessage = PhaseCompletedMessage;
		objectiveBaseContent.ShortDescription = ShortDescription;
		objectiveBaseContent.SecondaryDescription = SecondaryDescription;
		objectiveBaseContent.IsVisibleInEagleVision = IsVisibleInEagleVision;
		objectiveBaseContent.TrackerIcon = TrackerIcon;
		objectiveBaseContent.TerminationResult = TerminationResult;
		objectiveBaseContent.IsEliteObjective = IsEliteObjective;
		return objectiveBaseContent;
	}
}
