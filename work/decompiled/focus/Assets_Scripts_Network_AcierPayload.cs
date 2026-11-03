using System;
using System.IO;
using AcierProtocol;
using BB.Mobile.Net;

namespace Assets.Scripts.Network;

internal class AcierPayload : IPayload
{
	private const string m_contentType = "application/x-protobuf";

	private readonly Request m_request;

	private static readonly AcierProtocolSerializer g_serializer = new AcierProtocolSerializer();

	public string ContentType
	{
		get
		{
			return "application/x-protobuf";
		}
		private set
		{
			throw new ArgumentNullException();
		}
	}

	public Response response { get; private set; }

	public AcierPayload()
	{
		m_request = null;
	}

	public AcierPayload(Request acierRequest)
	{
		if (object.ReferenceEquals(acierRequest, null))
		{
			throw new ArgumentNullException();
		}
		m_request = acierRequest;
	}

	public void Serialize(Stream destination)
	{
		if (object.ReferenceEquals(m_request, null))
		{
			throw new ArgumentNullException();
		}
		if (!object.ReferenceEquals(destination, null))
		{
			g_serializer.Serialize(destination, m_request);
		}
	}

	public void Generate(Stream source)
	{
		if (!object.ReferenceEquals(source, null))
		{
			response = (Response)g_serializer.Deserialize(source, null, typeof(Response));
		}
	}
}
