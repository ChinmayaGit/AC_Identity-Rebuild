using UnityEngine;

internal abstract class Trigger : MonoBehaviour
{
	protected enum State
	{
		Inside,
		Outside
	}

	protected TriggerAction m_action;

	protected State m_state = State.Outside;

	protected void OnEnable()
	{
		m_action = GetComponent<TriggerAction>();
		TriggerManager.Register(this);
	}

	protected void OnDisable()
	{
		TriggerManager.Unregister(this);
	}

	public virtual bool Validate(bool verbose)
	{
		bool result = m_action != null;
		if (verbose)
		{
		}
		return result;
	}

	protected void TryEnter()
	{
		if (m_action.CanEnter())
		{
			m_action.Enter();
			m_state = State.Inside;
		}
	}

	protected void TryLeave()
	{
		if (m_action.CanLeave())
		{
			m_action.Leave();
			m_state = State.Outside;
		}
	}

	public abstract void RegisterTick();

	public abstract void UnregisterTick();
}
