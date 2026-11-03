using SharedBaseLib.Core;
using UbiCore.Debug;
using UnityEngine;

public class TriggerManager : MonoBehaviour
{
	private static TriggerManager ms_instance;

	private static bool ms_applicationQuits;

	private ProximityTriggerTick m_proximityTriggers;

	private TimeTriggerTick m_timeTriggers;

	private BBList<Trigger> m_triggers;

	[SerializeField]
	private float m_updateFrequencyInSeconds = 1f / 30f;

	private float m_nextUpdateTime;

	private float m_accumTime;

	private static Plane[] ms_frustumPlanes;

	public static TriggerManager Instance
	{
		get
		{
			if (ms_instance == null)
			{
				TriggerManager triggerManager = (TriggerManager)Object.FindObjectOfType(typeof(TriggerManager));
				if (triggerManager == null)
				{
					GameObject gameObject = GameObject.FindGameObjectWithTag("Main");
					if (!(gameObject == null))
					{
						ms_instance = gameObject.AddComponent<TriggerManager>();
						ms_instance.Initialize();
					}
				}
				else
				{
					ms_instance = triggerManager;
				}
			}
			return ms_instance;
		}
	}

	private void Initialize()
	{
		m_triggers = new BBList<Trigger>();
		m_proximityTriggers = gameObject.AddComponent<ProximityTriggerTick>();
		m_timeTriggers = gameObject.AddComponent<TimeTriggerTick>();
	}

	private void Start()
	{
		m_nextUpdateTime = m_updateFrequencyInSeconds;
	}

	private void Update()
	{
		float deltaTime = Time.deltaTime;
		if (m_updateFrequencyInSeconds < 1f / 60f)
		{
			m_accumTime += deltaTime;
			Tick();
			return;
		}
		m_nextUpdateTime -= deltaTime;
		m_accumTime += deltaTime;
		if (m_nextUpdateTime < 0f - m_updateFrequencyInSeconds)
		{
			m_nextUpdateTime = m_updateFrequencyInSeconds - (0f - m_nextUpdateTime) % m_updateFrequencyInSeconds;
			Tick();
		}
		else if (m_nextUpdateTime < 0f)
		{
			m_nextUpdateTime += m_updateFrequencyInSeconds;
			Tick();
		}
	}

	private void OnApplicationQuit()
	{
		ms_applicationQuits = true;
	}

	private void StartTick()
	{
		ms_frustumPlanes = null;
		m_proximityTriggers.StartTick();
		m_timeTriggers.StartTick();
	}

	private void Tick()
	{
		Profiling.Start("TriggerManager::Tick()", float.MaxValue);
		StartTick();
		if (Application.isEditor)
		{
			TickValidate();
		}
		Profiling.Start("ProximityTriggerTick::Tick()", float.MaxValue);
		m_proximityTriggers.Tick(m_accumTime);
		Profiling.Stop();
		Profiling.Start("TimeTriggerTick::Tick()", float.MaxValue);
		m_timeTriggers.Tick(m_accumTime);
		Profiling.Stop();
		m_accumTime = 0f;
		EndTick();
		Profiling.Stop();
	}

	private void EndTick()
	{
		m_proximityTriggers.EndTick();
		m_timeTriggers.EndTick();
	}

	private void TickValidate()
	{
		BBList<Trigger> bBList = null;
		foreach (Trigger trigger in m_triggers)
		{
			if (!trigger.Validate(verbose: true))
			{
				if (bBList == null)
				{
					bBList = new BBList<Trigger>();
				}
				bBList.Add(trigger);
			}
		}
		if (bBList == null)
		{
			return;
		}
		foreach (Trigger item in bBList)
		{
			item.enabled = false;
		}
	}

	private void RegisterTrigger(Trigger _trigger)
	{
		m_triggers.Add(_trigger);
		_trigger.RegisterTick();
	}

	private void UnregisterTrigger(Trigger _trigger)
	{
		m_triggers.Remove(_trigger);
		_trigger.UnregisterTick();
	}

	internal static void Register(Trigger _trigger)
	{
		if (!ms_applicationQuits && Application.isPlaying)
		{
			Instance.RegisterTrigger(_trigger);
		}
	}

	internal static void Unregister(Trigger _trigger)
	{
		if (!ms_applicationQuits && Application.isPlaying)
		{
			Instance.UnregisterTrigger(_trigger);
		}
	}

	public static Plane[] GetFrustumPlanes()
	{
		if (ms_frustumPlanes == null)
		{
			Camera main = Camera.main;
			if (main != null)
			{
				ms_frustumPlanes = GeometryUtility.CalculateFrustumPlanes(main);
			}
		}
		return ms_frustumPlanes;
	}
}
