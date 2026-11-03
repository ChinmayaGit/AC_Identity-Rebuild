using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AcierProtocol;
using Assets.Scripts.Achievements;
using Assets.Scripts.Engine;
using Assets.Scripts.Mission.Definitions;
using Assets.Scripts.Mission.Logic.Entities;
using Assets.Scripts.Mission.Logic.RuleSystem;
using Assets.Scripts.Mission.Logic.StateMachines;
using Assets.Scripts.Mission.Logic.Stats;
using Assets.Scripts.Mission.Mission.Interfaces;
using Assets.Scripts.Mission.UI.LogicInterface;
using Assets.Scripts.Mission.Utilities;
using Assets.Scripts.Tracking;
using Assets.Scripts.UI.State;
using Database;
using SharedBaseLib.Core;
using SharedBaseLib.System;
using SharedBaseLib.System.Extensions;
using UbiCore.Messenger;
using UbiCore.Utils;
using UnityEngine;

namespace Assets.Scripts.Mission.Logic.Components;

public class MissionResult : TurnUpdateableComponent, IReadOnlyMissionResult
{
	private class SkillUsageStats
	{
		public readonly string SkillName;

		public readonly int SkillGuid;

		public readonly int RuleGuid;

		public int UsageCounter;

		public int UsageTurnDuration;

		public SkillUsageStats(SkillData.ISkillDefinition _definition)
		{
			SkillName = _definition.Type.Name;
			SkillGuid = _definition.GameID.Guid;
			RuleGuid = _definition.RuleDefinition.GameID.Guid;
		}
	}

	private class NpcStatistics
	{
		public readonly BBDictionary<string, SkillUsageStats> SkillUsage = new BBDictionary<string, SkillUsageStats>();

		public int KillCount { get; set; }

		public GameID NpcTypeId { get; private set; }

		public NpcStatistics(GameID _gameId)
		{
			NpcTypeId = _gameId;
		}
	}

	private readonly BBDictionary<int, int> m_hotspotCounters = new BBDictionary<int, int>
	{
		{ 0, 0 },
		{ 1, 0 },
		{ 2, 0 },
		{ 3, 0 },
		{ 4, 0 },
		{ 5, 0 },
		{ 8, 0 },
		{ 9, 0 },
		{ 6, 0 },
		{ 7, 0 },
		{ 10, 0 }
	};

	private readonly BBDictionary<int, int> m_hotspotTurnDuration = new BBDictionary<int, int>
	{
		{ 0, 0 },
		{ 1, 0 },
		{ 2, 0 },
		{ 3, 0 },
		{ 8, 0 },
		{ 10, 0 }
	};

	private readonly BBDictionary<int, int> m_killedEnemyCountByCondition = new BBDictionary<int, int>
	{
		{ 28, 0 },
		{ 23, 0 },
		{ 24, 0 },
		{ 26, 0 },
		{ 18, 0 },
		{ 29, 0 },
		{ 15, 0 },
		{ 25, 0 },
		{ 20, 0 },
		{ 22, 0 },
		{ 17, 0 },
		{ 16, 0 }
	};

	private readonly BBDictionary<int, int> m_killedEnemyCountByKillType = new BBDictionary<int, int>();

	private readonly BBDictionary<int, int> m_notorietyCounters = new BBDictionary<int, int>
	{
		{ 0, 0 },
		{ 1, 0 },
		{ 2, 0 }
	};

	private readonly BBDictionary<string, SkillUsageStats> m_skillUsage = new BBDictionary<string, SkillUsageStats>();

	private readonly BBDictionary<int, NpcStatistics> m_NpcStatistics = new BBDictionary<int, NpcStatistics>();

	private readonly BBList<ulong> m_AchievementCounters = new BBList<ulong>(AchievementsManager.AchievementCounterCount);

	private readonly BBList<ulong> m_AchievementCountersPrevious = new BBList<ulong>(AchievementsManager.AchievementCounterCount);

	private readonly BBList<int> m_SucceededChallenges = new BBList<int>();

	private readonly ReadOnlyCollection<int> m_SucceededChallengesAccess;

	private readonly BBSimpleList<int> m_ExternallyCountedNpcStatSkillIndices = new BBSimpleList<int>();

	private readonly BBSimpleList<ObjectiveKill> m_KillObjectives = new BBSimpleList<ObjectiveKill>();

	private readonly BBSimpleList<ObjectiveKillEnemyType> m_KillTypeObjectives = new BBSimpleList<ObjectiveKillEnemyType>();

	private readonly BBSimpleList<EntityId> m_AssassinatedEnemyIds = new BBSimpleList<EntityId>();

	private bool m_omitCounting;

	public int AssassinationCount { get; private set; }

	public int CombatCount { get; private set; }

	public int EnemiesKilledInCombatCount { get; private set; }

	public int GroundTurnCount { get; private set; }

	public int MaxGroundTurnCount { get; private set; }

	public int EnterGroundCount { get; private set; }

	public int EnterRoofCount { get; private set; }

	public int FledCount { get; private set; }

	public int EnemiesFledCount { get; private set; }

	public int EnemiesKilledCount { get; private set; }

	public int EnemiesKilledWithin10MinutesCount { get; private set; }

	public int EnemiesShreddedCount { get; private set; }

	public int FrightenedTurnCount { get; private set; }

	public int CausedSplinterBombBleedingTurnCount { get; private set; }

	public int TracedBySeekersTurnCount { get; private set; }

	public float LowestBaseHealth { get; private set; }

	public float LowestBaseHealthMax { get; private set; }

	public float RegularDamageDealtSum { get; private set; }

	public int RegularDamageDealtCount { get; private set; }

	public int UsedHidingSpotCount { get; private set; }

	public int SentCourtesanGroupsCount { get; private set; }

	public int UsedSkillsCount { get; private set; }

	public int KilledEnemiesBySkillCount { get; private set; }

	public int EnemiesOnRooftopsKilledCount { get; private set; }

	public int KilledEnemiesFromCivilianCoverCount { get; private set; }

	public int EnemiesKilledWithLowPlayerHealthCount { get; private set; }

	public int EnemiesKilledWithCombo { get; private set; }

	public int EnemiesKilledWithBomb { get; private set; }

	public int PlayerDeathsCount { get; private set; }

	public int PlayerDiedByOwnBombCount { get; private set; }

	public int LostCaptivesCount { get; private set; }

	public int SuccessfullyParried { get; private set; }

	public int SwitchedTarget { get; private set; }

	public int SuccessfullHirelingAttacks { get; private set; }

	public int CriticalHitCount { get; private set; }

	public int CrowsKilledCount { get; private set; }

	public int FlushedPigeonsCount { get; private set; }

	public float Playtime { get; private set; }

	public long FailMissionID { get; set; }

	public int FailMissionLocaID { get; set; }

	public int TreasureChestsOpenedInMission { get; private set; }

	public int MessagesDeliveredInMission { get; private set; }

	public int AssassinatedMissionTargets { get; private set; }

	public int ConsumedLockPicks { get; private set; }

	public int ReviveWithAdColonyCount { get; private set; }

	public ReadOnlyCollection<int> SucceededChallenges => m_SucceededChallengesAccess;

	public override int Priority => 28;

