using UnityEngine;

[ExecuteInEditMode]
internal class ProximityTrigger : Trigger
{
	private enum Tags
	{
		All,
		Player,
		Enemy
	}

	[SerializeField]
	private Tags m_tags;

	private TriggerShape m_shape;

	private bool m_toggle;

	private new void OnEnable()
	{
		m_shape = GetComponent<TriggerShape>();
		base.OnEnable();
	}

	public override bool Validate(bool verbose)
	{
		bool flag = m_shape != null;
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
		ProximityTriggerTick.Instance.Register(this);
	}

	public override void UnregisterTick()
	{
		ProximityTriggerTick.Instance.Unregister(this);
	}

	private bool ShouldSkipTag(string tag)
	{
		return m_tags switch
		{
			Tags.Player => tag != "Player", 
			Tags.Enemy => tag != "Enemy", 
			_ => false, 
		};
	}

	public void Tick(TriggerTransform transform)
	{
		if (m_state == State.Inside)
		{
			TickInside(transform);
		}
		else
		{
			TickOutside(transform);
		}
	}

	private void TickInside(TriggerTransform _transform)
	{
		if (m_toggle && !ShouldSkipTag(_transform.tag) && m_shape.Inside(_transform.transform.position))
		{
			m_toggle = false;
		}
	}

	private void TickOutside(TriggerTransform _transform)
	{
		if (!m_toggle && !ShouldSkipTag(_transform.tag) && m_shape.Inside(_transform.transform.position))
		{
			m_toggle = true;
		}
	}

	public void Evalulate()
	{
		if (m_toggle)
		{
			if (m_state == State.Inside)
			{
				TryLeave();
			}
			else
			{
				TryEnter();
			}
		}
		m_toggle = m_state == State.Inside;
	}

	private void OnDrawGizmosSelected()
	{
		if (enabled && m_shape != null)
		{
			if (Application.isPlaying)
			{
				Gizmos.color = ((m_state == State.Inside) ? Color.red : Color.green);
			}
			else
			{
				Gizmos.color = Color.white;
			}
			m_shape.DrawGizmo();
		}
	}
}
