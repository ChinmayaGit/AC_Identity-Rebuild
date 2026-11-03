using Assets.Scripts.Mission.Camera;
using UbiCore.Utils;
using UnityEngine;

public class CameraStateDead : CameraStateOrbit
{
	[SerializeField]
	public DeadValues m_DeadValues = new DeadValues();

	public override AcierCameraController.ECameraState StateType => AcierCameraController.ECameraState.Dead;

	public override void StartState(ICameraState _previous)
	{
		base.StartState(_previous);
		if (m_DeadValues == null)
		{
			m_DeadValues = AcierCameraController.Instance.GlobalValues.m_deadValues;
		}
	}

	public override void CopyFrom(ICameraState _src)
	{
		base.CopyFrom(_src);
		CameraStateDead cameraStateDead = (CameraStateDead)_src;
		m_DeadValues = cameraStateDead.m_DeadValues;
	}

	protected override void CalculateReference()
	{
		m_referenceTargetPosition = AcierCameraController.AgentDataFilter.Position;
		base.CalculateReference();
	}

	protected override void CalculateRotation()
	{
		float num = UbiCore.Utils.Singleton<Timer>.Instance.deltaTime / UbiCore.Utils.Singleton<Timer>.Instance.timeScale;
		m_rotation *= Quaternion.AngleAxis(num * m_DeadValues.m_rotationSpeed, Vector3.up);
		base.CalculateRotation();
	}
}
