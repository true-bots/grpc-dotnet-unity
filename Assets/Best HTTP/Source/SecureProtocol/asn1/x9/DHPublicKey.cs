#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X9
{
	public class DHPublicKey
		: Asn1Encodable
	{
		readonly DerInteger y;

		public static DHPublicKey GetInstance(Asn1TaggedObject obj, bool isExplicit)
		{
			return GetInstance(DerInteger.GetInstance(obj, isExplicit));
		}

		public static DHPublicKey GetInstance(object obj)
		{
			if (obj == null || obj is DHPublicKey)
			{
				return (DHPublicKey)obj;
			}

			if (obj is DerInteger)
			{
				return new DHPublicKey((DerInteger)obj);
			}

			throw new ArgumentException("Invalid DHPublicKey: " + Platform.GetTypeName(obj), "obj");
		}

		public DHPublicKey(DerInteger y)
		{
			if (y == null)
			{
				throw new ArgumentNullException("y");
			}

			this.y = y;
		}

		public DerInteger Y
		{
			get { return y; }
		}

		public override Asn1Object ToAsn1Object()
		{
			return y;
		}
	}
}
#pragma warning restore
#endif