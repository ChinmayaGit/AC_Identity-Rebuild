using UnityEngine;

internal abstract class TriggerAction : MonoBehaviour
{
	public abstract bool CanEnter();

	public abstract bool CanLeave();

	public abstract bool Validate(bool verbose);

	public abstract void Enter();

	public abstract void Leave();
}
