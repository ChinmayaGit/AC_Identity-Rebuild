namespace Assets.Scripts.Mission.Logic.StateMachines;

public static class PlayerMainCarrierEntityExtensions
{
	public static bool GetIsWaitingForAnim(this IPlayerMainCarrierEntity _entity)
	{
		return _entity.PlayerMainCarrier.PlayerMain.CurrentStateType == EStateType.Generic_WaitingForAnim || _entity.PlayerMainCarrier.PlayerMain.CurrentStateType == EStateType.Generic_WaitingForAnimCancelable;
	}
}
