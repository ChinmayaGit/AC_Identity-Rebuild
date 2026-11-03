using Assets.Scripts.Mission.Logic.Combat;
using Assets.Scripts.Mission.Logic.Components;
using Assets.Scripts.Mission.Logic.StateMachines;
using Assets.Scripts.Mission.Selection;
using UbiCore.Utils;

namespace Assets.Scripts.Mission.Logic.Entities;

public class PlayerCharacter : AssassinCharacter, IParticipantCarrier, IDefinitionCarrier, ICharacterStateMachinesCarrier, IAssassinCharacterStateMachinesCarrier, IPlayerCharacterStateMachinesCarrier, IEntityStatsCarrier, IGameObjectProxyCarrier, IHighlightCarrier, IPlayerEventHandlerCarrier, ISpacialEntityCarrier, IStateMachinesCarrier, IUnityComponentCacheCarrier, IVisionCarrier, IEntity, IUpdateableEntity, IPositionProvider, IPositionEntity, IGameObjectEntity, IGameEntity, IPlayerMainCarrierEntity, IPlayerProfileCarrierEntity
{
	public override EntityTypes.EType Type => EntityTypes.EType.PlayerCharacter;

	public PlayerCharacterStateMachines PlayerCharacterStateMachines { get; private set; }

	public PlayerEventHandler PlayerEventHandler { get; private set; }

	public override AssassinInteraction AssassinInteraction { get; protected set; }

	public CombatParticipantProxy CombatParticipantProxy { get; private set; }

	public Vision Vision { get; private set; }

	public PlayerCharacter()
		: base((IEntity entity) => new PlayerCharacterStateMachines(entity))
	{
		PlayerCharacterStateMachines = (PlayerCharacterStateMachines)CharacterStateMachines;
		PlayerEventHandler = AddComponent<PlayerEventHandler>();
		Vision = AddComponent<Vision>();
		CombatParticipantProxy = AddComponent<CombatParticipantProxy>();
		AssassinInteraction = AddComponent(new AssassinInteraction(this, UbiCore.Utils.Singleton<SelectionManager>.Instance));
	}
}