	public MissionResult(IEntity _entity)
		: base(_entity)
	{
		for (int i = 0; i < AchievementsManager.AchievementCounterCount; i++)
		{
			m_AchievementCounters.Add(0uL);
			m_AchievementCountersPrevious.Add(0uL);
		}
		m_SucceededChallengesAccess = new ReadOnlyCollection<int>(m_SucceededChallenges);
	}

	public void IncreaseLostCaptivesCount()
	{
		LostCaptivesCount += 1;
	}

	public void IncreaseSuccessfullyParried()
	{
		SuccessfullyParried += 1;
	}

	public void IncreaseSwitchedTarget()
	{
		SwitchedTarget += 1;
	}

	public void IncreaseSuccessfullHirelingAttack()
	{
		SuccessfullHirelingAttacks += 1;
	}

	public void IncreaseCriticalHitCount()
	{
		CriticalHitCount += 1;
		IncreaseAchievementCounter(ECounterType.CriticalHits, 1uL);
	}

	public void IncreaseGroundTurnCount()
	{
		GroundTurnCount += 1;
		MaxGroundTurnCount = Math.Max(MaxGroundTurnCount, GroundTurnCount);
	}

	public void ResetGroundTurnCount()
	{
		GroundTurnCount = 0;
	}

	public void IncreaseEnterGroundCount()
	{
		EnterGroundCount += 1;
		IncreaseAchievementCounter(ECounterType.EnteredGround, 1uL);
	}

	public void IncreaseEnterRooftopCount()
	{
		EnterRoofCount += 1;
		IncreaseAchievementCounter(ECounterType.EnteredRoofs, 1uL);
	}

	public void IncreaseFledCounter()
	{
		FledCount += 1;
		IncreaseAchievementCounter(ECounterType.FleesFromCombat, 1uL);
	}

	public void IncreaseFlushedPigeons()
	{
		FlushedPigeonsCount++;
		IncreaseAchievementCounter(ECounterType.FlushedPigeons, 1uL);
	}

	public void IncreaseTreasureChestsOpenedInMission()
	{
		TreasureChestsOpenedInMission += 1;
	}

	public void IncreaseConsumedLockPicks(int _amount)
	{
		ConsumedLockPicks += _amount;
		IncreaseAchievementCounter(ECounterType.LockPicksUsed, (ulong)_amount);
	}

	public void IncreaseReviveCount(bool _paidWithCurrency)
	{
		if (!_paidWithCurrency)
		{
			ReviveWithAdColonyCount += 1;
		}
		IncreaseAchievementCounter(ECounterType.ReviveCount, 1uL);
	}

	public void IncreaseDoctorHealings()
	{
		IncreaseUsedHotspotCount(EHotspot.EHotspot_Doctor);
	}

	public void IncreaseSkillUsageOnNpcTargetCounter(Enum<SkillTypes> _skillType, NpcData.NpcType _npcType)
	{
		string name = _skillType.Name;
		if (Assets.Scripts.Engine.Helpers.Verify(m_ExternallyCountedNpcStatSkillIndices.Contains(_skillType.GetDefinition().BaseSkillType.Index)))
		{
			int guid = _npcType.GameID.Guid;
			if (!m_NpcStatistics.TryGetValue(guid, out var value))
			{
				value = new NpcStatistics(_npcType.GameID);
				m_NpcStatistics[guid] = value;
			}
			if (!value.SkillUsage.TryGetValue(name, out var value2))
			{
				value2 = new SkillUsageStats(_skillType.GetDefinition());
				value.SkillUsage[name] = value2;
			}
			value2.UsageCounter++;
		}
	}

	public void IncreaseCausedSplinterBombBleedingTurnCount()
	{
		CausedSplinterBombBleedingTurnCount += 1;
		if (CausedSplinterBombBleedingTurnCount % 10 == 0)
		{
			IncreaseAchievementCounter(ECounterType.BleedingSplinterBombSeconds, 1uL);
		}
	}

	public void IncreaseTracedBySeekersTurnCount()
	{
		TracedBySeekersTurnCount += 1;
		if (TracedBySeekersTurnCount % 10 == 0)
		{
			IncreaseAchievementCounter(ECounterType.SecondsTracedBySeekers, 1uL);
		}
	}

	public void IncreaseUsedHotspotCount(EHotspot _hotspot)
	{
		BBDictionary<int, int> hotspotCounters;
		BBDictionary<int, int> bBDictionary = (hotspotCounters = m_hotspotCounters);
		int key2;
		int key = (key2 = (int)_hotspot);
		key2 = hotspotCounters[key2];
		bBDictionary[key] = key2 + 1;
		switch (_hotspot)
		{
		case EHotspot.EHotspot_Bench:
			IncreaseAchievementCounter(ECounterType.UsedHidingSpots, 1uL);
			break;
		case EHotspot.EHotspot_Haystack:
			IncreaseAchievementCounter(ECounterType.HidingsInHaystacks, 1uL);
			IncreaseAchievementCounter(ECounterType.UsedHidingSpots, 1uL);
			break;
		case EHotspot.EHotspot_RooftopGarden:
			IncreaseAchievementCounter(ECounterType.UsedHidingSpots, 1uL);
			break;
		case EHotspot.EHotspot_Chests:
			break;
		case EHotspot.EHotspot_Doctor:
			IncreaseAchievementCounter(ECounterType.UsedDoctorSpots, 1uL);
			break;
		case EHotspot.EHotspot_CivilianCover:
		case EHotspot.EHotspot_Captives:
			break;
		}
	}

	public void IncreaseEnemiesShreddedCount()
	{
		EnemiesShreddedCount += 1;
		IncreaseAchievementCounter(ECounterType.EnemiesShredded, 1uL);
	}

	public void IncreaseFrightenedTurnCount()
	{
		FrightenedTurnCount += 1;
		if (FrightenedTurnCount % 10 == 0)
		{
			IncreaseAchievementCounter(ECounterType.FrightenedTimeInSeconds, 1uL);
		}
	}

	public void OnMissionNodeActivated(MissionAchievementEntry _node)
	{
		ECounterType? eCounterType = AchievementsManager.ToCounterType(_node.Data.Achievement);
		if (eCounterType.HasValue)
		{
			IncreaseAchievementCounter(eCounterType.Value, 1uL);
		}
	}

	public void AddSucceededChallenge(int _id)
	{
		if (!SucceededChallenges.Contains(_id))
		{
			m_SucceededChallenges.Add(_id);
		}
	}

	public void RemoveSucceededChallenge(int _id)
	{
		m_SucceededChallenges.Remove(_id);
	}

	public void ClearSucceededChallenges()
	{
		m_SucceededChallenges.Clear();
	}

	public void RegisterObjective(IObjectiveBase _objective)
	{
		ObjectiveKill objectiveKill = _objective as ObjectiveKill;
		if (objectiveKill != null)
		{
			m_KillObjectives.Add(objectiveKill);
			for (int num = m_AssassinatedEnemyIds.Count - 1; num >= 0; num--)
			{
				int i1 = num;
				if (objectiveKill.Targets.Any((IMissionEntity x) => ((IMissionSpawnEntity)x).FormerEntityId == m_AssassinatedEnemyIds[i1]))
				{
					IncreaseAssassinatedMissionTargets();
					m_AssassinatedEnemyIds.RemoveAt(num);
				}
			}
		}
		else
		{
			ObjectiveKillEnemyType objectiveKillEnemyType = _objective as ObjectiveKillEnemyType;
			if (objectiveKillEnemyType != null && !objectiveKillEnemyType.Data.ForbidToKill)
			{
				m_KillTypeObjectives.Add(objectiveKillEnemyType);
			}
		}
	}

