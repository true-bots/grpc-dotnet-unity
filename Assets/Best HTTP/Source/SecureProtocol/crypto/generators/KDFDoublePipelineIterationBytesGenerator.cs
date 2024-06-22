#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Macs;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Math;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Generators
{
	public sealed class KdfDoublePipelineIterationBytesGenerator
		: IMacDerivationFunction
	{
		// fields set by the constructor       
		readonly IMac prf;
		readonly int h;

		// fields set by init
		byte[] fixedInputData;

		int maxSizeExcl;

		// ios is i defined as an octet string (the binary representation)
		byte[] ios;
		bool useCounter;

		// operational
		int generatedBytes;

		// k is used as buffer for all K(i) values
		byte[] a;
		byte[] k;

		public KdfDoublePipelineIterationBytesGenerator(IMac prf)
		{
			this.prf = prf;
			h = prf.GetMacSize();
			a = new byte[h];
			k = new byte[h];
		}

		public void Init(IDerivationParameters parameters)
		{
			if (!(parameters is KdfDoublePipelineIterationParameters dpiParams))
			{
				throw new ArgumentException("Wrong type of arguments given");
			}

			// --- init mac based PRF ---

			prf.Init(new KeyParameter(dpiParams.Ki));

			// --- set arguments ---

			fixedInputData = dpiParams.FixedInputData;

			int r = dpiParams.R;
			ios = new byte[r / 8];

			if (dpiParams.UseCounter)
			{
				// this is more conservative than the spec
				BigInteger maxSize = BigInteger.One.ShiftLeft(r).Multiply(BigInteger.ValueOf(h));
				maxSizeExcl = maxSize.BitLength > 31 ? int.MaxValue : maxSize.IntValueExact;
			}
			else
			{
				maxSizeExcl = int.MaxValue;
			}

			useCounter = dpiParams.UseCounter;

			// --- set operational state ---

			generatedBytes = 0;
		}

		void GenerateNext()
		{
			if (generatedBytes == 0)
			{
				// --- step 4 ---
				prf.BlockUpdate(fixedInputData, 0, fixedInputData.Length);
				prf.DoFinal(a, 0);
			}
			else
			{
				// --- step 5a ---
				prf.BlockUpdate(a, 0, a.Length);
				prf.DoFinal(a, 0);
			}

			// --- step 5b ---
			prf.BlockUpdate(a, 0, a.Length);

			if (useCounter)
			{
				int i = generatedBytes / h + 1;

				// encode i into counter buffer
				switch (ios.Length)
				{
					case 4:
						ios[0] = (byte)(i >> 24);
						// fall through
						goto case 3;
					case 3:
						ios[ios.Length - 3] = (byte)(i >> 16);
						// fall through
						goto case 2;
					case 2:
						ios[ios.Length - 2] = (byte)(i >> 8);
						// fall through
						goto case 1;
					case 1:
						ios[ios.Length - 1] = (byte)i;
						break;
					default:
						throw new InvalidOperationException("Unsupported size of counter i");
				}

				prf.BlockUpdate(ios, 0, ios.Length);
			}

			prf.BlockUpdate(fixedInputData, 0, fixedInputData.Length);
			prf.DoFinal(k, 0);
		}

		public IDigest Digest
		{
			get { return (prf as HMac)?.GetUnderlyingDigest(); }
		}

		public int GenerateBytes(byte[] output, int outOff, int length)
		{
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
            return GenerateBytes(output.AsSpan(outOff, length));
#else
			if (generatedBytes >= maxSizeExcl - length)
			{
				throw new DataLengthException("Current KDFCTR may only be used for " + maxSizeExcl + " bytes");
			}

			int toGenerate = length;
			int posInK = generatedBytes % h;
			if (posInK != 0)
			{
				// copy what is left in the currentT (1..hash
				int toCopy = System.Math.Min(h - posInK, toGenerate);
				Array.Copy(k, posInK, output, outOff, toCopy);
				generatedBytes += toCopy;
				toGenerate -= toCopy;
				outOff += toCopy;
			}

			while (toGenerate > 0)
			{
				GenerateNext();
				int toCopy = System.Math.Min(h, toGenerate);
				Array.Copy(k, 0, output, outOff, toCopy);
				generatedBytes += toCopy;
				toGenerate -= toCopy;
				outOff += toCopy;
			}

			return length;
#endif
		}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
        public int GenerateBytes(Span<byte> output)
        {
            int length = output.Length;
            if (generatedBytes >= maxSizeExcl - length)
                throw new DataLengthException("Current KDFCTR may only be used for " + maxSizeExcl + " bytes");

            int posInK = generatedBytes % h;
            if (posInK != 0)
            {
                // copy what is left in the currentT (1..hash
                GenerateNext();
                int toCopy = System.Math.Min(h - posInK, output.Length);
                k.AsSpan(posInK, toCopy).CopyTo(output);
                generatedBytes += toCopy;
                output = output[toCopy..];
            }

            while (!output.IsEmpty)
            {
                GenerateNext();
                int toCopy = System.Math.Min(h, output.Length);
                k.AsSpan(0, toCopy).CopyTo(output);
                generatedBytes += toCopy;
                output = output[toCopy..];
            }

            return length;
        }
#endif

		public IMac Mac
		{
			get { return prf; }
		}
	}
}
#pragma warning restore
#endif