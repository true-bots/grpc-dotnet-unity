#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.CryptoPro;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Math;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters
{
	public abstract class Gost3410KeyParameters
		: AsymmetricKeyParameter
	{
		readonly Gost3410Parameters parameters;
		readonly DerObjectIdentifier publicKeyParamSet;

		protected Gost3410KeyParameters(
			bool isPrivate,
			Gost3410Parameters parameters)
			: base(isPrivate)
		{
			this.parameters = parameters;
		}

		protected Gost3410KeyParameters(
			bool isPrivate,
			DerObjectIdentifier publicKeyParamSet)
			: base(isPrivate)
		{
			parameters = LookupParameters(publicKeyParamSet);
			this.publicKeyParamSet = publicKeyParamSet;
		}

		public Gost3410Parameters Parameters
		{
			get { return parameters; }
		}

		public DerObjectIdentifier PublicKeyParamSet
		{
			get { return publicKeyParamSet; }
		}

		// TODO Implement Equals/GetHashCode

		static Gost3410Parameters LookupParameters(
			DerObjectIdentifier publicKeyParamSet)
		{
			if (publicKeyParamSet == null)
			{
				throw new ArgumentNullException("publicKeyParamSet");
			}

			Gost3410ParamSetParameters p = Gost3410NamedParameters.GetByOid(publicKeyParamSet);

			if (p == null)
			{
				throw new ArgumentException("OID is not a valid CryptoPro public key parameter set", "publicKeyParamSet");
			}

			return new Gost3410Parameters(p.P, p.Q, p.A);
		}
	}
}
#pragma warning restore
#endif