	public void UnregisterObjective(IObjectiveBase _objective)
	{
	}

	public void RegisterRegularDamageDealt(float _damage)
	{
		RegularDamageDealtSum += _damage;
		RegularDamageDealtCount += 1;
	}

	public AcierProtocol.MissionResult ToProtobuf()
	{
		AcierProtocol.MissionResult missionResult = new AcierProtocol.MissionResult();
		if (m_omitCounting)
		{
			return missionResult;
		}
		missionResult.assassinationCount = AssassinationCount;
		missionResult.combatCount = CombatCount;
		missionResult.enemiesKilledInCombatCount = EnemiesKilledInCombatCount;
		missionResult.enterGroundCount = EnterGroundCount;
		missionResult.enterRoofCount = EnterRoofCount;
		missionResult.fledCount = FledCount;
		missionResult.killedEnemyCount = EnemiesKilledCount;
		missionResult.lowestBaseHealth = LowestBaseHealth;
		missionResult.lowestBaseHealthMax = LowestBaseHealthMax;
		missionResult.usedHidingSpotCount = UsedHidingSpotCount;
		missionResult.usedSkillsCount = UsedSkillsCount;
		missionResult.enemiesFledCount = EnemiesFledCount;
		missionResult.airAssassinationCount = GetKilledEnemyCount(EKillType.AssassinationAir);
		missionResult.killsWhileGrabbingMoney = GetKilledEnemyCount(EStateType.CharacterCondition_Greedy);
		missionResult.killsWhileKnockedDown = GetKilledEnemyCount(EStateType.CharacterCondition_Knockdowned);
		missionResult.killsOutOfSmokeBombCover = GetKilledEnemyCount(EStateType.CharacterCondition_SmokeBombed);
		missionResult.killsBySkills = KilledEnemiesBySkillCount;
		missionResult.enemiesKilledOnRooftops = EnemiesOnRooftopsKilledCount;
		missionResult.enemiesKilledWith5PercentHealth = EnemiesKilledWithLowPlayerHealthCount;
		missionResult.enemiesKilledWithCombo = EnemiesKilledWithCombo;
		missionResult.enemiesKilledByBomb = EnemiesKilledWithBomb;
		missionResult.diesInMission = PlayerDeathsCount;
		missionResult.enemiesKilledWithin10Minutes = EnemiesKilledWithin10MinutesCount;
		missionResult.rooftopGardenAssassinationCount = GetKilledEnemyCount(EKillType.AssassinationRooftopGarden);
		missionResult.sentCourtesansCount = SentCourtesanGroupsCount;
		missionResult.succeededChallengeIds = missionResult.succeededChallengeIds.AddRange(SucceededChallenges);
		missionResult.dieByYourOwnBombCount = PlayerDiedByOwnBombCount;
		missionResult.duration = (int)(Playtime + 0.5f);
		missionResult.failReasonID = FailMissionID;
		missionResult.failReasonLocaID = FailMissionLocaID;
		missionResult.killsFromCivilianCover = KilledEnemiesFromCivilianCoverCount;
		missionResult.flushedPigeons = FlushedPigeonsCount;
		missionResult.bleedingSplinterBombSeconds = CausedSplinterBombBleedingTurnCount / 10;
		missionResult.successfullyParried = SuccessfullyParried;
		missionResult.switchedTarget = SwitchedTarget;
		missionResult.successfullHirelingAttacks = SuccessfullHirelingAttacks;
		missionResult.lockPickUsed = ConsumedLockPicks;
		missionResult.enemiesShredded = EnemiesShreddedCount;
		missionResult.frightenedTimeInSeconds = FrightenedTurnCount / 10;
		missionResult.enemiesKilledByBomb = EnemiesKilledWithBomb;
		missionResult.criticalHits = CriticalHitCount;
		missionResult.crowsKilled = CrowsKilledCount;
		missionResult.secondsTracedBySeekers = TracedBySeekersTurnCount / 10;
		missionResult.maxSecondsOnGround = GroundTurnCount / 10;
		for (int i = 0; i < LogicMain.EntityManager.EnemyCharacterCount; i++)
		{
			if (!LogicMain.EntityManager.GetEnemyCharacter(i).GetDeadOrAboutToDie())
			{
				missionResult.enemiesRemaining++;
			}
		}
		missionResult.reviveCountOnClient = ReviveWithAdColonyCount;
		missionResult.assassinatedMissionTargets = AssassinatedMissionTargets;
		missionResult.messagesDeliveredInMission = MessagesDeliveredInMission;
		missionResult.treasureChestsOpenedInMission = TreasureChestsOpenedInMission;
		missionResult.performanceTrack = new PerformanceTrack();
		missionResult.performanceTrack.deviceModelName = QualitySettingsHelper.GetDeviceIdentifier();
		missionResult.performanceTrack.deviceInfoTrack = DeviceManager.CreateTrackingDeviceInfo();
		missionResult.performanceTrack.appInfoTrack = DeviceManager.CreateTrackingAppInfo();
		string text = string.Empty;
		if (Main.Instance.Startup != null && Main.Instance.Startup.QualitySettings != null)
		{
			AcierProtocol.QualitySettings qualitySettings = Main.Instance.Startup.QualitySettings;
			text = "_" + qualitySettings.assetBundleQuality;
			if (qualitySettings.downScaling)
			{
				text += "_downscaled";
			}
		}
		missionResult.performanceTrack.environmentName = Application.loadedLevelName + text;
		missionResult.performanceTrack.maxFps = 1f / Mathf.Max(1f, Main.Instance.PerformanceTracker.MinSmoothedFrameTime) * 1000f;
		missionResult.performanceTrack.minFps = 1f / Mathf.Max(1f, Main.Instance.PerformanceTracker.MaxSmoothedFrameTime) * 1000f;
		missionResult.performanceTrack.avgFps = 1f / Mathf.Max(1f, Main.Instance.PerformanceTracker.AvgSmoothedFrameTime) * 1000f;
		foreach (KeyValuePair<int, NpcStatistics> npcStatistic in m_NpcStatistics)
		{
			AcierProtocol.MissionResult.NpcStats npcStats = new AcierProtocol.MissionResult.NpcStats();
			npcStats.npcTypeGameIdHash = npcStatistic.Key;
			npcStats.killCount = npcStatistic.Value.KillCount;
			AcierProtocol.MissionResult.NpcStats.SkillUsageStats[] array = new AcierProtocol.MissionResult.NpcStats.SkillUsageStats[npcStatistic.Value.SkillUsage.Keys.Count];
			Dictionary<string, SkillUsageStats>.ValueCollection.Enumerator enumerator2 = npcStatistic.Value.SkillUsage.Values.GetEnumerator();
			for (int j = 0; j < npcStatistic.Value.SkillUsage.Values.Count; j++)
			{
				enumerator2.MoveNext();
				SkillUsageStats current2 = enumerator2.Current;
				array[j] = new AcierProtocol.MissionResult.NpcStats.SkillUsageStats
				{
					num = current2.UsageCounter,
					ruleGuidHash = current2.RuleGuid
				};
			}
			npcStats.skillUsageStats = array;
			missionResult.npcStats = missionResult.npcStats.Add(npcStats);
		}
		Array values = Enum.GetValues(typeof(EAlertLevel));
		AcierProtocol.MissionResult.NotorietyStats[] array2 = new AcierProtocol.MissionResult.NotorietyStats[values.Length];
		for (int k = 0; k < values.Length; k++)
		{
			EAlertLevel level = (EAlertLevel)(int)values.GetValue(k);
			array2[k] = new AcierProtocol.MissionResult.NotorietyStats
			{
				level = level,
				num = GetNotoriety(level)
			};
		}
		missionResult.notorietyStats = array2;
		Array values2 = Enum.GetValues(typeof(EHotspot));
		AcierProtocol.MissionResult.HotspotStats[] array3 = new AcierProtocol.MissionResult.HotspotStats[values2.Length];
		for (int l = 0; l < values2.Length; l++)
		{
			EHotspot eHotspot = (EHotspot)(int)values2.GetValue(l);
			array3[l] = new AcierProtocol.MissionResult.HotspotStats
			{
				hotspotName = eHotspot.ToString(),
				num = GetUsedHotspotCount(eHotspot),
				duration = (int)((float)GetHotspotDuration(eHotspot) * 0.1f)
			};
		}
		missionResult.hotspotStats = array3;
		AcierProtocol.MissionResult.SkillStats[] array4 = new AcierProtocol.MissionResult.SkillStats[m_skillUsage.Keys.Count];
		int num = 0;
		foreach (KeyValuePair<string, SkillUsageStats> item in m_skillUsage)
		{
			AcierProtocol.MissionResult.SkillStats skillStats = new AcierProtocol.MissionResult.SkillStats();
			array4[num++] = skillStats;
			skillStats.ruleGuidHash = item.Value.RuleGuid;
			skillStats.num = item.Value.UsageCounter;
			skillStats.duration = (int)((float)item.Value.UsageTurnDuration * 0.1f);
		}
		missionResult.skillStats = array4;
		return missionResult;
	}

