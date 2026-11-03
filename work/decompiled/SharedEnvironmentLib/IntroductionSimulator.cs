using SharedBaseLib.Core;
using UnityEngine;

public class IntroductionSimulator : PipelineBehaviour
{
	private void Start()
	{
		BBList<string> bBList = new BBList<string>();
		Animation component = GetComponent<Animation>();
		foreach (AnimationState item in component)
		{
			bBList.Add(item.name);
		}
		if (bBList.Count > 0)
		{
			component.Play(bBList[0]);
		}
		for (int i = 1; i < bBList.Count; i++)
		{
			component.PlayQueued(bBList[i]);
		}
	}
}
