using System;
using System.Runtime.Serialization;
using System.Text;

namespace BB.Mobile.Net;

[Serializable]
public class MobileNetException : Exception
{
	public enum EStatus
	{
		Success = 0,
		NameResolutionFailure = 1,
		ConnectFailure = 2,
		ReceiveFailure = 3,
		SendFailure = 4,
		PipelineFailure = 5,
		RequestCanceled = 6,
		ProtocolError = 7,
		ConnectionClosed = 8,
		TrustFailure = 9,
		SecureChannelFailure = 10,
		ServerProtocolViolation = 11,
		KeepAliveFailure = 12,
		Pending = 13,
		Timeout = 14,
		ProxyNameResolutionFailure = 15,
		UnknownError = 16,
		MessageLengthLimitExceeded = 17,
		CacheEntryNotFound = 18,
		RequestProhibitedByCachePolicy = 19,
		RequestProhibitedByProxy = 20,
		SignatureVerificationFailure = 1000
	}

	private const string mSerializationNameStatus = "MNE-Status";

	private readonly EStatus mStatus = EStatus.RequestCanceled;

	public EStatus Status => mStatus;

	public bool PotentialConnectionIssue
	{
		get
		{
			switch (mStatus)
			{
			case EStatus.NameResolutionFailure:
			case EStatus.ConnectFailure:
			case EStatus.ReceiveFailure:
			case EStatus.SendFailure:
			case EStatus.PipelineFailure:
			case EStatus.ConnectionClosed:
			case EStatus.SecureChannelFailure:
			case EStatus.KeepAliveFailure:
			case EStatus.Timeout:
			case EStatus.ProxyNameResolutionFailure:
			case EStatus.UnknownError:
			case EStatus.RequestProhibitedByProxy:
				return true;
			default:
				return false;
			}
		}
	}

	public MobileNetException()
	{
	}

	public MobileNetException(string message)
		: base(message)
	{
	}

	public MobileNetException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	public MobileNetException(string message, Exception innerException, EStatus status)
		: base(message, innerException)
	{
		mStatus = status;
	}

	public MobileNetException(MobileNetException mobileNetException)
		: this(mobileNetException.Message, mobileNetException, mobileNetException.Status)
	{
	}

	protected MobileNetException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
		mStatus = (EStatus)info.GetValue("MNE-Status", mStatus.GetType());
	}

	public override void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		base.GetObjectData(info, context);
		info.AddValue("MNE-Status", mStatus);
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendFormat("{0}: {1}: {2}", GetType().FullName, mStatus, Message);
		if (InnerException != null)
		{
			stringBuilder.AppendFormat(" ---> {0}", InnerException);
		}
		if (StackTrace != null)
		{
			stringBuilder.Append(Environment.NewLine);
			stringBuilder.Append(StackTrace);
		}
		return stringBuilder.ToString();
	}
}