	public static bool ConvertTrackedHotspotType(InteractableObject _interactObject, out EHotspot _hotspotType, out ELimitType _limitType)
	{
		if (_interactObject is Haystack)
		{
			_hotspotType = EHotspot.EHotspot_Haystack;
			_limitType = ELimitType.HotspotHaystack;
			return true;
		}
		if (_interactObject is Bench)
		{
			_hotspotType = EHotspot.EHotspot_Bench;
			_limitType = ELimitType.HotspotBench;
			return true;
		}
		if (_interactObject is RooftopGarden)
		{
			_hotspotType = EHotspot.EHotspot_RooftopGarden;
			_limitType = ELimitType.HotspotRooftopGarden;
			return true;
		}
		if (_interactObject is Pigeonry)
		{
			_hotspotType = EHotspot.EHotspot_PigeonCoup;
			_limitType = ELimitType.HotspotPigeonCoup;
			return true;
		}
		_hotspotType = EHotspot.EHotspot_Bench;
		_limitType = ELimitType.Combat;
		return false;
	}

	public static bool ConvertTrackedHotspotType(EPatrolType _patrolType, out EHotspot _hotspotType, out ELimitType _limitType)
	{
		switch (_patrolType)
		{
		case EPatrolType.CevilianHidingSpot:
			_hotspotType = EHotspot.EHotspot_CivilianCover;
			_limitType = ELimitType.HotspotCivilianCover;
			return true;
		case EPatrolType.CourtesanHidingSpot:
			_hotspotType = EHotspot.EHotspot_Courtesans;
			_limitType = ELimitType.HotspotCourtesans;
			return true;
		default:
			_hotspotType = EHotspot.EHotspot_Bench;
			_limitType = ELimitType.Combat;
			return false;
		}
	}

	public static bool WillHidingLimitFailOnIncrease(ELimitType _limitType)
	{
		BBList<IObjectiveBase> missionObjectives = UbiCore.Utils.Singleton<MissionManager>.Instance.Phase.MissionObjectives;
		int count = missionObjectives.Count;
		for (int i = 0; i < count; i++)
		{
			IObjectiveBase objectiveBase = missionObjectives[i];
			if (objectiveBase.IsActive && objectiveBase is IMonitoredHidingBehaviour monitoredHidingBehaviour && monitoredHidingBehaviour.MonitorsLimitType(_limitType) && monitoredHidingBehaviour.PreventFailingOnIncrease && monitoredHidingBehaviour.WillFailOnIncrease(1))
			{
				return true;
			}
		}
		return false;
	}

	public int GetNotoriety(EAlertLevel _level)
	{
		return m_notorietyCounters[(int)_level];
	}

	public int GetSkillUsageCount(string _skillName)
	{
		if (new Enum<SkillTypes>(_skillName).IsValid && m_skillUsage.TryGetValue(_skillName, out var value))
		{
			return value.UsageCounter;
		}
		return 0;
	}

	public int GetSkillUsageTurnDuration(string _skillName)
	{
		if (new Enum<SkillTypes>(_skillName).IsValid && m_skillUsage.TryGetValue(_skillName, out var value))
		{
			return value.UsageTurnDuration;
		}
		return 0;
	}

	public int GetSkillUsageCount(string _skillName, NpcData.NpcType _targetNpcType)
	{
		if (m_NpcStatistics.TryGetValue(_targetNpcType.Definition.GameID.Guid, out var value) && value.SkillUsage.TryGetValue(_skillName, out var value2))
		{
			return value2.UsageCounter;
		}
		return 0;
	}

	public int GetUsedHotspotCount(EHotspot _hotspot)
	{
		return m_hotspotCounters[(int)_hotspot];
	}

	public int GetHotspotDuration(EHotspot _hotspot)
	{
		int value = 0;
		m_hotspotTurnDuration.TryGetValue((int)_hotspot, out value);
		return value;
	}

	public int GetKilledEnemyCount(EStateType _charCondition)
	{
		int value = 0;
		m_killedEnemyCountByCondition.TryGetValue((int)_charCondition, out value);
		return value;
	}

	public int GetKilledEnemyCount(EKillType _primaryKillType)
	{
		int value = 0;
		m_killedEnemyCountByKillType.TryGetValue((int)_primaryKillType, out value);
		return value;
	}

	public override void OnRegistered()
	{
		base.OnRegistered();
		IsActive = true;
		if (m_ExternallyCountedNpcStatSkillIndices.Count == 0)
		{
			m_ExternallyCountedNpcStatSkillIndices.AddRange(new int[3]
			{
				new Enum<SkillTypes>("Pickpocketing_01").Index,
				new Enum<SkillTypes>("Lossy_Dreams_Approaching").Index,
				new Enum<SkillTypes>("Greed_01").Index
			});
		}
		EarlyReset();
	}

