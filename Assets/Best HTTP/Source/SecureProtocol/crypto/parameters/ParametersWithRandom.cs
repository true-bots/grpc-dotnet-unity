#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Security;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters
{
	public class ParametersWithRandom
		: ICipherParameters
	{
		readonly ICipherParameters m_parameters;
		readonly SecureRandom m_random;

		public ParametersWithRandom(ICipherParameters parameters)
			: this(parameters, CryptoServicesRegistrar.GetSecureRandom())
		{
		}

		public ParametersWithRandom(ICipherParameters parameters, SecureRandom random)
		{
			if (parameters == null)
			{
				throw new ArgumentNullException(nameof(parameters));
			}

			if (random == null)
			{
				throw new ArgumentNullException(nameof(random));
			}

			m_parameters = parameters;
			m_random = random;
		}

		public ICipherParameters Parameters
		{
			get { return m_parameters; }
		}

		public SecureRandom Random
		{
			get { return m_random; }
		}
	}
}
#pragma warning restore
#endif