#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Digests
{
	/// <summary>
	/// ParallelHash - a hash designed  to  support the efficient hashing of very long strings, by taking advantage,
	/// of the parallelism available in modern processors with an optional XOF mode.
	/// <para>
	/// From NIST Special Publication 800-185 - SHA-3 Derived Functions:cSHAKE, KMAC, TupleHash and ParallelHash
	/// </para>
	/// </summary>
	public class ParallelHash
		: IXof, IDigest
	{
		static readonly byte[] N_PARALLEL_HASH = Strings.ToByteArray("ParallelHash");

		readonly CShakeDigest cshake;
		readonly CShakeDigest compressor;
		readonly int bitLength;
		readonly int outputLength;
		readonly int B;
		readonly byte[] buffer;
		readonly byte[] compressorBuffer;

		bool firstOutput;
		int nCount;
		int bufOff;

		/**
		 * Base constructor.
		 *
		 * @param bitLength bit length of the underlying SHAKE function, 128 or 256.
		 * @param S the customization string - available for local use.
		 * @param B the blocksize (in bytes) for hashing.
		 */
		public ParallelHash(int bitLength, byte[] S, int B)
			: this(bitLength, S, B, bitLength * 2)
		{
		}

		public ParallelHash(int bitLength, byte[] S, int B, int outputSize)
		{
			cshake = new CShakeDigest(bitLength, N_PARALLEL_HASH, S);
			compressor = new CShakeDigest(bitLength, new byte[0], new byte[0]);
			this.bitLength = bitLength;
			this.B = B;
			outputLength = (outputSize + 7) / 8;
			buffer = new byte[B];
			compressorBuffer = new byte[bitLength * 2 / 8];

			Reset();
		}

		public ParallelHash(ParallelHash source)
		{
			cshake = new CShakeDigest(source.cshake);
			compressor = new CShakeDigest(source.compressor);
			bitLength = source.bitLength;
			B = source.B;
			outputLength = source.outputLength;
			buffer = Arrays.Clone(source.buffer);
			compressorBuffer = Arrays.Clone(source.compressorBuffer);
		}

		public virtual string AlgorithmName
		{
			get { return "ParallelHash" + cshake.AlgorithmName.Substring(6); }
		}

		public virtual int GetByteLength()
		{
			return cshake.GetByteLength();
		}

		public virtual int GetDigestSize()
		{
			return outputLength;
		}

		public virtual void Update(byte b)
		{
			buffer[bufOff++] = b;
			if (bufOff == buffer.Length)
			{
				Compress();
			}
		}

		public virtual void BlockUpdate(byte[] inBuf, int inOff, int len)
		{
			len = System.Math.Max(0, len);

			//
			// fill the current word
			//
			int i = 0;
			if (bufOff != 0)
			{
				while (i < len && bufOff != buffer.Length)
				{
					buffer[bufOff++] = inBuf[inOff + i++];
				}

				if (bufOff == buffer.Length)
				{
					Compress();
				}
			}

			if (i < len)
			{
				while (len - i >= B)
				{
					Compress(inBuf, inOff + i, B);
					i += B;
				}
			}

			while (i < len)
			{
				Update(inBuf[inOff + i++]);
			}
		}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
        public virtual void BlockUpdate(ReadOnlySpan<byte> input)
        {
            //
            // fill the current word
            //
            int i = 0;
            if (bufOff != 0)
            {
                while (i < input.Length && bufOff != buffer.Length)
                {
                    buffer[bufOff++] = input[i++];
                }

                if (bufOff == buffer.Length)
                {
                    Compress();
                }
            }

            if (i < input.Length)
            {
                while (input.Length - i >= B)
                {
                    Compress(input, i, B);
                    i += B;
                }
            }

            while (i < input.Length)
            {
                Update(input[i++]);
            }
        }
#endif

		void Compress()
		{
			Compress(buffer, 0, bufOff);
			bufOff = 0;
		}

		void Compress(byte[] buf, int offSet, int len)
		{
			compressor.BlockUpdate(buf, offSet, len);
			compressor.OutputFinal(compressorBuffer, 0, compressorBuffer.Length);

			cshake.BlockUpdate(compressorBuffer, 0, compressorBuffer.Length);

			nCount++;
		}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
        private void Compress(ReadOnlySpan<byte> input, int pos, int len)
        {
            compressor.BlockUpdate(input.Slice(pos, len));
            compressor.OutputFinal(compressorBuffer, 0, compressorBuffer.Length);

            cshake.BlockUpdate(compressorBuffer, 0, compressorBuffer.Length);

            nCount++;
        }
#endif

		void WrapUp(int outputSize)
		{
			if (bufOff != 0)
			{
				Compress();
			}

			byte[] nOut = XofUtilities.RightEncode(nCount);
			byte[] encOut = XofUtilities.RightEncode(outputSize * 8);

			cshake.BlockUpdate(nOut, 0, nOut.Length);
			cshake.BlockUpdate(encOut, 0, encOut.Length);

			firstOutput = false;
		}

		public virtual int DoFinal(byte[] outBuf, int outOff)
		{
			if (firstOutput)
			{
				WrapUp(outputLength);
			}

			int rv = cshake.DoFinal(outBuf, outOff);

			Reset();

			return rv;
		}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
        public virtual int DoFinal(Span<byte> output)
        {
            if (firstOutput)
            {
                WrapUp(outputLength);
            }

            int rv = cshake.DoFinal(output);

            Reset();

            return rv;
        }
#endif

		public virtual int OutputFinal(byte[] outBuf, int outOff, int outLen)
		{
			if (firstOutput)
			{
				WrapUp(outputLength);
			}

			int rv = cshake.OutputFinal(outBuf, outOff, outLen);

			Reset();

			return rv;
		}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
        public virtual int OutputFinal(Span<byte> output)
        {
            if (firstOutput)
            {
                WrapUp(outputLength);
            }

            int rv = cshake.OutputFinal(output);

            Reset();

            return rv;
        }
#endif

		public virtual int Output(byte[] outBuf, int outOff, int outLen)
		{
			if (firstOutput)
			{
				WrapUp(0);
			}

			return cshake.Output(outBuf, outOff, outLen);
		}

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER || _UNITY_2021_2_OR_NEWER_
        public virtual int Output(Span<byte> output)
        {
            if (firstOutput)
            {
                WrapUp(0);
            }

            return cshake.Output(output);
        }
#endif

		public virtual void Reset()
		{
			cshake.Reset();
			Arrays.Clear(buffer);

			byte[] hdr = XofUtilities.LeftEncode(B);
			cshake.BlockUpdate(hdr, 0, hdr.Length);

			nCount = 0;
			bufOff = 0;
			firstOutput = true;
		}
	}
}
#pragma warning restore
#endif