#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Tls.Crypto.Impl.BC
{
	sealed class BcTlsHash
		: TlsHash
	{
		readonly BcTlsCrypto m_crypto;
		readonly int m_cryptoHashAlgorithm;
		readonly IDigest m_digest;

		internal BcTlsHash(BcTlsCrypto crypto, int cryptoHashAlgorithm)
			: this(crypto, cryptoHashAlgorithm, crypto.CreateDigest(cryptoHashAlgorithm))
		{
		}

		BcTlsHash(BcTlsCrypto crypto, int cryptoHashAlgorithm, IDigest digest)
		{
			m_crypto = crypto;
			m_cryptoHashAlgorithm = cryptoHashAlgorithm;
			m_digest = digest;
		}

		public void Update(byte[] data, int offSet, int length)
		{
			m_digest.BlockUpdate(data, offSet, length);
		}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
        public void Update(ReadOnlySpan<byte> input)
        {
            m_digest.BlockUpdate(input);
        }
#endif

		public byte[] CalculateHash()
		{
			byte[] rv = new byte[m_digest.GetDigestSize()];
			m_digest.DoFinal(rv, 0);
			return rv;
		}

		public TlsHash CloneHash()
		{
			IDigest clone = m_crypto.CloneDigest(m_cryptoHashAlgorithm, m_digest);
			return new BcTlsHash(m_crypto, m_cryptoHashAlgorithm, clone);
		}

		public void Reset()
		{
			m_digest.Reset();
		}
	}
}
#pragma warning restore
#endif