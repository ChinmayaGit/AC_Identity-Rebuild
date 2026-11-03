using System.Security.Cryptography.X509Certificates;

namespace BB.Mobile.Net;

public delegate CertificatePolicyErrors OverrideCertificatePolicyErrors(string hostname, X509Certificate certificate, X509Chain chain, CertificatePolicyErrors certificatePolicyErrors);
