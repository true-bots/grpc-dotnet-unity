#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.Text;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Security;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Pkix
{
	/// <summary>
	/// Summary description for PkixCertPathValidatorResult.
	/// </summary>
	public class PkixCertPathValidatorResult
		//: ICertPathValidatorResult
	{
		TrustAnchor trustAnchor;
		PkixPolicyNode policyTree;
		AsymmetricKeyParameter subjectPublicKey;

		public PkixPolicyNode PolicyTree
		{
			get { return policyTree; }
		}

		public TrustAnchor TrustAnchor
		{
			get { return trustAnchor; }
		}

		public AsymmetricKeyParameter SubjectPublicKey
		{
			get { return subjectPublicKey; }
		}

		public PkixCertPathValidatorResult(TrustAnchor trustAnchor, PkixPolicyNode policyTree,
			AsymmetricKeyParameter subjectPublicKey)
		{
			if (trustAnchor == null)
			{
				throw new ArgumentNullException(nameof(trustAnchor));
			}

			if (subjectPublicKey == null)
			{
				throw new ArgumentNullException(nameof(subjectPublicKey));
			}

			this.trustAnchor = trustAnchor;
			this.policyTree = policyTree;
			this.subjectPublicKey = subjectPublicKey;
		}

		public object Clone()
		{
			return new PkixCertPathValidatorResult(TrustAnchor, PolicyTree, SubjectPublicKey);
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("PKIXCertPathValidatorResult: [");
			sb.Append("  Trust Anchor: ").Append(TrustAnchor).AppendLine();
			sb.Append("  Policy Tree: ").Append(PolicyTree).AppendLine();
			sb.Append("  Subject Public Key: ").Append(SubjectPublicKey).AppendLine();
			return sb.ToString();
		}
	}
}
#pragma warning restore
#endif