using UnityEngine;

[ExecuteInEditMode]
internal class TriggerTransform : MonoBehaviour
{
	protected void OnEnable()
	{
		ProximityTriggerTick.Instance.Register(this);
	}

	protected void OnDisable()
	{
		ProximityTriggerTick.Instance.Unregister(this);
	}
}
