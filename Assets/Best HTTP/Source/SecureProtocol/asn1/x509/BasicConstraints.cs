#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Math;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X509
{
	public class BasicConstraints
		: Asn1Encodable
	{
		public static BasicConstraints GetInstance(Asn1TaggedObject obj, bool explicitly)
		{
			return GetInstance(Asn1Sequence.GetInstance(obj, explicitly));
		}

		public static BasicConstraints GetInstance(object obj)
		{
			if (obj is BasicConstraints)
			{
				return (BasicConstraints)obj;
			}

			if (obj is X509Extension)
			{
				return GetInstance(X509Extension.ConvertValueToObject((X509Extension)obj));
			}

			if (obj == null)
			{
				return null;
			}

			return new BasicConstraints(Asn1Sequence.GetInstance(obj));
		}

		public static BasicConstraints FromExtensions(X509Extensions extensions)
		{
			return GetInstance(X509Extensions.GetExtensionParsedValue(extensions, X509Extensions.BasicConstraints));
		}

		readonly DerBoolean cA;
		readonly DerInteger pathLenConstraint;

		BasicConstraints(
			Asn1Sequence seq)
		{
			if (seq.Count > 0)
			{
				if (seq[0] is DerBoolean)
				{
					cA = DerBoolean.GetInstance(seq[0]);
				}
				else
				{
					pathLenConstraint = DerInteger.GetInstance(seq[0]);
				}

				if (seq.Count > 1)
				{
					if (cA == null)
					{
						throw new ArgumentException("wrong sequence in constructor", "seq");
					}

					pathLenConstraint = DerInteger.GetInstance(seq[1]);
				}
			}
		}

		public BasicConstraints(
			bool cA)
		{
			if (cA)
			{
				this.cA = DerBoolean.True;
			}
		}

		/**
         * create a cA=true object for the given path length constraint.
         *
         * @param pathLenConstraint
         */
		public BasicConstraints(
			int pathLenConstraint)
		{
			cA = DerBoolean.True;
			this.pathLenConstraint = new DerInteger(pathLenConstraint);
		}

		public bool IsCA()
		{
			return cA != null && cA.IsTrue;
		}

		public BigInteger PathLenConstraint
		{
			get { return pathLenConstraint == null ? null : pathLenConstraint.Value; }
		}

		/**
         * Produce an object suitable for an Asn1OutputStream.
         * <pre>
         * BasicConstraints := Sequence {
         *    cA                  Boolean DEFAULT FALSE,
         *    pathLenConstraint   Integer (0..MAX) OPTIONAL
         * }
         * </pre>
         */
		public override Asn1Object ToAsn1Object()
		{
			Asn1EncodableVector v = new Asn1EncodableVector(2);
			v.AddOptional(cA,
				pathLenConstraint); // yes some people actually do this when cA is false...
			return new DerSequence(v);
		}

		public override string ToString()
		{
			if (pathLenConstraint == null)
			{
				return "BasicConstraints: isCa(" + IsCA() + ")";
			}

			return "BasicConstraints: isCa(" + IsCA() + "), pathLenConstraint = " + pathLenConstraint.Value;
		}
	}
}
#pragma warning restore
#endif