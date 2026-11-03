using System;
using System.Collections.Generic;
using AcierProtocol;
using Assets.Scripts.Audio;
using Assets.Scripts.Mission;
using Assets.Scripts.Mission.Camera;
using UbiCore.Utils;
using UbiCore.Utils.Extensions;
using UnityEngine;

public class CameraStateIntroduction : CameraStateBase
{
	private Animation m_animation;

	private Camera m_camera;

	private BarrierManager m_cachedBarrierMgr;

	public override bool DrawAvatar => false;

	public Introduction Introduction
	{
		get
		{
			if (m_animation != null)
			{
				return m_animation.GetComponent<Introduction>();
			}
			return null;
		}
	}

	public override AcierCameraController.ECameraState StateType => AcierCameraController.ECameraState.Introduction;

	public override void CopyFrom(ICameraState _src)
	{
	}

	public static bool HasSceneIntroAnimation()
	{
		return FindSceneIntroAnimation() != null;
	}

	public static GameObject FindSceneIntroAnimation()
	{
		GameObject gameObject = GameObject.FindGameObjectWithTag("Introduction");
		if (gameObject == null)
		{
			gameObject = GameObject.Find("EnvScenario/Introduction");
		}
		return gameObject;
	}

	public override void StartState(ICameraState _previous)
	{
		base.StartState(_previous);
		GameObject gameObject = FindSceneIntroAnimation();
		m_animation = gameObject.GetComponent<Animation>();
		m_camera = gameObject.GetComponent<Camera>();
		if (m_camera != null)
		{
			m_camera.enabled = false;
		}
		List<string> list = new List<string>();
		foreach (AnimationState item in m_animation)
		{
			list.Add(item.name);
		}
		if (list.Count > 0)
		{
			m_animation.Play(list[0]);
		}
		for (int i = 1; i < list.Count; i++)
		{
			m_animation.PlayQueued(list[i]);
		}
		AnimationEventListener component = gameObject.GetComponent<AnimationEventListener>();
		component.OnEvent = (AnimationEventListener.OnEventDelegate)Delegate.Combine(component.OnEvent, new AnimationEventListener.OnEventDelegate(OnAnimationEvent));
		AudioManager.Instance.EventManager.OnRegionIntroStarted();
		m_cachedBarrierMgr = UnityEngine.Object.FindObjectOfType<BarrierManager>();
		if (m_cachedBarrierMgr != null)
		{
			m_cachedBarrierMgr.SetVisible(_visible: false);
		}
	}

	public override void UpdateState()
	{
		base.UpdateState();
		if (!m_animation.isPlaying)
		{
			UbiCore.Utils.Singleton<MissionManager>.Instance.Go();
		}
		m_outputPosition = m_animation.transform.position;
		m_outputRotation = m_animation.transform.rotation;
		if (m_camera != null)
		{
			m_outputFieldOfView = m_camera.fieldOfView;
		}
	}

	public override void EndState()
	{
		if (LogicMain.PlayerEntity != null)
		{
			ObjectFade objectFade = LogicMain.PlayerEntity.GameObject.AddComponentIfNotExist<ObjectFade>();
			objectFade.FadeMode = EFadeMode.Fancy_EFadeMode;
			objectFade.FadeDuration = 2f;
			objectFade.OnFadeDoneCallback = ObjectFadeCallbacks.OnFadeFinishedDestroySelf;
			objectFade.FadeIn();
		}
		if (m_cachedBarrierMgr != null)
		{
			m_cachedBarrierMgr.SetVisible(_visible: true);
		}
	}

	public void OnAnimationEvent(string _desc)
	{
		Debug.Log("CameraStateIntroduction::OnAnimationEvent: " + _desc);
	}

	public void OnAbortedIntroduction()
	{
		m_animation.Stop();
		Introduction introduction = Introduction;
		if (introduction != null)
		{
			introduction.Abort();
		}
	}
}
