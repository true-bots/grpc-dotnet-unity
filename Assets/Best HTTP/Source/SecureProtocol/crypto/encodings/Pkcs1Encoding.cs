#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Digests;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Security;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Encodings
{
	/**
	* this does your basic Pkcs 1 v1.5 padding - whether or not you should be using this
	* depends on your application - see Pkcs1 Version 2 for details.
	*/
	public class Pkcs1Encoding
		: IAsymmetricBlockCipher
	{
		/**
		 * some providers fail to include the leading zero in PKCS1 encoded blocks. If you need to
		 * work with one of these set the system property BestHTTP.SecureProtocol.Org.BouncyCastle.Pkcs1.Strict to false.
		 */
		public const string StrictLengthEnabledProperty = "BestHTTP.SecureProtocol.Org.BouncyCastle.Pkcs1.Strict";

		const int HeaderLength = 10;

		/**
		 * The same effect can be achieved by setting the static property directly
		 * <p>
		 * The static property is checked during construction of the encoding object, it is set to
		 * true by default.
		 * </p>
		 */
		public static bool StrictLengthEnabled
		{
			get { return strictLengthEnabled[0]; }
			set { strictLengthEnabled[0] = value; }
		}

		static readonly bool[] strictLengthEnabled;

		static Pkcs1Encoding()
		{
			string strictProperty = Platform.GetEnvironmentVariable(StrictLengthEnabledProperty);

			strictLengthEnabled = new bool[] { strictProperty == null || Platform.EqualsIgnoreCase("true", strictProperty) };
		}


		SecureRandom random;
		IAsymmetricBlockCipher engine;
		bool forEncryption;
		bool forPrivateKey;
		bool useStrictLength;
		int pLen = -1;
		byte[] fallback = null;
		byte[] blockBuffer = null;

		/**
		 * Basic constructor.
		 *
		 * @param cipher
		 */
		public Pkcs1Encoding(
			IAsymmetricBlockCipher cipher)
		{
			engine = cipher;
			useStrictLength = StrictLengthEnabled;
		}

		/**
		 * Constructor for decryption with a fixed plaintext length.
		 *
		 * @param cipher The cipher to use for cryptographic operation.
		 * @param pLen Length of the expected plaintext.
		 */
		public Pkcs1Encoding(IAsymmetricBlockCipher cipher, int pLen)
		{
			engine = cipher;
			useStrictLength = StrictLengthEnabled;
			this.pLen = pLen;
		}

		/**
		 * Constructor for decryption with a fixed plaintext length and a fallback
		 * value that is returned, if the padding is incorrect.
		 *
		 * @param cipher
		 *            The cipher to use for cryptographic operation.
		 * @param fallback
		 *            The fallback value, we don't to a arraycopy here.
		 */
		public Pkcs1Encoding(IAsymmetricBlockCipher cipher, byte[] fallback)
		{
			engine = cipher;
			useStrictLength = StrictLengthEnabled;
			this.fallback = fallback;
			pLen = fallback.Length;
		}

		public string AlgorithmName
		{
			get { return engine.AlgorithmName + "/PKCS1Padding"; }
		}

		public IAsymmetricBlockCipher UnderlyingCipher
		{
			get { return engine; }
		}

		public void Init(bool forEncryption, ICipherParameters parameters)
		{
			AsymmetricKeyParameter kParam;
			if (parameters is ParametersWithRandom withRandom)
			{
				random = withRandom.Random;
				kParam = (AsymmetricKeyParameter)withRandom.Parameters;
			}
			else
			{
				random = CryptoServicesRegistrar.GetSecureRandom();
				kParam = (AsymmetricKeyParameter)parameters;
			}

			engine.Init(forEncryption, parameters);

			forPrivateKey = kParam.IsPrivate;
			this.forEncryption = forEncryption;
			blockBuffer = new byte[engine.GetOutputBlockSize()];

			if (pLen > 0 && fallback == null && random == null)
			{
				throw new ArgumentException("encoder requires random");
			}
		}

		public int GetInputBlockSize()
		{
			int baseBlockSize = engine.GetInputBlockSize();

			return forEncryption
				? baseBlockSize - HeaderLength
				: baseBlockSize;
		}

		public int GetOutputBlockSize()
		{
			int baseBlockSize = engine.GetOutputBlockSize();

			return forEncryption
				? baseBlockSize
				: baseBlockSize - HeaderLength;
		}

		public byte[] ProcessBlock(
			byte[] input,
			int inOff,
			int length)
		{
			return forEncryption
				? EncodeBlock(input, inOff, length)
				: DecodeBlock(input, inOff, length);
		}

		byte[] EncodeBlock(
			byte[] input,
			int inOff,
			int inLen)
		{
			if (inLen > GetInputBlockSize())
			{
				throw new ArgumentException("input data too large", "inLen");
			}

			byte[] block = new byte[engine.GetInputBlockSize()];

			if (forPrivateKey)
			{
				block[0] = 0x01; // type code 1

				for (int i = 1; i != block.Length - inLen - 1; i++)
				{
					block[i] = (byte)0xFF;
				}
			}
			else
			{
				random.NextBytes(block); // random fill

				block[0] = 0x02; // type code 2

				//
				// a zero byte marks the end of the padding, so all
				// the pad bytes must be non-zero.
				//
				for (int i = 1; i != block.Length - inLen - 1; i++)
				{
					while (block[i] == 0)
					{
						block[i] = (byte)random.NextInt();
					}
				}
			}

			block[block.Length - inLen - 1] = 0x00; // mark the end of the padding
			Array.Copy(input, inOff, block, block.Length - inLen, inLen);

			return engine.ProcessBlock(block, 0, block.Length);
		}

		/**
		 * Checks if the argument is a correctly PKCS#1.5 encoded Plaintext
		 * for encryption.
		 *
		 * @param encoded The Plaintext.
		 * @param pLen Expected length of the plaintext.
		 * @return Either 0, if the encoding is correct, or -1, if it is incorrect.
		 */
		static int CheckPkcs1Encoding(byte[] encoded, int pLen)
		{
			int correct = 0;
			/*
			 * Check if the first two bytes are 0 2
			 */
			correct |= encoded[0] ^ 2;

			/*
			 * Now the padding check, check for no 0 byte in the padding
			 */
			int plen = encoded.Length - (
				pLen /* Length of the PMS */
				+ 1 /* Final 0-byte before PMS */
			);

			for (int i = 1; i < plen; i++)
			{
				int tmp = encoded[i];
				tmp |= tmp >> 1;
				tmp |= tmp >> 2;
				tmp |= tmp >> 4;
				correct |= (tmp & 1) - 1;
			}

			/*
			 * Make sure the padding ends with a 0 byte.
			 */
			correct |= encoded[encoded.Length - (pLen + 1)];

			/*
			 * Return 0 or 1, depending on the result.
			 */
			correct |= correct >> 1;
			correct |= correct >> 2;
			correct |= correct >> 4;
			return ~((correct & 1) - 1);
		}

		/**
		 * Decode PKCS#1.5 encoding, and return a random value if the padding is not correct.
		 *
		 * @param in The encrypted block.
		 * @param inOff Offset in the encrypted block.
		 * @param inLen Length of the encrypted block.
		 * @param pLen Length of the desired output.
		 * @return The plaintext without padding, or a random value if the padding was incorrect.
		 * @throws InvalidCipherTextException
		 */
		byte[] DecodeBlockOrRandom(byte[] input, int inOff, int inLen)
		{
			if (!forPrivateKey)
			{
				throw new InvalidCipherTextException("sorry, this method is only for decryption, not for signing");
			}

			byte[] block = engine.ProcessBlock(input, inOff, inLen);
			byte[] random;
			if (fallback == null)
			{
				random = new byte[pLen];
				this.random.NextBytes(random);
			}
			else
			{
				random = fallback;
			}

			byte[] data = useStrictLength & (block.Length != engine.GetOutputBlockSize()) ? blockBuffer : block;

			/*
			 * Check the padding.
			 */
			int correct = CheckPkcs1Encoding(data, pLen);

			/*
			 * Now, to a constant time constant memory copy of the decrypted value
			 * or the random value, depending on the validity of the padding.
			 */
			byte[] result = new byte[pLen];
			for (int i = 0; i < pLen; i++)
			{
				result[i] = (byte)((data[i + (data.Length - pLen)] & ~correct) | (random[i] & correct));
			}

			Arrays.Fill(data, 0);

			return result;
		}

		/**
		* @exception InvalidCipherTextException if the decrypted block is not in Pkcs1 format.
		*/
		byte[] DecodeBlock(
			byte[] input,
			int inOff,
			int inLen)
		{
			/*
			 * If the length of the expected plaintext is known, we use a constant-time decryption.
			 * If the decryption fails, we return a random value.
			 */
			if (pLen != -1)
			{
				return DecodeBlockOrRandom(input, inOff, inLen);
			}

			byte[] block = engine.ProcessBlock(input, inOff, inLen);
			bool incorrectLength = useStrictLength & (block.Length != engine.GetOutputBlockSize());

			byte[] data;
			if (block.Length < GetOutputBlockSize())
			{
				data = blockBuffer;
			}
			else
			{
				data = block;
			}

			byte expectedType = (byte)(forPrivateKey ? 2 : 1);
			byte type = data[0];

			bool badType = type != expectedType;

			//
			// find and extract the message block.
			//
			int start = FindStart(type, data);

			start++; // data should start at the next byte

			if (badType | (start < HeaderLength))
			{
				Arrays.Fill(data, 0);
				throw new InvalidCipherTextException("block incorrect");
			}

			// if we get this far, it's likely to be a genuine encoding error
			if (incorrectLength)
			{
				Arrays.Fill(data, 0);
				throw new InvalidCipherTextException("block incorrect size");
			}

			byte[] result = new byte[data.Length - start];

			Array.Copy(data, start, result, 0, result.Length);

			return result;
		}

		int FindStart(byte type, byte[] block)
		{
			int start = -1;
			bool padErr = false;

			for (int i = 1; i != block.Length; i++)
			{
				byte pad = block[i];

				if ((pad == 0) & (start < 0))
				{
					start = i;
				}

				padErr |= (type == 1) & (start < 0) & (pad != (byte)0xff);
			}

			return padErr ? -1 : start;
		}
	}
}
#pragma warning restore
#endif