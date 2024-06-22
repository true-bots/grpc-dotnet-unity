#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Prng
{
	class X931Rng
	{
		const long BLOCK64_RESEED_MAX = 1L << (16 - 1);
		const long BLOCK128_RESEED_MAX = 1L << (24 - 1);
		const int BLOCK64_MAX_BITS_REQUEST = 1 << (13 - 1);
		const int BLOCK128_MAX_BITS_REQUEST = 1 << (19 - 1);

		readonly IBlockCipher mEngine;
		readonly IEntropySource mEntropySource;

		readonly byte[] mDT;
		readonly byte[] mI;
		readonly byte[] mR;

		byte[] mV;

		long mReseedCounter = 1;

		/**
		 *
		 * @param engine
		 * @param entropySource
		 */
		internal X931Rng(IBlockCipher engine, byte[] dateTimeVector, IEntropySource entropySource)
		{
			mEngine = engine;
			mEntropySource = entropySource;

			mDT = new byte[engine.GetBlockSize()];

			Array.Copy(dateTimeVector, 0, mDT, 0, mDT.Length);

			mI = new byte[engine.GetBlockSize()];
			mR = new byte[engine.GetBlockSize()];
		}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
        internal int Generate(Span<byte> output, bool predictionResistant)
        {
            int outputLen = output.Length;

            if (mR.Length == 8) // 64 bit block size
            {
                if (mReseedCounter > BLOCK64_RESEED_MAX)
                    return -1;

                if (outputLen > BLOCK64_MAX_BITS_REQUEST / 8)
                    throw new ArgumentException("Number of bits per request limited to " + BLOCK64_MAX_BITS_REQUEST, "output");
            }
            else
            {
                if (mReseedCounter > BLOCK128_RESEED_MAX)
                    return -1;

                if (outputLen > BLOCK128_MAX_BITS_REQUEST / 8)
                    throw new ArgumentException("Number of bits per request limited to " + BLOCK128_MAX_BITS_REQUEST, "output");
            }

            if (predictionResistant || mV == null)
            {
                mV = mEntropySource.GetEntropy();
                if (mV.Length != mEngine.GetBlockSize())
                    throw new InvalidOperationException("Insufficient entropy returned");
            }

            int m = outputLen / mR.Length;

            for (int i = 0; i < m; i++)
            {
                mEngine.ProcessBlock(mDT, mI);
                Process(mR, mI, mV);
                Process(mV, mR, mI);

                mR.CopyTo(output[(i * mR.Length)..]);

                Increment(mDT);
            }

            int bytesToCopy = outputLen - m * mR.Length;

            if (bytesToCopy > 0)
            {
                mEngine.ProcessBlock(mDT, mI);
                Process(mR, mI, mV);
                Process(mV, mR, mI);

                mR.AsSpan(0, bytesToCopy).CopyTo(output[(m * mR.Length)..]);

                Increment(mDT);
            }

            mReseedCounter++;

            return outputLen * 8;
        }
#else
		/**
		 * Populate a passed in array with random data.
		 *
		 * @param output output array for generated bits.
		 * @param predictionResistant true if a reseed should be forced, false otherwise.
		 *
		 * @return number of bits generated, -1 if a reseed required.
		 */
		internal int Generate(byte[] output, int outputOff, int outputLen, bool predictionResistant)
		{
			if (mR.Length == 8) // 64 bit block size
			{
				if (mReseedCounter > BLOCK64_RESEED_MAX)
				{
					return -1;
				}

				if (outputLen > BLOCK64_MAX_BITS_REQUEST / 8)
				{
					throw new ArgumentException("Number of bits per request limited to " + BLOCK64_MAX_BITS_REQUEST, "output");
				}
			}
			else
			{
				if (mReseedCounter > BLOCK128_RESEED_MAX)
				{
					return -1;
				}

				if (outputLen > BLOCK128_MAX_BITS_REQUEST / 8)
				{
					throw new ArgumentException("Number of bits per request limited to " + BLOCK128_MAX_BITS_REQUEST, "output");
				}
			}

			if (predictionResistant || mV == null)
			{
				mV = mEntropySource.GetEntropy();
				if (mV.Length != mEngine.GetBlockSize())
				{
					throw new InvalidOperationException("Insufficient entropy returned");
				}
			}

			int m = outputLen / mR.Length;

			for (int i = 0; i < m; i++)
			{
				mEngine.ProcessBlock(mDT, 0, mI, 0);
				Process(mR, mI, mV);
				Process(mV, mR, mI);

				Array.Copy(mR, 0, output, outputOff + i * mR.Length, mR.Length);

				Increment(mDT);
			}

			int bytesToCopy = outputLen - m * mR.Length;

			if (bytesToCopy > 0)
			{
				mEngine.ProcessBlock(mDT, 0, mI, 0);
				Process(mR, mI, mV);
				Process(mV, mR, mI);

				Array.Copy(mR, 0, output, outputOff + m * mR.Length, bytesToCopy);

				Increment(mDT);
			}

			mReseedCounter++;

			return outputLen * 8;
		}
#endif

		/**
		 * Reseed the RNG.
		 */
		internal void Reseed()
		{
			mV = mEntropySource.GetEntropy();
			if (mV.Length != mEngine.GetBlockSize())
			{
				throw new InvalidOperationException("Insufficient entropy returned");
			}

			mReseedCounter = 1;
		}

		internal IEntropySource EntropySource
		{
			get { return mEntropySource; }
		}

		void Process(byte[] res, byte[] a, byte[] b)
		{
			for (int i = 0; i != res.Length; i++)
			{
				res[i] = (byte)(a[i] ^ b[i]);
			}

			mEngine.ProcessBlock(res, 0, res, 0);
		}

		void Increment(byte[] val)
		{
			for (int i = val.Length - 1; i >= 0; i--)
			{
				if (++val[i] != 0)
				{
					break;
				}
			}
		}
	}
}
#pragma warning restore
#endif