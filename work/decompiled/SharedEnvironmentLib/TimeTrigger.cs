using UnityEngine;

[ExecuteInEditMode]
internal class TimeTrigger : Trigger
{
	[SerializeField]
	private float minPauseInSeconds = 1f;

	[SerializeField]
	private float maxPauseInSeconds = 2f;

	private float m_nextToggleTime;

	private void Start()
	{
		RandomizeNextToggleTime();
	}

	public override bool Validate(bool verbose)
	{
		bool flag = minPauseInSeconds <= maxPauseInSeconds;
		if (verbose)
		{
		}
		if (flag)
		{
			return base.Validate(verbose);
		}
		return false;
	}

	public override void RegisterTick()
	{
		TimeTriggerTick.Instance.Register(this);
	}

	public override void UnregisterTick()
	{
		TimeTriggerTick.Instance.Unregister(this);
	}

	public void Tick(float accumTime)
	{
		m_nextToggleTime -= accumTime;
		if (m_nextToggleTime < 0f)
		{
			Toggle();
		}
	}

	private void Toggle()
	{
		if (m_state == State.Inside)
		{
			TryLeave();
		}
		else
		{
			TryEnter();
		}
		RandomizeNextToggleTime();
	}

	private void RandomizeNextToggleTime()
	{
		m_nextToggleTime = Random.Range(minPauseInSeconds, maxPauseInSeconds);
	}
}
