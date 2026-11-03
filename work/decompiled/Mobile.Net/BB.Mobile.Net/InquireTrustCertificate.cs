using System.Security.Cryptography.X509Certificates;

namespace BB.Mobile.Net;

public delegate void InquireTrustCertificate(IAsyncValueResult<TrustLifetime> asyncResult, string hostname, X509Certificate certificate, X509Chain chain, CertificatePolicyErrors certificatePolicyErrors);
