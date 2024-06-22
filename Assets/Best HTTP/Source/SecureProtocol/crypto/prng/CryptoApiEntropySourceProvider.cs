#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.Security.Cryptography;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Prng
{
	public class CryptoApiEntropySourceProvider
		: IEntropySourceProvider
	{
		readonly RandomNumberGenerator mRng;
		readonly bool mPredictionResistant;

		public CryptoApiEntropySourceProvider()
			: this(RandomNumberGenerator.Create(), true)
		{
		}

		public CryptoApiEntropySourceProvider(RandomNumberGenerator rng, bool isPredictionResistant)
		{
			if (rng == null)
			{
				throw new ArgumentNullException("rng");
			}

			mRng = rng;
			mPredictionResistant = isPredictionResistant;
		}

		public IEntropySource Get(int bitsRequired)
		{
			return new CryptoApiEntropySource(mRng, mPredictionResistant, bitsRequired);
		}

		class CryptoApiEntropySource
			: IEntropySource
		{
			readonly RandomNumberGenerator mRng;
			readonly bool mPredictionResistant;
			readonly int mEntropySize;

			internal CryptoApiEntropySource(RandomNumberGenerator rng, bool predictionResistant, int entropySize)
			{
				mRng = rng;
				mPredictionResistant = predictionResistant;
				mEntropySize = entropySize;
			}

			#region IEntropySource Members

			bool IEntropySource.IsPredictionResistant
			{
				get { return mPredictionResistant; }
			}

			byte[] IEntropySource.GetEntropy()
			{
				byte[] result = new byte[(mEntropySize + 7) / 8];
				mRng.GetBytes(result);
				return result;
			}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
            int IEntropySource.GetEntropy(Span<byte> output)
            {
                int length = (mEntropySize + 7) / 8;
                mRng.GetBytes(output[..length]);
                return length;
            }
#endif

			int IEntropySource.EntropySize
			{
				get { return mEntropySize; }
			}

			#endregion
		}
	}
}
#pragma warning restore
#endif