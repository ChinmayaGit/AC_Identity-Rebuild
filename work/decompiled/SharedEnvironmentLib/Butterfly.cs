using UnityEngine;

public class Butterfly : MonoBehaviour
{
	private Vector3 LastPosition;

	private void Start()
	{
		LastPosition = Vector3.zero;
		Animation component = GetComponent<Animation>();
		component["FlappyWings"].time = Random.Range(0f, component["FlappyWings"].length);
	}

	private void Update()
	{
		if (LastPosition != Vector3.zero)
		{
			Vector3 vector = transform.position - LastPosition;
			transform.LookAt(transform.position - vector);
		}
		LastPosition = transform.position;
	}
}
