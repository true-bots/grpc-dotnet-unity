#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.IO;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Math.EC.Rfc7748;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Security;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.IO;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters
{
	public sealed class X448PrivateKeyParameters
		: AsymmetricKeyParameter
	{
		public static readonly int KeySize = X448.ScalarSize;
		public static readonly int SecretSize = X448.PointSize;

		private readonly byte[] data = new byte[KeySize];

		public X448PrivateKeyParameters(SecureRandom random)
			: base(true)
		{
			X448.GeneratePrivateKey(random, data);
		}

		public X448PrivateKeyParameters(byte[] buf)
			: this(Validate(buf), 0)
		{
		}

		public X448PrivateKeyParameters(byte[] buf, int off)
			: base(true)
		{
			Array.Copy(buf, off, data, 0, KeySize);
		}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
        public X448PrivateKeyParameters(ReadOnlySpan<byte> buf)
            : base(true)
        {
            if (buf.Length != KeySize)
                throw new ArgumentException("must have length " + KeySize, nameof(buf));

            buf.CopyTo(data);
        }
#endif

		public X448PrivateKeyParameters(Stream input)
			: base(true)
		{
			if (KeySize != Streams.ReadFully(input, data))
				throw new EndOfStreamException("EOF encountered in middle of X448 private key");
		}

		public void Encode(byte[] buf, int off)
		{
			Array.Copy(data, 0, buf, off, KeySize);
		}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
        public void Encode(Span<byte> buf)
        {
            data.CopyTo(buf);
        }
#endif

		public byte[] GetEncoded()
		{
			return Arrays.Clone(data);
		}

		public X448PublicKeyParameters GeneratePublicKey()
		{
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
            Span<byte> publicKey = stackalloc byte[X448.PointSize];
            X448.GeneratePublicKey(data, publicKey);
            return new X448PublicKeyParameters(publicKey);
#else
			byte[] publicKey = new byte[X448.PointSize];
			X448.GeneratePublicKey(data, 0, publicKey, 0);
			return new X448PublicKeyParameters(publicKey, 0);
#endif
		}

		public void GenerateSecret(X448PublicKeyParameters publicKey, byte[] buf, int off)
		{
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
            GenerateSecret(publicKey, buf.AsSpan(off));
#else
			byte[] encoded = new byte[X448.PointSize];
			publicKey.Encode(encoded, 0);
			if (!X448.CalculateAgreement(data, 0, encoded, 0, buf, off))
				throw new InvalidOperationException("X448 agreement failed");
#endif
		}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
        public void GenerateSecret(X448PublicKeyParameters publicKey, Span<byte> buf)
        {
            Span<byte> encoded = stackalloc byte[X448.PointSize];
            publicKey.Encode(encoded);
            if (!X448.CalculateAgreement(data, encoded, buf))
                throw new InvalidOperationException("X448 agreement failed");
        }
#endif

		private static byte[] Validate(byte[] buf)
		{
			if (buf.Length != KeySize)
				throw new ArgumentException("must have length " + KeySize, nameof(buf));

			return buf;
		}
	}
}
#pragma warning restore
#endif