	public override void DoTurnUpdate(int _deltaTurns)
	{
		base.DoTurnUpdate(_deltaTurns);
		if (LogicMain.CurrentTurn > 1)
		{
			float val = LogicMain.PlayerEntity.EntityStats.Get_float(StatTypes.EStatType.Character_Conflict_Health);
			LowestBaseHealth = Math.Min(LowestBaseHealth, val);
			float val2 = LogicMain.PlayerEntity.EntityStats.Get_float(StatTypes.EStatType.Character_Conflict_ActualMaxHealth);
			LowestBaseHealthMax = Math.Min(LowestBaseHealthMax, val2);
		}
		if (PlayerHelper.IsInHaystack)
		{
			BBDictionary<int, int> hotspotTurnDuration;
			BBDictionary<int, int> bBDictionary = (hotspotTurnDuration = m_hotspotTurnDuration);
			int key2;
			int key = (key2 = 0);
			key2 = hotspotTurnDuration[key2];
			bBDictionary[key] = key2 + 1;
		}
		else if (PlayerHelper.IsOnBench)
		{
			BBDictionary<int, int> hotspotTurnDuration2;
			BBDictionary<int, int> bBDictionary2 = (hotspotTurnDuration2 = m_hotspotTurnDuration);
			int key2;
			int key3 = (key2 = 1);
			key2 = hotspotTurnDuration2[key2];
			bBDictionary2[key3] = key2 + 1;
			if (m_hotspotTurnDuration[1] % 10 == 0)
			{
				IncreaseAchievementCounter(ECounterType.RestOnBenchSeconds, 1uL);
			}
		}
		else if (PlayerHelper.IsInRooftopGarden)
		{
			BBDictionary<int, int> hotspotTurnDuration3;
			BBDictionary<int, int> bBDictionary3 = (hotspotTurnDuration3 = m_hotspotTurnDuration);
			int key2;
			int key4 = (key2 = 2);
			key2 = hotspotTurnDuration3[key2];
			bBDictionary3[key4] = key2 + 1;
		}
		else if (PlayerHelper.IsHidingInCourtesans)
		{
			BBDictionary<int, int> hotspotTurnDuration4;
			BBDictionary<int, int> bBDictionary4 = (hotspotTurnDuration4 = m_hotspotTurnDuration);
			int key2;
			int key5 = (key2 = 8);
			key2 = hotspotTurnDuration4[key2];
			bBDictionary4[key5] = key2 + 1;
		}
		else if (PlayerHelper.IsHidingInPatrol)
		{
			BBDictionary<int, int> hotspotTurnDuration5;
			BBDictionary<int, int> bBDictionary5 = (hotspotTurnDuration5 = m_hotspotTurnDuration);
			int key2;
			int key6 = (key2 = 3);
			key2 = hotspotTurnDuration5[key2];
			bBDictionary5[key6] = key2 + 1;
		}
		else if (PlayerHelper.IsStalking)
		{
			BBDictionary<int, int> hotspotTurnDuration6;
			BBDictionary<int, int> bBDictionary6 = (hotspotTurnDuration6 = m_hotspotTurnDuration);
			int key2;
			int key7 = (key2 = 10);
			key2 = hotspotTurnDuration6[key2];
			bBDictionary6[key7] = key2 + 1;
		}
		Playtime += (float)_deltaTurns * 0.1f;
	}

	public override void OnActivate()
	{
		base.OnActivate();
		LogicMain.PlayerEntity.Skills.OnSkillEvent += OnSkillEvent;
		LogicMain.PlayerEntity.PlayerCharacterStateMachines.PlayerMain.StateChanged += PlayerMainOnStateChanged;
		LogicMain.PlayerEntity.PlayerCharacterStateMachines.PlayerAlertLevel.StateChanged += PlayerAlertLevelOnStateChanged;
		LogicMain.PlayerEntity.PlayerCharacterStateMachines.CharacterVitality.StateChanged += PlayerVitalityOnStateChanged;
		LogicMain.PlayerEntity.PlayerCharacterStateMachines.PlayerStalkingZone.StateChanged += PlayerStalkingZoneOnStateChanged;
		Messenger.Instance.AddListener<StateChangedMessage<CharacterInteraction>>(OnCharacterInteractionStateChanged);
		Messenger.Instance.AddListener<StateChangedMessage<ObjectInteraction>>(OnObjectInteractionStateChanged);
		Messenger.Instance.AddListener<StateChangedMessage<PatrolSeduce>>(OnPatrolSeduceStateChanged);
		Messenger.Instance.AddListener<StateChangedMessage<CharacterCondition>>(OnCharConditionStateChanged);
		Messenger.Instance.AddListener<NPCKillEvent>(OnNpcKilled);
		Reset(_onActivated: true);
		Singleton<StateManager>.Instance.AchievementsManager.OnMissionLoaded();
		m_omitCounting = Main.Instance.SceneLoader.CurrentMissionDescription.ObjectiveType == EObjectiveType.Tutorial_EObjectiveType;
	}

	public override void OnUnregistered()
	{
		base.OnUnregistered();
		LogicMain.PlayerEntity.Skills.OnSkillEvent -= OnSkillEvent;
		LogicMain.PlayerEntity.PlayerCharacterStateMachines.PlayerMain.StateChanged -= PlayerMainOnStateChanged;
		LogicMain.PlayerEntity.PlayerCharacterStateMachines.PlayerAlertLevel.StateChanged -= PlayerAlertLevelOnStateChanged;
		LogicMain.PlayerEntity.PlayerCharacterStateMachines.CharacterVitality.StateChanged -= PlayerVitalityOnStateChanged;
		LogicMain.PlayerEntity.PlayerCharacterStateMachines.PlayerStalkingZone.StateChanged -= PlayerStalkingZoneOnStateChanged;
		Messenger.Instance.RemoveListener<StateChangedMessage<CharacterInteraction>>(OnCharacterInteractionStateChanged);
		Messenger.Instance.RemoveListener<StateChangedMessage<ObjectInteraction>>(OnObjectInteractionStateChanged);
		Messenger.Instance.RemoveListener<StateChangedMessage<PatrolSeduce>>(OnPatrolSeduceStateChanged);
		Messenger.Instance.RemoveListener<StateChangedMessage<CharacterCondition>>(OnCharConditionStateChanged);
		Messenger.Instance.RemoveListener<NPCKillEvent>(OnNpcKilled);
		EarlyReset();
		Reset(_onActivated: false);
	}

	public void ResetHealthStats()
	{
		LowestBaseHealth = float.MaxValue;
		LowestBaseHealthMax = float.MaxValue;
	}

	public void ResetConsumedItems()
	{
		ConsumedLockPicks = 0;
	}

