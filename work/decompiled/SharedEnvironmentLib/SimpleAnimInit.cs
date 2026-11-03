using UnityEngine;

public class SimpleAnimInit : MonoBehaviour
{
	public float AnimOffset;

	public float AnimSpeed = 1f;

	private void Start()
	{
		foreach (AnimationState item in GetComponent<Animation>())
		{
			item.time = AnimOffset;
			item.speed = AnimSpeed;
		}
	}
}
