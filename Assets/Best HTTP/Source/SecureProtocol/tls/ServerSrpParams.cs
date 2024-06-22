#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.IO;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Math;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Tls
{
	public sealed class ServerSrpParams
	{
		BigInteger m_N, m_g, m_B;
		byte[] m_s;

		public ServerSrpParams(BigInteger N, BigInteger g, byte[] s, BigInteger B)
		{
			m_N = N;
			m_g = g;
			m_s = Arrays.Clone(s);
			m_B = B;
		}

		public BigInteger B
		{
			get { return m_B; }
		}

		public BigInteger G
		{
			get { return m_g; }
		}

		public BigInteger N
		{
			get { return m_N; }
		}

		public byte[] S
		{
			get { return m_s; }
		}

		/// <summary>Encode this <see cref="ServerSrpParams"/> to a <see cref="Stream"/>.</summary>
		/// <param name="output">the <see cref="Stream"/> to encode to.</param>
		/// <exception cref="IOException"/>
		public void Encode(Stream output)
		{
			TlsSrpUtilities.WriteSrpParameter(m_N, output);
			TlsSrpUtilities.WriteSrpParameter(m_g, output);
			TlsUtilities.WriteOpaque8(m_s, output);
			TlsSrpUtilities.WriteSrpParameter(m_B, output);
		}

		/// <summary>Parse a <see cref="ServerSrpParams"/> from a <see cref="Stream"/>.</summary>
		/// <param name="input">the <see cref="Stream"/> to parse from.</param>
		/// <returns>a <see cref="ServerSrpParams"/> object.</returns>
		/// <exception cref="IOException"/>
		public static ServerSrpParams Parse(Stream input)
		{
			BigInteger N = TlsSrpUtilities.ReadSrpParameter(input);
			BigInteger g = TlsSrpUtilities.ReadSrpParameter(input);
			byte[] s = TlsUtilities.ReadOpaque8(input, 1);
			BigInteger B = TlsSrpUtilities.ReadSrpParameter(input);

			return new ServerSrpParams(N, g, s, B);
		}
	}
}
#pragma warning restore
#endif