	private void OnNpcKilled(NPCKillEvent _message)
	{
		EnemiesKilledCount += 1;
		IncreaseAchievementCounter(ECounterType.Kills, 1uL);
		if (LogicMain.CurrentTime < 600f)
		{
			EnemiesKilledWithin10MinutesCount += 1;
			IncreaseAchievementCounter(ECounterType.KillingSpree10Minutes, 1uL);
		}
		EnemyCharacter enemy = _message.LogicEntity as EnemyCharacter;
		if (enemy == null)
		{
			return;
		}
		int guid = enemy.NpcDefinition.Definition.GameID.Guid;
		if (!m_NpcStatistics.TryGetValue(guid, out var value))
		{
			value = new NpcStatistics(enemy.NpcDefinition.Definition.GameID);
			m_NpcStatistics[guid] = value;
		}
		value.KillCount++;
		if (enemy.NpcDefinition.Definition.HumanoidGameID == UbiCore.Utils.Singleton<GlobalGameIDs>.Instance.HumanoidCrowleaderGuid)
		{
			IncreaseCrowsKilledCount();
		}
		EKillType eKillType = (EKillType)enemy.EntityStats.Get_int(StatTypes.EStatType.Character_Conflict_KillTypeFlags);
		EKillType key = eKillType & EKillType.Primary;
		int value2 = 0;
		m_killedEnemyCountByKillType.TryGetValue((int)key, out value2);
		value2++;
		m_killedEnemyCountByKillType[(int)key] = value2;
		if ((eKillType & EKillType.AssassinationRooftopGarden) != 0)
		{
			IncreaseAchievementCounter(ECounterType.EnemyDragsIntoRoofgarden, 1uL);
		}
		if ((eKillType & EKillType.Skill) == EKillType.Skill)
		{
			IncreaseEnemiesKilledBySkillCount();
		}
		if ((eKillType & EKillType.Bomb) == EKillType.Bomb)
		{
			IncreaseEnemiesKilledWithBomb();
		}
		if ((eKillType & EKillType.Assassination) != 0)
		{
			AssassinationCount += 1;
			IncreaseAchievementCounter(ECounterType.AssassinationKills, 1uL);
			int num = 0;
			for (num = 0; num < m_KillObjectives.Count; num++)
			{
				ObjectiveKill objectiveKill = m_KillObjectives[num];
				if (objectiveKill.Targets.Any((IMissionEntity x) =>
				{
					IMissionSpawnEntity missionSpawnEntity = (IMissionSpawnEntity)x;
					return missionSpawnEntity.EntityID == enemy.Id || missionSpawnEntity.FormerEntityId == enemy.Id;
				}))
				{
					IncreaseAssassinatedMissionTargets();
					break;
				}
			}
			if (num == m_KillObjectives.Count)
			{
				for (num = 0; num < m_KillTypeObjectives.Count; num++)
				{
					ObjectiveKillEnemyType objectiveKillEnemyType = m_KillTypeObjectives[num];
					if (objectiveKillEnemyType.NpcType == enemy.NpcDefinition.Definition.Type)
					{
						IncreaseAssassinatedMissionTargets();
						break;
					}
				}
				if (num == m_KillTypeObjectives.Count)
				{
					m_AssassinatedEnemyIds.Add(enemy.Id);
				}
			}
			if (LogicMain.PlayerEntity.PlayerCharacterStateMachines.AssassinApproachingTarget.PlayerMainStateOnSetTarget == EStateType.PlayerMain_HidingInPatrol && LogicMain.PlayerEntity.PlayerCharacterStateMachines.AssassinApproachingTarget.TargetDistanceOnSetTarget <= 7f)
			{
				IncreeaseKilledEnemiesFromCivilianCoverCount();
			}
		}
		if ((eKillType & EKillType.AssassinationAir) == EKillType.AssassinationAir)
		{
			IncreaseAchievementCounter(ECounterType.AirAssassinationKills, 1uL);
		}
		if ((eKillType & EKillType.Combat) == EKillType.Combat)
		{
			EnemiesKilledInCombatCount += 1;
		}
		if ((eKillType & EKillType.Token) == EKillType.Token)
		{
			IncreaseEnemiesKilledWithCombo();
		}
		if (NavMesh.SamplePosition(enemy.Position, out var hit, 0.5f, NavMeshLayers.WalkableLayerMaskPreset) && (hit.mask & NavMeshLayers.RoofSurfaceLayerMask) == NavMeshLayers.RoofSurfaceLayerMask)
		{
			IncreaseEnemiesOnRooftopsKilled();
		}
		EStateType state = enemy.EnemyCharacterStateMachines.CharacterCondition.GetCondition(EStateType.Invalid, _restartTask: false, _ignoreDeath: true).State;
		int value3 = 0;
		m_killedEnemyCountByCondition.TryGetValue((int)state, out value3);
		value3++;
		m_killedEnemyCountByCondition[(int)state] = value3;
		switch (state)
		{
		case EStateType.CharacterCondition_Greedy:
			IncreaseAchievementCounter(ECounterType.KillsWhileGrabingMoney, 1uL);
			break;
		case EStateType.CharacterCondition_Knockdowned:
			IncreaseAchievementCounter(ECounterType.KillsWhileKnockedDown, 1uL);
			break;
		case EStateType.CharacterCondition_SmokeBombed:
			IncreaseAchievementCounter(ECounterType.KillsOutOfSmokeBombCover, 1uL);
			break;
		}
		PlayerStats playerStats = LogicMain.PlayerEntity.PlayerStats;
		float num2 = playerStats.Get_float(StatTypes.EStatType.Character_Conflict_Health) / playerStats.Get_float(StatTypes.EStatType.Character_Conflict_BaseHealth);
		if (0f < num2 && num2 < 0.05f)
		{
			IncreaseEnemiesKilledWithLowPlayerHealth();
		}
	}

	private void OnCharacterInteractionStateChanged(StateChangedMessage<CharacterInteraction> _message)
	{
		if (_message.To == EStateType.CharacterInteraction_OfferAccepted)
		{
			InteractionCharacter entity = _message.StateMachine.Entity;
			if (entity.NpcDefinition.Definition.InteractionType != NpcData.NpcDefinition.ENpcInteractionType.Merchant)
			{
			}
		}
	}

	private void OnObjectInteractionStateChanged(StateChangedMessage<ObjectInteraction> _message)
	{
		if (_message.To != EStateType.Object_Disabled)
		{
			return;
		}
		InteractableObject entity = _message.StateMachine.Entity;
		if (entity.InteractableObjectDefinition.Definition.Category == EObjectCategory.Mission_EObjectCategory)
		{
			switch (entity.InteractableObjectDefinition.Definition.SubCategory)
			{
			case EObjectSubCategory.Chest:
				IncreaseUsedHotspotCount(EHotspot.EHotspot_Chests);
				break;
			case EObjectSubCategory.PigeonCoup:
				IncreaseUsedHotspotCount(EHotspot.EHotspot_PigeonCoup);
				break;
			case EObjectSubCategory.Elevated:
				IncreaseUsedHotspotCount(EHotspot.EHotspot_Chests);
				break;
			case (EObjectSubCategory)3:
				break;
			}
		}
	}

	private void OnPatrolSeduceStateChanged(StateChangedMessage<PatrolSeduce> _message)
	{
		if (_message.To == EStateType.PatrolSeduce_Approaching)
		{
			IncreaseSentCourtesanGroupsCount();
		}
	}

	private void OnCharConditionStateChanged(StateChangedMessage<CharacterCondition> _message)
	{
		if (_message.To == EStateType.CharacterCondition_Fleeing)
		{
			IncreaseEnemiesFledCounter();
		}
	}

