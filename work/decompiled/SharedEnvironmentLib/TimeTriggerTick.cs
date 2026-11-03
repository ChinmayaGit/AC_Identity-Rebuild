using SharedBaseLib.Core;
using UnityEngine;

internal class TimeTriggerTick : MonoBehaviour
{
	private static TimeTriggerTick ms_instance;

	private BBList<TimeTrigger> m_triggers;

	public static TimeTriggerTick Instance => ms_instance;

	private void Awake()
	{
		ms_instance = this;
		m_triggers = new BBList<TimeTrigger>();
	}

	public void StartTick()
	{
	}

	public void Tick(float accumTime)
	{
		foreach (TimeTrigger trigger in m_triggers)
		{
			trigger.Tick(accumTime);
		}
	}

	public void EndTick()
	{
	}

	public void Register(TimeTrigger _trigger)
	{
		m_triggers.Add(_trigger);
	}

	public void Unregister(TimeTrigger _trigger)
	{
		m_triggers.Remove(_trigger);
	}
}
