using System;

namespace BB.Mobile.Net;

[Flags]
public enum CertificatePolicyErrors
{
	None = 0,
	RemoteCertificateNotAvailable = 1,
	RemoteCertificateNameMismatch = 2,
	RemoteCertificateChainErrors = 4,
	PinsetRestricted = 0x80
}
