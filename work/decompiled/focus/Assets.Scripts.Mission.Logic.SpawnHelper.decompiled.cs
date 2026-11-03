using AcierProtocol;
using Assets.Scripts.Audio;
using Assets.Scripts.Database;
using Assets.Scripts.Engine;
using Assets.Scripts.Mission.Definitions;
using Assets.Scripts.Mission.Logic.CharacterPooling;
using Assets.Scripts.Mission.Logic.Components;
using Assets.Scripts.Mission.Logic.Entities;
using Assets.Scripts.Mission.Logic.RuleSystem;
using Assets.Scripts.Mission.Logic.StateMachines;
using Assets.Scripts.Mission.Logic.Stats;
using Assets.Scripts.Mission.Utilities;
using Database;
using Humanoids;
using SharedBaseLib.Core;
using SharedBaseLib.System;
using UbiCore.Debug;
using UbiCore.Utils;
using UbiCore.Utils.Extensions;
using UnityEngine;

namespace Assets.Scripts.Mission.Logic;

public static class SpawnHelper
{
	public static GameObject SpawnPlayer(AvatarData.IAvatarDefinition _definition, Transform _transform)
	{
		Profiling.Start("SpawnHelper::SpawnPlayer", float.MaxValue);
		PlayerCharacter playerCharacter = Helpers.CreateEntity<PlayerCharacter>(_definition);
		LogicMain.EntityManager.RegisterEntity(playerCharacter);
		AssassinRendering.ResetStaticPrivates();
		SetupEffects(_definition);
		HumanoidDef item = Main.Instance.GameDB.GetItem<HumanoidDef>(_definition.HumanoidGameID);
		GameObject gameObject = Singleton<HumanoidFactory>.Instance.CreateHumanoidFromDefinition(item, _receiveShadows: false, playerCharacter.CharacterAppearance, "Player");
		ItemDef item2 = Main.Instance.GameDB.GetItem<ItemDef>(_definition.WeaponGameID);
		WeaponHandler weaponHandler = gameObject.AddComponentIfNotExist<WeaponHandler>();
		weaponHandler.Initialize(item2);
		EntityWrapperComponent entityWrapperComponent = gameObject.AddComponent<EntityWrapperComponent>();
		entityWrapperComponent.Entity = playerCharacter;
		LogicMain.InitPlayer(gameObject);
		NavMeshAgent component = gameObject.GetComponent<NavMeshAgent>();
		component.avoidancePriority = 25;
		GameObject gameObject2 = GameObject.Find("MapScenario");
		RenderingCharacterLightingSettings component2 = gameObject2.GetComponent<RenderingCharacterLightingSettings>();
		if (component2 != null)
		{
			Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].receiveShadows = component2.IsAnimus;
			}
		}
		gameObject.transform.parent = _transform;
		Assets.Scripts.Mission.Helpers.SetTransformAndActivate(gameObject, _transform.position, _transform.rotation);
		gameObject.AddComponentIfNotExist<AnimatorIK>();
		gameObject.AddComponentIfNotExist<AudioFootstepPlayer>();
		AnimatorSoundStart animatorSoundStart = gameObject.AddComponentIfNotExist<AnimatorSoundStart>();
		animatorSoundStart.Attach();
		gameObject.AddComponentIfNotExist<AlternativeCharacterMesh>();
		PlayerCharacter entity = entityWrapperComponent.GetEntity<PlayerCharacter>();
		entity.HeadIcon.IconAttachment = new EntityIcon();
		gameObject.AddComponent<SlowMotionEventManager>();
		GameObject gameObject3 = GameObject.FindGameObjectWithTag("Main");
		gameObject3.BroadcastMessage("OnPlayerSpawned", gameObject, SendMessageOptions.DontRequireReceiver);
		GameObject gameObject4 = UnityEngine.Object.Instantiate(UnityEngine.Resources.Load("Effects/Prefabs/CriticalHealthEffect") as GameObject);
		gameObject4.transform.parent = gameObject.transform;
		Profiling.Stop();
		return gameObject;
	}

	public static GameObject CreateHireling(AvatarData.IAvatarDefinition _definition)
	{
		Profiling.Start("SpawnHelper::CreateHireling", float.MaxValue);
		SetupEffects(_definition);
		Assets.Scripts.Database.CharacterAppearance appearance = new Assets.Scripts.Database.CharacterAppearance(_definition);
		GameObject gameObject = Singleton<HumanoidFactory>.Instance.CreateHumanoidFromDB(_definition.HumanoidGameID, _receiveShadows: false, appearance, "Player");
		gameObject.transform.parent = LogicMain.CharacterManager.CharacterPoolNode.transform;
		gameObject.transform.position = Vector3.up * 10000f;
		ItemDef item = Main.Instance.GameDB.GetItem<ItemDef>(_definition.WeaponGameID);
		if (item != null)
		{
			WeaponHandler weaponHandler = gameObject.AddComponentIfNotExist<WeaponHandler>();
			weaponHandler.Initialize(item);
			weaponHandler.ShowWeapon(_show: false);
		}
		gameObject.AddComponent<EntityWrapperComponent>();
		NavMeshAgent component = gameObject.GetComponent<NavMeshAgent>();
		component.avoidancePriority = 100;
		component.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
		Transform transform = gameObject.transform.FindChild("Body_LOD0");
		if (transform != null)
		{
			transform.gameObject.GetComponent<Renderer>().enabled = false;
			transform.gameObject.GetComponent<Renderer>().receiveShadows = false;
		}
		AnimatorIK animatorIK = gameObject.AddComponentIfNotExist<AnimatorIK>();
		animatorIK.enabled = false;
		gameObject.GetComponent<Collider>().enabled = false;
		Profiling.Stop();
		return gameObject;
	}

	public static HirelingCharacter SpawnHireling(AvatarData.IAvatarDefinition _definition, GameObject _go, EntityId _targetEntityId)
	{
		GameEntity gameEntity = _targetEntityId.GameEntity;
		Vector3 vector = ComputeSpawnPos(gameEntity.Position, gameEntity.Rotation * Quaternion.Euler(0f, 180f, 0f), 10f, 5f, LogicConstants.AutoPassableNavMeshLayerMask, _centerIsTarget: true, out var _path);
		Quaternion rotation;
		if (_path == null)
		{
			vector = gameEntity.Position;
			rotation = gameEntity.Rotation;
		}
		else
		{
			rotation = Quaternion.LookRotation(gameEntity.Position - vector);
		}
		return SpawnHireling(_definition, _go, _targetEntityId, vector, rotation);
	}

	public static HirelingCharacter SpawnHireling(AvatarData.IAvatarDefinition _definition, GameObject _go, EntityId _targetEntityId, Vector3 _position, Quaternion _rotation)
	{
		Profiling.Start("SpawnHelper::SpawnHireling", float.MaxValue);
		Transform transform = _go.transform.FindChild("Body_LOD0");
		if (transform != null)
		{
			transform.gameObject.GetComponent<Renderer>().enabled = true;
		}
		_go.GetComponent<WeaponHandler>().ShowWeapon(_show: true);
		Assets.Scripts.Mission.Helpers.SetTransformAndActivate(_go, _position, _rotation);
		HirelingCharacter hirelingCharacter = Helpers.CreateEntity<HirelingCharacter>(_definition);
		hirelingCharacter.HirelingCharacterStateMachines.HirelingMain.TargetEntityId = _targetEntityId;
		LogicMain.EntityManager.RegisterEntity(hirelingCharacter);
		hirelingCharacter.Position = _position;
		hirelingCharacter.Rotation = _rotation;
		NavMeshAgent component = _go.GetComponent<NavMeshAgent>();
		component.avoidancePriority = 25;
		component.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
		_go.GetComponent<EntityWrapperComponent>().Entity = hirelingCharacter;
		hirelingCharacter.OnActivate();
		_go.GetComponent<Collider>().enabled = true;
		_go.GetComponent<AnimatorIK>().enabled = true;
		Renderer[] componentsInChildren = _go.GetComponentsInChildren<Renderer>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].receiveShadows = false;
		}
		ObjectFade objectFade = _go.AddComponentIfNotExist<ObjectFade>();
		objectFade.FadeMode = EFadeMode.Fancy_EFadeMode;
		objectFade.FadeDuration = 1.5f;
		objectFade.OnFadeDoneCallback = ObjectFadeCallbacks.OnFadeFinishedDestroySelf;
		objectFade.FadeIn();
		Profiling.Stop();
		return hirelingCharacter;
	}

	public static void DespawnHireling(HirelingCharacter _entity)
	{
		GameObject gameObject = _entity.GameObject;
		gameObject.GetComponent<WeaponHandler>().ShowWeapon(_show: false);
		_entity.Despawn();
	}

	public static GameObject CreateNpc(LogicAdapter.EntityData _ed)
	{
		Profiling.Start("SpawnHelper::CreateNpc", float.MaxValue);
		NpcData.INpcDefinition definition = UbiCore.Utils.Singleton<NpcData>.Instance.GetDefinition(_ed.NpcType);
		EntityTypes.EType entityType = definition.EntityType;
		bool flag = entityType == EntityTypes.EType.EnemyCharacter;
		string nameAndTag = ((!flag) ? null : "Enemy");
		HumanoidDef item = Main.Instance.GameDB.GetItem<HumanoidDef>(definition.HumanoidGameID);
		ICharacterAppearance characterAppearance = null;
		characterAppearance = _ed.Entity.FindComponent<Assets.Scripts.Mission.Logic.Components.CharacterAppearance>();
		GameObject gameObject = Singleton<HumanoidFactory>.Instance.CreateHumanoidFromDefinition(item, _receiveShadows: true, characterAppearance, nameAndTag, GameID.IsValid(definition.HumanoidPrefabOverride) ? definition.HumanoidPrefabOverride : null);
		gameObject.AddComponent<EntityWrapperComponent>();
		gameObject.AddComponentIfNotExist<AnimatorSoundStart>();
		GameObject gameObject2 = null;
		if (GameID.IsValid(definition.WeaponGameID))
		{
			ItemDef item2 = Main.Instance.GameDB.GetItem<ItemDef>(definition.WeaponGameID);
			if (Assets.Scripts.Engine.Helpers.Verify((item.WeaponFlags & (uint)item2.WeaponType) == (uint)item2.WeaponType, "{0} is not able to hold weapon type {1}", item.Name, item2.WeaponType))
			{
				WeaponHandler weaponHandler = gameObject.AddComponentIfNotExist<WeaponHandler>();
				weaponHandler.Initialize(item2);
			}
		}
		if (GameID.IsValid(definition.RangedWeaponGameID))
		{
			ItemDef item3 = Main.Instance.GameDB.GetItem<ItemDef>(definition.RangedWeaponGameID);
			if (Assets.Scripts.Engine.Helpers.Verify((item.WeaponFlags & (uint)item3.WeaponType) == (uint)item3.WeaponType, "{0} is not able to hold ranged weapon type {1}", item.Name, item3.WeaponType))
			{
				WeaponHandler weaponHandler2 = gameObject.AddComponent<WeaponHandler>();
				weaponHandler2.Initialize(item3);
			}
		}
		if (flag)
		{
			gameObject2 = gameObject.FindChildByName("SelectionCollider");
		}
		else if (entityType == EntityTypes.EType.InteractionCharacter || entityType == EntityTypes.EType.AwareInteractionCharacter)
		{
			gameObject2 = gameObject.FindChildByName("SelectionCollider");
		}
		gameObject.AddComponentIfNotExist<AnimatorIK>();
		gameObject.transform.parent = LogicMain.CharacterManager.CharacterPoolNode.transform;
		if ((bool)gameObject2)
		{
			gameObject2.tag = "Interactable";
			gameObject2.layer = Layers.InteractorsLayer;
		}
		Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].receiveShadows = false;
		}
		GameObject gameObject3 = GameObject.FindGameObjectWithTag("Main");
		gameObject3.BroadcastMessage("OnNPCSpawned", gameObject, SendMessageOptions.DontRequireReceiver);
		Profiling.Stop();
		FixupLightProbeAnchor(gameObject, _ed, EStateType.CharacterCondition_OnBench, "Head");
		return gameObject;
	}

	private static void FixupLightProbeAnchor(GameObject _rootGo, LogicAdapter.EntityData _ed, EStateType _state, string _nodeName)
	{
		if (!(_ed.Entity is NpcCharacter { CharacterStateMachines: ICharacterConditionCarrier characterStateMachines }) || !(_rootGo != null) || characterStateMachines.CharacterCondition.CurrentStateType != _state)
		{
			return;
		}
		Renderer[] componentsInChildren = _rootGo.GetComponentsInChildren<Renderer>();
		int num = componentsInChildren.Length;
		if (num > 0)
		{
			GameObject gameObject = _rootGo.FindChildByName(_nodeName);
			Transform probeAnchor = ((!(gameObject != null)) ? null : gameObject.transform);
			for (int i = 0; num > i; i++)
			{
				componentsInChildren[i].probeAnchor = probeAnchor;
			}
		}
	}

	public static void PostSetupNpc(NpcCharacter _entity, bool _isRelevant, EMapIconType _mapIconType, int _rolledNameLocaId, int _rolledPortraitNameLocaId)
	{
		Profiling.Start("SpawnHelper::PostSetupNpc", float.MaxValue);
		if (_entity is InteractionCharacter interactionCharacter)
		{
			ApproachEntityIcon approachEntityIcon = new ApproachEntityIcon();
			interactionCharacter.HeadIcon.IconAttachment = approachEntityIcon;
			approachEntityIcon.Visible = true;
			approachEntityIcon.SetIcon(interactionCharacter.NpcDefinition.Definition.InteractionIcon, 5);
			interactionCharacter.InteractionCharacterStats.Set_bool(StatTypes.EStatType.Interaction_IsRelevant, _isRelevant);
		}
		_entity.HeadIcon.SetInteractable((HeadIcon _headIcon) => PlayerHelper.AssassinInteraction.TapEntity(_headIcon.Entity.Id));
		if (_mapIconType != EMapIconType.Invalid_EMapIconType)
		{
			_entity.EntityStats.Set_int(StatTypes.EStatType.Entity_MapIconType, (int)_mapIconType);
			_entity.EntityStats.Set_int(StatTypes.EStatType.Entity_DisplayNameLocaKey, _rolledNameLocaId);
			_entity.EntityStats.Set_int(StatTypes.EStatType.Character_PortraitDisplayNameLocaKey, _rolledPortraitNameLocaId);
		}
		Profiling.Stop();
	}

	public static Vector3 ComputeSpawnPos(Vector3 _centerPosition, Quaternion _centerRotation, float _maxDistance, float _minDistance, int _layerMask, bool _centerIsTarget, out NavMeshPath _path)
	{
		Vector3 vector = default;
		NavMeshPath navMeshPath = new NavMeshPath();
		float num = _maxDistance;
		while (num >= _minDistance && navMeshPath.status != NavMeshPathStatus.PathComplete)
		{
			for (float num2 = 0f; num2 <= 180f; num2 += 10f)
			{
				if (navMeshPath.status == NavMeshPathStatus.PathComplete)
				{
					break;
				}
				int num3 = 0;
				float num4 = 1f;
				while (num4 >= -1f && num3 < 2 && navMeshPath.status != NavMeshPathStatus.PathComplete)
				{
					Vector3 vector2 = Quaternion.AngleAxis(num2 * num4, Vector3.up) * _centerRotation * Vector3.forward * num;
					vector = _centerPosition + vector2;
					if (NavMesh.SamplePosition(vector, out var hit, 50f, NavMeshLayers.WalkableLayerMaskPreset))
					{
						vector = hit.position;
						if (_centerIsTarget)
						{
							NavMesh.CalculatePath(vector, _centerPosition, _layerMask, navMeshPath);
						}
						else
						{
							NavMesh.CalculatePath(_centerPosition, vector, _layerMask, navMeshPath);
						}
					}
					num4 *= 2f;
					num3++;
				}
			}
			num -= 0.5f;
		}
		_path = ((navMeshPath.status != NavMeshPathStatus.PathComplete) ? null : navMeshPath);
		return vector;
	}

	private static void SetupEffects(AvatarData.IAvatarDefinition _definition)
	{
		BBList<GameID> bBList = new BBList<GameID>();
		foreach (RulesCollection.RuleParamPair complementedRuleDefinition in _definition.ComplementedRuleDefinitions)
		{
			GameID effectPackageGameID = complementedRuleDefinition.RuleDefinition.EffectPackageGameID;
			if (!bBList.Contains(effectPackageGameID))
			{
				bBList.Add(effectPackageGameID);
			}
		}
		Singleton<EffectManager>.Instance.PrepareEffects(Main.Instance.GameDB, bBList.ToArray());
		Singleton<EffectManager>.Instance.setupEffectPackages();
	}
}
