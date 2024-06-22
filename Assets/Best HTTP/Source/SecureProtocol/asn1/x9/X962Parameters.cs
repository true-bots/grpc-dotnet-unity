#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X9
{
	public class X962Parameters
		: Asn1Encodable, IAsn1Choice
	{
		readonly Asn1Object _params;

		public static X962Parameters GetInstance(
			object obj)
		{
			if (obj == null || obj is X962Parameters)
			{
				return (X962Parameters)obj;
			}

			if (obj is Asn1Object)
			{
				return new X962Parameters((Asn1Object)obj);
			}

			if (obj is byte[])
			{
				try
				{
					return new X962Parameters(Asn1Object.FromByteArray((byte[])obj));
				}
				catch (Exception e)
				{
					throw new ArgumentException("unable to parse encoded data: " + e.Message, e);
				}
			}

			throw new ArgumentException("unknown object in getInstance()");
		}

		public X962Parameters(
			X9ECParameters ecParameters)
		{
			_params = ecParameters.ToAsn1Object();
		}

		public X962Parameters(
			DerObjectIdentifier namedCurve)
		{
			_params = namedCurve;
		}

		public X962Parameters(
			Asn1Null obj)
		{
			_params = obj;
		}

		X962Parameters(Asn1Object obj)
		{
			_params = obj;
		}

		public bool IsNamedCurve
		{
			get { return _params is DerObjectIdentifier; }
		}

		public bool IsImplicitlyCA
		{
			get { return _params is Asn1Null; }
		}

		public Asn1Object Parameters
		{
			get { return _params; }
		}

		/**
         * Produce an object suitable for an Asn1OutputStream.
         * <pre>
         * Parameters ::= CHOICE {
         *    ecParameters ECParameters,
         *    namedCurve   CURVES.&amp;id({CurveNames}),
         *    implicitlyCA Null
         * }
         * </pre>
         */
		public override Asn1Object ToAsn1Object()
		{
			return _params;
		}
	}
}
#pragma warning restore
#endif