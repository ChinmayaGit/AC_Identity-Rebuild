using System;
using AcierProtocol;
using ProtoBuf;

namespace Assets.Scripts.Network.Missions.Mission.Data;

[Serializable]
public sealed class ObjectiveTimerData : ObjectiveBaseData
{
	public int TimeInSeconds = 300;

	public ObjectiveResultWrapper CompletionResultWrapper = new ObjectiveResultWrapper(EObjectiveResult.Succeeded_EObjectiveResult, EObjectiveProgress.Succeeded_EObjectiveProgress);

	public ObjectiveResultWrapper InitialResultWrapper = new ObjectiveResultWrapper(EObjectiveResult.Undefined_EObjectiveResult, EObjectiveProgress.InProgress_EObjectiveProgress);

	public ObjectiveResultWrapper TerminationResultWrapper = new ObjectiveResultWrapper(EObjectiveResult.Failed_EObjectiveResult, EObjectiveProgress.Failed_EObjectiveProgress);

	public ObjectiveTimerData()
	{
	}

	public ObjectiveTimerData(ObjectiveTimerContent _objectiveTimerContent)
		: base(_objectiveTimerContent.ObjectiveBaseContent)
	{
		TimeInSeconds = _objectiveTimerContent.TimeInSeconds;
		CompletionResultWrapper = new ObjectiveResultWrapper(_objectiveTimerContent.CompletionResult);
		InitialResultWrapper = new ObjectiveResultWrapper(_objectiveTimerContent.InitialResult);
		TerminationResultWrapper = new ObjectiveResultWrapper(_objectiveTimerContent.TerminationResult);
	}

	public override IExtensible SerializeProto()
	{
		ObjectiveResult completionResult = CompletionResultWrapper.ToProtobuf();
		ObjectiveResult initialResult = InitialResultWrapper.ToProtobuf();
		ObjectiveResult terminationResult = TerminationResultWrapper.ToProtobuf();
		ObjectiveTimerContent objectiveTimerContent = new ObjectiveTimerContent();
		objectiveTimerContent.ObjectiveBaseContent = (ObjectiveBaseContent)base.SerializeProto();
		objectiveTimerContent.TimeInSeconds = TimeInSeconds;
		objectiveTimerContent.CompletionResult = completionResult;
		objectiveTimerContent.InitialResult = initialResult;
		objectiveTimerContent.TerminationResult = terminationResult;
		return objectiveTimerContent;
	}
}