	private void OnSkillEvent(SkillEvent _message)
	{
		SkillData.ISkillDefinition definition = _message.SkillType.GetDefinition();
		string name = definition.BaseSkillType.Name;
		if (_message.SubType == ESkillEventType.ActivityStart)
		{
			if ((definition.ActivationType & ESkillActivationType.Slots) == definition.ActivationType)
			{
				UsedSkillsCount += 1;
				IncreaseAchievementCounter(ECounterType.UsedSkills, 1uL);
			}
			if (!m_skillUsage.TryGetValue(name, out var value))
			{
				value = new SkillUsageStats(definition);
				m_skillUsage[name] = value;
			}
			value.UsageCounter++;
			if (!m_ExternallyCountedNpcStatSkillIndices.Contains(definition.BaseSkillType.Index) && _message.TargetNpcType != null)
			{
				int guid = _message.TargetNpcType.GameID.Guid;
				if (!m_NpcStatistics.TryGetValue(guid, out var value2))
				{
					value2 = new NpcStatistics(_message.TargetNpcType.GameID);
					m_NpcStatistics[guid] = value2;
				}
				if (!value2.SkillUsage.TryGetValue(name, out value))
				{
					value = new SkillUsageStats(definition);
					value2.SkillUsage[name] = value;
				}
				value.UsageCounter++;
			}
			switch (name)
			{
			case "Smoke_Bomb_01":
				IncreaseAchievementCounter(ECounterType.UsedSmokeBombs, 1uL);
				break;
			case "Call_Hireling":
				IncreaseAchievementCounter(ECounterType.NumHirelingsUsed, 1uL);
				break;
			}
		}
		else if ((_message.SubType == ESkillEventType.Abort || _message.SubType == ESkillEventType.ActivityEnd) && _message.TurnDuration > 0)
		{
			if (!m_skillUsage.TryGetValue(name, out var value3))
			{
				value3 = new SkillUsageStats(definition);
				m_skillUsage[name] = value3;
			}
			value3.UsageTurnDuration += _message.TurnDuration;
			ulong increase = (ulong)((float)_message.TurnDuration * 0.1f);
			switch (name)
			{
			case "Disguise_01":
				IncreaseAchievementCounter(ECounterType.DisguiseTimeInSeconds, increase);
				break;
			case "Eagle_Vision":
				IncreaseAchievementCounter(ECounterType.EagleVisionUsageInSeconds, increase);
				break;
			}
		}
	}

	private void PlayerAlertLevelOnStateChanged(IStateMachine _stateMachine, EStateType _from, EStateType _to)
	{
		switch (_to)
		{
		case EStateType.CharacterAlertLevel_Alerted:
			IncreaseNotorietyCounter(EAlertLevel.Alerted);
			break;
		case EStateType.CharacterAlertLevel_Anonymous:
			IncreaseNotorietyCounter(EAlertLevel.Anonymous);
			break;
		case EStateType.CharacterAlertLevel_Exposed:
			IncreaseNotorietyCounter(EAlertLevel.Exposed);
			break;
		}
	}

	private void PlayerVitalityOnStateChanged(IStateMachine _stateMachine, EStateType _from, EStateType _to)
	{
		if (_to == EStateType.Character_Dead)
		{
			IncreasePlayerDeathsCount();
			EKillType eKillType = (EKillType)_stateMachine.EntityBase.EntityStats.Get_int(StatTypes.EStatType.Character_Conflict_KillTypeFlags);
			if ((eKillType & (EKillType.Skill | EKillType.Suicide)) == (EKillType.Skill | EKillType.Suicide))
			{
				IncreasePlayerDiedByOwnBombCount();
			}
		}
	}

	private void PlayerStalkingZoneOnStateChanged(IStateMachine _stateMachine, EStateType _from, EStateType _to)
	{
		if (_to == EStateType.PlayerStalkingZone_Stalking)
		{
			UsedHidingSpotCount += 1;
			IncreaseUsedHotspotCount(EHotspot.EHotspot_StalkingZone);
		}
	}

	private void PlayerMainOnStateChanged(IStateMachine _stateMachine, EStateType _from, EStateType _to)
	{
		switch (_to)
		{
		case EStateType.PlayerMain_HidingInObject:
		{
			PlayerMain.HidingInObjectState.HidingInObjectPayload payload2 = _stateMachine.GetPayload<PlayerMain.HidingInObjectState.HidingInObjectPayload>(_to);
			if (!payload2.ShouldExit)
			{
				InteractableObject entity4 = payload2.EntityId.GetEntity<InteractableObject>();
				if (ConvertTrackedHotspotType(entity4, out var _hotspotType2, out var _))
				{
					UsedHidingSpotCount += 1;
					IncreaseUsedHotspotCount(_hotspotType2);
				}
			}
			break;
		}
		case EStateType.PlayerMain_HidingInPatrol:
		{
			PlayerMain.HidingInPatrolState.HidingInPatrolPayload payload = _stateMachine.GetPayload<PlayerMain.HidingInPatrolState.HidingInPatrolPayload>(_to);
			if (!payload.ShouldExit)
			{
				Assets.Scripts.Mission.Logic.Entities.Patrol entity3 = payload.PatrolId.GetEntity<Assets.Scripts.Mission.Logic.Entities.Patrol>();
				EPatrolType type = entity3.PatrolDefinition.Definition.Type;
				if (ConvertTrackedHotspotType(type, out var _hotspotType, out var _))
				{
					UsedHidingSpotCount += 1;
					IncreaseUsedHotspotCount(_hotspotType);
				}
			}
			break;
		}
		case EStateType.PlayerMain_NpcInteraction:
		{
			InteractionCharacter entity2 = _stateMachine.GetPayload<PlayerMain.InteractingWithEntityState.InteractingWithEntityPayload>(_to).EntityId.GetEntity<InteractionCharacter>();
			if (entity2.NpcDefinition.Definition.InteractionType != NpcData.NpcDefinition.ENpcInteractionType.Merchant && entity2.NpcDefinition.Definition.InteractionType == NpcData.NpcDefinition.ENpcInteractionType.Mission)
			{
				IncreaseMessagesDeliveredInMission();
			}
			break;
		}
		case EStateType.PlayerMain_ObjectInteraction:
		{
			InteractableObject entity = _stateMachine.GetPayload<PlayerMain.InteractingWithEntityState.InteractingWithEntityPayload>(_to).EntityId.GetEntity<InteractableObject>();
			if (entity.InteractableObjectDefinition.Definition.Category != EObjectCategory.Mission_EObjectCategory)
			{
			}
			break;
		}
		case EStateType.CombatCharacterMain_Combat:
			IncreaseCombatCounter();
			break;
		}
	}

	private void IncreaseAchievementCounter(ECounterType _counterType, ulong _increase)
	{
		if ((int)_counterType < m_AchievementCounters.Count)
		{
			BBList<ulong> achievementCounters;
			BBList<ulong> bBList = (achievementCounters = m_AchievementCounters);
			int index2;
			int index = (index2 = (int)_counterType);
			ulong num = achievementCounters[index2];
			bBList[index] = num + _increase;
			if (!m_omitCounting)
			{
				Singleton<StateManager>.Instance.AchievementsManager.CheckIngameAchievements(_counterType, m_AchievementCounters, m_AchievementCountersPrevious);
			}
			m_AchievementCountersPrevious[(int)_counterType] = m_AchievementCounters[(int)_counterType];
		}
	}

