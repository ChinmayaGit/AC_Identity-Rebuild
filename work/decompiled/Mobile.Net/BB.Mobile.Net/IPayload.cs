using System.IO;

namespace BB.Mobile.Net;

public interface IPayload
{
	string ContentType { get; }

	void Serialize(Stream destination);

	void Generate(Stream source);
}
