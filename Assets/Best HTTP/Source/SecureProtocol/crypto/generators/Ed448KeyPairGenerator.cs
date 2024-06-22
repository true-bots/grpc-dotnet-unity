#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Security;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Generators
{
	public class Ed448KeyPairGenerator
		: IAsymmetricCipherKeyPairGenerator
	{
		SecureRandom random;

		public virtual void Init(KeyGenerationParameters parameters)
		{
			random = parameters.Random;
		}

		public virtual AsymmetricCipherKeyPair GenerateKeyPair()
		{
			Ed448PrivateKeyParameters privateKey = new Ed448PrivateKeyParameters(random);
			Ed448PublicKeyParameters publicKey = privateKey.GeneratePublicKey();
			return new AsymmetricCipherKeyPair(publicKey, privateKey);
		}
	}
}
#pragma warning restore
#endif