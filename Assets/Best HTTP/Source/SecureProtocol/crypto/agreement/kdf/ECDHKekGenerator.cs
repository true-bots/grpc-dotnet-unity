#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X509;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Generators;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Agreement.Kdf
{
	/**
	* X9.63 based key derivation function for ECDH CMS.
	*/
	public sealed class ECDHKekGenerator
		: IDerivationFunction
	{
		readonly IDerivationFunction m_kdf;

		DerObjectIdentifier algorithm;
		int keySize;
		byte[] z;

		public ECDHKekGenerator(IDigest digest)
		{
			m_kdf = new Kdf2BytesGenerator(digest);
		}

		public void Init(IDerivationParameters param)
		{
			DHKdfParameters parameters = (DHKdfParameters)param;

			algorithm = parameters.Algorithm;
			keySize = parameters.KeySize;
			z = parameters.GetZ(); // TODO Clone?
		}

		public IDigest Digest
		{
			get { return m_kdf.Digest; }
		}

		public int GenerateBytes(byte[] outBytes, int outOff, int length)
		{
			Check.OutputLength(outBytes, outOff, length, "output buffer too small");

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
            return GenerateBytes(outBytes.AsSpan(outOff, length));
#else
			// TODO Create an ASN.1 class for this (RFC3278)
			// ECC-CMS-SharedInfo
			DerSequence s = new DerSequence(
				new AlgorithmIdentifier(algorithm, DerNull.Instance),
				new DerTaggedObject(true, 2, new DerOctetString(Pack.UInt32_To_BE((uint)keySize))));

			m_kdf.Init(new KdfParameters(z, s.GetDerEncoded()));

			return m_kdf.GenerateBytes(outBytes, outOff, length);
#endif
		}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
        public int GenerateBytes(Span<byte> output)
        {
            // TODO Create an ASN.1 class for this (RFC3278)
            // ECC-CMS-SharedInfo
            DerSequence s = new DerSequence(
                new AlgorithmIdentifier(algorithm, DerNull.Instance),
                new DerTaggedObject(true, 2, new DerOctetString(Pack.UInt32_To_BE((uint)keySize))));

            m_kdf.Init(new KdfParameters(z, s.GetDerEncoded()));

            return m_kdf.GenerateBytes(output);
        }
#endif
	}
}
#pragma warning restore
#endif