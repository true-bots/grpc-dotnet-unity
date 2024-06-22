#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Math;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Bcpg
{
	/// <remarks>Base class for an RSA secret (or priate) key.</remarks>
	public class RsaSecretBcpgKey
		: BcpgObject, IBcpgKey
	{
		readonly MPInteger d, p, q, u;
		readonly BigInteger expP, expQ, crt;

		public RsaSecretBcpgKey(
			BcpgInputStream bcpgIn)
		{
			d = new MPInteger(bcpgIn);
			p = new MPInteger(bcpgIn);
			q = new MPInteger(bcpgIn);
			u = new MPInteger(bcpgIn);

			expP = d.Value.Remainder(p.Value.Subtract(BigInteger.One));
			expQ = d.Value.Remainder(q.Value.Subtract(BigInteger.One));
			crt = BigIntegers.ModOddInverse(p.Value, q.Value);
		}

		public RsaSecretBcpgKey(
			BigInteger d,
			BigInteger p,
			BigInteger q)
		{
			// PGP requires (p < q)
			int cmp = p.CompareTo(q);
			if (cmp >= 0)
			{
				if (cmp == 0)
				{
					throw new ArgumentException("p and q cannot be equal");
				}

				BigInteger tmp = p;
				p = q;
				q = tmp;
			}

			this.d = new MPInteger(d);
			this.p = new MPInteger(p);
			this.q = new MPInteger(q);
			u = new MPInteger(BigIntegers.ModOddInverse(q, p));

			expP = d.Remainder(p.Subtract(BigInteger.One));
			expQ = d.Remainder(q.Subtract(BigInteger.One));
			crt = BigIntegers.ModOddInverse(p, q);
		}

		public BigInteger Modulus
		{
			get { return p.Value.Multiply(q.Value); }
		}

		public BigInteger PrivateExponent
		{
			get { return d.Value; }
		}

		public BigInteger PrimeP
		{
			get { return p.Value; }
		}

		public BigInteger PrimeQ
		{
			get { return q.Value; }
		}

		public BigInteger PrimeExponentP
		{
			get { return expP; }
		}

		public BigInteger PrimeExponentQ
		{
			get { return expQ; }
		}

		public BigInteger CrtCoefficient
		{
			get { return crt; }
		}

		/// <summary>The format, as a string, always "PGP".</summary>
		public string Format
		{
			get { return "PGP"; }
		}

		/// <summary>Return the standard PGP encoding of the key.</summary>
		public override byte[] GetEncoded()
		{
			try
			{
				return base.GetEncoded();
			}
			catch (Exception)
			{
				return null;
			}
		}

		public override void Encode(
			BcpgOutputStream bcpgOut)
		{
			bcpgOut.WriteObjects(d, p, q, u);
		}
	}
}
#pragma warning restore
#endif