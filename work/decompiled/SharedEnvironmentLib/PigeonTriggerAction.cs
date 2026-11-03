using System.Collections;
using UnityEngine;

internal class PigeonTriggerAction : TriggerAction
{
	[SerializeField]
	private Animation m_animation;

	[SerializeField]
	private AnimationClip[] m_enterClips;

	[SerializeField]
	private AnimationClip m_leaveClip;

	private SkinnedMeshRenderer m_skm;

	private TriggerShape m_shape;

	private void Start()
	{
		m_skm = GetComponentInChildren<SkinnedMeshRenderer>();
		m_shape = GetComponent<TriggerShape>();
	}

	public override bool CanEnter()
	{
		return true;
	}

	public override bool CanLeave()
	{
		Plane[] frustumPlanes = TriggerManager.GetFrustumPlanes();
		if (m_shape == null || frustumPlanes == null)
		{
			return true;
		}
		if (!m_shape.Inside(frustumPlanes))
		{
			return true;
		}
		return false;
	}

	public override bool Validate(bool verbose)
	{
		bool result = m_animation != null;
		if (verbose)
		{
		}
		return result;
	}

	public override void Enter()
	{
		int num = Random.Range(0, m_enterClips.Length);
		string text = m_enterClips[num].name;
		m_animation.Play(text);
		StartCoroutine(WaitAndCallback(text));
	}

	private IEnumerator WaitAndCallback(string _clipName)
	{
		while (m_animation.IsPlaying(_clipName))
		{
			yield return new WaitForEndOfFrame();
		}
		m_skm.enabled = false;
	}

	public override void Leave()
	{
		string animation = m_leaveClip.name;
		m_animation.Play(animation);
		m_animation.Sample();
		StopAllCoroutines();
		m_skm.enabled = true;
	}
}
