using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace BB.Mobile.Net;

public static class CertificateValidator
{
	private static readonly object mTrustSentinel;

	private static volatile bool mTrustAll;

	private static InterlockedObject<SynchronizeDelegate<InquireTrustCertificate>> mTrustCallback;

	private static InterlockedObject<OverrideCertificatePolicyErrors> mPolicyErrorOverride;

	private static InterlockedObject<HostPinsetStore> mHostPinsets;

	static CertificateValidator()
	{
		mTrustSentinel = new object();
		mTrustCallback = new InterlockedObject<SynchronizeDelegate<InquireTrustCertificate>>();
		mPolicyErrorOverride = new InterlockedObject<OverrideCertificatePolicyErrors>();
		mHostPinsets = new InterlockedObject<HostPinsetStore>();
		ServicePointManager.ServerCertificateValidationCallback = HandleCertificateValidation;
	}

	public static void Enregister(SynchronizeDelegate<InquireTrustCertificate> trustCallback)
	{
		if (mTrustCallback.CompareExchange(trustCallback, null) != null)
		{
			throw new InvalidOperationException("A certificate trust callback is already registered");
		}
	}

	public static void Deregister(SynchronizeDelegate<InquireTrustCertificate> trustCallback)
	{
		if (mTrustCallback.CompareExchange(null, trustCallback) != trustCallback)
		{
			throw new InvalidOperationException("The certificate trust callback is not the currently registered callback");
		}
	}

	public static void Enregister(OverrideCertificatePolicyErrors errorsOverrideCallback)
	{
		if (mPolicyErrorOverride.CompareExchange(errorsOverrideCallback, null) != null)
		{
			throw new InvalidOperationException("A certificate trust callback is already registered");
		}
	}

	public static void Deregister(OverrideCertificatePolicyErrors errorsOverrideCallback)
	{
		if (mPolicyErrorOverride.CompareExchange(null, errorsOverrideCallback) != errorsOverrideCallback)
		{
			throw new InvalidOperationException("The certificate trust callback is not the currently registered callback");
		}
	}

	public static void Enregister(HostPinsetStore store)
	{
		if (mHostPinsets.CompareExchange(store, null) != null)
		{
			throw new InvalidOperationException("A host pinset store is already registered");
		}
	}

	public static void Deregister(HostPinsetStore store)
	{
		if (mHostPinsets.CompareExchange(null, store) != store)
		{
			throw new InvalidOperationException("The host pinset store is not currently registered");
		}
	}

	private static bool HandleCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
	{
		if (mTrustAll)
		{
			return true;
		}
		string text = ((sender is WebRequest) ? ((WebRequest)sender).RequestUri.Host : new Uri((string)sender).Host);
		CertificatePolicyErrors certificatePolicyErrors = (CertificatePolicyErrors)sslPolicyErrors;
		HostPinsetStore value = mHostPinsets.Value;
		certificatePolicyErrors = (CertificatePolicyErrors)((int)certificatePolicyErrors | ((value != null && !value.Verify(text, certificate)) ? 128 : 0));
		if (certificatePolicyErrors == CertificatePolicyErrors.None)
		{
			return true;
		}
		OverrideCertificatePolicyErrors value2 = mPolicyErrorOverride.Value;
		if (value2 != null)
		{
			CertificatePolicyErrors certificatePolicyErrors2 = value2(text, certificate, chain, certificatePolicyErrors);
			if (certificatePolicyErrors2 != CertificatePolicyErrors.None)
			{
				certificatePolicyErrors &= ~certificatePolicyErrors2;
				if (certificatePolicyErrors == CertificatePolicyErrors.None)
				{
					return true;
				}
			}
		}
		lock (mTrustSentinel)
		{
			if (!mTrustAll)
			{
				SynchronizeDelegate<InquireTrustCertificate> value3 = mTrustCallback.Value;
				if (value3 != null)
				{
					AsyncValueResult<int, string> asyncValueResult = new AsyncValueResult<int, string>(text);
					value3.Invoke(asyncValueResult, text, certificate, chain, certificatePolicyErrors);
					if (!asyncValueResult.WaitForCompletion())
					{
						throw new TargetInvocationException(asyncValueResult.Exception);
					}
					mTrustAll = asyncValueResult.Result != 0;
				}
			}
			return mTrustAll;
		}
	}

	[Conditional("TRACE")]
	private static void TraceTrust(string hostname, X509Certificate certificate, X509Chain chain, CertificatePolicyErrors certificatePolicyErrors, bool trust)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendFormat("Connection {0} for {1}: {2}", trust ? "trust overridden" : "untrusted and REJECTED", hostname, certificatePolicyErrors);
		stringBuilder.AppendLine();
		X509ChainPolicy chainPolicy = chain.ChainPolicy;
		stringBuilder.AppendFormat("Chain Policy -> Revocation Mode: {0}; Revocation Flags: {1}; Verification Flags: {2}", chainPolicy.RevocationMode, chainPolicy.RevocationFlag, chainPolicy.VerificationFlags);
		stringBuilder.AppendLine();
		stringBuilder.Append("Chain Status: ");
		stringBuilder.AppendLine();
		stringBuilder.Append("SHA1 Fingerprint: ");
		stringBuilder.Append(certificate.GetCertHashString());
		if ((certificatePolicyErrors & CertificatePolicyErrors.RemoteCertificateChainErrors) != 0)
		{
			foreach (X509ChainElement chainElement in chain.ChainElements)
			{
				stringBuilder.AppendLine();
				X509Certificate2 certificate2 = chainElement.Certificate;
				stringBuilder.AppendFormat("Certificate for {0}: ", certificate2.GetNameInfo(X509NameType.SimpleName, forIssuer: false));
			}
		}
		else
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendFormat("Certificate for {0}: No Chain Errors", certificate.Subject);
		}
	}

	[Conditional("TRACE")]
	private static void AppendChainStatus(this StringBuilder stringBuilder, X509ChainStatus[] chainStatus)
	{
		int num = chainStatus?.Length ?? 0;
		if (num > 0)
		{
			stringBuilder.Append(chainStatus[0].Status);
			for (int i = 1; i < num; i++)
			{
				stringBuilder.Append(", ");
				stringBuilder.Append(chainStatus[i].Status);
			}
		}
		else
		{
			stringBuilder.Append(X509ChainStatusFlags.NoError);
		}
	}
}
