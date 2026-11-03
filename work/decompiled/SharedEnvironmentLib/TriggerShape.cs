using UnityEngine;

internal abstract class TriggerShape : MonoBehaviour
{
	public abstract Bounds bounds { get; protected set; }

	public abstract bool Inside(Vector3 _worldPos);

	public abstract bool Inside(Plane[] _planes);

	public abstract void DrawGizmo();
}
