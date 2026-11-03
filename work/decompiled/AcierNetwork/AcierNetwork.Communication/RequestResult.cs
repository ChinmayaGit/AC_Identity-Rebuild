namespace AcierNetwork.Communication;

public class RequestResult
{
	public enum Status
	{
		InProgress,
		Suceeded,
		Failed
	}

	private float m_progress;

	private Status m_status;

	public float Progress
	{
		get
		{
			return m_progress;
		}
		set
		{
			m_progress = value;
		}
	}

	public Status RequestStatus
	{
		get
		{
			return m_status;
		}
		private set
		{
			m_status = value;
			if (m_status != Status.InProgress)
			{
				m_progress = 1f;
			}
		}
	}

	public bool IsCompleted => RequestStatus != Status.InProgress;

	public bool Suceeded => RequestStatus == Status.Suceeded;

	public bool Failed => RequestStatus == Status.Failed;

	public RequestResult(Status _status = Status.InProgress, float _progress = 0f)
	{
		m_progress = _progress;
		RequestStatus = _status;
	}

	public void SetSuccess()
	{
		RequestStatus = Status.Suceeded;
	}

	public void SetFailed()
	{
		RequestStatus = Status.Failed;
	}

	public override string ToString()
	{
		return string.Format("{0}: {1}, {2}", GetType().Name, RequestStatus, m_progress.ToString("F4"));
	}
}
