using UnityEngine;

public class CloneReflection : PipelineBehaviour
{
	private void Start()
	{
		Clone();
	}

	public void Clone()
	{
		GameObject gameObject = Object.Instantiate(base.gameObject);
		gameObject.transform.localPosition = transform.position;
		gameObject.transform.localRotation = transform.rotation;
		gameObject.transform.localScale = transform.lossyScale;
		gameObject.layer = LayerMask.NameToLayer("Reflection");
		GameObject gameObject2 = GameObject.Find("EnvScenario/Reflection");
		if (gameObject2 == null)
		{
			gameObject2 = new GameObject("Reflection");
			GameObject gameObject3 = GameObject.Find("EnvScenario");
			if (gameObject3 == null)
			{
				gameObject3 = new GameObject("EnvScenario");
			}
			gameObject2.transform.parent = gameObject3.transform;
		}
		gameObject.transform.parent = gameObject2.transform;
		Object.Destroy(this);
	}
}