	private void EarlyReset()
	{
		m_SucceededChallenges.Clear();
	}

	private void Reset(bool _onActivated)
	{
		AssassinationCount = 0;
		CombatCount = 0;
		EnemiesKilledInCombatCount = 0;
		GroundTurnCount = 0;
		MaxGroundTurnCount = 0;
		EnterGroundCount = 0;
		EnterRoofCount = 0;
		FledCount = 0;
		EnemiesFledCount = 0;
		EnemiesKilledCount = 0;
		EnemiesKilledWithin10MinutesCount = 0;
		EnemiesShreddedCount = 0;
		FrightenedTurnCount = 0;
		CriticalHitCount = 0;
		CrowsKilledCount = 0;
		CausedSplinterBombBleedingTurnCount = 0;
		TracedBySeekersTurnCount = 0;
		UsedHidingSpotCount = 0;
		SentCourtesanGroupsCount = 0;
		UsedSkillsCount = 0;
		KilledEnemiesBySkillCount = 0;
		EnemiesOnRooftopsKilledCount = 0;
		KilledEnemiesFromCivilianCoverCount = 0;
		EnemiesKilledWithLowPlayerHealthCount = 0;
		EnemiesKilledWithCombo = 0;
		EnemiesKilledWithBomb = 0;
		PlayerDeathsCount = 0;
		PlayerDiedByOwnBombCount = 0;
		LostCaptivesCount = 0;
		SuccessfullyParried = 0;
		SwitchedTarget = 0;
		SuccessfullHirelingAttacks = 0;
		FlushedPigeonsCount = 0;
		Playtime = 0f;
		RegularDamageDealtSum = 0f;
		RegularDamageDealtCount = 0;
		FailMissionID = 0L;
		FailMissionLocaID = 0;
		TreasureChestsOpenedInMission = 0;
		MessagesDeliveredInMission = 0;
		AssassinatedMissionTargets = 0;
		ReviveWithAdColonyCount = 0;
		m_omitCounting = false;
		ResetHealthStats();
		m_skillUsage.Clear();
		foreach (int item in m_notorietyCounters.Keys.ToList())
		{
			m_notorietyCounters[item] = 0;
		}
		PlayerAlertLevelOnStateChanged(LogicMain.PlayerEntity.PlayerCharacterStateMachines.PlayerAlertLevel, EStateType.Invalid, LogicMain.PlayerEntity.PlayerCharacterStateMachines.PlayerAlertLevel.CurrentStateType);
		foreach (int item2 in m_hotspotCounters.Keys.ToList())
		{
			m_hotspotCounters[item2] = 0;
		}
		foreach (int item3 in m_hotspotTurnDuration.Keys.ToList())
		{
			m_hotspotTurnDuration[item3] = 0;
		}
		foreach (int item4 in m_killedEnemyCountByCondition.Keys.ToList())
		{
			m_killedEnemyCountByCondition[item4] = 0;
		}
		m_killedEnemyCountByKillType.Clear();
		m_NpcStatistics.Clear();
		if (!_onActivated)
		{
			m_KillObjectives.Clear();
			m_KillTypeObjectives.Clear();
			m_AssassinatedEnemyIds.Clear();
		}
		if (Singleton<StateManager>.Instance.ProfileManager.Profile == null)
		{
			return;
		}
		for (int i = 0; i < m_AchievementCounters.Count; i++)
		{
			m_AchievementCounters[i] = 0uL;
			m_AchievementCountersPrevious[i] = 0uL;
		}
		Counter[] counters = Singleton<StateManager>.Instance.ProfileManager.Profile.counters.counters;
		foreach (Counter counter in counters)
		{
			int num = (int)counter.ECounterTypeOrdinal;
			if (num < m_AchievementCounters.Count)
			{
				m_AchievementCounters[num] = (ulong)counter.num;
				m_AchievementCountersPrevious[num] = (ulong)counter.num;
			}
		}
	}

	private void IncreaseEnemiesFledCounter()
	{
		EnemiesFledCount += 1;
		IncreaseAchievementCounter(ECounterType.EnemyFleesFromCombat, 1uL);
	}

	private void IncreaseEnemiesKilledBySkillCount()
	{
		KilledEnemiesBySkillCount += 1;
		IncreaseAchievementCounter(ECounterType.KillsBySkills, 1uL);
	}

	private void IncreaseEnemiesOnRooftopsKilled()
	{
		EnemiesOnRooftopsKilledCount += 1;
		IncreaseAchievementCounter(ECounterType.KillsOnRooftops, 1uL);
	}

	private void IncreeaseKilledEnemiesFromCivilianCoverCount()
	{
		KilledEnemiesFromCivilianCoverCount += 1;
		IncreaseAchievementCounter(ECounterType.KillsOutOfCivilianCover, 1uL);
	}

	private void IncreaseEnemiesKilledWithLowPlayerHealth()
	{
		EnemiesKilledWithLowPlayerHealthCount += 1;
		IncreaseAchievementCounter(ECounterType.KillsWith5PercentageHealth, 1uL);
	}

	private void IncreaseEnemiesKilledWithCombo()
	{
		EnemiesKilledWithCombo += 1;
		IncreaseAchievementCounter(ECounterType.KillTokenComboKills, 1uL);
	}

	private void IncreaseEnemiesKilledWithBomb()
	{
		EnemiesKilledWithBomb += 1;
		IncreaseAchievementCounter(ECounterType.EnemiesKilledByBomb, 1uL);
	}

	private void IncreaseCrowsKilledCount()
	{
		CrowsKilledCount += 1;
		IncreaseAchievementCounter(ECounterType.CrowsKilled, 1uL);
	}

	private void IncreasePlayerDeathsCount()
	{
		PlayerDeathsCount += 1;
		IncreaseAchievementCounter(ECounterType.DiesInMissions, 1uL);
	}

	private void IncreasePlayerDiedByOwnBombCount()
	{
		PlayerDiedByOwnBombCount += 1;
		IncreaseAchievementCounter(ECounterType.DieByYourOwnBomb, 1uL);
	}

	private void IncreaseSentCourtesanGroupsCount()
	{
		SentCourtesanGroupsCount += 1;
		IncreaseAchievementCounter(ECounterType.Sendcourtesans, 1uL);
	}

	private void IncreaseCombatCounter()
	{
		CombatCount += 1;
		IncreaseAchievementCounter(ECounterType.Combats, 1uL);
	}

	private void IncreaseNotorietyCounter(EAlertLevel _level)
	{
		BBDictionary<int, int> notorietyCounters;
		BBDictionary<int, int> bBDictionary = (notorietyCounters = m_notorietyCounters);
		int key2;
		int key = (key2 = (int)_level);
		key2 = notorietyCounters[key2];
		bBDictionary[key] = key2 + 1;
	}

	private void IncreaseMessagesDeliveredInMission()
	{
		MessagesDeliveredInMission += 1;
	}

	private void IncreaseAssassinatedMissionTargets()
	{
		AssassinatedMissionTargets += 1;
	}
}
