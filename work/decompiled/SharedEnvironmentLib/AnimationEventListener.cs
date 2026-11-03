using UnityEngine;

public class AnimationEventListener : MonoBehaviour
{
	public delegate void OnEventDelegate(string _desc);

	public OnEventDelegate OnEvent { get; set; }

	public void OnAnimationEvent(string _desc)
	{
		if (OnEvent != null)
		{
			OnEvent(_desc);
		}
	}
}
